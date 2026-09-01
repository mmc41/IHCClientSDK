using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;
using Ihc.Vis.Catalog;
using TypeCode = Ihc.Vis.Schema.TypeCode;
namespace Ihc.Vis.Editing
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
        private static readonly Regex LeadingZeroToken = new(@"^_0x0+[0-9a-fA-F]+$", RegexOptions.Compiled);

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
            ProjectElement reassigned = Reassign(catalogBody, allocator, idMap, enumDefinitions, hoisted, view, isRoot: true);

            // Pass 2: rewrite IDREF attributes through the old→new map (schema-driven, never by attribute name).
            // No post-hoc "no idMap key remains" assertion is possible here: a freshly allocated token can
            // legitimately equal an unrelated pre-insert id (counters overlap catalog seed ranges), so the
            // guarantee lives structurally in Reassign (every id maps or throws) + RemapIdRefs (every mapped
            // IdRef rewrites).
            ProjectElement remapped = RemapIdRefs(reassigned, idMap, view);

            // Pass 3: reconcile catalog numeric precision with the project's (e.g. a light's "500.00" → "500").
            ProjectElement normalized = NormalizeNumerics(remapped, view);

            // Pass 4: canonicalize opaque _0x hex tokens (strip leading zeros, e.g. an airlink template's
            // device_type "_0x080a" → "_0x80a") — done before Canonicalize so DTD-default comparison sees the
            // canonical form.
            ProjectElement tokenized = NormalizeTokens(normalized);

            // Pass 5: canonicalize enumerated tokens a template authored with a punctuation variant (e.g. an s0
            // kWh's accessibility typo "readwrite" → the DTD token "read-write") — again before Canonicalize so the
            // now-canonical value can match the project default and drop out.
            ProjectElement enumsCanonical = NormalizeEnums(tokenized, view);

            // Cross-DTD default materialization + drop editor-only attributes (helpid/access/…) + ATTLIST order.
            ProjectElement inserted = Canonicalizer.Canonicalize(enumsCanonical, view, UndeclaredAttributePolicy.Drop);

            ProjectElement updatedEnums = hoisted.Count == 0
                ? enumDefinitions
                : enumDefinitions with { Children = Concat(enumDefinitions.Children, hoisted) };

            return new InsertResult(inserted, updatedEnums);
        }

        private static ProjectElement Reassign(ProjectElement element, IdAllocator allocator,
            Dictionary<string, string> idMap, ProjectElement enumDefinitions, List<ProjectElement> hoisted,
            ProjectSchemaView view, bool isRoot)
        {
            string? oldId = element.GetAttribute("id");
            ElementId? newId = element.Id;
            EquatableArray<(string Name, string Value)> attrs = element.Attrs;

            if (oldId is not null)
            {
                // Every id-bearing element gets a fresh id — including open-world tags the registry does not
                // know, whose type-code suffix is recovered from the source token (keeping the source id
                // verbatim would mint a duplicate id on copy/insert, spec ch. 02 §2.2).
                int code = TypeCode.ForTag(element.Tag)
                    ?? (ElementId.TryParse(oldId, out ElementId parsed)
                        ? parsed.TypeCode
                        : throw new InvalidOperationException(
                            $"Cannot insert/copy element <{element.Tag}>: no type code is registered for the tag " +
                            $"and its id '{oldId}' is not a parseable _0x token, so a fresh id cannot be allocated."));
                ElementId allocated = allocator.Allocate(code);
                idMap[oldId] = allocated.ToToken();
                newId = allocated;
                attrs = ProjectElement.SetAttribute(attrs, "id", allocated.ToToken());
            }

            if (isRoot)
            {
                attrs = StripMenuPrefixFromName(attrs);
            }

            if (ResourceMaterialization.Icon(element.Tag) is { } canonicalIcon)
            {
                attrs = ProjectElement.SetAttribute(attrs, "icon", canonicalIcon);   // vendor stamps the per-resource-type GUI icon on insert
            }

            attrs = StampRequiredNullTokens(attrs, element.Tag, view);   // #REQUIRED-yet-empty → null token "_0x0"

            var children = ImmutableArray.CreateBuilder<ProjectElement>();
            foreach (ProjectElement child in element.Children)
            {
                if (child.Tag == "enum_definition")
                {
                    HoistOrResolveEnum(child, allocator, idMap, enumDefinitions, hoisted);  // not added to subtree
                }
                else
                {
                    children.Add(Reassign(child, allocator, idMap, enumDefinitions, hoisted, view, isRoot: false));
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
            bool byTypeid = typeid is not null && typeid != ElementId.NullToken;
            string? name = stub.GetAttribute("name");

            bool KeyMatches(ProjectElement def) => def.Tag == "enum_definition"
                && (byTypeid ? def.GetAttribute("typeid") == typeid : name is not null && def.GetAttribute("name") == name);

            foreach (ProjectElement candidate in enumDefinitions.Children.Concat(hoisted))
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
            foreach (ProjectElement value in stub.Children)
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
            allocator.Allocate(TypeCode.RequireForTag("enum_definition"));   // burn the def id (discarded — not emitted)
            string? stubId = stub.GetAttribute("id");
            if (stubId is not null && existing.GetAttribute("id") is { } existingId)
            {
                idMap[stubId] = existingId;
            }
            foreach (ProjectElement value in stub.Children)
            {
                if (value.Tag != "enum_value")
                {
                    continue;
                }
                allocator.Allocate(TypeCode.RequireForTag("enum_value"));    // burn each value id (discarded)
                string? stubValueId = value.GetAttribute("id");
                if (stubValueId is not null && MatchValue(existing, value)?.GetAttribute("id") is { } matchId)
                {
                    idMap[stubValueId] = matchId;
                }
            }
        }

        /// <summary>
        /// Hoists a fresh copy of the stub (def + values) with allocated ids, appended to <paramref name="hoisted"/>.
        /// Also reused by <see cref="ProjectEditor.NormalizeCatalogEnums"/> to reproduce the vendor's load-time
        /// re-hoist of the built-in catalog enums (same allocation order: def then values, document order).
        /// </summary>
        internal static void HoistFresh(ProjectElement stub, IdAllocator allocator,
            Dictionary<string, string> idMap, List<ProjectElement> hoisted)
        {
            string? stubId = stub.GetAttribute("id");
            ElementId defId = allocator.Allocate(TypeCode.RequireForTag("enum_definition"));
            if (stubId is not null)
            {
                idMap[stubId] = defId.ToToken();
            }

            var values = ImmutableArray.CreateBuilder<ProjectElement>();
            foreach (ProjectElement value in stub.Children)
            {
                if (value.Tag != "enum_value")
                {
                    continue;
                }
                string? oldValueId = value.GetAttribute("id");
                ElementId valueId = allocator.Allocate(TypeCode.RequireForTag("enum_value"));
                if (oldValueId is not null)
                {
                    idMap[oldValueId] = valueId.ToToken();
                }
                values.Add(value with { Id = valueId, Attrs = ProjectElement.SetAttribute(value.Attrs, "id", valueId.ToToken()) });
            }
            hoisted.Add(stub with { Id = defId, Attrs = ProjectElement.SetAttribute(stub.Attrs, "id", defId.ToToken()), Children = values.ToImmutable() });
        }

        /// <summary>Finds the value inside <paramref name="existingDef"/> that the stub value maps to: by typeid when present, else by name.</summary>
        private static ProjectElement? MatchValue(ProjectElement existingDef, ProjectElement value)
        {
            string? typeid = value.GetAttribute("typeid");
            if (typeid is not null && typeid != ElementId.NullToken)
            {
                return FindValueBy(existingDef, "typeid", typeid);
            }
            string? name = value.GetAttribute("name");
            return name is null ? null : FindValueBy(existingDef, "name", name);
        }

        // The one attribute-rewrite tree walk the four insert-normalization passes share: applies the rule
        // (schema, name, value → rewritten value, or null to keep) to every attribute of every element in the
        // subtree, pre-order. The per-pass rules stay tiny and fidelity-critical; the traversal lives once.
        private static ProjectElement RewriteAttributes(ProjectElement element, ProjectSchemaView? view,
            Func<ElementSchema?, string, string, string?> rule)
        {
            ElementSchema? schema = view?.TryGet(element.Tag);
            ImmutableArray<(string Name, string Value)> attrs = element.Attrs.AsImmutableArray();
            bool anyAttrRewritten = false;
            for (int i = 0; i < attrs.Length; i++)
            {
                if (rule(schema, attrs[i].Name, attrs[i].Value) is { } rewritten
                    && !string.Equals(rewritten, attrs[i].Value, StringComparison.Ordinal))
                {
                    attrs = attrs.SetItem(i, (attrs[i].Name, rewritten));
                    anyAttrRewritten = true;
                }
            }

            // The Canonicalizer's P3 sharing rule, applied here too: an unchanged child returns ITSELF, so tracking
            // reference equality lets an untouched subtree stay shared rather than be deep-copied. Without it each of
            // the four normalization passes rebuilt its whole subtree — and NormalizeOnOpen runs one over the entire
            // project, discarding on open exactly the structural sharing the commit path works to keep.
            ImmutableArray<ProjectElement> sourceChildren = element.Children.AsImmutableArray();
            bool anyChildRewritten = false;
            var childBuilder = ImmutableArray.CreateBuilder<ProjectElement>(sourceChildren.Length);
            foreach (ProjectElement child in sourceChildren)
            {
                ProjectElement rewrittenChild = RewriteAttributes(child, view, rule);
                anyChildRewritten |= !ReferenceEquals(rewrittenChild, child);
                childBuilder.Add(rewrittenChild);
            }

            return anyAttrRewritten || anyChildRewritten
                ? element with { Attrs = attrs, Children = childBuilder.MoveToImmutable() }
                : element;
        }

        /// <summary>
        /// Rewrites every schema-declared IDREF attribute through the old→new <paramref name="idMap"/> (never by
        /// attribute name), recursing into children. Reused by <see cref="ProjectEditor.NormalizeCatalogEnums"/> to
        /// repoint <c>resource_enum</c> references at the re-hoisted catalog enums.
        /// </summary>
        internal static ProjectElement RemapIdRefs(ProjectElement element, Dictionary<string, string> idMap, ProjectSchemaView view) =>
            RewriteAttributes(element, view, (schema, name, value) =>
                schema is not null && schema.IsIdRef(name) && idMap.TryGetValue(value, out string? mapped)
                    ? mapped : null);

        /// <summary>
        /// Reconciles a freshly-inserted subtree's numeric attribute precision with the project's: for each attribute
        /// whose project DTD default is a fixed-point decimal, the value is re-emitted with that default's number of
        /// decimal places (how IHC Visual reconciles a catalog template against the project on insert, spec ch. 09 —
        /// e.g. a light whose catalog inivalue default is <c>"500.00"</c> becomes <c>"500"</c> against the project
        /// default <c>"0"</c>, while a temperature's <c>"20.00"</c> is preserved against <c>"0.00"</c>). Applied only
        /// to the inserted subtree, so loaded elements keep their on-disk precision (round-trip fidelity).
        /// </summary>
        private static ProjectElement NormalizeNumerics(ProjectElement element, ProjectSchemaView view) =>
            RewriteAttributes(element, view, (schema, name, value) =>
                schema is not null && TryNormalizeToDefaultPrecision(schema, name, value, out string reformatted)
                    ? reformatted : null);

        /// <summary>
        /// Canonicalizes opaque <c>_0x</c> hex tokens in the inserted subtree by stripping leading zeros (e.g. an
        /// airlink template's <c>device_type="_0x080a"</c> → <c>"_0x80a"</c>), matching how IHC Visual re-emits every
        /// id/reference/device token in canonical minimal-width hex (the oracle carries no leading-zero token). Only
        /// verbatim-copied external tokens are affected — reassigned ids and remapped IDREFs are already canonical —
        /// and only in the inserted subtree, so loaded elements keep their on-disk form (round-trip fidelity),
        /// consistent with <see cref="NormalizeNumerics"/>.
        /// </summary>
        private static ProjectElement NormalizeTokens(ProjectElement element) =>
            RewriteAttributes(element, view: null, (_, _, value) =>
                LeadingZeroToken.IsMatch(value) ? StripLeadingZeros(value) : null);

        /// <summary>
        /// Canonicalizes an inserted subtree's enumerated attribute values to the exact DTD token when a template
        /// authored a punctuation variant (e.g. product2315.def writes a kWh's <c>accessibility</c> as the typo
        /// "readwrite" for the DTD token "read-write"). IHC Visual re-emits the canonical token, so — combined with
        /// Canonicalize's default-elision — the value then matches the project default and drops out. Only the
        /// inserted subtree is touched, so loaded elements keep their on-disk spelling (round-trip fidelity).
        /// </summary>
        private static ProjectElement NormalizeEnums(ProjectElement element, ProjectSchemaView view) =>
            RewriteAttributes(element, view, (schema, name, value) =>
                schema is not null && TryCanonicalizeEnum(schema, name, value, out string canonical)
                    ? canonical : null);

        /// <summary>Maps a value that is not an exact enum token to the token it matches ignoring hyphens ("readwrite" → "read-write").</summary>
        private static bool TryCanonicalizeEnum(ElementSchema schema, string attrName, string value, out string canonical)
        {
            canonical = value;
            AttrSchema? attr = schema.FindAttr(attrName);
            if (attr is null || attr.EnumValues.IsEmpty || attr.EnumValues.Contains(value))
            {
                return false;   // undeclared, not an enumerated attribute, or already an exact token
            }
            foreach (string token in attr.EnumValues)
            {
                if (token.Replace("-", string.Empty) == value.Replace("-", string.Empty))
                {
                    canonical = token;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Strips leading zeros after the <c>_0x</c> prefix, keeping at least one hex digit (<c>_0x080a</c> → <c>_0x80a</c>).</summary>
        private static string StripLeadingZeros(string token)
        {
            int i = 3;   // past "_0x"
            while (i < token.Length - 1 && token[i] == '0')
            {
                i++;
            }
            return "_0x" + token.Substring(i);
        }

        private static bool TryNormalizeToDefaultPrecision(ElementSchema schema, string attrName, string value, out string reformatted)
        {
            reformatted = value;
            const NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            AttrSchema? attr = schema.FindAttr(attrName);
            if (attr is null || attr.Kind != AttrKind.Defaulted
                || !TryFixedPointPlaces(attr.Default, out int places)
                || !decimal.TryParse(value, style, CultureInfo.InvariantCulture, out decimal number))
            {
                return false;   // undeclared, not a fixed-point numeric attribute, or a non-numeric value — leave verbatim
            }
            // Only reformat when it PRESERVES the value: "F"+places pads/trims trailing zeros ("500.00"→"500";
            // decimal equality ignores scale) but ALSO rounds ("12.5", places 0 → "13"). Skip the rounding case so
            // an authored inivalue is never silently mutated on insert. (places is 0..2 across the corpus; the
            // <=28 guard keeps decimal.Round's argument in range for any pathological default.)
            if (places <= 28 && decimal.Round(number, places) != number)
            {
                return false;
            }
            reformatted = number.ToString("F" + DecToken.Format(places), CultureInfo.InvariantCulture);
            return reformatted != value;
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

        private static ProjectElement? FindValueBy(ProjectElement def, string attrName, string wanted)
        {
            foreach (ProjectElement value in def.Children)
            {
                if (value.Tag == "enum_value" && value.GetAttribute(attrName) == wanted)
                {
                    return value;
                }
            }
            return null;
        }

        /// <summary>
        /// Materializes the vendor's null token <c>"_0x0"</c> on every attribute the <em>project</em> schema declares
        /// <c>#REQUIRED</c> that the catalog <c>.def</c> left empty or absent — i.e. the <c>.def</c>'s own DTD defaulted
        /// it to <c>""</c> while the project DTD requires it (spec ch. 09 §9.3.7 cross-DTD reconciliation). Observed on
        /// an airlink product's <c>serialnumber</c> and an RS-485 LED-dimmer channel's <c>channel_id</c>; a <c>#REQUIRED</c>
        /// attribute the <c>.def</c> actually fills (a product_identifier, device_type, an rs485 <c>channel</c> number,
        /// a date's <c>year/month/day</c>) is non-empty and untouched. Schema-derived (against <paramref name="view"/>,
        /// so it also covers a custom component resolved from the file's own inline DTD) rather than a per-type table;
        /// the element's own <c>id</c> is excluded — it is already allocated.
        /// </summary>
        private static EquatableArray<(string Name, string Value)> StampRequiredNullTokens(
            EquatableArray<(string Name, string Value)> attrs, string tag, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(tag);
            if (schema is null)
            {
                return attrs;
            }
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Kind == AttrKind.Required && attr.Render != AttrRender.Id
                    && string.IsNullOrEmpty(ProjectElement.GetAttribute(attrs, attr.Name)))
                {
                    attrs = ProjectElement.SetAttribute(attrs, attr.Name, ElementId.NullToken);
                }
            }
            return attrs;
        }

        private static EquatableArray<(string Name, string Value)> StripMenuPrefixFromName(
            EquatableArray<(string Name, string Value)> attrs)
        {
            // Absent name → nothing to strip; SetAttribute would APPEND one, so the null check is not just a fast path.
            if (ProjectElement.GetAttribute(attrs, "name") is not { } name)
            {
                return attrs;
            }
            string stripped = MenuPrefix.Strip(name);
            return stripped == name ? attrs : ProjectElement.SetAttribute(attrs, "name", stripped);
        }

        private static EquatableArray<ProjectElement> Concat(EquatableArray<ProjectElement> existing, List<ProjectElement> added) =>
            existing.AsImmutableArray().AddRange(added);
    }
}
