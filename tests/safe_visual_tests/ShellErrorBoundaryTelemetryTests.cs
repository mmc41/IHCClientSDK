using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The shell's two boundaries: start-up, and the catch every command funnels through.
///
/// Start-up used to produce NO span at all - the work between launch and a usable window hung off nothing, so
/// it could not be seen as one operation and its failures were reported unlike every other command's. The
/// error boundary itself swallows on purpose (the installer gets one fixed Danish sentence rather than an
/// English diagnostic naming element tags); that contract is unchanged, and the point is that swallowing is
/// no longer the same as vanishing.
/// </summary>
[TestFixture]
public class ShellErrorBoundaryTelemetryTests
{
    [Test]
    public async Task Startup_EmitsANamedSpan()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "MainWindowViewModel."))
        {
            using ShellHarness harness = ShellHarness.Create();
            MainWindowViewModel vm = harness.CreateViewModel();

            using TraceProbe probe = TraceProbe.Start();
            await vm.InitializeAsync();

            Activity startup = probe.Span(capture, "MainWindowViewModel.InitializeAsync");
            Assert.Multiple(() =>
            {
                Assert.That(startup.Status, Is.EqualTo(ActivityStatusCode.Unset), "a clean start-up is not an error");
                Assert.That(startup.GetTagItem("ihc.edit.status"), Is.EqualTo("ok"));
            });
        }
    }

    /// <summary>
    /// A start-up that fails must report like any other command failure - and still reach the Danish dialog.
    /// </summary>
    [Test]
    public async Task AFailingCommand_ReachesTheDanishDialog_AndCarriesTheNormalizedErrorType()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "MainWindowViewModel."))
        {
            using ShellHarness harness = ShellHarness.Create();
            MainWindowViewModel vm = harness.CreateViewModel();
            await vm.InitializeAsync();

            // A faulting StateChanged subscriber makes the next command throw inside the boundary.
            harness.Session.StateChanged += (_, _) => throw new System.TimeoutException("a faulting refresh");

            using TraceProbe probe = TraceProbe.Start();
            await vm.NewCommand.ExecuteAsync(null);

            // The failing span is THIS command's, found by the probe rather than by taking the last error in a
            // process-wide capture — which would be whichever fixture errored most recently.
            Activity failed = probe.Spans(capture).Single(s => s.Status == ActivityStatusCode.Error);
            Assert.Multiple(() =>
            {
                Assert.That(failed.GetTagItem("ihc.edit.status"), Is.EqualTo("failed"));
                Assert.That(failed.GetTagItem("error.type"), Is.EqualTo("System.TimeoutException"),
                    "the normalized type, not the English diagnostic the installer never sees");
                Assert.That(harness.Dialogs.LastProblem, Is.Not.Null,
                    "the Danish problem dialog still appears - the boundary's contract is unchanged");
            });
        }
    }

    /// <summary>
    /// The dialog is DISMISSED and gone, so a fault that only ever appeared there flashed
    /// once and left nothing behind. The boundary now does both — one dialog AND one durable row per fault.
    /// </summary>
    /// <remarks>
    /// The row is asserted to carry the SAME message the dialog carries. Two channels describing one fault in
    /// two different ways is worse than one channel: a support case would then have to decide which of them to
    /// believe.
    /// </remarks>
    [Test]
    public async Task AFailingCommandLeavesOneDialogAndOneDurableRow()
    {
        using ShellHarness harness = ShellHarness.Create();
        InternalErrorLog sink = new();
        MainWindowViewModel vm = harness.CreateViewModel(internalErrors: sink);
        await vm.InitializeAsync();
        Assert.That(sink.Rows, Is.Empty, "non-vacuity: a healthy start-up leaves no row");

        harness.Session.StateChanged += (_, _) => throw new System.TimeoutException("a faulting refresh");
        await vm.NewCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProblem, Is.Not.Null, "the dialog still appears");
            InternalErrorRow row = sink.Rows.Single();
            Assert.That(row.Error.Message, Is.EqualTo(harness.Dialogs.LastProblem!.Message),
                "and says exactly what the dialog said — one fault, one description");
            Assert.That(row.Error.Origin, Is.EqualTo(Ihc.Vis.Problems.InternalErrorOrigin.Host));
            Assert.That(row.Error.Detail, Does.Contain("a faulting refresh"),
                "with the original exception captured, which the dialog deliberately never shows");
            Assert.That(row.Error.Detail, Does.Contain(nameof(MainWindowViewModel)),
                "and the operation it escaped from, which the exception cannot say itself");
        });
    }

    /// <summary>
    /// The panel LISTS what the boundary appends. The last hop: without the sink reaching the panel, every layer
    /// beneath is wired and the user still sees nothing.
    /// </summary>
    [Test]
    public async Task AFailingCommandsRowIsListedInTheProblemsPanel()
    {
        using ShellHarness harness = ShellHarness.Create();
        InternalErrorLog sink = new();
        MainWindowViewModel vm = harness.CreateViewModel(internalErrors: sink);
        await vm.InitializeAsync();

        harness.Session.StateChanged += (_, _) => throw new System.TimeoutException("a faulting refresh");
        await vm.NewCommand.ExecuteAsync(null);

        Assert.That(vm.Problems.Rows.OfType<InternalErrorRowViewModel>().Count(), Is.EqualTo(1),
            "the shell's own panel shows it, which is the whole point of a durable row");
    }

    /// <summary>
    /// Activating a finding is a command like any other and needs the same boundary. It did not have one: the
    /// method was a bare <c>async Task</c> and the panel discards the task it returns, so a fault on the way to
    /// the fix produced NOTHING - no dialog, no log record, no span. The shipped symptom is the worst kind:
    /// double-click a finding and the application appears to ignore you.
    /// </summary>
    [Test]
    public async Task AFailingProblemActivation_ReachesTheDanishDialog_AndAFailedSpan()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "MainWindowViewModel."))
        {
            using ShellHarness harness = ShellHarness.Create();
            MainWindowViewModel vm = harness.CreateViewModel();
            await vm.InitializeAsync();
            ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;

            // Activation reveals the finding's element by moving the owning pane's selected node, so a subscriber
            // that faults there faults INSIDE the activation - the same technique the test above uses on
            // StateChanged. One-shot, so the fault cannot follow the view-model into teardown.
            bool faulted = false;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.SelectedInstallationNode) && !faulted)
                {
                    faulted = true;
                    throw new System.TimeoutException("a faulting reveal");
                }
            };

            using TraceProbe probe = TraceProbe.Start();
            await ProblemsTestData.ActivateAsync(harness, vm, locality, null, "doc-name-empty");

            Activity failed = probe.Spans(capture).Single(s => s.Status == ActivityStatusCode.Error);
            Assert.Multiple(() =>
            {
                Assert.That(faulted, Is.True, "sanity: the reveal really was reached, so the fault is the one under test");
                Assert.That(failed.OperationName, Is.EqualTo("MainWindowViewModel.ActivateProblemAsync"),
                    "the failing span is the activation's own, not a caller's");
                Assert.That(failed.GetTagItem("error.type"), Is.EqualTo("System.TimeoutException"));
                Assert.That(harness.Dialogs.LastProblem, Is.Not.Null,
                    "the installer is told something went wrong instead of watching a double-click do nothing");
            });
        }
    }
}
