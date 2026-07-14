using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>US-056: copy/paste produces an independent duplicate with fresh ids, leaves the original unchanged,
/// drops links whose other end is outside the copy, refuses illegal targets, and is undoable.</summary>
public class CopyPasteTests
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

    private static IEnumerable<ProjectElement> ProductsUnder(ProjectElement group) =>
        group.ChildrenOrEmpty().Where(c => c.Tag.StartsWith("product_"));

    [Test]
    public async Task Copy_Product_ToAnotherLocality_IsIndependentDuplicateWithFreshIds()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var sourceId = ProductsUnder(harness.Session.Current!.FindById(locA)!).First().Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;

        var newId = await harness.Session.CopyNodeAsync(sourceId, locB);

        Assert.Multiple(() =>
        {
            Assert.That(newId, Is.Not.Null.And.Not.EqualTo(sourceId), "the duplicate has a fresh id");
            Assert.That(harness.Session.Current!.FindById(sourceId), Is.Not.Null, "the original is left unchanged");
            Assert.That(ProductsUnder(harness.Session.Current!.FindById(locB)!).Count(), Is.EqualTo(1), "the copy lands under the target");
            Assert.That(ProductsUnder(harness.Session.Current!.FindById(locA)!).Count(), Is.EqualTo(1), "the source locality still has its product");
        });
    }

    [Test]
    public async Task Copy_DropsLinksWhoseOtherEndIsOutsideTheCopy()
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
        var sourceId = ProductsUnder(harness.Session.Current!.FindById(locA)!).First().Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;

        var newId = await harness.Session.CopyNodeAsync(sourceId, locB);

        var copy = harness.Session.Current!.FindById(newId!.Value)!;
        var original = harness.Session.Current!.FindById(sourceId)!;
        Assert.Multiple(() =>
        {
            Assert.That(copy.DescendantsAndSelf().Any(d => d.Tag is "link_from_resource" or "link_to_resource"), Is.False,
                "the copy starts unlinked — the external link is not carried over");
            Assert.That(original.DescendantsAndSelf().Any(d => d.Tag is "link_from_resource" or "link_to_resource"), Is.True,
                "the original keeps its link");
        });
    }

    [Test]
    public async Task Copy_IntoIllegalContainer_IsRefused()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var sourceId = ProductsUnder(harness.Session.Current!.FindById(locA)!).First().Id!.Value;

        Assert.That(await harness.Session.CopyNodeAsync(sourceId, sourceId), Is.Null, "a product is not a legal paste target");
    }

    // US-056: the Copy/Paste route duplicates, the copy is not consumed (paste again for a second), and it is undoable.
    [Test]
    public async Task CopyPaste_Command_DuplicatesTwice_AndIsUndoable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var sourceId = ProductsUnder(harness.Session.Current!.FindById(locA)!).First().Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;

        vm.CopyCommand.Execute(FindNode(vm.InstallationNodes, sourceId));
        await vm.PasteCommand.ExecuteAsync(FindNode(vm.InstallationNodes, locB));
        await vm.PasteCommand.ExecuteAsync(FindNode(vm.InstallationNodes, locB));   // copy not consumed → paste again
        Assert.That(ProductsUnder(harness.Session.Current!.FindById(locB)!).Count(), Is.EqualTo(2), "two independent copies");

        await harness.Session.UndoAsync();
        Assert.That(ProductsUnder(harness.Session.Current!.FindById(locB)!).Count(), Is.EqualTo(1), "undo removes the last paste");
    }
}
