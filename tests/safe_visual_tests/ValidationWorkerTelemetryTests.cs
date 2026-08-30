using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using Microsoft.Extensions.Time.Testing;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// What a debounced validation run reports about itself.
///
/// The run is DEBOUNCED and COALESCED: several edits collapse into one run, and a run in flight can be
/// superseded by a newer document state before its result is bound. So the run belongs to no single edit,
/// which is why the edit that triggered it is attached as a LINK rather than as a parent - a parent edge
/// would claim a causal ownership that is not true of the second, third and fourth edits it also serves.
/// </summary>
[TestFixture]
public class ValidationWorkerTelemetryTests
{
    private static readonly EquatableArray<ValidationFinding> NoFindings =
        new System.Collections.Immutable.ImmutableArray<ValidationFinding>();

    /// <summary>A minimal project; the worker never inspects it, the fake validate delegate does.</summary>
    private static Project EmptyProject() =>
        new(new ProjectElement("utcs_project", null,
            new System.Collections.Immutable.ImmutableArray<(string, string)>(),
            new System.Collections.Immutable.ImmutableArray<ProjectElement>()));

    private static ValidationRequest RequestFor(Project project, int generation, int version) =>
        new(project, version, generation);

    private const string ProbeSourceName = "Ihc.WorkerProbe";
    private static readonly ActivitySource ProbeSource = new(ProbeSourceName);

