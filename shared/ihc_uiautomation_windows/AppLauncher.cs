using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Ihc.UiAutomation;

/// <summary>
/// A started application and the window it was waited for, if it appeared.
/// </summary>
/// <param name="Process">
/// The started process. The caller owns it — it is live whether or not a window was found, which is what makes
/// a failed launch diagnosable (exit code, standard error) instead of merely absent.
/// </param>
/// <param name="Wait">
/// What the wait for that window saw: how long it looked, how often, and — when no window appeared — the
/// reason. This is what makes a failed launch a sentence rather than a null.
/// </param>
public sealed record LaunchedApp(Process Process, UiaWaitResult<UiaElement> Wait)
{
    /// <summary>
    /// The first window satisfying the caller's predicate, or <see langword="null"/> if none appeared before the
    /// timeout — including because the process exited first.
    /// </summary>
    public UiaElement? MainWindow => Wait.Value;
}

/// <summary>
/// Starting an application and waiting for it to be DRIVEABLE, and stopping every instance of one.
/// </summary>
public static class AppLauncher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Starts an executable and waits for one of its visible top-level windows to satisfy
    /// <paramref name="isReady"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a predicate rather than "the first window".</b> An application's first window is often a
    /// splash, a restore prompt or an empty shell whose content has not been built. Driving that window is how a
    /// scenario fails intermittently on a slower machine. What "ready" means belongs to whoever knows the
    /// application — typically "the element a loaded shell always publishes is present".</para>
    ///
    /// <para>Polling rather than an event: a UI-Automation window-opened subscription has to be registered
    /// before the window exists and delivers on a provider thread, which buys nothing here — the wait is
    /// bounded and a quarter-second granularity is invisible next to an application's start-up.</para>
    /// </remarks>
    /// <param name="session">The automation session the returned window belongs to.</param>
    /// <param name="executablePath">The executable to start.</param>
    /// <param name="arguments">Command-line arguments, passed one per element so nothing needs quoting.</param>
    /// <param name="isReady">What makes a window the one to drive.</param>
    /// <param name="timeout">How long to wait for such a window.</param>
    public static LaunchedApp Start(
        UiaSession session,
        string executablePath,
        IEnumerable<string> arguments,
        Func<UiaElement, bool> isReady,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(isReady);

        ProcessStartInfo start = new(executablePath) { UseShellExecute = false };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);

        Process process = Process.Start(start)
            ?? throw new InvalidOperationException($"could not start {executablePath}");

        return new LaunchedApp(process, WaitForWindow(session, process, isReady, timeout));
    }

    /// <summary>
    /// Waits for a visible top-level window of <paramref name="process"/> that satisfies
    /// <paramref name="isReady"/>.
    /// </summary>
    /// <returns>
    /// The wait, whose <see cref="UiaWaitResult{T}.Value"/> is the window and whose
    /// <see cref="UiaWaitResult{T}.LastSeen"/> is why there is none.
    /// </returns>
    public static UiaWaitResult<UiaElement> WaitForWindow(
        UiaSession session,
        Process process,
        Func<UiaElement, bool> isReady,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(isReady);

        return UiaWait.Until(
            probe: () => DesktopWindows.OfProcess(session, process.Id).FirstOrDefault(isReady),
            satisfied: _ => true,
            timeout: timeout,
            poll: PollInterval,
            // An exited process will publish no window however long it is given, so serving out the timeout
            // only delays the report and buries the reason. Said here, the reason survives into the result
            // instead of being something the caller has to go and work out from the Process it still holds.
            giveUp: () => process.HasExited
                ? $"process {process.Id} exited with code {process.ExitCode}"
                : null);
    }

    /// <summary>
    /// Stops every running instance of <paramref name="processName"/> (given WITHOUT its <c>.exe</c> suffix),
    /// with its child processes, and waits for each to be gone.
    /// </summary>
    /// <remarks>
    /// Waiting is the whole point, not politeness. A surviving instance keeps its binaries open, so the next
    /// build of the application fails with a file-in-use error that names a file rather than the cause; and a
    /// second instance left running is a second window a driver may attach to.
    /// </remarks>
    /// <returns>How many processes were asked to stop.</returns>
    public static int KillAll(string processName, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(processName);

        Process[] running = Process.GetProcessesByName(processName);
        List<Process> stopping = new(running.Length);
        try
        {
            foreach (Process process in running)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    stopping.Add(process);
                }
                catch (InvalidOperationException)
                {
                    // It exited between being listed and being killed, which is the outcome asked for.
                }
                catch (Win32Exception)
                {
                    // Access denied: another user's instance, or one at a higher elevation. Reporting it here
                    // would be a lie about ownership — the caller finds out by looking for a window and not
                    // getting the one it expects.
                }
            }

            // All asked to stop FIRST, then waited for against ONE deadline. They are dying in parallel, so
            // giving each its own full timeout would multiply the budget by however many were running — three
            // stale instances turned a twenty-second bound into a minute of fixture set-up.
            long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
            foreach (Process process in stopping)
                process.WaitForExit((int)Math.Max(0, deadline - Environment.TickCount64));
        }
        finally
        {
            foreach (Process process in running)
                process.Dispose();
        }

        return stopping.Count;
    }
}
