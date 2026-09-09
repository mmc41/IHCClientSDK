using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
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
    // an operand, and returns the resulting arithmetic menu headers beside the SDK offers they were built from.
    private static async Task<(ShellHarness harness, IReadOnlyList<string> operators,
        IReadOnlyList<ProgramOperationOffer> offers)>
        ArithmeticMenuFor(string targetTag, string targetName)
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children[3].ElementId!.Value;   // Internal variables

        // One operand of each numeric type, so every grid cell has a candidate to offer.
        await harness.Session.AddVariableAsync(section, "resource_floating_point", "Flt");
        await harness.Session.AddVariableAsync(section, "resource_integer", "Int");
        await harness.Session.AddVariableAsync(section, "resource_counter", "Cnt");
        await harness.Session.AddVariableAsync(section, targetTag, targetName);

        TreeNodeViewModel target = TreeNodes.FindPin(vm.InstallationNodes, targetName)!;
        TreeNodeViewModel commands = TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsCommandsContainer)!;
        vm.UseInProgramCommand.Execute(target);
        vm.SelectNode(commands);
        return (harness,
            vm.ProgramArithmeticMenu.Select(m => m.Header).ToList(),
            harness.Session.GetProgramOperations(commands.ElementId!.Value, target.ElementId!.Value));
    }

    // The operator symbols the SDK OFFERS for this target — asked of the facade, not re-derived from the method
    // tables, which are the engine's own. What this fixture still checks is that the menu renders exactly what was
    // offered, neither dropping a live operator nor inventing a dead one; which cells are live at all is pinned
    // SDK-side, where the grid lives.
    private static string[] LiveOperatorsFor(IReadOnlyList<ProgramOperationOffer> offers) =>
        [.. offers.Where(o => o.Kind == ProgramOperationKind.Arithmetic && o.NeedsOperand)
            .Select(o => o.OperatorSymbol!)];

    [Test]
    public async Task EachNumericTarget_RendersExactlyTheOperatorsTheSdkOffers()
    {
        foreach ((string tag, string name) in new[]
                 {
                     ("resource_floating_point", "Kommatal"),
                     ("resource_integer", "Tal"),
                     ("resource_counter", "Tæller"),
                 })
        {
            var (harness, operators, offers) = await ArithmeticMenuFor(tag, name);
            using var _1 = harness;

            string[] expected = LiveOperatorsFor(offers);
            // The two-operand categories are labelled "<name> <op>= …"; the counter's one-operand steps are "<name> ± 1".
            var offered = operators.Where(h => h.Contains("= …")).Select(h => h[(name.Length + 1)..(name.Length + 2)]).ToList();

            // Both sides are DERIVED, so an extraction bug that produced two empty lists would satisfy
            // Is.EquivalentTo. Assert each side is real before comparing them.
            Assert.That(expected, Is.Not.Empty, $"{tag}: the grid really has live cells for this target");
            Assert.That(offered, Is.Not.Empty, $"{tag}: the operator symbols were parsed out of the menu headers");
            Assert.That(offered, Is.EquivalentTo(expected),
                $"{tag}: the menu renders exactly the operators the SDK offered ({string.Join(" ", expected)}); "
                + "that those ARE the grid's live cells is pinned SDK-side by "
                + "ProgramAuthoringQueryTests.ArithmeticOffersAreExactlyTheGridsLiveCells");
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
            var (harness, operators, offers) = await ArithmeticMenuFor(tag, name);
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
