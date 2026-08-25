using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The Problemer panel's SKELETON: a full-width bottom region between the trees and the status bar, resizable,
/// visible by default, and toggled by a checkable Vis row.
///
/// <para>LAYOUT ORDER is the load-bearing part, and the reason this fixture realizes the window instead of
/// asserting on the view-model alone. MainWindow's root is a DockPanel whose status bar is already docked
/// Bottom, so a panel bottom-docked after it lands BELOW the status bar — at the window edge, on the wrong side
/// of it — and no view-model test can see that. The panel therefore lives in the DockPanel's FILL child, a row
/// Grid holding trees / splitter / panel, which is also what makes the splitter work at all: a GridSplitter
/// resizes nothing unless its parent is a Grid. Both properties are asserted geometrically below.</para>
///
/// <para>Visibility and height are SESSION-ONLY by decision: nothing here is persisted, so there is no settings
/// round-trip to assert and a fresh shell always starts with the panel shown.</para>
/// </summary>
public class ProblemsPanelSkeletonTests
{
    private const string PanelId = "ProblemsPanel";
    private const string SplitterId = "ProblemsSplitter";
    private const string ToggleRowId = "view.toggleProblems";

    private static Control? ById(Window window, string id) =>
        window.GetLogicalDescendants().OfType<Control>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id);

    [Test]
    public void ThePanelIsVisibleByDefaultBecauseAFindingUserSeesNothingOtherwise()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();

        Assert.That(vm.IsProblemsPanelVisible, Is.True,
            "a panel that starts hidden makes every validation finding invisible until a user finds the menu row");
    }

    [Test]
    public async Task TheVisRowTogglesThePanelAndIsAlwaysAvailable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.That(vm.Registry.Rows.Select(r => r.Id), Does.Contain(ToggleRowId),
            "the toggle is a registry row like its two Vis siblings, not a bare command");

        await vm.ToggleProblemsCommand.ExecuteAsync(null);
        Assert.That(vm.IsProblemsPanelVisible, Is.False, "the row hides the panel");

        await vm.ToggleProblemsCommand.ExecuteAsync(null);
        Assert.That(vm.IsProblemsPanelVisible, Is.True, "and shows it again — session-only, no persistence");
    }

    [AvaloniaTest]
    public async Task TheShellHostsThePanelAndItsSplitterWithAutomationIds()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(ById(window, PanelId), Is.Not.Null, $"the panel root carries AutomationId '{PanelId}'");
            Assert.That(ById(window, SplitterId), Is.Not.Null,
                $"the horizontal splitter carries AutomationId '{SplitterId}' — it is the only way a driver, or a "
                + "keyboard user, resizes the panel");
        });

        window.Close();
    }

    /// <summary>
    /// The layout-order assertion, made geometrically and in WINDOW coordinates. Bounds alone would not do it:
    /// the three regions sit at different nesting depths, and each control's Bounds are relative to its own
    /// parent — the panel would report Top = 0 inside its own row and "prove" it is at the top of the window.
    /// Translating each origin into the window is what makes trees / panel / status bar comparable at all.
    /// </summary>
    [AvaloniaTest]
    public async Task ThePanelSitsBetweenTheTreesAndTheStatusBarNotBelowIt()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control panel = ById(window, PanelId)!;
        Control statusBar = window.GetLogicalDescendants().OfType<Border>().Single(b => b.Name == "StatusBar");
        Control trees = ById(window, "InstallationTree")!;

        double panelTop = panel.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        double statusBarTop = statusBar.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        double treesTop = trees.TranslatePoint(new Point(0, 0), window)!.Value.Y;

        Assert.Multiple(() =>
        {
            Assert.That(panelTop + panel.Bounds.Height, Is.LessThanOrEqualTo(statusBarTop),
                "the panel ends where the status bar begins — laid out below it, the status bar would be the "
                + "band a user reads findings above, which is the wrong side of the window edge");
            Assert.That(panelTop, Is.GreaterThan(treesTop),
                "and it sits UNDER the trees, not over them");
            Assert.That(panel.Bounds.Width, Is.EqualTo(window.ClientSize.Width).Within(1.0),
                "full width — it spans both tree panes, it is not a third column");
        });

        window.Close();
    }

    [AvaloniaTest]
    public async Task TogglingTheRowHidesAndReshowsTheRealizedPanel()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control panel = ById(window, PanelId)!;
        Assert.That(panel.IsVisible, Is.True, "default-visible in the realized shell, not only in the view-model");

        await vm.ToggleProblemsCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.That(panel.IsVisible, Is.False, "the Vis row's binding reaches the realized panel");

        await vm.ToggleProblemsCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.That(panel.IsVisible, Is.True);

        window.Close();
    }
}
