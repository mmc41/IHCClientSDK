using System.Collections.Generic;
using System.Linq;

namespace Ihc.Tests.Shared
{
    /// <summary>One recorded finding: the oracle's own six columns, projected for comparison against a panel.</summary>
    internal sealed record OracleRow(string Severity, string Code, string Category, string Locator, string Message);

    /// <summary>
    /// The corpus oracle addressed BY CASE, as a run-time lookup — so an expected value is never a second,
    /// hand-kept copy of a committed file.
    /// </summary>
    /// <remarks>
    /// Shared between the assembly that CONSUMES these rows (the end-to-end suite, which compares them against
    /// what the running application publishes) and the assembly that GUARDS them (the GUI suite, whose
    /// <c>FindingOracleLinkTests</c> pins the counts). That split is the whole reason this is not a private
    /// helper on the end-to-end harness: the guard has to reach exactly the reader the consumers use, from a
    /// suite whose own failure is unambiguous.
    /// <para>The guard earns its place because a consumer's assertions are built ON this list, so a reader that
    /// silently returned nothing would make them trivially true rather than red. CI does exercise some of those
    /// consumers now — the headless leg runs every scenario not marked desktop-only — but not the desktop-only
    /// ones and not the real-GUI mode, and a silent zero would still read as a pass in all of them.</para>
    /// </remarks>
    internal static class FindingOracleRows
    {
        /// <summary>
        /// The findings the corpus oracle records for a case, in the oracle's own (production) order.
        /// </summary>
        /// <param name="caseName">
        /// The oracle's CASE name — the first column, e.g. <c>Project6-Errors</c>. Note it carries no <c>.vis</c>
        /// suffix and is prefixed by a corpus folder (<c>fixture/</c>, <c>authentic/</c>), so a caller passing a
        /// file name would match nothing. That is exactly what happened when this helper was first written, and
        /// it failed LOUDLY rather than silently because every test using it asserts the row count is non-zero
        /// first.
        /// </param>
        internal static IReadOnlyList<OracleRow> Rows(string caseName) =>
        [
            .. ByCase()[caseName].Select(finding => new OracleRow(
                finding.Severity, finding.Code, finding.Category, finding.Locator ?? NoLocator, finding.Message)),
        ];

        /// <summary>The rule ids the oracle records for a case, in production order.</summary>
        internal static IReadOnlyList<string> Codes(string caseName) =>
            [.. Rows(caseName).Select(r => r.Code)];

        /// <summary>
        /// The whole corpus, grouped by case leaf and read ONCE. The files are committed oracles and cannot
        /// change under a run, while a fixture asks for its case repeatedly — so re-opening and re-parsing the
        /// whole corpus per question was work no caller wanted. An <see cref="ILookup{TKey, TElement}"/> also
        /// answers an unknown case with an empty sequence rather than throwing, which is the behaviour the
        /// filter it replaced had.
        /// <para>
        /// Assigned on first use rather than in a field initializer: a missing or corrupt oracle then surfaces
        /// as that reader's own exception on the test that asked, instead of a type-initializer failure on
        /// every other test this class serves.
        /// </para>
        /// </summary>
        private static ILookup<string, RecordedFinding>? byCase;

        private static ILookup<string, RecordedFinding> ByCase() =>
            byCase ??= FindingOracleHarness.ReadAll().ToLookup(finding => CaseLeaf(finding.Case));

        /// <summary>
        /// A case name without its corpus folder, so a caller may pass <c>Project6-Errors</c> for a case the
        /// oracle records as <c>fixture/Project6-Errors</c>.
        /// <para>
        /// Load-bearing, and quietly so: the classes that consume these rows do not run in CI, so dropping this
        /// would not fail a gated run — it would simply match nothing, and every assertion built on an empty
        /// list would pass. That is why <c>FindingOracleLinkTests</c> exists in the suite that does run there.
        /// </para>
        /// </summary>
        private static string CaseLeaf(string caseName) =>
            caseName.LastIndexOf('/') is var slash && slash >= 0 ? caseName[(slash + 1)..] : caseName;

        /// <summary>
        /// What a row shows for a finding that names no element. The oracle records absence as a MISSING
        /// attribute, which reads back as null; these rows keep the non-null shape their consumers were written
        /// against.
        /// </summary>
        private const string NoLocator = "-";
    }
}
