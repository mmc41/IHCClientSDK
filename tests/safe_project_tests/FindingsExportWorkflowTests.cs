using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Where the panel's list goes, and what happens when it cannot get there.
///
/// <para><b>The two halves are deliberately separate objects.</b> The panel decided WHAT to export and is tested
/// over that; this workflow decides WHERE, and is tested over the dialog it asks and the failure it reports. The
/// seam between them is a plain request value, which is what lets each be driven without the other.</para>
///
/// <para><b>Cancelling is not a failure.</b> A user who dismisses the save dialog said no; writing nothing and
/// reporting nothing is the whole correct behaviour, and a dialog appearing to tell them their export "failed"
/// would be the defect.</para>
/// </summary>
public class FindingsExportWorkflowTests
{
    private static FindingsExportRequest Request() =>
        new(
            EquatableArray.CreateRange<ValidationFinding>(
            [
                new ValidationFinding(
                    new Problem(new ProblemCode("doc-name-empty"), "Navnet mangler.",
                        EquatableArray<ProblemArgument>.Empty),
                    ValidationSeverity.Warning, ValidationCategory.Documentation,
                    new FindingLocation("utcs_project", null, null), EquatableArray<FindingLocation>.Empty),
            ]),
            "host:code desc",
            EquatableArray.CreateRange<ValidationSeverity>([ValidationSeverity.Error, ValidationSeverity.Warning]),
            // ASYMMETRIC on purpose: Fatale fejl hidden, Fejl shown. Both-shown was the state the writer also
            // DERIVES from @severities when a producer passes no filter, so a file produced from it proves only
            // that some value arrived — not that the panel's value did. "ordinary" can be reached no other way.
            new ErrorTierFilter(Refusing: false, Ordinary: true));