    /// <summary>
    /// Notifies from inside a probe activity and returns its context, so this test's run can be told apart
    /// from every other fixture's - the listener is process-wide and the suite runs many workers.
    /// </summary>
    private static ActivityContext NotifyUnderProbe(ValidationWorker worker, ValidationRequest request)
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == ProbeSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using Activity probe = ProbeSource.StartActivity("Probe")!;
        worker.Notify(request);
        return probe.Context;
    }

    /// <summary>
    /// THE CHARACTERIZATION TEST, written before the instrumentation and kept afterwards: what IS ambient
    /// inside the worker's run? Whether the run is trace-orphaned was a hypothesis rather than a measurement;
    /// this records the answer either way, because the link design is right regardless of what it turns out
    /// to be.
    /// </summary>
    [Test]
    public async Task Characterize_WhatIsAmbientInsideTheWorkersRun()
    {
        var clock = new FakeTimeProvider();
        Activity? insideRun = null;
        Activity? parentInsideRun = null;
        var done = new TaskCompletionSource();

        using var source = new ActivitySource("Ihc.CharacterizationProbe");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var worker = new ValidationWorker(
            validate: _ =>
            {
                insideRun = Activity.Current;
                parentInsideRun = Activity.Current?.Parent;
                return NoFindings;
            },
            onCompleted: _ => done.TrySetResult(),
            post: action => action(),
            time: clock);

        // Notify from INSIDE an activity, which is what an edit-driven notification looks like.
        using (source.StartActivity("TheEditThatTriggeredIt"))
        {
            worker.Notify(RequestFor(EmptyProject(), generation: 1, version: 1));
        }

        clock.Advance(System.TimeSpan.FromSeconds(1));
        await done.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        // Recorded, not asserted to be a particular value: this test exists to say what is TRUE.
        TestContext.Out.WriteLine($"Activity.Current inside the run: {insideRun?.OperationName ?? "<null>"}");
        TestContext.Out.WriteLine($"Its parent: {parentInsideRun?.OperationName ?? "<null>"}");

        Assert.That(insideRun?.OperationName, Is.Not.EqualTo("TheEditThatTriggeredIt"),
            "the run is off the notifying thread, so the notifier's activity must not be ambient inside it - " +
            "if this ever fails, the run has become synchronous and the link design should be revisited");
    }

    /// <summary>
    /// Waits for THIS test's spans, identified by the link back to its own probe context.
    ///
    /// Two hazards, both real and both hit while writing these: the completion callback is posted from
    /// INSIDE the run while the scope is still open, so waiting on it and then reading races the dispose
    /// that ends the span; and the listener is PROCESS-WIDE, so every other fixture's ValidationWorker
    /// lands here too and a bare Single() fails once the whole suite runs. Filtering by the link solves
    /// the second using the very mechanism under test.
    /// </summary>
    private static async Task<Activity[]> SpansAsync(TelemetryCapture capture, ActivityContext probe, int count)
    {
        Activity[] mine = Mine(capture, probe);
        for (int i = 0; i < 200 && mine.Length < count; i++)
        {
            await Task.Delay(25);
            mine = Mine(capture, probe);
        }
        return mine;
    }

    private static CapturedPoint[] PointsFor(TelemetryCapture capture, string outcome) =>
        capture.Points.Where(p => (string?)p.Tag("ihc.validation.outcome") == outcome).ToArray();

    private static Activity[] Mine(TelemetryCapture capture, ActivityContext probe) =>
        capture.SpansNamed("ValidationWorker.Run")
            .Where(s => s.Links.Any(l => l.Context.SpanId == probe.SpanId))
            .OrderBy(s => s.StartTimeUtc).ToArray();

    [Test]
    public async Task ABoundRun_ReportsBound_AndRecordsItsDuration()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "ValidationWorker.Run" },
            instruments: new[] { "ihc.validation.run.duration" });
        var clock = new FakeTimeProvider();
        var done = new TaskCompletionSource();

        using var worker = new ValidationWorker(
            validate: _ => NoFindings,
            onCompleted: _ => done.TrySetResult(),
            post: action => action(),
            time: clock);

        ActivityContext probe = NotifyUnderProbe(worker, RequestFor(EmptyProject(), 1, 1));
        clock.Advance(System.TimeSpan.FromSeconds(1));
        await done.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        Activity run = (await SpansAsync(capture, probe, 1)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(run.GetTagItem("ihc.validation.outcome"), Is.EqualTo("bound"));
            Assert.That(run.Status, Is.EqualTo(ActivityStatusCode.Unset), "a bound run is not an error");
            Assert.That(PointsFor(capture, "bound"), Is.Not.Empty, "the run is timed");
            Assert.That(PointsFor(capture, "bound")[0].Tag("ihc.validation.outcome"), Is.EqualTo("bound"),
                "the metric dimension must agree with the span");
        });
    }

    /// <summary>A run whose result is no longer current binds nothing - and says so, rather than vanishing.</summary>
    [Test]
    public async Task ASupersededRun_ReportsSuperseded()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "ValidationWorker.Run" },
            instruments: new[] { "ihc.validation.run.duration" });
        var clock = new FakeTimeProvider();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var bound = new TaskCompletionSource();

        using var worker = new ValidationWorker(
            validate: _ =>
            {
                started.TrySetResult();
                release.Task.Wait();
                return NoFindings;
            },
            onCompleted: _ => bound.TrySetResult(),
            post: action => action(),
            time: clock);

        ActivityContext probe = NotifyUnderProbe(worker, RequestFor(EmptyProject(), 1, 1));
        clock.Advance(System.TimeSpan.FromSeconds(1));
        await started.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        // A newer version arrives while the first run is still working: its result is now stale.
        worker.Notify(RequestFor(EmptyProject(), generation: 1, version: 2));
        release.TrySetResult();
        clock.Advance(System.TimeSpan.FromSeconds(1));
        await bound.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        Assert.That((await SpansAsync(capture, probe, 1)).Select(s => s.GetTagItem("ihc.validation.outcome")?.ToString()),
            Does.Contain("superseded"),
            "the first run finished but its answer was already stale - that is not the same as succeeding");
    }

    [Test]
    public async Task AFaultingRun_ReportsFaulted_AndCarriesTheNormalizedErrorType()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "ValidationWorker.Run" },
            instruments: new[] { "ihc.validation.run.duration" });
        var clock = new FakeTimeProvider();
        var faulted = new TaskCompletionSource();

        using var worker = new ValidationWorker(
            validate: _ => throw new System.TimeoutException("the rule set hung"),
            onCompleted: _ => { },
            post: action => action(),
            time: clock,
            onFaulted: _ => faulted.TrySetResult());

        ActivityContext probe = NotifyUnderProbe(worker, RequestFor(EmptyProject(), 1, 1));
        clock.Advance(System.TimeSpan.FromSeconds(1));
        await faulted.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        Activity run = (await SpansAsync(capture, probe, 1)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(run.GetTagItem("ihc.validation.outcome"), Is.EqualTo("faulted"));
            Assert.That(run.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(run.GetTagItem("error.type"), Is.EqualTo("System.TimeoutException"));
        });
    }

    /// <summary>
    /// The link, not a parent. A debounced run serves several edits, so claiming one of them as its parent
    /// would be a causal statement that is false for the others.
    /// </summary>
    [Test]
    public async Task TheRunLinksBackToTheNotifyingContext_RatherThanBeingParentedByIt()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "ValidationWorker.Run" },
            instruments: new[] { "ihc.validation.run.duration" });
        var clock = new FakeTimeProvider();
        var done = new TaskCompletionSource();

        using var source = new ActivitySource("Ihc.LinkProbe");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var worker = new ValidationWorker(
            validate: _ => NoFindings,
            onCompleted: _ => done.TrySetResult(),
            post: action => action(),
            time: clock);

        ActivityContext editContext;
        using (Activity edit = source.StartActivity("TheEdit")!)
        {
            editContext = edit.Context;
            worker.Notify(RequestFor(EmptyProject(), generation: 1, version: 1));
        }

        clock.Advance(System.TimeSpan.FromSeconds(1));
        await done.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        Activity run = (await SpansAsync(capture, editContext, 1)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(run.Links.Select(l => l.Context.SpanId), Does.Contain(editContext.SpanId),
                "the triggering edit is reachable from the run");
            Assert.That(run.Parent?.SpanId, Is.Not.EqualTo(editContext.SpanId),
                "but it is a LINK, not a parent - the run serves every coalesced edit, not just this one");
        });
    }
}
