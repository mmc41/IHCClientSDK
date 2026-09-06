using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The locked-block question, answered from a <see cref="ProjectIndex"/>.
    /// </summary>
    /// <remarks>
    /// An extension in the SESSION layer rather than a member of the index, because "locked" is
    /// <see cref="ProjectEditor"/>'s definition and <see cref="Ihc.Vis.Projects"/> sits BELOW the editing layer —
    /// a member would make the model reach up into the mutation layers it is the substrate for
    /// (<c>ProjectsLayer_ReachesNeitherEditingNorSession</c> is what says so).
    /// </remarks>
    internal static class ProjectIndexLockedBlockExtensions
    {
        /// <inheritdoc cref="IsWithinLockedBlock(ProjectIndex, ProjectElement, bool)"/>
        /// <remarks>An absent id is nobody's refusal to make, matching the form
        /// <see cref="ProjectEditor.IsWithinLockedBlock"/> takes.</remarks>
        internal static bool IsWithinLockedBlock(this ProjectIndex index, ElementId id, bool inclusive) =>
            index.FindById(id) is { } target && index.IsWithinLockedBlock(target, inclusive);

        /// <summary>
        /// Whether <paramref name="target"/> lies at/within a locked function block — the same T003 rule
        /// <see cref="ProjectEditor.IsWithinLockedBlock"/> answers (and the same
        /// <see cref="ProjectEditor.IsLockedBlock"/> definition of "locked"), but resolved by walking UP the index
        /// instead of running a whole-tree DFS per call. That matters because the menu gates and the drag-over probe
        /// ask it per pointer event, which is exactly the cost the index exists to remove.
        /// <para>The walk steps by ELEMENT rather than by id, so it visits exactly the ancestors the engine's
        /// <c>BuildPath</c> chain holds: an id-less element between the block and its descendant is an ordinary
        /// open-world shape, and a walk that stopped there would report a locked subtree as unlocked.</para>
        /// </summary>
        internal static bool IsWithinLockedBlock(this ProjectIndex index, ProjectElement target, bool inclusive)
        {
            ProjectElement? current = inclusive ? target : index.ParentOf(target);
            while (current is not null)
            {
                if (ProjectEditor.IsLockedBlock(current))
                {
                    return true;
                }
                current = index.ParentOf(current);
            }
            return false;
        }
    }
}
