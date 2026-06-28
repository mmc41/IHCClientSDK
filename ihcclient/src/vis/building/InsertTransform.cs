#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    /// <c>enum_definition</c> children to the project-level container (reusing an existing one with the same
    /// <c>typeid</c>, else appending a fresh copy) and rewrite the references to them; strip the <c>NN#</c> menu
    /// prefix from the root name; and materialize cross-DTD default differences (done by canonicalizing the
    /// effective catalog values against the project schema, which also drops editor-only attributes like
    /// <c>helpid</c>).
    /// </summary>
    internal static class InsertTransform
    {
        private static readonly Regex MenuPrefix = new(@"^\d+#", RegexOptions.Compiled);

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

            // Cross-DTD default materialization + drop editor-only attributes + ATTLIST order.
            ProjectElement inserted = Canonicalizer.Canonicalize(remapped, view);

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
            string? typeid = stub.GetAttribute("typeid");
            ProjectElement? existing = typeid is not null && typeid != "_0x0"
                ? FindEnumByTypeid(enumDefinitions, typeid)
                : null;

            if (existing is not null)
            {
                MapStubToExisting(stub, existing, idMap);   // built-in enum: reuse the project copy, no new ids
                return;
            }

            // User enum (no typeid match): hoist a fresh copy with allocated ids.
            string? stubId = stub.GetAttribute("id");
            ElementId defId = allocator.Allocate(TypeCodeFor("enum_definition"));
            if (stubId is not null)
            {
                idMap[stubId] = defId.ToToken();
            }

            var values = ImmutableArray.CreateBuilder<ProjectElement>();
            if (!stub.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement value in stub.Children)
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
            }
            hoisted.Add(stub with { Id = defId, Attrs = SetAttribute(Attrs(stub), "id", defId.ToToken()), Children = values.ToImmutable() });
        }

        private static void MapStubToExisting(ProjectElement stub, ProjectElement existing, Dictionary<string, string> idMap)
        {
            string? stubId = stub.GetAttribute("id");
            string? existingId = existing.GetAttribute("id");
            if (stubId is not null && existingId is not null)
            {
                idMap[stubId] = existingId;
            }
            if (stub.Children.IsDefaultOrEmpty)
            {
                return;
            }
            foreach (ProjectElement value in stub.Children)
            {
                if (value.Tag != "enum_value")
                {
                    continue;
                }
                string? valueTypeid = value.GetAttribute("typeid");
                string? stubValueId = value.GetAttribute("id");
                ProjectElement? match = valueTypeid is not null ? FindValueByTypeid(existing, valueTypeid) : null;
                if (stubValueId is not null && match?.GetAttribute("id") is { } matchId)
                {
                    idMap[stubValueId] = matchId;
                }
            }
        }

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
