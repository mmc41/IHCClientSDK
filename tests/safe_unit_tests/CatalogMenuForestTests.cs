using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Products;

namespace safe_unit_tests;

/// <summary>
/// H2 / D08 (T004): the product insertion menu's top-level categories are DERIVED from the catalog products' own
/// <c>CategoryPath</c> — never a hardcoded four-category filter — so a product with an empty <c>CategoryPath</c>
/// (an imported <c>.def</c>) stays reachable, under an "Imported/Uncategorized" bucket. <see cref="CatalogMenu"/> is
/// Avalonia-free projection logic, so it is unit-tested here.
/// </summary>
public class CatalogMenuForestTests
{
    private sealed class NoopCommand : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private static ICommand Cmd(ProductDefinition _) => new NoopCommand();

    [Test]
    public void BuildProductForest_ReachesEmptyCategoryProduct_InImportedBucket()
    {
        var real = new ProjectAppService(new IhcSettings()).GetAvailableProducts();
        // An imported .def has an empty CategoryPath (CatalogReader.ReadProduct) — model one by cloning a real product.
        var imported = real.First() with
        {
            CategoryPath = "",
            ProductIdentifier = "_0ximported",
            DisplayName = "Imported Widget",
        };
        var products = real.Append(imported).ToList();

        // The finding (H2): the legacy fixed four-category projections cannot reach an empty-category product at all.
        bool legacyReaches = new[] { "Datalinie produkter", "LK IHC Wireless produkter", "Bus Produkter", "Specielle produkter" }
            .SelectMany(cat => Leaves(CatalogMenu.Build(products, cat, Cmd)))
            .Any(l => l.ProductIdentifier == "_0ximported");
        Assert.That(legacyReaches, Is.False, "the four hardcoded categories drop an empty-category product (the H2 bug)");

        // The fix: the data-derived forest reaches it, in the Imported/Uncategorized bucket, without dropping any
        // product or losing the four vendor categories (rendered with their app-side English labels).
        var forest = CatalogMenu.BuildProductForest(products, Cmd);
        var bucket = forest.FirstOrDefault(f => f.Header == CatalogMenu.ImportedCategoryLabel);
        Assert.Multiple(() =>
        {
            Assert.That(Leaves(forest).Any(l => l.ProductIdentifier == "_0ximported"), Is.True,
                "the imported product is present in the built product menu (H2 fixed)");
            Assert.That(bucket, Is.Not.Null, "an Imported/Uncategorized bucket appears for empty-category products (D08)");
            Assert.That(Leaves(bucket!.Children).Any(l => l.ProductIdentifier == "_0ximported"), Is.True,
                "the imported product lives in that bucket");
            Assert.That(Leaves(forest).Count(), Is.EqualTo(products.Count),
                "every product is reachable — none is dropped by the menu projection");
            Assert.That(forest.Select(f => f.Header),
                Is.SupersetOf(new[] { "Wired products", "IHC Wireless products", "Bus products", "Special products" }),
                "the vendor categories keep their app-side English labels, derived from the catalog data (D08)");
            Assert.That(forest[^1].Header, Is.EqualTo(CatalogMenu.ImportedCategoryLabel),
                "the imported/uncategorized bucket sorts last");
        });
    }

    private static IEnumerable<ProductMenuItemViewModel> Leaves(IEnumerable<ProductMenuItemViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
                yield return node;
            else
                foreach (var leaf in Leaves(node.Children))
                    yield return leaf;
        }
    }
}
