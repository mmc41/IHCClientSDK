using System;
using System.Linq;
using ihc_openvisual;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Shutdown releases the composition root's collaborators in a stated order, and the order is the point: a
/// subscriber has to let go before the object raising the event is torn down.
///
/// <para>Run as a bare loop, one collaborator that could not tear itself down took the rest with it — the
/// remaining items were never released, and the fault left through the shutdown event, which has no caller to
/// catch it and therefore left no record at all. Both halves are asserted here: the rest still get released, and
/// the failure is reported.</para>
/// </summary>
[TestFixture]
public class ShutdownDisposalTests
{
    private sealed class Throwing : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("this collaborator cannot let go");
    }

    /// <summary>A collaborator whose failure cannot describe itself — see <see cref="UnreadableException"/>.</summary>
    private sealed class Undescribable : IDisposable
    {
        public void Dispose() => throw new UnreadableException();
    }

    private sealed class Counting : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    [Test]
    public void AFailingDisposal_DoesNotStopTheOnesAfterIt()
    {
        using CapturedFaults faults = new();
        Counting before = new();
        Counting after = new();

        ihc_openvisual.App.DisposeAll(before, new Throwing(), after);

        Assert.Multiple(() =>
        {
            Assert.That(before.Disposed, Is.True);
            Assert.That(after.Disposed, Is.True,
                "one collaborator that cannot let go must not keep the ones after it attached");
        });
    }

    [Test]
    public void AFailingDisposal_IsReported()
    {
        using CapturedFaults faults = new();

        ihc_openvisual.App.DisposeAll(new Throwing());

        Assert.Multiple(() =>
        {
            Assert.That(faults.Rows, Is.Not.Empty,
                "a fault off the shutdown event has no caller, so this is the only record it can have");
            Assert.That(faults.Rows.Single().Detail, Does.Contain(nameof(Throwing)),
                "and it has to name which teardown failed");
        });
    }

    /// <summary>
    /// The loop's guard has ONE statement in it — the report — so the loop is only as safe as that call is. A
    /// teardown whose exception cannot be read would, if reporting read it unguarded, throw out of the catch
    /// that exists to keep the remaining teardowns running, which is the defect this method was written to close
    /// rather than to relocate.
    /// </summary>
    [Test]
    public void ADisposalWhoseFaultCannotBeDescribed_StillDoesNotStopTheOnesAfterIt()
    {
        using CapturedFaults faults = new();
        Counting after = new();

        ihc_openvisual.App.DisposeAll(new Undescribable(), after);

        Assert.Multiple(() =>
        {
            Assert.That(after.Disposed, Is.True, "the report is the only statement in the guard, so it may not throw");
            Assert.That(faults.Rows, Is.Not.Empty,
                "and the row still lands, named by type where the exception could not word itself");
        });
    }

    [Test]
    public void ACleanShutdownReportsNothing()
    {
        using CapturedFaults faults = new();
        Counting only = new();

        ihc_openvisual.App.DisposeAll(only);

        Assert.Multiple(() =>
        {
            Assert.That(only.Disposed, Is.True);
            Assert.That(faults.Rows, Is.Empty);
        });
    }
}