    // ── The happy path ──────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ExportingWritesThePanelsListToTheChosenFile()
    {
        using ShellHarness harness = ShellHarness.Create();
        string path = harness.TempPath("findings-export.xml");
        harness.Dialogs.SaveReportPath = path;
        await harness.Session.NewAsync();

        await harness.Session.ExportFindingsAsync(Request());

        string text = Ihc.ProjectFile.Encoding.GetString(await File.ReadAllBytesAsync(path));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>"));
            Assert.That(text, Does.Contain(" code=\"doc-name-empty\""), "the request's own finding");
            Assert.That(text, Does.Contain(" order=\"host:code desc\""), "the panel's order label, verbatim");
            Assert.That(
                text, Does.Contain(" severities=\"Error Warning\""),
                "the tiers the panel included — which the findings themselves cannot say");
            Assert.That(
                text, Does.Contain(" error_tiers=\"ordinary\""),
                "the panel's own split reached the file: a value the writer's fallback cannot produce, since "
                + "that derives both halves together from @severities");
            Assert.That(harness.Dialogs.LastProblem, Is.Null, "a successful export reports nothing");
        });
    }

    /// <summary>
    /// The FINDINGS door is the one asked, and the suggested name is the open document's with the findings
    /// extension. Which door was reached is what decides the dialog's title, its filter label and its extension,
    /// so reaching the report one would be wrong on all three.
    /// </summary>
    [Test]
    public async Task TheSaveDialogIsAskedForAnXmlFileNamedAfterTheDocument()
    {
        using ShellHarness harness = ShellHarness.Create();
        harness.Dialogs.SaveReportPath = null;
        await harness.Session.NewAsync();

        await harness.Session.ExportFindingsAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.AskedForFindings, Is.True);
            Assert.That(harness.Dialogs.LastReportFormat, Is.Null, "…and never the report door");
            Assert.That(
                harness.Dialogs.LastReportSuggestedName,
                Does.EndWith($"-fejlliste.{FindingExportFormat.FileExtension}"));
        });
    }

    // ── Cancelling ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A dismissed dialog writes nothing and says nothing. The user already said no.</summary>
    [Test]
    public async Task ACancelledDialogWritesNothingAndReportsNothing()
    {
        using ShellHarness harness = ShellHarness.Create();
        harness.Dialogs.SaveReportPath = null;
        await harness.Session.NewAsync();

        await harness.Session.ExportFindingsAsync(Request());

        Assert.Multiple(() =>
        {
            // That the dialog was OFFERED is what makes the two silences below meaningful: the workflow reached
            // the destination question and stopped on the answer, rather than never getting that far. Asserting
            // instead that some temp path holds no file would prove nothing — the fake was never given one, so
            // no run of any implementation could have created it.
            Assert.That(harness.Dialogs.AskedForFindings, Is.True, "the destination was asked for");
            Assert.That(harness.Dialogs.LastProblem, Is.Null, "cancelling is not a failure");
            Assert.That(harness.Dialogs.LastMessage, Is.Null, "and nothing was shown either");
        });
    }

    // ── Failing ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A destination that cannot be written reports the host's own coded problem, in Danish, under the export's
    /// own title — not the report title, because the user never asked for a report.
    /// </summary>
    [Test]
    public async Task AWriteFailureRaisesTheCodedDanishProblem()
    {
        using ShellHarness harness = ShellHarness.Create();
        // A DIRECTORY path: opening it for writing fails, and does so without depending on permissions.
        string unwritable = harness.TempPath("findings-export-dir");
        Directory.CreateDirectory(unwritable);
        harness.Dialogs.SaveReportPath = unwritable;
        await harness.Session.NewAsync();

        await harness.Session.ExportFindingsAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProblem, Is.Not.Null, "the failure reached the user");
            Assert.That(harness.Dialogs.LastMessageTitle, Is.EqualTo(ProjectFindingsWorkflow.ExportFailedTitle));
            Assert.That(ProjectFindingsWorkflow.ExportFailedTitle, Is.EqualTo("Eksport mislykkedes"));
            Assert.That(
                harness.Dialogs.LastProblem!.Code, Is.EqualTo(HostProblemCodes.FindingsExportFailed));
            Assert.That(
                harness.Dialogs.LastProblem.Code.Value,
                Is.EqualTo("app.openvisual.findings-export-failed"));
            Assert.That(
                harness.Dialogs.LastProblem.Message, Is.EqualTo("Fejllisten kunne ikke gemmes."),
                "Danish, and the sentence the catalogue authored");
        });
    }

    /// <summary>
    /// The code is a declared catalogue row, not a string minted at the raise site. A code with nothing behind
    /// it fails the completeness gate, and a row missing from the entry array is invisible to it.
    /// </summary>
    [Test]
    public void TheFailureCodeIsDeclaredInTheHostCatalogue()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                HostProblemCatalog.Current.Entries.Select(e => e.Code),
                Does.Contain(HostProblemCodes.FindingsExportFailed),
                "declared AND in the array the governance checks read");
            Assert.That(
                HostProblemCodes.All, Does.Contain(HostProblemCodes.FindingsExportFailed));
        });
    }

    // ── The picker's own strings ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Each document this app writes is described ONCE — title, filter label and extension together — and a
    /// findings list is described as itself. It used to say "Gem rapport" with an "HTML-rapport" filter for
    /// every caller, which was true while reports were the only caller and is wrong on both lines for a
    /// findings list.
    /// </summary>
    [Test]
    public void EachSavedDocumentIsDescribedByItsOwnRow()
    {
        AvaloniaDialogService.SaveFileDescription findings = AvaloniaDialogService.FindingsList;
        AvaloniaDialogService.SaveFileDescription html = AvaloniaDialogService.DescribeReport(ReportFormat.Html);
        AvaloniaDialogService.SaveFileDescription text = AvaloniaDialogService.DescribeReport(ReportFormat.Text);

        Assert.Multiple(() =>
        {
            Assert.That(findings.Title, Is.EqualTo("Gem fejlliste"));
            Assert.That(findings.FileTypeLabel, Is.EqualTo("XML-fejlliste"));
            Assert.That(findings.Extension, Is.EqualTo(FindingExportFormat.FileExtension));

            // The two report rows are unchanged — this was an addition, never a re-wording.
            Assert.That(html.Title, Is.EqualTo("Gem rapport"));
            Assert.That(html.FileTypeLabel, Is.EqualTo("HTML-rapport"));
            Assert.That(html.Extension, Is.EqualTo("html"));
            Assert.That(text.Title, Is.EqualTo("Gem rapport"));
            Assert.That(text.FileTypeLabel, Is.EqualTo("Tekstrapport"));
            Assert.That(text.Extension, Is.EqualTo("txt"));
        });
    }
}
