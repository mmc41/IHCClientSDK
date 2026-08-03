using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The shell's markup must resolve every binding at BUILD time (Avalonia architecture review A-06/QC-05,
/// performance BP-07): compiled bindings are checked by the XAML compiler and cost no runtime reflection, so a
/// renamed view-model member breaks the build instead of silently emptying a control. A stray
/// <c>{ReflectionBinding}</c> opts one control back out of that guarantee — and here it opted out the one place a
/// binding failure is invisible, since an unbound <c>Command</c> just makes a menu item do nothing.
/// <para>Both directions are needed: the markup assertion alone would pass with a WRONG <c>x:DataType</c> (which
/// produces null bindings), and the runtime assertion alone would pass with reflection bindings restored.</para>
/// </summary>
public class RecentProjectsBindingTests : AvaloniaTestBase
{
    [Test]
    public void MainWindowMarkup_UsesNoReflectionBinding()
    {
        string xaml = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));

        Assert.That(xaml, Does.Not.Contain("{ReflectionBinding"),
            "every binding in the shell is compiled — annotate the enclosing Style/DataTemplate with x:DataType "
            + "rather than dropping to reflection");
    }

    /// <summary>The runtime half: each generated Recent-projects item really does receive the entry's open command
    /// and its path. A wrong <c>x:DataType</c> compiles and then binds nothing, so the markup check above cannot
    /// stand alone.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task RecentProjectsMenu_BindsCommandAndPath()
    {
        using var harness = ShellHarness.Create();
        string first = harness.TempPath("alpha.vis");
        string second = harness.TempPath("beta.vis");
        harness.Recent.Add(first);
        harness.Recent.Add(second);

        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(vm.RecentProjects.Select(r => r.Path), Is.EquivalentTo(new[] { first, second }),
            "precondition: the view-model exposes both entries (most recent first)");

        // Realize the generated containers: an item materializes only once its whole ancestor menu chain is open,
        // so the File menu opens before its Recent submenu. The declared items are logical children of the Menu,
        // so they are reachable as objects before anything is realized visually.
        var declared = window.GetLogicalDescendants().OfType<MenuItem>().ToList();
        declared.First(item => AutomationProperties.GetAutomationId(item) == "MenuFile").Open();
        Dispatcher.UIThread.RunJobs();
        MenuItem recentMenu = declared.First(item => item.Name == "RecentProjectsMenu");
        recentMenu.Open();
        Dispatcher.UIThread.RunJobs();

        var items = recentMenu.Items.OfType<RecentProjectViewModel>()
            .Select(entry => recentMenu.ContainerFromItem(entry))
            .OfType<MenuItem>()
            .ToList();
        Assert.That(items, Is.Not.Empty, "the recent entries materialize as menu items");
        Assert.Multiple(() =>
        {
            Assert.That(items.Select(i => i.Command), Has.All.Not.Null,
                "every recent item is wired to the open command");
            Assert.That(items.Select(i => i.CommandParameter), Is.EquivalentTo(new object[] { first, second }),
                "and carries its own path as the command parameter");
        });
    }
}
