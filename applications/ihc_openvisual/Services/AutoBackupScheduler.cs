using System;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Projects;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Services;

/// <summary>
/// T019 (M6): the auto-backup WRITER extracted from <see cref="ProjectWorkflow"/> — the crash-recovery timer
/// (10-minute default) plus the write itself: serialize the current project snapshot to the recovery location and
/// stamp its marker. Owns the timer and the write lock (so the timer path and the change-counter path never write
/// concurrently) and their disposal. Document state stays in <see cref="ProjectWorkflow"/>: the passed delegate
/// captures the snapshot + origin on THIS writer's worker thread — a legal off-thread READ of the lock-serialized
/// document (crudarch D04; this read-only path is also the one sanctioned ConfigureAwait(false) site) — and the
/// change-threshold trigger stays in the workflow (it owns the change counter); this collaborator only writes and
/// schedules.
/// </summary>
internal sealed class AutoBackupScheduler(
    BackupService backup, ProjectAppService service, TimeProvider timeProvider, ILogger logger,
    TimeSpan interval, Func<(Project? Snapshot, string? Origin)> captureSnapshot,
    TimeSpan? disposeTimeout = null) : IDisposable
{
    /// <summary>How long <see cref="Dispose"/> waits for an in-flight backup before giving up. Bounded because
    /// Dispose runs on the UI thread (the app's ShutdownRequested): an unbounded wait would freeze the quit for as
    /// long as a save takes, and a save to a disconnected network path never finishes at all.</summary>
    private static readonly TimeSpan DefaultDisposeTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _disposeTimeout = disposeTimeout ?? DefaultDisposeTimeout;
    private readonly SemaphoreSlim _lock = new(1, 1);
    // The context this scheduler was built on — the UI thread in the running app. The write itself deliberately runs
    // off it (see WriteAsync), so the failure notification is posted back rather than raised from the pool thread:
    // a handler that ends up setting a bound property would otherwise mutate the UI off-thread. Null in a plain unit
    // test, where the event is raised inline. SynchronizationContext is BCL, so this stays Avalonia-free.
    private readonly SynchronizationContext? _origin = SynchronizationContext.Current;
    private ITimer? _timer;
    private bool _disposed;

    /// <summary>
    /// Raised when a scheduled backup did NOT write a recovery copy, carrying the reason. Crash recovery is a
    /// promise the application makes silently, so a failure has to be un-silent: logging alone left both the user
    /// and any automation client believing unsaved work was protected when nothing had been written (UX review
    /// CORE-02). The message is user-facing text.
    /// </summary>
    public event EventHandler<string>? BackupFailed;

    /// <summary>Whether the most recent backup attempt failed — the health state a caller can read at any time,
    /// rather than only at the instant the event fired. Cleared by the next successful write.</summary>
    public bool LastAttemptFailed { get; private set; }

    /// <summary>Starts the periodic auto-backup timer (idempotent).</summary>
    public void Start() =>
        _timer ??= timeProvider.CreateTimer(_ => _ = WriteAsync(), null, interval, interval);

    /// <summary>Writes the current project snapshot to the recovery location. Invoked by the timer and by the
    /// change counter; serialized so two backups never write the recovery file concurrently.</summary>
    public async Task WriteAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            (Project? snapshot, string? origin) = captureSnapshot();
            if (snapshot is null)
                return;
            backup.EnsureDirectory();
            // ConfigureAwait(false) on the WRITE too, not just on the lock: the change-counter path calls WriteAsync
            // from the UI thread, where an uncontended semaphore completes synchronously and leaves the Avalonia
            // SynchronizationContext current — so this await would post its continuation back to the UI thread. That
            // continuation is what releases the lock, and Dispose() (App's ShutdownRequested, also the UI thread)
            // blocks on the same lock — quitting during a backup write would deadlock. Legal here for the documented
            // reason: this whole path only READS the lock-serialized document (crudarch D04).
            await service.Save(snapshot, backup.RecoveryProjectPath).ConfigureAwait(false);
            backup.WriteMarker(origin, timeProvider.GetUtcNow());
            LastAttemptFailed = false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-backup failed");
            LastAttemptFailed = true;
            Report($"Automatisk sikkerhedskopi mislykkedes: {ex.Message}. Gem projektet for at sikre dit arbejde.");
        }
        finally
        {
            _lock.Release();
        }
    }

    // Raises BackupFailed on the thread this scheduler was created on (the UI thread in the app), so a handler may
    // touch bound state. Posted ONLY when the failure surfaced somewhere else — a write that failed before leaving
    // the origin context is reported inline, since posting it would queue a notification behind whatever pumps that
    // context next (and in a non-pumping host it would never be delivered at all).
    private void Report(string message)
    {
        if (BackupFailed is not { } handler)
            return;
        if (_origin is null || ReferenceEquals(SynchronizationContext.Current, _origin))
            handler(this, message);
        else
            _origin.Post(_ => handler(this, message), null);
    }

    public void Dispose()
    {
        if (_disposed)
            return;   // idempotent: the guarded drain below would throw on a second (disposed) call
        _disposed = true;
        _timer?.Dispose();   // stop new timer-driven backups from firing
        // Wait for any in-flight auto-backup to finish and release the lock before disposing it — disposing a
        // SemaphoreSlim held by a running backup would fault that backup's Release.
        //
        // BOUNDED, because this runs on the UI thread (App's ShutdownRequested): an unbounded wait hangs the quit
        // for as long as the save takes, and a save to a slow or disconnected path would hang it forever, with the
        // window already closing and nothing left to report the stall (threading review WS-01). On timeout the
        // backup is still running and still owns the lock, so the semaphore is deliberately NOT disposed — freeing
        // it under its holder would fault that backup's Release on the way out. Leaking one SemaphoreSlim (which
        // holds no unmanaged handle unless its AvailableWaitHandle was read, and it never is here) at process exit
        // is the cheaper of the two outcomes.
        if (_lock.Wait(_disposeTimeout))
        {
            _lock.Dispose();
        }
        else
        {
            logger.LogWarning(
                "Auto-backup still running after {Timeout}; abandoning the drain so shutdown can proceed",
                _disposeTimeout);
        }
    }
}
