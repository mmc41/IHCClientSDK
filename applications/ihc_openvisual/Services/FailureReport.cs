using System;
using System.Threading.Tasks;
using Ihc;
using Ihc.Vis.Problems;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Services;

/// <summary>
/// The ONE way a workflow reports that something went wrong: the span's outcome, the log record, and the dialog
/// the installer sees — in that order, from one call.
///
/// <para><b>Why a helper rather than a convention.</b> The three were written out by hand at every workflow catch
/// site, and "forgot the <c>SetOutcome</c>" was therefore an available mistake — one that costs nothing at the
/// time and shows up much later as an operation that failed for the user and succeeded in the telemetry. Two
/// shipped sites had exactly that shape, and neither was found by a test: they were found by reading. Folding
/// the triplet into one call removes the mistake by construction instead of by inspection.</para>
///
/// <para><b>The ORDER is part of the contract.</b> The outcome is recorded BEFORE the dialog, which awaits a
/// person: recording after it would fold arbitrary think-time into the operation, and a process that dies while
/// the modal is up would record nothing at all. That reasoning is <c>OperationScope</c>'s documented cost, and
/// this is the one place that has to honour it.</para>
///
/// <para><b>Two shapes, because there are two ways to fail.</b> <see cref="FailedAsync"/> is for an exception;
/// <see cref="RefusedAsync"/> is for a condition the workflow detected itself and has no exception for — a
/// folder that is not there, a handover the OS declined. The second used to have no home at all, which is why
/// those sites showed a dialog and left their span reading OK.</para>
/// </summary>
internal static class FailureReport
{
    /// <summary>
    /// Reports a failure carried by an exception.
    /// </summary>
    /// <param name="scope">The operation that failed; told first.</param>
    /// <param name="logger">Where the English diagnostic goes.</param>
    /// <param name="dialogs">The port the Danish problem is shown through.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="problem">The bound problem framing the failure for the installer.</param>
    /// <param name="failure">The exception. Also what the span's error type is derived from.</param>
    /// <param name="message">The log message template.</param>
    /// <param name="args">The log message's arguments.</param>
    public static Task FailedAsync(
        OperationScope scope, ILogger logger, IDialogService dialogs,
        string title, Problem problem, Exception failure,
        string message, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(failure);

        scope.SetOutcome(OperationOutcome.Failed(failure));
        Log(logger, failure, message, args);
        return RaisedProblemDisplay.ShowAsync(dialogs, title, problem, failure);
    }

    /// <summary>
    /// Reports a failure the workflow detected for itself, identified by its problem CODE rather than by an
    /// exception.
    /// </summary>
    /// <remarks>
    /// <c>FailedWith</c> rather than <c>Refused</c>: a refusal in this app's vocabulary is a rule declining an
    /// edit, and these are not that — the folder really is missing, the viewer really did not open. A support
    /// query counting operations that did not do what they were asked wants them counted.
    /// </remarks>
    /// <param name="scope">The operation that failed; told first.</param>
    /// <param name="logger">Where the English diagnostic goes.</param>
    /// <param name="dialogs">The port the Danish problem is shown through.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="problem">The bound problem, whose code becomes the span's error type.</param>
    /// <param name="message">The log message template.</param>
    /// <param name="args">The log message's arguments.</param>
    public static Task RefusedAsync(
        OperationScope scope, ILogger logger, IDialogService dialogs,
        string title, Problem problem,
        string message, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(problem);

        scope.SetOutcome(OperationOutcome.FailedWith(problem.Code.Value));
        Log(logger, null, message, args);
        return dialogs.ShowProblemAsync(title, problem);
    }

    /// <summary>
    /// The one logging call, and the one place CA2254 is answered.
    /// </summary>
    /// <remarks>
    /// The analyser wants a message template that is constant AT THE CALL. It cannot be, here: this method
    /// exists precisely so that every such site, whatever its own template, shares one ordering guarantee, and a
    /// helper that forced a single template would have to drop the file name, the report kind or the block id
    /// that make each of those log records worth reading. Every template reaching this method IS a literal at
    /// its own call site, so the structured record a provider receives is exactly what it would have been if the
    /// call had been written out — the analyser simply cannot see through one indirection to say so.
    /// <para>The alternative considered and rejected: take an <c>Action&lt;ILogger&gt;</c> so each caller writes
    /// its own log line. That satisfies the analyser and hands back the very thing this type removes — a
    /// three-part sequence a caller can get half right.</para>
    /// </remarks>
#pragma warning disable CA2254 // Template should be a static expression — see the remarks above.
    private static void Log(ILogger logger, Exception? failure, string message, object?[] args)
    {
        if (failure is null)
        {
            logger.LogError(message, args);
            return;
        }
        logger.LogError(failure, message, args);
    }
#pragma warning restore CA2254
}
