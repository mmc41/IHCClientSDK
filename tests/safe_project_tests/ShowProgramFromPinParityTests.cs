using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests;

/// <summary>
/// <i>Show program</i> is offered on a function block's PIN, not only on the block (uxparity S-28). The vendor's
/// context menu for `Input/Kip` is <c>Logmærke · Stoppunkt · Vis program · Egenskaber</c> — from a pin you can go
/// straight to the program that uses it, without first walking back up to the block.
/// </summary>
public class ShowProgramFromPinParityTests
{
    private static async Task<(ShellHarness Harness, ihc_openvisual.ViewModels.MainWindowViewModel Vm,
        ihc_openvisual.ViewModels.TreeNodeViewModel Pin)> BlockWithPinAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var pin = vm.FunctionNodes[0].Children[0].Children[0].Children
            .First(s => s.NodeKind == "section:inputs").Children.First(p => p.IsPin);
        return (harness, vm, pin);
    }

    [Test]
    public async Task ShowProgram_FromAnFbPin_EntersProgrammingModeForItsBlock()
    {
        (ShellHarness harness, var vm, var pin) = await BlockWithPinAsync();
        using var _ = harness;
        var block = vm.FunctionNodes[0].Children[0].Children[0];

        vm.EnterProgrammingModeCommand.Execute(pin);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsProgrammingMode, Is.True, "a pin opens the program of the block that owns it");
            Assert.That(vm.FunctionNodes[0].DisplayName, Is.EqualTo(block.DisplayName));
        });
    }

    /// <summary>A node that is not in a function block at all still does nothing.</summary>
    [Test]
    public async Task ShowProgram_FromALocality_DoesNothing()
    {
        (ShellHarness harness, var vm, _) = await BlockWithPinAsync();
        using var _h = harness;

        vm.EnterProgrammingModeCommand.Execute(vm.InstallationNodes[0].Children[1]);

        Assert.That(vm.IsProgrammingMode, Is.False);
    }
}
