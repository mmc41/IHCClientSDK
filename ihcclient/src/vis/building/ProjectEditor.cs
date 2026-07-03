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

        internal ProjectEditor(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            root = project.Root;
            inlineDtdBlocks = project.InlineDtdBlocks;   // carry the file's captured DTD so open-world edits round-trip
            allocator = IdAllocator.ForProject(project);
        }

        internal IdAllocator Allocator => allocator;

        /// <summary>The schema resolver for this session (the file's own inline DTD first, registry fallback).</summary>
        internal ProjectSchemaView SchemaView => ProjectSchemaView.For(inlineDtdBlocks);

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

            ElementId defId = allocator.Allocate(TypeCodeFor("enum_definition"));
            var valueElements = ImmutableArray.CreateBuilder<ProjectElement>(values.Length);
            var valueRefs = ImmutableArray.CreateBuilder<(string, ElementId)>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                ElementId valueId = allocator.Allocate(TypeCodeFor("enum_value"));
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

            return new EnumDefinitionRef(defId, valueRefs.ToImmutable());
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
        /// project saveable. Retired <c>_0x</c> ids are not reused; a no-op when the id is absent. This is the
        /// id-addressed generic delete that backs every <c>Remove*</c> handle. Returns <c>this</c> for chaining.
        /// </summary>
        public ProjectEditor DeleteById(ElementId id)
        {
            ProjectElement? subtree = FindById(root, id);
            if (subtree is null)
            {
                return this;                             // absent id → nothing to delete
            }
            var partnerIds = new List<ElementId>();
            CollectLinkPartners(subtree, partnerIds);    // (a) partner ids of every link half inside the subtree
            root = RemoveById(root, id);                 // (d) remove the subtree (its own link halves go with it)
            foreach (ElementId partnerId in partnerIds)
            {
                root = RemoveById(root, partnerId);      // (b) remove each external reciprocal half (no-op if internal)
            }
            return this;
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

            ElementId linkFromId = allocator.Allocate(TypeCodeFor("link_from_resource"));   // from-half allocated first
            ElementId linkToId = allocator.Allocate(TypeCodeFor("link_to_resource"));

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
        /// the same orientation — deleting both halves. Returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor Unlink(ResourceRef from, ResourceRef to)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            RemoveLinkHalf(RequireId(from), "link_from_resource", RequireId(to), "link_to_resource");
            RemoveLinkHalf(RequireId(to), "link_to_resource", RequireId(from), "link_from_resource");
            return this;
        }

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
            ProjectElement withCounter = SetAttribute(root, "last_unique_id", allocator.LastUniqueIdToken);
            ProjectSchemaView view = ProjectSchemaView.For(inlineDtdBlocks);
            return new Project(Canonicalizer.Canonicalize(withCounter, view)) { InlineDtdBlocks = inlineDtdBlocks };
        }

        // ----- insert (called by GroupRef) -----

        internal ElementId InsertComponent(ElementId groupId, ProjectElement catalogBody,
            ImmutableDictionary<string, string> descriptorBlocks)
        {
            MergeNonRegistryBlocks(catalogBody, descriptorBlocks);
            ProjectElement enumDefinitions = root.FindChild(EnumDefinitionsTag)
                ?? throw new InvalidOperationException("The project has no enum_definitions container.");
            InsertResult result = InsertTransform.Insert(catalogBody, allocator, enumDefinitions,
                ProjectSchemaView.For(inlineDtdBlocks));
            root = ReplaceChildByTag(root, EnumDefinitionsTag, result.EnumDefinitions);
            AppendChild(groupId, result.InsertedRoot);
            return result.InsertedRoot.Id
                ?? throw new InvalidOperationException("Inserted component root has no id.");
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
        }

        // ----- resource builders (called by ProductRef) -----

        internal ResourceRef UpsertResourceChild(ElementId parentId, string tag, string name,
            IReadOnlyList<(string Name, string Value)> attrs)
        {
            ProjectElement parent = Require(parentId);
            ProjectElement? existing = parent.Children.IsDefaultOrEmpty
                ? null
                : parent.Children.FirstOrDefault(c => c.Tag == tag && c.GetAttribute("name") == name);

            if (existing is { Id: { } existingId })
            {
                Mutate(existingId, e => ApplyAttributes(e, attrs));
                return new ResourceRef(name, existingId);
            }

            ElementId id = allocator.Allocate(TypeCodeFor(tag));
            ProjectElement resource = ApplyAttributes(SimpleElement(tag, id, ("name", name)), attrs);
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
            ElementId id = allocator.Allocate(TypeCodeFor("scenes"));
            ProjectElement scenes = SimpleElement("scenes", id,
                ("name", "Scenarier"), ("scene_resource", outputId.ToToken()));
            AppendChild(productId, scenes);
        }

        // ----- lookups (called by handles) -----

        internal ElementId? FindChildIdByName(ElementId parentId, string tag, string name)
        {
            ProjectElement parent = Require(parentId);
            if (parent.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            ProjectElement? match = parent.Children.FirstOrDefault(c => c.Tag == tag && c.GetAttribute("name") == name);
            return match?.Id;
        }

        internal ElementId? FindDescendantIdByName(ElementId rootId, string name, params string[] tags)
        {
            ProjectElement start = Require(rootId);
            return FindDescendant(start, e => e.GetAttribute("name") == name
                && (tags.Length == 0 || tags.Contains(e.Tag)))?.Id;
        }

        internal void SetAttributeById(ElementId id, string name, string value) =>
            Mutate(id, e => SetAttribute(e, name, value));

        // ----- tree machinery -----

        private static void CollectLinkPartners(ProjectElement element, List<ElementId> partners)
        {
            if (element.Tag is "link_from_resource" or "link_to_resource"
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
            ElementId id = allocator.Allocate(TypeCodeFor("group"));
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
            root = ReplaceById(root, id, map);
        }

        private void RemoveLinkHalf(ElementId ownerId, string halfTag, ElementId partnerId, string partnerTag)
        {
            ProjectElement partner = Require(partnerId);
            string? partnerHalfId = partner.Children.IsDefaultOrEmpty
                ? null
                : partner.Children.FirstOrDefault(c => c.Tag == partnerTag)?.GetAttribute("id");

            Mutate(ownerId, owner =>
            {
                if (owner.Children.IsDefaultOrEmpty)
                {
                    return owner;
                }
                ImmutableArray<ProjectElement> kept = owner.Children
                    .Where(c => !(c.Tag == halfTag && (partnerHalfId is null || c.GetAttribute("link") == partnerHalfId)))
                    .ToImmutableArray();
                return owner with { Children = kept };
            });
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
                result = SetAttribute(result, name, value);
            }
            return result;
        }

        private static ProjectElement SetAttribute(ProjectElement element, string name, string value)
        {
            ImmutableArray<(string Name, string Value)> attrs =
                element.Attrs.IsDefaultOrEmpty ? ImmutableArray<(string, string)>.Empty : element.Attrs;
            for (int i = 0; i < attrs.Length; i++)
            {
                if (attrs[i].Name == name)
                {
                    return element with { Attrs = attrs.SetItem(i, (name, value)) };
                }
            }
            return element with { Attrs = attrs.Add((name, value)) };
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

        private static int TypeCodeFor(string tag) => TypeCode.ForTag(tag)
            ?? throw new InvalidOperationException($"No type code registered for '{tag}'.");
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
