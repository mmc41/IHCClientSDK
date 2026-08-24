using System;
using System.Threading.Tasks;

using Ihc.Vis.Problems;

namespace ihc_openvisual.Services;

/// <summary>
/// THE ONE PLACE the shell decides which SHAPE a raised exception is shown as.
///
/// <para>The problem contract has two child relationships and they are not interchangeable: a
/// <see cref="ProblemChain"/> is one failure restated more precisely, so exactly one sentence is shown; a
/// <see cref="ProblemAggregate"/> is N independent failures, so the head and EVERY item are shown. Rendering
/// either by the other's rule is the defect the two types exist to prevent — one shows a failure twice, the
/// other loses all but one of N findings.</para>
///
/// <para>Every catch site used to compose a chain unconditionally, so an exception carrying an aggregate — today
/// only <c>ProjectValidationException</c>, whose items ARE the findings that explain why the operation stopped —
/// fell through to the shell's generic framing and the findings were never shown at all. The decision belongs
/// here rather than at each site, because a site that has to remember which shape it might receive is a site that
/// will eventually forget.</para>
///
/// <para>NOT a second composition rule (D01): the shapes are still rendered by <c>ProblemPresenter</c>, unchanged.
/// This chooses which of its existing cases applies.</para>
/// </summary>
internal static class RaisedProblemDisplay
{
    /// <summary>
    /// Shows the exception in the shape it carries: its aggregate when it has one, else the shell's framing
    /// narrated over its coded cause.
    /// </summary>
    /// <param name="dialogs">The surface to show it on.</param>
    /// <param name="title">The shell's own framing of the box.</param>
    /// <param name="framing">The shell's coded problem for this site.</param>
    /// <param name="raised">The exception that was caught.</param>
    public static Task ShowAsync(
        IDialogService dialogs, string title, Problem framing, Exception raised)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(raised);
        return raised is IProblemCarrier { Aggregate: { } aggregate }
            ? dialogs.ShowProblemAsync(title, aggregate)
            : dialogs.ShowProblemAsync(title, HostProblems.Narrate(framing, raised));
    }
}
