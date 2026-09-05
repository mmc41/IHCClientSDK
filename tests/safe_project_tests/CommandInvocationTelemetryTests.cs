using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The command funnel: one count per invocation of a registered row.
///
/// Menu bar, toolbar, context flyout and gesture all materialize from ONE local function inside
/// <c>Register</c>, so counting there covers every route without the four surfaces having to agree on
/// anything. The claim is deliberately narrow - registered rows only, and no surface dimension, because the
/// function cannot see which surface invoked it - and these tests pin the narrowness as much as the counting.
///
/// <para>What it no longer claims: the funnel used to hand the row's task straight back, so it observed
/// neither how long the row ran nor whether it faulted, and its span closed at the row's first await. It now
/// AWAITS that task, which is what makes the span the gesture's root in time as well as in shape - and what
/// lets a fault reach the count as an <c>error.type</c> dimension. Both are pinned below.</para>
/// </summary>
[TestFixture]
public class CommandInvocationTelemetryTests
{
    /// <summary>
    /// The gate's assertion: BOTH the ordinary command and the gesture command are counted, because both
    /// materialize from the same local function. A gesture that bypassed the count would make keyboard use
    /// look like no use at all.
    /// </summary>
    [Test]
    public async Task InvokingARowThroughBothTheCommandAndTheGesture_CountsBothKeyedByTheRowId()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.Registry.Commands["app.settings"].ExecuteAsync(null);
        await vm.Registry.GestureCommands["app.settings"].ExecuteAsync(null);

