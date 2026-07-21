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
            .Single(m => Normalize(m.Header) == "Products");

        // The menu-bar Products submenu is wired to the single data-driven product menu (H2/D08) — the same source
        // the tree context menu binds — so both routes always expose the same catalog-derived categories.
        Assert.That(products.ItemsSource, Is.SameAs(viewModel.ProductsMenu),
            "the menu-bar Insert ▸ Products submenu is bound to the data-driven ProductsMenu");
        var categories = viewModel.ProductsMenu.Select(m => m.Header).ToList();
        Assert.That(categories, Is.EqualTo(new[] { "Wired products", "IHC Wireless products", "Bus products", "Special products" }),
            "the built-in catalog derives exactly the four vendor categories, in order (F-088); imports would add an Imported/Uncategorized bucket");
    }

    // Menu headers carry an access-key underscore ("_Products"); strip it for a stable label comparison.
    private static string Normalize(object? header) => (header as string ?? string.Empty).Replace("_", string.Empty);
}
