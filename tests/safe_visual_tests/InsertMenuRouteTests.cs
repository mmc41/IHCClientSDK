using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// A-35 / F-088 (comparereal): route-equivalence for the product-insert menus (US-044). The menu-bar
/// <c>Insert ▸ Products</c> submenu must expose the SAME four catalog categories as the tree context menu —
/// Wired / Wireless / Bus / Special — so a bus product (IHC LED Dimmer 2 kanaler, SMS Modem) is reachable from
/// both routes, not the right-click route only. The gap was purely the menu-bar XAML omitting the Bus item.
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

        var categories = products.Items.OfType<MenuItem>().Select(m => Normalize(m.Header)).ToList();

        Assert.That(categories, Is.EqualTo(new[] { "Wired products", "IHC Wireless products", "Bus products", "Special products" }),
            "the menu-bar Insert ▸ Products submenu exposes the same four categories, in the same order, as the tree context menu (F-088)");
    }

    // Menu headers carry an access-key underscore ("_Bus products"); strip it for a stable label comparison.
    private static string Normalize(object? header) => (header as string ?? string.Empty).Replace("_", string.Empty);
}
