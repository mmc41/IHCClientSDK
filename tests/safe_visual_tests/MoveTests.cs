using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>US-054: moving a node to another container re-parents it with identity (ids) and links preserved;
/// illegal and self/no-op targets are refused; the move is reversible.</summary>
public class MoveTests
{
    private static TreeNodeViewModel? FindNode(IEnumerable<TreeNodeViewModel> nodes, ElementId id)
    {
        foreach (var n in nodes)
        {
            if (n.ElementId == id)
                return n;
            if (FindNode(n.Children, id) is { } found)
                return found;
        }
        return null;
    }

    [Test]
    public async Task Move_ProductToAnotherLocality_PreservesIdAndLinks()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(locA, block.MasterType);
        var productPin = vm.InstallationNodes[0].Children[0].Children[0].Children[0];
        var blockPin = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0];
        await harness.Session.LinkPinsAsync(productPin.ElementId!.Value, blockPin.ElementId!.Value);
        var productId = harness.Session.Current!.FindById(locA)!.ChildrenOrEmpty().First(c => c.Tag.StartsWith("product_")).Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;

        var ok = await harness.Session.MoveNodeAsync(productId, locB);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locB), "the product is re-parented under the target");
            Assert.That(harness.Session.Current!.FindById(productId), Is.Not.Null, "its id is unchanged (identity preserved)");
            Assert.That(harness.Session.Current!.FindById(blockPin.ElementId!.Value)!.ChildrenOrEmpty()
                .Any(c => c.Tag is "link_from_resource" or "link_to_resource"), Is.True, "its link survives the move");
        });
    }

    [Test]
    public async Task Move_IntoSameParent_IsRefusedAsNoOp()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var productId = harness.Session.Current!.FindById(locA)!.ChildrenOrEmpty().First(c => c.Tag.StartsWith("product_")).Id!.Value;

        Assert.That(await harness.Session.MoveNodeAsync(productId, locA), Is.False, "moving into the current parent is a no-op");
    }

    [Test]
    public async Task Move_IntoIllegalContainer_IsRefused()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var productId = harness.Session.Current!.FindById(locA)!.ChildrenOrEmpty().First(c => c.Tag.StartsWith("product_")).Id!.Value;

        // A product cannot be moved into itself (not a legal container).
        Assert.That(await harness.Session.MoveNodeAsync(productId, productId), Is.False);
    }

    // US-054: the Cut/Paste route performs the same move as a drag, and it is undoable.
    [Test]
    public async Task CutPaste_MovesProduct_AndIsUndoable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var productId = harness.Session.Current!.FindById(locA)!.ChildrenOrEmpty().First(c => c.Tag.StartsWith("product_")).Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;

        vm.CutCommand.Execute(FindNode(vm.InstallationNodes, productId));
        await vm.PasteCommand.ExecuteAsync(FindNode(vm.InstallationNodes, locB));
        Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locB), "cut+paste moves the product");

        await harness.Session.UndoAsync();
        Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locA), "the move is undoable");
    }
}
