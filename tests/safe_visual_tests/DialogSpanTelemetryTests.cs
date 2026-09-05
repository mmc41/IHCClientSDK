using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using ihc_openvisual.Services;
using Ihc.Tests.Shared;
using Ihc.Vis;
using Ihc.Vis.Reporting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Where the waiting goes.
///
/// <para>A modal's time is a person thinking, and without a span of its own it is billed to whatever operation
/// raised the dialog — silently. Measured on a live save before this existed: <c>ProjectWorkflow.SaveToAsync</c>
/// read 13.6 s over a <c>ProjectAppService.Save</c> of 24 ms, all of the difference being a failure dialog
/// nobody had dismissed yet, and the save-duration histogram recorded the 13.6 s. A reader could not tell that
/// from slow file I/O; with the child span it is a subtraction.</para>
///
/// <para>These tests run with NO owner window, which is exactly the interesting shape for them: every door
/// returns at its own owner guard, so the assertions are about the span the door opens rather than about
/// Avalonia's modal machinery, and no test has to dismiss a dialog to finish.</para>
/// </summary>
[TestFixture]
public class DialogSpanTelemetryTests : AvaloniaTestBase
{
    private const string Scope = "IhcOpenVisual";

    [AvaloniaTest]
    public void AWindowDialogAndAPickerEachOpenASpanNamedForTheDoorTheInstallerUsed()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(Scope,
            spanPrefix: "AvaloniaDialogService.");
        var dialogs = new AvaloniaDialogService(NullLoggerFactory.Instance);

        _ = dialogs.EditPropertiesAsync("Egenskaber", "navn", "note");   // a window dialog that ANSWERS a value
        _ = dialogs.ShowModuleMapAsync(DatalineModuleMap.Empty);         // one that answers nothing
        _ = dialogs.PickOpenProjectAsync(null);                          // a native picker

