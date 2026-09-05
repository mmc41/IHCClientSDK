using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

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
                Assert.That(startup.GetTagItem("ihc.operation.status"), Is.EqualTo("ok"));
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
                Assert.That(failed.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
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
    /// The widest catch shows the exception in the SHAPE it carries. It used to render every escaping exception
    /// as the shell's own framing alone, so a refusal carrying N independent findings arrived as one generic
    /// sentence and the findings that explain it were shown nowhere.
    /// </summary>
    /// <remarks>
    /// <c>RaisedProblemDisplayTests</c> pins the decider in isolation; this pins that the boundary is WIRED to
    /// it. The two are different claims, and the second is the one the shipped defect falsified — the decider
    /// was correct and unreachable from here.
    /// </remarks>
    [Test]
    public async Task AnAggregateEscapingTheBoundaryIsShownWithEveryItem()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        harness.Session.StateChanged += (_, _) => throw RefusedByValidation(
            "Mangler påkrævet attribut", "Ukendt attribut 'bogus' på <group>.");
        await vm.NewCommand.ExecuteAsync(null);

        Assert.That(harness.Dialogs.LastProblemAggregate, Is.Not.Null,
            "the boundary hands the exception to the shape decider instead of framing it generically");
        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProblemAggregate!.Items, Has.Length.EqualTo(2),
                "every independent finding survives the widest catch in the application");
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("Mangler påkrævet attribut"));
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("Ukendt attribut 'bogus' på <group>."));
        });
    }

    /// <summary>
    /// A CODED cause escaping the boundary is narrated rather than replaced: the installer is told which rule
    /// said no, and the durable row keeps the boundary's own framing plus the exception's full text.
    /// </summary>
    /// <remarks>
    /// The two channels differ here, and that is the design rather than a leak: the row is the record and holds
    /// strictly more — the operation the exception escaped from, which the dialog never shows and the exception
    /// cannot say itself.
    /// </remarks>
    [Test]
    public async Task ACodedCauseEscapingTheBoundaryIsNarratedWhileTheRowKeepsTheFraming()
    {
        using ShellHarness harness = ShellHarness.Create();
        InternalErrorLog sink = new();
        MainWindowViewModel vm = harness.CreateViewModel(internalErrors: sink);
        await vm.InitializeAsync();

        harness.Session.StateChanged += (_, _) => throw new Ihc.Vis.Io.ProjectFormatException(
            Ihc.Vis.Io.LoadRefusalCodes.Empty, "the stream holds no bytes");
        await vm.NewCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProblemChain, Is.Not.Null,
                "a chain carrier is one failure restated more precisely, not a list");
            Assert.That(harness.Dialogs.LastProblem!.Code.Value, Is.EqualTo("load-empty"),
                "the installer is shown the coded cause, not the shell's catch-all sentence");
            InternalErrorRow row = sink.Rows.Single();
            Assert.That(row.Error.Detail, Does.Contain(nameof(MainWindowViewModel)),
                "and the row names the operation it escaped from, which no other channel can");
            Assert.That(row.Error.Detail, Does.Contain("the stream holds no bytes"),
                "with the original English diagnostic the dialog deliberately never shows");
        });
    }

    /// <summary>
    /// With no sink of its own the boundary must NOT claim it filed the row, or the report's fallback is
    /// suppressed and the fault reaches neither channel.
    /// </summary>
    /// <remarks>
    /// The sink is an optional constructor dependency; the composition root supplies one and a test need not.
    /// Passing a constant "already filed" reads correct against the wiring that happens to ship and loses the
    /// fault everywhere else — the one claim a boundary must never make on someone else's behalf.
    /// </remarks>
    [Test]
    public async Task WithNoSinkOfItsOwnTheBoundaryLeavesTheFaultToTheReportsFallback()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();   // no internalErrors
        await vm.InitializeAsync();

        using CapturedFaults captured = new();
        harness.Session.StateChanged += (_, _) => throw new System.TimeoutException("a faulting refresh");
        await vm.NewCommand.ExecuteAsync(null);

        Assert.That(captured.Rows.Select(r => r.Detail), Has.One.Contains("a faulting refresh"),
            "the fault still reaches the application's fault port, exactly once");
    }

    private static Ihc.Vis.Validation.ProjectValidationException RefusedByValidation(params string[] errors) =>
        new(Ihc.Vis.Problems.OperationCodes.Save, Ihc.Vis.Validation.ProjectValidationResult.FromFindings(
        [
            .. errors.Select((message, i) => new Ihc.Vis.Validation.ProjectValidationFinding(
                Ihc.Vis.Validation.ValidationSeverity.Error, "attr-required", $"_0x{i:x}", message)),
        ]));

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
