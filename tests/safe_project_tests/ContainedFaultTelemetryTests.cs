using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using Ihc.Vis.Problems;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The bookkeeping that runs AFTER an operation has already succeeded — the save point, the recent entry, the
/// repaint — is contained on purpose: a fault there says nothing about whether the project reached disk, so the
/// operation keeps answering <c>Ok</c> and its span keeps status <c>Unset</c>. That contract is not in question
/// here.
///
/// <para>What is in question is what a READER of the trace can see. Contained that way and no further, the fault
/// left the span completely untouched: an operator opening the save's trace saw a flawless save, and the only
/// surviving evidence was a log record in a different stream. Recorded as an <c>exception</c> EVENT on the same
/// span, the fault travels with the operation it happened inside — which is also the shape the collector already
/// treats as "something went wrong in here", since it matches a span carrying an exception event even when the
/// status stayed unset.</para>
/// </summary>
[TestFixture]
public class ContainedFaultTelemetryTests
{
    /// <summary>Everything a case here reads back from one save.</summary>
    /// <param name="Outcome">What the save answered its caller.</param>
    /// <param name="Span">The save's own span, as the probe found it.</param>
    /// <param name="Faults">The durable rows that landed.</param>
    /// <param name="FileWritten">Whether the project reached disk — read before the scratch directory goes.</param>
    private sealed record SaveRun(
        Ihc.OperationOutcome Outcome,
        Activity Span,
        IReadOnlyList<InternalError> Faults,
        bool FileWritten);

    /// <summary>
    /// One <i>Gem som</i> with the repaint subscriber the case chooses. The repaint is the LAST contained step
    /// of a save and a subscriber is the one collaborator a test owns, so it is the door every case here uses.
    /// </summary>
    /// <remarks>
    /// A listener is opened for every case, including the clean one: with no listener the save's span is null,
    /// so the event is never written, the exception is never read, and a case meaning to exercise the recording
    /// would quietly exercise nothing. The capture closes before the span is handed back — an
    /// <see cref="Activity"/> is a plain object once stopped, while a listener left open would collect every
    /// later case in the assembly.
    /// </remarks>
    /// <param name="repaint">What the state-change subscriber does, or null for a save with nothing wrong.</param>
    private static async Task<SaveRun> SaveOnce(Action? repaint)
    {
        using TelemetryCapture spans = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanPrefix: "ProjectWorkflow.");
        using TraceProbe probe = TraceProbe.Start();
        List<InternalError> faults = [];
        using ShellHarness harness = ShellHarness.Create(faultSink: faults.Add);
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        if (repaint is { } subscriber)
        {
            harness.Session.StateChanged += (_, _) => subscriber();
        }
        harness.Dialogs.SavePath = harness.TempPath("saved.vis");

        Ihc.OperationOutcome outcome = await harness.Session.SaveAsAsync();
        return new SaveRun(
            outcome,
            probe.SpansNamed(spans, "ProjectWorkflow.SaveToAsync").Last(),
            faults,
            System.IO.File.Exists(harness.Dialogs.SavePath));
    }

    [Test]
    public async Task ABookkeepingFaultAfterASuccessfulSave_IsCarriedByTheSavesOwnSpan()
    {
        SaveRun save = await SaveOnce(() => throw new TimeoutException("a faulting repaint"));

        Assert.Multiple(() =>
        {
            Assert.That(save.Outcome.Status, Is.EqualTo(Ihc.OperationStatus.Ok),
                "the bytes reached disk, so the save succeeded");
            Assert.That(save.Span.Status, Is.EqualTo(ActivityStatusCode.Unset),
                "and the span says so — a contained fault does not fail the operation");
            Assert.That(save.Faults, Is.Not.Empty, "the fault is still a durable Intern fejl row");
            Assert.That(save.Span.Events.Any(e => e.Name == "exception"), Is.True,
                "the fault has to be ON the operation's span; without it a trace reader sees a flawless save");
        });
    }

    [Test]
    public async Task TheEventOnTheSpanNamesTheFaultThatWasContained()
    {
        SaveRun save = await SaveOnce(() => throw new TimeoutException("a faulting repaint"));

        ActivityEvent fault = save.Span.Events.First(e => e.Name == "exception");
        Assert.That(
            fault.Tags.Any(t => t.Key == "exception.type" && (t.Value as string) == typeof(TimeoutException).FullName),
            Is.True,
            "the event carries WHAT was contained, not merely that something was");
    }

    /// <summary>
    /// The event is recorded from the catch that has ALREADY contained the fault, and recording it reads the
    /// exception. A subscriber throwing something whose message cannot be read would, unguarded, have that read
    /// throw out of the containment itself: the save un-contained after the bytes were on disk, the caller
    /// handed a failure over a file that was written, and the durable row skipped on the way out.
    /// </summary>
    [Test]
    public async Task ABookkeepingFaultThatCannotDescribeItself_StaysContained()
    {
        SaveRun save = await SaveOnce(() => throw new UnreadableException());

        Assert.Multiple(() =>
        {
            Assert.That(save.Span.Events.Any(e => e.Name == "exception"), Is.False,
                "non-vacuity: writing the event is what failed, so there is no event — which is why the row "
                + "below is the record that has to survive");
            Assert.That(save.Outcome.Status, Is.EqualTo(Ihc.OperationStatus.Ok),
                "the bytes reached disk, and describing the fault is not allowed to change that");
            Assert.That(save.FileWritten, Is.True, "non-vacuity: it really saved");
            Assert.That(save.Faults, Is.Not.Empty,
                "and the row still lands — a fault that cannot word itself is still a fault worth recording");
        });
    }

    [Test]
    public async Task ACleanSaveLeavesNoEventOnItsSpan()
    {
        SaveRun save = await SaveOnce(repaint: null);

        Assert.Multiple(() =>
        {
            Assert.That(save.Span.Events.Any(), Is.False, "a save with nothing wrong in it says nothing went wrong");
            Assert.That(save.Faults, Is.Empty, "and leaves no row either");
        });
    }
}
