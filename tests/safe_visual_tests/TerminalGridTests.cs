using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// US-012's two terminal grids, as the renderer actually lays them out and routes them.
///
/// <para>The grids are a hand-written composite (D12) rendered from ONE template for both sides, so the two
/// things a copy-paste pair used to get wrong are pinned here: that a header sits over the columns it heads,
/// and that <i>Konfigurer</i> addresses the row the installer selected. Neither had a test — the pair was
/// covered only by "both grids exist and their buttons are enabled".</para>
/// </summary>
public class TerminalGridTests : AvaloniaTestBase
{
    private const string Lampeudtag = "_0x2202";     // wired: hosts the terminal grids
    private const string TemperatureSensor = "_0x2124";   // hosts the Indstillinger grid (T070)

    private static readonly ProductTerminal FirstInput =
        new("Tryk (venstre)", "Datalinie 1.01", "Blå", "Ved døren", IsOutput: false, PinId: "_0x525b");

    private static readonly ProductTerminal SecondInput =
        new("Tryk (højre)", "Datalinie 1.02", "Brun", "Ved vinduet", IsOutput: false, PinId: "_0x525c");

    private static readonly ProductTerminal OnlyOutput =
        new("Udgang", "Datalinie 1.03", "Sort", "Lampe", IsOutput: true, PinId: "_0x525d");

    // ── Header alignment ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Each column header sits over the column it heads.
    /// <para>The header grid is authored OUTSIDE the list and the row grids are realized INSIDE it, so an
    /// identical column spec is not on its own enough: the theme's <c>ListBoxItem</c> padding insets every
    /// row inside a header that is not inset, and the columns shear apart — measured at +12 px on the left
    /// edge and −12 px on the right, with all three copies of the spec in perfect agreement. That is why
    /// this is asserted on the realized GEOMETRY and not on the spec strings.</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EachTerminalHeader_SitsOverTheColumnItHeads()
    {
        ProductDialogWindow window = OpenWired();

        foreach (string listId in new[] { "dlg.terminaler.indgange", "dlg.terminaler.udgange" })
        {
            ListBox list = ListWithId(window, listId);
            double[] header = ColumnEdges(HeaderOf(list, columns: 4), window);
            double[] row = ColumnEdges(FirstRowGrid(list), window);

            Assert.That(row, Is.EqualTo(header).Within(0.5),
                $"{listId}: every column boundary of a row lands on the boundary of the header above it");
        }
    }

    /// <summary>The sensors' <i>Indstillinger</i> grid is the same composite with three columns, and had the
    /// same shear for the same reason.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheSettingsHeader_SitsOverTheColumnItHeads()
    {
        ProductDialogWindow window = Open(TemperatureSensor, [],
            [new ProductSetting("Kalibrering", "Justering af måling", "0,5")]);

        ListBox list = ListWithId(window, "dlg.indstillinger.liste");
        double[] header = ColumnEdges(HeaderOf(list, columns: 3), window);
        double[] row = ColumnEdges(FirstRowGrid(list), window);

        Assert.That(row, Is.EqualTo(header).Within(0.5));
    }

    /// <summary>
    /// The four column widths are written ONCE, and each realized grid still owns its own definitions.
    /// <para>Both halves matter. A single spec is what keeps the header in step with the rows; a shared
    /// <c>ColumnDefinitions</c> INSTANCE would be a different bug — Avalonia's definition list re-parents a
    /// definition on assignment ("moves a definition from its current parent tree"), so every newly realized
    /// row would steal the columns from the row before it.</para>
    /// </summary>
    [AvaloniaTest]
    public void EveryTerminalGrid_HasTheSameWidths_AndItsOwnDefinitions()
    {
        ProductDialogWindow window = OpenWired();

        List<Grid> grids = [.. window.GetVisualDescendants().OfType<Grid>()
            .Where(g => g.ColumnDefinitions.Count == 4)];

        Assert.Multiple(() =>
        {
            Assert.That(grids, Has.Count.GreaterThanOrEqualTo(4),
                "two headers and at least one row per side are realized");
            Assert.That(grids.Select(g => string.Join(",", g.ColumnDefinitions.Select(c => c.Width))).Distinct(),
                Has.Exactly(1).Items, "every terminal grid is laid out to the same one spec");
            List<object> definitions =
                [.. grids.Select(g => (object)g.ColumnDefinitions).Distinct(ReferenceEqualityComparer.Instance)];
            Assert.That(definitions, Has.Count.EqualTo(grids.Count),
                "…and no two grids share a ColumnDefinitions instance, which would re-parent the definitions");
        });
    }

