using System.IO;
using System.Threading.Tasks;
using ihc_openvisual;
using ihc_openvisual.Services;

namespace safe_visual_tests;

/// <summary>
/// The launch file (os-integration BP-11a): a <c>.vis</c> handed to the executable — by "Open with…", by a
/// double-click once the association is registered, or by typing a path — is the document the app opens.
/// <para>argv is the only route for this on Windows and Linux (Avalonia's <c>ActivationKind.File</c> is
/// macOS/iOS/Android-only), so the whole feature is the two halves tested here: reading the path out of the
/// arguments, and start-up opening it instead of the empty starter project.</para>
/// </summary>
public class StartupProjectPathTests
{
    private static string SampleProject() =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis");

    [Test]
    public void ParseStartupProjectPath_TakesTheFirstNonSwitchArgument()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Program.ParseStartupProjectPath([]), Is.Null, "launched with no arguments");
            Assert.That(Program.ParseStartupProjectPath(["--skip-recovery"]), Is.Null, "switches are not files");
            Assert.That(Program.ParseStartupProjectPath([@"C:\projects\Hus.vis"]), Is.EqualTo(@"C:\projects\Hus.vis"));
            Assert.That(Program.ParseStartupProjectPath(["--skip-recovery", "/home/mmc/Hus.vis"]),
                Is.EqualTo("/home/mmc/Hus.vis"),
                "a leading slash is an absolute POSIX path, not a switch");
        });
    }

    [Test]
    public async Task Start_WithAProjectOnTheCommandLine_OpensThatProject()
    {
        using var harness = ShellHarness.Create();
        string path = SampleProject();

        await harness.Session.StartAsync(skipRecovery: true, startupProjectPath: path);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.FilePath, Is.EqualTo(path), "the launch file became the open document");
            Assert.That(harness.Session.DocumentName, Is.EqualTo(Path.GetFileName(path)));
            Assert.That(harness.Session.IsDirty, Is.False, "opening is not an edit");
            Assert.That(harness.Recent.Items, Does.Contain(Path.GetFullPath(path)),
                "opened like any other project, so it joins the recent list");
        });
    }

    [Test]
    public async Task Start_WithNoProjectOnTheCommandLine_OpensTheEmptyProject()
    {
        using var harness = ShellHarness.Create();

        await harness.Session.StartAsync(skipRecovery: true);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current, Is.Not.Null, "the standard empty project");
            Assert.That(harness.Session.FilePath, Is.Null);
        });
    }

    /// <summary>An unreadable or non-project path must not block the launch: a bad file association, a renamed
    /// file or a stale shortcut is reported and the app still comes up on the empty project.</summary>
    [Test]
    public async Task Start_WithAnUnopenableProject_ReportsItAndStillOpensTheEmptyProject()
    {
        using var harness = ShellHarness.Create();
        string missing = harness.TempPath("does-not-exist.vis");

        await harness.Session.StartAsync(skipRecovery: true, startupProjectPath: missing);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("does-not-exist.vis"), "the failure names the file");
            Assert.That(harness.Session.Current, Is.Not.Null, "the app still opened on the empty project");
            Assert.That(harness.Session.FilePath, Is.Null);
        });
    }

    /// <summary>Crash recovery outranks the launch file: unsaved work from the previous session is the scarcer
    /// thing, and the named file is one Open away, whereas a discarded backup is gone.</summary>
    [Test]
    public async Task Start_WithBothARecoveryBackupAndALaunchFile_RecoversFirst()
    {
        // Simulate a crash while editing a named project: save it, change it, back it up, abandon the session.
        using var harness = ShellHarness.Create();
        harness.Dialogs.SavePath = harness.TempPath("crashed.vis");
        await harness.Session.StartAsync(skipRecovery: true);
        await harness.Session.SaveAsAsync();
        await harness.Session.AddLocalityAsync();
        await harness.Session.AutoBackupAsync();
        harness.Session.Dispose();

        using var restarted = ShellHarness.Restart(harness.TempDir);
        restarted.Dialogs.ConfirmResult = true;   // the installer accepts the recovery offer

        await restarted.Session.StartAsync(startupProjectPath: SampleProject());

        Assert.Multiple(() =>
        {
            Assert.That(restarted.Dialogs.ConfirmCalls, Is.EqualTo(1), "the recovery prompt still ran");
            Assert.That(restarted.Session.FilePath, Is.EqualTo(harness.TempPath("crashed.vis")),
                "the recovered document won, not the launch file");
            Assert.That(restarted.Session.IsDirty, Is.True, "recovered work is unsaved work");
        });
    }

    /// <summary>Declining recovery does not cost the launch file: the empty project is the fallback for having no
    /// file to open, not the answer to "no thanks".</summary>
    [Test]
    public async Task Start_WhenRecoveryIsDeclined_OpensTheLaunchFile()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync(skipRecovery: true);
        await harness.Session.AutoBackupAsync();
        harness.Session.Dispose();

        using var restarted = ShellHarness.Restart(harness.TempDir);
        restarted.Dialogs.ConfirmResult = false;

        await restarted.Session.StartAsync(startupProjectPath: SampleProject());

        Assert.That(restarted.Session.FilePath, Is.EqualTo(SampleProject()));
    }
}
