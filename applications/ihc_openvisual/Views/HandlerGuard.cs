using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Views;

/// <summary>
/// Containment for the view layer's <c>async void</c> event handlers. An <c>async void</c> method has nothing
/// awaiting it, so a fault after its first <c>await</c> is raised on the synchronization context with no caller to
/// catch it; and a window-lifecycle handler (<c>Closing</c>, <c>Closed</c>, <c>Activated</c>) is worse still —
/// it runs straight off the window message loop, where neither <c>Dispatcher.UnhandledException</c> nor
/// <c>AppDomain.UnhandledException</c> can see it at all (Avalonia logging review AP-06/WS-11). Either way the app
/// dies with no record, which is precisely what the telemetry pipeline exists to prevent.
/// <para>Every such handler runs its body through here instead. The fault is recorded and RETURNED rather than
/// rethrown, so a caller that has a sensible reaction (cancel the quit, drop a drag highlight) can take it.</para>
/// <para><b>It also leaves a DURABLE row.</b> A log line is the right home for the diagnostic, and it is not a
/// record anyone reads while the app is still running — so the fault that no global net can see would otherwise
/// be invisible to the person it just happened to. The row goes through the same static-context port
/// <see cref="Services.TaskSupervisor"/> owns, so there is one port and one composition-root line for every
/// layer that has no constructor to inject one through.</para>
/// <para>View-layer work that has a view-model to route through should use the view-model's own
/// <c>RunAsync</c> error boundary instead — that one also reports to the user via the status bar and a dialog.
/// This guard is the floor for handlers that have no view-model in reach.</para>
/// </summary>
internal static class HandlerGuard
{
    /// <summary>Runs <paramref name="work"/>, containing any fault. Returns the exception it caught, or null when
    /// the handler completed.</summary>
    internal static async Task<Exception?> RunAsync(Func<Task> work, ILogger? logger, string handler)
    {
        try
        {
            await work();
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Handler {Handler} failed: {Message}", handler, ex.Message);
            // BOTH channels, and the return value is unchanged: a caller that reacts to the exception still
            // gets it. This floor is the ONLY one that can see a window-lifecycle fault at all, so dropping the
            // user-visible half here would mean dropping it everywhere.
            Services.TaskSupervisor.Report(ex, handler);
            return ex;
        }
    }
}
