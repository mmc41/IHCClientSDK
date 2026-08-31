using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis.Problems;
using ihc_openvisual.Services;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// A fault reported BEFORE the composition root attaches the sink is kept, and delivered when it does.
///
/// <para><b>The hole.</b> <c>Report</c> returned early while the port was null, and the port is attached inside
/// <c>StartWithClassicDesktopLifetime</c> — which <c>Program</c> calls AFTER firing the telemetry self-check and
/// after registering the <c>UnobservedTaskException</c> handler. A self-check that failed fast therefore logged
/// a warning and left no durable row: the two reporters most likely to fire during start-up were the two
/// guaranteed to be talking to nobody.</para>
///
/// <para><b>Why buffer rather than move the self-check.</b> Buffering closes the race for EVERY pre-composition
/// reporter rather than the one that happened to be noticed, keeps <c>Program</c>'s documented "config and
/// telemetry first" ordering, and matches why this supervisor exists at all: "no port yet" is the same class of
/// hole as "no observer yet".</para>
///
/// <para><b>Bounded, dropping the oldest.</b> The same reasoning <c>InternalErrorLog</c> records: a fault storm
/// is the normal shape of a bad day, and the newest faults are the ones a reader still has a chance of acting
/// on.</para>
///
/// <para><b>Detaching DISCARDS and re-arms.</b> The supervisor is process-wide static and this suite detaches in
/// teardown. A one-shot latch that never re-armed would make the behaviour untestable, and a backlog that
/// survived a detach would leak one test's faults into the next.</para>
/// </summary>
[TestFixture]
public class TaskSupervisorBufferingTests
{
    [SetUp]
    [TearDown]
    public void DetachThePort() => TaskSupervisor.ReportTo(null);

    private static void ReportOne(string message) =>
        TaskSupervisor.Report(new InvalidOperationException(message), "SomeType.SomeMember");

    private static List<InternalError> Attach()
    {
        List<InternalError> reported = [];
        TaskSupervisor.ReportTo(reported.Add);
        return reported;
    }

    [Test]
    public void AFaultReportedBeforeAttachIsDeliveredWhenThePortArrives()
    {
        ReportOne("the self-check failed before anything was listening");

        List<InternalError> reported = Attach();

        Assert.Multiple(() =>
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported[0].Detail, Does.Contain("the self-check failed before anything was listening"),
                "the captured text survives the wait, which is the whole point of keeping it");
            Assert.That(reported[0].Origin, Is.EqualTo(InternalErrorOrigin.Host));
        });
    }

    [Test]
    public void BufferedFaultsAreDeliveredOldestFirst()
    {
        ReportOne("first");
        ReportOne("second");
        ReportOne("third");

        List<InternalError> reported = Attach();

        Assert.That(reported.Select(Ordinal).ToArray(), Is.EqualTo(new[] { "first", "second", "third" }).AsCollection,
            "order is the reader's only clue to what happened before what");
    }

    [Test]
    public void DetachingDiscardsTheBacklogSoItCannotReachTheNextPort()
    {
        ReportOne("belongs to whoever was running before");

        TaskSupervisor.ReportTo(null);
        List<InternalError> reported = Attach();

        Assert.That(reported, Is.Empty);
    }

    [Test]
    public void TheBufferIsBoundedAndDropsTheOldest()
    {
        for (int i = 0; i < TaskSupervisor.BufferCapacity + 1; i++)
        {
            ReportOne($"fault-{i}");
        }

        List<InternalError> reported = Attach();

        Assert.Multiple(() =>
        {
            Assert.That(reported, Has.Count.EqualTo(TaskSupervisor.BufferCapacity));
            Assert.That(reported.Select(Ordinal), Does.Not.Contain("fault-0"),
                "the OLDEST is what a bounded buffer gives up");
            Assert.That(Ordinal(reported[^1]), Is.EqualTo($"fault-{TaskSupervisor.BufferCapacity}"),
                "and the newest is kept, which is the half a reader can still act on");
        });
    }

    /// <summary>
    /// A fault reported while a port IS attached goes straight there and is not also buffered — otherwise a
    /// later detach-and-reattach would deliver it a second time.
    /// </summary>
    [Test]
    public void AFaultReportedWhileAttachedIsNotAlsoBuffered()
    {
        List<InternalError> first = Attach();
        ReportOne("delivered live");
        Assert.That(first, Has.Count.EqualTo(1), "precondition: it went straight to the attached port");

        List<InternalError> second = Attach();

        Assert.That(second, Is.Empty, "and there was no second copy waiting in the buffer");
    }

    /// <summary>The buffer is drained by the attach, not merely copied: a second attach receives nothing.</summary>
    [Test]
    public void TheBacklogIsDeliveredOnceAndThenGone()
    {
        ReportOne("only once");

        List<InternalError> first = Attach();
        List<InternalError> second = Attach();

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(second, Is.Empty);
        });
    }

    /// <summary>
    /// A sink that throws while the backlog drains must not take the rest of the backlog with it — the same
    /// fail-open rule the live path already follows, applied to the delivery of what was held.
    /// </summary>
    [Test]
    public void ABrokenSinkDoesNotStopTheRestOfTheBacklog()
    {
        ReportOne("first");
        ReportOne("second");
        List<InternalError> survived = [];

        TaskSupervisor.ReportTo(fault =>
        {
            if (Ordinal(fault) == "first")
                throw new InvalidOperationException("the sink is broken");
            survived.Add(fault);
        });

        Assert.That(survived.Select(Ordinal).ToArray(), Is.EqualTo(new[] { "second" }).AsCollection);
    }

    /// <summary>The message this test wrote, recovered from the captured detail.</summary>
    /// <remarks>
    /// The detail is <c>"&lt;origin&gt;: &lt;exception&gt;"</c> and the exception was never thrown, so it carries
    /// no stack — the text after the last separator is exactly the message.
    /// </remarks>
    private static string Ordinal(InternalError fault) =>
        fault.Detail[(fault.Detail.LastIndexOf(": ", StringComparison.Ordinal) + 2)..];
}
