using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// How the Problemer panel is SET: a small monospace face over tightly packed rows, the way a log or a compiler's
/// error list is set — not the proportional, touch-spaced form the rest of the shell uses.
///
/// <para>This is a legibility requirement, not a taste one. One corpus project produces 150 findings; at Fluent's
/// stock 38px row the panel's default height shows four of them, and the columns of a proportional font do not
/// line up under each other, so the list cannot be scanned in bulk at all. The assertions below are therefore
/// about what a reader GETS — a screenful of findings, a header band that does not out-weigh the rows it labels —
/// rather than about particular pixel values, which are free to be re-tuned.</para>
/// </summary>
public class ProblemsPanelDensityTests : AvaloniaTestBase
{
    /// <summary>A realized shell over a fixture that produces 150 findings, with the validation debounce driven
    /// off a fake clock so the rows are bound before anything is measured.</summary>
    private sealed class Rig : IDisposable
    {
        public FakeTimeProvider Clock { get; } = new();
        public ShellHarness Harness { get; }
        public MainWindowViewModel Shell { get; }
        public MainWindow Window { get; }

        private Rig()
        {
            Harness = ShellHarness.Create(Clock);
            Shell = Harness.CreateViewModel();
            Window = new MainWindow { DataContext = Shell };
        }

        public static async Task<Rig> ShowingFindingsAsync()
        {
            Rig rig = new();
            await rig.Shell.InitializeAsync(ProblemsTestData.FixturePath("Project6-Errors.vis"));
            rig.Clock.Advance(ValidationWorker.DefaultDebounce);
            await rig.Shell.Problems.Idle.WaitAsync(TimeSpan.FromSeconds(30));
            CurrentTestWindow = rig.Window;
            rig.Window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.That(rig.Shell.Problems.Rows, Is.Not.Empty,
                "sanity: the fixture must produce findings, or every measurement below is vacuous");
            return rig;
        }

        public void Dispose()
        {
            Window.Close();
            Shell.Dispose();
            Harness.Dispose();
        }
    }

    // Visual descendants, not logical: the sort headers and the row cells are template/data-template content,
    // which the logical tree does not carry.
    private static Control ById(Visual root, string id) =>
        root.GetVisualDescendants().OfType<Control>()
            .First(c => AutomationProperties.GetAutomationId(c) == id);

    private static Control ByName(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);

    private static TableViewRow[] RealizedRows(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TableViewRow>()];

    /// <summary>The first piece of finding text in the list — a cell's own TextBlock, not a header's.</summary>
    private static TextBlock FirstCellText(Visual root) =>
        RealizedRows(root)[0].GetVisualDescendants().OfType<TextBlock>().First(t => !string.IsNullOrEmpty(t.Text));

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheFindingsAreSetInTheMonospaceFaceNotTheProportionalUiFont()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();

        TextBlock cell = FirstCellText(rig.Window);
        TextBlock header = ById(rig.Window, "problems.sort.code").GetVisualDescendants().OfType<TextBlock>().First();

