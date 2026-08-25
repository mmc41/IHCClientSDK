using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// A-35 / F-088 (comparereal) + H2/D08 (T004): route-equivalence for the product-insert menus (US-044). The menu-bar
/// <c>Insert ▸ Products</c> submenu is bound to the SAME data-driven product menu (<c>ProductsMenu</c>) as the tree
/// context menu, whose top categories are derived from the catalog — Wired / Wireless / Bus / Special (plus an
/// Imported/Uncategorized bucket when empty-category products are present) — so a bus product (IHC LED Dimmer, SMS
/// Modem) is reachable from both routes. The category set is catalog data, not a hardcoded per-route XAML list.
/// </summary>
public class InsertMenuRouteTests : AvaloniaTestBase
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MenuBar_InsertProducts_ExposesAllFourCategories()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var menu = window.GetVisualDescendants().OfType<Menu>().First();
        var insert = menu.Items.OfType<MenuItem>()
            .Single(m => AutomationProperties.GetAutomationId(m) == "MenuInsert");
        var products = insert.Items.OfType<MenuItem>()
            .Single(m => Normalize(m.Header) == "Produkter");

        // The menu-bar Products submenu is wired to the single data-driven product menu (H2/D08) — the same source
        // the tree context menu binds — so both routes always expose the same catalog-derived categories.
        Assert.That(products.ItemsSource, Is.SameAs(viewModel.ProductsMenu),
            "the menu-bar Insert ▸ Products submenu is bound to the data-driven ProductsMenu");
        var categories = viewModel.ProductsMenu.Select(m => m.Header).ToList();
        // Alignment F-12a: the vendor's Indsæt ▸ Produkter lists Bus FIRST (armed bar dump, 2026-08-09):
        // Bus, Datalinie, Wireless, Specielle.
        Assert.That(categories, Is.EqualTo(new[] { CatalogMenu.BusProductsCategory, CatalogMenu.WiredProductsCategory, CatalogMenu.WirelessProductsCategory, CatalogMenu.SpecialProductsCategory }),
            "the built-in catalog derives exactly the four vendor categories, in the vendor's menu order (F-088/F-12a); imports would add an Imported/Uncategorized bucket");
    }

    /// <summary>
    /// Alignment F-12b: the vendor's Indsæt ▸ Program elementer lists
    /// "Ny case værdi som angiver tilstand" FIRST (then Program, Under program, Logik gruppe), and its label
    /// carries no ellipsis — measured 2026-08-09, armed bar dump.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ProgramElements_FollowVendorOrder_AndCaseValueLabelCarriesNoEllipsis()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var menu = window.GetVisualDescendants().OfType<Menu>().First();
        var insert = menu.Items.OfType<MenuItem>()
            .Single(m => AutomationProperties.GetAutomationId(m) == "MenuInsert");
        var programElements = insert.Items.OfType<MenuItem>()
            .Single(m => AutomationProperties.GetAutomationId(m) == "menu.programElements");

        var labels = programElements.Items.OfType<MenuItem>().Select(m => Normalize(m.Header)).ToList();
        Assert.That(labels, Is.EqualTo(new[]
            { "Ny case værdi som angiver tilstand", "Program", "Under program", "Logik gruppe" }),
            "vendor order and wording, measured 2026-08-09");
    }

    /// <summary>
    /// Alignment F-20: the vendor closes every node flyout with a separator
    /// immediately before Egenskaber (measured 2026-08-09 on a locality; story 11's inventory table shows the same
    /// on every node kind). The separator follows Egenskaber's own visibility gate, so the root flyout — which has
    /// no Egenskaber — does not end in a dangling rule.
    /// </summary>
    [AvaloniaTest]
    public async Task NodeFlyout_CarriesASeparatorImmediatelyBeforeEgenskaber()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();

        var flyout = (MenuFlyout)window.Resources["NodeContextMenu"]!;
        var items = flyout.Items.Cast<object>().ToList();
        int egenskaber = items.FindIndex(i =>
            i is MenuItem m && AutomationProperties.GetAutomationId(m) == "ctx.node.properties");

        Assert.Multiple(() =>
        {
            Assert.That(egenskaber, Is.GreaterThan(0), "the flyout carries Egenskaber");
            Assert.That(items[egenskaber - 1], Is.InstanceOf<Separator>(),
                "vendor: a separator sits immediately before Egenskaber (F-20)");
        });
    }

    // Menu headers carry an access-key underscore ("_Produkter"); strip it for a stable label comparison.
    private static string Normalize(object? header) => (header as string ?? string.Empty).Replace("_", string.Empty);
}
