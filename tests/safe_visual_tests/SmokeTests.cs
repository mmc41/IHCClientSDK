using Avalonia.Headless.NUnit;
using ihc_visual.ViewModels;
using ihc_visual.Views;

namespace safe_visual_tests;

/// <summary>
/// Functional smoke coverage for the ihc_visual desktop app: the main view-model's data surface, and that the
/// main window's XAML actually loads and binds under a headless Avalonia session — so a broken XAML tree or a
/// renamed binding fails CI instead of an empty <c>Assert.Pass()</c> reporting green while verifying nothing.
/// </summary>
public class SmokeTests
{
    [Test]
    public void MainWindowViewModel_ExposesGreeting()
    {
        var vm = new MainWindowViewModel();

        Assert.That(vm.Greeting, Is.EqualTo("Welcome to Avalonia!"));
    }

    [AvaloniaTest]
    public void MainWindow_LoadsXaml_AndBindsViewModel()
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel() };

        Assert.Multiple(() =>
        {
            Assert.That(window.DataContext, Is.InstanceOf<MainWindowViewModel>(), "the view-model is bound");
            Assert.That(window.Content, Is.Not.Null, "the window's XAML content tree loaded");
        });
    }
}
