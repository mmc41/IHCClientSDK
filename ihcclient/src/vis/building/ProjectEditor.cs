#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        /// <summary>Gets the named locality (room), seeding it if necessary, and returns its live handle.</summary>
        public GroupRef Group(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = FindGroupByName(name) ?? SeedGroup(name);
            return new GroupRef(this, id);
        }

        /// <summary>
        /// Removes a locality (room) and everything in it (and any links to/from its resources). Retired
        /// <c>_0x</c> ids are not reused; returns <c>this</c> for optional chaining.
        /// </summary>
        public ProjectEditor RemoveGroup(GroupRef group)
        {
            ArgumentNullException.ThrowIfNull(group);
            RemoveSubtree(group.Id);
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

        internal void RemoveSubtree(ElementId id)
        {
            root = RemoveById(root, id);
        }

        // ----- tree machinery -----

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

        private ProjectElement Require(ElementId id) => FindById(root, id)
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
