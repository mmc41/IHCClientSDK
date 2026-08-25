using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-8 (owner ruling 2026-08-10): a catalog leaf on the menu bar
/// greys itself when its insert cannot run, exactly as the reference application greys one.
///
/// <para>Measured divergence: the reference application greys every insertable leaf whose insert is not
/// currently possible — the function-block templates and AutoProof with nothing selected, and again the
/// moment the *Installation* pane takes focus (a block belongs to the Functions pane) — while keeping the
/// category containers above them live. OpenVisual greyed its ~15 hand-registered bar rows but never a
/// catalog leaf: every product and every template stayed enabled with nothing selected, and clicking one
/// only then refused, in the status bar.</para>
///
/// <para>FR-2.1b ("the menu bar is deliberately NOT pane-gated") is about not HIDING and not pane-SCOPING the
/// vocabulary — the bar still lists every command — not about enablement. That is the reading the rest of the
/// app already implements, and the owner ruled for it; the story text was amended to say so.</para>
///
/// <para>The gate is the SAME predicate the insert body checks, so a greyed leaf and a refused invoke can
/// never disagree. Containers are deliberately not asserted disabled: the vendor keeps them live.</para>
/// </summary>
public class CatalogLeafAvailabilityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> BuildAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        return (harness, vm);
    }

    /// <summary>Every leaf under a menu forest, ignoring the category containers above them.</summary>
    private static IEnumerable<ProductMenuItemViewModel> Leaves(IEnumerable<ProductMenuItemViewModel> forest)
    {
        foreach (var item in forest)
        {
            if (item.IsLeaf) yield return item;
            foreach (var nested in Leaves(item.Children)) yield return nested;
        }
    }

    private static bool AnyLeafExecutable(IEnumerable<ProductMenuItemViewModel> forest) =>
        Leaves(forest).Any(leaf => leaf.Command?.CanExecute(null) == true);

    private static bool AllLeavesExecutable(IEnumerable<ProductMenuItemViewModel> forest) =>
        Leaves(forest).All(leaf => leaf.Command?.CanExecute(null) == true);

    [Test]
    public async Task WithNothingSelected_NoCatalogLeafOffersItself()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        Assert.Multiple(() =>
        {
            Assert.That(Leaves(vm.ProductsMenu).Any(), Is.True, "the product catalog produced leaves to judge");
            Assert.That(Leaves(vm.FunctionBlocksMenu).Any(), Is.True, "the block catalog produced leaves to judge");
            Assert.That(AnyLeafExecutable(vm.ProductsMenu), Is.False,
                "no product can be inserted with nothing selected, so no product leaf offers itself");
            Assert.That(AnyLeafExecutable(vm.FunctionBlocksMenu), Is.False,
                "same for the function-block templates");
        });
    }

    /// <summary>
    /// Pane ownership, which is where the reference application is strictest: a locality selected in the
    /// Installation pane makes the PRODUCTS insertable and the BLOCKS not — the vendor greys Tom Funktionsblok
    /// and AutoProof in exactly this state (measured with the pane really focused, 2026-08-10).
    /// </summary>
    [Test]
    public async Task LocalityInTheInstallationPane_OffersProducts_ButNotBlocks()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        vm.IsInstallationPaneActive = true;
        vm.SelectNode(vm.InstallationNodes[0].Children[0]);      // a locality row

        Assert.Multiple(() =>
        {
            Assert.That(AllLeavesExecutable(vm.ProductsMenu), Is.True,
                "every product is insertable into a locality selected in its own pane");
            Assert.That(AnyLeafExecutable(vm.FunctionBlocksMenu), Is.False,
                "a block belongs to the Functions pane, so no template offers itself from the Installation pane");
        });
    }

    [Test]
    public async Task LocalityInTheFunctionsPane_OffersBlocks_ButNotProducts()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        vm.IsInstallationPaneActive = false;
        vm.SelectNode(vm.FunctionNodes[0].Children[0]);

        Assert.Multiple(() =>
        {
            Assert.That(AllLeavesExecutable(vm.FunctionBlocksMenu), Is.True,
                "every template is insertable into a locality selected in the Functions pane");
            Assert.That(AnyLeafExecutable(vm.ProductsMenu), Is.False,
                "products belong to the Installation pane");
        });
    }

    /// <summary>
    /// The half that a CanExecute gate silently gets wrong: the commands outlive the selection, so without an
    /// explicit re-query the menu keeps whatever availability it had when it was built — greyed forever, or
    /// live forever. Asserted as a TRANSITION rather than a state for that reason.
    /// </summary>
    [Test]
    public async Task ChangingTheSelection_ReQueriesEveryLeaf()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;
        Assert.That(AnyLeafExecutable(vm.ProductsMenu), Is.False, "precondition: nothing selected");

        vm.IsInstallationPaneActive = true;
        vm.SelectNode(vm.InstallationNodes[0].Children[0]);
        Assert.That(AllLeavesExecutable(vm.ProductsMenu), Is.True, "selecting a locality re-enables the leaves");

        vm.SelectNode(vm.InstallationNodes[0]);                  // the root, not a locality
        Assert.That(AnyLeafExecutable(vm.ProductsMenu), Is.False,
            "moving to a node that cannot host a product greys them again");
    }
}