        // Both dialog shapes, deliberately: a door that returns a value and one that returns none take
        // different funnels, and only the valued one had a test until the void funnel showed up uncovered.
        Assert.That(capture.Spans.Select(s => s.OperationName), Is.EquivalentTo(new[]
        {
            "AvaloniaDialogService.EditPropertiesAsync",
            "AvaloniaDialogService.ShowModuleMapAsync",
            "AvaloniaDialogService.PickOpenProjectAsync",
        }), "each modal carries the name of the door, not of the helper behind it");
    }

    /// <summary>
    /// The button box — the shape the measured 13.6 s failure dialog had — and the property that matters most:
    /// the span is open for as long as the box is. Asserted by absence and then presence, because a capture
    /// records a span when it STOPS.
    /// </summary>
    [AvaloniaTest]
    public void TheSpanOfAButtonBoxStaysOpenUntilTheBoxIsDismissed()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(Scope,
            spanPrefix: "AvaloniaDialogService.");
        Window shell = new();
        CurrentTestWindow = shell;
        shell.Show();
        Dispatcher.UIThread.RunJobs();
        var dialogs = new AvaloniaDialogService(NullLoggerFactory.Instance) { Owner = shell };

        Task<bool> answered = dialogs.ConfirmAsync("Titel", "Besked?");
        Dispatcher.UIThread.RunJobs();

        Assert.That(capture.Spans, Is.Empty, "nobody has answered yet, so the wait is still being timed");

        shell.OwnedWindows.Single().Close();   // the title-bar X, which resolves to the safe default
        Dispatcher.UIThread.RunJobs();

        Assert.That(answered.IsCompleted, Is.True);
        Assert.That(capture.Spans.Select(s => s.OperationName),
            Is.EqualTo(new[] { "AvaloniaDialogService.ConfirmAsync" }),
            "the span ends when the person answers, so its duration IS the wait");

        shell.Close();
    }

    /// <summary>
    /// The pass-through, pinned: two public doors share one private picker, and the span has to report the
    /// door. A literal inside the helper would name the helper for both, which is the case where the name
    /// stops distinguishing anything.
    /// </summary>
    [AvaloniaTest]
    public void TwoDoorsSharingOnePickerAreToldApart()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(Scope,
            spanPrefix: "AvaloniaDialogService.");
        var dialogs = new AvaloniaDialogService(NullLoggerFactory.Instance);

        _ = dialogs.PickSaveReportAsync("rapport.html", ReportFormat.Html);
        _ = dialogs.PickSaveFindingsAsync("fund.xml");

        Assert.That(capture.Spans.Select(s => s.OperationName), Is.EquivalentTo(new[]
        {
            "AvaloniaDialogService.PickSaveReportAsync",
            "AvaloniaDialogService.PickSaveFindingsAsync",
        }));
    }

    /// <summary>
    /// The coded door, which is the one the audit's worst measurement was: a failure dialog that held a 24 ms
    /// save open for 13.6 s. Routed through the message door it produced a span named <c>ShowMessageAsync</c> —
    /// the same name an informational box gets, so a reader could not tell which of them was doing the holding.
    /// It carries the door's own name and the problem's code.
    /// </summary>
    [AvaloniaTest]
    public void AProblemDialogIsNamedForItsCodedDoorAndCarriesTheCode()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(Scope,
            spanPrefix: "AvaloniaDialogService.");
        // An owner and a dismissal, unlike the doors above: the coded door renders through the button box, which
        // stays open until someone answers it — and a capture records a span when it STOPS.
        Window shell = new();
        CurrentTestWindow = shell;
        shell.Show();
        Dispatcher.UIThread.RunJobs();
        var dialogs = new AvaloniaDialogService(NullLoggerFactory.Instance) { Owner = shell };
        Ihc.Vis.Problems.Problem problem = HostProblems.ControllerRequiredSend();

        Task shown = dialogs.ShowProblemAsync("Fejl", problem);
        Dispatcher.UIThread.RunJobs();
        shell.OwnedWindows.Single().Close();
        Dispatcher.UIThread.RunJobs();

        Assert.That(shown.IsCompleted, Is.True);
        Activity modal = capture.Spans.Single();
        Assert.Multiple(() =>
        {
            Assert.That(modal.OperationName, Is.EqualTo("AvaloniaDialogService.ShowProblemAsync"),
                "the door the installer met, not the box it renders through");
            Assert.That(modal.GetTagItem("ihc.problem.code"), Is.EqualTo(problem.Code.Value),
                "and WHICH problem held the operation open");
        });

        shell.Close();
    }

    /// <summary>
    /// A dialog span must be a CHILD of whatever raised it — that is the whole mechanism by which the caller's
    /// remaining time becomes readable. Asserted against an explicit ambient operation rather than against a
    /// real workflow, so the test says one thing.
    /// </summary>
    [AvaloniaTest]
    public void TheModalSpanIsAChildOfTheOperationThatRaisedIt()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(Scope,
            spanPrefix: "AvaloniaDialogService.");
        using TraceProbe probe = TraceProbe.Start("RaisingOperation");
        var dialogs = new AvaloniaDialogService(NullLoggerFactory.Instance);

        _ = dialogs.EditPropertiesAsync("Egenskaber", "navn", "note");

        Activity modal = probe.Span(capture, "AvaloniaDialogService.EditPropertiesAsync");
        Assert.That(modal.Parent, Is.Not.Null,
            "a modal that parents nothing leaves its wait attributed to no operation at all");
        Assert.That(modal.Parent!.OperationName, Is.EqualTo("RaisingOperation"));
    }

    /// <summary>
    /// And what a dialog leaves BEHIND, which is the other half of the same property. Starting a span makes it
    /// <see cref="Activity.Current"/>, and the funnel is a plain method — no async kickoff restores the
    /// caller's ambient activity for it. Unrestored, everything the gesture did AFTER the dialog became a child
    /// of a modal that had already stopped: the apply behind a properties dialog, the delete behind its
    /// confirm, the whole remainder of the gesture nested one level too deep and under a span that had ended.
    /// </summary>
    [AvaloniaTest]
    public async Task ADialogDoesNotAdoptWhatTheGestureDoesAfterIt()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(Scope,
            spanPrefix: "AvaloniaDialogService.");
        using TraceProbe probe = TraceProbe.Start("Gesture");
        var dialogs = new AvaloniaDialogService(NullLoggerFactory.Instance);

        _ = await dialogs.EditPropertiesAsync("Egenskaber", "navn", "note");
        await dialogs.ShowModuleMapAsync(DatalineModuleMap.Empty);   // the next thing the gesture does

        Activity first = probe.Span(capture, "AvaloniaDialogService.EditPropertiesAsync");
        Activity next = probe.Span(capture, "AvaloniaDialogService.ShowModuleMapAsync");
        Assert.Multiple(() =>
        {
            Assert.That(next.Parent, Is.Not.SameAs(first),
                "the second is not INSIDE the first — the first had closed before it opened");
            Assert.That(first.Parent, Is.Not.Null);
            Assert.That(next.Parent, Is.SameAs(first.Parent),
                "both belong to the gesture, which is what keeps the gesture's remaining time readable");
        });
    }

    /// <summary>
    /// A dialog can fail in two places and the funnel has to classify both: before it is built, which the
    /// synchronous arm records, and after — a window that cannot be shown, a storage provider that throws.
    /// Without the second arm the scope is disposed with the default outcome, so a modal that BROKE is
    /// recorded exactly like one the installer answered.
    /// </summary>
    [AvaloniaTest]
    public void ADialogThatFaultsAfterItIsRaised_IsRecordedFailedRatherThanAnswered()
    {
        Window shell = new();
        CurrentTestWindow = shell;
        shell.Show();
        Dispatcher.UIThread.RunJobs();
        shell.Close();   // an owner that can no longer host a modal
        Dispatcher.UIThread.RunJobs();
        using TelemetryCapture capture = TelemetryCapture.Listen(Scope,
            spanPrefix: "AvaloniaDialogService.");
        var dialogs = new AvaloniaDialogService(NullLoggerFactory.Instance) { Owner = shell };

        Assert.CatchAsync(() => dialogs.ShowModuleMapAsync(DatalineModuleMap.Empty),
            "the caller's exception is rethrown unchanged; recording it is additive");

        Activity modal = capture.Spans.Single();
        Assert.Multiple(() =>
        {
            Assert.That(modal.OperationName, Is.EqualTo("AvaloniaDialogService.ShowModuleMapAsync"));
            Assert.That(modal.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(modal.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
        });
    }
}
