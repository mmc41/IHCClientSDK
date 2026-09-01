using Ihc.Vis.Model;

namespace Ihc.Vis.Problems
{
    /// <summary>
    /// A CAUSE/DETAIL PAIR: an operation outcome and the ONE condition that caused it, each restating the same
    /// failure at a different precision. <i>Projektet kunne ikke åbnes</i> ← <i>Filen er tom</i>.
    /// <para>
    /// This is the composition rule for the catalogue rows that refuse an operation. The <see cref="Operation"/>
    /// carries the dotted operation code (<c>io.load</c>, <c>io.save</c>, <c>import.catalog</c>,
    /// <c>bridge.download</c>, <c>bridge.upload</c>); the <see cref="Cause"/> keeps its bare published id
    /// (<c>load-empty</c>). So the operation is identifiable without reading the cause, the cause keeps the id
    /// the catalogue published, and no dotted <c>io.load-empty</c> is minted — which would have renamed a
    /// published id.
    /// </para>
    /// <para>
    /// NON-RECURSIVE, deliberately: a <see cref="Problem"/> has no child of its own, so a chain is exactly two
    /// levels and "at most one child per level" is structural rather than a convention.
    /// </para>
    /// </summary>
    /// <param name="Operation">The operation that failed. The less specific of the two.</param>
    /// <param name="Cause">The condition that caused it. The one a user-facing renderer shows.</param>
    /// <remarks>
    /// <b>RENDERING RULE, case 2 of 3 — a cause/detail pair.</b> Exactly ONE sentence reaches the user, and it is
    /// <see cref="Cause"/>'s: the more specific of two statements of the SAME failure. <see cref="Operation"/>'s
    /// message is not shown beside it and not concatenated with it — both levels describe one failure, so showing
    /// both shows the user that failure twice. The operation stays useful all the same: its CODE identifies which
    /// operation failed, for grouping, filtering and the log, without its sentence ever being rendered.
    /// </remarks>
    public sealed record ProblemChain(Problem Operation, Problem Cause);

    /// <summary>
    /// A SET OF AGGREGATE ITEMS: N INDEPENDENT problems, each about a different thing, all of which must be
    /// shown. <i>Projektet har 7 fejl</i>, plus the seven.
    /// <para>
    /// The distinction from <see cref="ProblemChain"/> is not stylistic, and keeping them as two TYPES rather
    /// than one shape with a flag is the point. This type has no most-specific member and no way to reduce
    /// itself to a single problem, so "use the most detailed child" applied to N independent findings — which
    /// would silently discard all but one — does not compile. Rendering a chain as a list is the same defect
    /// from the other end: it shows the user one failure twice.
    /// </para>
    /// </summary>
    /// <param name="Head">
    /// The problem naming the failure as a whole — typically an operation-level code with the item count as a
    /// declared <see cref="ProblemArgumentType.Integer"/> argument.
    /// </param>
    /// <param name="Items">
    /// The independent problems, in the producer's stable order (for validation: document-scan order). Every
    /// item is rendered; none is ever collapsed into the head.
    /// </param>
    /// <remarks>
    /// <b>RENDERING RULE, case 3 of 3 — a set of independent items.</b> <see cref="Head"/> frames the failure as
    /// a whole and EVERY member of <see cref="Items"/> is then rendered as its own complete entry, in order.
    /// Nothing is collapsed into the head, nothing is elided behind a count, and items are never joined into one
    /// sentence: they are about different things, so each needs its own. This is the exact inverse of case 2, and
    /// applying either rule to the other shape is a defect — one shows a failure twice, the other loses all but
    /// one of N findings.
    /// </remarks>
    public sealed record ProblemAggregate(Problem Head, EquatableArray<Problem> Items)
    {
        /// <summary>Adds an item, returning a new aggregate. The receiver is unchanged.</summary>
        /// <param name="item">The independent problem to append, after the items already present.</param>
        public ProblemAggregate With(Problem item) => this with { Items = [.. Items, item] };
    }
}
