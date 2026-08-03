using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Programs;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W3 / F4 (uxparity2 T024): the arithmetic menu offers exactly the AUTHORABLE cells of the F-108 grid for the armed
/// target's type — no dead cell, and no boolean method.
/// <para>
/// T008 enumerated the grid by executing it: 15 live cells of 36. The operator set therefore differs BY TARGET TYPE —
/// float offers <c>+ - *</c>, integer <c>+ / *</c>, counter <c>+</c> plus its two one-operand steps — so the invariant
/// is per type. In particular this must NOT be written as "subtraction is never offered": subtraction is live on a
/// FLOAT target, and pinning its absence globally would forbid correct behaviour.
/// </para>
/// </summary>
public class ArithmeticOperatorSetTests : AvaloniaTestBase
{
    // Arms a variable of `targetTag` as the arithmetic target, with one variable of every numeric type available as
    // an operand, and returns the resulting arithmetic menu headers.
    private static async Task<(ShellHarness harness, IReadOnlyList<string> operators, MainWindowViewModel vm)>
        ArithmeticMenuFor(string targetTag, string targetName)
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        ElementId section = vm.InstallationNodes[0].Children[3].ElementId!.Value;   // Internal variables

        // One operand of each numeric type, so every grid cell has a candidate to offer.
        await harness.Session.AddVariableAsync(section, "resource_floating_point", "Flt");
        await harness.Session.AddVariableAsync(section, "resource_integer", "Int");
        await harness.Session.AddVariableAsync(section, "resource_counter", "Cnt");
        await harness.Session.AddVariableAsync(section, targetTag, targetName);

        vm.UseInProgramCommand.Execute(
            TreeNodes.FindPin(vm.InstallationNodes, targetName)!);
        vm.SelectNode(TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        return (harness, vm.ProgramArithmeticMenu.Select(m => m.Header).ToList(), vm);
    }

    // The operator symbols the SDK grid says are authorable for this target against ANY numeric operand — the same
    // derivation T008 ran, so the test cannot drift from the engine it is checking.
    private static string[] LiveOperatorsFor(string targetTag) =>
        new[] { "+", "-", "/", "*" }
            .Where(op => ProgramMethodCatalog.NumericVariableTags
                .Any(operand => ProgramMethodCatalog.ArithmeticToken(op, targetTag, operand) is not null))
            .ToArray();

    [Test]
    public async Task EachNumericTarget_OffersExactlyTheGridsLiveOperators()
    {
        foreach ((string tag, string name) in new[]
                 {
                     ("resource_floating_point", "Kommatal"),
                     ("resource_integer", "Tal"),
                     ("resource_counter", "Tæller"),
                 })
        {
            var (harness, operators, _) = await ArithmeticMenuFor(tag, name);
            using var _1 = harness;

            string[] expected = LiveOperatorsFor(tag);
            // The two-operand categories are labelled "<name> <op>= …"; the counter's one-operand steps are "<name> ± 1".
            var offered = operators.Where(h => h.Contains("= …")).Select(h => h[(name.Length + 1)..(name.Length + 2)]).ToList();

            // Both sides are DERIVED, so an extraction bug that produced two empty lists would satisfy
            // Is.EquivalentTo. Assert each side is real before comparing them.
            Assert.That(expected, Is.Not.Empty, $"{tag}: the grid really has live cells for this target");
            Assert.That(offered, Is.Not.Empty, $"{tag}: the operator symbols were parsed out of the menu headers");
            Assert.That(offered, Is.EquivalentTo(expected),
                $"{tag}: the menu offers exactly the grid's live operators ({string.Join(" ", expected)})");
        }
    }

    // The specific F4 claim, stated per type so it forbids only what is genuinely dead.
    [Test]
    public async Task Subtraction_IsOfferedOnAFloatTarget_AndNeverOnAnIntegerOrCounter()
    {
        var (floatHarness, floatOps, _) = await ArithmeticMenuFor("resource_floating_point", "Kommatal");
        using var _1 = floatHarness;
        var (intHarness, intOps, _) = await ArithmeticMenuFor("resource_integer", "Tal");
        using var _2 = intHarness;
        var (cntHarness, cntOps, _) = await ArithmeticMenuFor("resource_counter", "Tæller");
        using var _3 = cntHarness;

        Assert.Multiple(() =>
        {
            Assert.That(floatOps.Any(h => h.Contains("-= …")), Is.True,
                "a FLOAT target really does subtract (_0x64/_0x69) — F4 must not be pinned as a global absence");
            Assert.That(intOps.Any(h => h.Contains("-= …")), Is.False, "an integer target has no live subtraction cell");
            Assert.That(cntOps.Any(h => h.Contains("-= …")), Is.False, "nor does a counter");
            Assert.That(cntOps.Any(h => h.Contains("- 1")), Is.True,
                "…but a counter DOES decrement, through its one-operand step (_0x57)");
        });
    }

    // No boolean method ever reaches the ARITHMETIC menu, whatever the target type.
    [Test]
    public async Task NoBooleanMethod_IsOfferedInTheArithmeticMenu()
    {
        foreach ((string tag, string name) in new[]
                 {
                     ("resource_floating_point", "Kommatal"),
                     ("resource_integer", "Tal"),
                     ("resource_counter", "Tæller"),
                 })
        {
            var (harness, operators, _) = await ArithmeticMenuFor(tag, name);
            using var _1 = harness;

            Assert.That(operators, Is.Not.Empty, $"{tag}: the menu is populated (otherwise this proves nothing)");
            foreach (string header in operators)
            {
                Assert.That(header, Does.Not.Contain("ON").And.Not.Contain("OFF").And.Not.Contain("NOT"),
                    $"{tag}: '{header}' — arithmetic offers no boolean method");
            }
        }
    }
}
