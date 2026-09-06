using System.Collections.Generic;

using Ihc.Vis.Model;

namespace Ihc.Vis.Projects
{
    /// <summary>
    /// The id→element and element→parent maps of one immutable project tree, built in a single pre-order walk — the
    /// one lookup substrate in the engine, answering both <see cref="Project.FindById"/>/<see cref="Project.FindParent"/>
    /// and the session's legality gates.
    /// <para>
    /// It exists because those lookups are asked per rendered row, not per operation: a tree projection resolves
    /// every link row's partner pin and then every ancestor of that pin, so a walking implementation multiplies one
    /// O(N) sweep by thousands of callers. The maps answer both in O(1) instead.
    /// </para>
    /// <para>
    /// <b>One resolution, then its parentage.</b> An id resolves to the first element carrying it in document order
    /// — what the depth-first search this replaced returned — and every OTHER answer is derived from that element
    /// rather than looked up beside it. That is what makes the maps substitutable for the engine's own
    /// <c>BuildPath</c> chain: <see cref="FindParent"/> is the parent of <see cref="FindById"/>'s answer, and
    /// <see cref="ParentOf"/> continues the same chain upwards, so a caller combining the element with its parent or
    /// its ancestors is describing ONE position in the tree. A map answering parentage independently could not: on a
    /// duplicated id its first-wins answer is the first element whose CHILDREN carry the id, which may be a
    /// different, shallower branch than the element itself sits in — and a gate that classified a delete from that
    /// pair would refuse, or allow, what the engine does not.
    /// </para>
    /// <para>
    /// <b>Parentage is keyed by ELEMENT, not by id.</b> Ids are not the tree's skeleton: an element without one is
    /// an ordinary open-world shape here — the engine admits element types a project's own DTD declares, the
    /// edit-open guard skips id-less elements rather than refusing them, and <c>ProjectChangeSet</c> rolls an
    /// id-less descendant's change up into its nearest id-bearing ancestor. An upward walk keyed on ids would stop
    /// at the first such element and report the ancestors above it as absent, which for the locked-block
    /// authorization means silently allowing an edit the engine then refuses.
    /// </para>
    /// <para>
    /// <b>Identity.</b> An index is a pure function of an immutable tree, so the <see cref="Root"/> it was built
    /// from is its whole identity: two indexes of one root answer identically and are interchangeable, and a site
    /// handed an index BESIDE a project can check the pair in O(1) through <see cref="RequireBuiltFor"/>. It is
    /// the revision id the model already carries — an immutable root's reference is a content-exact version, so
    /// nothing has to be bumped at a mint site for it to be right.
    /// </para>
    /// <para>
    /// <b>Lifetime.</b> A <see cref="Project"/> holds its index WEAKLY, so hold the instance you were handed for as
    /// long as you want the lookups to stay O(1) between calls: the session pins the current snapshot's, a report
    /// pins one for its own loop, and everything else is a gesture or two and may rebuild. Holding nothing costs a
    /// walk, never a wrong answer — the maps are keyed on an immutable tree's root, so a rebuilt index answers
    /// exactly as the released one did. The consumer rule follows from those two together: RESOLVE through the
    /// snapshot you are asked about, HOLD what you were handed for as long as you want O(1), and never carry a
    /// held index over to another snapshot.
    /// </para>
    /// </summary>
    internal sealed class ProjectIndex
    {
        private readonly Dictionary<ElementId, ProjectElement> _byId;
        private readonly Dictionary<ProjectElement, ProjectElement> _parents;

        private ProjectIndex(
            ProjectElement root,
            Dictionary<ElementId, ProjectElement> byId,
            Dictionary<ProjectElement, ProjectElement> parents)
        {
            Root = root;
            _byId = byId;
            _parents = parents;
        }

        /// <summary>The tree these maps were built from — see <b>Identity</b> above, and
        /// <see cref="RequireBuiltFor"/> for what reads it.</summary>
        internal ProjectElement Root { get; }

        /// <summary>
        /// Refuses unless these maps were built from <paramref name="project"/>'s tree — the O(1) pairing check
        /// a site that holds an index BESIDE a project runs before trusting the two together. Answering from
        /// another tree's maps produces no error of its own: ids resolve or fail to resolve plausibly, so the
        /// wrong answer travels on as a legitimate one.
        /// <para>It lives here rather than at each site because the index is what knows its tree, and because a
        /// throw is the right shape for it ONLY below the session layer: this is a programming defect, not a
        /// refusal of anything a user asked for, so it carries an English diagnostic for the log and the trace
        /// rather than a coded Danish sentence. <paramref name="diagnostic"/> is the caller's, because what has
        /// gone wrong is the caller's to say — a mispaired diff and a session pinning maps of a tree it no longer
        /// serves are different defects.</para>
        /// </summary>
        /// <param name="project">The project these maps are about to be used to answer for.</param>
        /// <param name="diagnostic">What the mismatch means at the call site.</param>
        internal void RequireBuiltFor(Project project, string diagnostic)
        {
            if (!ReferenceEquals(Root, project.Root))
            {
                throw new InvalidOperationException(diagnostic);
            }
        }

