#nullable enable
using System.Collections.Immutable;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// The function block's variable sections in document order, each with its display label — a shared SDK
    /// FB-domain descriptor consumed by the FB documentation report, the Functions-pane tree node builder and the
    /// numeric-operand projection, so all three stay in lockstep. Not scoped to reporting: it is a fact about the
    /// FB grammar (<c>inputs</c>/<c>outputs</c>/<c>settings</c>/<c>internalsettings</c>), not a report layout.
    /// </summary>
    public static class FunctionBlockSections
    {
        /// <summary>The four variable sections a function block declares, in document order, paired with the label the
        /// UI and reports render for each.</summary>
        public static readonly ImmutableArray<(string Container, string Label)> All = ImmutableArray.Create(
            ("inputs", "Input"),
            ("outputs", "Output"),
            ("settings", "Settings"),
            ("internalsettings", "Internal variables"));
    }
}
