using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;

namespace safe_visual_tests;

/// <summary>US-010: the catalog → wired-products insertion menu projection (Avalonia-free logic).</summary>
public class CatalogMenuTests
{
    [Test]
    public void BuildWiredProducts_GroupsByCategory_StripsSortPrefixes_AndWiresLeaves()
    {
        var products = new ProjectAppService(new IhcSettings()).GetAvailableProducts();

        var menu = CatalogMenu.BuildWiredProducts(products, _ => new RelayCommand(() => { }));
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
