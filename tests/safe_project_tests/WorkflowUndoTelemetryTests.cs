using System.Diagnostics;
using System.Threading.Tasks;
using Ihc;
using Ihc.Vis.Model;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// What undo and redo report about themselves.
///
/// All three history operations return a bare <c>bool</c>, and <c>false</c> covered three different
/// situations: nothing to undo, a refusal, and an outright failure. As decoration-only capture.Spans they carried
/// no outcome at all, so none of the three could be told from the others in the backend - an undo that
/// FAILED looked exactly like one the user pressed with an empty history.
/// </summary>
[TestFixture]
public class WorkflowUndoTelemetryTests
{
    [Test]
    public async Task UndoAndRedo_EmitSpansCarryingAnOutcome()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "ProjectWorkflow."))
        {
            using ShellHarness harness = ShellHarness.Create();
            ihc_openvisual.ViewModels.MainWindowViewModel vm = harness.CreateViewModel();
            await vm.InitializeAsync();
            ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
            await harness.Session.AddEmptyFunctionBlockAsync(locality);

            using TraceProbe probe = TraceProbe.Start();
            Assert.That(await harness.Session.UndoAsync(), Is.True, "the inserted block is undoable");
            Assert.That(await harness.Session.RedoAsync(), Is.True);

            Activity undo = probe.Span(capture, "ProjectWorkflow.UndoAsync");
            Activity redo = probe.Span(capture, "ProjectWorkflow.RedoAsync");

            Assert.Multiple(() =>
            {
                Assert.That(undo.GetTagItem("ihc.edit.status"), Is.EqualTo("ok"),
                    "the outcome attribute these capture.Spans did not have before");
                Assert.That(redo.GetTagItem("ihc.edit.status"), Is.EqualTo("ok"));
                Assert.That(undo.Status, Is.EqualTo(ActivityStatusCode.Unset), "a successful undo is not an error");
            });
        }
    }

    /// <summary>
    /// The gate's assertion: an undo that FAILS leaves the span Error, where before it left Unset.
    ///
    /// The document's history step itself can only commit or no-op, so a genuine failure is an exception
    /// escaping the operation - here a <c>StateChanged</c> subscriber that faults while the undo's refresh
    /// runs, which is a real GUI failure mode. The old decoration-only span had no catch at all, so this
    /// went out looking like a successful undo.
    /// </summary>
    [Test]
    public async Task Undo_WhenTheRefreshItTriggersFaults_LeavesTheSpanError()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "ProjectWorkflow."))
        {
            using ShellHarness harness = ShellHarness.Create();
            ihc_openvisual.ViewModels.MainWindowViewModel vm = harness.CreateViewModel();
            await vm.InitializeAsync();
            ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
            await harness.Session.AddEmptyFunctionBlockAsync(locality);

            harness.Session.StateChanged += (_, _) => throw new System.TimeoutException("a faulting refresh");

            using TraceProbe probe = TraceProbe.Start();
            Assert.ThrowsAsync<System.TimeoutException>(async () => await harness.Session.UndoAsync());

            Activity undo = probe.Span(capture, "ProjectWorkflow.UndoAsync");
            Assert.Multiple(() =>
            {
                Assert.That(undo.Status, Is.EqualTo(ActivityStatusCode.Error),
                    "a failed undo must not read as a successful one");
                Assert.That(undo.GetTagItem("ihc.edit.status"), Is.EqualTo("failed"));
                Assert.That(undo.GetTagItem("error.type"), Is.EqualTo("System.TimeoutException"));
            });
        }
    }

    /// <summary>
    /// The other half, and the reason the three cases are classified rather than collapsed: an undo with
    /// nothing to undo returns the same <c>false</c> as a failure, and must NOT be reported as one.
    /// </summary>
    [Test]
    public async Task Undo_WithNothingToUndo_IsNotReportedAsAFailure()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "ProjectWorkflow."))
        {
            using ShellHarness harness = ShellHarness.Create();
            ihc_openvisual.ViewModels.MainWindowViewModel vm = harness.CreateViewModel();
            await vm.InitializeAsync();

            using TraceProbe probe = TraceProbe.Start();
            Assert.That(await harness.Session.UndoAsync(), Is.False);

            Activity undo = probe.Span(capture, "ProjectWorkflow.UndoAsync");
            Assert.Multiple(() =>
            {
                Assert.That(undo.Status, Is.EqualTo(ActivityStatusCode.Unset),
                    "nothing to undo is the operation working, not failing");
                Assert.That(undo.GetTagItem("ihc.edit.status"), Is.EqualTo("ok"));
                Assert.That(undo.GetTagItem("error.type"), Is.Null);
            });
        }
    }
}
