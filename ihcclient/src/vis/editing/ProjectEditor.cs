#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;
using TypeCode = Ihc.Vis.Schema.TypeCode;
using static Ihc.Vis.Editing.ProjectTreeOps;   // T015: the pure immutable-tree primitives, imported so call sites are unchanged
using static Ihc.Vis.Editing.DeleteCascade;    // T016: the delete/copy reference-integrity cluster (schema-bearing calls pass SchemaView)
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// The mutable edit session over an immutable <see cref="Project"/> — the authoring (write) surface a GUI
    /// drives. Open it with <c>project.Edit()</c>, mutate through live handles (<see cref="Group(string)"/> → products /
    /// function blocks / resources; <see cref="Link(ResourceRef,ResourceRef)"/>/<see cref="Unlink(ResourceRef,ResourceRef)"/>; the <c>Remove*</c> methods), then call
    /// <see cref="ToProject"/> to commit a fresh immutable snapshot to save. Loaded <c>_0x</c> ids are preserved;
    /// new ones are allocated eagerly for added elements off the project counter (deletes leave permanent holes).
    /// </summary>
    /// <remarks>
    /// Internally the session holds the project as an immutable <see cref="ProjectElement"/> tree that is rebuilt on
    /// each mutation; handles address their target by its stable <see cref="ElementId"/>, so they survive every
    /// rebuild. Read/browse from the generic <see cref="Project"/>/<see cref="ProjectElement"/> model (via
    /// <see cref="ToProject"/>), not these write-only handles.
    /// </remarks>
    public sealed class ProjectEditor
    {
        internal const string FollowLinkName = "Følg Link";
        internal const string SceneLinkName = "Scenarie link";
        private const string GroupsTag = "groups";
        private const string EnumDefinitionsTag = "enum_definitions";

        private ProjectElement root;
        private readonly IdAllocator allocator;
        private ImmutableDictionary<string, string> inlineDtdBlocks;   // grows as unregistered inserted types are adopted
        private ProjectSchemaView? schemaView;   // memoized view over inlineDtdBlocks; invalidated when they change
        private bool catalogEnumsNormalized;      // NormalizeCatalogEnums has already run on this editor (idempotence guard, D03)

        internal ProjectEditor(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            root = project.Root;
            inlineDtdBlocks = project.InlineDtdBlocks;   // carry the file's captured DTD so open-world edits round-trip
            // P1b (W4-1): an SDK-committed project registered its open analysis — the id counter high-water mark, and
            // implicitly that it is canonical, undeclared-attribute-free and duplicate-id-free. Reuse it: re-seeding the
            // allocator from the cached counter is exactly what a fresh scan would return, so the guards below are
            // redundant. Any un-registered project (loaded, consumer-created, or a with-clone) takes the full safe path.
            if (EditAnalysisCache.TryGet(project) is { } analysis)
            {
                allocator = new IdAllocator(analysis.AllocatorSeed);
                return;
            }
            allocator = IdAllocator.ForProject(project);
            EditAnalysisCache.CountFullAnalysis();
            // An attribute the grammar does not declare would fail the save; failing here — before the user
            // invests edits — beats a commit-time crash, and beats the silent drop canonicalization once did.
            SchemaGuards.GuardTreeNoUnknownAttributes(root, SchemaView);
            GuardNoDuplicateIdTokens(root);
        }

        /// <summary>
        /// Editing addresses elements by id, so a document with duplicate id tokens (loadable for inspection,
        /// but every id-addressed lookup resolves first-match) is not editable until repaired.
        /// </summary>
        private static void GuardNoDuplicateIdTokens(ProjectElement root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Walk(ProjectElement element)
            {
                if (element.GetAttribute("id") is { } token && !seen.Add(token))
                {
                    throw new InvalidOperationException(
                        $"Cannot edit: several elements share the id token '{token}', so id-addressed editing " +
                        "would silently target the wrong element. Repair the duplicate ids first " +
                        $"({nameof(ProjectAppService)}.{nameof(ProjectAppService.Validate)} lists them).");
                }
                foreach (ProjectElement child in element.ChildrenOrEmpty())
                {
                    Walk(child);
                }
            }
            Walk(root);
        }

        internal IdAllocator Allocator => allocator;

        /// <summary>
        /// The schema resolver for this session (the file's own inline DTD first, registry fallback), memoized so
        /// the interactive attribute-edit path does not re-parse every inline DTD block on each access.
        /// </summary>
        internal ProjectSchemaView SchemaView => schemaView ??= ProjectSchemaView.For(inlineDtdBlocks);

        /// <summary>Gets the named locality (room), seeding it if necessary, and returns its live handle.</summary>
        public GroupRef Group(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = FindGroupByName(name) ?? SeedGroup(name);
            return new GroupRef(this, id);
        }

        /// <summary>
        /// Returns the live handle for an existing locality (room) addressed by its stable id — the id-addressed
        /// counterpart of <see cref="Group(string)"/>, so a GUI that holds element ids (and must disambiguate
        /// same-named rooms) can add products/function blocks to exactly the intended locality. Throws when the id
        /// is absent or does not address a <c>group</c>.
        /// </summary>
        public GroupRef Group(ElementId id)
        {
            RequireTagged(id, "group");
            return new GroupRef(this, id);
        }

        /// <summary>
        /// Returns the live handle for an existing function block addressed by its stable id — the id-addressed
        /// counterpart of <see cref="GroupRef.FunctionBlock(string)"/>, for a GUI that holds element ids (e.g. to
        /// export a selected block via <see cref="FunctionBlockRef.ExportDefinition"/>). Throws when the id is absent
        /// or does not address a <c>functionblock</c>.
        /// </summary>
        public FunctionBlockRef FunctionBlock(ElementId id)
        {
            RequireTagged(id, "functionblock");
            return new FunctionBlockRef(this, id);
        }

        /// <summary>
        /// Appends a brand-new locality (room) named <paramref name="name"/> under the project's <c>groups</c>
        /// container and returns its live handle — the "insert locality" authoring action (US-008). Unlike
        /// <see cref="Group(string)"/> (find-or-seed by name) this <b>always</b> adds a new room, appended last (the bottom
        /// of the tree), so repeated inserts yield distinct same-named rooms, as IHC Visual does. Its id is minted
        /// fresh off the project counter.
        /// </summary>
        public GroupRef AddGroup(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return new GroupRef(this, SeedGroup(name));
        }

        /// <summary>
        /// Authors a new project-global enum definition — as IHC Visual does when a user creates an enum "on first
        /// use" (e.g. project2's "NyTypeForThisProject"): allocates the definition id then one id per value in
        /// document order (R1), stamps each value with its 0-based <c>index</c>, appends the definition (with its
        /// values) to the project's <c>enum_definitions</c> container, and returns a handle for wiring
        /// <c>resource_enum</c> references. Passing no values authors an empty definition (e.g. project3's "TestEnum").
        /// </summary>
        public EnumDefinitionRef AddEnumDefinition(string name, params string[] values)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(values);

            ElementId defId = allocator.Allocate(TypeCode.RequireForTag("enum_definition"));
            var valueElements = ImmutableArray.CreateBuilder<ProjectElement>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                valueElements.Add(SimpleElement("enum_value", allocator.Allocate(TypeCode.RequireForTag("enum_value")),
                    ("name", values[i]), ("index", DecToken.Format(i))));
            }
            ProjectElement def = SimpleElement("enum_definition", defId, ("name", name))
                with { Children = valueElements.ToImmutable() };

            ProjectElement container = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            root = ReplaceChildByTag(root, EnumDefinitionsTag,
                container with { Children = AppendTo(container.Children, def) });

            return ToEnumRef(def);
        }

        /// <summary>
        /// Resolves a pre-existing project-global enum definition by its exact <c>name</c> and returns the same
        /// wiring handle <see cref="AddEnumDefinition"/> hands out — for wiring an enum operand
        /// (<see cref="ConditionRef.AddEnumOperand"/>) or appending values (<see cref="AddEnumValues"/>) to an
        /// enum authored in an earlier session. Built-in catalog (<c>typeid</c>-bearing, "[read only]") enums
        /// resolve too — they are legal wiring targets; only mutation refuses them. Throws when no definition
        /// carries the name (the message lists the available definitions).
        /// </summary>
        public EnumDefinitionRef EnumDefinition(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ProjectElement container = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            ProjectElement def = container.ChildrenOrEmpty()
                .FirstOrDefault(c => c.Tag == "enum_definition" && c.GetAttribute("name") == name)
                ?? throw new InvalidOperationException(
                    $"The project has no enum definition named '{name}'; available definitions: " +
                    $"({string.Join(" | ", container.ChildrenOrEmpty().Where(c => c.Tag == "enum_definition").Select(c => c.GetAttribute("name")))}).");
            return ToEnumRef(def);
        }

        /// <summary>
        /// Appends values to an existing project-global enum definition — as IHC Visual's enum dialog does
        /// (oracle <c>project3-KompleksWired-enumappend.vis</c>, ENG-A3): the definition keeps its id and its
        /// document position (append <b>in place</b>, self-closed → open), each value allocates one fresh id in
        /// argument order, and <c>index</c> continues 0-based from the existing value count (<c>index="0"</c> is
        /// elided as the DTD default on commit). Returns a refreshed handle covering old and new values (the
        /// handle passed in stays stale). Throws on built-in catalog (<c>typeid</c>-bearing, "[read only]")
        /// definitions — IHC Visual does not let their values be edited either.
        /// </summary>
        public EnumDefinitionRef AddEnumValues(EnumDefinitionRef definition, params string[] values)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(values);

            ProjectElement container = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            // By parsed ElementId (not raw token text), the codebase's id-matching convention: a foreign file's
            // non-canonical spelling of the definition id must still resolve.
            ProjectElement def = container.ChildrenOrEmpty()
                .FirstOrDefault(c => c.Id == definition.Id)
                ?? throw new InvalidOperationException(
                    $"Enum definition '{definition.Typedef}' is no longer part of the project.");
            if (def.GetAttribute("typeid") is { } typeid && typeid != ElementId.NullToken)
            {
                throw new InvalidOperationException(
                    $"Enum definition '{def.GetAttribute("name")}' is a built-in catalog type — \"[read only]\" " +
                    "in IHC Visual — so its values cannot be edited.");
            }

            int existing = def.ChildrenOrEmpty().Count(c => c.Tag == "enum_value");
            var appended = ImmutableArray.CreateBuilder<ProjectElement>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                appended.Add(SimpleElement("enum_value", allocator.Allocate(TypeCode.RequireForTag("enum_value")),
                    ("name", values[i]), ("index", DecToken.Format(existing + i))));
            }
            ProjectElement updated = def with { Children = def.ChildrenOrEmpty().Concat(appended).ToImmutableArray() };
            root = ReplaceChildByTag(root, EnumDefinitionsTag, container with
            {
                Children = container.ChildrenOrEmpty().Select(c => ReferenceEquals(c, def) ? updated : c).ToImmutableArray(),
            });
            return ToEnumRef(updated);
        }

        /// <summary>
        /// Relabels one existing value of a USER enum type (US-030 relabel, PG-5): changes the value's <c>name</c> in
        /// place, preserving its id and <c>index</c> — so only the label byte-differs and the change round-trips
        /// faithfully (reorder / remove / rename-type are out of scope, D05). Refuses a built-in catalog type
        /// (<c>typeid</c>-bearing, "[read only]" in IHC Visual) and a value id that is not part of the definition.
        /// Returns the updated handle.
        /// </summary>
        public EnumDefinitionRef RelabelEnumValue(EnumDefinitionRef definition, ElementId valueId, string newName)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(newName);
            ProjectElement def = FindById(root, definition.Id)
                ?? throw new InvalidOperationException($"Enum definition '{definition.Typedef}' is no longer part of the project.");
            if (def.GetAttribute("typeid") is { } typeid && typeid != ElementId.NullToken)
            {
                throw new InvalidOperationException(
                    $"Enum definition '{def.GetAttribute("name")}' is a built-in catalog type — \"[read only]\" " +
                    "in IHC Visual — so its values cannot be edited.");
            }
            if (!def.ChildrenOrEmpty().Any(v => v.Tag == "enum_value" && v.Id == valueId))
            {
                throw new InvalidOperationException(
                    $"Enum definition '{def.GetAttribute("name")}' has no value with id {valueId.ToToken()}.");
            }
            SetAttributeById(valueId, "name", newName);   // in-place relabel — id and index preserved (byte-faithful)
            return ToEnumRef(FindById(root, definition.Id)!);
        }

        /// <summary>Builds the wiring handle for an in-tree <c>enum_definition</c> element.</summary>
        private static EnumDefinitionRef ToEnumRef(ProjectElement definition)
        {
            var values = ImmutableArray.CreateBuilder<(string, ElementId)>();
            foreach (ProjectElement value in definition.ChildrenOrEmpty().Where(c => c.Tag == "enum_value"))
            {
                values.Add((value.GetAttribute("name") ?? "", value.Id!.Value));
            }
            return new EnumDefinitionRef(definition.GetAttribute("name") ?? "", definition.Id!.Value, values.ToImmutable());
        }

        /// <summary>
        /// Reproduces IHC Visual's one-time, load-time normalization of the built-in catalog enums (the
        /// <c>typeid</c>-bearing "[read only]" definitions, e.g. project3's "Persienne tilstand"/"Logning"): moves
        /// them to the bottom of <c>enum_definitions</c>, renumbers each — definition then values, in document order —
        /// to fresh ids off the project counter, and rewrites every <c>resource_enum</c> reference
        /// (<c>typedef</c>/<c>inivalue</c>) to the new ids. <c>typeid</c>/<c>name</c>/<c>index</c> are preserved.
        /// The SDK's <em>passive</em> load deliberately keeps the original low enum ids (byte round-trip fidelity), so
        /// this is <b>not</b> automatic; authoring flows that must match the vendor's post-load byte layout call it
        /// <b>once</b>, right after <see cref="ProjectEditingExtensions.Edit"/> and before any insert/copy. Returns
        /// <c>this</c> for chaining; a project with no catalog enums is left untouched.
        /// </summary>
        public ProjectEditor NormalizeCatalogEnums()
        {
            if (catalogEnumsNormalized)
            {
                // Idempotence guard (D03): a re-hoist re-mints fresh def+value ids, so a second call on the same
                // editor would burn another block of ids and move the (already-normalized) enums again. The catalog
                // enums keep their non-null typeid after the first hoist, so the filter below would re-select them —
                // hence this explicit guard rather than relying on the state to self-terminate.
                return this;
            }
            ProjectElement container = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            catalogEnumsNormalized = true;   // set once the container resolves, so any later call is a no-op (incl. the count==0 path)

            var catalogEnums = container.ChildrenOrEmpty()
                .Where(c => c.Tag == "enum_definition"
                    && c.GetAttribute("typeid") is { } typeid && typeid != ElementId.NullToken)
                .ToList();
            if (catalogEnums.Count == 0)
            {
                return this;   // no built-in catalog enums to re-hoist (idempotent for such a project)
            }

            var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var reHoisted = new List<ProjectElement>();
            foreach (ProjectElement catalogEnum in catalogEnums)
            {
                // Same primitive the catalog-insert path uses: allocate def id then value ids in document order.
                InsertTransform.HoistFresh(catalogEnum, allocator, idMap, reHoisted);
            }

            var movedIds = catalogEnums.Select(e => e.GetAttribute("id")!).ToHashSet(StringComparer.Ordinal);
            IEnumerable<ProjectElement> kept = container.ChildrenOrEmpty()
                .Where(c => !(c.GetAttribute("id") is { } id && movedIds.Contains(id)));
            ProjectElement normalized = container with { Children = kept.Concat(reHoisted).ToImmutableArray() };

            root = ReplaceChildByTag(root, EnumDefinitionsTag, normalized);
            root = InsertTransform.RemapIdRefs(root, idMap, SchemaView);   // repoint resource_enum refs tree-wide
            return this;
        }

        /// <summary>
        /// Updates the id-less <c>project_info</c> root metadata block (Dokumentation ▸ Projektinfo, US-039):
        /// upsert — only the fields the callback sets are written; setting <c>""</c> clears a field (dropped as
        /// the DTD default on commit, the vendor's blank ⇒ attribute-omitted semantics). The three metadata
        /// blocks declare no id, so they are unreachable through the id-addressed edit surface — this is their
        /// sanctioned write path.
        /// </summary>
        public ProjectEditor SetProjectInfo(Func<ProjectInfoBuilder, ProjectInfoBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return UpdateMetadataChild("project_info", configure(new ProjectInfoBuilder()).Attributes);
        }

        /// <summary>Updates the id-less <c>customer_info</c> root metadata block; semantics as <see cref="SetProjectInfo"/>.</summary>
        public ProjectEditor SetCustomerInfo(Func<PartyInfoBuilder, PartyInfoBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return UpdateMetadataChild("customer_info", configure(new PartyInfoBuilder()).Attributes);
        }

        /// <summary>Updates the id-less <c>installer_info</c> root metadata block; semantics as <see cref="SetProjectInfo"/>.</summary>
        public ProjectEditor SetInstallerInfo(Func<PartyInfoBuilder, PartyInfoBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return UpdateMetadataChild("installer_info", configure(new PartyInfoBuilder()).Attributes);
        }

        // The builder-driven metadata setters (SetProjectInfo/SetCustomerInfo/SetInstallerInfo) and the tag-driven
        // SetMetadata upsert the same id-less top-level block the same way, so this routes them through the single
        // SetMetadata implementation (review theme 2 DRY).
        private ProjectEditor UpdateMetadataChild(string tag, IReadOnlyList<(string Name, string Value)> attributes) =>
            SetMetadata(tag, attributes.ToArray());

        /// <summary>
        /// Opens a <see cref="ProgramBuilder"/> over an existing <c>program_simple</c> (addressed by id) to author its
        /// events and nested logic by hand — the id-addressed program-authoring entry a GUI drives after selecting a
        /// program node. The target must be a <c>program_simple</c> owning the <c>events</c>/<c>actions</c> containers
        /// an empty function block's <c>fb.def</c> skeleton provides.
        /// </summary>
        public ProgramBuilder Program(ElementId programSimpleId)
        {
            RequireTagged(programSimpleId, "program_simple");
            return new ProgramBuilder(this, programSimpleId);
        }

        /// <summary>
        /// Wraps an existing resource element (a product/function-block input, output, setting or internal variable,
        /// addressed by its id) as a <see cref="ResourceRef"/> operand — the id-addressed operand factory a GUI drives
        /// when it holds only element ids (US-028: author an event/command referencing a selected variable). The
        /// element must be live; its display name is read from the <c>name</c> attribute for the operand handle.
        /// </summary>
        public ResourceRef Resource(ElementId id)
        {
            ProjectElement resource = Require(id);
            return new ResourceRef(resource.GetAttribute("name") ?? string.Empty, id);
        }

        /// <summary>
        /// Sets attributes on a singleton, id-less top-level metadata container addressed by <paramref name="tag"/>
        /// (<c>project_info</c>/<c>customer_info</c>/<c>installer_info</c> — US-039): these carry no <c>id</c>, so they
        /// are reached by tag rather than by <see cref="TryResolve"/>. A blank value clears the attribute to its DTD
        /// default. Returns this. Throws when the project has no such container.
        /// </summary>
        public ProjectEditor SetMetadata(string tag, params (string Name, string Value)[] attributes)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ArgumentNullException.ThrowIfNull(attributes);
            ProjectElement child = root.FindChild(tag)
                ?? throw new InvalidOperationException($"The project has no <{tag}> container.");
            foreach ((string name, string value) in attributes)
            {
                child = child.WithAttribute(name, value);
            }
            root = ReplaceChildByTag(root, tag, child);
            return this;
        }

        /// <summary>
        /// Opens a <see cref="BranchRef"/> over an existing <c>actions</c> container (addressed by id) — a program's
        /// root "Commands" group or a sub-program's true/false branch — to append commands or nested sub-programs by
        /// hand (US-028/US-029). The id-addressed command-authoring entry a GUI drives after selecting an actions node.
        /// </summary>
        public BranchRef Branch(ElementId actionsId)
        {
            // A case value's <case_action> is itself a command container (CaseRef.Case returns a BranchRef over it),
            // so it is a legal Branch target alongside a plain <actions> group (US-031).
            RequireTagged(actionsId, "actions", "case_action");
            return new BranchRef(this, actionsId);
        }

        /// <summary>
        /// Opens a <see cref="CaseRef"/> over an existing <c>program_case</c> switch (addressed by id) to add case
        /// values or reach its default (Else) branch (US-031) — the id-addressed entry a GUI drives after selecting a
        /// Case node. The default (Else) branch is the switch's document-last <c>actions</c> child (ENG2-B2).
        /// </summary>
        public CaseRef Case(ElementId caseId)
        {
            ProjectElement kase = RequireTagged(caseId, "program_case");
            ProjectElement elseBranch = kase.ChildrenOrEmpty().Last(c => c.Tag == "actions");
            return new CaseRef(this, caseId, elseBranch.Id!.Value);
        }

        /// <summary>
        /// Opens a <see cref="ConditionsGroupRef"/> over an existing <c>conditions</c> group (addressed by id) — the
        /// id-addressed entry a GUI drives after selecting a Betingelser node in a loaded project (US-029: OR/AND
        /// toggle, add condition rows, add nested logic groups).
        /// </summary>
        public ConditionsGroupRef ConditionsGroup(ElementId conditionsId)
        {
            RequireTagged(conditionsId, "conditions");
            return new ConditionsGroupRef(this, conditionsId);
        }

        /// <summary>
        /// Resolves an id to a generic <see cref="ElementRef"/> handle — the id-addressed, write-side counterpart
        /// of <see cref="Project.FindById"/> and the foundation of a GUI selection model. Unlike the name-addressed
        /// <see cref="Group(string)"/>/<see cref="GroupRef.Product"/>/<see cref="GroupRef.FunctionBlock"/> lookups it
        /// addresses any element (resources, links, program nodes) and disambiguates same-named siblings. Returns
        /// <c>false</c> with a null handle when no element in the session carries that id.
        /// </summary>
        public bool TryResolve(ElementId id, [NotNullWhen(true)] out ElementRef? handle)
        {
            if (FindById(root, id) is null)
            {
                handle = null;
                return false;
            }
            handle = new ElementRef(this, id);
            return true;
        }

        /// <summary>
        /// Resolves <paramref name="id"/> to a live <see cref="ElementRef"/> handle, or throws an
        /// <see cref="InvalidOperationException"/> naming the missing <paramref name="noun"/> — the throwing
        /// counterpart of <see cref="TryResolve"/> and the single require-or-throw resolver the id-addressed editing
        /// guards route through (review theme 2). Pass the noun the caller means ("locality", "pin", "product") so a
        /// stale id reads for that operation instead of a generic id miss.
        /// </summary>
        internal ElementRef Resolve(ElementId id, string noun) =>
            TryResolve(id, out ElementRef? handle)
                ? handle
                : throw new InvalidOperationException($"The {noun} (id {id.ToToken()}) no longer exists.");

        /// <summary>
        /// Resolves <paramref name="id"/> and asserts its tag is one of <paramref name="expectedTags"/>, returning the
        /// element; throws an <see cref="InvalidOperationException"/> naming the actual and expected tags otherwise —
        /// the single "require the expected tag or throw" primitive the id-addressed entry points route through
        /// (review theme 2). At least one expected tag must be supplied.
        /// </summary>
        internal ProjectElement RequireTagged(ElementId id, params string[] expectedTags)
        {
            ProjectElement element = Require(id);
            if (Array.IndexOf(expectedTags, element.Tag) < 0)
            {
                throw new InvalidOperationException(
                    $"Element {id.ToToken()} is a <{element.Tag}>, not {DescribeExpectedTags(expectedTags)}.");
            }
            return element;
        }

        private static string DescribeExpectedTags(string[] expectedTags) =>
            "a " + string.Join(" or ", expectedTags.Select(tag => $"<{tag}>"));

        /// <summary>
        /// Removes a locality (room) and everything in it, cascading the reciprocal follow-link halves outside it
        /// that point into its resources (via <see cref="DeleteById(ElementId)"/>). Retired <c>_0x</c> ids are not reused;
        /// returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor RemoveGroup(GroupRef group)
        {
            ArgumentNullException.ThrowIfNull(group);
            return DeleteById(group.Id);
        }

        /// <summary>
        /// Deletes the element with the given id and its whole subtree, cascading the reciprocal follow-link halves
        /// that live <b>outside</b> the subtree and point into it — so deleting a wired product, function block,
        /// resource or locality keeps the <c>link_from_resource</c>/<c>link_to_resource</c> bijection intact and the
        /// project saveable. A cascade partner is removed only when it really is a link half pointing back into the
        /// deleted subtree (a foreign file's stray <c>link</c> IDREF must never delete an unrelated element). When
        /// any other schema-declared IDREF (a program's <c>link1</c>/<c>link2</c>, a <c>scenes</c> binding, an
        /// enum's <c>typedef</c>/<c>inivalue</c>) still points into the deleted set, the delete throws <em>before</em>
        /// committing — the session never holds a dangling reference. Retired <c>_0x</c> ids are not reused; a
        /// no-op when the id is absent. Returns <c>this</c> for chaining.
        /// </summary>
        public ProjectEditor DeleteById(ElementId id) => DeleteById(id, DeleteReferencePolicy.Strict);

        /// <summary>
        /// Deletes as <see cref="DeleteById(ElementId)"/>, with <paramref name="policy"/> deciding the fate of
        /// program rows that still reference the deleted subtree:
        /// <see cref="DeleteReferencePolicy.CascadeReferences"/> removes each referencing
        /// <c>action</c>/<c>condition</c>/<c>event</c> row whole (any link slot, parents kept — the vendor US-009
        /// semantics pinned by ENG2-A5), while <see cref="DeleteReferencePolicy.Strict"/> refuses the delete.
        /// Returns <c>this</c> for chaining.
        /// </summary>
        public ProjectEditor DeleteById(ElementId id, DeleteReferencePolicy policy)
        {
            ProjectElement? subtree = FindById(root, id);
            if (subtree is null)
            {
                return this;                             // absent id → nothing to delete
            }
            if (DeletionRefusalReason(root, id) is { } refusal)
            {
                throw new InvalidOperationException(refusal);   // catalog pin / locked-block node (review3 H1)
            }
            var deletedIds = new HashSet<ElementId>();
            CollectIds(subtree, deletedIds);
            var partnerIds = new List<ElementId>();
            CollectLinkPartners(subtree, partnerIds);    // partner ids of every link half inside the subtree

            ProjectElement candidate = RemoveById(root, id);
            foreach (ElementId partnerId in partnerIds)
            {
                if (FindById(candidate, partnerId) is { } partner
                    && ReciprocalHalfTags.Contains(partner.Tag)
                    && ElementId.TryParse(partner.GetAttribute("link"), out ElementId back)
                    && deletedIds.Contains(back))
                {
                    candidate = RemoveById(candidate, partnerId);
                    deletedIds.Add(partnerId);
                }
            }
            if (policy == DeleteReferencePolicy.CascadeReferences)
            {
                candidate = CascadeReferencingRows(candidate, deletedIds, SchemaView);
            }
            List<string> dangling = FindDanglingReferences(candidate, deletedIds, SchemaView);
            if (dangling.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Deleting {id.ToToken()} would leave dangling references: {string.Join("; ", dangling)}. " +
                    "Delete or rewire the referring elements first.");
            }
            root = candidate;
            return this;
        }

        /// <summary>
        /// The SDK-authoritative rule (review3 H1 / ADR-002, D09) for whether <paramref name="id"/> may be deleted as
        /// the DIRECT target: a product's catalog-declared pin (a <c>resource_</c>/<c>dataline_</c>/<c>airlink_</c>
        /// child of a product device root) and any node inside a LOCKED function block are owned by the
        /// catalog/library, not the installer, so deleting them on their own is refused — the returned reason names
        /// why, or <c>null</c> when the delete is allowed. Only the direct target is inspected, so a subtree delete
        /// that removes such a node with its product or block (or after the block is unlocked) still works. Shared by
        /// the engine (<see cref="DeleteById(ElementId, DeleteReferencePolicy)"/>), the <c>DeleteNode</c> command's
        /// legality check and <c>PreviewDelete</c> so one owner decides deletability across every surface.
        /// </summary>
        internal static string? DeletionRefusalReason(ProjectElement root, ElementId id)
        {
            var chain = new List<ProjectElement>();
            string? reason = null;
            if (BuildPath(root, id, chain))
            {
                ProjectElement target = chain[^1];
                ProjectElement? parent = chain.Count >= 2 ? chain[^2] : null;
                // Link halves / scene members are wiring, removed via the link operations (US-057) — legitimate even
                // on a locked block's pins — so the catalog/lock ownership rule never applies to them.
                if (IsWiringNode(target.Tag))
                {
                    reason = null;
                }
                else if (parent is not null && ProductClassifier.IsProduct(parent.Tag) && IsCatalogPinTag(target.Tag))
                {
                    reason = $"\"{target.GetAttribute("name") ?? target.Tag}\" is a catalog-declared pin of its "
                           + "product and cannot be deleted on its own — delete the product to remove it.";
                }
                else if (IsWithinLockedBlock(root, id, inclusive: false))
                {
                    reason = "This node is inside a locked function block and cannot be deleted — unlock the block first.";
                }
            }
            return reason;
        }

        // Wiring: link halves and scene members are attached/detached by the link operations (US-057), never owned by
        // the catalog/library the way a pin or a locked program node is — so removing one is legitimate even inside a
        // locked block's pin. Exempting them keeps RemoveLink/Unlink and the GUI's link-row delete working. The set is
        // ReciprocalTags.All — the single source of truth the delete cascade and the link/scene bijection checks also
        // read — so a new scene-capable family is exempted here automatically, without this literal to keep in sync.
        private static bool IsWiringNode(string tag) => ReciprocalTags.All.Contains(tag);

        // A catalog pin family: a product's resource_/dataline_/airlink_ child exists because the product's catalog
        // type declares it (review3 H1). Function-block variables share the resource_ prefix but hang off a variable
        // section, not a product, so DeletionRefusalReason's parent-is-a-product test distinguishes the two.
        private static bool IsCatalogPinTag(string tag) =>
            tag.StartsWith("resource_", StringComparison.Ordinal)
            || tag.StartsWith("dataline_", StringComparison.Ordinal)
            || tag.StartsWith("airlink_", StringComparison.Ordinal);

        // A locked (library) function block: locking is the explicit locked="yes" flag (Unlock clears it to the "no"
        // default the canonicalizer omits), so the raw attribute alone identifies it without a project/DTD view.
        private static bool IsLockedBlock(ProjectElement element) =>
            element.Tag == "functionblock" && element.GetAttribute("locked") == "yes";

        /// <summary>
        /// T003 — the ONE central locked-ancestor authorization (generalising <see cref="DeletionRefusalReason"/>'s
        /// locked clause): whether a STRUCTURAL mutation whose subtree/target is <paramref name="id"/> must be refused
        /// because <paramref name="id"/> lies within a locked (<c>locked="yes"</c>) function block — the library owns
        /// that subtree until it is unlocked. <paramref name="inclusive"/> counts <paramref name="id"/> itself being
        /// the locked block: inserting/moving/copying a child INTO the block (inclusive) is refused, while reordering
        /// the block among its own siblings or deleting the whole block (exclusive) is not. Shared by the engine
        /// insert/reorder/move/copy primitives (which throw <see cref="InvalidOperationException"/>) and the session
        /// commands' <c>Evaluate</c> (which returns a refusal verdict), so one rule decides "is this locked?" for every
        /// surface and whoever drives the editor.
        /// </summary>
        internal static bool IsWithinLockedBlock(ProjectElement root, ElementId id, bool inclusive)
        {
            var chain = new List<ProjectElement>();
            if (!BuildPath(root, id, chain))
            {
                return false;                       // absent id → not this rule's refusal to make
            }
            IEnumerable<ProjectElement> scope = inclusive ? chain : chain.Take(chain.Count - 1);
            return scope.Any(IsLockedBlock);
        }

        /// <summary>The refusal a structural edit targeting a locked block's subtree reports — the engine throw and the
        /// session verdict share this one message (T003).</summary>
        internal const string LockedBlockEditRefusal =
            "This node is inside a locked function block and cannot be edited — unlock the block first.";

        // Throws when a structural insert/move/copy would mutate a locked block's subtree (T003) — the engine half of
        // the central authorization, so a direct engine caller is refused exactly where a session command's Evaluate
        // returns a refusal verdict. `inclusive` per IsWithinLockedBlock (an insert/copy/move TARGET counts itself).
        private void RefuseIfLockedTarget(ElementId targetId, bool inclusive)
        {
            if (IsWithinLockedBlock(root, targetId, inclusive))
            {
                throw new InvalidOperationException(LockedBlockEditRefusal);
            }
        }

        // CascadeReferencingRows / RowReferencesDeleted / IsDeletedIdRef / FindDanglingReferences — the reference
        // cascade + strict dangling guard — moved to DeleteCascade (T016); DeleteById passes SchemaView to them.

        /// <summary>
        /// Wires a reciprocal follow-link between two live resources, writing both halves in a single call.
        /// <paramref name="from"/> is the source ("→") side and receives the <c>link_from_resource</c>;
        /// <paramref name="to"/> is the sink ("←") side and receives the <c>link_to_resource</c>; the two halves
        /// point at each other. Returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor Link(ResourceRef from, ResourceRef to)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            ProjectElement fromEl = RequireLive(from);   // both ends must exist before any id is allocated or a half
            ProjectElement toEl = RequireLive(to);       // appended: a stale handle must fail here, not half-write a link
            ElementId fromId = fromEl.Id!.Value;
            ElementId toId = toEl.Id!.Value;

            if (fromId == toId)   // D06: a pin cannot link to itself — refused for direct engine callers too, matching the session
                throw new InvalidOperationException(
                    $"Cannot link '{fromEl.GetAttribute("name")}' to itself: {LinkRoles.SelfLinkReason}.");

            if (!LinkRoles.CanLink(fromEl.Tag, toEl.Tag))
                throw new InvalidOperationException(
                    $"Cannot link '{fromEl.GetAttribute("name")}' to '{toEl.GetAttribute("name")}': {LinkRoles.Explain(fromEl.Tag, toEl.Tag)}.");

            ElementId linkFromId = allocator.Allocate(TypeCode.RequireForTag("link_from_resource"));   // from-half allocated first
            ElementId linkToId = allocator.Allocate(TypeCode.RequireForTag("link_to_resource"));

            ProjectElement linkFrom = SimpleElement("link_from_resource", linkFromId,
                ("name", FollowLinkName), ("icon", ResourceMaterialization.RequireIcon("link_from_resource")), ("link", linkToId.ToToken()));
            ProjectElement linkTo = SimpleElement("link_to_resource", linkToId,
                ("name", FollowLinkName), ("icon", ResourceMaterialization.RequireIcon("link_to_resource")), ("link", linkFromId.ToToken()));

            AppendChild(fromId, linkFrom);
            AppendChild(toId, linkTo);
            return this;
        }

        /// <summary>
        /// Whether <see cref="Link(ElementId,ElementId)"/> would accept these two pins — the check a GUI runs before
        /// offering the gesture, so it can refuse with its own message instead of catching. <paramref name="fromId"/>
        /// is the source end (it would receive the <c>link_from_resource</c> half), <paramref name="toId"/> the sink.
        /// False when either id does not resolve, or when the shape is one IHC Visual refuses (see
        /// <see cref="Ihc.Vis.Schema.LinkRoles"/>). A pin to <em>itself</em> (<paramref name="fromId"/> ==
        /// <paramref name="toId"/>) is also refused (D06): the vendor never produces a self-link, so
        /// <see cref="Link(ElementId,ElementId)"/> rejects it too and this stays in lock-step.
        /// </summary>
        public bool CanLink(ElementId fromId, ElementId toId)
        {
            ProjectElement? from = FindById(root, fromId);
            ProjectElement? to = FindById(root, toId);
            return fromId != toId && from is not null && to is not null && LinkRoles.CanLink(from.Tag, to.Tag);
        }

        /// <summary>
        /// Id-addressed <see cref="Link(ResourceRef,ResourceRef)"/> — the entry a GUI drives from two selected pins
        /// (it holds element ids, not the internal <see cref="ResourceRef"/> handles). <paramref name="fromId"/>
        /// receives the <c>link_from_resource</c> half, <paramref name="toId"/> the <c>link_to_resource</c> half.
        /// Both ids must resolve to existing elements, and the pair must satisfy <see cref="CanLink"/> — an illegal
        /// shape throws before anything is mutated. Returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor Link(ElementId fromId, ElementId toId) =>
            Link(Resource(fromId), Resource(toId));

        /// <summary>Id-addressed <see cref="Unlink(ResourceRef,ResourceRef)"/> — removes the reciprocal follow-link
        /// pair between the two pins (US-057). Returns <c>this</c> for chaining.</summary>
        public ProjectEditor Unlink(ElementId fromId, ElementId toId) =>
            Unlink(Resource(fromId), Resource(toId));

        /// <summary>
        /// Removes the reciprocal follow-link between two live resources — the inverse of <see cref="Link(ResourceRef,ResourceRef)"/> with
        /// the same orientation — deleting exactly the two halves of that pair. Throws when the resources are not
        /// follow-linked in this orientation (nothing is mutated then), so a stale or mistaken unlink can never
        /// silently delete other links. Returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor Unlink(ResourceRef from, ResourceRef to)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            ProjectElement fromEl = RequireLive(from);
            ProjectElement toEl = RequireLive(to);

            if (FindReciprocalPair(fromEl, toEl, t => t == ReciprocalTags.FollowLinkFromTag, ReciprocalTags.FollowLinkToTag)
                is not { } pair)
            {
                throw new InvalidOperationException(
                    $"Resources '{from.Name}' and '{to.Name}' are not follow-linked in this orientation; nothing to unlink.");
            }
            root = RemoveById(root, pair.FromHalf);
            root = RemoveById(root, pair.ToHalf);
            return this;
        }

        /// <summary>
        /// The first mutually-reciprocal pair between two parents — the half matching <paramref name="fromTag"/>
        /// inside <paramref name="fromEl"/>, the half matching <paramref name="toTag"/> inside
        /// <paramref name="toEl"/>, each pointing at the other via <c>link</c> — or <c>null</c> when no such pair
        /// exists. Serves <see cref="Unlink(ResourceRef,ResourceRef)"/> (follow-link halves) and <see cref="UnlinkScene(ResourceRef,ScenesRef)"/> (scene member ↔
        /// <c>scene_link</c>). Matching is by exact reciprocity — never "first half of the tag" — so multi-link
        /// owners and shared sinks resolve to the requested pair only.
        /// </summary>
        private static (ElementId FromHalf, ElementId ToHalf)? FindReciprocalPair(ProjectElement fromEl,
            ProjectElement toEl, Func<string, bool> fromTag, string toTag)
        {
            foreach (ProjectElement half in fromEl.ChildrenOrEmpty())
            {
                if (!fromTag(half.Tag) || half.Id is not { } fromHalfId)
                {
                    continue;
                }
                foreach (ProjectElement partner in toEl.ChildrenOrEmpty())
                {
                    // Reciprocity by parsed ElementId (not raw token text), matching GetLinks / ResolveLinkOpposite /
                    // the DeleteById cascade: a foreign file's non-canonical spelling (leading zeros, case) of an
                    // otherwise-reciprocal link must still resolve, not throw "not follow-linked".
                    if (partner.Tag == toTag && partner.Id is { } toHalfId
                        && ElementId.TryParse(half.GetAttribute("link"), out ElementId fromHalfLink) && fromHalfLink == toHalfId
                        && ElementId.TryParse(partner.GetAttribute("link"), out ElementId toHalfLink) && toHalfLink == fromHalfId)
                    {
                        return (fromHalfId, toHalfId);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Wires a scene membership between an FB scene output pin and a product's scenes container, writing both
        /// reciprocal halves in a single call (US-024, spec ch. 08 §§8.4–8.5): the member row derived from
        /// <paramref name="value"/> (<c>scene_relay</c>/<c>scene_dimmer</c>/<c>scene_shutter</c>, carrying the
        /// values) inside the scenes container, and the <c>scene_link</c> back-reference inside the pin — the two
        /// pointing at each other. Allocation is member-first (the vendor's order, pinned by the
        /// <c>-scenelinks</c> oracle). A member kind contradicting the container's bound output family throws;
        /// unknown families stay permissive (open-world). Returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor LinkScene(ResourceRef sceneOutput, ScenesRef target, SceneValue value)
        {
            ArgumentNullException.ThrowIfNull(sceneOutput);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(value);
            ProjectElement pin = RequireLive(sceneOutput);   // both ends must exist before any id is allocated or a half
            ElementId pinId = pin.Id!.Value;
            ProjectElement scenes = Require(target.Id);   // appended — a stale handle must fail here (Link precedent)
            if (pin.Tag != "resource_scene")
            {
                throw new InvalidOperationException(
                    $"Resource '{sceneOutput.Name}' is a <{pin.Tag}>; only <resource_scene> outputs fire scenes.");
            }
            RequireSceneKindMatch(scenes, target, value);

            ElementId memberId = allocator.Allocate(TypeCode.RequireForTag(value.MemberTag));   // member first (ENG-A2)
            ElementId sceneLinkId = allocator.Allocate(TypeCode.RequireForTag("scene_link"));

            var memberAttrs = new List<(string Name, string Value)>(value.Attributes.Length + 2) { ("name", SceneLinkName) };
            memberAttrs.AddRange(value.Attributes);
            memberAttrs.Add(("link", sceneLinkId.ToToken()));
            ProjectElement member = SimpleElement(value.MemberTag, memberId, memberAttrs.ToArray());
            ProjectElement sceneLink = SimpleElement("scene_link", sceneLinkId,
                ("name", SceneLinkName), ("icon", ResourceMaterialization.RequireIcon("scene_link")), ("link", memberId.ToToken()));

            AppendChild(target.Id, member);
            AppendChild(pinId, sceneLink);
            return this;
        }

        /// <summary>Id-addressed <see cref="LinkScene(ResourceRef,ScenesRef,SceneValue)"/> — the entry a GUI drives
        /// from a selected FB scene output pin and a product's scenes container (US-024).</summary>
        public ProjectEditor LinkScene(ElementId sceneOutputId, ElementId scenesId, SceneValue value)
        {
            ResourceRef sceneOutput = Resource(sceneOutputId);
            ProjectElement scenes = Require(scenesId);
            return LinkScene(sceneOutput,
                             new ScenesRef(scenes.GetAttribute("name") ?? string.Empty, scenesId), value);
        }

        /// <summary>
        /// Edits an existing scene member's stored value in place (US-058): rewrites only the member row's value
        /// attributes from <paramref name="value"/> (<c>relay_value</c>, or <c>dimming_value</c>/<c>ramptime_ms</c>,
        /// or <c>shutter_position</c>), so its <c>id</c>, <c>name</c>, <c>link</c> (the <c>IDREF #REQUIRED</c> back to
        /// the scene_link) and any <c>delay_ms</c>/<c>note</c>/<c>udf</c> are preserved. The member's tag must match
        /// <paramref name="value"/>'s kind — a mismatch throws, nothing is mutated. Returns <c>this</c> for chaining.
        /// </summary>
        public ProjectEditor SetSceneValue(ElementId memberId, SceneValue value)
        {
            ArgumentNullException.ThrowIfNull(value);
            ProjectElement member = Require(memberId);
            if (member.Tag != value.MemberTag)
            {
                throw new InvalidOperationException(
                    $"Scene member <{member.Tag}> (id {memberId.ToToken()}) cannot take a {value.MemberTag} value.");
            }
            foreach ((string Name, string Value) attr in value.Attributes)
            {
                SetAttributeById(memberId, attr.Name, attr.Value);
            }
            return this;
        }

        /// <summary>
        /// Toggles a "Log …" row's log mark (US-068, the vendor's &amp;Logmærke): a Logning <c>resource_enum</c> flips
        /// its <c>inivalue</c> between "Off" and its first logging mode. Throws when the target is not a Logning row,
        /// so a mistaken toggle can never rewrite an ordinary enum's initial value. Returns <c>this</c> for chaining.
        /// </summary>
        public ProjectEditor ToggleLogMark(ElementId logRowId)
        {
            RefuseIfLockedTarget(logRowId, inclusive: true);   // T004: no log-mark toggle inside a locked block
            ProjectElement row = Require(logRowId);
            if (row.Tag != "resource_enum" || !ElementId.TryParse(row.GetAttribute("typedef"), out ElementId defId)
                || FindById(root, defId) is not { } def || def.GetAttribute("typeid") != ProjectElementRead.LogEnumTypeId)
            {
                throw new InvalidOperationException($"{logRowId.ToToken()} is not a Logning 'Log …' row.");
            }
            System.Collections.Generic.List<ProjectElement> values =
                def.ChildrenOrEmpty().Where(v => v.Tag == "enum_value").ToList();
            ProjectElement? off = values.FirstOrDefault(v => v.GetAttribute("name") == "Off");
            ProjectElement? on = values.FirstOrDefault(v => v.GetAttribute("name") != "Off");
            if (off?.Id is not { } offId || on?.Id is not { } onId)
            {
                throw new InvalidOperationException($"The Logning type of {logRowId.ToToken()} lacks Off/on values.");
            }
            bool currentlyOff = row.GetAttribute("inivalue") == offId.ToToken();
            SetAttributeById(logRowId, "inivalue", (currentlyOff ? onId : offId).ToToken());
            return this;
        }

        /// <summary>Id-addressed <see cref="UnlinkScene(ResourceRef,ScenesRef)"/> — removes the scene membership pair
        /// between a scene output pin and a scenes container (US-057).</summary>
        public ProjectEditor UnlinkScene(ElementId sceneOutputId, ElementId scenesId)
        {
            ResourceRef sceneOutput = Resource(sceneOutputId);
            ProjectElement scenes = Require(scenesId);
            return UnlinkScene(sceneOutput,
                               new ScenesRef(scenes.GetAttribute("name") ?? string.Empty, scenesId));
        }

        /// <summary>
        /// Removes the scene membership between an FB scene output pin and a product's scenes container — the
        /// inverse of <see cref="LinkScene(ResourceRef,ScenesRef,SceneValue)"/> — deleting exactly the two halves of that pair. Throws when the two
        /// are not scene-linked (nothing is mutated then), so a stale or mistaken unlink can never silently delete
        /// other memberships. Returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor UnlinkScene(ResourceRef sceneOutput, ScenesRef target)
        {
            ArgumentNullException.ThrowIfNull(sceneOutput);
            ArgumentNullException.ThrowIfNull(target);
            ProjectElement pin = RequireLive(sceneOutput);
            ProjectElement scenes = Require(target.Id);

            if (FindReciprocalPair(scenes, pin, ReciprocalTags.SceneMemberTags.Contains, ReciprocalTags.SceneLinkTag)
                is not { } pair)
            {
                throw new InvalidOperationException(
                    $"Resource '{sceneOutput.Name}' and scenes container '{target.Name}' are not scene-linked; " +
                    "nothing to unlink.");
            }
            root = RemoveById(root, pair.FromHalf);
            root = RemoveById(root, pair.ToHalf);
            return this;
        }

        private void RequireSceneKindMatch(ProjectElement scenes, ScenesRef target, SceneValue value)
        {
            if (ElementId.TryParse(scenes.GetAttribute("scene_resource"), out ElementId boundId)
                && FindById(root, boundId) is { } bound
                && SceneRules.PinnedMemberTagFor(bound.Tag) is { } pinned
                && pinned != value.MemberTag)
            {
                throw new InvalidOperationException(
                    $"Scenes container '{target.Name}' is bound to a <{bound.Tag}> output, which takes {pinned} " +
                    $"members; a {value.MemberTag} value cannot be linked here.");
            }
        }

        /// <summary>
        /// Clones an existing in-project subtree (the clipboard copy/paste) under a new parent: deep-copies it
        /// through the same transform as a catalog insert — fresh ids off the project counter (each element's
        /// type-code suffix preserved), internal IDREFs remapped through one old→new map, and shared enums reused —
        /// then applies <paramref name="policy"/> to any reciprocal half (follow-link half or scene row) whose
        /// partner lies outside the copy. Works for any subtree (locality, product, function block or bare
        /// resource). Returns the new root's id. The source is left untouched.
        /// </summary>
        public ElementId CopySubtree(ElementId sourceId, ElementId targetParentId,
            LinkCopyPolicy policy = LinkCopyPolicy.DropExternal)
        {
            ProjectElement source = Require(sourceId);
            Require(targetParentId);                      // fail fast if the paste target does not exist
            RefuseIfLockedTarget(targetParentId, inclusive: true);   // T003: no copy INTO a locked block's subtree
            if (FindById(source, targetParentId) is not null)
            {
                // The target is the source itself or lives inside it — the clone would nest inside a copy of itself.
                // Refuse it exactly as MoveSubtree does (a paste into a node's own descendant is never valid).
                throw new InvalidOperationException(
                    $"Cannot copy {sourceId.ToToken()} into itself or its own descendant {targetParentId.ToToken()}.");
            }
            // Drop external reciprocal halves BEFORE the clone allocates ids: the vendor's paste consumes no id for
            // a dropped half, so removing them afterwards (copy-then-prune) would leave a phantom id burn. Internal
            // pairs (both ends inside the copy) stay and are remapped by InsertTransform.
            ProjectElement body = policy == LinkCopyPolicy.DropExternal ? DropExternalReciprocalHalves(source) : source;
            // A catalog product's .def body carries its enum as its first child, so a vendor paste allocates (and
            // discards) that enum's def+value ids between the product id and its first serialized child — the
            // "enum-footprint" id burn. The in-project instance dropped that stub on its original insert;
            // reconstruct it from the project's shared enum so the existing enum-dedup path
            // (HoistOrResolveEnum → BurnAndMapToExisting) reproduces the burn. Gate on device-root placement, not
            // product_identifier presence: a non-root rs485_led_dimmer_channel also declares product_identifier but
            // is not a pasted product, and must be copied one-id-per-element unchanged.
            if (PlacementRules.IsDeviceRoot(body.Tag))
            {
                body = PrependReferencedEnumStubs(body);
            }
            return InsertComponent(targetParentId, body, CatalogGrammar.Empty);
        }

        /// <summary>
        /// Prepends, as the copied body's first children, a clone of each distinct project enum a
        /// <c>resource_enum</c> in <paramref name="source"/> references (by <c>typedef</c>, in first-appearance
        /// order) — mirroring the enum a catalog product's <c>.def</c> body carries first. The insert pipeline then
        /// allocates-and-discards each stub's def+value ids (they dedup against that same project enum), reproducing
        /// IHC Visual's enum-footprint id burn on a product paste; the reused shared enum keeps its id. Returns
        /// <paramref name="source"/> unchanged when it references no enum. Only the single-<c>typeid</c>-enum product
        /// copy is oracle-verified (the vendor driver cannot persist a pasted function block).
        /// </summary>
        private ProjectElement PrependReferencedEnumStubs(ProjectElement source)
        {
            ProjectElement? container = root.FindChild(EnumDefinitionsTag);
            if (container is null)
            {
                return source;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stubs = new List<ProjectElement>();
            foreach (ProjectElement element in source.DescendantsAndSelf())
            {
                if (element.GetAttribute("typedef") is { } typedef && typedef != ElementId.NullToken && seen.Add(typedef)
                    && container.ChildrenOrEmpty().FirstOrDefault(d => d.GetAttribute("id") == typedef) is { } def)
                {
                    stubs.Add(def);
                }
            }
            return stubs.Count == 0
                ? source
                : source with { Children = stubs.Concat(source.ChildrenOrEmpty()).ToImmutableArray() };
        }

        /// <summary>
        /// Relocates an existing subtree to a new parent while <b>preserving every id</b> — the drag-move / cut+paste
        /// reparent (spec ch. 02 §6.6: ids never change on a move). Detaches the subtree and re-appends it under
        /// <paramref name="targetParentId"/> at an optional <paramref name="index"/> (end when omitted), leaving all
        /// reciprocal links intact because nothing is re-id'd. Throws if the target is the subtree itself or inside
        /// it (which would detach the target). Returns <c>this</c> for chaining.
        /// </summary>
        public ProjectEditor MoveSubtree(ElementId sourceId, ElementId targetParentId, int? index = null)
        {
            ProjectElement subtree = Require(sourceId);   // the exact node — ids preserved verbatim
            Require(targetParentId);
            RefuseIfLockedTarget(targetParentId, inclusive: true);   // T003: no move INTO a locked block (checked
                                                                     // before the detach, so a refused move is atomic)
            if (!CanMoveSubtree(sourceId, targetParentId))
            {
                throw new InvalidOperationException(
                    $"Cannot move {sourceId.ToToken()} into itself or its own descendant {targetParentId.ToToken()}.");
            }
            root = RemoveById(root, sourceId);            // detach (ids untouched)
            InsertChildAt(targetParentId, subtree, index);
            return this;
        }

        /// <summary>
        /// Whether <see cref="MoveSubtree(ElementId, ElementId, int?)"/> could relocate <paramref name="sourceId"/>
        /// under <paramref name="targetParentId"/> — the non-mutating predicate a GUI reads for a drag-over hint
        /// (mirrors <see cref="CanLink"/>). True iff both ids resolve and the target is neither the subtree itself nor
        /// a node inside it (the structural cycle a move would create by detaching the target). Container-admissibility
        /// is a grammar concern the caller applies, exactly as <see cref="MoveSubtree(ElementId, ElementId, int?)"/>
        /// leaves it to the caller.
        /// </summary>
        public bool CanMoveSubtree(ElementId sourceId, ElementId targetParentId)
        {
            ProjectElement? subtree = FindById(root, sourceId);
            return subtree is not null
                && FindById(root, targetParentId) is not null
                && FindById(subtree, targetParentId) is null;
        }

        /// <summary>
        /// Reorders <paramref name="id"/> to position <paramref name="index"/> among its <b>same-tag siblings</b> within
        /// its current parent — the id-preserving drag-reorder (US-055) and the primitive behind Move up/down.
        /// <paramref name="index"/> is a same-tag position (a locality moves among localities, a product among
        /// products), <b>clamped</b> to the sibling range so an out-of-range drop lands at the nearest end rather than
        /// throwing. Ids and links are untouched (it is a <see cref="MoveSubtree"/> with no re-id). Throws if the node
        /// has no parent. Returns <c>this</c> for chaining.
        /// </summary>
        public ProjectEditor ReorderSubtree(ElementId id, int index)
        {
            RefuseIfLockedTarget(id, inclusive: false);   // T003: no reorder of a node INSIDE a locked block (the
                                                          // block itself may still be reordered among its siblings)
            ProjectElement node = Require(id);
            ProjectElement parent = FindParentOf(root, id)
                ?? throw new InvalidOperationException($"Cannot reorder {id.ToToken()}: it has no parent.");
            List<ProjectElement> sameTag = parent.ChildrenOrEmpty().Where(c => c.Tag == node.Tag).ToList();
            int clamped = Math.Clamp(index, 0, sameTag.Count - 1);
            // Translate the same-tag position to the absolute child index of the sibling currently sitting there.
            int absolute = parent.ChildrenOrEmpty().ToList().FindIndex(c => c.Id == sameTag[clamped].Id);
            return MoveSubtree(id, parent.Id!.Value, absolute);
        }

        private void InsertChildAt(ElementId parentId, ProjectElement child, int? index) =>
            Mutate(parentId, parent =>
            {
                ImmutableArray<ProjectElement> children = parent.ChildrenOrEmpty();
                int at = index is { } i ? Math.Clamp(i, 0, children.Length) : children.Length;
                return parent with { Children = children.Insert(at, child) };
            });

        // DropExternalReciprocalHalves (the copy-prune) moved to DeleteCascade (T016).

        // ----- placement legality (read; right-click "insert…" menus and gray-out) -----

        /// <summary>
        /// Whether an element of type <paramref name="childTag"/> may be inserted directly under the target node —
        /// the placement-legality predicate for graying out illegal right-click actions (spec ch. 03/04, §6.3.1,
        /// §8.2). A parent type the containment model does not cover is permissive (returns <c>true</c>), so a legal
        /// insert is never blocked by an unmodeled rule.
        /// </summary>
        public bool CanInsert(ElementId targetId, string childTag)
        {
            ArgumentNullException.ThrowIfNull(childTag);
            return PlacementRules.CanInsert(Require(targetId).Tag, childTag, FindParentOf(root, targetId)?.Tag);
        }

        /// <summary>
        /// The item types legal to insert under the target node, grouped by category, for building a context menu —
        /// empty for a parent type the containment model does not cover (the GUI then relies on
        /// <see cref="CanInsert"/>'s permissive default).
        /// </summary>
        public IReadOnlyList<InsertOption> GetInsertableAt(ElementId targetId) =>
            PlacementRules.OptionsFor(Require(targetId).Tag, FindParentOf(root, targetId)?.Tag);

        // ----- link navigation (read; the F4 jump and far-end path) -----

        /// <summary>
        /// Enumerates the follow-link rows a resource owns — its <c>link_from_resource</c>/<c>link_to_resource</c>
        /// children — as <see cref="LinkInfo"/> (row id, direction tag, and the partner-row id its <c>link</c> IDREF
        /// points at). Empty for an element that owns no links. The read side of the link model for a GUI.
        /// </summary>
        public IReadOnlyList<LinkInfo> GetLinks(ElementId resourceId)
        {
            ProjectElement resource = Require(resourceId);
            var links = new List<LinkInfo>();
            foreach (ProjectElement child in resource.ChildrenOrEmpty())
            {
                if (child.Tag is "link_from_resource" or "link_to_resource"
                    && child.Id is { } rowId
                    && ElementId.TryParse(child.GetAttribute("link"), out ElementId partner))
                {
                    links.Add(new LinkInfo(rowId, child.Tag, partner));
                }
            }
            return links;
        }

        /// <summary>
        /// Resolves a link row's opposite endpoint (the F4 jump): follows the row's <c>link</c> IDREF to the partner
        /// link element and returns its <b>parent</b> — the peer resource (spec ch. 06 §6.4.1). Returns <c>null</c>
        /// when the id is not a link row or the partner/peer is missing.
        /// </summary>
        public ElementRef? ResolveLinkOpposite(ElementId linkRowId)
        {
            ProjectElement? row = FindById(root, linkRowId);
            if (row?.Tag is not ("link_from_resource" or "link_to_resource")
                || !ElementId.TryParse(row.GetAttribute("link"), out ElementId partnerLinkId))
            {
                return null;
            }
            ProjectElement? peer = FindParentOf(root, partnerLinkId);
            return peer?.Id is { } peerId ? new ElementRef(this, peerId) : null;
        }

        /// <summary>
        /// Renders an element's human-readable location as <c>locality / product-or-block / pin</c> — the
        /// significant ancestors (a <c>group</c>, a <c>functionblock</c> or a device root per
        /// <see cref="PlacementRules.IsDeviceRoot"/>) followed by the element's own name, skipping structural
        /// containers (<c>inputs</c>/<c>outputs</c>/…). Empty when the id is absent. Used for the "Link fra…"
        /// far-end decoration.
        /// </summary>
        public string GetFullPath(ElementId elementId)
        {
            var chain = new List<ProjectElement>();
            if (!BuildPath(root, elementId, chain))
            {
                return string.Empty;
            }
            var parts = new List<string>();
            for (int i = 0; i < chain.Count; i++)
            {
                bool isTarget = i == chain.Count - 1;
                if ((isTarget || IsPathSignificant(chain[i].Tag)) && chain[i].GetAttribute("name") is { } name)
                {
                    parts.Add(name);
                }
            }
            return string.Join(" / ", parts);
        }

        private static bool IsPathSignificant(string tag) =>
            tag is "group" or "functionblock" || PlacementRules.IsDeviceRoot(tag);

        // FindParentOf / BuildPath moved to ProjectTreeOps (T015).

        /// <summary>
        /// Produces the immutable, canonical project snapshot: every existing id preserved, new ids already
        /// allocated, and the root <c>last_unique_id</c> rewritten from the counter high-water mark.
        /// </summary>
        public Project ToProject()
        {
            ProjectElement withCounter = root.WithAttribute("last_unique_id", allocator.LastUniqueIdToken);
            // Throw (not Drop): a session tree can only hold undeclared attributes through an internal bug —
            // inserted subtrees are pre-canonicalized and the loaded tree was guarded at open — and silently
            // dropping one here would be data loss the plain serializer refuses.
            var committed = new Project(Canonicalizer.Canonicalize(withCounter, SchemaView, UndeclaredAttributePolicy.Throw))
            {
                InlineDtdBlocks = inlineDtdBlocks,
            };
            ProjectContracts.AssertCore(committed, "edit-commit");
            // P1b (W4-1): register this SDK-produced snapshot's analysis. The written last_unique_id IS the allocator
            // counter, and every present/referenced id is ≤ it, so IdAllocator.ForProject(committed) would return
            // exactly this counter — the next Edit() of `committed` reuses it and skips the tree re-scan + guards.
            EditAnalysisCache.Register(committed, allocator.Counter);
            return committed;
        }

        // ----- insert (called by GroupRef) -----

        internal ElementId InsertComponent(ElementId groupId, ProjectElement catalogBody, CatalogGrammar grammar)
        {
            // Fail fast on a stale/absent target before ANY commit (block adoption, id burn, enum hoist): otherwise a
            // dead group id leaves the session half-mutated — hoisted enums with no owning component and burnt ids.
            Require(groupId);
            MergeNonRegistryBlocks(catalogBody, grammar);
            ProjectElement enumDefinitions = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            // The reader hands back the RAW catalog body (no DTD-defaulted attributes) so it re-emits
            // byte-faithfully; re-materialize the component's effective values from its own grammar here, since the
            // cross-DTD reconciliation below (InsertTransform → Canonicalizer) reads them (spec ch. 09 §9.3.7).
            ProjectElement effectiveBody = CatalogDefaults.Materialize(catalogBody, ProjectSchemaView.For(grammar));
            InsertResult result = InsertTransform.Insert(effectiveBody, allocator, enumDefinitions, SchemaView);
            // Validate before committing anything, so a failed insert leaves no half-mutated session
            // (hoisted enums without their component, or an unaddressable inserted root).
            ElementId insertedId = result.InsertedRoot.Id
                ?? throw new InvalidOperationException(
                    $"Inserted component root <{result.InsertedRoot.Tag}> has no id; the insert was aborted " +
                    "before mutating the project.");
            root = ReplaceChildByTag(root, EnumDefinitionsTag, result.EnumDefinitions);
            AppendChild(groupId, result.InsertedRoot);
            return insertedId;
        }

        /// <summary>
        /// Adopts a project-form DTD block for each inserted element type the static registry does not declare, so an
        /// unregistered/custom component still serializes (open-world). The block text is rendered from the
        /// component's structured grammar at this insert boundary — the only place a catalog declaration becomes
        /// <c>.vis</c> block text — via the project rendering (an orphan ATTLIST gets its synthesized
        /// <c>&lt;!ELEMENT tag ANY&gt;</c> line; a catalog-faithful orphan block would make the saved file
        /// unloadable). Registry-known types keep their curated registry block (nothing merged), so this is a no-op
        /// for the standard catalog.
        /// </summary>
        private void MergeNonRegistryBlocks(ProjectElement body, CatalogGrammar grammar)
        {
            if (grammar is null || grammar.Declarations.IsEmpty)
            {
                return;
            }
            ImmutableDictionary<string, string>.Builder builder = inlineDtdBlocks.ToBuilder();
            void Walk(ProjectElement e)
            {
                if (!builder.ContainsKey(e.Tag)
                    && ProjectSchemaRegistry.TryGet(e.Tag) is null
                    && grammar.TryGetDeclaration(e.Tag) is { } declaration)
                {
                    builder[e.Tag] = CatalogDtdEmitter.RenderProjectBlock(declaration);
                }
                foreach (ProjectElement c in e.ChildrenOrEmpty())
                {
                    Walk(c);
                }
            }
            Walk(body);
            inlineDtdBlocks = builder.ToImmutable();
            schemaView = null;   // inline DTD changed → drop the memoized view so the next access reparses
        }

        // ----- resource builders (called by ProductRef) -----

        internal ResourceRef UpsertResourceChild(ElementId parentId, string tag, string name,
            IReadOnlyList<(string Name, string Value)> attrs)
        {
            ProjectElement parent = Require(parentId);
            ProjectElement? existing = parent.ChildrenOrEmpty()
                .FirstOrDefault(c => c.Tag == tag && c.GetAttribute("name") == name);

            if (existing is not null)
            {
                if (existing.Id is not { } existingId)
                {
                    throw new InvalidOperationException(
                        $"Cannot configure <{tag}> '{name}': its id token '{existing.GetAttribute("id")}' is not a " +
                        "parseable _0x id, so the existing resource cannot be addressed for update.");
                }
                Mutate(existingId, e => ApplyAttributes(e, attrs));
                return new ResourceRef(name, existingId);
            }

            return AddResourceChild(parentId, tag, name, attrs);
        }

        /// <summary>
        /// Adds a fresh resource of type <paramref name="tag"/> as the last child of <paramref name="parentId"/> —
        /// always a NEW node (never upserted), so a hand-authored function block may legitimately hold repeat-named
        /// resources (project2's two "Kommatal"/"Scenarie" outputs). Allocates its id off the project counter and
        /// materializes the vendor's freshly-created presentation attributes (icon + <c>#REQUIRED</c> value initials)
        /// before applying <paramref name="attrs"/>, so a caller override / an enum's <c>typedef</c>+<c>inivalue</c>
        /// wins on any name collision. Returns the new resource's live handle.
        /// </summary>
        internal ResourceRef AddResourceChild(ElementId parentId, string tag, string name,
            IReadOnlyList<(string Name, string Value)> attrs)
        {
            RefuseIfLockedTarget(parentId, inclusive: true);   // T003: no variable/pin insert into a locked block
            ElementId id = allocator.Allocate(TypeCode.RequireForTag(tag));
            ProjectElement resource = ApplyAttributes(SimpleElement(tag, id, ("name", name)),
                ResourceMaterialization.NewResourceDefaults(tag));
            resource = ApplyAttributes(resource, attrs);
            AppendChild(parentId, resource);
            return new ResourceRef(name, id);
        }

        internal void EnsureScenesBoundToFirstOutput(ElementId productId)
        {
            ProjectElement product = Require(productId);
            if (product.ChildrenOrEmpty().Any(c => c.Tag == "scenes"))
            {
                return;   // the catalog deep-copy already provides the scenes container
            }
            ProjectElement? output = product.ChildrenOrEmpty().FirstOrDefault(c => c.Tag == "dataline_output");
            if (output?.Id is not { } outputId)
            {
                return;   // nothing to bind scenes to
            }
            ElementId id = allocator.Allocate(TypeCode.RequireForTag("scenes"));
            ProjectElement scenes = SimpleElement("scenes", id,
                ("name", ProductDefinitionBuilder.DefaultScenesName), ("scene_resource", outputId.ToToken()));
            AppendChild(productId, scenes);
        }

        // ----- lookups (called by handles) -----

        internal ElementId? FindChildIdByName(ElementId parentId, string tag, string name) =>
            FindChildIdByName(parentId, t => t == tag, name);

        internal ElementId? FindChildIdByName(ElementId parentId, Func<string, bool> tagMatch, string name)
        {
            ProjectElement parent = Require(parentId);
            if (parent.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            ProjectElement? match = parent.Children.FirstOrDefault(c => tagMatch(c.Tag) && c.GetAttribute("name") == name);
            return match?.Id;
        }

        internal ElementId? FindDescendantIdByName(ElementId rootId, string name, params string[] tags)
        {
            ProjectElement start = Require(rootId);
            return start.FindDescendantOrSelf(e => e.GetAttribute("name") == name
                && (tags.Length == 0 || tags.Contains(e.Tag)))?.Id;
        }

        internal void SetAttributeById(ElementId id, string name, string value) =>
            Mutate(id, element =>
            {
                ElementSchema? schema = SchemaView.TryGet(element.Tag);
                if (schema is not null && schema.FindAttr(name) is null)
                {
                    // Reject at the write, with the target named — the alternative is a commit-time throw far
                    // from the mutation that caused it (canonicalization refuses undeclared attributes).
                    throw new ArgumentException(
                        $"Attribute '{name}' is not declared for <{element.Tag}> (id {id.ToToken()}); " +
                        "the .vis grammar would reject it on save.", nameof(name));
                }
                return element.WithAttribute(name, value);
            });

        /// <summary>Sets <paramref name="name"/> to <paramref name="value"/> on the first descendant of (or including)
        /// <paramref name="parent"/> matching <paramref name="match"/> that resolves to a live handle, if any — the
        /// "find descendant → resolve → set value" micro-pattern the metadata commands share (T029). A no-op when no
        /// matching descendant resolves, matching the callers' original guarded form.</summary>
        internal void SetDescendantAttribute(ProjectElement parent, Func<ProjectElement, bool> match, string name, string value)
        {
            if (parent.DescendantsAndSelf().FirstOrDefault(match) is { Id: { } id }
                && TryResolve(id, out ElementRef? handle))
            {
                handle.SetAttribute(name, value);
            }
        }

        /// <summary>
        /// Resolves a resource handle to its live element, requiring both that the handle carries an id and that the
        /// element still exists in the session — wiring a stale handle into a program/link/scene would persist a
        /// dangling reference. The single resource-id guard the link, scene and program paths route through (review
        /// theme 2 DRY): its id is <c>result.Id!.Value</c>.
        /// </summary>
        internal ProjectElement RequireLive(ResourceRef resource)
        {
            ElementId id = resource.Id ?? throw new InvalidOperationException(
                $"Resource '{resource.Name}' has no allocated id; it is not a live element.");
            return Require(id);
        }

        // ----- generic child authoring (called by ProgramBuilder) -----

        /// <summary>
        /// Allocates a fresh id for <paramref name="tag"/> off the project counter, builds the element with that id
        /// plus <paramref name="attrs"/>, appends it as the last child of <paramref name="parentId"/>, and returns the
        /// new id. The un-upserted counterpart of <see cref="UpsertResourceChild"/> — programs legitimately hold
        /// repeated same-named leaves (two "%P = ON" actions), so every call adds a distinct node.
        /// </summary>
        internal ElementId AllocateChild(ElementId parentId, string tag, params (string Name, string Value)[] attrs)
        {
            RefuseIfLockedTarget(parentId, inclusive: true);   // T003: no insert into a locked block's subtree
            ElementId id = allocator.Allocate(TypeCode.RequireForTag(tag));
            AppendChild(parentId, SimpleElement(tag, id, attrs));
            return id;
        }

        /// <summary>As <see cref="AllocateChild"/>, but inserts the new element at <paramref name="index"/> among
        /// the parent's children (clamped; appends when past the end) instead of last.</summary>
        internal ElementId AllocateChildAt(ElementId parentId, string tag, int index,
            params (string Name, string Value)[] attrs)
        {
            RefuseIfLockedTarget(parentId, inclusive: true);   // T003: no insert into a locked block's subtree
            ElementId id = allocator.Allocate(TypeCode.RequireForTag(tag));
            InsertChildAt(parentId, SimpleElement(tag, id, attrs), index);
            return id;
        }

        /// <summary>Returns the id of the parent's first child with the given tag, or throws when absent.</summary>
        internal ElementId RequireChildId(ElementId parentId, string childTag) =>
            Require(parentId).FindChild(childTag)?.Id
            ?? throw new InvalidOperationException($"Element {parentId.ToToken()} has no <{childTag}> child.");

        /// <summary>Returns the id of the parent's single child with the given tag, or throws unless there is exactly one.</summary>
        internal ElementId RequireSoleChildId(ElementId parentId, string childTag)
        {
            ProjectElement parent = Require(parentId);
            ImmutableArray<ProjectElement> matches = parent.ChildrenOrEmpty()
                .Where(c => c.Tag == childTag).ToImmutableArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Element {parentId.ToToken()} has {matches.Length} <{childTag}> children; expected exactly one.");
            }
            return matches[0].Id ?? throw new InvalidOperationException($"The <{childTag}> child has no id.");
        }

        // ----- tree machinery -----

        // ReciprocalHalfTags / CollectLinkPartners / CollectExternalReciprocalHalves — the reciprocal-half
        // collection primitives — moved to DeleteCascade (T016); CollectIds moved to ProjectTreeOps (T015).

        private ElementId? FindGroupByName(string name)
        {
            ProjectElement? groups = root.FindChild(GroupsTag);
            if (groups is null || groups.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            ProjectElement? match = groups.Children.FirstOrDefault(g => g.Tag == "group" && g.GetAttribute("name") == name);
            if (match is null)
            {
                return null;   // truly absent → the caller seeds a fresh room
            }
            // A name match whose id token is unparseable is unaddressable, not absent: seeding a second same-named
            // room would silently duplicate it (and land later inserts in the wrong one). Mirror UpsertResourceChild.
            return match.Id ?? throw new InvalidOperationException(
                $"Cannot open locality '{name}': its id token '{match.GetAttribute("id")}' is not a parseable _0x id, " +
                "so the existing room cannot be addressed for editing. Repair the id first.");
        }

        private ElementId SeedGroup(string name)
        {
            ProjectElement groups = root.FindChild(GroupsTag)
                ?? throw new InvalidOperationException("The project has no groups container.");
            ElementId id = allocator.Allocate(TypeCode.RequireForTag("group"));
            ProjectElement group = SimpleElement("group", id, ("name", name), ("icon", ResourceMaterialization.RequireIcon("group")));
            root = ReplaceChildByTag(root, GroupsTag, groups with { Children = AppendTo(groups.Children, group) });
            return id;
        }

        internal ProjectElement Require(ElementId id) => FindById(root, id)
            ?? throw new InvalidOperationException($"No element with id {id.ToToken()} in the edit session.");

        private void AppendChild(ElementId parentId, ProjectElement child) =>
            Mutate(parentId, p => p with { Children = AppendTo(p.Children, child) });

        private void Mutate(ElementId id, Func<ProjectElement, ProjectElement> map)
        {
            // One traversal that both finds and replaces the target; an absent id must never silently no-op
            // (stale-handle corruption), so a miss throws exactly as the old fail-fast Require pre-check did.
            ProjectElement updated = ReplaceById(root, id, map, out bool found);
            if (!found)
            {
                throw new InvalidOperationException($"No element with id {id.ToToken()} in the edit session.");
            }
            root = updated;
        }

        // FindById / ReplaceById / RemoveById / ReplaceChildByTag / ApplyAttributes / SimpleElement / AppendTo —
        // the pure immutable-tree primitives — moved to ProjectTreeOps (T015), imported via `using static`.

    }

    /// <summary>
    /// Authoring entry points layered over <see cref="Project"/>.
    /// </summary>
    public static class ProjectEditingExtensions
    {
        /// <summary>
        /// Opens a mutable edit session over a project (just loaded or created) — the deliberate read-to-write
        /// boundary. Usage: <c>project.Edit()</c> → mutate via handles → <see cref="ProjectEditor.ToProject"/> → save.
        /// </summary>
        public static ProjectEditor Edit(this Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return new ProjectEditor(project);
        }
    }
}
