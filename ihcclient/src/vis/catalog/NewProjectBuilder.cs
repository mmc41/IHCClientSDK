#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Ihc.Projects
{
    /// <summary>
    /// Builds a fresh v4 <see cref="Project"/> from the catalog File→New templates, reproducing what IHC Visual
    /// does between File→New and the first save (spec ch. 09 §9.4.2 / ch. 10 §10.3): take the legacy
    /// <c>NewDoc.idf</c> skeleton (ten default rooms + fixed template ids), upgrade it to v4 (drop <c>helpid</c>,
    /// materialize <c>icon="_0x15"</c> on rooms because the v4 default is <c>_0x0</c>), seed the two built-in
    /// enums from <c>EnumeratorDefinitions.def</c> and append the empty <c>documentation_modules</c> — allocating
    /// ids 0x41–0x50 in document order off the template's <c>last_unique_id="_0x40"</c> — and stamp creation
    /// metadata. The result is canonicalized so it serializes byte-identically and re-loads structurally equal.
    /// </summary>
    internal static class NewProjectBuilder
    {
        public static Project Build(ICatalog catalog, ProjectDetails details, DateTimeOffset creationTime)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(details);

            ProjectElement skeleton = catalog.NewProjectSkeleton;
            var allocator = new IdAllocator(SeedFromSkeleton(skeleton));

            // Allocation order is document order: the two enum definitions (+ their values) first (0x41–0x4d),
            // then the documentation-module skeleton (0x4e–0x50) — matching the vendor's empty file.
            ProjectElement enumDefinitions = BuildEnumDefinitions(skeleton, catalog.BuiltInEnumerators, allocator);
            ProjectElement documentationModules = BuildDocumentationModules(allocator);
            ProjectElement groups = RequireChild(skeleton, "groups");

            string stamp = PackedStamp.FromDateTime(creationTime).ToToken();   // id1 == id2 at creation; Save re-stamps id2
            ProjectElement root = Node("utcs_project", id: null, new[]
                {
                    ("version_major", "4"),
                    ("version_minor", "0"),
                    ("id1", stamp),
                    ("id2", stamp),
                    ("last_unique_id", allocator.LastUniqueIdToken),
                },
                new[]
                {
                    BuildModified(creationTime),
                    Node("customer_info", null, NoAttrs, NoChildren),
                    BuildInstallerInfo(details),
                    BuildProjectInfo(details),
                    enumDefinitions,
                    groups,
                    documentationModules,
                });

            return new Project(Canonicalizer.Canonicalize(root, ProjectSchemaView.RegistryOnly));
        }

        private static long SeedFromSkeleton(ProjectElement skeleton)
        {
            string? lastUniqueId = skeleton.GetAttribute("last_unique_id");
            return lastUniqueId is not null
                && lastUniqueId.StartsWith("_0x", StringComparison.Ordinal)
                && long.TryParse(lastUniqueId.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long seed)
                ? seed
                : 0x40;
        }

        private static ProjectElement BuildEnumDefinitions(ProjectElement skeleton, ProjectElement template, IdAllocator allocator)
        {
            ProjectElement skeletonEnums = RequireChild(skeleton, "enum_definitions");
            string containerId = skeletonEnums.GetAttribute("id") ?? "_0x3046";
            string containerName = skeletonEnums.GetAttribute("name") ?? "Enumerator definitioner";

            var definitions = ImmutableArray.CreateBuilder<ProjectElement>();
            foreach (ProjectElement def in Children(template))
            {
                if (def.Tag != "enum_definition")
                {
                    continue;
                }
                string defId = allocator.Allocate(TypeCodeFor("enum_definition")).ToToken();
                var values = ImmutableArray.CreateBuilder<ProjectElement>();
                foreach (ProjectElement value in Children(def))
                {
                    if (value.Tag != "enum_value")
                    {
                        continue;
                    }
                    string valueId = allocator.Allocate(TypeCodeFor("enum_value")).ToToken();
                    values.Add(Node("enum_value", valueId, CopyAttrs(value, "typeid", "name", "index"), NoChildren));
                }
                definitions.Add(Node("enum_definition", defId, CopyAttrs(def, "typeid", "name", "note"), values));
            }
            return Node("enum_definitions", containerId, new[] { ("name", containerName) }, definitions);
        }

        private static ProjectElement BuildDocumentationModules(IdAllocator allocator)
        {
            string modulesId = allocator.Allocate(TypeCodeFor("documentation_modules")).ToToken();
            string inputsId = allocator.Allocate(TypeCodeFor("dataline_input_modules")).ToToken();
            string outputsId = allocator.Allocate(TypeCodeFor("dataline_output_modules")).ToToken();
            return Node("documentation_modules", modulesId, NoAttrs, new[]
            {
                Node("dataline_input_modules", inputsId, NoAttrs, NoChildren),
                Node("dataline_output_modules", outputsId, NoAttrs, NoChildren),
            });
        }

        private static ProjectElement BuildModified(DateTimeOffset moment) =>
            Node("modified", null, new[]
            {
                ("year", Dec(moment.Year)),
                ("month", Dec(moment.Month)),
                ("day", Dec(moment.Day)),
                ("hour", Dec(moment.Hour)),
                ("minute", Dec(moment.Minute)),
            }, NoChildren);

        private static ProjectElement BuildInstallerInfo(ProjectDetails details) =>
            Node("installer_info", null, new[]
            {
                ("name", details.InstallerName),
                ("country", details.InstallerCountry),
            }, NoChildren);

        private static ProjectElement BuildProjectInfo(ProjectDetails details) =>
            Node("project_info", null, new[] { ("programmer", details.Programmer) }, NoChildren);

        // --- small construction helpers (the final Canonicalize pass fixes order / omits defaults / drops unknowns) ---

        private static readonly (string, string)[] NoAttrs = Array.Empty<(string, string)>();
        private static readonly ProjectElement[] NoChildren = Array.Empty<ProjectElement>();

        private static ProjectElement Node(string tag, string? id, IEnumerable<(string Name, string Value)> attrs,
                                           IEnumerable<ProjectElement> children)
        {
            ElementId? parsedId = id is not null && ElementId.TryParse(id, out ElementId p) ? p : null;
            var bag = ImmutableArray.CreateBuilder<(string, string)>();
            if (id is not null)
            {
                bag.Add(("id", id));
            }
            bag.AddRange(attrs);
            return new ProjectElement(tag, parsedId, bag.ToImmutable(), children.ToImmutableArray());
        }

        private static IEnumerable<(string, string)> CopyAttrs(ProjectElement source, params string[] names)
        {
            foreach (string name in names)
            {
                string? value = source.GetAttribute(name);
                if (value is not null)
                {
                    yield return (name, value);
                }
            }
        }

        private static IEnumerable<ProjectElement> Children(ProjectElement element) =>
            element.Children.IsDefaultOrEmpty ? NoChildren : element.Children;

        private static ProjectElement RequireChild(ProjectElement parent, string tag) =>
            parent.FindChild(tag) ?? throw new InvalidOperationException(
                $"The File→New template is missing the required '{tag}' element.");

        private static int TypeCodeFor(string tag) => TypeCode.ForTag(tag)
            ?? throw new InvalidOperationException($"No type code registered for '{tag}'.");

        private static string Dec(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
