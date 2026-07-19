using System;
using System.IO;
using System.Threading.Tasks;
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
