using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-22 (tmp/align-campaign-2026-08-10.md): inside programming mode the reference application offers
/// <i>Vis program</i> on <b>no</b> context flyout at all — you are already in the program.
///
/// <para>Measured 2026-08-11 on an unlocked block, every row type the mode exposes, in both panes:</para>
/// <list type="bullet">
/// <item>the block root, a section, and a section pin (installation pane) — none carry it;</item>
/// <item>all ten program-row types (functions pane) — the <c>Programmer</c> container, a program, an events
/// container, an event row, a commands container, a conditional command, a conditions group, a condition row, a
/// true-branch commands container and an action row — none carry it either.</item>
/// </list>
///
/// <para>OpenVisual offered it on <b>every</b> program row. This is the same rule the earlier F-13c note stated
/// ("reaching a section means you are already in the program") — right about programming mode and wrong about
/// configuration mode, where alignment F-16 established the vendor DOES offer it on a section. So the mode, not
/// the node kind, is what withdraws it; the F-16 fix corrected one node kind and left the rest.</para>
/// </summary>
public class ProgrammingModeShowProgramParityTests : AvaloniaTestBase
{
    private static IEnumerable<TreeNodeViewModel> Walk(IEnumerable<TreeNodeViewModel> roots)
    {
        foreach (TreeNodeViewModel n in roots)
        {
            yield return n;
            foreach (TreeNodeViewModel c in Walk(n.Children))
                yield return c;
        }
    }

    private static Availability ShowProgramOn(MainWindowViewModel vm, TreeNodeViewModel node)
    {
        vm.SelectNode(node);
        return vm.Registry.ContextMenu["view.showProgram"];
    }

    [Test]
    public async Task ProgrammingMode_NoRowOffersTheWayIn()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        // A program of its own, so the program-row kinds this is really about actually exist to be judged.
        vm.SelectNode(TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.Programs)!);
        await vm.AddProgramCommand.ExecuteAsync(null);

        List<TreeNodeViewModel> rows = Walk(vm.InstallationNodes).Concat(Walk(vm.FunctionNodes)).ToList();
        Assert.That(rows, Is.Not.Empty, "fixture precondition: programming mode realized some rows");

        List<string> offenders = rows
            .Where(n => ShowProgramOn(vm, n) != Availability.Hidden)
            .Select(n => $"{n.Kind}:{n.DisplayName}")
            .ToList();

        Assert.That(offenders, Is.Empty,
            "the program is already open, so the vendor withdraws 'Vis program' from every row in this mode");
    }

    /// <summary>The other half of the rule, so the fix cannot be "hide it everywhere": in CONFIGURATION mode the
    /// vendor offers it on a block, on a pin, and (F-16) on a section.</summary>
    [Test]
    public async Task ConfigurationMode_StillOffersItOnABlock()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        vm.Registry.Commands["program.leaveMode"].Execute(null);

        TreeNodeViewModel block = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.FunctionBlock)!;

        Assert.That(ShowProgramOn(vm, block), Is.Not.EqualTo(Availability.Hidden),
            "with no program open, a block is exactly where the way in belongs");
    }
}
