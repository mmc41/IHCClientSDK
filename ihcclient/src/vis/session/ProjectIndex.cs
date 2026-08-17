#nullable enable
using System.Collections.Frozen;
using System.Collections.Generic;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The per-commit id→element / id→parent lookup substrate (proposal P1a) that a command's
    /// <see cref="ProjectCommand.Evaluate"/> and the Wave-3 drag-over probe share, so legality checks stop paying
    /// repeated O(N) tree walks per pointer event. Build-once/read-many over <c>FrozenDictionary</c> (the engine's
    /// registry idiom); the session rebuilds it once per commit.
    /// </summary>
    internal sealed class ProjectIndex
    {
        private readonly FrozenDictionary<ElementId, ProjectElement> _byId;
        private readonly FrozenDictionary<ElementId, ProjectElement> _parentById;

        private ProjectIndex(
            FrozenDictionary<ElementId, ProjectElement> byId,
            FrozenDictionary<ElementId, ProjectElement> parentById)
        {
            _byId = byId;
            _parentById = parentById;
        }

        /// <summary>Builds the index in a single pre-order walk of the project tree (id-less elements are skipped).
        /// First-wins on a duplicate id, matching the first-match order of <see cref="Project.FindById"/>.</summary>
        public static ProjectIndex Build(Project project)
        {
            var byId = new Dictionary<ElementId, ProjectElement>();
            var parentById = new Dictionary<ElementId, ProjectElement>();
            Walk(project.Root, byId, parentById);
            return new ProjectIndex(byId.ToFrozenDictionary(), parentById.ToFrozenDictionary());
        }

        private static void Walk(
            ProjectElement element,
            Dictionary<ElementId, ProjectElement> byId,
            Dictionary<ElementId, ProjectElement> parentById)
        {
            if (element.Id is { } id)
            {
                byId.TryAdd(id, element);
            }
            foreach (ProjectElement child in element.Children)
            {
                if (child.Id is { } childId)
                {
                    parentById.TryAdd(childId, element);
                }
                Walk(child, byId, parentById);
            }
        }

        /// <summary>The id→element map itself, for a caller that needs the whole set rather than one lookup (the
        /// per-commit change-set diff, which would otherwise re-walk the same tree).</summary>
        public IReadOnlyDictionary<ElementId, ProjectElement> ById => _byId;

        /// <summary>The element with the given id, or null when no id-bearing element matches.</summary>
        public ProjectElement? FindById(ElementId id) =>
            _byId.TryGetValue(id, out ProjectElement? element) ? element : null;

        /// <summary>The parent element of the id-bearing element with the given id, or null (a root or an absent id).</summary>
        public ProjectElement? FindParent(ElementId id) =>
            _parentById.TryGetValue(id, out ProjectElement? parent) ? parent : null;

        /// <summary>
        /// Whether <paramref name="id"/> lies at/within a locked function block — the same T003 rule
        /// <see cref="ProjectEditor.IsWithinLockedBlock"/> answers (and the same
        /// <see cref="ProjectEditor.IsLockedBlock"/> definition of "locked"), but resolved by walking UP this index
        /// instead of running a whole-tree DFS per call. That matters because the menu gates and the drag-over probe
        /// ask it per pointer event, which is exactly the cost this index exists to remove.
        /// <para>The upward walk is keyed on ids, and the only id-less elements a project has are its root and the
        /// top-level metadata blocks — neither of which can be a function block — so it tests the same candidate
        /// ancestors the DFS does. An absent id is nobody's refusal to make, matching the DFS form.</para>
        /// </summary>
        public bool IsWithinLockedBlock(ElementId id, bool inclusive)
        {
            ProjectElement? current = inclusive ? FindById(id) : FindParent(id);
            bool locked = false;
            while (current is not null && !locked)
            {
                locked = ProjectEditor.IsLockedBlock(current);
                current = current.Id is { } currentId ? FindParent(currentId) : null;
            }
            return locked;
        }
    }
}
