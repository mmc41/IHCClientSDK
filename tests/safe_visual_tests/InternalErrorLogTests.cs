using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis.Problems;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The sink every fault in the application ends up in. It is the LAST place a fault can be reported to, which
/// decides most of its shape: it must survive a storm, it must survive its own readers, and it must be
/// appendable from threads that are not the UI's — the dispatcher, the unobserved-task layer and
/// <c>AppDomain</c> all deliver from somewhere else.
/// </summary>
[TestFixture]
public class InternalErrorLogTests
{
    private static InternalError Fault(string code = "internal.unexpected", string detail = "at Foo()") =>
        ProblemsTestData.Fault(code, "Uventet fejl under 'Foo'.", "boom", detail: detail);

    [Test]
    public void TheSameFaultTwice_IsOneRowWithACount()
    {
        var log = new InternalErrorLog();

        log.Append(Fault());
        log.Append(Fault());
        log.Append(Fault(detail: "at Bar()"));

        Assert.Multiple(() =>
        {
            Assert.That(log.Rows, Has.Count.EqualTo(2), "the repeat costs a counter, not a slot");
            Assert.That(log.Rows[0].Occurrences, Is.EqualTo(2));
            Assert.That(log.Rows[1].Occurrences, Is.EqualTo(1),
                "a different captured detail is a different fault, however equal its code");
        });
    }

    /// <summary>The timestamp cannot be part of the identity, or nothing would ever de-duplicate.</summary>
    [Test]
    public void TwoObservationsAtDifferentTimes_AreStillOneFault()
    {
        var log = new InternalErrorLog();

        log.Append(Fault());
        log.Append(Fault() with { Observed = DateTimeOffset.UnixEpoch.AddHours(3) });

        Assert.Multiple(() =>
        {
            Assert.That(log.Rows, Has.Count.EqualTo(1));
            Assert.That(log.Rows[0].Error.Observed, Is.EqualTo(DateTimeOffset.UnixEpoch),
                "the row keeps the FIRST sighting, which with the count says more than the latest would");
        });
    }

    [Test]
    public void ThePositionOfARepeatedRow_DoesNotMove()
    {
        var log = new InternalErrorLog();
        log.Append(Fault(detail: "first"));
        log.Append(Fault(detail: "second"));

        log.Append(Fault(detail: "first"));

        Assert.That(log.Rows.Select(r => r.Error.Detail), Is.EqualTo(new[] { "first", "second" }).AsCollection,
            "a storm that reordered the list under the reader on every fault would be unreadable");
    }

    [Test]
    public void TheRingDropsTheOldest_RatherThanGrowingWithoutBound()
    {
        var log = new InternalErrorLog(capacity: 3);

        foreach (int i in Enumerable.Range(1, 5))
        {
            log.Append(Fault(detail: "fault " + i));
        }

        Assert.That(log.Rows.Select(r => r.Error.Detail),
            Is.EqualTo(new[] { "fault 3", "fault 4", "fault 5" }).AsCollection,
            "the newest are what a user is about to describe to someone");
    }

    /// <summary>
    /// The dispatcher, the unobserved-task layer and AppDomain all deliver off the UI thread, so a lost append
    /// under contention would lose exactly the faults hardest to reproduce.
    /// </summary>
    [Test]
    public async Task AppendingFromManyThreadsAtOnce_LosesNothing()
    {
        var log = new InternalErrorLog(capacity: 500);
        const int Threads = 16;
        const int Each = 25;

        await Task.WhenAll(Enumerable.Range(0, Threads).Select(thread => Task.Run(() =>
        {
            foreach (int i in Enumerable.Range(0, Each))
            {
                log.Append(Fault(detail: $"t{thread}-{i}"));
            }
        })));

        Assert.That(log.Rows, Has.Count.EqualTo(Threads * Each),
            "every distinct fault survived the contention");
    }

    [Test]
    public void MovingTheGeneration_ClearsTheLog()
    {
        var log = new InternalErrorLog();
        log.FollowGeneration(1);
        log.Append(Fault());

        log.FollowGeneration(2);

        Assert.That(log.Rows, Is.Empty, "an internal error lives for the session, and a load starts a new one");
    }

