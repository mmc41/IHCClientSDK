using System;
using System.IO;
using System.Threading.Tasks;

namespace safe_visual_tests;

/// <summary>
/// Review Low ("startup async void"): App wires <c>window.Opened</c> to an async-void handler that awaits
/// <c>InitializeAsync</c> → <see cref="ihc_openvisual.Services.ProjectWorkflow.StartAsync"/>, so an exception thrown
/// while loading a corrupt crash-recovery <c>.vis</c> becomes an unobserved exception that crashes the app at launch.
/// StartAsync must instead route a failed recovery through its own error path (like <c>OpenAsync</c>): report it and
/// fall back to a fresh project.
/// </summary>
public class StartupRecoveryTests
{
    [Test]
    public async Task StartAsync_CorruptRecoveryFile_ReportsErrorAndOpensFreshProject()
    {
        using var harness = ShellHarness.Create();
        harness.Backup.EnsureDirectory();
        File.WriteAllText(harness.Backup.RecoveryProjectPath, "this is not a valid .vis file <<<broken>>>");
        harness.Backup.WriteMarker(originPath: null, savedAtUtc: DateTimeOffset.UtcNow);
        harness.Dialogs.ConfirmResult = true;   // the user chooses to recover

        await harness.Session.StartAsync();      // must NOT throw despite the corrupt recovery file

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current, Is.Not.Null, "the app stays alive on a fresh project after a failed recovery");
            Assert.That(harness.Dialogs.LastMessage, Is.Not.Null, "the recovery failure is reported to the user");
            Assert.That(harness.Backup.HasRecovery(), Is.False, "the unusable recovery backup is discarded");
        });
    }
}
