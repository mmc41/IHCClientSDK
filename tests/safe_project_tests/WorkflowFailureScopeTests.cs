using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Tests.Shared;
using Ihc.Vis;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The two shipped operations that told the installer they had failed and told their span they had not.
///
/// <para>Both are the same shape and neither is an exception: the workflow detects the condition itself, shows a
/// problem, and returns. The <c>SetOutcome</c> was simply not written — which is what makes it worth removing as
/// an available mistake rather than fixing twice. <c>FailureReport</c> is that removal; these two tests are the
/// reproduce-first evidence that the sites really were wrong.</para>
///
/// <para><b>What this fixture does NOT prove.</b> Routing many blocks through one helper raises the covered
/// fraction without adding a single assertion about the blocks it did not touch. Only a per-operation assertion
/// holds that guarantee; this fixture covers the sites the analysis actually caught.</para>
/// </summary>
[TestFixture]
public class WorkflowFailureScopeTests
{
    /// <summary>
    /// An import folder that does not exist. The installer gets <i>Mappen findes ikke</i>; the span used
    /// to end OK, so an import that imported nothing counted as an import that worked.
    /// </summary>
    [Test]
    public async Task AMissingImportFolderMarksItsScope()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanPrefix: "CatalogImportWorkflow.");
        using TraceProbe probe = TraceProbe.Start();
        using ShellHarness harness = ShellHarness.Create();
        string missing = Path.Combine(harness.TempDir, "no-such-catalog-folder");

        CatalogImportOutcome outcome = await harness.Session.ImportCatalogFolderAsync(missing, persist: false);

        Activity? span = probe.SpansNamed(capture, "CatalogImportWorkflow.ImportFolderAsync").LastOrDefault();
        Assert.Multiple(() =>
        {
            Assert.That(outcome.FolderMissing, Is.True, "non-vacuity: the branch ran");
            Assert.That(outcome.Imported, Is.Zero);
            // The scope's answer, FORWARDED on the value rather than left for the span alone: the gesture that
            // asked for the import has no other way to learn that the folder was not there.
            Assert.That(outcome.Outcome.IsOk, Is.False,
                "the import reports what it recorded, so the invoking gesture cannot read it as a success");
            AssertRefusedScope(harness, span, "app.openvisual.catalog-folder-missing");
        });
    }

    /// <summary>
    /// The report was produced and the OS opened no viewer for it. Same shape, same defect — and the
    /// same one the About window's link had, which is what makes it a pattern rather than an oversight.
    /// </summary>
    [Test]
    public async Task AReportThatOpensNoViewerMarksItsScope()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanPrefix: "ProjectReportWorkflow.");
        using TraceProbe probe = TraceProbe.Start();
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        harness.Dialogs.OpenExternalUrlSucceeds = false;   // a machine with no handler for the document

        await harness.Session.ViewReportInBrowserAsync(ReportKind.Functions, ReportMode.Full, ReportFormat.Html);

        Activity? span = probe.SpansNamed(capture, "ProjectReportWorkflow.ViewInBrowserAsync").LastOrDefault();
        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastOpenedUrl, Is.Not.Null, "non-vacuity: a report really was produced");
            AssertRefusedScope(harness, span, "app.openvisual.report-not-openable");
        });
    }

    /// <summary>
    /// The happy paths stay unmarked. A helper that reported on every call would be worse than the mistake it
    /// replaces: every successful import and every opened report would read as a failure.
    /// </summary>
    [Test]
    public async Task AnImportThatWorksLeavesItsScopeUnmarked()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanPrefix: "CatalogImportWorkflow.");
        using TraceProbe probe = TraceProbe.Start();
        using ShellHarness harness = ShellHarness.Create();
        string empty = Directory.CreateDirectory(Path.Combine(harness.TempDir, "empty-catalog")).FullName;

        await harness.Session.ImportCatalogFolderAsync(empty, persist: false);

        Assert.That(
            probe.Span(capture, "CatalogImportWorkflow.ImportFolderAsync").Status,
            Is.EqualTo(ActivityStatusCode.Unset));
    }
    /// <summary>
    /// What "the scope was marked" MEANS, in one place: the installer was told, the span ended Error, and it
    /// carries the code. Shared because the defect these tests reproduce is a site that satisfied the first of
    /// those and none of the rest — so a per-test copy is a copy that can quietly drop the half that matters.
    /// </summary>
    /// <param name="expectedCode">The <c>error.type</c> the span must carry — the only part that differs.</param>
    private static void AssertRefusedScope(ShellHarness harness, Activity? span, string expectedCode)
    {
        Assert.That(harness.Dialogs.LastProblem, Is.Not.Null, "the installer was told, as before");
        Assert.That(span, Is.Not.Null);
        Assert.That(span!.Status, Is.EqualTo(ActivityStatusCode.Error),
            "and now the span is told too — it used to end OK");
        Assert.That(span.GetTagItem("error.type"), Is.EqualTo(expectedCode),
            "carrying the code, so the conditions this operation can fail on stay distinguishable");
    }
}