    [Test]
    public void TheSameGenerationAgain_ClearsNothing()
    {
        var log = new InternalErrorLog();
        log.FollowGeneration(4);
        log.Append(Fault());

        log.FollowGeneration(4);

        Assert.That(log.Rows, Has.Count.EqualTo(1),
            "the monitor announces on every publish; only a MOVE of the generation is a new document");
    }

    [Test]
    public void ChangedIsRaised_ForAnAppendAndForAClear_ButNotForANoOpGeneration()
    {
        var log = new InternalErrorLog();
        int changes = 0;
        log.Changed += (_, _) => changes++;

        log.Append(Fault());
        int afterAppend = changes;
        log.FollowGeneration(1);
        int afterNoOpClear = changes;
        log.FollowGeneration(2);

        Assert.Multiple(() =>
        {
            Assert.That(afterAppend, Is.EqualTo(1), "an append changes the rows");
            Assert.That(afterNoOpClear, Is.EqualTo(1), "a first generation with nothing to drop changes nothing");
            Assert.That(changes, Is.EqualTo(2), "and the clear that actually dropped a row announced itself");
        });
    }

    /// <summary>
    /// The sink survives its own readers. A subscriber that throws while being TOLD about a fault would
    /// otherwise destroy the record of the fault it was being told about — and there is nothing above the sink
    /// to report that to.
    /// </summary>
    [Test]
    public void ASubscriberThatThrows_DoesNotBreakTheAppend()
    {
        var log = new InternalErrorLog();
        log.Changed += (_, _) => throw new InvalidOperationException("a broken reader");

        Assert.DoesNotThrow(() => log.Append(Fault()));
        Assert.That(log.Rows, Has.Count.EqualTo(1), "and the fault is recorded, which is the point of the sink");
    }

    /// <summary>
    /// Every append is COUNTED, on an instrument of its own. Separate from <c>ihc.problem.raised</c>, which
    /// means "presented to a user through the dialog service": most faults are never presented at all, and
    /// folding them in would silently change what every existing query over that counter measures.
    /// </summary>
    [Test]
    public void EveryAppend_IsCounted_WithItsCodeAndOrigin()
    {
        using Ihc.Tests.Shared.TelemetryCapture counts =
            Ihc.Tests.Shared.TelemetryCapture.ListenWithTracingDisabled(
                ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
                instruments: new[] { "ihc.internal_error.observed", "ihc.problem.raised" });
        var log = new InternalErrorLog();

        log.Append(Fault(detail: "first"));
        log.Append(Fault(detail: "first"));   // a repeat: one row, but two occurrences
        log.Append(Fault(code: "internal.rule-failed", detail: "other"));

        var observed = counts.Points
            .Where(p => (string?)p.Tag("ihc.problem.code") is not null)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(observed, Has.Length.EqualTo(3),
                "counted per OCCURRENCE, repeats included — the list says what is wrong, the metric how often");
            Assert.That(observed.Count(p => (string?)p.Tag("ihc.problem.code") == "internal.rule-failed"),
                Is.EqualTo(1));
            Assert.That(observed.Select(p => (string?)p.Tag("ihc.internal_error.origin")).Distinct(),
                Is.EqualTo(new[] { "Sdk" }).AsCollection,
                "the origin travels on the point, because it is not derivable from the code");
        });
    }

    /// <summary>The announcement goes through the host's own marshal, so a background append reaches the UI
    /// thread the same way every other background result does.</summary>
    [Test]
    public void ChangedIsAnnouncedThroughTheSuppliedPost()
    {
        List<Action> posted = [];
        var log = new InternalErrorLog(post: posted.Add);
        int changes = 0;
        log.Changed += (_, _) => changes++;

        log.Append(Fault());

        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Zero, "nothing is announced until the marshal runs it");
            Assert.That(posted, Has.Count.EqualTo(1));
        });
        posted[0]();
        Assert.That(changes, Is.EqualTo(1));
    }
}
