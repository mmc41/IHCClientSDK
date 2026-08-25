using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Dragging the Problemer splitter, and dragging it BACK. The panel is the one region of the shell whose height a
/// user sets by hand, so the invariant under test is the one a hand-resize can break: the panel occupies exactly
/// the grid row the splitter sized — no band of dead background above or below it, and no overhang past the row
/// into the status bar.
///
/// <para>Reproduces the reported defect: the panel carried a fixed <c>Height</c> while its row was <c>Auto</c>, so
/// the splitter resized the ROW and the panel kept its original height inside it. Dragging up left whitespace
/// (a 180-high panel centred in a taller row); dragging down left the panel overhanging a shorter row, drawn over
/// the status bar with its lower rows unreachable. Both directions also survived a drag back, because the row had
/// stopped being <c>Auto</c> the moment the splitter first wrote a pixel height into it.</para>
///
/// <para>The gesture is driven through real pointer input on the splitter rather than by writing row heights, so
/// what the test exercises is the control a user actually grabs.</para>
/// </summary>
public class ProblemsPanelResizeTests : AvaloniaTestBase
{
    private const string PanelId = "ProblemsPanel";
    private const string SplitterId = "ProblemsSplitter";

    private static Control ById(Window window, string id) =>
        window.GetLogicalDescendants().OfType<Control>()
            .First(c => AutomationProperties.GetAutomationId(c) == id);

    /// <summary>Grabs the splitter at its centre and moves it <paramref name="dy"/> pixels (negative = upwards,
    /// growing the panel), then releases.</summary>
    private static void DragSplitter(Window window, Control splitter, double dy)
    {
        Point centre = splitter.TranslatePoint(
            new Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2), window)!.Value;

        window.MouseDown(centre, MouseButton.Left);
        window.MouseMove(centre + new Vector(0, dy), RawInputModifiers.LeftMouseButton);
        window.MouseUp(centre + new Vector(0, dy), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static double TopIn(Window window, Control control) =>
        control.TranslatePoint(new Point(0, 0), window)!.Value.Y;

    /// <summary>The panel fills the band between the splitter and the status bar — the whole band and nothing
    /// beyond it. Asserted after every drag; a violation IS the reported whitespace/overhang.</summary>
    private static void AssertPanelFillsItsRow(Window window, string after)
    {
        Control panel = ById(window, PanelId);
        Control splitter = ById(window, SplitterId);
        Border statusBar = window.GetLogicalDescendants().OfType<Border>().Single(b => b.Name == "StatusBar");

        double splitterBottom = TopIn(window, splitter) + splitter.Bounds.Height;
        double panelTop = TopIn(window, panel);
        double panelBottom = panelTop + panel.Bounds.Height;
        double statusBarTop = TopIn(window, statusBar);

        Assert.Multiple(() =>
        {
            Assert.That(panelTop, Is.EqualTo(splitterBottom).Within(1.0),
                $"{after}: the panel starts where the splitter ends — a gap here is the dead band a user sees "
                + "above the findings after dragging the splitter up");
            Assert.That(panelBottom, Is.EqualTo(statusBarTop).Within(1.0),
                $"{after}: and ends where the status bar begins — short of it leaves whitespace, past it draws "
                + "findings under the status bar where they cannot be clicked");
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DraggingTheSplitterUp_GrowsThePanelItselfNotJustItsRow()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control panel = ById(window, PanelId);
        Control splitter = ById(window, SplitterId);
        double before = panel.Bounds.Height;

        DragSplitter(window, splitter, -100);

        Assert.That(panel.Bounds.Height, Is.EqualTo(before + 100).Within(2.0),
            "the drag grows the PANEL, not an empty row around a panel that stayed its original height");
        AssertPanelFillsItsRow(window, "after dragging up");

        window.Close();
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DraggingTheSplitterBackAndForth_ReturnsThePanelToWhereItStarted()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control panel = ById(window, PanelId);
        Control splitter = ById(window, SplitterId);
        double before = panel.Bounds.Height;

        DragSplitter(window, splitter, -80);
        AssertPanelFillsItsRow(window, "after dragging up");

        DragSplitter(window, splitter, 80);
        Assert.That(panel.Bounds.Height, Is.EqualTo(before).Within(2.0),
            "back where it started — a drag and its reverse cancel out");
        AssertPanelFillsItsRow(window, "after dragging back down");

        DragSplitter(window, splitter, 60);
        AssertPanelFillsItsRow(window, "after dragging down past the starting height");

        window.Close();
    }

    /// <summary>
    /// The floor. A drag far past the bottom must leave a panel that is still there and still operable rather than
    /// one squeezed to nothing — and, above all, one that does not keep its old height and overhang the status bar
    /// (the "non-responsive findings view": rows drawn outside the row, under the status bar, taking no clicks).
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DraggingTheSplitterToTheBottom_LeavesAPanelThatIsStillThere()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control panel = ById(window, PanelId);
        Control splitter = ById(window, SplitterId);

        DragSplitter(window, splitter, 5000);

        Assert.That(panel.Bounds.Height, Is.GreaterThan(0),
            "the panel keeps a floor — collapsing it to nothing is what the Vis toggle is for");
        AssertPanelFillsItsRow(window, "after dragging to the bottom");

        window.Close();
    }

    /// <summary>
    /// The ceiling: the trees may not be squeezed away either. Dragging the splitter to the top must stop while
    /// both tree panes are still usable.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DraggingTheSplitterToTheTop_LeavesTheTreesUsable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control splitter = ById(window, SplitterId);
        Control trees = ById(window, "InstallationTree");

        DragSplitter(window, splitter, -5000);

        Assert.That(trees.Bounds.Height, Is.GreaterThan(0),
            "the trees keep a floor of their own — the panel may not eat the workspace");
        AssertPanelFillsItsRow(window, "after dragging to the top");

        window.Close();
    }

    /// <summary>
    /// Hiding the panel after a resize must leave NO trace of it. Once the splitter has written a pixel height
    /// into the row, an unconditional row height keeps that band reserved and the Vis toggle appears to do
    /// nothing but blank a stripe above the status bar.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task HidingThePanelAfterAResize_GivesTheSpaceBackToTheTrees()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control splitter = ById(window, SplitterId);
        Control trees = ById(window, "InstallationTree");

        DragSplitter(window, splitter, -100);
        double treesResized = trees.Bounds.Height;

        await vm.ToggleProblemsCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.That(trees.Bounds.Height, Is.GreaterThan(treesResized + 90),
            "hiding the panel hands its whole band back to the trees — a reserved empty row is the defect");

        await vm.ToggleProblemsCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.That(trees.Bounds.Height, Is.EqualTo(treesResized).Within(2.0),
            "and showing it again restores the height the user dragged, not the factory default");
        AssertPanelFillsItsRow(window, "after hiding and re-showing");

        window.Close();
    }
}
