#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ihc.Projects
{
    /// <summary>
    /// The result of inserting a catalog component into a project: the deep-copied, re-id'd subtree to place under
    /// a group, plus the (possibly grown) project-level <c>enum_definitions</c> container after enum hoisting.
    /// </summary>
    internal readonly record struct InsertResult(ProjectElement InsertedRoot, ProjectElement EnumDefinitions);

    /// <summary>
    /// Transforms a catalog component body (a product <c>.def</c> or function-block <c>.ifb</c> root, parsed with
    /// its DTD defaults applied) into a project subtree, exactly as IHC Visual does on insert (spec ch. 09
    /// §9.2.6/§9.3.7): deep-copy the structure; allocate a fresh id for every element off the project counter
    /// keeping its type-code suffix; remap every internal IDREF through the same old→new map; hoist
    /// <c>enum_definition</c> children to the project-level container, but DEDUP any that duplicate an
    /// enum_definition already in the project (matched by <c>typeid</c> when present, else by name) — allocating
    /// then discarding the duplicate's def+value ids so the counter still advances (a permanent hole) and rewiring
    /// its references to the pre-existing def (R-enum, spec ch. 09 §9.3.7 / experiments B3); a fresh copy is appended
    /// only when the enum has no match, and the references rewritten to it; strip the <c>NN#</c> menu
    /// prefix from the root name; and materialize cross-DTD default differences (done by canonicalizing the
    /// effective catalog values against the project schema, which also drops editor-only attributes like
    /// <c>helpid</c>).
    /// </summary>
    internal static class InsertTransform
    {
        private static readonly Regex MenuPrefix = new(@"^\d+#", RegexOptions.Compiled);

        /// <summary>
        /// The per-resource-type GUI icon IHC Visual stamps on every resource when a component is inserted.
        /// Function-block <c>.ifb</c> templates bake these in (so they survive the deep-copy), but product
        /// <c>.def</c> templates omit them — their <c>icon</c> defaults to <c>_0x0</c> and IHC Visual assigns the
        /// canonical icon on insert — so we must stamp them here for byte-fidelity. The table is derived from every
        /// authentic oracle (each listed type maps to exactly one icon, verified conflict-free); resource types not
        /// listed carry no canonical icon (effective <c>_0x0</c>, elided on save).
        /// </summary>
        private static readonly Dictionary<string, string> ResourceIcons = new(StringComparer.Ordinal)
        {
            ["resource_enum"] = "_0x22",
            ["resource_input"] = "_0x36",
            ["resource_output"] = "_0x39",
            ["resource_timer"] = "_0x43",
            ["resource_flag"] = "_0x33",
            ["resource_time"] = "_0x2f",
            ["resource_date"] = "_0x29",
            ["resource_weekday"] = "_0x2c",
            ["resource_timertime"] = "_0x4d",
            ["resource_holiday"] = "_0x9b",
        };

        public static InsertResult Insert(ProjectElement catalogBody, IdAllocator allocator,
            ProjectElement enumDefinitions, ProjectSchemaView view)
        {
            ArgumentNullException.ThrowIfNull(catalogBody);
            ArgumentNullException.ThrowIfNull(allocator);
            ArgumentNullException.ThrowIfNull(enumDefinitions);
            ArgumentNullException.ThrowIfNull(view);

            var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var hoisted = new List<ProjectElement>();

            // Pass 1: document-order id allocation + enum hoist/resolve (populates idMap, strips enum children).
            ProjectElement reassigned = Reassign(catalogBody, allocator, idMap, enumDefinitions, hoisted, isRoot: true);

            // Pass 2: rewrite IDREF attributes through the old→new map (schema-driven, never by attribute name).
            ProjectElement remapped = RemapIdRefs(reassigned, idMap, view);

            // Pass 3: reconcile catalog numeric precision with the project's (e.g. a light's "500.00" → "500").
            ProjectElement normalized = NormalizeNumerics(remapped, view);

            // Cross-DTD default materialization + drop editor-only attributes + ATTLIST order.
            ProjectElement inserted = Canonicalizer.Canonicalize(normalized, view);

            ProjectElement updatedEnums = hoisted.Count == 0
                ? enumDefinitions
                : enumDefinitions with { Children = Concat(enumDefinitions.Children, hoisted) };

            return new InsertResult(inserted, updatedEnums);
        }

        private static ProjectElement Reassign(ProjectElement element, IdAllocator allocator,
            Dictionary<string, string> idMap, ProjectElement enumDefinitions, List<ProjectElement> hoisted, bool isRoot)
        {
            string? oldId = element.GetAttribute("id");
            int? typeCode = TypeCode.ForTag(element.Tag);
            ElementId? newId = element.Id;
            ImmutableArray<(string, string)> attrs = element.Attrs.IsDefaultOrEmpty
                ? ImmutableArray<(string, string)>.Empty
                : element.Attrs;

            if (oldId is not null && typeCode is { } code)
            {
                ElementId allocated = allocator.Allocate(code);
                idMap[oldId] = allocated.ToToken();
                newId = allocated;
                attrs = SetAttribute(attrs, "id", allocated.ToToken());
            }

            if (isRoot)
            {
                attrs = StripMenuPrefixFromName(attrs);
            }

            if (ResourceIcons.TryGetValue(element.Tag, out string? canonicalIcon))
            {
                attrs = SetAttribute(attrs, "icon", canonicalIcon);   // vendor stamps the per-resource-type GUI icon on insert
            }

            var children = ImmutableArray.CreateBuilder<ProjectElement>();
            if (!element.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement child in element.Children)
                {
                    if (child.Tag == "enum_definition")
                    {
                        HoistOrResolveEnum(child, allocator, idMap, enumDefinitions, hoisted);  // not added to subtree
                    }
                    else
                    {
                        children.Add(Reassign(child, allocator, idMap, enumDefinitions, hoisted, isRoot: false));
                    }
                }
            }

            return element with { Id = newId, Attrs = attrs, Children = children.ToImmutable() };
        }

        private static void HoistOrResolveEnum(ProjectElement stub, IdAllocator allocator,
            Dictionary<string, string> idMap, ProjectElement enumDefinitions, List<ProjectElement> hoisted)
        {
            ProjectElement? existing = FindMatchingEnum(stub, enumDefinitions, hoisted);

            if (existing is not null)
            {
                BurnAndMapToExisting(stub, existing, allocator, idMap);   // R-enum: allocate+discard ids, rewire to the existing def
                return;
            }

            HoistFresh(stub, allocator, idMap, hoisted);                  // no match: hoist a fresh copy with allocated ids
        }

        /// <summary>
        /// Finds the pre-existing <c>enum_definition</c> the stub duplicates, or null when it is new. A duplicate
        /// must (1) match the key — the stub's <c>typeid</c> when present and non-zero, else its name — AND (2) have
        /// every stub <c>enum_value</c> map onto one of its values (by typeid else name). Two enums that share only a
        /// name but carry different value sets (e.g. the seed "Persienne tilstand" vs the very different 3.1.01
        /// "Persienne tilstand") are NOT the same enum and must hoist fresh, else the rewired references dangle.
        /// Candidates are the project container's current children plus any enum hoisted earlier in this same insert.
        /// </summary>
        private static ProjectElement? FindMatchingEnum(ProjectElement stub, ProjectElement enumDefinitions, List<ProjectElement> hoisted)
        {
            string? typeid = stub.GetAttribute("typeid");
            bool byTypeid = typeid is not null && typeid != "_0x0";
            string? name = stub.GetAttribute("name");

            bool KeyMatches(ProjectElement def) => def.Tag == "enum_definition"
                && (byTypeid ? def.GetAttribute("typeid") == typeid : name is not null && def.GetAttribute("name") == name);

            foreach (ProjectElement candidate in Children(enumDefinitions).Concat(hoisted))
            {
                if (KeyMatches(candidate) && AllValuesMap(stub, candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>True when every <c>enum_value</c> in the stub resolves to a value in <paramref name="candidate"/> (by typeid else name).</summary>
        private static bool AllValuesMap(ProjectElement stub, ProjectElement candidate)
        {
            foreach (ProjectElement value in Children(stub))
            {
                if (value.Tag == "enum_value" && MatchValue(candidate, value) is null)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// R-enum dedup: the stub duplicates <paramref name="existing"/>, so IHC Visual allocates the stub's
        /// def+value ids in document order (advancing the counter, leaving a permanent hole) but discards them and
        /// rewires the references to the existing def. Value mapping: by <c>typeid</c> when the stub value carries
        /// one, else by name.
        /// </summary>
        private static void BurnAndMapToExisting(ProjectElement stub, ProjectElement existing,
            IdAllocator allocator, Dictionary<string, string> idMap)
        {
            allocator.Allocate(TypeCodeFor("enum_definition"));   // burn the def id (discarded — not emitted)
            string? stubId = stub.GetAttribute("id");
            if (stubId is not null && existing.GetAttribute("id") is { } existingId)
            {
                idMap[stubId] = existingId;
            }
            foreach (ProjectElement value in Children(stub))
            {
                if (value.Tag != "enum_value")
                {
                    continue;
                }
                allocator.Allocate(TypeCodeFor("enum_value"));    // burn each value id (discarded)
                string? stubValueId = value.GetAttribute("id");
                if (stubValueId is not null && MatchValue(existing, value)?.GetAttribute("id") is { } matchId)
                {
                    idMap[stubValueId] = matchId;
                }
            }
        }

        /// <summary>Hoists a fresh copy of the stub (def + values) with allocated ids, appended to the project container.</summary>
        private static void HoistFresh(ProjectElement stub, IdAllocator allocator,
            Dictionary<string, string> idMap, List<ProjectElement> hoisted)
        {
            string? stubId = stub.GetAttribute("id");
            ElementId defId = allocator.Allocate(TypeCodeFor("enum_definition"));
            if (stubId is not null)
            {
                idMap[stubId] = defId.ToToken();
            }

            var values = ImmutableArray.CreateBuilder<ProjectElement>();
            foreach (ProjectElement value in Children(stub))
            {
                if (value.Tag != "enum_value")
                {
                    continue;
                }
                string? oldValueId = value.GetAttribute("id");
                ElementId valueId = allocator.Allocate(TypeCodeFor("enum_value"));
                if (oldValueId is not null)
                {
                    idMap[oldValueId] = valueId.ToToken();
                }
                values.Add(value with { Id = valueId, Attrs = SetAttribute(Attrs(value), "id", valueId.ToToken()) });
            }
            hoisted.Add(stub with { Id = defId, Attrs = SetAttribute(Attrs(stub), "id", defId.ToToken()), Children = values.ToImmutable() });
        }

        /// <summary>Finds the value inside <paramref name="existingDef"/> that the stub value maps to: by typeid when present, else by name.</summary>
        private static ProjectElement? MatchValue(ProjectElement existingDef, ProjectElement value)
        {
            string? typeid = value.GetAttribute("typeid");
            if (typeid is not null && typeid != "_0x0")
            {
                return FindValueByTypeid(existingDef, typeid);
            }
            string? name = value.GetAttribute("name");
            return name is null ? null : FindValueByName(existingDef, name);
        }

        private static IEnumerable<ProjectElement> Children(ProjectElement element) =>
            element.Children.IsDefaultOrEmpty ? Enumerable.Empty<ProjectElement>() : element.Children;

        private static ProjectElement RemapIdRefs(ProjectElement element, Dictionary<string, string> idMap, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            ImmutableArray<(string Name, string Value)> attrs = Attrs(element);
            if (schema is not null && !attrs.IsDefaultOrEmpty)
            {
                for (int i = 0; i < attrs.Length; i++)
                {
                    if (IsIdRef(schema, attrs[i].Name) && idMap.TryGetValue(attrs[i].Value, out string? mapped))
                    {
                        attrs = attrs.SetItem(i, (attrs[i].Name, mapped));
                    }
                }
            }

            ImmutableArray<ProjectElement> children = element.Children.IsDefaultOrEmpty
                ? ImmutableArray<ProjectElement>.Empty
                : element.Children.Select(c => RemapIdRefs(c, idMap, view)).ToImmutableArray();

            return element with { Attrs = attrs, Children = children };
        }

        private static bool IsIdRef(ElementSchema schema, string attrName)
        {
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Name == attrName)
                {
                    return attr.Render == AttrRender.IdRef;
                }
            }
            return false;
        }

        /// <summary>
        /// Reconciles a freshly-inserted subtree's numeric attribute precision with the project's: for each attribute
        /// whose project DTD default is a fixed-point decimal, the value is re-emitted with that default's number of
        /// decimal places (how IHC Visual reconciles a catalog template against the project on insert, spec ch. 09 —
        /// e.g. a light whose catalog inivalue default is <c>"500.00"</c> becomes <c>"500"</c> against the project
        /// default <c>"0"</c>, while a temperature's <c>"20.00"</c> is preserved against <c>"0.00"</c>). Applied only
        /// to the inserted subtree, so loaded elements keep their on-disk precision (round-trip fidelity).
        /// </summary>
        private static ProjectElement NormalizeNumerics(ProjectElement element, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            ImmutableArray<(string Name, string Value)> attrs = Attrs(element);
            if (schema is not null && !attrs.IsDefaultOrEmpty)
            {
                for (int i = 0; i < attrs.Length; i++)
                {
                    if (TryNormalizeToDefaultPrecision(schema, attrs[i].Name, attrs[i].Value, out string reformatted))
                    {
                        attrs = attrs.SetItem(i, (attrs[i].Name, reformatted));
                    }
                }
            }

            ImmutableArray<ProjectElement> children = element.Children.IsDefaultOrEmpty
                ? ImmutableArray<ProjectElement>.Empty
                : element.Children.Select(c => NormalizeNumerics(c, view)).ToImmutableArray();

            return element with { Attrs = attrs, Children = children };
        }

        private static bool TryNormalizeToDefaultPrecision(ElementSchema schema, string attrName, string value, out string reformatted)
        {
            reformatted = value;
            const NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Name != attrName)
                {
                    continue;
                }
                if (attr.Kind != AttrKind.Defaulted
                    || !TryFixedPointPlaces(attr.Default, out int places)
                    || !decimal.TryParse(value, style, CultureInfo.InvariantCulture, out decimal number))
                {
                    return false;   // not a fixed-point numeric attribute, or a non-numeric value — leave verbatim
                }
                reformatted = number.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                return reformatted != value;
            }
            return false;
        }

        /// <summary>True when the DTD default is a plain fixed-point decimal; yields its number of decimal places.</summary>
        private static bool TryFixedPointPlaces(string dtdDefault, out int places)
        {
            places = 0;
            if (!decimal.TryParse(dtdDefault, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out _))
            {
                return false;   // non-numeric default (enum token, _0x id, text) — not a fixed-point attribute
            }
            int dot = dtdDefault.IndexOf('.');
            places = dot < 0 ? 0 : dtdDefault.Length - dot - 1;
            return true;
        }

        private static ProjectElement? FindEnumByTypeid(ProjectElement enumDefinitions, string typeid)
        {
            if (enumDefinitions.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            foreach (ProjectElement def in enumDefinitions.Children)
            {
                if (def.Tag == "enum_definition" && def.GetAttribute("typeid") == typeid)
                {
                    return def;
                }
            }
            return null;
        }

        private static ProjectElement? FindValueByTypeid(ProjectElement def, string typeid)
        {
            if (def.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            foreach (ProjectElement value in def.Children)
            {
                if (value.Tag == "enum_value" && value.GetAttribute("typeid") == typeid)
                {
                    return value;
                }
            }
            return null;
        }

        private static ProjectElement? FindValueByName(ProjectElement def, string name)
        {
            foreach (ProjectElement value in Children(def))
            {
                if (value.Tag == "enum_value" && value.GetAttribute("name") == name)
                {
                    return value;
                }
            }
            return null;
        }

        private static ImmutableArray<(string, string)> StripMenuPrefixFromName(ImmutableArray<(string Name, string Value)> attrs)
        {
            for (int i = 0; i < attrs.Length; i++)
            {
                if (attrs[i].Name == "name")
                {
                    string stripped = MenuPrefix.Replace(attrs[i].Value, string.Empty);
                    return stripped == attrs[i].Value ? attrs : attrs.SetItem(i, ("name", stripped));
                }
            }
            return attrs;
        }

        private static ImmutableArray<(string, string)> Attrs(ProjectElement element) =>
            element.Attrs.IsDefaultOrEmpty ? ImmutableArray<(string, string)>.Empty : element.Attrs;

        private static ImmutableArray<(string, string)> SetAttribute(ImmutableArray<(string Name, string Value)> attrs, string name, string value)
        {
            for (int i = 0; i < attrs.Length; i++)
            {
                if (attrs[i].Name == name)
                {
                    return attrs.SetItem(i, (name, value));
                }
            }
            return attrs.Add((name, value));
        }

        private static ImmutableArray<ProjectElement> Concat(ImmutableArray<ProjectElement> existing, List<ProjectElement> added)
        {
            ImmutableArray<ProjectElement> baseArray = existing.IsDefaultOrEmpty ? ImmutableArray<ProjectElement>.Empty : existing;
            return baseArray.AddRange(added);
        }

        private static int TypeCodeFor(string tag) => TypeCode.ForTag(tag)
            ?? throw new InvalidOperationException($"No type code registered for '{tag}'.");
    }
}
