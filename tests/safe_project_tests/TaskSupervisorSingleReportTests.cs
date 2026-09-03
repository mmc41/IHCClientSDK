using System;
using ihc_openvisual.Services;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// ONE exception instance is ONE fault, however many of the application's nets happen to see it.
///
/// <para><b>The defect this pins.</b> A fault raised inside a dispatcher-posted action reaches two nets. The
/// dispatcher handler reports it and marks it handled — but the <c>DispatcherOperation</c> backing the post still
/// holds the exception on its own task, which is later collected unobserved, so
/// <c>TaskScheduler.UnobservedTaskException</c> reports the very same instance a second time. Measured: one
/// injected fault, two <i>Intern fejl</i> rows and two increments of <c>ihc.internal.error.observed</c>.</para>
///
/// <para><b>Why the sink's own de-duplication cannot do it.</b> <see cref="InternalErrorLog"/> folds repeats by
/// code AND captured detail, and the detail carries the observing site — <c>Dispatcher.UnhandledException</c>
/// against <c>TaskScheduler.UnobservedTaskException</c>. Two different strings, so two rows, correctly by that
/// rule. The duplication has to be recognised where the identity still exists: at the exception instance.</para>
///
/// <para><b>What it must NOT suppress.</b> Two separate faults that merely look alike are two events and stay
/// two rows — only a re-report of the same instance is dropped.</para>
/// </summary>
[TestFixture]
public class TaskSupervisorSingleReportTests
{
    /// <summary>The dispatcher net and the unobserved-task net, in that order, over one exception.</summary>
    [Test]
    public void TheSameExceptionSeenByTwoNetsIsReportedOnce()
    {
        using CapturedFaults faults = new();
        InvalidOperationException failure = new("a posted action broke");

        TaskSupervisor.Report(failure, "Dispatcher.UnhandledException");
        TaskSupervisor.Report(failure, "TaskScheduler.UnobservedTaskException");

        Assert.That(faults.Rows, Has.Count.EqualTo(1), "one fault, one row");
    }

    /// <summary>The FIRST net wins, because it is the one that knows where the fault actually happened.</summary>
    [Test]
    public void TheFirstNetToSeeItSuppliesTheOrigin()
    {
        using CapturedFaults faults = new();
        InvalidOperationException failure = new("a posted action broke");

        TaskSupervisor.Report(failure, "Dispatcher.UnhandledException");
        TaskSupervisor.Report(failure, "TaskScheduler.UnobservedTaskException");

        Assert.That(faults.Rows[0].Detail, Does.StartWith("Dispatcher.UnhandledException"),
            "the later, vaguer sighting must not overwrite the prompt one");
    }

    /// <summary>Two DISTINCT faults stay two, even when they say the same thing.</summary>
    [Test]
    public void TwoDistinctFaultsAreStillTwoReports()
    {
        using CapturedFaults faults = new();

        TaskSupervisor.Report(new InvalidOperationException("the work broke"), "SomeType.SomeMember");
        TaskSupervisor.Report(new InvalidOperationException("the work broke"), "SomeType.SomeMember");

        Assert.That(faults.Rows, Has.Count.EqualTo(2),
            "a second occurrence is a second event; only a re-sighting of one instance is dropped");
    }
}
