using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// The function block's variable sections in document order, each with its display label — a shared SDK
    /// FB-domain descriptor consumed by the FB documentation report, the Functions-pane tree node builder and the
    /// numeric-operand projection, so all three stay in lockstep. Not scoped to reporting: it is a fact about the
    /// FB grammar (<c>inputs</c>/<c>outputs</c>/<c>settings</c>/<c>internalsettings</c>), not a report layout.
    /// </summary>
    internal static class FunctionBlockSections
    {
        /// <summary>The four variable sections a function block declares, in document order, paired with the label the
        /// UI and reports render for each.</summary>
        /// <summary>
        /// The four sections, declared ONCE and in document order: the container tag each is stored under, the
        /// caption it falls back to, and whether it holds variables internal to the block rather than part of its
        /// interface. Everything else here is a view of this one table.
        /// </summary>
        public static readonly EquatableArray<FunctionBlockSectionInfo> Descriptions =
            (EquatableArray<FunctionBlockSectionInfo>)ImmutableArray.Create(
                new FunctionBlockSectionInfo("inputs", "Input", IsInternal: false),
                new FunctionBlockSectionInfo("outputs", "Output", IsInternal: false),
                new FunctionBlockSectionInfo("settings", "Settings", IsInternal: false),
                new FunctionBlockSectionInfo("internalsettings", "Internal variables", IsInternal: true));

        /// <summary>The same sections as the (container, label) pairs the engine's older readers iterate.</summary>
        public static readonly ImmutableArray<(string Container, string Label)> All =
            [.. Descriptions.Select(section => (section.Container, section.Caption))];
    }
}
