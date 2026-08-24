using System.Collections.Generic;
using System.Linq;
using Ihc.Vis.Problems;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// T040: THE shell's one presentation path for the SDK's coded problem contract — every user-facing message the
/// application shows is rendered here, whether the code is SDK-owned or (from T041) host-owned. One contract, one
/// form, one place: a host family is an extension of the vocabulary, never a second error-reporting system inside
/// the GUI (R16 REV 4), and the shell does not get to choose per site (R16 REV 5).
///
/// <para><b>What is rendered.</b> The Danish <see cref="Problem.Message"/>, whole and as it stands, with the
/// problem's IDENTITY as a subordinate bracketed suffix after it — never a prefix, because the one published
/// counter-argument to rendering codes is precisely that they displace the message (R18(a)). A problem carrying no
/// code renders its message alone rather than an empty bracket pointing at nothing.</para>
///
/// <para><b>What is not.</b> The English <see cref="Problem.Diagnostic"/> never appears beside the Danish message
/// (ARCHITECTURE.md invariant 10) — a caller that wants it logs it. Nor is anything rendered about who OWNS the
/// code: the owner is the code's own first segment (<see cref="ProblemCode.Family"/>), so there is no owner marker
/// here for a site to get wrong.</para>
///
/// <para><b>Where argument binding lives — deliberately not here.</b> A problem's declared arguments are bound
/// into its catalogue entry's Danish template by the entry itself, at the producer, which is the ONLY text
/// assembly the design permits. This path therefore takes the message it is given rather than re-deriving it: a
/// second binder here would be a second answer to "what does this code say", and for the sites that interpolate at
/// the raise site it would be a WRONG one — their problem carries a finished sentence and no arguments at all.</para>
///
/// <para><b>Composition.</b> The three cases are the rule stated on the SDK types themselves, applied unchanged:
/// a bare <see cref="Problem"/> is one entry; a <see cref="ProblemChain"/> renders its CAUSE, once (both levels
/// state one failure, so showing both shows it twice — the operation's code stays available for the log); a
/// <see cref="ProblemAggregate"/> renders its head and then EVERY item, in the producer's order, each as its own
/// complete entry. Applying either composition rule to the other shape is the defect the two types exist to
/// prevent.</para>
///
/// <para>Avalonia-free on purpose, like the other presentation helpers here: it is text, and it is pinned by
/// <c>ProblemPresentationTests</c> in the headless suite. Per D06 the Fuld report appendix renders no identity, so
/// this path is the GUI's alone and no report oracle depends on it.</para>
/// </summary>
internal static class ProblemPresenter
{
    /// <summary>One entry per line, which is how a message dialog shows a set of them.</summary>
    private const char EntrySeparator = '\n';

    /// <summary>Case 1 — a bare problem: its Danish message, whole, with identity as a bracketed suffix.</summary>
    public static string Text(Problem problem) =>
        problem.Code.Value is { Length: > 0 } code ? $"{problem.Message} [{code}]" : problem.Message;

    /// <summary>
    /// Case 2 — a cause/detail chain: the CAUSE's entry, and only it. The traversal rule decides which of the two
    /// codes the user sees; the operation's sentence is never shown beside it.
    /// </summary>
    public static string Text(ProblemChain chain) => Text(chain.Cause);

    /// <summary>
    /// Case 3 — a set of independent problems: the head's entry followed by every item's, in order. Returned as
    /// the entries rather than as one string so a list surface can show them as rows.
    /// </summary>
    public static IReadOnlyList<string> Entries(ProblemAggregate aggregate) =>
        [Text(aggregate.Head), .. aggregate.Items.Select(Text)];

    /// <summary>Case 3 as a message-dialog body: the same entries, one per line.</summary>
    public static string Text(ProblemAggregate aggregate) =>
        string.Join(EntrySeparator, Entries(aggregate));
}