        Assert.Multiple(() =>
        {
            Assert.That(cell.FontFamily.Name, Is.EqualTo(ihc_openvisual.Program.MonoFontFamily),
                "a findings row is a column-aligned readout — the app's proportional UI font cannot line "
                + "codes and ids up under each other");
            Assert.That(header.FontFamily.Name, Is.EqualTo(ihc_openvisual.Program.MonoFontFamily),
                "and the column captions are set in the same face, or the header floats above its own columns");
            Assert.That(cell.FontFamily.Name, Is.Not.EqualTo(FontManager.Current.DefaultFontFamily.Name),
                "sanity: the panel really departs from the app-wide face rather than merely restating it");
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheFindingsAreSetSmallerThanTheSurroundingWorkspace()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();

        double workspace = rig.Window.FontSize;

        Assert.Multiple(() =>
        {
            Assert.That(FirstCellText(rig.Window).FontSize, Is.LessThan(workspace),
                "the panel is a dense readout below the workspace, not another pane of it");
            Assert.That(ByName(rig.Window, "ProblemsPanelHeader").GetValue(TextBlock.FontSizeProperty),
                Is.LessThan(workspace),
                "including its title — the band was the tallest thing on screen for a region that reports, "
                + "rather than commands, anything");
        });
    }

    /// <summary>
    /// The point of the whole exercise: the panel's DEFAULT height has to be worth opening. A screenful is the
    /// requirement; the row height that delivers it is an implementation detail.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheDefaultPanelHeightShowsAScreenfulOfFindingsNotAHandful()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();

        double rowHeight = RealizedRows(rig.Window)[0].Bounds.Height;
        ScrollContentPresenter viewport = ById(rig.Window, "ProblemsList")
            .GetVisualDescendants().OfType<ScrollContentPresenter>().First();
        int fit = (int)Math.Floor(viewport.Bounds.Height / rowHeight);

        Assert.That(fit, Is.GreaterThanOrEqualTo(7),
            $"the panel opens onto {fit} of the project's {rig.Shell.Problems.Rows.Count} findings "
            + $"({rowHeight}px per row in a {viewport.Bounds.Height}px viewport) — at Fluent's stock spacing "
            + "this was TWO, which is a list a reader scrolls rather than scans");
    }

    /// <summary>
    /// The column captions label the rows; they may not out-weigh them. Fluent's stock band is 42px against a
    /// 38px row — nearly a row and a half of chrome before the first finding.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheColumnHeaderBandDoesNotOutweighTheRowsItLabels()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();

        double rowHeight = RealizedRows(rig.Window)[0].Bounds.Height;
        Control headers = ById(rig.Window, "ProblemsList")
            .GetVisualDescendants().OfType<TableViewColumnHeadersPresenter>().First();
        double bandHeight = headers.FindAncestorOfType<Border>()!.Bounds.Height;

        Assert.That(bandHeight, Is.LessThanOrEqualTo(rowHeight * 1.5),
            $"the caption band is {bandHeight}px over {rowHeight}px rows");
    }

    /// <summary>
    /// The panel's own title band against the two tree panes': the three are the shell's pane headers and read as
    /// a set, so the one carrying the filter chips may not tower over the other two.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ThePanelTitleBandSitsLevelWithTheTreePaneHeaders()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();

        double problems = ByName(rig.Window, "ProblemsPanelHeader").FindAncestorOfType<Border>()!.Bounds.Height;
        double trees = rig.Window.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.Text == rig.Shell.InstallationPaneHeader)
            .FindAncestorOfType<Border>()!.Bounds.Height;

        Assert.That(problems, Is.LessThanOrEqualTo(trees),
            $"the Problemer band is {problems}px against the tree panes' {trees}px — it carries three filter "
            + "chips, which is no reason for it to be the tallest band in the window");
    }

    /// <summary>
    /// Density is not an escape from Vis ▸ Tekststørrelse. The panel names its own size, so it would sit out a
    /// text-scale step entirely if that size were not one of the scaled tokens.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ChoosingALargerTextSizeStillGrowsTheFindings()
    {
        using ShellHarness harness = ShellHarness.Create();
        var theme = new ThemeService();
        MainWindowViewModel vm = harness.CreateViewModel(theme: theme);
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        double before = ByName(window, "ProblemsPanelHeader").GetValue(TextBlock.FontSizeProperty);

        vm.SetTextScaleCommand.Execute(TextScale.Largest);
        Dispatcher.UIThread.RunJobs();

        Assert.That(ByName(window, "ProblemsPanelHeader").GetValue(TextBlock.FontSizeProperty), Is.GreaterThan(before),
            "the panel's own font token scales with the rest — a region that opted out of the accessibility "
            + "setting would be the one region a reader who needs it cannot read");

        window.Close();
    }
}
