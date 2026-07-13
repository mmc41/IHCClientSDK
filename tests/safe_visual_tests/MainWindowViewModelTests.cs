using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>Shell view-model behaviour (US-001/051): the title, the two locality tree panes, and the
/// toolbar/status-bar/theme view state. Pure logic — no Avalonia UI needed.</summary>
public class MainWindowViewModelTests
{
    [Test]
    public async Task Initialize_BuildsLocalitiesRootWithTenRooms_InBothPanes()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();

        await vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes, Has.Count.EqualTo(1));
            Assert.That(vm.InstallationNodes[0].DisplayName, Is.EqualTo("Localities"));
            Assert.That(vm.InstallationNodes[0].Children, Has.Count.EqualTo(10));
            Assert.That(vm.FunctionNodes[0].Children, Has.Count.EqualTo(10));
        });
    }

    [Test]
    public async Task Title_ReflectsDocumentName()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.That(vm.Title, Is.EqualTo("Untitled - IHC OpenVisual"));

        harness.Dialogs.SavePath = harness.TempPath("house.vis");
        await harness.Session.SaveAsAsync();

        Assert.That(vm.Title, Is.EqualTo("house.vis - IHC OpenVisual"));
    }

    [Test]
    public async Task ToggleToolbar_FlipsVisibility()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.That(vm.IsToolbarVisible, Is.True);

        vm.ToggleToolbarCommand.Execute(null);
        Assert.That(vm.IsToolbarVisible, Is.False);

        vm.ToggleToolbarCommand.Execute(null);
        Assert.That(vm.IsToolbarVisible, Is.True);
    }

    [Test]
    public async Task ToggleStatusBar_FlipsVisibility()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        vm.ToggleStatusBarCommand.Execute(null);

        Assert.That(vm.IsStatusBarVisible, Is.False);
    }

    [Test]
    public async Task SetTheme_UpdatesCurrentTheme()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        vm.SetThemeCommand.Execute(AppTheme.Dark);

        Assert.That(vm.CurrentTheme, Is.EqualTo(AppTheme.Dark));
    }
}
