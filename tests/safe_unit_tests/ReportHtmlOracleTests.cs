using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Projects;

namespace safe_unit_tests;

/// <summary>
/// The oracle-gated E2E harness for the HTML report format (spec R5/AC1, D5/D9): each committed
/// <c>tests/testdata/reports/*.html</c> oracle regenerates byte-identically through the public facade using
/// the app's REAL <see cref="SvgReportIconProvider"/> — the oracles embed OpenVisual's inline
/// <c>&lt;symbol&gt;</c> sprite and <c>#icon-logo</c> banner, so the HTML byte contract lives here in
/// <c>safe_unit_tests</c> (which references the app assembly) rather than in the SDK-only suite. The provider
/// must be constructible WITHOUT a running Avalonia platform (S09 — plain embedded-resource read). Generator
/// emits UTF-8 no BOM + LF; CRLF→LF normalization is applied to the ORACLE side only (S06). The coverage
/// matrix, oracle naming, pinned clock and byte assert are the shared <see cref="ReportOracles"/> harness.
/// </summary>
public class ReportHtmlOracleTests
{
    private static ProjectAppService App() =>
        new(new IhcSettings(), new BuiltInCatalog(), ReportOracles.Clock());

    private static string TestData(params string[] parts) =>
        Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "testdata" }.Concat(parts).ToArray());

    private static object[][] HtmlOracleCases() => [.. ReportOracles.Cases("html")];

    [TestCaseSource(nameof(HtmlOracleCases))]
    public async Task HtmlReport_RegeneratesOracle_ByteForByte(
        string oracleFile, string projectFile, ReportKind kind, ReportMode mode)
    {
        Project project = await App().Load(TestData("projects", projectFile));
        using var output = new MemoryStream();

        await App().GenerateReport(project, kind, mode, ReportMimeTypes.Html, output, new SvgReportIconProvider());

        ReportOracles.AssertMatchesOracle(
            File.ReadAllBytes(TestData("reports", oracleFile)), output.ToArray(), oracleFile);
    }

    /// <summary>
    /// Regenerates all twelve <c>*.html</c> oracles into <c>reports.generated/</c> beside the test binary — the
    /// HTML half of the same deliberate, diff-then-adopt regeneration the <c>*.txt</c> oracles get.
    /// <see cref="ExplicitAttribute"/> so it never runs in the gate.
    /// TODO: Delete this test and the <c>reports.generated/</c> folder once the oracles are stable and the HTML report is no longer changing.
    /// </summary>
    [Test]
    [Explicit("Regenerates the checked-in *.html report oracles. Run deliberately, then diff the emitted files "
        + "against tests/testdata/reports/ before copying them over.")]
    [Category("OracleRegeneration")]
    public async Task Regenerate_TheHtmlOracles()
    {
        foreach (object[] oracleCase in HtmlOracleCases())
        {
            Project project = await App().Load(TestData("projects", (string)oracleCase[1]));
            using var output = new MemoryStream();

            await App().GenerateReport(
                project, (ReportKind)oracleCase[2], (ReportMode)oracleCase[3], ReportMimeTypes.Html, output,
                new SvgReportIconProvider());

            TestContext.Out.WriteLine(ReportOracles.WriteGenerated((string)oracleCase[0], output.ToArray()));
        }
    }
}
