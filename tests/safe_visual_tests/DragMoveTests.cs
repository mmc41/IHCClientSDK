using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// A-31 (US-054) — dragging a product onto a locality performs the <b>same id-preserving re-parent as Cut/Paste</b>
/// (the same <see cref="ProjectWorkflow.MoveNodeAsync"/> op), with drop-target highlighting; an illegal target (the
/// node itself, one of its descendants, or a container that may not hold it) is not highlighted and the drop is
/// refused with a reason. The legality is the SDK's move contract (self/descendant + container-admissibility), asked
/// via <see cref="ProjectWorkflow.CanApply"/> (the MoveNode command's own Evaluate) — not re-encoded in the view-model.
/// </summary>
public class DragMoveTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId productId, ElementId locA, ElementId locB)>
        BuildAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var productId = harness.Session.Current!.FindById(locA)!.Children.First(c => c.Tag.StartsWith("product_", StringComparison.Ordinal)).Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;
        return (harness, vm, productId, locA, locB);
    }

    // uxparity S-11: after a DROP the target row opens with everything under it, and stays open — measured
    // against IHC Visual, where a second drag onto another row leaves the first one open too. It shows what the
    // drop landed next to. The keyboard supplements deliberately do not do this (see the move-up test below).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Drop_ExpandsTheTargetRowAndItsSubtree()
    {
        var (harness, vm, productId, locA, locB) = await BuildAsync();
        using var _ = harness;
        // Give locality B a product of its own, so the drop target has a subtree that can be revealed.
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locB, product.ProductIdentifier);
        TreeNodeViewModel targetNode = TreeNodes.FindById(vm.InstallationNodes, locB)!;
        targetNode.IsExpanded = false;
        foreach (TreeNodeViewModel c in targetNode.Children)
            c.IsExpanded = false;

        await vm.DragDrop.PerformDropAsync(productId, locB);

        TreeNodeViewModel after = TreeNodes.FindById(vm.InstallationNodes, locB)!;
        Assert.Multiple(() =>
        {
            Assert.That(after.IsExpanded, Is.True, "the drop target opens");
            Assert.That(after.Children.All(c => c.Children.Count == 0 || c.IsExpanded), Is.True,
                "and so does everything under it");
        });
    }

    private static async Task<(ShellHarness harness, MainWindowViewModel vm, MainWindow window, ElementId productId, ElementId locA, ElementId locB)>
        ShowAsync()
    {
        var (harness, vm, productId, locA, locB) = await BuildAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        for (int i = 0; i < 3; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
        }
        return (harness, vm, window, productId, locA, locB);
    }

    // The core gesture: drop the product on locality B and it re-parents there with its identity preserved (same SDK op
    // as Cut/Paste — the id is unchanged, not a fresh copy), reflected in the Installation tree, with "Flyttet." feedback.
    [Test]
    public async Task DropProductOnLocality_Reparents_LikeCutPaste()
    {
        var (harness, vm, productId, locA, locB) = await BuildAsync();
        using var _ = harness;

        Assert.That(vm.DragDrop.CanDropOn(productId, locB).Effect, Is.EqualTo(DropEffect.Move), "the drag-over shows a Move over a legal locality");

        await vm.DragDrop.PerformDropAsync(productId, locB);

        var treeLocA = TreeNodes.FindById(vm.InstallationNodes, locA)!;
        var treeLocB = TreeNodes.FindById(vm.InstallationNodes, locB)!;
        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.FindById(productId), Is.Not.Null, "the id is preserved (a move, not a fresh copy)");
            Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locB), "re-parented under the target locality");
            Assert.That(TreeNodes.FindById(treeLocB.Children, productId), Is.Not.Null, "shown under locality B in the tree");
            Assert.That(TreeNodes.FindById(treeLocA.Children, productId), Is.Null, "no longer under locality A");
            Assert.That(vm.StatusText, Is.EqualTo("Flyttet."));
        });
    }

    // An illegal target — a descendant of the dragged node (here the product's own pin) — is refused, and it says why
    // rather than failing silently. The legality is the SDK's, so the view-model never re-encodes the rule.
    [Test]
    public async Task DropOnDescendant_IsRefused_WithReason()
    {
        var (harness, vm, productId, _, _) = await BuildAsync();
        using var _ = harness;
        var productNode = TreeNodes.FindById(vm.InstallationNodes, productId)!;
        var pinId = productNode.Children.First(c => c.ElementId is not null).ElementId!.Value;

        DropVerdict verdict = vm.DragDrop.CanDropOn(productId, pinId);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Ok, Is.False, "a product cannot be dropped into its own descendant");
            Assert.That(verdict.Effect, Is.EqualTo(DropEffect.None), "so the drag-over shows no drop");
            Assert.That(verdict.Reason, Is.Not.Null.And.Not.Empty, "the refusal carries a reason for the status bar");
        });
    }

    // The highlight follows legality: DragOver a legal locality yields Move; DragOver the product's CURRENT parent
    // (already there — refused by the same move contract) yields None, so it is not highlighted.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DragOver_HighlightsLegalLocality_NotIllegal()
    {
        var (harness, vm, window, productId, locA, locB) = await ShowAsync();
        using var _ = harness;
        CurrentTestWindow = window;

        var product = TreeNodes.FindById(vm.InstallationNodes, productId)!;
        var localityB = TreeNodes.FindById(vm.InstallationNodes, locB)!;
        var currentParent = TreeNodes.FindById(vm.InstallationNodes, locA)!;

        var overLegal = window.DragOverEffect(product, localityB);
        var overIllegal = window.DragOverEffect(product, currentParent);

        Assert.Multiple(() =>
        {
            Assert.That(overLegal, Is.EqualTo(DragDropEffects.Move), "a legal locality highlights as a Move");
            Assert.That(overIllegal, Is.EqualTo(DragDropEffects.None), "the current-parent locality (already there) is not a legal target");
        });
    }
}