        CapturedPoint[] counts = capture.Points
            .Where(c => (string?)c.Tag("ihc.command.id") == "app.settings").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(counts, Has.Length.EqualTo(2),
                "both routes go through the one shared local function, so both are counted");
            Assert.That(capture.Spans.Count(s => (string?)s.GetTagItem("ihc.command.id") == "app.settings"),
                Is.EqualTo(2), "one span per invocation, carrying the row id");
        });
    }

    /// <summary>
    /// The narrow claim, pinned: every counted id is a REGISTERED row. If anything outside the registry
    /// were ever counted here, the instrument would silently become something other than what it says.
    /// </summary>
    [Test]
    public async Task EveryCountedIdIsARegisteredRow()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.Registry.Commands["app.settings"].ExecuteAsync(null);
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);

        string[] counted = capture.Points.Select(c => (string?)c.Tag("ihc.command.id") ?? string.Empty).ToArray();

        Assert.That(counted, Is.Not.Empty);
        Assert.That(counted.All(id => vm.Registry.Commands.ContainsKey(id)), Is.True,
            "an id outside the registered row set would mean the counter measures something else");
    }

    /// <summary>
    /// The documented absence, asserted so a later change cannot add it by accident: no surface dimension.
    /// A SUCCEEDING row also carries no <c>error.type</c> - that one appears only on a fault, which
    /// <see cref="AFaultingRow_MarksTheInvocationSpanFailedAndKeepsTheCommandId"/> pins.
    /// </summary>
    [Test]
    public async Task TheCountCarriesNoSurfaceDimension()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.Registry.Commands["app.settings"].ExecuteAsync(null);

        CapturedPoint count = capture.Points[0];
        Assert.That(count.Tags.Keys, Is.EquivalentTo(new[] { "ihc.operation.status", "ihc.command.id" }),
            "the registry cannot observe which surface invoked it, so it must not pretend to");
    }

    /// <summary>
    /// The span is the gesture's root, so it has to LAST as long as the gesture. Returning the row's task
    /// closed it at the row's first await: measured against a live save, the root read 10 ms while the work
    /// it parented ran 20 s - a tree whose root timed none of it.
    /// <para>Asserted by absence and then presence, because a capture records a span when it STOPS: while the
    /// row is still working there must be no span, and completing the row must produce one.</para>
    /// </summary>
    [Test]
    public async Task TheSpanStaysOpenUntilTheRowsTaskCompletes_RatherThanEndingAtItsFirstAwait()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        var stillWorking = new TaskCompletionSource<Ihc.OperationOutcome>();
        vm.Registry.Register(new CommandSpec("test.slowRow", null, Surfaces.MenuBar,
            _ => stillWorking.Task, _ => EditVerdict.Allow));

        Task invocation = vm.Registry.Commands["test.slowRow"].ExecuteAsync(null);

        Assert.That(SpansFor(capture, "test.slowRow"), Is.Empty,
            "the row has not finished, so the span that is supposed to cover it must still be open");

        stillWorking.SetResult(Ihc.OperationOutcome.Ok);
        await invocation;

        Assert.That(SpansFor(capture, "test.slowRow"), Has.Length.EqualTo(1),
            "completing the row ends the span, so the span spans exactly the row's work");
    }

    /// <summary>
    /// The failure-to-gesture join: a fault deep in a workflow leaves the ancestors' status alone (they
    /// handled it and answered), so the only way back from a red span to the gesture that caused it is the
    /// trace root - which has to carry the command id AND be marked when the fault reaches it unhandled.
    /// </summary>
    [Test]
    public void AFaultingRow_MarksTheInvocationSpanFailedAndKeepsTheCommandId()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();

        vm.Registry.Register(new CommandSpec("test.faultingRow", null, Surfaces.MenuBar,
            _ => Task.FromException<Ihc.OperationOutcome>(new System.InvalidOperationException("boom")),
            _ => EditVerdict.Allow));

        Assert.ThrowsAsync<System.InvalidOperationException>(
            () => vm.Registry.Commands["test.faultingRow"].ExecuteAsync(null),
            "the caller's exception is rethrown unchanged; recording it is additive");

        Activity span = SpansFor(capture, "test.faultingRow").Single();
        CapturedPoint count = capture.Points
            .Single(p => (string?)p.Tag("ihc.command.id") == "test.faultingRow");

        Assert.Multiple(() =>
        {
            Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(span.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
            Assert.That(span.GetTagItem("error.type"), Is.EqualTo("System.InvalidOperationException"));
            Assert.That(span.GetTagItem("ihc.command.id"), Is.EqualTo("test.faultingRow"),
                "the id is what turns a red span back into the gesture a person made");
            Assert.That(count.Tag("error.type"), Is.EqualTo("System.InvalidOperationException"),
                "the count carries the failure too, now that the funnel observes the task");
        });
    }

    /// <summary>
    /// The ROLL-UP, which is the case a thrown exception does not reach. A shell that handles its own failure
    /// — a save that could not be written, reported in a dialog and answered to its caller — throws nothing,
    /// so awaiting the row leaves this span reading <c>ok</c> for a gesture that did not work. What closes it
    /// is the row ANSWERING an outcome, and the funnel putting that answer on the root.
    /// <para>Both signals, because the metric is the half a trace query cannot repair: a point on
    /// <c>ihc.command.invocation</c> can be joined to its own span and to nothing below it, so a failure that
    /// stops at a child span is invisible to every rate built on the counter.</para>
    /// </summary>
    [TestCase("failed", "internal.edit-failed", ActivityStatusCode.Error)]
    [TestCase("refused", "edit.no-project-open", ActivityStatusCode.Unset)]
    [TestCase("cancelled", null, ActivityStatusCode.Unset)]
    public async Task ARowsHandledOutcome_ReachesTheGesturesRootSpanAndItsCount(
        string status, string? problemCode, ActivityStatusCode spanStatus)
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();

        Ihc.OperationOutcome answered = status switch
        {
            "failed" => Ihc.OperationOutcome.FailedWith(problemCode!),
            "refused" => Ihc.OperationOutcome.Refused(problemCode!),
            _ => Ihc.OperationOutcome.Cancelled,
        };
        vm.Registry.Register(new CommandSpec($"test.{status}Row", null, Surfaces.MenuBar,
            _ => Task.FromResult(answered), _ => EditVerdict.Allow));

        await vm.Registry.Commands[$"test.{status}Row"].ExecuteAsync(null);

        Activity span = SpansFor(capture, $"test.{status}Row").Single();
        CapturedPoint count = capture.Points
            .Single(p => (string?)p.Tag("ihc.command.id") == $"test.{status}Row");

        Assert.Multiple(() =>
        {
            Assert.That(span.GetTagItem("ihc.operation.status"), Is.EqualTo(status),
                "the gesture's root says what the gesture did, not merely that nothing threw");
            Assert.That(span.Status, Is.EqualTo(spanStatus),
                "and only a failure is an Error - a refusal and a cancellation are the app working");
            Assert.That(span.GetTagItem("ihc.problem.code"), Is.EqualTo(problemCode));
            Assert.That(count.Tag("ihc.operation.status"), Is.EqualTo(status),
                "the count carries the same answer, or a failure rate built on it is wrong");
        });
    }

    /// <summary>
    /// The pre-execute hook is part of the gesture, so a throw from it is a gesture that FAILED. It used to run
    /// outside the guard, which disposed the scope with the default outcome — a failure recorded as a success,
    /// which is the single thing the outcome machinery exists to prevent.
    /// </summary>
    [Test]
    public void AThrowingPreExecuteHook_IsRecordedAsAFailedInvocation_NotASuccessfulOne()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        bool rowRan = false;
        var registry = new CommandRegistry(
            () => ShellContext.Empty,
            beforeExecute: _ => throw new System.InvalidOperationException("the bridge broke"));
        registry.Register(new CommandSpec("test.hookRow", null, Surfaces.MenuBar,
            _ => { rowRan = true; return Task.FromResult(Ihc.OperationOutcome.Ok); }, _ => EditVerdict.Allow));

        Assert.ThrowsAsync<System.InvalidOperationException>(
            () => registry.Commands["test.hookRow"].ExecuteAsync(null));

        Activity span = SpansFor(capture, "test.hookRow").Single();
        Assert.Multiple(() =>
        {
            Assert.That(rowRan, Is.False, "the hook threw, so the row never ran - and the count must say so");
            Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(span.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
            Assert.That(capture.Points.Single(p => (string?)p.Tag("ihc.command.id") == "test.hookRow")
                .Tag("error.type"), Is.EqualTo("System.InvalidOperationException"));
        });
    }

    /// <summary>
    /// The same claim on REAL rows, which is the half a fake row cannot make. Every case above hands the funnel
    /// a row that already answers an outcome, so it pins the plumbing and says nothing about the doors — and the
    /// doors were where it was missing: a command body that returns without telling its boundary leaves the
    /// gesture's root reading <c>ok</c>, whatever the gesture actually did.
    /// <para>Both of these are one event — the installer closed the picker the gesture opened — and it is
    /// neither a failure, nor a rule declining, nor the gesture doing what it was asked.</para>
    /// </summary>
    [TestCase("catalog.importFile")]
    [TestCase("catalog.importFolder")]
    public async Task ADismissedPickerOnARealRow_ReachesTheGesturesRootAsCancelled(string commandId)
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        // The fake answers null from both catalog pickers unless a test sets a path: the dialog was dismissed.
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });

        await vm.Registry.Commands[commandId].ExecuteAsync(null);

        Activity root = SpansFor(capture, commandId).Single();
        Assert.Multiple(() =>
        {
            Assert.That(root.GetTagItem("ihc.operation.status"), Is.EqualTo("cancelled"));
            Assert.That(root.Status, Is.EqualTo(ActivityStatusCode.Unset),
                "changing your mind is not breakage");
            Assert.That(capture.Points.Single(p => (string?)p.Tag("ihc.command.id") == commandId)
                    .Tag("ihc.operation.status"), Is.EqualTo("cancelled"),
                "and the count says so too, or a cancel rate cannot be told from a success rate");
        });
    }

    /// <summary>
    /// The library-save gesture, which is where the roll-up was most obviously missing: <c>ProjectWorkflow</c>
    /// marks its OWN span for a save that could not be written, and the gesture above it answered <c>ok</c>
    /// either way. Here the installer presses <i>Annuller</i> on the name-and-note dialog, so nothing is
    /// written at all.
    /// </summary>
    [Test]
    public async Task AnAbandonedLibrarySave_ReachesTheGesturesRootAsCancelled()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        harness.Dialogs.PropertiesResult = null;   // Annuller on "Gem Funktionsblok..."
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" });

        await vm.Registry.Commands["node.saveBlock"].ExecuteAsync(null);

        Assert.That(SpansFor(capture, "node.saveBlock").Single().GetTagItem("ihc.operation.status"),
            Is.EqualTo("cancelled"),
            "a library save the installer abandoned is not a library save that happened");
    }

    /// <summary>
    /// And the destructive one, where the difference matters most: answering <i>Nej</i> to the cascade confirm
    /// deletes nothing, so a delete rate that counted it as a delete would be measuring the prompt rather than
    /// the deletions.
    /// </summary>
    [Test]
    public async Task ADeleteTheInstallerDeclined_ReachesTheGesturesRootAsCancelled()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        TreeNodeViewModel locality = vm.InstallationNodes[0].Children[0];
        await harness.Session.AddProductAsync(locality.ElementId!.Value, "_0x2701");   // so the delete has to ask
        vm.SelectedNode = vm.InstallationNodes[0].Children[0];
        harness.Dialogs.ConfirmResult = false;                              // "Nej — behold lokaliteten"
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" });

        await vm.Registry.Commands["edit.delete"].ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(SpansFor(capture, "edit.delete").Single().GetTagItem("ihc.operation.status"),
                Is.EqualTo("cancelled"));
            Assert.That(harness.Session.Current!.FindById(locality.ElementId!.Value), Is.Not.Null,
                "and nothing was deleted, which is what makes 'cancelled' the true answer");
        });
    }

    /// <summary>
    /// A real row whose door FAILED rather than being abandoned. The import reported itself — dialog, log, its
    /// own span marked — and then answered a value the gesture read only for its count, so the root above it
    /// recorded success for an import that imported nothing.
    /// </summary>
    [Test]
    public async Task AFailingImportOnARealRow_ReachesTheGesturesRootAsFailed()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        harness.Dialogs.CatalogFolderPath = harness.TempPath("no-such-catalog-folder");
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" });

        await vm.Registry.Commands["catalog.importFolder"].ExecuteAsync(null);

        Activity root = SpansFor(capture, "catalog.importFolder").Single();
        Assert.Multiple(() =>
        {
            Assert.That(root.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
            Assert.That(root.GetTagItem("ihc.problem.code"),
                Is.EqualTo("app.openvisual.catalog-folder-missing"),
                "and WHICH failure, carried up from the run that recorded it rather than re-derived here");
        });
    }

    /// <summary>The captured invocation spans of one row, in the order they ended.</summary>
    private static Activity[] SpansFor(TelemetryCapture capture, string commandId) =>
        capture.Spans.Where(s => (string?)s.GetTagItem("ihc.command.id") == commandId).ToArray();
}
