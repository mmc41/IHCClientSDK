using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>
/// Deleting a product that other logic references happens without a prompt, as in IHC Visual
/// (uxparity S-15): the references cascade away with it and the result is identical either way, so the
/// extra question was friction the reference tool does not impose. A locality holding content still
/// asks (S-09) — the vendor's prompt is about what a node CONTAINS, not what points at it.
/// </summary>
public class DeleteProductParityTests
{
    [Test]
    public async Task DeleteReferencedProduct_DoesNotPrompt_AndCascades()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId localityId = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(localityId, product.ProductIdentifier);
        await harness.Session.AddEmptyFunctionBlockAsync(localityId);
        ProjectElement block = harness.Session.Current!.FindById(localityId)!
            .Children.First(c => c.Tag == "functionblock");
        ElementId inPin = (await harness.Session.AddVariableAsync(
            block.FindChild("inputs")!.Id!.Value, "resource_input", "InA"))!.Value;
        ProjectElement placed = harness.Session.Current!.FindById(localityId)!
            .Children.First(c => c.Tag != "functionblock");
        ElementId productId = placed.Id!.Value;
        await harness.Session.LinkPinsAsync(placed.Descendants().First(d => d.Tag == "dataline_input").Id!.Value, inPin);
        int confirmsBefore = harness.Dialogs.ConfirmCalls;

        await vm.DeleteCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, productId)!);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.ConfirmCalls, Is.EqualTo(confirmsBefore), "no prompt for a referenced product");
            Assert.That(harness.Session.Current!.FindById(productId), Is.Null, "the product is gone");
            Assert.That(harness.Session.Current!.FindById(inPin)!.DescendantsAndSelf().Any(e => e.IsLinkHalf), Is.False,
                "and the link half that pointed at it went with it");
        });
    }
}
