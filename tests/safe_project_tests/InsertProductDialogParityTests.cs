using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Projects;
using ihc_openvisual.ViewModels;

namespace Ihc.Vis.Tests;

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

    /// <summary>
    /// THE ROUTE: placing a wired product asks its dialog for the documentation, and the product that arrives
    /// opens on its terminals -- the one observable effect at the far end.
    /// </summary>
    [Test]
    public async Task InsertProduct_AsksItsDialog_AndPlacesAProductOpenedOnItsTerminals()
    {
        var (harness, vm, _) = await ReadyAsync();
        using var _h = harness;
        var leaf = FirstWiredLeaf(vm);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        TreeNodeViewModel placed = vm.InstallationNodes[0].Children[2].Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditProductDialogCalls, Is.EqualTo(1),
                "the product's documentation is asked for as part of placing it");
            Assert.That(placed.Children, Is.Not.Empty, "precondition: the product brought terminals");
            Assert.That(placed.IsExpanded, Is.True, "the placed product opens so its terminals are visible");
        });
    }

    [Test]
    public async Task InsertProduct_Cancelled_InsertsNothing()
    {
        var (harness, vm, _) = await ReadyAsync();
        using var _h = harness;
        string idsBefore = harness.Session.Current!.LastUniqueId!;
        harness.Dialogs.CancelProductDialog = true;
        var leaf = FirstWiredLeaf(vm);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        // That a cancelled insert places nothing is asserted by
        // InsertStatusHonestyTests.CancellingTheDialog_SaysSo_AndInsertsNothing. What is left here is the half
        // nothing else states: the id counter does not move either, which is the vendor behaviour a cancelled
        // insert is measured against.
        Assert.That(harness.Session.Current!.LastUniqueId, Is.EqualTo(idsBefore),
            "the id counter is untouched — the vendor burns nothing on Annuller");
    }
}
