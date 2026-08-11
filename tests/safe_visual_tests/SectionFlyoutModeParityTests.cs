using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-16 (tmp/align-campaign-2026-08-10.md): a block SECTION's context flyout follows the MODE.
///
/// <para>Measured 2026-08-11 against the reference application on one unlocked block, the same section row read in
/// both modes:</para>
/// <list type="bullet">
/// <item><b>configuration mode</b> (the section reached in the Funktioner pane): <c>Vis program</c> — and nothing
/// else. No type list, no Egenskaber.</item>
/// <item><b>programming mode</b>: the section's own signal type, the value types with <c>Enum</c> among them, and
/// <c>Egenskaber…</c> — and no <c>Vis program</c>.</item>
/// </list>
///
/// <para>OpenVisual offered the type list plus <c>Egenskaber…</c> in BOTH modes and <c>Vis program</c> in neither.
/// The rule it encoded — "a section's flyout omits Vis program, because reaching a section means you are already in
/// the program" (F-13c) — was measured in programming mode and generalized. It is false of the other mode: a
/// section is reachable in the Funktioner pane with no program open at all, and that is exactly where the vendor
/// offers the way IN.</para>
///
/// <para>The locked case is unchanged and stays covered by <c>LockedBlockProgrammingStatusTests</c>: a locked block
/// in programming mode offers <c>Egenskaber…</c> alone, which the vendor confirms.</para>
/// </summary>
public class SectionFlyoutModeParityTests : AvaloniaTestBase
{
    /// <summary>An unlocked block whose <i>Input</i> section holds one variable, left in PROGRAMMING mode.
    /// The variable is what makes the section visible in configuration mode at all: a childless container is
    /// hidden there (A-18), so an empty block has no section row to right-click.</summary>
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> BlockWithAnInputAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        ElementId inputSection = vm.InstallationNodes[0].Children[0].ElementId!.Value;   // "Input"
        await harness.Session.AddVariableAsync(inputSection, "resource_input", "Doorbell");
        return (harness, vm);
    }

    /// <summary>The same block back in CONFIGURATION mode, returning its <i>Input</i> row as reached in the
    /// Funktioner pane — the state the vendor answers <c>Vis program</c> in.</summary>
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, TreeNodeViewModel section)>
        ConfigurationModeAsync()
    {
        var (harness, vm) = await BlockWithAnInputAsync();
        vm.Registry.Commands["program.leaveMode"].Execute(null);
        TreeNodeViewModel block = vm.FunctionNodes[0].Children[0].Children[0];
        return (harness, vm, block.Children[0]);
    }

    /// <summary>Show-program's gate resolves the OWNING block by walking the ancestry of the node's element id, so
    /// this must carry the section's REAL id: a synthetic one resolves to no block, the gate refuses, and the row
    /// reads Hidden for a reason that has nothing to do with the surface policy under test.</summary>
    private static Availability ShowProgramOn(MainWindowViewModel vm, ElementId sectionId, Surface surface)
    {
        var node = new NodeContext(sectionId, TreeNodeKind.Section,
            IsPin: false, IsProductTerminal: false, IsLinkRow: false, IsLinkTarget: false, IsLogMarkPin: false,
            IsOutputPin: false, IsEventsContainer: false, IsCommandsContainer: false,
            IsConditionsContainer: false, IsCaseNode: false, IsLockedBlock: false,
            CanCut: false, CanCopy: false, CanReorder: false);
        return CommandRegistry.For(vm.Registry.Rows.Single(r => r.Id == "view.showProgram"),
                                   vm.Context with { Node = node }, surface);
    }

    [Test]
    public async Task ConfigurationMode_SectionFlyout_OffersTheWayIntoTheProgramAndNothingElse()
    {
        var (harness, vm, section) = await ConfigurationModeAsync();
        using var _ = harness;

        vm.SelectNode(section);

        Assert.That(vm.SectionFlyoutItems.Select(i => i.Header), Is.EqualTo(new[] { "Vis program" }),
            "in configuration mode the vendor's section flyout is Vis program alone — no type list, no Egenskaber");
    }

    [Test]
    public async Task ConfigurationMode_SectionKeepsShowProgram()
    {
        var (harness, vm, section) = await ConfigurationModeAsync();
        using var _ = harness;

        vm.SelectNode(section);

        Assert.That(ShowProgramOn(vm, section.ElementId!.Value, Surface.ContextMenu),
            Is.Not.EqualTo(Availability.Hidden),
            "a section reached with no program open is precisely where the vendor offers the way in");
    }

    [Test]
    public async Task ProgrammingMode_SectionFlyout_KeepsTheTypeListAndDropsShowProgram()
    {
        var (harness, vm) = await BlockWithAnInputAsync();
        using var _ = harness;

        // In programming mode the block's sections hang off the INSTALLATION pane's root (the block itself).
        TreeNodeViewModel section = vm.InstallationNodes[0].Children[0];
        vm.SelectNode(section);

        Assert.Multiple(() =>
        {
            var headers = vm.SectionFlyoutItems.Select(i => i.Header).ToList();
            Assert.That(headers, Is.Not.Empty, "the palette is what a section offers once the program is open");
            Assert.That(headers[^1], Is.EqualTo("Egenskaber…"), "Egenskaber closes the programming-mode flyout");
            Assert.That(headers, Does.Not.Contain("Vis program"),
                "the program is already open, so the vendor drops the way in");
            Assert.That(ShowProgramOn(vm, section.ElementId!.Value, Surface.ContextMenu),
                Is.EqualTo(Availability.Hidden),
                "and the registry agrees with the flyout it feeds");
        });
    }
}
