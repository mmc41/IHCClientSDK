using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// What the project lifecycle reports about itself.
///
/// Every one of these operations returns a <c>bool</c> and SWALLOWS its exception after showing a dialog, so
/// a failed open and a user-cancelled one were the same <c>false</c> to every caller and to the backend. The
/// outcome is therefore recorded in the catch BEFORE the dialog: the dialog waits for a person, so recording
/// after it would fold arbitrary think-time into the operation, and the return discards the exception.
///
/// <para>Every span here is picked through a <see cref="TraceProbe"/> rather than by name alone. The capture's
/// listener is process-wide, so a bare <c>Single(…)</c> over an operation name asserts that no other live
/// workflow in the assembly emitted one — a claim this fixture cannot make and does not need to.</para>
/// </summary>
[TestFixture]
public class WorkflowLifecycleTelemetryTests
{
    /// <summary>
    /// The gate's assertion. A missing file makes <c>OpenAsync</c> return <c>false</c> after showing a dialog
    /// and discarding the exception - so before this, the failure left NOTHING behind at all.
    /// </summary>
    [Test]
    public async Task AFailingOpen_RecordsAFailedOutcomeAndAHistogramPoint_EvenThoughItReturnsFalse()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "ProjectWorkflow.",
            instruments: new[] { "ihc.project.load.duration", "ihc.project.save.duration" });
        using ShellHarness harness = ShellHarness.Create();

        using TraceProbe probe = TraceProbe.Start();
        bool opened = await harness.Session.OpenAsync(harness.TempPath("no-such-project.vis"));

        Assert.That(opened, Is.False, "the method still returns false - that contract is unchanged");

        Activity open = probe.Span(capture, "ProjectWorkflow.OpenAsync");
        Assert.Multiple(() =>
        {
            Assert.That(open.Status, Is.EqualTo(ActivityStatusCode.Error),
                "a failure that returns false must not look like a success");
            Assert.That(open.GetTagItem("ihc.edit.status"), Is.EqualTo("failed"));
            Assert.That(open.GetTagItem("error.type"), Is.Not.Null, "the normalized type, not the dialog text");
            Assert.That(open.GetTagItem("ihc.project.source"), Is.EqualTo("file"));

            Assert.That(capture.PointsOf("ihc.project.load.duration").Count, Is.EqualTo(1),
                "a failed load is still timed - excluding failures flatters every latency graph");
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
        Assert.That(await harness.Session.SaveAsAsync(), Is.True);

        using TraceProbe probe = TraceProbe.Start();
        Assert.That(await harness.Session.OpenAsync(path), Is.True);

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
