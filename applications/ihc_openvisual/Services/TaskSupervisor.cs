using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Vis.Problems;

namespace ihc_openvisual.Services;

/// <summary>
/// The application's ONE supervised fire-and-forget.
///
/// <para>A task nobody awaits is a task nobody observes: its fault surfaces on the finalizer thread through
/// <c>TaskScheduler.UnobservedTaskException</c> — arbitrarily later, attributed to nothing, and possibly after
/// the state it concerned has moved on. Handing the task here instead observes it at once and routes the fault
/// to the sink with the ORIGIN it was started from, which is the one fact the fault itself cannot carry.</para>
///
/// <para><b>Static, like the guard beside it</b>, because the callers are view code-behind and a worker — layers
/// with no constructor a port could be injected through. The port is set once by the composition root, and a
/// fault reported before it arrives is BUFFERED rather than dropped — the start-up reporters run first, so
/// "no port yet" is the normal state at exactly the moment the most interesting faults happen.</para>
/// </summary>
internal static class TaskSupervisor
{
    /// <summary>How many faults reported before a port is attached are kept.</summary>
    /// <remarks>
    /// The SINK's own bound, not a second one chosen to match it: what this buffer holds is on its way there,
    /// so a backlog it could not accept would be a bound with nothing behind it. The OLDEST is what a full
    /// buffer gives up, for <see cref="InternalErrorLog"/>'s reason — a fault storm is the normal shape of a
    /// bad day, and the newest faults are the ones a reader still has a chance of acting on.
    /// </remarks>
    internal const int BufferCapacity = InternalErrorLog.DefaultCapacity;

    private static Action<InternalError>? port;

    /// <summary>What was reported while no port was attached, oldest first. Guarded by <see cref="Gate"/>.</summary>
    private static readonly Queue<InternalError> Buffered = new();

    /// <summary>Guards <see cref="port"/> and <see cref="Buffered"/> TOGETHER, which is the point of it.</summary>
    /// <remarks>
    /// Deciding "is a port attached?" and "then buffer instead" have to be one step. Read separately, a report
    /// racing an attach can see no port, then buffer into a queue the attach has already drained — which loses
    /// exactly the fault this buffer exists to keep.
    /// </remarks>
    private static readonly object Gate = new();

    /// <summary>
    /// Points the supervisor at the sink and hands it whatever was reported before it arrived, oldest first.
    /// Called once, by the composition root. Passing null detaches it, which is what a test does when it has
    /// finished with its own.
    /// </summary>
    /// <remarks>
    /// DETACHING DISCARDS the backlog and re-arms buffering. The supervisor is process-wide static and the suite
    /// detaches in teardown, so a backlog that survived a detach would hand one test's faults to the next, and a
    /// one-shot latch that never re-armed would make the behaviour untestable.
    /// </remarks>
    /// <param name="faultPort">Where a supervised fault goes, or null to report nowhere.</param>
    internal static void ReportTo(Action<InternalError>? faultPort)
    {
        InternalError[] backlog;
        lock (Gate)
        {
            port = faultPort;
            backlog = [.. Buffered];
            Buffered.Clear();
        }
        if (faultPort is not { } sink)
        {
            return;   // Detached: the backlog above was the discard.
        }
        // Drained OUTSIDE the lock. The sink marshals to the owning thread and raises its own change event, and
        // holding this type's lock across another component's work is how two correct components deadlock.
        foreach (InternalError fault in backlog)
        {
            Deliver(sink, fault);
        }
    }

