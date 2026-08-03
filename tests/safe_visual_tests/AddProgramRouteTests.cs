using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W4 / F11 (uxparity2 T019): OpenVisual must offer a route to CREATE a program, so <i>Insert ▸ Program elements</i>
/// carries four entries rather than three. The missing piece was an SDK command (T018); this pins the GUI route on
/// top of it — the row exists, is gated to a block's programs container, and actually creates the element.
/// </summary>
public class AddProgramRouteTests : AvaloniaTestBase
{
    // An unlocked block in programming mode, with its "Programs" container node selected.
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, TreeNodeViewModel programs)> ProgrammingAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        TreeNodeViewModel programs = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.Programs)!;
        return (harness, vm, programs);
    }

    [Test]
    public async Task AddProgram_IsOfferedOnTheProgramsContainer_AndCreatesAProgram()
    {
        var (harness, vm, programs) = await ProgrammingAsync();
        using var _ = harness;
        vm.SelectNode(programs);

        int before = programs.Children.Count;
        Assert.That(vm.Registry.Bar["program.addProgram"].Enabled, Is.True,
            "Insert ▸ Program elements offers 'Program' on a block's programs container");

        await vm.AddProgramCommand.ExecuteAsync(null);

        TreeNodeViewModel refreshed = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.Programs)!;
        Assert.Multiple(() =>
        {
            Assert.That(refreshed.Children.Count, Is.EqualTo(before + 1), "a program row appeared in the tree");
            Assert.That(harness.Session.Current!.Root.Descendants().Count(e => e.Tag == "program_simple"),
                Is.EqualTo(before + 1), "…and the element exists in the project");
        });
    }

    // The gate must be the programs container specifically — not "anything in programming mode".
    [Test]
    public async Task AddProgram_IsGreyedAwayFromTheProgramsContainer()
    {
        var (harness, vm, _) = await ProgrammingAsync();
        using var _1 = harness;
        vm.SelectNode(TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsEventsContainer)!);

        Availability bar = vm.Registry.Bar["program.addProgram"];
        Assert.Multiple(() =>
        {
            Assert.That(bar.Enabled, Is.False, "an events container does not hold programs");
            Assert.That(bar.Reason, Is.Not.Null.And.Not.Empty, "…and the grey explains itself (QC-06)");
        });
    }

    // A-27: a locked library block is view-only, so it cannot gain a program either.
    [Test]
    public async Task AddProgram_IsGreyedInsideALockedBlock()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        Assert.That(vm.IsProgrammingBlockLocked, Is.True, "precondition: the block is locked");
        vm.SelectNode(TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.Programs)!);

        Assert.That(vm.Registry.Bar["program.addProgram"].Enabled, Is.False,
            "a locked block's program list cannot be extended");
    }
}