        /// <summary>
        /// <see cref="RequireBuiltFor"/> for a caller whose index lives in a NULLABLE field — a pin. Holding no
        /// index at all is the same bookkeeping defect as holding the wrong one, and is refused the same way:
        /// the field is set beside the snapshot it belongs to, so a null beside an open document says the two
        /// were written apart.
        /// </summary>
        /// <param name="pinned">The index the caller believes it is holding for <paramref name="project"/>.</param>
        /// <param name="project">The project those maps are about to be used to answer for.</param>
        /// <param name="diagnostic">What the mismatch means at the call site.</param>
        internal static void RequirePinnedFor(ProjectIndex? pinned, Project project, string diagnostic)
        {
            if (pinned is null)
            {
                throw new InvalidOperationException($"{diagnostic} Nothing is pinned at all.");
            }
            pinned.RequireBuiltFor(project, diagnostic);
        }

        /// <summary>Builds the maps from <paramref name="root"/> in one pre-order walk.</summary>
        internal static ProjectIndex Build(ProjectElement root)
        {
            // Built per snapshot that is queried, so its cost scales with the project and is paid on every edit
            // whose result anybody looks at. The element count is what makes a slow index explicable rather than
            // merely slow. Through the core, because a fault in a walk nobody expects to throw is exactly the one
            // whose silent success is never noticed - and every later lookup depends on this index having been built.
            return Telemetry.Run(nameof(Build), scope =>
            {
                var byId = new Dictionary<ElementId, ProjectElement>();
                // By REFERENCE. ProjectElement is a record whose equality recurses through its whole subtree, so
                // the default comparer would hash every element in O(subtree) and collide two structurally equal
                // siblings onto one entry — which is the parentage of one of them, answered for both.
                var parents = new Dictionary<ProjectElement, ProjectElement>(ReferenceEqualityComparer.Instance);
                Walk(root, parent: null, byId, parents);
                byId.TrimExcess();
                parents.TrimExcess();
                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.ProjectElementCount, byId.Count);
                return new ProjectIndex(root, byId, parents);
            });
        }

        /// <summary>This index's entry point into the instrumentation core.</summary>
        private static readonly OperationTelemetry Telemetry =
            new OperationTelemetry(SdkTelemetryRegistry.Surface, nameof(ProjectIndex));

        private static void Walk(
            ProjectElement element, ProjectElement? parent,
            Dictionary<ElementId, ProjectElement> byId,
            Dictionary<ProjectElement, ProjectElement> parents)
        {
            if (parent is not null)
            {
                // TryAdd on both maps, so BOTH answer with the first position in document order. Structural
                // sharing can put one instance under two parents, and first-wins is then the same choice the
                // id map makes rather than whichever occurrence the walk happened to reach last.
                parents.TryAdd(element, parent);
            }
            if (element.Id is { } id)
            {
                byId.TryAdd(id, element);
            }
            foreach (ProjectElement child in element.Children)
            {
                Walk(child, element, byId, parents);
            }
        }

        /// <summary>Every id the tree carries, for a caller that needs the whole key set rather than one lookup
        /// (the per-commit change-set diff, which would otherwise re-walk the same tree).</summary>
        internal IEnumerable<ElementId> Ids => _byId.Keys;

        /// <summary>The element carrying <paramref name="id"/>, or null when none does.</summary>
        internal ProjectElement? FindById(ElementId id) => _byId.GetValueOrDefault(id);

        /// <summary>The parent of <paramref name="element"/>, or null when it is the tree's root (or not in this
        /// tree at all) — the step an upward walk takes, and the one lookup that does not need the element to
        /// carry an id.</summary>
        internal ProjectElement? ParentOf(ProjectElement element) => _parents.GetValueOrDefault(element);

        /// <summary>The parent of the element carrying <paramref name="id"/>, or null (a root, or an absent id).</summary>
        internal ProjectElement? FindParent(ElementId id) =>
            FindById(id) is { } element ? ParentOf(element) : null;
    }
}
