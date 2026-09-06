using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using Ihc.Vis.Reporting;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// A report is the one gesture with a PICKER between the command and the work: the menu row raises a dialog, the
/// dialog's own command calls the workflow, and the workflow is where the failure is worded. Every existing test
/// of a failing report calls the workflow directly, which skips both of the layers in between — so what was
/// pinned was the workflow, and never the gesture.
///
/// <para>Driven here the way a person drives it: invoke the menu row, answer the picker, and read the two
/// channels the path has. The picker's answer runs INSIDE the command's scope, which is what makes the span
/// assertion meaningful — a responder that ran afterwards would be asserting about a scope already closed.</para>
/// </summary>
[TestFixture]
public class ReportGestureFailureTests
{
    [Test]
    public async Task AReportThatCannotBeViewed_IsShownAndTraced()
    {
        using TelemetryCapture spans = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName);
        using TraceProbe probe = TraceProbe.Start();
        CapturingLoggerFactory logs = new();
        using ShellHarness harness = ShellHarness.Create(loggerFactory: logs);
        MainWindowViewModel vm = harness.CreateViewModel(logs);
        await vm.InitializeAsync();

        harness.Dialogs.Reset();
        logs.Clear();
        // A machine with no handler for the document — the condition the workflow detects for itself, with no
        // exception to carry it.
        harness.Dialogs.OpenExternalUrlSucceeds = false;
        harness.Dialogs.ReportPickerResponder = picker => picker.ViewInBrowserCommand.ExecuteAsync(null);

        await vm.FunctionsReportCommand.ExecuteAsync(null);

        Activity failed = probe.Spans(spans)
            .Single(s => s.OperationName == "ProjectReportWorkflow.ViewInBrowserAsync");
        Assert.Multiple(() =>
        {
            // The SAME assertion the workflow-door test makes, not a weaker restatement of it: driving the
            // operation through the gesture changes which layers run, never what "the scope was marked" means.
            WorkflowFailureScopeTests.AssertRefusedScope(harness, failed, "app.openvisual.report-not-openable");
            Assert.That(logs.Messages, Has.Some.Contains("Error"));
        });
    }

    [Test]
    public async Task AReportThatIsGenerated_LeavesItsSpanClean()
    {
        using TelemetryCapture spans = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName);
        using TraceProbe probe = TraceProbe.Start();
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        harness.Dialogs.Reset();
        harness.Dialogs.OpenExternalUrlSucceeds = true;
        harness.Dialogs.ReportPickerResponder = picker => picker.ViewInBrowserCommand.ExecuteAsync(null);

        await vm.FunctionsReportCommand.ExecuteAsync(null);

        Activity viewed = probe.Spans(spans)
            .Single(s => s.OperationName == "ProjectReportWorkflow.ViewInBrowserAsync");
        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProblem, Is.Null, "nothing went wrong, so nothing is shown");
            Assert.That(viewed.Status, Is.EqualTo(ActivityStatusCode.Unset));
            Assert.That(harness.Dialogs.LastOpenedUrl, Is.Not.Null, "and the report really was opened");
        });
    }
}
