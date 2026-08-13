using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
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
/// T070: the sensors' <i>Indstillinger</i> grid — a THIRD terminal-style grid the vendor draws beneath
/// Indgange and Udgange, listing the product's calibration settings with a Navn, a Note and a Værdi.
///
/// <para>Measured on product 036 (Temperatur sensor): the vendor shows
/// <c>Kalibrering af rumføler | Offset fra målt temperatur | 0,0 °C</c> and the same for the floor
/// sensor. OpenVisual showed no such section, so the offsets were unreachable — an absent capability of
/// the F-52 kind, not a layout difference.</para>
///
/// <para>It is invisible to the classifier and to the vendor's own field dump: the rows live in a list
/// view, and <c>product.getProperties</c> reports only Edit/ComboBox fields. Six catalog products carry
/// these settings and every one of their field dumps looks complete. <b>Only the side-by-side picture
/// showed it</b>, which is the argument for reviewing the composite on every product rather than
/// trusting a clean machine verdict.</para>
/// </summary>
public class SettingsGridTests : AvaloniaTestBase
{
    private const string TemperatureSensor = "_0x2124";
    private const string PlainPushButton = "_0x2101";

    private static (ProductDialogDescriptor Dialog, ProductSetting[] Settings) Compose(string productIdentifier)
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality,
            app.GetAvailableProducts().First(p => p.ProductIdentifier == productIdentifier))).Value;

        // Built the way the coordinator builds them — from SettingElements, with the value through the app's own
        // per-type formatter. Projecting the raw attributes instead would assert a row the dialog never renders.
        Project current = session.Current!;
        ProjectElement element = current.FindById(placed)!;
        ProductSetting[] settings = [.. new ProductView(current, element).SettingElements
            .Select(current.View)
            .Select(view => new ProductSetting(
                view.Name ?? string.Empty,
                view.Note ?? string.Empty,
                VariableValueFormat.For(view.Element.Tag, view.Effective) ?? string.Empty))];
        return (app.GetProductDialog(current, placed), settings);
    }

    /// <summary>The descriptor declares the grid for a product that HAS settings.</summary>
    [Test]
    public void ASensorsDialog_DeclaresTheSettingsGrid()
    {
        (ProductDialogDescriptor dialog, ProductSetting[] settings) = Compose(TemperatureSensor);

        Assert.Multiple(() =>
        {
            Assert.That(dialog.Groups.Any(g => g.Widgets.Contains(DialogWidgetKind.SettingsGrid)), Is.True,
                "the vendor draws an Indstillinger grid for this product");
            Assert.That(settings.Select(s => s.Name),
                Is.EqualTo(new[] { "Kalibrering af rumføler", "Kalibrering af gulvføler" }).AsCollection);
            Assert.That(settings[0].Note, Is.EqualTo("Offset fra målt temperatur"));
        });
    }

    /// <summary>
    /// And NOT for a product that has none. Presence matters here in a way it does not for the terminal
    /// grids: the vendor shows both terminal grids always (US-012), but shows Indstillinger only where
    /// there are settings — measured on products 021/029/033/034, none of which draws one.
    /// </summary>
    [Test]
    public void APlainProductsDialog_DeclaresNoSettingsGrid()
    {
        (ProductDialogDescriptor dialog, ProductSetting[] settings) = Compose(PlainPushButton);

        Assert.Multiple(() =>
        {
            Assert.That(dialog.Groups.Any(g => g.Widgets.Contains(DialogWidgetKind.SettingsGrid)), Is.False);
            Assert.That(settings, Is.Empty);
        });
    }

    /// <summary>The window renders the rows, captioned as the vendor captions them.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheSettingsGrid_RendersItsRows()
    {
        (ProductDialogDescriptor dialog, ProductSetting[] settings) = Compose(TemperatureSensor);
        var window = new ProductDialogWindow();
        CurrentTestWindow = window;
        window.Populate(new ProductDialogViewModel(dialog, terminals: null, settings: settings));
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ListBox? grid = window.GetVisualDescendants().OfType<ListBox>()
            .FirstOrDefault(l => l.Name == "SettingsList");
        var captions = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text).Where(t => t is not null).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(grid, Is.Not.Null, "the Indstillinger grid is rendered");
            Assert.That(grid!.IsEffectivelyVisible, Is.True);
            Assert.That(grid.ItemCount, Is.EqualTo(2), "one row per setting");
            Assert.That(captions, Does.Contain("Indstillinger <klik for at konfigurere>"),
                "captioned as the vendor captions it");
            Assert.That(captions, Does.Contain("Værdi"), "and the value column is headed");
        });
    }
}
