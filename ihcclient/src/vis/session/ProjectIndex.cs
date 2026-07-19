#nullable enable
using System.Collections.Frozen;
using System.Collections.Generic;
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
        private readonly FrozenDictionary<ElementId, ProjectElement> byId;
        private readonly FrozenDictionary<ElementId, ProjectElement> parentById;

        private ProjectIndex(
            FrozenDictionary<ElementId, ProjectElement> byId,
            FrozenDictionary<ElementId, ProjectElement> parentById)
        {
            this.byId = byId;
            this.parentById = parentById;
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
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                if (child.Id is { } childId)
                {
                    parentById.TryAdd(childId, element);
                }
                Walk(child, byId, parentById);
            }
        }

        /// <summary>The element with the given id, or null when no id-bearing element matches.</summary>
        public ProjectElement? FindById(ElementId id) =>
            byId.TryGetValue(id, out ProjectElement? element) ? element : null;

        /// <summary>The parent element of the id-bearing element with the given id, or null (a root or an absent id).</summary>
        public ProjectElement? FindParent(ElementId id) =>
            parentById.TryGetValue(id, out ProjectElement? parent) ? parent : null;
    }
}
