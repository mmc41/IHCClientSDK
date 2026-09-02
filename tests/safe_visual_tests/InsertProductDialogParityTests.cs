using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Projects;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>
/// Placing a product asks for its documentation as part of placing it, the way IHC Visual does
/// (uxparity S-12): the product dialog opens on insert, and cancelling it inserts nothing at all —
/// measured against the vendor, where Annuller leaves the tree and the id counter untouched.
/// </summary>
public class InsertProductDialogParityTests
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, string productName)> ReadyAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[2]);   // an empty room
        return (harness, vm, harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie", StringComparison.Ordinal)).DisplayName);
    }

    private static ProductMenuItemViewModel FirstWiredLeaf(MainWindowViewModel vm)
    {
        static ProductMenuItemViewModel? Leaf(System.Collections.Generic.IEnumerable<ProductMenuItemViewModel> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.IsLeaf)
                    return n;
                if (n.Children.Count > 0 && Leaf(n.Children) is { } found)
                    return found;
            }
            return null;
        }
        return Leaf(vm.ProductsMenu.First(c => c.Header == CatalogMenu.WiredProductsCategory).Children)!;
    }

    private static int ProductCount(Project project) =>
        project.Groups.Sum(g => g.Children.Count(c => c.Tag.StartsWith("product_", StringComparison.Ordinal)));

    [Test]
    public async Task InsertProduct_OpensTheProductDialog()
    {
        var (harness, vm, _) = await ReadyAsync();
        using var _h = harness;
        var leaf = FirstWiredLeaf(vm);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        Assert.That(harness.Dialogs.EditProductDialogCalls, Is.EqualTo(1),
            "the product's documentation is asked for as part of placing it");
    }

    [Test]
    public async Task InsertProduct_RevealsTheProductsTerminals()
    {
        var (harness, vm, _) = await ReadyAsync();
        using var _h = harness;
        var leaf = FirstWiredLeaf(vm);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        TreeNodeViewModel placed = vm.InstallationNodes[0].Children[2].Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(placed.Children, Is.Not.Empty, "precondition: the product brought terminals");
            Assert.That(placed.IsExpanded, Is.True, "the placed product opens so its terminals are visible");
        });
    }

    [Test]
    public async Task InsertProduct_Cancelled_InsertsNothing()
    {
        var (harness, vm, _) = await ReadyAsync();
        using var _h = harness;
        int before = ProductCount(harness.Session.Current!);
        string idsBefore = harness.Session.Current!.LastUniqueId!;
        harness.Dialogs.CancelProductDialog = true;
        var leaf = FirstWiredLeaf(vm);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(ProductCount(harness.Session.Current!), Is.EqualTo(before), "no product is placed");
            Assert.That(harness.Session.Current!.LastUniqueId, Is.EqualTo(idsBefore),
                "and the id counter is untouched — the vendor burns nothing on Annuller");
        });
    }
}
