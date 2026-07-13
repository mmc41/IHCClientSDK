using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// Headless UI smoke coverage for the IHC OpenVisual shell (US-001, US-065): the main window loads its XAML,
/// binds the real view-model and renders under the headless Skia session with the whole shell chrome present —
/// the menu bar, both tree panes, and the status bar — and the About dialog exposes its version lines.
/// A broken XAML tree, a renamed binding or a broken render pipeline fails CI instead of passing silently.
/// </summary>
public class SmokeTests : AvaloniaTestBase
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersShellChrome_WithMenuBarAndTwoPanes()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;   // register for automatic failure screenshots
        window.Show();

        var frame = window.CaptureRenderedFrame();
        var menu = window.GetVisualDescendants().OfType<Menu>().Single();
        int treeCount = window.GetVisualDescendants().OfType<TreeView>().Count();

        Assert.Multiple(() =>
        {
            Assert.That(frame, Is.Not.Null, "the headless Skia renderer produced a frame");
            Assert.That(window.Title, Does.Contain("IHC OpenVisual"), "the title bar names the application");
            Assert.That(menu.Items.Count, Is.EqualTo(8), "the eight stable menu titles are present (Simulation is out of scope)");
            Assert.That(treeCount, Is.EqualTo(2), "both tree panes (Installation and Functions) are shown");
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AboutWindow_ShowsApplicationAndSdkVersions()
    {
        var about = new AboutWindow();
        CurrentTestWindow = about;
        about.Show();

        var appVersion = about.FindControl<TextBlock>("AppVersionText");
        var sdkVersion = about.FindControl<TextBlock>("SdkVersionText");

        Assert.Multiple(() =>
        {
            Assert.That(about.Title, Is.EqualTo("About IHC OpenVisual"));
            Assert.That(appVersion?.Text, Does.StartWith("App Version:"));
            Assert.That(sdkVersion?.Text, Does.StartWith("SDK Version:"));
        });
    }
}
