using System;
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
/// with no constructor a port could be injected through. The port is set once by the composition root and
/// NO-OPS when unset, so a test, a design-time instance and a headless run all supervise without reporting.</para>
/// </summary>
internal static class TaskSupervisor
{
    private static Action<InternalError>? port;

    /// <summary>
    /// Points the supervisor at the sink. Called once, by the composition root. Passing null detaches it, which
    /// is what a test does when it has finished with its own.
    /// </summary>
    /// <param name="faultPort">Where a supervised fault goes, or null to report nowhere.</param>
    internal static void ReportTo(Action<InternalError>? faultPort) => Volatile.Write(ref port, faultPort);

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
    /// Reports one fault to the app's sink, if a port has been set.
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
        // Checked BEFORE the payload is built, because building it is the expensive half: the interpolation
        // below calls Exception.ToString(), which formats the whole stack trace. Unset is the normal state of a
        // test, a design-time instance and a headless run, and none of them should pay for a report nobody
        // receives.
        if (Volatile.Read(ref port) is null)
        {
            return;
        }
        Report(HostProblems.Unexpected(fault), InternalErrorOrigin.Host, $"{origin}: {fault}");
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
        if (Volatile.Read(ref port) is not { } sink)
        {
            return;
        }
        try
        {
            sink(InternalError.From(problem, origin, detail));
        }
        catch (Exception)
        {
            // Fail-open, for the same reason the SDK's fault port is: a broken sink must not turn a reportable
            // fault into a second one raised from a continuation nobody is watching either.
        }
    }
}
