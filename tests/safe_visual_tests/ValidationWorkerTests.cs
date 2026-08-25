using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The background validation loop, as a component that knows nothing about Avalonia, about
/// <c>ProjectWorkflow</c>, or about where a generation number comes from.
///
/// <para>It exists because a whole-project run is CPU work and the shell must stay responsive while a user types.
/// ADR-001's host contract is the specification it implements — capture on the owning thread, compute on the pool,
/// discard a superseded result, marshal the binding back, honour the token at both boundaries — plus the
/// SINGLE-FLIGHT rule the ADR leaves to the host: at most one run in flight, and at most one pending request,
/// which always carries the NEWEST snapshot. Without that rule a burst of keystrokes queues a run per keystroke
/// and the panel spends its life catching up.</para>
///
/// <para>GENERATION IS OPAQUE HERE. The worker never asks what a generation means or how one is derived; it
/// compares them for equality and nothing else. That is precisely what keeps this component testable without a
/// shell: everything below drives it through its own surface, on a FakeTimeProvider, with a validate delegate the
/// test controls.</para>
///
/// <para>These tests ARE multithreaded, unavoidably and by design — the subject is a thread-pool loop. Every wait
/// is bounded, so a regression fails on a timeout rather than hanging the suite.</para>
/// </summary>
public class ValidationWorkerTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    /// <summary>A distinct project instance per call — the worker is keyed on identity here, not on content.</summary>
    private static Project NewProject() =>
        new ProjectAppService(new IhcSettings()).CreateNew(new ProjectDetails("P", "I", "DK"));

    /// <summary>Records what the worker asked for and what it bound back, and can hold a run open on demand.</summary>
    private sealed class Probe
    {
        public ConcurrentQueue<Project> Validated { get; } = new();
        public ConcurrentQueue<ValidationOutcome> Bound { get; } = new();
        public ConcurrentQueue<Exception> Faults { get; } = new();
        public int Concurrent;
        public int MaxConcurrent;
        public Func<Project, EquatableArray<ValidationFinding>>? Body;

        public EquatableArray<ValidationFinding> Validate(Project project)
        {
            int now = Interlocked.Increment(ref Concurrent);
            InterlockedMax(ref MaxConcurrent, now);
            try
            {
                Validated.Enqueue(project);
                return Body?.Invoke(project) ?? EquatableArray<ValidationFinding>.Empty;
            }
            finally
            {
                Interlocked.Decrement(ref Concurrent);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int seen = Volatile.Read(ref target);
            while (value > seen)
            {
                int was = Interlocked.CompareExchange(ref target, value, seen);
                if (was == seen)
                    return;
                seen = was;
            }
        }
    }

    private static (ValidationWorker Worker, Probe Probe, FakeTimeProvider Clock) Build()
    {
        Probe probe = new();
        FakeTimeProvider clock = new();
        ValidationWorker worker = new(
            probe.Validate,
            outcome => probe.Bound.Enqueue(outcome),
            // The marshal step, inverted for the test: the shell passes Dispatcher.UIThread.Post here, so the
            // worker itself never names Avalonia. Running the callback inline is the honest test double — it
            // keeps the ORDER the worker produced without adding a queue the worker cannot see.
            action => action(),
            clock,
            fault => probe.Faults.Enqueue(fault));
        return (worker, probe, clock);
    }

    private static async Task IdleAsync(ValidationWorker worker) =>
        await worker.Idle.WaitAsync(Bound);

    [Test]
    public async Task ABurstOfChangesRunsOnceAfterTheQuietPeriod()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        using ValidationWorker _ = worker;
        Project first = NewProject(), second = NewProject(), third = NewProject();

        worker.Notify(new ValidationRequest(first, 1, 1));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        worker.Notify(new ValidationRequest(second, 2, 1));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        worker.Notify(new ValidationRequest(third, 3, 1));
        Assert.That(probe.Validated, Is.Empty, "nothing runs while the changes are still arriving");

        clock.Advance(Debounce);
        await IdleAsync(worker);

        Assert.Multiple(() =>
        {
            Assert.That(probe.Validated, Has.Count.EqualTo(1), "one run for the whole burst, not one per change");
            Assert.That(probe.Validated.Single(), Is.SameAs(third), "and it validates the NEWEST snapshot");
            Assert.That(probe.Bound.Single().Version, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task ASaveDoesNotRevalidateBecauseNothingAboutTheDocumentChanged()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        using ValidationWorker _ = worker;
        Project project = NewProject();

        worker.Notify(new ValidationRequest(project, 7, 1));
        clock.Advance(Debounce);
        await IdleAsync(worker);
        Assert.That(probe.Validated, Has.Count.EqualTo(1), "precondition: the edit ran");

        // MarkSaved raises StateChanged with the version UNCHANGED. Same generation, same version — the document
        // is byte-for-byte what was just validated, so a second run would produce the same rows at CPU cost.
        worker.Notify(new ValidationRequest(project, 7, 1));
        clock.Advance(Debounce);
        await IdleAsync(worker);

        Assert.That(probe.Validated, Has.Count.EqualTo(1), "a save-only notification runs nothing");
    }

    [Test]
    public async Task AChangeArrivingMidRunIsCoalescedIntoExactlyOneFollowUpCarryingTheNewestSnapshot()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        using ValidationWorker _ = worker;
        Project running = NewProject(), a = NewProject(), b = NewProject(), c = NewProject();
        using ManualResetEventSlim hold = new(false);
        using ManualResetEventSlim entered = new(false);
        probe.Body = p =>
        {
            if (ReferenceEquals(p, running))
            {
                entered.Set();
                hold.Wait(Bound);
            }
            return EquatableArray<ValidationFinding>.Empty;
        };

        worker.Notify(new ValidationRequest(running, 1, 1));
        clock.Advance(Debounce);
        Assert.That(entered.Wait(Bound), Is.True, "precondition: the first run is in flight and held open");

        // Three more changes while that run is stuck. They must collapse to ONE follow-up, not three.
        foreach ((Project p, int v) in new[] { (a, 2), (b, 3), (c, 4) })
        {
            worker.Notify(new ValidationRequest(p, v, 1));
            clock.Advance(Debounce);
        }
        Assert.That(probe.Validated, Has.Count.EqualTo(1), "no second run starts while the first is in flight");

        hold.Set();
        await IdleAsync(worker);

        Assert.Multiple(() =>
        {
            Assert.That(probe.Validated, Has.Count.EqualTo(2), "the held run, then exactly one coalesced follow-up");
            Assert.That(probe.Validated.Last(), Is.SameAs(c), "the follow-up carries the newest snapshot, not the oldest queued one");
            Assert.That(probe.MaxConcurrent, Is.EqualTo(1), "never two runs in flight");
        });
    }

    [Test]
    public async Task ASupersededResultIsDiscardedRatherThanBound()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        using ValidationWorker _ = worker;
        Project stale = NewProject(), fresh = NewProject();
        using ManualResetEventSlim hold = new(false);
        using ManualResetEventSlim entered = new(false);
        probe.Body = p =>
        {
            if (ReferenceEquals(p, stale))
            {
                entered.Set();
                hold.Wait(Bound);
            }
            return EquatableArray<ValidationFinding>.Empty;
        };

        worker.Notify(new ValidationRequest(stale, 1, 1));
        clock.Advance(Debounce);
        Assert.That(entered.Wait(Bound), Is.True, "precondition: the doomed run is in flight");

        worker.Notify(new ValidationRequest(fresh, 2, 1));
        clock.Advance(Debounce);
        hold.Set();
        await IdleAsync(worker);

        Assert.Multiple(() =>
        {
            Assert.That(probe.Bound.Select(o => o.Version), Is.EqualTo(new[] { 2 }),
                "latest-wins: the run whose document moved on is thrown away, never merged");
            Assert.That(probe.Validated, Has.Count.EqualTo(2), "it still RAN — the engine cannot be interrupted mid-run");
        });
    }

    [Test]
    public async Task AReplacementStartsANewGenerationCancelsPendingWorkAndValidatesTheNewProjectOnce()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        using ValidationWorker _ = worker;
        Project old = NewProject(), replacement = NewProject();

        // An edit is pending — notified, but its quiet period has not elapsed.
        worker.Notify(new ValidationRequest(old, 5, 1));
        clock.Advance(TimeSpan.FromMilliseconds(100));

        // Ny/Åbn/Luk: a different generation. The pending run for the PREVIOUS file must never happen — binding
        // its rows into the freshly opened project is exactly the stale-rows defect this keying exists to stop.
        worker.Notify(new ValidationRequest(replacement, 1, 2));
        clock.Advance(Debounce);
        await IdleAsync(worker);

        Assert.Multiple(() =>
        {
            Assert.That(probe.Validated.Select(p => ReferenceEquals(p, replacement)), Is.EqualTo(new[] { true }),
                "the replacement is validated exactly once, and the superseded file not at all");
            Assert.That(probe.Bound.Select(o => o.Generation), Is.EqualTo(new[] { 2 }));
        });
    }

    [Test]
    public async Task AResultFromAnAbandonedGenerationIsNeverBound()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        using ValidationWorker _ = worker;
        Project old = NewProject(), replacement = NewProject();
        using ManualResetEventSlim hold = new(false);
        using ManualResetEventSlim entered = new(false);
        probe.Body = p =>
        {
            if (ReferenceEquals(p, old))
            {
                entered.Set();
                hold.Wait(Bound);
            }
            return EquatableArray<ValidationFinding>.Empty;
        };

        worker.Notify(new ValidationRequest(old, 5, 1));
        clock.Advance(Debounce);
        Assert.That(entered.Wait(Bound), Is.True, "precondition: the old generation's run is in flight");

        worker.Notify(new ValidationRequest(replacement, 1, 2));
        clock.Advance(Debounce);
        hold.Set();
        await IdleAsync(worker);

        Assert.That(probe.Bound.Select(o => o.Generation), Is.EqualTo(new[] { 2 }),
            "a run that completes after its project was closed binds nothing");
    }

    [Test]
    public async Task DisposalCancelsEverythingSoNothingBindsAfterTheViewModelDetaches()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        Project project = NewProject();

        worker.Notify(new ValidationRequest(project, 1, 1));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        worker.Dispose();

        clock.Advance(Debounce);
        await Task.Delay(50);

        Assert.Multiple(() =>
        {
            Assert.That(probe.Validated, Is.Empty, "a disposed worker starts nothing");
            Assert.That(probe.Bound, Is.Empty, "and binds nothing");
        });

        Assert.DoesNotThrow(worker.Dispose, "disposal is idempotent");
    }

    [Test]
    public async Task AFaultingRunIsObservedAndDoesNotEscapeTheLoop()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        using ValidationWorker _ = worker;
        Project bad = NewProject(), good = NewProject();
        probe.Body = p => ReferenceEquals(p, bad)
            ? throw new InvalidOperationException("rule blew up")
            : EquatableArray<ValidationFinding>.Empty;

        worker.Notify(new ValidationRequest(bad, 1, 1));
        clock.Advance(Debounce);
        await IdleAsync(worker);

        Assert.That(probe.Faults.Select(f => f.Message), Is.EqualTo(new[] { "rule blew up" }),
            "the fault is reported, not swallowed and not left to TaskScheduler.UnobservedTaskException");

        // And the loop survives it: the next change still validates.
        worker.Notify(new ValidationRequest(good, 2, 1));
        clock.Advance(Debounce);
        await IdleAsync(worker);

        Assert.Multiple(() =>
        {
            Assert.That(probe.Bound.Select(o => o.Version), Is.EqualTo(new[] { 2 }), "the faulted run bound nothing");
            Assert.That(probe.Validated, Has.Count.EqualTo(2), "a fault does not wedge the loop");
        });
    }

    [Test]
    public async Task TheOutcomeCarriesTheFindingsTheRunProducedAndTheKeysItRanFor()
    {
        (ValidationWorker worker, Probe probe, FakeTimeProvider clock) = Build();
        using ValidationWorker _ = worker;
        Project project = NewProject();
        ValidationFinding finding = new(
            new Ihc.Vis.Problems.Problem(new Ihc.Vis.Problems.ProblemCode("synthetic-warning"), "En advarsel.",
                EquatableArray<Ihc.Vis.Problems.ProblemArgument>.Empty),
            ValidationSeverity.Warning, ValidationCategory.Documentation, null,
            EquatableArray<FindingLocation>.Empty);
        probe.Body = _ => System.Collections.Immutable.ImmutableArray.Create(finding);

        worker.Notify(new ValidationRequest(project, 11, 4));
        clock.Advance(Debounce);
        await IdleAsync(worker);

        ValidationOutcome outcome = probe.Bound.Single();
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Findings, Is.EqualTo(new[] { finding }));
            Assert.That(outcome.Version, Is.EqualTo(11));
            Assert.That(outcome.Generation, Is.EqualTo(4), "the keys travel with the result so a binder can re-check them");
        });
    }
}
