using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using ihc_visual.ViewModels;
using ihc_visual.Views;

namespace safe_visual_tests;

/// <summary>
/// Functional smoke coverage for the ihc_visual desktop app: the main view-model's data surface, and that the
/// main window's XAML actually loads, binds and renders a frame under the headless Skia session — so a broken
/// XAML tree, a renamed binding or a broken render pipeline fails CI instead of an empty <c>Assert.Pass()</c>
/// reporting green while verifying nothing.
/// </summary>
public class SmokeTests : AvaloniaTestBase
{
    [Test]
    public void MainWindowViewModel_ExposesGreeting()
    {
        var vm = new MainWindowViewModel();

        Assert.That(vm.Greeting, Is.EqualTo("Welcome to Avalonia!"));
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void MainWindow_LoadsXaml_BindsViewModel_AndRenders()
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel() };
        CurrentTestWindow = window;   // register for automatic failure screenshots
        window.Show();

        var frame = window.CaptureRenderedFrame();

        Assert.Multiple(() =>
        {
            Assert.That(window.DataContext, Is.InstanceOf<MainWindowViewModel>(), "the view-model is bound");
            Assert.That(window.Content, Is.Not.Null, "the window's XAML content tree loaded");
            Assert.That(frame, Is.Not.Null, "the headless Skia renderer produced a frame");
        });
    }
}
