using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>US-053: delete any project node — an unreferenced leaf silently, a referenced node with a
/// confirm-and-cascade (link halves + program rows), reversible as one undo step; structural nodes refused.</summary>
public class DeletionTests
{
    private static ElementId? FindTagged(IEnumerable<ProjectElement> roots, string tag)
    {
        foreach (var e in roots)
        {
            if (e.Tag == tag && e.Id is { } id)
                return id;
            if (FindTagged(e.Children, tag) is { } found)
                return found;
        }
        return null;
    }

    [Test]
    public async Task Delete_UnreferencedProduct_RemovesWithoutConfirm()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        var productId = harness.Session.Current!.FindById(loc)!.Children.First(c => c.Tag.StartsWith("product_")).Id!.Value;
        harness.Dialogs.ConfirmResult = false;   // must NOT be consulted for an unreferenced node

        var ok = await harness.Session.DeleteNodeAsync(productId);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, "an unreferenced product deletes without confirmation");
            Assert.That(harness.Session.Current!.FindById(productId), Is.Null);
        });
    }

    // W2-13: the confirm lives in the GUI (Preview→confirm→Apply), so this drives the real Delete command.
    // Deleting a REFERENCED product does not ask — measured against IHC Visual (uxparity S-15), which deletes it
    // silently and produces a byte-identical file; the prompt is reserved for what a node CONTAINS (a locality with
    // products still asks, see Delete_LocalityWithProducts_*). The cascade still happens and one undo reverses it.
    [Test]
    public async Task Delete_LinkedProduct_CascadesWithoutPromptAndIsUndoableAsOneStep()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var productPin = vm.InstallationNodes[0].Children[0].Children[0].Children[0];
        var blockPin = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0];
        await harness.Session.LinkPinsAsync(productPin.ElementId!.Value, blockPin.ElementId!.Value);
        var productId = harness.Session.Current!.FindById(loc)!.Children.First(c => c.Tag.StartsWith("product_")).Id!.Value;

        // No prompt: the product and the reciprocal link half on the block pin go together straight away.
        await vm.DeleteCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, productId));
        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.ConfirmCalls, Is.Zero, "a referenced product deletes without asking");
            Assert.That(harness.Session.Current!.FindById(productId), Is.Null, "the product is removed");
            Assert.That(harness.Session.Current!.FindById(blockPin.ElementId!.Value)!.Children
                .Any(c => c.Tag is "link_from_resource" or "link_to_resource"), Is.False, "the block's link half cascades away");
        });

        // One undo restores the product AND the link — a single step.
        await harness.Session.UndoAsync();
        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.FindById(productId), Is.Not.Null, "undo restores the product");
            Assert.That(harness.Session.Current!.FindById(blockPin.ElementId!.Value)!.Children
                .Any(c => c.Tag is "link_from_resource" or "link_to_resource"), Is.True, "and the link — one step");
        });
    }

    [Test]
    public async Task Delete_StructuralContainer_IsRefused()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var block = harness.Session.Current!.FindById(loc)!.Children.First(c => c.Tag == "functionblock");
        var inputsSection = block.FindChild("inputs")!.Id!.Value;

        var ok = await harness.Session.DeleteNodeAsync(inputsSection);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False, "a structural section container cannot be deleted");
            Assert.That(harness.Session.Current!.FindById(inputsSection), Is.Not.Null);
        });
    }

    // US-053/US-044: the Delete command deletes any node, reachable via the same command all three routes call.
    [Test]
    public async Task DeleteCommand_DeletesAnUnusedVariable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsId = vm.InstallationNodes[0].Children[2].ElementId!.Value;
        var varId = (await harness.Session.AddVariableAsync(settingsId, "resource_flag", "Away"))!.Value;

        await vm.DeleteCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, varId));

        Assert.That(harness.Session.Current!.FindById(varId), Is.Null, "the unused variable is deleted");
    }
}
