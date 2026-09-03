using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;

namespace Ihc.Vis.Tests;

/// <summary>US-010: the catalog → wired-products insertion menu projection (Avalonia-free logic).</summary>
public class CatalogMenuTests
{
    [Test]
    public void WiredProducts_GroupsByCategory_StripsSortPrefixes_AndWiresLeaves()
    {
        var products = new ProjectAppService(new IhcSettings()).GetProductCatalogItems();

        var menu = CatalogMenu.Build(products, CatalogMenu.WiredProductsCategory, _ => new RelayCommand(() => { }));
        var leaves = AllLeaves(menu).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(menu.Select(m => m.Header), Does.Contain("Input"), "the vendor '01#Input' category shows stripped");
            Assert.That(menu.Select(m => m.Header), Does.Contain("Output"));
            Assert.That(menu[0].Header, Is.EqualTo("Input"), "categories order by their NN# prefix");
            Assert.That(menu.All(m => !m.Header.Contains('#')), Is.True, "NN# sort prefixes are stripped from labels");
            Assert.That(menu.First(m => m.Header == "Input").Children, Is.Not.Empty, "Input nests sub-categories/products");
            Assert.That(leaves, Is.Not.Empty, "there are product leaves");
            Assert.That(leaves.All(l => l.ProductIdentifier is not null && l.Command is not null), Is.True,
                "every leaf carries a product id and an insert command");
            Assert.That(leaves.All(l => l.Children.Count == 0), Is.True, "leaves are not categories");
        });
    }

    // A-11 (E-7): the product catalog builds FOUR top categories (Wired / Wireless / Bus / Special). Bus holds the
    // SMS Modem + IHC LED Dimmer; Special is a real category with three subcategories; all 100 products are reachable.
    [Test]
    public void ProductMenu_HasFourCategories_100Leaves()
    {
        var products = new ProjectAppService(new IhcSettings()).GetProductCatalogItems();
        RelayCommand Cmd(CatalogItem _) => new(() => { });

        var wired = CatalogMenu.Build(products, CatalogMenu.WiredProductsCategory, Cmd);
        var wireless = CatalogMenu.Build(products, CatalogMenu.WirelessProductsCategory, Cmd);
        var bus = CatalogMenu.Build(products, CatalogMenu.BusProductsCategory, Cmd);
        var special = CatalogMenu.Build(products, CatalogMenu.SpecialProductsCategory, Cmd);

        Assert.Multiple(() =>
        {
            Assert.That(AllLeaves(bus).Select(l => l.Header),
                Is.EquivalentTo(new[] { "SMS Modem", "IHC LED Dimmer 2 kanaler" }), "Bus holds exactly the two bus products");
            Assert.That(special.Where(m => !m.IsLeaf).Select(m => m.Header),
                Is.EquivalentTo(new[] { "Modificeret Wireless produkter", "Vinduer", "Udgaet produkter" }),
                "Special has its three subcategories");
            Assert.That(AllLeaves(special).Count(), Is.EqualTo(11), "Special reaches all 11 special products");
            int total = new[] { wired, wireless, bus, special }.Sum(m => AllLeaves(m).Count());
            Assert.That(total, Is.EqualTo(100), "every catalog product is reachable across the four categories");
        });
    }

    // The UI is Danish, so the catalog's own category names are shown VERBATIM — no translation layer. IHC Visual
    // shows the same folder names (menu.dumpBar: Datalinie produkter ▸ Input / Output / Dimmer / Generelle), so this
    // pins that the app no longer restates the catalog's language.
    [Test]
    public void CatalogMenu_SubcategoriesRenderVerbatim()
    {
        var app = new ProjectAppService(new IhcSettings());
        var wired = CatalogMenu.Build(app.GetProductCatalogItems(), CatalogMenu.WiredProductsCategory, _ => new RelayCommand(() => { }));
        var fb = CatalogMenu.BuildFunctionBlocks(app.GetFunctionBlockCatalogItems(), _ => new RelayCommand(() => { }));

        var wiredFolders = AllFolders(wired).Select(f => f.Header).ToList();
        var fbFolders = AllFolders(fb).Select(f => f.Header).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(wiredFolders, Does.Contain("Generelle"), "the catalog's own 'Generelle' subcategory renders verbatim");
            Assert.That(wiredFolders, Does.Contain("Input").And.Contain("Output"),
                "…as do Input/Output, which the vendor also shows untranslated");
            Assert.That(fbFolders.Any(h => h.Contains("Foretrukne")), Is.True,
                "FB library folders stay verbatim too (US-018)");
        });
    }

    private static IEnumerable<ProductMenuItemViewModel> AllFolders(IEnumerable<ProductMenuItemViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
                continue;
            yield return node;
            foreach (var folder in AllFolders(node.Children))
                yield return folder;
        }
    }

    private static IEnumerable<ProductMenuItemViewModel> AllLeaves(IEnumerable<ProductMenuItemViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
                yield return node;
            else
                foreach (var leaf in AllLeaves(node.Children))
                    yield return leaf;
        }
    }
}
