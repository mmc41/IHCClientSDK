using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W3 / F3 (uxparity2 T023): invoking an arithmetic OPERAND adds a command row. F3 recorded the opposite — "returns
/// Ok and produces no row" — and T009's live re-measurement explained it: the arithmetic submenu's CATEGORY HEADER
/// (`Tal *= …`) carries no command at all, only its operand children do, so invoking the header is a permanent no-op.
/// <para>
/// Both halves are pinned here. The header being inert is not a bug to fix — it is the shape of a submenu — but it
/// IS the thing that made a working feature look broken, so it is asserted deliberately rather than left implicit.
/// </para>
/// </summary>
public class ArithmeticMenuAuthoringTests : AvaloniaTestBase
{
    // A block in programming mode with two numeric variables and an armed integer target on the Commands node.
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, TreeNodeViewModel commands)> ArmedAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId settings = vm.InstallationNodes[0].Children[3].ElementId!.Value;   // Internal variables
        await harness.Session.AddVariableAsync(settings, "resource_integer", "Tal");
        await harness.Session.AddVariableAsync(settings, "resource_floating_point", "Kommatal");

        TreeNodeViewModel target = TreeNodes.FindPin(vm.InstallationNodes, "Tal")!;
        TreeNodeViewModel commands = TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsCommandsContainer)!;
        vm.UseInProgramCommand.Execute(target);     // arm the arithmetic TARGET
        vm.SelectNode(commands);                    // …and select the container the menu hangs off
        return (harness, vm, commands);
    }

    [Test]
    public async Task InvokingAnArithmeticOperand_AddsACommandRow()
    {
        var (harness, vm, _) = await ArmedAsync();
        using var _1 = harness;

        ProductMenuItemViewModel category = vm.ProgramArithmeticMenu.First(m => m.Header.Contains("+="));
        ProductMenuItemViewModel operand = category.Children.First(c => c.Header == "Kommatal");
        int before = harness.Session.Current!.Root.Descendants().Count(e => e.Tag == "action");

        await ((IAsyncRelayCommand)operand.Command!).ExecuteAsync(null);

        Assert.That(harness.Session.Current!.Root.Descendants().Count(e => e.Tag == "action"),
            Is.EqualTo(before + 1), "invoking the operand leaf authors exactly one action row");
    }

    // F3's explanation, pinned: the category header has NO command, so it cannot author and cannot report failure.
    [Test]
    public async Task TheArithmeticCategoryHeader_CarriesNoCommand_SoItCannotAuthor()
    {
        var (harness, vm, _) = await ArmedAsync();
        using var _1 = harness;

        // Without this the loop below would pass vacuously on an empty menu — which is exactly the state a broken
        // arming step produces, and would turn this test into a detector of nothing.
        Assert.That(vm.ProgramArithmeticMenu, Is.Not.Empty, "the arithmetic menu really is populated");

        Assert.Multiple(() =>
        {
            foreach (ProductMenuItemViewModel category in vm.ProgramArithmeticMenu)
            {
                Assert.That(category.Command, Is.Null,
                    $"'{category.Header}' is a submenu header — invoking it is a no-op by construction (F3)");
                Assert.That(category.Children, Is.Not.Empty,
                    $"'{category.Header}' is only offered because it HAS authorable operands");
                foreach (ProductMenuItemViewModel operand in category.Children)
                    Assert.That(operand.Command, Is.Not.Null, $"…and every operand under it can author");
            }
        });
    }

    // Only the grid's live cells appear: an integer target offers + / * and never subtraction (T008).
    [Test]
    public async Task AnIntegerTarget_OffersOnlyTheGridsLiveOperators()
    {
        var (harness, vm, _) = await ArmedAsync();
        using var _1 = harness;

        var operators = vm.ProgramArithmeticMenu.Select(m => m.Header).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(operators.Any(h => h.Contains("+=")), Is.True, "int += is authorable");
            Assert.That(operators.Any(h => h.Contains("/=")), Is.True, "int /= is authorable");
            Assert.That(operators.Any(h => h.Contains("*=")), Is.True, "int *= is authorable with a float operand");
            Assert.That(operators.Any(h => h.Contains("-=")), Is.False,
                "subtraction has NO live cell for an integer target — the reference application's 4th entry is a dead popup entry (F4)");
        });
    }
}