    /// <summary>
    /// Observes <paramref name="work"/> and reports a fault in it as <c>app.openvisual.unexpected</c>.
    /// <para>
    /// RETURNS the observing task rather than discarding it, so this method contains no unobserved task of its
    /// own — a supervisor that leaked one would be the defect it exists to remove. Callers discard the return,
    /// which is the one discard the containment gate admits.
    /// </para>
    /// </summary>
    /// <param name="work">The task to observe.</param>
    /// <param name="origin">Where it was started, as <c>Type.Member</c> — the fault cannot say this itself.</param>
    /// <returns>The observing continuation.</returns>
    internal static Task Fire(Task work, string origin)
    {
        ArgumentNullException.ThrowIfNull(work);
        // Continues ALWAYS, and reads Exception — which is what marks the antecedent OBSERVED. Deliberately not
        // OnlyOnFaulted: that option CANCELS the continuation when the antecedent succeeds, so the task this
        // returns would fault-as-cancelled on the happy path. Production discards it and would never notice;
        // a caller that awaited it would be misled.
        //
        // A CANCELLED antecedent has a null Exception and so reports nothing, which is right: the validation
        // worker abandons a generation on every document change, and routine cancellation is not a fault.
        //
        // Synchronous execution so the report happens on whichever thread faulted rather than being queued
        // behind unrelated work.
        return work.ContinueWith(
            finished => Report(finished.Exception, origin),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Reports the fault an <see cref="AggregateException"/> wraps, unwrapping a lone inner one.</summary>
    internal static void Report(AggregateException? failure, string origin)
    {
        if (failure is null)
        {
            return;
        }
        // The INNER exception where there is exactly one: an AggregateException wrapping a single fault says
        // nothing a reader wants and hides the type that does.
        Report(failure.InnerExceptions.Count == 1 ? failure.InnerExceptions[0] : failure, origin);
    }

    /// <summary>
    /// Reports one fault to the app's sink, or holds it until a port is attached.
    /// </summary>
    /// <remarks>
    /// Shared with <see cref="Views.HandlerGuard"/> rather than duplicated there. This type owns the app's
    /// STATIC-CONTEXT fault port — the one for layers with no constructor a port could be injected through — and
    /// the guard is another such layer. One port means one composition-root line and one fail-open guard; a
    /// second port beside it would be two copies of each, free to drift.
    /// </remarks>
    /// <param name="fault">The exception to report.</param>
    /// <param name="origin">Where it was observed, as <c>Type.Member</c> — the fault cannot say this itself.</param>
    internal static void Report(Exception fault, string origin)
    {
        if (!ClaimFirstSighting(fault))
        {
            return;   // Another net already reported this exact fault; see ClaimFirstSighting.
        }
        // The payload is built even with no port attached, and that is the price of the buffer: a report made
        // before the composition root arrives has to be KEPT, and it cannot be kept without being formed. The
        // interpolation calls Exception.ToString(), so an unattached run pays for a stack trace it may never
        // show — bounded by BufferCapacity, and cheap beside losing the start-up faults altogether.
        Report(HostProblems.Unexpected(fault), InternalErrorOrigin.Host, $"{origin}: {fault}");
    }

    /// <summary>
    /// Answers whether this is the FIRST net to see <paramref name="fault"/>, marking it as seen.
    /// </summary>
    /// <remarks>
    /// <para><b>One exception instance is one fault, however many nets catch it.</b> A fault raised inside a
    /// dispatcher-posted action reaches two: the dispatcher handler reports it and marks it handled, and the
    /// <c>DispatcherOperation</c> backing the post still carries it on a task that is later collected unobserved
    /// — so the unobserved-task layer reports the same instance again, arbitrarily later. That produced two rows
    /// and two metric increments for one event.</para>
    /// <para><b>The sink cannot do this.</b> <see cref="InternalErrorLog"/> folds repeats by code and captured
    /// detail, and the detail names the observing site, so the two sightings differ in exactly the field it
    /// compares. The identity survives only on the exception itself, which is where the question is answered.</para>
    /// <para><b>The FIRST sighting wins</b>, because it is the prompt one and the one whose origin says where the
    /// fault actually happened; the unobserved layer's stamp is a discovery time attributed to nothing.</para>
    /// <para><b>Fail-open on an exception that cannot be marked.</b> <see cref="Exception.Data"/> is writable on
    /// every BCL exception but a custom type may override it; a de-duplication that swallowed a fault it could
    /// not mark would be worse than the duplicate it removes.</para>
    /// </remarks>
    private static bool ClaimFirstSighting(Exception fault)
    {
        // UNDER THE GATE, because the check and the mark have to be one step: the nets that reach here run on
        // different threads — a dispatcher operation and the finalizer thread — and read separately, two of them
        // can both see an unmarked exception and both report it, which is the duplicate this exists to remove.
        // Exception.Data is not itself thread-safe either, so the lock is doing two jobs.
        //
        // The claim is the SDK's, not a second one beside it: an exception the SDK's own fault port already
        // reported arrives here marked, and this returns false for it.
        lock (Gate)
        {
            return InternalError.ClaimReport(fault);
        }
    }

    /// <summary>
    /// Reports a fault whose PROBLEM and ORIGIN the caller already knows.
    /// </summary>
    /// <remarks>
    /// The overload above is the common case — an exception the shell can only describe as unexpected, raised by
    /// its own code. A caller that reaches this one has something more precise to say: a platform boundary that
    /// discarded an exception is neither the host's fault nor describable by the catch-all's sentence.
    /// </remarks>
    /// <param name="problem">The problem to record, already bound.</param>
    /// <param name="origin">Which layer it belongs to.</param>
    /// <param name="detail">The captured technical text, including where it was observed.</param>
    internal static void Report(Problem problem, InternalErrorOrigin origin, string detail)
    {
        InternalError fault = InternalError.From(problem, origin, detail);
        Action<InternalError> sink;
        lock (Gate)
        {
            if (port is not { } attached)
            {
                // No port YET. The stamp on the fault is already taken, so what a late delivery loses is only
                // its promptness, not when it happened.
                if (Buffered.Count == BufferCapacity)
                {
                    Buffered.Dequeue();
                }
                Buffered.Enqueue(fault);
                return;
            }
            sink = attached;
        }
        Deliver(sink, fault);
    }

    /// <summary>Hands one fault to the sink, absorbing a sink that throws.</summary>
    /// <remarks>
    /// Fail-open, for the same reason the SDK's fault port is: a broken sink must not turn a reportable fault
    /// into a second one raised from a continuation nobody is watching either. Per fault rather than around the
    /// drain, so one bad row does not take the rest of the backlog with it.
    /// </remarks>
    private static void Deliver(Action<InternalError> sink, InternalError fault)
    {
        try
        {
            sink(fault);
        }
        catch (Exception)
        {
            // See the fail-open note above.
        }
    }
}
