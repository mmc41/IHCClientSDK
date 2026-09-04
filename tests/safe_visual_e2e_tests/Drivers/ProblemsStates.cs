using ihc_openvisual.ViewModels;

namespace safe_visual_e2e_tests;

/// <summary>
/// The words a driver reports the Problemer panel's state in, and what BOUND means over them.
/// </summary>
/// <remarks>
/// <para>Declared here because BOTH drivers answer <c>problems state</c>, and a scenario reads the envelope
/// without knowing which of them produced it. Written inline in each, the vocabulary and the rule over it were
/// the same contract in two places with nothing comparing the copies.</para>
///
/// <para><b>Bound excludes <see cref="Stale"/> as well as <see cref="Validating"/>.</b> A stale panel is showing
/// the PREVIOUS result: its counts are real numbers about a superseded document, which is the one shape a
/// scenario cannot tell apart from a correct answer. It is also what a wait that TIMED OUT leaves behind, so a
/// rule that admitted it would turn every such timeout into a pass against superseded findings.</para>
/// </remarks>
internal static class ProblemsStates
{
    /// <summary>No result is bound for the current document yet.</summary>
    internal const string Validating = "validating";

    /// <summary>A result is bound, but the document has moved past it.</summary>
    internal const string Stale = "stale";

    /// <summary>Up to date, and the run produced nothing.</summary>
    internal const string Clean = "clean";

    /// <summary>Up to date, with rows to show.</summary>
    internal const string Findings = "findings";

    /// <summary>
    /// The word for a view-model state, which is how the headless driver names what it reads directly.
    /// </summary>
    internal static string Of(ProblemsState state) => state.ToString().ToLowerInvariant();

    /// <summary>Whether the counts beside this state describe the CURRENT document.</summary>
    internal static bool IsBound(string state) => state is not (Validating or Stale);

    /// <summary>The same question of a view-model state, so the headless driver asks it in one step.</summary>
    internal static bool IsBound(ProblemsState state) => IsBound(Of(state));
}
