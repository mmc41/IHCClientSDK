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
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// The mutable edit session over an immutable <see cref="Project"/> — the authoring (write) surface a GUI
    /// drives. Open it with <c>project.Edit()</c>, mutate through live handles (<see cref="Group"/> → products /
    /// function blocks / resources; <see cref="Link"/>/<see cref="Unlink"/>; the <c>Remove*</c> methods), then call
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

        internal ProjectEditor(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            root = project.Root;
            inlineDtdBlocks = project.InlineDtdBlocks;   // carry the file's captured DTD so open-world edits round-trip
            allocator = IdAllocator.ForProject(project);
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
                if (!element.Children.IsDefaultOrEmpty)
                {
                    foreach (ProjectElement child in element.Children)
                    {
                        Walk(child);
                    }
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
                    ("name", values[i]), ("index", i.ToString(CultureInfo.InvariantCulture))));
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

            string defToken = definition.Typedef;
            ProjectElement container = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            ProjectElement def = container.ChildrenOrEmpty()
                .FirstOrDefault(c => c.GetAttribute("id") == defToken)
                ?? throw new InvalidOperationException(
                    $"Enum definition '{defToken}' is no longer part of the project.");
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
                    ("name", values[i]), ("index", (existing + i).ToString(CultureInfo.InvariantCulture))));
            }
            ProjectElement updated = def with { Children = def.ChildrenOrEmpty().Concat(appended).ToImmutableArray() };
            root = ReplaceChildByTag(root, EnumDefinitionsTag, container with
            {
                Children = container.ChildrenOrEmpty().Select(c => ReferenceEquals(c, def) ? updated : c).ToImmutableArray(),
            });
            return ToEnumRef(updated);
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
            ProjectElement container = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");

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

        private ProjectEditor UpdateMetadataChild(string tag, IReadOnlyList<(string Name, string Value)> attributes)
        {
            ProjectElement block = root.FindChild(tag)
                ?? throw new InvalidOperationException($"The project has no <{tag}> metadata element.");
            foreach ((string name, string value) in attributes)
            {
                block = block.WithAttribute(name, value);
            }
            root = ReplaceChildByTag(root, tag, block);
            return this;
        }

        /// <summary>
        /// Opens a <see cref="ProgramBuilder"/> over an existing <c>program_simple</c> (addressed by id) to author its
        /// events and nested logic by hand — the id-addressed program-authoring entry a GUI drives after selecting a
        /// program node. The target must be a <c>program_simple</c> owning the <c>events</c>/<c>actions</c> containers
        /// an empty function block's <c>fb.def</c> skeleton provides.
        /// </summary>
        public ProgramBuilder Program(ElementId programSimpleId)
        {
            ProjectElement program = Require(programSimpleId);
            if (program.Tag != "program_simple")
            {
                throw new InvalidOperationException(
                    $"Element {programSimpleId.ToToken()} is a <{program.Tag}>, not a <program_simple>.");
            }
            return new ProgramBuilder(this, programSimpleId);
        }

        /// <summary>
        /// Opens a <see cref="ConditionsGroupRef"/> over an existing <c>conditions</c> group (addressed by id) — the
        /// id-addressed entry a GUI drives after selecting a Betingelser node in a loaded project (US-029: OR/AND
        /// toggle, add condition rows, add nested logic groups).
        /// </summary>
        public ConditionsGroupRef ConditionsGroup(ElementId conditionsId)
        {
            ProjectElement conditions = Require(conditionsId);
            if (conditions.Tag != "conditions")
            {
                throw new InvalidOperationException(
                    $"Element {conditionsId.ToToken()} is a <{conditions.Tag}>, not a <conditions>.");
            }
            return new ConditionsGroupRef(this, conditionsId);
        }

        /// <summary>
        /// Resolves an id to a generic <see cref="ElementRef"/> handle — the id-addressed, write-side counterpart
        /// of <see cref="Project.FindById"/> and the foundation of a GUI selection model. Unlike the name-addressed
        /// <see cref="Group"/>/<see cref="GroupRef.Product"/>/<see cref="GroupRef.FunctionBlock"/> lookups it
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
                candidate = CascadeReferencingRows(candidate, deletedIds);
            }
            List<string> dangling = FindDanglingReferences(candidate, deletedIds);
            if (dangling.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Deleting {id.ToToken()} would leave dangling references: {string.Join("; ", dangling)}. " +
                    "Delete or rewire the referring elements first.");
            }
            root = candidate;
            return this;
        }

        // The vendor US-009 reference cascade (ENG2-A5, §18 M-B = row-only, any-link-slot): every
        // action/condition/event row whose link1 or link2 points into the deleted set is removed WHOLE — its
        // embedded operand children go with it — while parent groups stay (emptied containers survive). Fixpoint
        // because a removed row's own ids join the set and may be referenced by further rows; anything the capture
        // does not pin (scenes bindings, enum typedefs, case criteria) is left for the strict guard to refuse.
        private ProjectElement CascadeReferencingRows(ProjectElement tree, HashSet<ElementId> deletedIds)
        {
            bool removedAny = true;
            while (removedAny)
            {
                removedAny = false;
                var rows = new List<ElementId>();
                void Walk(ProjectElement element)
                {
                    if (element.Tag is "action" or "condition" or "event" && element.Id is { } rowId
                        && (SlotHits(element, "link1", deletedIds) || SlotHits(element, "link2", deletedIds)))
                    {
                        rows.Add(rowId);    // the whole row goes; no need to look inside it
                    }
                    else if (!element.Children.IsDefaultOrEmpty)
                    {
                        foreach (ProjectElement child in element.Children)
                        {
                            Walk(child);
                        }
                    }
                }
                Walk(tree);
                foreach (ElementId rowId in rows)
                {
                    if (FindById(tree, rowId) is { } row)
                    {
                        CollectIds(row, deletedIds);
                        tree = RemoveById(tree, rowId);
                        removedAny = true;
                    }
                }
            }
            return tree;
        }

        private static bool SlotHits(ProjectElement row, string slot, HashSet<ElementId> deletedIds) =>
            ElementId.TryParse(row.GetAttribute(slot), out ElementId target) && deletedIds.Contains(target);

        private List<string> FindDanglingReferences(ProjectElement tree, HashSet<ElementId> deletedIds)
        {
            var hits = new List<string>();
            void Walk(ProjectElement element)
            {
                ElementSchema? schema = SchemaView.TryGet(element.Tag);
                if (schema is not null && !element.Attrs.IsDefaultOrEmpty)
                {
                    foreach ((string name, string value) in element.Attrs)
                    {
                        if (schema.IsIdRef(name) && ElementId.TryParse(value, out ElementId target)
                            && deletedIds.Contains(target))
                        {
                            hits.Add($"<{element.Tag}> {(element.Id is { } eid ? eid.ToToken() : "?")} {name}='{value}'");
                        }
                    }
                }
                if (!element.Children.IsDefaultOrEmpty)
                {
                    foreach (ProjectElement child in element.Children)
                    {
                        Walk(child);
                    }
                }
            }
            Walk(tree);
            return hits;
        }

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
            ElementId fromId = RequireId(from);
            ElementId toId = RequireId(to);
            Require(fromId);   // both ends must exist before any id is allocated or a half appended:
            Require(toId);     // a stale handle must fail here, not half-write a link

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
        /// Removes the reciprocal follow-link between two live resources — the inverse of <see cref="Link"/> with
        /// the same orientation — deleting exactly the two halves of that pair. Throws when the resources are not
        /// follow-linked in this orientation (nothing is mutated then), so a stale or mistaken unlink can never
        /// silently delete other links. Returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor Unlink(ResourceRef from, ResourceRef to)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            ProjectElement fromEl = Require(RequireId(from));
            ProjectElement toEl = Require(RequireId(to));

            if (FindReciprocalPair(fromEl, toEl) is not { } pair)
            {
                throw new InvalidOperationException(
                    $"Resources '{from.Name}' and '{to.Name}' are not follow-linked in this orientation; nothing to unlink.");
            }
            root = RemoveById(root, pair.FromHalf);
            root = RemoveById(root, pair.ToHalf);
            return this;
        }

        /// <summary>
        /// The first mutually-reciprocal follow-link pair between the two resources (the from-half on the source,
        /// the to-half on the sink, each pointing at the other), or <c>null</c> when no such pair exists. Matching
        /// is by exact reciprocity — never "first half of the tag" — so multi-link owners and shared sinks resolve
        /// to the requested pair only.
        /// </summary>
        private static (ElementId FromHalf, ElementId ToHalf)? FindReciprocalPair(ProjectElement fromEl, ProjectElement toEl)
        {
            foreach (ProjectElement half in fromEl.ChildrenOrEmpty())
            {
                if (half.Tag != "link_from_resource" || half.Id is not { } fromHalfId)
                {
                    continue;
                }
                foreach (ProjectElement partner in toEl.ChildrenOrEmpty())
                {
                    // Reciprocity by parsed ElementId (not raw token text), matching GetLinks / ResolveLinkOpposite /
                    // the DeleteById cascade: a foreign file's non-canonical spelling (leading zeros, case) of an
                    // otherwise-reciprocal link must still resolve, not throw "not follow-linked".
                    if (partner.Tag == "link_to_resource" && partner.Id is { } toHalfId
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
            ElementId pinId = RequireId(sceneOutput);
            ProjectElement pin = Require(pinId);      // both ends must exist before any id is allocated or a half
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

        /// <summary>
        /// Removes the scene membership between an FB scene output pin and a product's scenes container — the
        /// inverse of <see cref="LinkScene"/> — deleting exactly the two halves of that pair. Throws when the two
        /// are not scene-linked (nothing is mutated then), so a stale or mistaken unlink can never silently delete
        /// other memberships. Returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor UnlinkScene(ResourceRef sceneOutput, ScenesRef target)
        {
            ArgumentNullException.ThrowIfNull(sceneOutput);
            ArgumentNullException.ThrowIfNull(target);
            ProjectElement pin = Require(RequireId(sceneOutput));
            ProjectElement scenes = Require(target.Id);

            if (FindScenePair(scenes, pin) is not { } pair)
            {
                throw new InvalidOperationException(
                    $"Resource '{sceneOutput.Name}' and scenes container '{target.Name}' are not scene-linked; " +
                    "nothing to unlink.");
            }
            root = RemoveById(root, pair.Member);
            root = RemoveById(root, pair.SceneLink);
            return this;
        }

        // The scene-capable output families with pinned member kinds (US-024, ch. 08 §8.4): relays/sockets take
        // scene_relay, dimmer regulation takes scene_dimmer. Unknown families stay permissive (the open-world
        // CanInsert convention) — only a known mismatch is a hard error.
        private static string? PinnedMemberTagFor(string boundOutputTag) => boundOutputTag switch
        {
            "dataline_output" or "airlink_relay" => "scene_relay",
            "airlink_dimming" => "scene_dimmer",
            _ => null,
        };

        private void RequireSceneKindMatch(ProjectElement scenes, ScenesRef target, SceneValue value)
        {
            if (ElementId.TryParse(scenes.GetAttribute("scene_resource"), out ElementId boundId)
                && FindById(root, boundId) is { } bound
                && PinnedMemberTagFor(bound.Tag) is { } pinned
                && pinned != value.MemberTag)
            {
                throw new InvalidOperationException(
                    $"Scenes container '{target.Name}' is bound to a <{bound.Tag}> output, which takes {pinned} " +
                    $"members; a {value.MemberTag} value cannot be linked here.");
            }
        }

        /// <summary>
        /// The first mutually-reciprocal scene pair between the container and the pin (the member row inside the
        /// scenes container, the <c>scene_link</c> inside the pin, each pointing at the other), or <c>null</c> when
        /// no such pair exists. Matching mirrors <see cref="FindReciprocalPair"/>: exact reciprocity by parsed
        /// <see cref="ElementId"/>, so multi-membership containers and shared pins resolve to the requested pair only.
        /// </summary>
        private static (ElementId Member, ElementId SceneLink)? FindScenePair(ProjectElement scenes, ProjectElement pin)
        {
            foreach (ProjectElement member in scenes.ChildrenOrEmpty())
            {
                if (!ReciprocalTags.SceneMemberTags.Contains(member.Tag) || member.Id is not { } memberId)
                {
                    continue;
                }
                foreach (ProjectElement partner in pin.ChildrenOrEmpty())
                {
                    if (partner.Tag == "scene_link" && partner.Id is { } sceneLinkId
                        && ElementId.TryParse(member.GetAttribute("link"), out ElementId memberLink) && memberLink == sceneLinkId
                        && ElementId.TryParse(partner.GetAttribute("link"), out ElementId partnerLink) && partnerLink == memberId)
                    {
                        return (memberId, sceneLinkId);
                    }
                }
            }
            return null;
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
            if (FindById(subtree, targetParentId) is not null)
            {
                throw new InvalidOperationException(
                    $"Cannot move {sourceId.ToToken()} into itself or its own descendant {targetParentId.ToToken()}.");
            }
            root = RemoveById(root, sourceId);            // detach (ids untouched)
            InsertChildAt(targetParentId, subtree, index);
            return this;
        }

        private void InsertChildAt(ElementId parentId, ProjectElement child, int? index) =>
            Mutate(parentId, parent =>
            {
                ImmutableArray<ProjectElement> children = parent.Children.IsDefaultOrEmpty
                    ? ImmutableArray<ProjectElement>.Empty
                    : parent.Children;
                int at = index is { } i ? Math.Clamp(i, 0, children.Length) : children.Length;
                return parent with { Children = children.Insert(at, child) };
            });

        /// <summary>
        /// Returns a copy of <paramref name="source"/> with every reciprocal half (follow-link half or scene row)
        /// whose partner lies outside the subtree removed — applied to the source body <b>before</b> the clone
        /// allocates ids, so a dropped half consumes no id (matching the vendor paste for follow-links; the scene
        /// extension is SDK-defined under the same rule, no parity capture yet). Internal pairs are left for
        /// <see cref="InsertTransform"/> to deep-copy and remap.
        /// </summary>
        private static ProjectElement DropExternalReciprocalHalves(ProjectElement source)
        {
            var insideIds = new HashSet<ElementId>();
            CollectIds(source, insideIds);
            var external = new List<ElementId>();
            CollectExternalReciprocalHalves(source, insideIds, external);
            ProjectElement pruned = source;
            foreach (ElementId halfId in external)
            {
                pruned = RemoveById(pruned, halfId);
            }
            return pruned;
        }

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
            if (!resource.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement child in resource.Children)
                {
                    if (child.Tag is "link_from_resource" or "link_to_resource"
                        && child.Id is { } rowId
                        && ElementId.TryParse(child.GetAttribute("link"), out ElementId partner))
                    {
                        links.Add(new LinkInfo(rowId, child.Tag, partner));
                    }
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

        private static ProjectElement? FindParentOf(ProjectElement element, ElementId childId)
        {
            if (element.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            foreach (ProjectElement child in element.Children)
            {
                if (child.Id == childId)
                {
                    return element;
                }
                ProjectElement? found = FindParentOf(child, childId);
                if (found is not null)
                {
                    return found;
                }
            }
            return null;
        }

        private static bool BuildPath(ProjectElement element, ElementId targetId, List<ProjectElement> chain)
        {
            chain.Add(element);
            if (element.Id == targetId)
            {
                return true;
            }
            if (!element.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement child in element.Children)
                {
                    if (BuildPath(child, targetId, chain))
                    {
                        return true;
                    }
                }
            }
            chain.RemoveAt(chain.Count - 1);
            return false;
        }

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
                if (!e.Children.IsDefaultOrEmpty)
                {
                    foreach (ProjectElement c in e.Children)
                    {
                        Walk(c);
                    }
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
            ProjectElement? existing = parent.Children.IsDefaultOrEmpty
                ? null
                : parent.Children.FirstOrDefault(c => c.Tag == tag && c.GetAttribute("name") == name);

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
            if (!product.Children.IsDefaultOrEmpty && product.Children.Any(c => c.Tag == "scenes"))
            {
                return;   // the catalog deep-copy already provides the scenes container
            }
            ProjectElement? output = product.Children.IsDefaultOrEmpty
                ? null
                : product.Children.FirstOrDefault(c => c.Tag == "dataline_output");
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

        /// <summary>
        /// Resolves a live resource handle to its id, requiring both that the handle carries an id and that the
        /// element still exists in the session — wiring a stale handle into a program would persist a dangling
        /// reference.
        /// </summary>
        internal ElementId RequireLive(ResourceRef resource)
        {
            ElementId id = resource.Id ?? throw new InvalidOperationException(
                $"Resource '{resource.Name}' has no allocated id; it cannot be wired into a program.");
            Require(id);
            return id;
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
            ElementId id = allocator.Allocate(TypeCode.RequireForTag(tag));
            AppendChild(parentId, SimpleElement(tag, id, attrs));
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
            ImmutableArray<ProjectElement> matches = parent.Children.IsDefaultOrEmpty
                ? ImmutableArray<ProjectElement>.Empty
                : parent.Children.Where(c => c.Tag == childTag).ToImmutableArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Element {parentId.ToToken()} has {matches.Length} <{childTag}> children; expected exactly one.");
            }
            return matches[0].Id ?? throw new InvalidOperationException($"The <{childTag}> child has no id.");
        }

        // ----- tree machinery -----

        // The reciprocal-pair tags (follow-link halves + scene rows, spec ch. 06 §6.4, ch. 08) — sourced from the
        // schema layer so this editor's delete cascade, the validator's bijection checks and the copy-prune all read
        // one definition; only elements of these types may be cascaded on a delete.
        private static readonly IReadOnlySet<string> ReciprocalHalfTags = ReciprocalTags.All;

        private static void CollectLinkPartners(ProjectElement element, List<ElementId> partners)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                if (ReciprocalHalfTags.Contains(e.Tag)
                    && ElementId.TryParse(e.GetAttribute("link"), out ElementId partner))
                {
                    partners.Add(partner);
                }
            }
        }

        private static void CollectIds(ProjectElement element, HashSet<ElementId> ids)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                if (e.Id is { } id)
                {
                    ids.Add(id);
                }
            }
        }

        private static void CollectExternalReciprocalHalves(ProjectElement element, HashSet<ElementId> insideIds, List<ElementId> external)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                // A null-token link is an unwired row, not an external one — the validator's scene-bijection rule
                // deems that a legitimate authored state, so the prune must not make it vanish on copy.
                if (ReciprocalTags.All.Contains(e.Tag)
                    && e.Id is { } halfId
                    && e.GetAttribute("link") is { } linkToken && linkToken != ElementId.NullToken
                    && ElementId.TryParse(linkToken, out ElementId partner)
                    && !insideIds.Contains(partner))
                {
                    external.Add(halfId);                  // reciprocal partner lies outside the copied subtree
                }
            }
        }

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

        private static ProjectElement? FindById(ProjectElement element, ElementId id) =>
            element.FindDescendantOrSelf(e => e.Id == id);

        private static ProjectElement ReplaceById(ProjectElement element, ElementId id,
            Func<ProjectElement, ProjectElement> map, out bool found)
        {
            if (element.Id == id)
            {
                found = true;
                return map(element);
            }
            if (element.Children.IsDefaultOrEmpty)
            {
                found = false;
                return element;
            }
            bool changed = false;
            found = false;
            var builder = ImmutableArray.CreateBuilder<ProjectElement>(element.Children.Length);
            foreach (ProjectElement child in element.Children)
            {
                if (found)
                {
                    builder.Add(child);   // ids are unique — once the target is replaced, the remaining siblings copy verbatim
                    continue;
                }
                ProjectElement replaced = ReplaceById(child, id, map, out found);
                changed |= !ReferenceEquals(replaced, child);
                builder.Add(replaced);
            }
            return changed ? element with { Children = builder.ToImmutable() } : element;
        }

        private static ProjectElement RemoveById(ProjectElement element, ElementId id)
        {
            if (element.Children.IsDefaultOrEmpty)
            {
                return element;
            }
            var builder = ImmutableArray.CreateBuilder<ProjectElement>();
            bool changed = false;
            foreach (ProjectElement child in element.Children)
            {
                if (child.Id == id)
                {
                    changed = true;
                    continue;
                }
                ProjectElement kept = RemoveById(child, id);
                changed |= !ReferenceEquals(kept, child);
                builder.Add(kept);
            }
            return changed ? element with { Children = builder.ToImmutable() } : element;
        }

        private static ProjectElement ReplaceChildByTag(ProjectElement parent, string tag, ProjectElement replacement)
        {
            if (parent.Children.IsDefaultOrEmpty)
            {
                return parent;
            }
            ImmutableArray<ProjectElement> children = parent.Children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].Tag == tag)
                {
                    return parent with { Children = children.SetItem(i, replacement) };
                }
            }
            return parent;
        }

        private static ProjectElement ApplyAttributes(ProjectElement element, IReadOnlyList<(string Name, string Value)> attrs)
        {
            ProjectElement result = element;
            foreach ((string name, string value) in attrs)
            {
                result = result.WithAttribute(name, value);
            }
            return result;
        }

        private static ProjectElement SimpleElement(string tag, ElementId id, params (string Name, string Value)[] attrs)
        {
            var bag = ImmutableArray.CreateBuilder<(string, string)>(attrs.Length + 1);
            bag.Add(("id", id.ToToken()));
            bag.AddRange(attrs);
            return new ProjectElement(tag, id, bag.ToImmutable(), ImmutableArray<ProjectElement>.Empty);
        }

        private static ImmutableArray<ProjectElement> AppendTo(ImmutableArray<ProjectElement> children, ProjectElement child) =>
            (children.IsDefaultOrEmpty ? ImmutableArray<ProjectElement>.Empty : children).Add(child);

        private static ElementId RequireId(ResourceRef resource) => resource.Id
            ?? throw new InvalidOperationException($"Resource '{resource.Name}' has no allocated id; it cannot be linked.");
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
