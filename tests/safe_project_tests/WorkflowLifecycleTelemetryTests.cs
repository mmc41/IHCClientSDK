using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// What the project lifecycle reports about itself.
///
/// Every one of these operations SWALLOWS its exception after showing a dialog, so the outcome is recorded in
/// the catch BEFORE the dialog: the dialog waits for a person, so recording after it would fold arbitrary
/// think-time into the operation, and the return discards the exception.
///
/// <para>They used to return a bare <c>bool</c> as well, which made a failed open and a cancelled one the same
/// answer to every caller — the span knew and nothing above it could. They now answer an
/// <c>OperationOutcome</c>, and the ones below pin what that distinguishes.</para>
///
/// <para>Every span here is picked through a <see cref="TraceProbe"/> rather than by name alone. The capture's
/// listener is process-wide, so a bare <c>Single(…)</c> over an operation name asserts that no other live
/// workflow in the assembly emitted one — a claim this fixture cannot make and does not need to.</para>
/// </summary>
[TestFixture]
public class WorkflowLifecycleTelemetryTests
{
    /// <summary>
    /// The gate's assertion. A missing file makes <c>OpenAsync</c> answer not-Ok after showing a dialog and
    /// discarding the exception - so before this, the failure left NOTHING behind at all.
    /// </summary>
    [Test]
    public async Task AFailingOpen_RecordsAFailedOutcomeAndAHistogramPoint_EvenThoughItAnswersRatherThanThrows()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "ProjectWorkflow.",
            instruments: new[] { "ihc.project.load.duration", "ihc.project.save.duration" });
        using ShellHarness harness = ShellHarness.Create();

        using TraceProbe probe = TraceProbe.Start();
        bool opened = (await harness.Session.OpenAsync(harness.TempPath("no-such-project.vis"))).IsOk;

        Assert.That(opened, Is.False, "the method still answers not-Ok rather than throwing - that is unchanged");

        Activity open = probe.Span(capture, "ProjectWorkflow.OpenAsync");
        Assert.Multiple(() =>
        {
            Assert.That(open.Status, Is.EqualTo(ActivityStatusCode.Error),
                "a failure that returns false must not look like a success");
            Assert.That(open.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
            Assert.That(open.GetTagItem("error.type"), Is.Not.Null, "the normalized type, not the dialog text");
            Assert.That(open.GetTagItem("ihc.project.source"), Is.EqualTo("file"));

            Assert.That(capture.PointsOf("ihc.project.load.duration").Count, Is.EqualTo(1),
                "a failed load is still timed - excluding failures flatters every latency graph");
        });
    }

    /// <summary>
    /// The quit gate's two "no"s, which used to be one. <c>CanQuitAsync</c> answers that the window must stay
    /// open both when the installer pressed <i>Fortryd</i> and when the save they asked for could not be
    /// written — and while the answer was a bool, nothing downstream could tell those apart: the same
    /// <c>false</c>, the same <c>ok</c> on every span above the one that failed.
    /// <para>Both cases still cancel the quit. What changed is that they now SAY which they were.</para>
    /// </summary>
    [Test]
    public async Task TheQuitGate_TellsACancelledQuitApartFromOneAFailedSaveStopped()
    {
        using ShellHarness harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await harness.Session.AddLocalityAsync();   // dirty, so the prompt is raised at all

        harness.Dialogs.SaveChangesResult = SaveChangesResult.Cancel;
        Ihc.OperationOutcome cancelled = await harness.Session.CanQuitAsync();

        // "Gem" this time, onto a path that cannot be written: a directory that does not exist.
        harness.Dialogs.SaveChangesResult = SaveChangesResult.Save;
        harness.Dialogs.SavePath = System.IO.Path.Combine(harness.TempPath("no-such-dir"), "x.vis");
        Ihc.OperationOutcome brokenSave = await harness.Session.CanQuitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cancelled.Status, Is.EqualTo(Ihc.OperationStatus.Cancelled),
                "the installer changed their mind, which is not a failure of anything");
            Assert.That(brokenSave.Status, Is.EqualTo(Ihc.OperationStatus.Failed),
                "the save broke - reporting that as a cancellation is what hid it");
            Assert.That(cancelled.IsOk, Is.False, "and both still stop the quit");
            Assert.That(brokenSave.IsOk, Is.False);
        });
    }

    /// <summary>
    /// The other half of the same distinction, on the doors a gesture actually calls: a dismissed picker and a
    /// prompt the installer stopped are <c>Cancelled</c>, never <c>Failed</c> — so a cancel rate and an error
    /// rate built on these operations measure different things, which is the point of the fourth value.
    /// </summary>
    [Test]
    public async Task ADismissedPickerAndAStoppedPrompt_AreCancelled_NotFailures()
    {
        using ShellHarness harness = ShellHarness.Create();
        await harness.Session.StartAsync();

        harness.Dialogs.OpenPath = null;   // the installer closed the file picker
        Ihc.OperationOutcome noFileChosen = await harness.Session.OpenWithPickerAsync();

        await harness.Session.AddLocalityAsync();   // dirty, so New has to prompt
        harness.Dialogs.SaveChangesResult = SaveChangesResult.Cancel;
        Ihc.OperationOutcome newStopped = await harness.Session.NewAsync();
        Ihc.OperationOutcome closeStopped = await harness.Session.CloseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(noFileChosen.Status, Is.EqualTo(Ihc.OperationStatus.Cancelled));
            Assert.That(newStopped.Status, Is.EqualTo(Ihc.OperationStatus.Cancelled),
                "the prompt's answer is FORWARDED, not flattened into a bare no");
            Assert.That(closeStopped.Status, Is.EqualTo(Ihc.OperationStatus.Cancelled));
            Assert.That(harness.Session.IsDirty, Is.True, "and nothing was discarded on the way");
        });
    }

    /// <summary>
    /// The LAUNCH, when the file it was given cannot be opened. Two things were missing and they are different
    /// halves of one problem: <c>StartAsync</c> answered nothing at all, so the failure marked
    /// <c>OpenAsync</c>'s span and stopped there while every operation above it — the launch included — read
    /// ok; and the dialog was the only surface that said anything, so once it was dismissed nothing explained
    /// why the installer was looking at an empty project.
    /// <para>The recovery itself is unchanged and asserted: a bad file association must never block the
    /// launch.</para>
    /// </summary>
    [Test]
    public async Task AStartUpFileThatCannotBeOpened_FailsTheLaunchAndLeavesADurableRow()
    {
        List<Problems.InternalError> filed = [];
        using ShellHarness harness = ShellHarness.Create(faultSink: filed.Add);

        Ihc.OperationOutcome started = await harness.Session.StartAsync(harness.TempPath("no-such-project.vis"));

        Assert.Multiple(() =>
        {
            Assert.That(started.Status, Is.EqualTo(Ihc.OperationStatus.Failed),
                "recovering from a failure is not the same as not having one");
            Assert.That(harness.Session.Current, Is.Not.Null,
                "and it DID recover — the shell is usable over the standard empty project");
            Assert.That(filed.Select(e => e.Code.Value),
                Is.EqualTo(new[] { "app.openvisual.project-open-failed" }),
                "the row is what outlives the dialog, and is what makes the empty window explicable");
            Assert.That(filed[0].Origin, Is.EqualTo(Problems.InternalErrorOrigin.Platform),
                "what failed is the file the machine handed over, not this application");
        });
    }

    /// <summary>
    /// The peer that must NOT file a row, so the exception above stays an exception. File ▸ Open leaves the
    /// installer where they were and the dialog is the whole story — collecting every unreadable file in the
    /// fault tier is exactly what <c>FailureReport.ReportIfUnanticipated</c> argues against.
    /// </summary>
    [Test]
    public async Task AnOrdinaryOpenThatFails_LeavesNoFaultRow()
    {
        List<Problems.InternalError> filed = [];
        using ShellHarness harness = ShellHarness.Create(faultSink: filed.Add);
        await harness.Session.StartAsync();

        Ihc.OperationOutcome opened = await harness.Session.OpenAsync(harness.TempPath("no-such-project.vis"));

        Assert.Multiple(() =>
        {
            Assert.That(opened.IsOk, Is.False, "it still fails, and still says so on its span and to its caller");
            Assert.That(filed, Is.Empty, "but a wrong file chosen deliberately is not a fault in the tool");
        });
    }

    [Test]
    public async Task ASuccessfulOpen_CarriesSourcePathAndFileSize()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "ProjectWorkflow.",
            instruments: new[] { "ihc.project.load.duration", "ihc.project.save.duration" });
        using ShellHarness harness = ShellHarness.Create();
        string path = harness.TempPath("lifecycle.vis");
        harness.Dialogs.SavePath = path;
        await harness.Session.NewAsync();
        Assert.That((await harness.Session.SaveAsAsync()).IsOk, Is.True);

        using TraceProbe probe = TraceProbe.Start();
        Assert.That((await harness.Session.OpenAsync(path)).IsOk, Is.True);

        Activity open = probe.Span(capture, "ProjectWorkflow.OpenAsync");
        Assert.Multiple(() =>
        {
            Assert.That(open.Status, Is.EqualTo(ActivityStatusCode.Unset));
            Assert.That(open.GetTagItem("ihc.project.source"), Is.EqualTo("file"));
            Assert.That(open.GetTagItem("ihc.project.path"), Is.EqualTo(path));
            Assert.That(open.GetTagItem("ihc.project.file_size"), Is.Not.Null.And.Not.EqualTo(0L),
                "the size of what was actually read");
            Assert.That(capture.PointsOf("ihc.project.load.duration").Count, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// The confirm prompt is a CHILD, not part of the parent. Its duration is a person reading a dialog, and
    /// folded into the parent it would make every load and save percentile measure reading speed.
    /// </summary>
    [Test]
    public async Task TheSaveConfirmation_IsItsOwnChildSpan()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "ProjectWorkflow.",
            instruments: new[] { "ihc.project.load.duration", "ihc.project.save.duration" });
        using ShellHarness harness = ShellHarness.Create();

        using TraceProbe probe = TraceProbe.Start();
        await harness.Session.NewAsync();

        Activity confirm = probe.Span(capture, "ProjectWorkflow.ConfirmSaveIfDirtyAsync");
        Activity parent = probe.Span(capture, "ProjectWorkflow.NewAsync");
        Assert.That(confirm.Parent, Is.SameAs(parent),
            "the prompt runs INSIDE the lifecycle operation, as its child rather than as part of it");
    }
}
