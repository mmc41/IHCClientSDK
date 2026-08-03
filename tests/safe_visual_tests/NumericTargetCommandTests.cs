using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W3 / F5 (uxparity2 T025): a NUMERIC variable is not a boolean, so its Add-command menu must not offer
/// <c>set to ON</c> / <c>set to OFF</c> / <c>set to NOT …</c>.
/// <para>
/// T008/T009 located the cause in the SDK rather than the GUI: <c>ClassifyPin</c> mapped <c>resource_integer</c> and
/// <c>resource_counter</c> to <see cref="Ihc.Vis.Programs.ProgramPinType.Bool"/> — the documented default — so
/// <c>CommandsFor</c> handed them the boolean list and the GUI rendered it faithfully. The fix is a numeric pin
/// family; no GUI-side filter could have done it.
/// </para>
/// </summary>
public class NumericTargetCommandTests : AvaloniaTestBase
{
    // Arms a variable of `tag` on the Commands container and returns the Add-command menu headers.
    private static async Task<(ShellHarness harness, System.Collections.Generic.IReadOnlyList<string> commands)>
        CommandMenuFor(string tag, string name)
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        ElementId section = vm.InstallationNodes[0].Children[3].ElementId!.Value;   // Internal variables
        await harness.Session.AddVariableAsync(section, tag, name);

        vm.UseInProgramCommand.Execute(
            TreeNodes.FindPin(vm.InstallationNodes, name)!);
        vm.SelectNode(TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        return (harness, vm.ProgramCommandMenu.Select(m => m.Header).ToList());
    }

    [Test]
    public async Task NumericTargets_OfferNoBooleanCommand()
    {
        foreach ((string tag, string name) in new[]
                 {
                     ("resource_integer", "Tal"),
                     ("resource_counter", "Tæller"),
                 })
        {
            var (harness, commands) = await CommandMenuFor(tag, name);
            using var _ = harness;

            Assert.That(commands.Where(h => h.Contains("ON") || h.Contains("OFF") || h.Contains("NOT")), Is.Empty,
                $"{tag}: a numeric register has no ON/OFF state to be set to — got [{string.Join(" | ", commands)}]");
        }
    }

    // The armed-detector: a genuine BOOL pin must still get the boolean commands, or the test above would pass
    // simply because the menu stopped being built at all.
    [Test]
    public async Task ABooleanPin_StillOffersItsBooleanCommands()
    {
        var (harness, commands) = await CommandMenuFor("resource_flag", "Flag");
        using var _ = harness;

        Assert.Multiple(() =>
        {
            Assert.That(commands, Is.Not.Empty, "a flag still has commands — the menu machinery works");
            Assert.That(commands.Any(h => h.Contains("ON")), Is.True, "…including setting it ON");
            Assert.That(commands.Any(h => h.Contains("OFF")), Is.True, "…and OFF");
        });
    }

    // A numeric target keeps its ARITHMETIC menu — that family is keyed on the numeric type set, orthogonal to the
    // pin-type family, so removing the boolean commands must not remove the arithmetic the target legitimately has.
    [Test]
    public async Task ANumericTarget_KeepsItsArithmeticMenu()
    {
        var harness = ShellHarness.Create();
        using var _ = harness;
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        ElementId section = vm.InstallationNodes[0].Children[3].ElementId!.Value;
        await harness.Session.AddVariableAsync(section, "resource_integer", "Tal");
        await harness.Session.AddVariableAsync(section, "resource_floating_point", "Kommatal");

        vm.UseInProgramCommand.Execute(TreeNodes.FindPin(vm.InstallationNodes, "Tal")!);
        vm.SelectNode(TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsCommandsContainer)!);

        Assert.That(vm.ProgramArithmeticMenu, Is.Not.Empty,
            "an integer target still reaches arithmetic — that menu is keyed on the numeric type set, not the pin family");
    }
}
