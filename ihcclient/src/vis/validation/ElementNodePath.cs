using System.Collections.Generic;
using System.Text;

using Ihc.Vis.Model;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// WHERE a node is, exactly: a restricted positional path — element names plus same-tag sibling indexes —
    /// built while the <see cref="ProjectElement"/> is still in hand.
    /// <para>
    /// This exists because a finding's locator is not always an identifier. <c>Locate</c> anchors a finding to
    /// <c>element.GetAttribute("id") ?? element.Tag</c>, and that string selects exactly one node for almost every
    /// finding — but not for a token two elements share, not for a token that does not parse, and not for a bare
    /// tag once a second element carries it. For those, the path is the only thing that names the node, and it can
    /// only be built HERE, where the element itself is known. It is never reconstructed from a locator.
    /// </para>
    /// <para>
    /// RESTRICTED means the path uses element names and positions and nothing else: no attribute predicates, no
    /// axes, no functions. That keeps it stable under any change to attribute values and cheap to evaluate, and it
    /// is enough to select exactly one node — the property <see cref="Of"/>'s indexes exist to guarantee.
    /// </para>
    /// <para>
    /// <b>Built once per run, over the run's shared analyses.</b> Both questions it answers are about the whole
    /// document — "how many nodes answer to this locator" and "what is the route to this one" — and a per-site
    /// implementation would re-walk the tree for each of them, once per finding site. That is the exact pattern
    /// <see cref="IProjectAnalyses"/> exists to have deleted, so the tag census comes from
    /// <see cref="IProjectAnalyses.WithTag"/>, the route from <see cref="ITopologyAnalysis.Parent"/>, and the only
    /// index without a shared home — how many elements carry a given parsed <see cref="ElementId"/> — is built
    /// here at most once, on the first question that needs it.
    /// </para>
    /// <para>
    /// Internal on purpose. The path travels out of the engine as a value on <c>FindingLocation</c>; the builder
    /// itself is not something a consumer has any reason to call, because a consumer no longer holds the tree the
    /// path is relative to.
    /// </para>
    /// </summary>
    /// <param name="analyses">The run's shared analyses, over the tree every path is relative to.</param>
    internal sealed class ElementNodePath(IProjectAnalyses analyses)
    {
        /// <summary>
        /// How many elements carry each parsed id, built at most once per run and only if asked.
        /// <para>
        /// Deliberately NOT <see cref="IIdAnalysis.DuplicateTokenGroups"/>, which is the answer to a neighbouring
        /// but different question: that one collides RAW TOKENS, first-holder-wins, because that is what the
        /// duplicate-id rule reports on. The locator question is about what an id-index lookup would select, which
        /// is keyed by the PARSED id — two spellings of one id are one key there and two keys in the token map.
        /// </para>
        /// </summary>
        private Dictionary<ElementId, int>? holdersById;

        /// <summary>The tree every path is relative to; the run's element list is pre-order, so its head is the root.</summary>
        private ProjectElement Root => analyses.Elements[0];

        /// <summary>
        /// The path from the run's root to <paramref name="target"/>, or <c>null</c> when the target is not in
        /// that tree.
        /// <para>
        /// A step carries a 1-based index only where a sibling shares its tag, so an element with no same-tag
        /// sibling reads as its bare name. The root is the base case and is always its bare tag: it has no parent
        /// to be indexed within.
        /// </para>
        /// <para>
        /// Walks UP from the target rather than searching down from the root: the parent map is already built, so
        /// the route costs the target's depth instead of a scan of the document. Reference identity, not value
        /// equality, is what "this node" means — <see cref="ProjectElement"/> is a record, so two structurally
        /// identical siblings are equal by value and a value-matched walk would hand the second one the first
        /// one's path. That is the exact defect an exact node path exists to close. The parent map is
        /// reference-keyed, which is what makes the climb carry that meaning; a node from another tree is not a
        /// key in it and so reports no parent.
        /// </para>
        /// </summary>
        /// <param name="target">The node to locate, matched by REFERENCE.</param>
        internal string? Of(ProjectElement target)
        {
            ProjectElement root = Root;
            if (ReferenceEquals(target, root))
            {
                return "/" + root.Tag;
            }

            List<string> steps = [];
            ProjectElement current = target;
            while (!ReferenceEquals(current, root))
            {
                if (analyses.Topology.Parent(current) is not { } parent)
                {
                    return null;   // not in this tree: no parent, and not the root either
                }

                steps.Add(Step(parent, current));
                current = parent;
            }

            var path = new StringBuilder().Append('/').Append(root.Tag);
            for (int i = steps.Count - 1; i >= 0; i--)
            {
                path.Append('/').Append(steps[i]);
            }

            return path.ToString();
        }

        /// <summary>
        /// The path to <paramref name="target"/>, but only when the locator the finding carries does not select
        /// exactly one node; <c>null</c> when the locator already identifies it.
        /// <para>
        /// The locator is the element's <c>id</c> token when it has one and its tag otherwise, so the question has
        /// two forms and this decides which applies from the element, never from the locator string:
        /// </para>
        /// <list type="bullet">
        /// <item><description>An <c>id</c> token is an ID-INDEX lookup. A well-formed token exactly one element
        /// carries selects it; a token TWO carry selects neither; and a MALFORMED token is not a key any index
        /// holds, so it selects nothing. Both failures are ambiguity, from opposite directions.</description></item>
        /// <item><description>A bare tag is a TAG COUNT over the tree. One such node means the tag already selects
        /// it. This half is not optional even though the corpus currently witnesses only single-node cases: a
        /// second node carrying the tag is what makes the first one's locator stop selecting.</description></item>
        /// </list>
        /// <para>
        /// Note what this deliberately does NOT read: <see cref="ProjectElement.Id"/> being null. That is true both
        /// of an element with no <c>id</c> attribute at all — the document root, an unrecognized element — and of
        /// one whose token is malformed, and only the second is ambiguous. Presence of the ATTRIBUTE picks the
        /// branch; parseability decides within it.
        /// </para>
        /// </summary>
        /// <param name="target">The element the finding is anchored to.</param>
        internal string? WhenLocatorIsAmbiguous(ProjectElement target) =>
            SelectedBy(target) == 1 ? null : Of(target);

        /// <summary>How many nodes the locator of <paramref name="target"/> selects, as a set lookup per site.</summary>
        private int SelectedBy(ProjectElement target)
        {
            string? token = target.GetAttribute("id");
            if (token is null)
            {
                return analyses.WithTag(target.Tag).Length;
            }

            // A malformed token is not a key, so nothing answers to it. Counting holders of the raw string instead
            // would find the element itself and wrongly call it identified.
            if (ElementId.ParseOrNull(token) is not { } id)
            {
                return 0;
            }

            holdersById ??= CountHoldersById();
            return holdersById.TryGetValue(id, out int holders) ? holders : 0;
        }

        private Dictionary<ElementId, int> CountHoldersById()
        {
            Dictionary<ElementId, int> holders = [];
            foreach (ProjectElement element in analyses.Elements)
            {
                if (element.Id is { } id)
                {
                    holders[id] = holders.TryGetValue(id, out int seen) ? seen + 1 : 1;
                }
            }

            return holders;
        }

        /// <summary>
        /// One step: the child's tag, plus its 1-based position among the parent's SAME-TAG children when more
        /// than one of them shares it. Counting all siblings instead would renumber a step whenever an unrelated
        /// sibling of another tag was added or removed.
        /// </summary>
        private static string Step(ProjectElement parent, ProjectElement child)
        {
            int position = 0;
            int sameTag = 0;
            foreach (ProjectElement sibling in parent.Children)
            {
                if (sibling.Tag != child.Tag)
                {
                    continue;
                }

                sameTag++;
                if (ReferenceEquals(sibling, child))
                {
                    position = sameTag;
                }
            }

            return sameTag > 1 ? $"{child.Tag}[{position}]" : child.Tag;
        }
    }
}
