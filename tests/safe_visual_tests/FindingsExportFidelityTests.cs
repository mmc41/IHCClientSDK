using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Validation;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// WYSIWYG, end to end: the file a user gets holds exactly the rows their panel was showing, in exactly that
/// order.
///
/// <para><b>This property is FALSE of the SDK path, by design.</b> <c>ExportFindings(project, stream)</c> writes
/// the whole categorized run, and a no-drift test in <c>safe_project_tests</c> asserts precisely that. The host
/// path is the opposite claim — filtered and re-sorted — so it can only be asserted here, over a real panel
/// driven through real gestures. Asserting it in the SDK suite would be asserting the wrong thing about the
/// wrong door.</para>
///
/// <para><b>Why the whole shell rather than a stubbed workflow.</b> Every link in the chain is load-bearing and
/// each has its own way to break: the panel projects and sorts, the request carries the findings, the workflow
/// picks the path, the facade writes the bytes. A test that stubbed any of them would still pass while a user
/// got a file that did not match their screen.</para>
/// </summary>
public class FindingsExportFidelityTests
{
    /// <summary>The exported file's finding lines, in file order.</summary>
    private static string[] ExportedLines(string path) =>
    [
        .. Ihc.ProjectFile.Encoding.GetString(File.ReadAllBytes(path))
            .Split("\r\n")
            .Where(l => l.Contains("<finding ")),
    ];

    private static string Attribute(string line, string name) =>
        line.Split($" {name}=\"")[1].Split('"')[0];

    /// <summary>The rows the panel is showing right now, as the pair a file line can also speak.</summary>
    private static (string Code, string Severity)[] Visible(ProblemsShellRig rig) =>
        [.. rig.Panel.Rows.Select(r => (r.Code, r.Severity.ToString()))];

    /// <summary>
    /// The fixture, opened and validated through the real shell: 150 findings across more than one tier, which
    /// is what makes hiding one and re-sorting a column change the answer.
    /// </summary>
    private static async Task<ProblemsShellRig> ShowingTheErrorsFixtureAsync()
    {
        ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        await rig.Harness.Session.OpenAsync(ProblemsTestData.FixturePath("Project6-Errors.vis"));
        await rig.SettleAsync();
        return rig;
    }

    /// <summary>
    /// The whole claim: after a tier is hidden AND a column re-sorted, the file's findings are the visible
    /// rows' findings, one for one, in order.
    ///
    /// <para>Compared by the pair a row and a file line can BOTH speak — code and severity — rather than by
    /// index against the validation result, which is neither filtered nor sorted and would make this pass for
    /// the wrong reason.</para>
    ///
    /// <para><b>Two filter gestures, because no fixture mixes tiers.</b> Nothing in the catalogue can emit
    /// <c>Info</c> yet, and this fixture's 150 findings are all <c>Warning</c> — so hiding one tier either
    /// removes nothing or removes everything, and neither alone proves the file follows the filter. Hiding the
    /// EMPTY tier keeps 150 rows and proves the ORDER is carried across a re-sort; hiding the POPULATED one
    /// drops the file to zero lines in step with the panel. Together they pin both halves, and the day a
    /// fixture produces two populated tiers this collapses back into one gesture.</para>
    /// </summary>
    [Test]
    public async Task TheWrittenFileHoldsExactlyTheVisibleRowsInTheVisibleOrder()
    {
        using ProblemsShellRig rig = await ShowingTheErrorsFixtureAsync();
        string path = rig.Harness.TempPath("fidelity.xml");
        rig.Harness.Dialogs.SaveReportPath = path;
        ProblemsColumnViewModel code = rig.Panel.Columns.Single(c => c.Column == ProblemsColumn.Code);

        int everything = rig.Panel.Rows.Count;
        rig.Panel.Tiers.Single(t => t.Severity == ValidationSeverity.Info).IsShown = false;
        code.SortCommand.Execute(null);
        code.SortCommand.Execute(null);                       // and again, so the sort is DESCENDING
        bool descending = !rig.Panel.SortAscending;
        (string Code, string Severity)[] visibleAfterSort = Visible(rig);
        await rig.Panel.ExportCommand.ExecuteAsync(null);
        string[] afterSort = ExportedLines(path);

        rig.Harness.Dialogs.SaveReportPath = rig.Harness.TempPath("fidelity-hidden.xml");
        rig.Panel.Tiers.Single(t => t.Severity == ValidationSeverity.Warning).IsShown = false;
        (string Code, string Severity)[] visibleAfterFilter = Visible(rig);
        await rig.Panel.ExportCommand.ExecuteAsync(null);
        string[] afterFilter = ExportedLines(rig.Harness.TempPath("fidelity-hidden.xml"));

        Assert.Multiple(() =>
        {
            Assert.That(everything, Is.GreaterThan(1), "non-vacuity: the fixture really does produce findings");
            Assert.That(descending, Is.True, "precondition: the second click reversed the sort");
            Assert.That(visibleAfterSort, Has.Length.EqualTo(everything),
                "precondition: hiding the empty tier removed nothing, so this half is about ORDER");
            Assert.That(visibleAfterFilter, Is.Empty,
                "precondition: hiding the populated tier removed everything, so that half is about the FILTER");

            Assert.That(
                afterSort.Select(l => (Attribute(l, "code"), Attribute(l, "severity"))),
                Is.EqualTo(visibleAfterSort),
                "one for one, in the order the panel was showing");
            Assert.That(
                afterFilter, Is.Empty,
                "and the file follows the filter down to nothing, rather than writing the whole run");
        });
    }

