using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W7 / F6 (uxparity2 T026): entering programming mode shows the block's variable sections OPEN — the block root and
/// all four sections expanded — so the installer sees the block's data without expanding four nodes by hand.
/// <para>
/// Measured (`tmp/uxparity2/verify/V6/findings.md`): the reference application opens this pane with the root and all
/// four sections <c>expanded=true</c> (87 nodes realized); OpenVisual opened it with the root collapsed and a single
/// node. Configuration mode is deliberately NOT changed — a tree of collapsed blocks is correct there.
/// </para>
/// </summary>
public class ProgrammingModeExpansionTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> ProgrammingModeAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        return (harness, vm);
    }

    [Test]
    public async Task EnteringProgrammingMode_OpensTheBlockRootAndAllFourSections()
    {
        var (harness, vm) = await ProgrammingModeAsync();
        using var _ = harness;

        TreeNodeViewModel root = vm.InstallationNodes[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.IsExpanded, Is.True, "the block root opens expanded");
            Assert.That(root.Children, Has.Count.EqualTo(4),
                "programming mode shows all four sections (Internal variables included, A-17)");
            foreach (TreeNodeViewModel section in root.Children)
                Assert.That(section.IsExpanded, Is.True, $"the '{section.DisplayName}' section opens expanded");
        });
    }

    [Test]
    public async Task EnterProgrammingMode_WithAuthoredProgram_CollapsesProgramRowsBelowOpenContainers()
    {
        var (harness, vm) = await ProgrammingModeAsync();
        using var _ = harness;
        TreeNodeViewModel programs = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.Programs)!;
        vm.SelectNode(programs);
        await vm.AddProgramCommand.ExecuteAsync(null);
        TreeNodeViewModel commands = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.Commands)!;
        await harness.Session.AddSubProgramAsync(commands.ElementId!.Value);
        vm.LeaveProgrammingModeCommand.Execute(null);
        TreeNodeViewModel block = TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsFunctionBlock)!;

        vm.EnterProgrammingModeCommand.Execute(block);

        TreeNodeViewModel programRoot = vm.FunctionNodes[0];
        TreeNodeViewModel programList = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.Programs)!;
        TreeNodeViewModel program = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.Program)!;
        TreeNodeViewModel subProgram = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.SubProgram)!;
        Assert.Multiple(() =>
        {
            Assert.That(programRoot.IsExpanded, Is.True, "the block root opens expanded");
            Assert.That(programList.IsExpanded, Is.True, "the Programmer container opens expanded");
            Assert.That(program.IsExpanded, Is.False, "an authored Program starts collapsed");
            Assert.That(subProgram.IsExpanded, Is.False, "an authored Under program starts collapsed");
        });
    }

    // The variables inside a section are visible without further expanding — the point of the change is that the
    // installer sees the block's data, not just four section headers.
    [Test]
    public async Task ExpandedSections_RevealTheirVariables()
    {
        var (harness, vm) = await ProgrammingModeAsync();
        using var _ = harness;
        ElementId inputs = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddVariableAsync(inputs, "resource_input", "Doorbell");

        TreeNodeViewModel inputSection = vm.InstallationNodes[0].Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(inputSection.IsExpanded, Is.True, "…and stays expanded after an edit re-projects the tree");
            Assert.That(inputSection.Children.Any(c => c.DisplayName == "Doorbell"), Is.True,
                "the variable is visible without expanding anything");
        });
    }

    // Configuration mode is unchanged: a locality's blocks stay collapsed, or the installation tree would explode
    // open on every project load.
    [Test]
    public async Task ConfigurationMode_LeavesBlocksCollapsed()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);

        TreeNodeViewModel block = TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsFunctionBlock)!;
        Assert.That(block.IsExpanded, Is.False,
            "a block in the configuration tree stays collapsed — only programming mode opens one up");
    }
}
