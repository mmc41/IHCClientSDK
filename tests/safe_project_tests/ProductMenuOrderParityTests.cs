using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Alignment F-9 — the product insert menu renders the catalog's OWN order, not an alphabetical one.
///
/// <para>Measured live 2026-08-11 by dumping the reference application's <i>Produkter</i> flyout to depth 5 (every
/// family, every subgroup, every leaf). Its leaves are not alphabetical and not in id order either: they are the
/// order the catalog itself declares, which the catalog encodes exactly as it encodes folder order — an
/// <c>NN#</c> prefix on the component's <c>name</c> (<c>01#Lampeudtag</c>, <c>02#Stikkontakt</c>, <c>05#Diode</c>),
/// stripped for display.</para>
///
/// <para>OpenVisual ordered folders by that prefix but sorted LEAVES by display name, so every leaf list in the
/// menu was alphabetised. F-9 caught it on <i>Bus Produkter</i>, the only family small enough to have been compared
/// member by member; it is in fact every family.</para>
///
/// <para>The order is not cosmetic. The reference application's Output list runs
/// <i>Lampeudtag, Stikkontakt, Output 1-10V…</i> — lighting outlets, then sockets, then dimming outputs, then the
/// heating cluster — a functional grouping an installer navigates by position. Alphabetising scatters it, and
/// leaves someone who knows the original's menu reaching for the wrong row.</para>
/// </summary>
public class ProductMenuOrderParityTests
{
    /// <summary>The reference application's <i>Datalinie produkter ▸ Output</i>, verbatim and in order (measured
    /// 2026-08-11). Seventeen leaves, alphabetical in neither direction; note <i>Dørlås</i> before <i>Telestat</i>,
    /// which share the prefix <c>07#</c> and are separated by the rest of the name.</summary>
    private static readonly string[] VendorOutputOrder =
    [
        "Lampeudtag", "Stikkontakt", "Output 1-10V", "Output 1-10V IHC/SA", "Diode", "Ringeklokke", "Dørlås",
        "Telestat", "Vandvarmer", "El-radiator", "El-gulvvarme", "Ventilator", "Cirkulationspumpe",
        "Magnetventil NC", "Magnetventil NO", "Lydgiver intern", "Lydgiver ekstern",
    ];

    /// <summary>The original's <i>Bus Produkter</i>: SMS Modem first. This is the pair F-9 was raised on, and it is
    /// the case a "sort alphabetically" rule gets backwards — <i>I</i> precedes <i>S</i>.</summary>
    private static readonly string[] VendorBusOrder = ["SMS Modem", "IHC LED Dimmer 2 kanaler"];

    /// <summary>The original's <i>LK IHC Wireless produkter ▸ Input</i> — a third family, so the rule is shown to
    /// hold across the catalog rather than on one lucky list. <i>Nøglering</i> before <i>Fjernbetjening</i> is the
    /// giveaway: alphabetically it is the other way round.</summary>
    private static readonly string[] VendorWirelessInputOrder =
    [
        "Tryk 2 tast", "Tryk 4 tast", "Tryk 6 tast", "Nøglering", "Fjernbetjening", "Puck input",
    ];

    [Test]
    public async Task WiredOutput_KeepsTheCatalogsOwnOrder()
    {
        Assert.That(await LeavesUnderAsync(CatalogMenu.WiredProductsCategory, "Output"),
            Is.EqualTo(VendorOutputOrder).AsCollection);
    }

    [Test]
    public async Task BusProducts_KeepTheCatalogsOwnOrder()
    {
        Assert.That(await LeavesUnderAsync(CatalogMenu.BusProductsCategory),
            Is.EqualTo(VendorBusOrder).AsCollection);
    }

    [Test]
    public async Task WirelessInput_KeepsTheCatalogsOwnOrder()
    {
        Assert.That(await LeavesUnderAsync(CatalogMenu.WirelessProductsCategory, "Input"),
            Is.EqualTo(VendorWirelessInputOrder).AsCollection);
    }

    /// <summary>Folders and leaves share ONE ordering sequence. The original's <i>Datalinie ▸ Input</i> runs
    /// <c>01#LK FUGA</c>, <c>02#LK OPUS</c>, then the three PIR products (<c>03#</c>–<c>05#</c>), then
    /// <c>06#IR fjernbetjeninger</c> and <c>07#Mini Modul</c>, then <c>08#Ringetryk</c> — subfolders and products
    /// interleaved and numbered together. Emitting all folders and then all leaves is the natural shape and the
    /// wrong one, and no per-list sort can repair it, because the information lives in the numbering the two kinds
    /// SHARE. Found by diffing all 100 leaves of both menus once the leaf order above was fixed.</summary>
    [Test]
    public async Task FoldersAndProducts_InterleaveAsTheOriginalDoes()
    {
        IReadOnlyList<ProductMenuItemViewModel> forest = await ProductsMenuAsync();
        ProductMenuItemViewModel input = forest.Single(f => f.Header == CatalogMenu.WiredProductsCategory)
            .Children.Single(c => c.Header == "Input");

        Assert.That(input.Children.Take(8).Select(c => c.Header).ToArray(),
            Is.EqualTo(new[]
            {
                "LK FUGA", "LK OPUS", "PIR", "PIR med skumringsrelæ", "PIR alarm",
                "IR fjernbetjeninger", "Mini Modul", "Ringetryk",
            }).AsCollection);
    }

    /// <summary>The families themselves keep the original's order, which was already right — pinned so the leaf fix
    /// cannot disturb it.</summary>
    [Test]
    public async Task TheFamilies_KeepTheOriginalsOrder()
    {
        IReadOnlyList<ProductMenuItemViewModel> forest = await ProductsMenuAsync();

        Assert.That(forest.Select(f => f.Header).ToArray(),
            Is.EqualTo(new[]
            {
                "Bus Produkter", "Datalinie produkter", "LK IHC Wireless produkter", "Specielle produkter",
            }).AsCollection);
    }

    /// <summary>The leaf labels under a family, optionally descending one named subgroup — the menu as a driver
    /// reads it, built from the real catalog rather than from a fixture, because the ORDER under test is the
    /// catalog's own.</summary>
    private static async Task<string[]> LeavesUnderAsync(string family, string? subgroup = null)
    {
        IReadOnlyList<ProductMenuItemViewModel> forest = await ProductsMenuAsync();
        ProductMenuItemViewModel node = forest.Single(f => f.Header == family);
        if (subgroup is not null)
            node = node.Children.Single(c => c.Header == subgroup);
        return [.. node.Children.Where(c => c.Command is not null).Select(c => c.Header)];
    }

    private static async Task<IReadOnlyList<ProductMenuItemViewModel>> ProductsMenuAsync()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        return [.. viewModel.ProductsMenu];
    }
}