    // ── The Konfigurer route ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <i>Konfigurer indgang</i> addresses the row that is SELECTED, not the first one.
    /// <para>Untested until now: the pair of buttons recovered their list by walking the visual tree for a
    /// hard-coded control name, so renaming the list in markup still compiled and silently disabled both
    /// buttons.</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void Konfigurer_AddressesTheSelectedTerminal()
    {
        ProductDialogWindow window = OpenWired();
        ListWithId(window, "dlg.terminaler.indgange").SelectedItem = SecondInput;

        Click(window, "dlg.terminaler.konfigurerIndgang");

        ProductDialogWidgetAction? action = window.AcceptedResult?.WidgetAction;
        Assert.Multiple(() =>
        {
            Assert.That(action?.Kind, Is.EqualTo(DialogWidgetKind.TerminalGrids));
            Assert.That(action?.Target, Is.EqualTo(Pin(SecondInput)),
                "the SELECTED row is addressed, not the pre-selected first one");
        });
    }

    /// <summary>And the output side addresses its OWN selection — the two grids are rendered from one
    /// template, so this is what proves the button reads the section it belongs to rather than whichever
    /// list the renderer happened to realize first.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void KonfigurerUdgang_AddressesTheOutputSide()
    {
        ProductDialogWindow window = OpenWired();

        Click(window, "dlg.terminaler.konfigurerUdgang");

        Assert.That(window.AcceptedResult?.WidgetAction?.Target, Is.EqualTo(Pin(OnlyOutput)));
    }

    /// <summary>Clearing the selection and pressing <i>Konfigurer</i> does nothing at all: the grids are
    /// pre-selected on open, so an empty selection means the installer actively cleared it, and addressing
    /// the first row anyway would configure a terminal they did not pick.</summary>
    [AvaloniaTest]
    public void Konfigurer_WithNothingSelected_AddressesNothing()
    {
        ProductDialogWindow window = OpenWired();
        ListWithId(window, "dlg.terminaler.indgange").SelectedItem = null;

        Click(window, "dlg.terminaler.konfigurerIndgang");

        Assert.Multiple(() =>
        {
            Assert.That(window.AcceptedResult, Is.Null, "no widget action, and no commit");
            Assert.That(window.IsVisible, Is.True, "the dialog stays open");
        });
    }

    /// <summary>Both grids are pre-selected on open, which is the state the rule above is stated against.</summary>
    [AvaloniaTest]
    public void BothGrids_ArePreSelectedOnOpen()
    {
        ProductDialogWindow window = OpenWired();

        Assert.Multiple(() =>
        {
            Assert.That(ListWithId(window, "dlg.terminaler.indgange").SelectedItem, Is.EqualTo(FirstInput));
            Assert.That(ListWithId(window, "dlg.terminaler.udgange").SelectedItem, Is.EqualTo(OnlyOutput));
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static ProductDialogWindow OpenWired() =>
        Open(Lampeudtag, [FirstInput, SecondInput, OnlyOutput], []);

    /// <summary>Drives the ONE generic dialog on a REAL composed descriptor — a stub descriptor hosts no
    /// widgets, so the grids under test would not be built at all.</summary>
    private static ProductDialogWindow Open(
        string productIdentifier, IReadOnlyList<ProductTerminal> terminals, IReadOnlyList<ProductSetting> settings)
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality,
            app.GetAvailableProducts().First(p => p.ProductIdentifier == productIdentifier))).Value;

        var window = new ProductDialogWindow();
        CurrentTestWindow = window;
        window.Populate(new ProductDialogViewModel(
            app.GetProductDialog(session.Current!, placed), terminals, settings));
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ElementId Pin(ProductTerminal terminal)
    {
        Assert.That(ElementId.TryParse(terminal.PinId, out ElementId id), Is.True,
            "the fixture's pin token is one the dialog can address");
        return id;
    }

    private static ListBox ListWithId(Window window, string automationId) =>
        window.GetVisualDescendants().OfType<ListBox>()
            .Single(l => AutomationProperties.GetAutomationId(l) == automationId);

    private static void Click(Window window, string automationId)
    {
        window.GetVisualDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetAutomationId(b) == automationId)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The header grid of a list: the one grid beside it, outside any row.</summary>
    private static Grid HeaderOf(ListBox list, int columns) =>
        list.GetVisualAncestors().OfType<StackPanel>().First()
            .GetVisualDescendants().OfType<Grid>()
            .First(g => g.ColumnDefinitions.Count == columns
                        && !g.GetVisualAncestors().OfType<ListBoxItem>().Any());

    private static Grid FirstRowGrid(ListBox list) =>
        list.GetVisualDescendants().OfType<ListBoxItem>().First()
            .GetVisualDescendants().OfType<Grid>().First();

    /// <summary>Every column boundary of <paramref name="grid"/>, left to right, in window coordinates —
    /// which is the only frame in which a header and a row can be compared at all.</summary>
    private static double[] ColumnEdges(Grid grid, Visual root)
    {
        double x = grid.TranslatePoint(new Point(0, 0), root)!.Value.X;
        var edges = new List<double> { x };
        foreach (ColumnDefinition column in grid.ColumnDefinitions)
        {
            x += column.ActualWidth;
            edges.Add(x);
        }
        return [.. edges];
    }
}