    /// <summary>
    /// The order really is the PANEL's and not the engine's. Exporting the same project twice under opposite
    /// sort directions must produce two files whose finding order is reversed with respect to each other — a
    /// file that ignored the sort would produce two identical ones.
    /// </summary>
    [Test]
    public async Task ReversingTheSortReversesTheFile()
    {
        using ProblemsShellRig rig = await ShowingTheErrorsFixtureAsync();
        ProblemsColumnViewModel code = rig.Panel.Columns.Single(c => c.Column == ProblemsColumn.Code);

        rig.Harness.Dialogs.SaveReportPath = rig.Harness.TempPath("ascending.xml");
        code.SortCommand.Execute(null);
        await rig.Panel.ExportCommand.ExecuteAsync(null);
        string[] ascending = ExportedLines(rig.Harness.TempPath("ascending.xml"));

        rig.Harness.Dialogs.SaveReportPath = rig.Harness.TempPath("descending.xml");
        code.SortCommand.Execute(null);
        await rig.Panel.ExportCommand.ExecuteAsync(null);
        string[] descending = ExportedLines(rig.Harness.TempPath("descending.xml"));

        Assert.Multiple(() =>
        {
            Assert.That(ascending, Has.Length.GreaterThan(1), "non-vacuity");
            Assert.That(ascending, Has.Length.EqualTo(descending.Length), "the same rows, differently ordered");
            Assert.That(
                descending.Select(l => Attribute(l, "code")),
                Is.EqualTo(ascending.Select(l => Attribute(l, "code")).Reverse()));
        });
    }

    /// <summary>
    /// The file states the filter it was written under. Without this a short file and a short list of problems
    /// are the same document, and a reader who was sent one would have no way to tell which they had.
    /// </summary>
    [Test]
    public async Task TheFileRecordsWhichTiersWereIncluded()
    {
        using ProblemsShellRig rig = await ShowingTheErrorsFixtureAsync();
        string path = rig.Harness.TempPath("tiers.xml");
        rig.Harness.Dialogs.SaveReportPath = path;

        rig.Panel.Tiers.Single(t => t.Severity == ValidationSeverity.Warning).IsShown = false;
        await rig.Panel.ExportCommand.ExecuteAsync(null);

        string text = Ihc.ProjectFile.Encoding.GetString(await File.ReadAllBytesAsync(path));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain(" severities=\"Error Info\""), "enum order, and Warning excluded");
            Assert.That(text, Does.Not.Contain(" severity=\"Warning\""), "and no Warning row survived");
            Assert.That(text, Does.Contain(" order=\"host:"), "the order is labelled as the host's, not production");
        });
    }

    /// <summary>
    /// The panel's own name for what it exported reaches the file's <c>source</c>. The SDK cannot supply it — a
    /// project carries no path — so a file that named itself anything else would be naming a guess.
    /// </summary>
    [Test]
    public async Task TheFileNamesTheOpenDocumentAsItsSource()
    {
        using ProblemsShellRig rig = await ShowingTheErrorsFixtureAsync();
        string path = rig.Harness.TempPath("source.xml");
        rig.Harness.Dialogs.SaveReportPath = path;

        await rig.Panel.ExportCommand.ExecuteAsync(null);
        string text = Ihc.ProjectFile.Encoding.GetString(await File.ReadAllBytesAsync(path));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain(" source=\"Project6-Errors.vis\""));
            Assert.That(
                rig.Harness.Dialogs.LastReportSuggestedName, Is.EqualTo("Project6-Errors-fejlliste.xml"),
                "and the dialog suggested a name built from the same document");
        });
    }

    /// <summary>
    /// Every tier off writes a file with no findings that SAYS so — the case that makes the severities record
    /// load-bearing, driven here through the real gestures rather than through a constructed request.
    /// </summary>
    [Test]
    public async Task EveryTierOffWritesAnEmptyFileThatSaysWhy()
    {
        using ProblemsShellRig rig = await ShowingTheErrorsFixtureAsync();
        string path = rig.Harness.TempPath("hidden.xml");
        rig.Harness.Dialogs.SaveReportPath = path;

        foreach (ProblemsTierViewModel tier in rig.Panel.Tiers)
        {
            tier.IsShown = false;
        }

        await rig.Panel.ExportCommand.ExecuteAsync(null);
        string text = Ihc.ProjectFile.Encoding.GetString(await File.ReadAllBytesAsync(path));

        Assert.Multiple(() =>
        {
            Assert.That(ExportedLines(path), Is.Empty);
            Assert.That(text, Does.Contain(" severities=\"\""),
                "not a clean project: nothing was included, and the file is the only thing that can say so");
        });
    }

}
