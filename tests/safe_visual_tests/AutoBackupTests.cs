using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Microsoft.Extensions.Time.Testing;

namespace safe_visual_tests;

/// <summary>W2-15 / D8: the crash-backup timer runs on the injected <see cref="TimeProvider"/>, so a
/// <see cref="FakeTimeProvider"/> can drive it deterministically — advancing by the interval fires a backup with
/// no real 10-minute wait.</summary>
public class AutoBackupTests
{
    [Test]
    public async Task AutoBackup_Timer_FiresOnTimeProviderAdvance()
    {
        var time = new FakeTimeProvider();
        using var harness = ShellHarness.Create(timeProvider: time, autoBackupInterval: TimeSpan.FromMinutes(10));
        await harness.Session.StartAsync(skipRecovery: true);   // opens a fresh project and starts the timer
        Assert.That(File.Exists(harness.Backup.RecoveryProjectPath), Is.False, "no backup before the interval elapses");

        time.Advance(TimeSpan.FromMinutes(10));   // fire the timer callback deterministically — no sleep

        Assert.That(await WaitForFileAsync(harness.Backup.RecoveryProjectPath, TimeSpan.FromSeconds(5)), Is.True,
            "the 10-minute timer wrote a crash backup");
    }

    /// <summary>
    /// Quitting must not hang on a slow backup. <c>Dispose</c> runs on the UI thread (the app's
    /// <c>ShutdownRequested</c>) and drains the write lock so it never disposes a semaphore a running backup still
    /// holds. That drain used to be UNBOUNDED: a save to a disconnected network path would freeze the quit forever,
    /// with the window already gone and no way to report anything (threading review WS-01 — a blocking wait on a
    /// UI-thread-reachable path).
    /// <para>The blocking is done by the injected <c>captureSnapshot</c> delegate, which runs INSIDE the lock — so
    /// the contention is real and deterministic, with no app service mocked and no sleeping to guess at timing.</para>
    /// </summary>
    [Test]
    public void Dispose_WhileABackupIsInFlight_GivesUpOnTheDrainInsteadOfHangingTheQuit()
    {
        using var harness = ShellHarness.Create();
        var logs = new CapturingLoggerFactory();
        using var backupIsStuck = new ManualResetEventSlim(false);
        var enteredTheLock = new ManualResetEventSlim(false);
        var scheduler = new AutoBackupScheduler(
            harness.Backup, harness.ProjectService, TimeProvider.System, logs.Logger,
            TimeSpan.FromHours(1),
            captureSnapshot: () =>
            {
                enteredTheLock.Set();
                backupIsStuck.Wait();            // hold the write lock until the assertions are done
                return (null, null);
            },
            disposeTimeout: TimeSpan.FromMilliseconds(200));

        // On its own thread, NOT inline: an uncontended SemaphoreSlim.WaitAsync completes synchronously, so
        // WriteAsync runs captureSnapshot on whichever thread called it — inline it would block this test.
        Task inFlight = Task.Run(() => scheduler.WriteAsync());
        Assert.That(enteredTheLock.Wait(TimeSpan.FromSeconds(5)), Is.True,
            "precondition: the backup is inside the lock, so Dispose really has to contend for it");

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        scheduler.Dispose();
        elapsed.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromSeconds(3)),
                "Dispose gives up on the drain rather than blocking the UI thread indefinitely");
            Assert.That(logs.Messages, Has.Some.Contains("backup"),
                "and says so, instead of abandoning the in-flight backup silently");
        });

        backupIsStuck.Set();                     // release the stuck backup so the test tears down cleanly
        inFlight.Wait(TimeSpan.FromSeconds(5));
    }

    // The timer callback runs AutoBackupAsync fire-and-forget; its file write completes on a background continuation,
    // so poll (with a real-time budget) for the recovery file rather than assuming it lands synchronously.
    private static async Task<bool> WaitForFileAsync(string path, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
                return true;
            await Task.Delay(20);
        }
        return File.Exists(path);
    }
}
