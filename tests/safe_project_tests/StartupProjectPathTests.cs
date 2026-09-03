using System.IO;
using System.Threading.Tasks;
using ihc_openvisual;
using ihc_openvisual.Services;

namespace Ihc.Vis.Tests;

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
            Assert.That(Program.ParseStartupProjectPath(["--verbose"]), Is.Null, "switches are not files");
            Assert.That(Program.ParseStartupProjectPath([@"C:\projects\Hus.vis"]), Is.EqualTo(@"C:\projects\Hus.vis"));
            Assert.That(Program.ParseStartupProjectPath(["--verbose", "/home/mmc/Hus.vis"]),
                Is.EqualTo("/home/mmc/Hus.vis"),
                "a leading slash is an absolute POSIX path, not a switch");
        });
    }

    [Test]
    public async Task Start_WithAProjectOnTheCommandLine_OpensThatProject()
    {
        using var harness = ShellHarness.Create();
        string path = SampleProject();

        await harness.Session.StartAsync(startupProjectPath: path);

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

        await harness.Session.StartAsync();

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

        await harness.Session.StartAsync(startupProjectPath: missing);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("does-not-exist.vis"), "the failure names the file");
            Assert.That(harness.Session.Current, Is.Not.Null, "the app still opened on the empty project");
            Assert.That(harness.Session.FilePath, Is.Null);
        });
    }
}
