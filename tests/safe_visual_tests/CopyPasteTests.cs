using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>US-056 (UI face): the Copy/Paste clipboard route duplicates, the copy is not consumed (paste again for
/// a second), and it is undoable. The copy <b>semantics</b> — independent duplicate with fresh ids (byte-matched to
/// the engine), external link halves dropped, and illegal targets refused — now live in
/// <c>safe_project_tests.StructureCommandTests</c> (W2-16).</summary>
public class CopyPasteTests
{
    private static IEnumerable<ProjectElement> ProductsUnder(ProjectElement group) =>
        group.Children.Where(c => c.Tag.StartsWith("product_", StringComparison.Ordinal));

    // US-056: the Copy/Paste route duplicates, the copy is not consumed (paste again for a second), and it is undoable.
    [Test]
    public async Task CopyPaste_Command_DuplicatesTwice_AndIsUndoable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var sourceId = ProductsUnder(harness.Session.Current!.FindById(locA)!).First().Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;

        vm.CopyCommand.Execute(TreeNodes.FindById(vm.InstallationNodes, sourceId));
        await vm.PasteCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, locB));
        await vm.PasteCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, locB));   // copy not consumed → paste again
        Assert.That(ProductsUnder(harness.Session.Current!.FindById(locB)!).Count(), Is.EqualTo(2), "two independent copies");

        await harness.Session.UndoAsync();
        Assert.That(ProductsUnder(harness.Session.Current!.FindById(locB)!).Count(), Is.EqualTo(1), "undo removes the last paste");
    }
}
