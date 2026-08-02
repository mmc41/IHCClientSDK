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
    TimeSpan interval, Func<(Project? Snapshot, string? Origin)> captureSnapshot) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ITimer? _timer;
    private bool _disposed;

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
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-backup failed");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;   // idempotent: the guarded _lock.Wait() below would throw on a second (disposed) call
        _disposed = true;
        _timer?.Dispose();   // stop new timer-driven backups from firing
        // Wait for any in-flight auto-backup to finish and release the lock before disposing it — disposing a
        // SemaphoreSlim held by a running backup would fault that backup's Release.
        _lock.Wait();
        _lock.Dispose();
    }
}
