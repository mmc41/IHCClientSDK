#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;
using TypeCode = Ihc.Vis.Schema.TypeCode;
namespace Ihc.Vis.Projects
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
        // localityNames: the names to give the template's default rooms, in document order — for an authoring
        // frontend that seeds rooms in its own language. Null (the default) keeps the template's own names, so the
        // vendor path stays byte-identical. Applied only when the count matches the template's room count exactly,
        // so a customised template is never rewritten; a room the template left unnamed keeps its blank name.
        public static Project Build(ICatalog catalog, ProjectDetails details, DateTimeOffset creationTime,
            SeedIdLayout seedLayout = SeedIdLayout.EnumsFirst, IReadOnlyList<string>? localityNames = null)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(details);

            ProjectElement skeleton = catalog.NewProjectSkeleton;
            var allocator = new IdAllocator(SeedFromSkeleton(skeleton));

            // The seed sub-order varies by IHC Visual build (anomaly A-1): EnumsFirst (Project0/Project1) allocates
            // the two built-in enums (+values) first (0x41–0x4d) then the documentation modules (0x4e–0x50);
            // ModulesFirst (project2) reverses the allocation. Document emission order is identical either way.
            ProjectElement enumDefinitions;
            ProjectElement documentationModules;
            if (seedLayout == SeedIdLayout.ModulesFirst)
            {
                documentationModules = BuildDocumentationModules(allocator);
                enumDefinitions = BuildEnumDefinitions(skeleton, catalog.BuiltInEnumerators, allocator);
            }
            else
            {
                enumDefinitions = BuildEnumDefinitions(skeleton, catalog.BuiltInEnumerators, allocator);
                documentationModules = BuildDocumentationModules(allocator);
            }
            ProjectElement groups = RenameLocalities(RequireChild(skeleton, "groups"), localityNames);

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
                    BuildCustomerInfo(details),
                    BuildInstallerInfo(details),
                    BuildProjectInfo(details),
                    enumDefinitions,
                    groups,
                    documentationModules,
                });

            return new Project(Canonicalizer.Canonicalize(root, ProjectSchemaView.RegistryOnly,
                                                          UndeclaredAttributePolicy.Drop));   // sheds the skeleton's legacy helpid
        }

        // Names the template's rooms as the caller asked, while the skeleton is being assembled — a name is an
        // attribute, so no id is allocated and the tree shape is untouched. Doing it here rather than re-opening the
        // finished project to repair it keeps the room names a property of the template the builder produces, and
        // addresses each room by POSITION rather than by its current name (two default rooms sharing a name would
        // make a name-keyed lookup ambiguous).
        private static ProjectElement RenameLocalities(ProjectElement groups, IReadOnlyList<string>? names)
        {
            ImmutableArray<ProjectElement> rooms = groups.Children.AsImmutableArray();
            if (names is null || names.Count != rooms.Count(c => c.Tag == "group"))
            {
                return groups;   // no request, or a customised template — leave it exactly as the catalog wrote it
            }
            var renamed = ImmutableArray.CreateBuilder<ProjectElement>(rooms.Length);
            int position = 0;
            foreach (ProjectElement room in rooms)
            {
                if (room.Tag != "group")
                {
                    renamed.Add(room);
                    continue;
                }
                // An unnamed element is not a default room — renaming it would invent a name the template withheld.
                renamed.Add(string.IsNullOrEmpty(room.GetAttribute("name"))
                    ? room
                    : room.WithAttribute("name", names[position]));
                position++;
            }
            return groups with { Children = renamed.MoveToImmutable() };
        }

        private static long SeedFromSkeleton(ProjectElement skeleton) =>
            // The same high-water-mark rule IdAllocator.ForProject applies to loaded projects: never trust the
            // template's last_unique_id alone — an edited/hand-built skeleton whose ids exceed it would make
            // the very first allocations mint duplicates.
            Math.Max(HexToken.ParseValueOrDefault(skeleton.GetAttribute("last_unique_id"), 0x40),
                     IdAllocator.MaxCounterPresent(skeleton));

        private static ProjectElement BuildEnumDefinitions(ProjectElement skeleton, ProjectElement template, IdAllocator allocator)
        {
            ProjectElement skeletonEnums = RequireChild(skeleton, "enum_definitions");
            string containerId = skeletonEnums.GetAttribute("id") ?? "_0x3046";
            string containerName = skeletonEnums.GetAttribute("name") ?? "Enumerator definitioner";

            var definitions = ImmutableArray.CreateBuilder<ProjectElement>();
            foreach (ProjectElement def in template.Children)
            {
                if (def.Tag != "enum_definition")
                {
                    continue;
                }
                string defId = allocator.Allocate(TypeCode.RequireForTag("enum_definition")).ToToken();
                var values = ImmutableArray.CreateBuilder<ProjectElement>();
                foreach (ProjectElement value in def.Children)
                {
                    if (value.Tag != "enum_value")
                    {
                        continue;
                    }
                    string valueId = allocator.Allocate(TypeCode.RequireForTag("enum_value")).ToToken();
                    values.Add(Node("enum_value", valueId, CopyAttrs(value, "typeid", "name", "index"), NoChildren));
                }
                definitions.Add(Node("enum_definition", defId, CopyAttrs(def, "typeid", "name", "note"), values));
            }
            if (definitions.Count == 0)
            {
                throw new InvalidDataException(
                    "Data\\EnumeratorDefinitions.def under the configured IHC Visual install dir supplied no " +
                    "enum_definition elements — the installation is incomplete or the template was edited. " +
                    "CreateNew cannot seed the built-in enums, and a half-seeded project would break every " +
                    "catalog insert that references them.");
            }
            return Node("enum_definitions", containerId, new[] { ("name", containerName) }, definitions);
        }

        private static ProjectElement BuildDocumentationModules(IdAllocator allocator)
        {
            string modulesId = allocator.Allocate(TypeCode.RequireForTag("documentation_modules")).ToToken();
            string inputsId = allocator.Allocate(TypeCode.RequireForTag("dataline_input_modules")).ToToken();
            string outputsId = allocator.Allocate(TypeCode.RequireForTag("dataline_output_modules")).ToToken();
            return Node("documentation_modules", modulesId, NoAttrs, new[]
            {
                Node("dataline_input_modules", inputsId, NoAttrs, NoChildren),
                Node("dataline_output_modules", outputsId, NoAttrs, NoChildren),
            });
        }

        private static ProjectElement BuildModified(DateTimeOffset moment) =>
            Node("modified", null, new[]
            {
                ("year", DecToken.Format(moment.Year)),
                ("month", DecToken.Format(moment.Month)),
                ("day", DecToken.Format(moment.Day)),
                ("hour", DecToken.Format(moment.Hour)),
                ("minute", DecToken.Format(moment.Minute)),
            }, NoChildren);

        // Unset optional details pass through as "" — exactly the DTD default, so the final Canonicalize
        // pass drops them and an all-default block serializes as the same empty element it always did.
        private static ProjectElement BuildCustomerInfo(ProjectDetails details) =>
            Node("customer_info", null, new[]
            {
                ("name", details.CustomerName ?? ""),
                ("address", details.CustomerAddress ?? ""),
                ("city", details.CustomerCity ?? ""),
                ("zipcode", details.CustomerZipCode ?? ""),
                ("country", details.CustomerCountry ?? ""),
                ("phone", details.CustomerPhone ?? ""),
                ("mobilephone", details.CustomerMobilePhone ?? ""),
                ("email", details.CustomerEmail ?? ""),
            }, NoChildren);

        private static ProjectElement BuildInstallerInfo(ProjectDetails details) =>
            Node("installer_info", null, new[]
            {
                ("name", details.InstallerName),
                ("address", details.InstallerAddress ?? ""),
                ("city", details.InstallerCity ?? ""),
                ("zipcode", details.InstallerZipCode ?? ""),
                ("country", details.InstallerCountry),
                ("phone", details.InstallerPhone ?? ""),
                ("mobilephone", details.InstallerMobilePhone ?? ""),
                ("email", details.InstallerEmail ?? ""),
            }, NoChildren);

        private static ProjectElement BuildProjectInfo(ProjectDetails details) =>
            Node("project_info", null, new[]
            {
                ("programmer", details.Programmer),
                ("number", details.ProjectNumber ?? ""),
                ("drawing", details.Drawing ?? ""),
                ("type", details.ProjectType ?? ""),
                ("description", details.Description ?? ""),
            }, NoChildren);

        // --- small construction helpers (the final Canonicalize pass fixes order / omits defaults / drops unknowns) ---

        private static readonly (string, string)[] NoAttrs = Array.Empty<(string, string)>();
        private static readonly ProjectElement[] NoChildren = Array.Empty<ProjectElement>();

        private static ProjectElement Node(string tag, string? id, IEnumerable<(string Name, string Value)> attrs,
                                           IEnumerable<ProjectElement> children)
        {
            // The ids passed here are canonical _0x tokens (each is an IdAllocator .ToToken() or the skeleton's
            // canonical container id), so parsing to ElementId and letting ProjectElement.Create re-render the id
            // attribute is byte-identical — and drops this method's re-implementation of Create's id-bag assembly.
            ElementId? parsedId = id is not null && ElementId.TryParse(id, out ElementId p) ? p : null;
            return ProjectElement.Create(tag, parsedId, attrs, children);
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

        private static ProjectElement RequireChild(ProjectElement parent, string tag) =>
            parent.FindChild(tag) ?? throw new InvalidOperationException(
                $"The File→New template is missing the required '{tag}' element.");
    }
}
