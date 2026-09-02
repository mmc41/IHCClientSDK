using System.Linq;
using System.Threading.Tasks;

namespace safe_visual_tests;

/// <summary>
/// F4 on a link row must land where IHC Visual lands (uxparity S-25). Measured live on
/// `Project1-SimpelWired.vis`, jumping from the link row under the product pin
/// `Stue / LK FUGA Tryk 2 tast / Tryk (venstre)`:
///
/// <list type="bullet">
/// <item>the vendor's caret ends on <c>Stue / LK FUGA Tryk 2 tast (Ved dør) / Tryk (venstre)</c> in the
/// Functions pane — the <b>reciprocal link row</b>, i.e. the other half of the same wire;</item>
/// <item>OpenVisual selected <c>Kip</c> — the pin that <i>owns</i> that row, one level up.</item>
/// </list>
///
/// <para>The difference is not cosmetic: the vendor leaves you standing on a link row, which is itself
/// F4-able and Delete-able, so the wire stays the thing you are working with. Landing on the pin ends
/// the navigation — the row you came for is a child you must then find.</para>
/// </summary>
public class LinkNavigationParityTests
{
    private static async Task<(ShellHarness Harness, ViewModelPair Pair)> WiredAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie", StringComparison.Ordinal) && p.Resources.Any(r => r.Tag == "dataline_input"));
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var productPin = vm.InstallationNodes[0].Children[0].Children[0].Children.First(c => c.NodeKind == "pin:dataline_input");
        var fbInput = vm.FunctionNodes[0].Children[0].Children[0].Children
            .First(s => s.NodeKind == "section:inputs").Children.First(p => p.IsPin);
        await harness.Session.LinkPinsAsync(productPin.ElementId!.Value, fbInput.ElementId!.Value);
        return (harness, new ViewModelPair(vm));
    }

    internal sealed record ViewModelPair(ihc_openvisual.ViewModels.MainWindowViewModel Vm);

    [Test]
    public async Task NavigateLinkOpposite_SelectsTheReciprocalLinkRow_NotItsPin()
    {
        (ShellHarness harness, ViewModelPair pair) = await WiredAsync();
        using var _ = harness;
        var vm = pair.Vm;
        var productLinkRow = vm.InstallationNodes[0].Children[0].Children[0].Children
            .First(c => c.NodeKind == "pin:dataline_input").Children.First(c => c.IsLinkRow);
        var fbPin = vm.FunctionNodes[0].Children[0].Children[0].Children
            .First(s => s.NodeKind == "section:inputs").Children.First(p => p.IsPin);
        var reciprocalRow = fbPin.Children.First(c => c.IsLinkRow);

        vm.NavigateLinkOppositeCommand.Execute(productLinkRow);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedFunctionsNode, Is.SameAs(reciprocalRow),
                "the jump lands on the other HALF of the wire, not on the pin that owns it");
            Assert.That(vm.SelectedFunctionsNode!.IsLinkRow, Is.True);
        });
    }

    /// <summary>And back again from the far half — the same rule in the other direction.</summary>
    [Test]
    public async Task NavigateLinkOpposite_FromTheFarHalf_SelectsTheNearLinkRow()
    {
        (ShellHarness harness, ViewModelPair pair) = await WiredAsync();
        using var _ = harness;
        var vm = pair.Vm;
        var productPin = vm.InstallationNodes[0].Children[0].Children[0].Children
            .First(c => c.NodeKind == "pin:dataline_input");
        var productLinkRow = productPin.Children.First(c => c.IsLinkRow);
        var reciprocalRow = vm.FunctionNodes[0].Children[0].Children[0].Children
            .First(s => s.NodeKind == "section:inputs").Children.First(p => p.IsPin)
            .Children.First(c => c.IsLinkRow);

        vm.NavigateLinkOppositeCommand.Execute(reciprocalRow);

        Assert.That(vm.SelectedInstallationNode, Is.SameAs(productLinkRow));
    }
}
