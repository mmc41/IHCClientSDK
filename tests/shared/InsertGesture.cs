using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace Ihc.Tests;

/// <summary>
/// Placing a product through the SHIPPED gesture — select a locality, then invoke the catalog menu leaf that
/// inserts under it. Shared because more than one fixture asks what the application does around that gesture, and
/// the route has to be the real one: a test that called the view-model's insert directly would skip the menu
/// leaf's own command and stop proving that the menu reaches it.
/// </summary>
internal static class InsertGesture
{
    /// <summary>A wired output product, addressed by the name the leaf carries.</summary>
    public const string Lampeudtag = "Lampeudtag";

    /// <summary>The standard empty project's kitchen — the locality these fixtures insert into.</summary>
    public static TreeNodeViewModel Kitchen(MainWindowViewModel vm) =>
        vm.InstallationNodes[0].Children.Single(c => c.DisplayName == "Køkken");

    /// <summary>Inserts <see cref="Lampeudtag"/> under <see cref="Kitchen"/> through the menu leaf's own command.</summary>
    public static async Task InsertLampeudtagAsync(MainWindowViewModel vm)
    {
        vm.SelectNode(Kitchen(vm));
        ProductMenuItemViewModel leaf = vm.ProductsMenu
            .Single(f => f.Header == CatalogMenu.WiredProductsCategory)
            .Children.Single(c => c.Header == "Output")
            .Children.Single(c => c.Header == Lampeudtag);
        await ((IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);
    }
}
