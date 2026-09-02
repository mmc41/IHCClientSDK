using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>US-054 (UI face): the Cut/Paste clipboard route performs a move and it is undoable. The move
/// <b>semantics</b> — re-parent with id/link preservation (byte-matched to the engine), and refusal of an illegal
/// or current-parent target — now live in <c>safe_project_tests.StructureCommandTests</c> (W2-16).</summary>
public class MoveTests
{
    // US-054: the Cut/Paste route performs the same move as a drag, and it is undoable.
    [Test]
    public async Task CutPaste_MovesProduct_AndIsUndoable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var productId = harness.Session.Current!.FindById(locA)!.Children.First(c => c.Tag.StartsWith("product_", StringComparison.Ordinal)).Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;

        vm.CutCommand.Execute(TreeNodes.FindById(vm.InstallationNodes, productId));
        await vm.PasteCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, locB));
        Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locB), "cut+paste moves the product");

        await harness.Session.UndoAsync();
        Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locA), "the move is undoable");
    }
}
