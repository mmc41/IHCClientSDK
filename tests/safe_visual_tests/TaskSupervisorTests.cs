using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis.Problems;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The application's one supervised fire-and-forget. A task nobody awaits is a task nobody observes: its fault
/// surfaces on the finalizer thread through <c>TaskScheduler.UnobservedTaskException</c>, arbitrarily later and
/// attributed to nothing. Handing it here observes it at once and reports it with the ORIGIN it was started
/// from — the one fact the fault itself cannot carry.
/// <para>
/// The port is process-wide static, so every test here restores it. That is the cost of a supervisor callable
/// from view code-behind, which has no constructor to inject one through.
/// </para>
/// </summary>
[TestFixture]
public class TaskSupervisorTests
{
    // SetUp as well as TearDown: detaching also DISCARDS the pre-attach buffer, so this is what keeps a
    // fault another fixture reported while nothing was attached out of this one's expectations.
    [SetUp]
    [TearDown]
    public void DetachThePort() => TaskSupervisor.ReportTo(null);

    private static (List<InternalError> Reported, Task Observed) FireFaulting(Exception failure)
    {
        List<InternalError> reported = [];
        TaskSupervisor.ReportTo(reported.Add);
        return (reported, TaskSupervisor.Fire(Task.FromException(failure), "SomeType.SomeMember"));
    }

    [Test]
    public async Task AFaultingTask_IsReportedWithItsOrigin()
    {
        var (reported, observed) = FireFaulting(new InvalidOperationException("the work broke"));
        await observed;

        Assert.Multiple(() =>
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported[0].Origin, Is.EqualTo(InternalErrorOrigin.Host),
                "the shell supervised it, so the shell is where it was observed");
            Assert.That(reported[0].Detail, Does.Contain("SomeType.SomeMember"),
                "the origin is the one fact the fault cannot carry — without it a supervised fault says only "
                + "that something, somewhere, broke");
            Assert.That(reported[0].Detail, Does.Contain("the work broke"));
        });
    }

    /// <summary>
    /// The INNER exception is what gets reported. A task's fault arrives wrapped in an
    /// <see cref="AggregateException"/>, and a wrapper around exactly one fault hides the type a reader needs
    /// while adding nothing.
    /// </summary>
    [Test]
    public async Task ASingleFault_IsUnwrappedFromItsAggregate()
    {
        var (reported, observed) = FireFaulting(new TimeoutException("too slow"));
        await observed;

        Assert.That(reported[0].Detail, Does.Contain(nameof(TimeoutException))
            .And.Not.Contain(nameof(AggregateException)));
    }

    [Test]
    public async Task ATaskThatSucceeds_ReportsNothing()
    {
        List<InternalError> reported = [];
        TaskSupervisor.ReportTo(reported.Add);

        await TaskSupervisor.Fire(Task.CompletedTask, "SomeType.SomeMember");

        Assert.That(reported, Is.Empty, "supervision is not surveillance: only a fault is an event");
    }

    /// <summary>
    /// A CANCELLED task is not a fault. The validation worker abandons a generation on every document change,
    /// so reporting cancellation would turn ordinary editing into a stream of internal errors.
    /// </summary>
    [Test]
    public async Task ACancelledTask_ReportsNothing()
    {
        List<InternalError> reported = [];
        TaskSupervisor.ReportTo(reported.Add);

        await TaskSupervisor.Fire(Task.FromCanceled(new System.Threading.CancellationToken(canceled: true)),
            "ValidationWorker.SomeMember");

        Assert.That(reported, Is.Empty);
    }

    /// <summary>Unset, the supervisor still OBSERVES — it simply has nowhere to report to. That is what makes
    /// it safe in a test, a design-time instance and a headless run.</summary>
    [Test]
    public async Task WithNoPort_ItObservesWithoutThrowing()
    {
        TaskSupervisor.ReportTo(null);

        Task observed = TaskSupervisor.Fire(
            Task.FromException(new InvalidOperationException("nobody is listening")), "SomeType.SomeMember");

        await observed;
        Assert.That(observed.IsCompletedSuccessfully, Is.True,
            "the observing continuation completes; the fault it read is not re-raised into it");
    }

    /// <summary>
    /// Fail-open. The observing continuation is itself unawaited in production, so a port that threw would
    /// raise a SECOND unobserved fault — the exact defect the supervisor exists to remove, caused by the
    /// supervisor.
    /// </summary>
    [Test]
    public async Task APortThatThrows_DoesNotFaultTheObservingContinuation()
    {
        TaskSupervisor.ReportTo(_ => throw new InvalidOperationException("the sink is broken"));

        Task observed = TaskSupervisor.Fire(
            Task.FromException(new InvalidOperationException("the work broke")), "SomeType.SomeMember");

        await observed;
        Assert.That(observed.IsCompletedSuccessfully, Is.True);
    }

    /// <summary>The antecedent is OBSERVED, which is the whole point: an unread fault would reach the finalizer
    /// thread later instead.</summary>
    [Test]
    public async Task TheSupervisedTasksFault_IsMarkedObserved()
    {
        Task faulted = Task.FromException(new InvalidOperationException("the work broke"));
        TaskSupervisor.ReportTo(null);

        await TaskSupervisor.Fire(faulted, "SomeType.SomeMember");

        Assert.That(faulted.Exception, Is.Not.Null,
            "reading Exception is what marks it observed, and the supervisor has already done so");
    }
}
