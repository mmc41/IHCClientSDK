using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// fablerefac W3-6: the shell refresh drives the keyed reconciler (W3-4) in place instead of clearing and
/// rebuilding both panes, so a config-mode edit preserves the node instances it did not touch — Avalonia keeps
/// their containers, and selection/expansion survive by identity. Structure stays rebuild-equivalent (the whole
/// existing tree suite is the regression guard); these tests pin the incremental behaviour.
/// </summary>
public class ReconcileRefreshTests : AvaloniaTestBase
{
    [Test]
    public async Task ConfigEdit_PreservesUntouchedNodeIdentity()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId untouched = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        TreeNodeViewModel before = TreeNodes.FindById(vm.InstallationNodes, untouched)!;
        TreeNodeViewModel rootBefore = vm.InstallationNodes[0];

        await harness.Session.AddLocalityAsync();   // an unrelated edit — the untouched locality must not be rebuilt

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes[0], Is.SameAs(rootBefore), "the Localities root keeps its instance (in-place reconcile)");
            Assert.That(TreeNodes.FindById(vm.InstallationNodes, untouched), Is.SameAs(before),
                "an untouched locality keeps its node instance across an edit (reconcile, not rebuild)");
        });
    }

    [Test]
    public async Task ConfigEdit_KeepsSelection_OnTheReconcilePath()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        TreeNodeViewModel selected = TreeNodes.FindById(vm.InstallationNodes, loc)!;
        vm.SelectedInstallationNode = selected;

        await harness.Session.AddLocalityAsync();   // an unrelated edit — reconcile path

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedInstallationNode, Is.SameAs(selected), "selection survives an edit by identity on the reconcile path");
            Assert.That(TreeNodes.FindById(vm.InstallationNodes, loc), Is.SameAs(selected), "and the selected node is still the one in the tree");
        });
    }

    [Test]
    public async Task Undo_ReconcilesInPlace_SelectionAndExpansionSurviveByIdentity()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        TreeNodeViewModel rootBefore = vm.InstallationNodes[0];
        TreeNodeViewModel selected = TreeNodes.FindById(vm.InstallationNodes, loc)!;
        vm.SelectedInstallationNode = selected;
        bool flippedExpansion = !selected.IsExpanded;   // a deliberate non-default view state the undo must not disturb
        selected.IsExpanded = flippedExpansion;

        await harness.Session.AddLocalityAsync();   // the edit to reverse
        await harness.Session.UndoAsync();          // crudarch G3/T007: the undo delta reconciles in place

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes[0], Is.SameAs(rootBefore),
                "the Localities root keeps its instance across the undo — the reconciler path ran, not a rebuild");
            Assert.That(TreeNodes.FindById(vm.InstallationNodes, loc), Is.SameAs(selected),
                "an untouched node keeps its instance across the undo");
            Assert.That(vm.SelectedInstallationNode, Is.SameAs(selected), "selection survives the undo by identity");
            Assert.That(selected.IsExpanded, Is.EqualTo(flippedExpansion),
                "expansion state survives the undo untouched on the same instance");
        });
    }

    [Test]
    public async Task Redo_KeepsSelectionOnTheCurrentTreeNode()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddLocalityAsync();
        await harness.Session.UndoAsync();
        vm.SelectedInstallationNode = TreeNodes.FindById(vm.InstallationNodes, loc);

        await harness.Session.RedoAsync();          // redo reconciles in place too (T007); the assert also holds on the rebuild fallback

        Assert.That(vm.SelectedInstallationNode, Is.SameAs(TreeNodes.FindById(vm.InstallationNodes, loc)),
            "the installer's selection survives a redo (E14)");
    }

    [Test]
    public async Task ConfigEdit_RenamedNodeReRendersInPlace()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId target = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        TreeNodeViewModel before = TreeNodes.FindById(vm.InstallationNodes, target)!;

        await harness.Session.RenameLocalityAsync(target, "Renamed-XYZ", string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(TreeNodes.FindById(vm.InstallationNodes, target), Is.SameAs(before), "the renamed node keeps its instance");
            Assert.That(before.DisplayName, Is.EqualTo("Renamed-XYZ"), "the label re-rendered in place on the same instance");
        });
    }
}
