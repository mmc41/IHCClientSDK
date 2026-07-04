#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Ihc.Projects
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
            var valueRefs = ImmutableArray.CreateBuilder<(string, ElementId)>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                ElementId valueId = allocator.Allocate(TypeCode.RequireForTag("enum_value"));
                valueElements.Add(SimpleElement("enum_value", valueId,
                    ("name", values[i]), ("index", i.ToString(CultureInfo.InvariantCulture))));
                valueRefs.Add((values[i], valueId));
            }
            ProjectElement def = SimpleElement("enum_definition", defId, ("name", name))
                with { Children = valueElements.ToImmutable() };

            ProjectElement container = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            root = ReplaceChildByTag(root, EnumDefinitionsTag,
                container with { Children = AppendTo(container.Children, def) });

            return new EnumDefinitionRef(name, defId, valueRefs.ToImmutable());
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
        /// that point into its resources (via <see cref="DeleteById"/>). Retired <c>_0x</c> ids are not reused;
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
        public ProjectEditor DeleteById(ElementId id)
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
                        if (IsIdRefAttr(schema, name) && ElementId.TryParse(value, out ElementId target)
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

        private static bool IsIdRefAttr(ElementSchema schema, string name)
        {
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Name == name)
                {
                    return attr.Render == AttrRender.IdRef;
                }
            }
            return false;
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
                ("name", FollowLinkName), ("icon", "_0x47"), ("link", linkToId.ToToken()));
            ProjectElement linkTo = SimpleElement("link_to_resource", linkToId,
                ("name", FollowLinkName), ("icon", "_0x4a"), ("link", linkFromId.ToToken()));

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
            foreach (ProjectElement half in ChildrenOf(fromEl))
            {
                if (half.Tag != "link_from_resource" || half.Id is not { } fromHalfId)
                {
                    continue;
                }
                foreach (ProjectElement partner in ChildrenOf(toEl))
                {
                    if (partner.Tag == "link_to_resource" && partner.Id is { } toHalfId
                        && half.GetAttribute("link") == partner.GetAttribute("id")
                        && partner.GetAttribute("link") == half.GetAttribute("id"))
                    {
                        return (fromHalfId, toHalfId);
                    }
                }
            }
            return null;
        }

        private static IEnumerable<ProjectElement> ChildrenOf(ProjectElement element) =>
            element.Children.IsDefaultOrEmpty ? Enumerable.Empty<ProjectElement>() : element.Children;

        /// <summary>
        /// Clones an existing in-project subtree (the clipboard copy/paste) under a new parent: deep-copies it
        /// through the same transform as a catalog insert — fresh ids off the project counter (each element's
        /// type-code suffix preserved), internal IDREFs remapped through one old→new map, and shared enums reused —
        /// then applies <paramref name="policy"/> to any follow-link half whose reciprocal partner lies outside the
        /// copy. Works for any subtree (locality, product, function block or bare resource). Returns the new root's
        /// id. The source is left untouched.
        /// </summary>
        public ElementId CopySubtree(ElementId sourceId, ElementId targetParentId,
            LinkCopyPolicy policy = LinkCopyPolicy.DropExternal)
        {
            ProjectElement source = Require(sourceId);
            Require(targetParentId);                      // fail fast if the paste target does not exist
            ElementId copyRootId = InsertComponent(targetParentId, source, ImmutableDictionary<string, string>.Empty);
            if (policy == LinkCopyPolicy.DropExternal)
            {
                DropExternalLinkHalves(copyRootId);
            }
            return copyRootId;
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

        private void DropExternalLinkHalves(ElementId copyRootId)
        {
            ProjectElement copy = Require(copyRootId);
            var copyIds = new HashSet<ElementId>();
            CollectIds(copy, copyIds);
            var external = new List<ElementId>();
            CollectExternalLinkHalves(copy, copyIds, external);
            foreach (ElementId halfId in external)
            {
                root = RemoveById(root, halfId);          // structural removal only; the source's own links stay intact
            }
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
        /// significant ancestors (a <c>group</c>, a <c>product_*</c> or a <c>functionblock</c>) followed by the
        /// element's own name, skipping structural containers (<c>inputs</c>/<c>outputs</c>/…). Empty when the id is
        /// absent. Used for the "Link fra…" far-end decoration.
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
            tag is "group" or "functionblock" || tag.StartsWith("product_", StringComparison.Ordinal);

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

        internal ElementId InsertComponent(ElementId groupId, ProjectElement catalogBody,
            ImmutableDictionary<string, string> descriptorBlocks)
        {
            MergeNonRegistryBlocks(catalogBody, descriptorBlocks);
            ProjectElement enumDefinitions = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            InsertResult result = InsertTransform.Insert(catalogBody, allocator, enumDefinitions, SchemaView);
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
        /// Adopts the descriptor's inline-DTD block for each inserted element type the static registry does not
        /// declare, so an unregistered/custom component still serializes (open-world). Registry-known types keep
        /// their curated registry block (nothing merged), so this is a no-op for the standard catalog.
        /// </summary>
        private void MergeNonRegistryBlocks(ProjectElement body, ImmutableDictionary<string, string> descriptorBlocks)
        {
            if (descriptorBlocks is null || descriptorBlocks.IsEmpty)
            {
                return;
            }
            ImmutableDictionary<string, string>.Builder builder = inlineDtdBlocks.ToBuilder();
            void Walk(ProjectElement e)
            {
                if (!builder.ContainsKey(e.Tag)
                    && ProjectSchemaRegistry.TryGet(e.Tag) is null
                    && descriptorBlocks.TryGetValue(e.Tag, out string? block))
                {
                    builder[e.Tag] = block;
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
                ("name", "Scenarier"), ("scene_resource", outputId.ToToken()));
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
            return FindDescendant(start, e => e.GetAttribute("name") == name
                && (tags.Length == 0 || tags.Contains(e.Tag)))?.Id;
        }

        internal void SetAttributeById(ElementId id, string name, string value)
        {
            ProjectElement element = Require(id);
            ElementSchema? schema = SchemaView.TryGet(element.Tag);
            if (schema is not null && !SchemaGuards.HasAttribute(schema, name))
            {
                // Reject at the write, with the target named — the alternative is a commit-time throw far
                // from the mutation that caused it (canonicalization refuses undeclared attributes).
                throw new ArgumentException(
                    $"Attribute '{name}' is not declared for <{element.Tag}> (id {id.ToToken()}); " +
                    "the .vis grammar would reject it on save.", nameof(name));
            }
            root = ReplaceById(root, id, e => e.WithAttribute(name, value));
        }

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

        // Follow-link halves and scene rows both pair reciprocally via their `link` IDREF (spec ch. 06 §6.4,
        // ch. 08): deleting one side must cascade the other, and only elements of these types may be cascaded.
        private static readonly HashSet<string> ReciprocalHalfTags = new(StringComparer.Ordinal)
        {
            "link_from_resource", "link_to_resource",
            "scene_link", "scene_dimmer", "scene_relay", "scene_shutter",
        };

        private static void CollectLinkPartners(ProjectElement element, List<ElementId> partners)
        {
            if (ReciprocalHalfTags.Contains(element.Tag)
                && ElementId.TryParse(element.GetAttribute("link"), out ElementId partner))
            {
                partners.Add(partner);
            }
            if (element.Children.IsDefaultOrEmpty)
            {
                return;
            }
            foreach (ProjectElement child in element.Children)
            {
                CollectLinkPartners(child, partners);
            }
        }

        private static void CollectIds(ProjectElement element, HashSet<ElementId> ids)
        {
            if (element.Id is { } id)
            {
                ids.Add(id);
            }
            if (element.Children.IsDefaultOrEmpty)
            {
                return;
            }
            foreach (ProjectElement child in element.Children)
            {
                CollectIds(child, ids);
            }
        }

        private static void CollectExternalLinkHalves(ProjectElement element, HashSet<ElementId> insideIds, List<ElementId> external)
        {
            if (element.Tag is "link_from_resource" or "link_to_resource"
                && element.Id is { } halfId
                && ElementId.TryParse(element.GetAttribute("link"), out ElementId partner)
                && !insideIds.Contains(partner))
            {
                external.Add(halfId);                      // reciprocal partner lies outside the copied subtree
            }
            if (element.Children.IsDefaultOrEmpty)
            {
                return;
            }
            foreach (ProjectElement child in element.Children)
            {
                CollectExternalLinkHalves(child, insideIds, external);
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
            return match?.Id;
        }

        private ElementId SeedGroup(string name)
        {
            ProjectElement groups = root.FindChild(GroupsTag)
                ?? throw new InvalidOperationException("The project has no groups container.");
            ElementId id = allocator.Allocate(TypeCode.RequireForTag("group"));
            ProjectElement group = SimpleElement("group", id, ("name", name), ("icon", "_0x15"));
            root = ReplaceChildByTag(root, GroupsTag, groups with { Children = AppendTo(groups.Children, group) });
            return id;
        }

        internal ProjectElement Require(ElementId id) => FindById(root, id)
            ?? throw new InvalidOperationException($"No element with id {id.ToToken()} in the edit session.");

        private void AppendChild(ElementId parentId, ProjectElement child) =>
            Mutate(parentId, p => p with { Children = AppendTo(p.Children, child) });

        private void Mutate(ElementId id, Func<ProjectElement, ProjectElement> map)
        {
            Require(id);   // fail fast: mutating an absent id must never silently no-op (stale-handle corruption)
            root = ReplaceById(root, id, map);
        }

        private static ProjectElement? FindById(ProjectElement element, ElementId id)
        {
            if (element.Id == id)
            {
                return element;
            }
            if (element.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            foreach (ProjectElement child in element.Children)
            {
                ProjectElement? found = FindById(child, id);
                if (found is not null)
                {
                    return found;
                }
            }
            return null;
        }

        private static ProjectElement? FindDescendant(ProjectElement element, Func<ProjectElement, bool> predicate)
        {
            if (predicate(element))
            {
                return element;
            }
            if (element.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            foreach (ProjectElement child in element.Children)
            {
                ProjectElement? found = FindDescendant(child, predicate);
                if (found is not null)
                {
                    return found;
                }
            }
            return null;
        }

        private static ProjectElement ReplaceById(ProjectElement element, ElementId id, Func<ProjectElement, ProjectElement> map)
        {
            if (element.Id == id)
            {
                return map(element);
            }
            if (element.Children.IsDefaultOrEmpty)
            {
                return element;
            }
            bool changed = false;
            var builder = ImmutableArray.CreateBuilder<ProjectElement>(element.Children.Length);
            foreach (ProjectElement child in element.Children)
            {
                ProjectElement replaced = ReplaceById(child, id, map);
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
