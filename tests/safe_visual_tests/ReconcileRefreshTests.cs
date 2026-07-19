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
