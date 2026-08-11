using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-21 (tmp/align-campaign-2026-08-10.md): the shape of the <c>Enum</c> type picker inside a block
/// section's flyout.
///
/// <para>The vendor's submenu is <c>Ny type...</c>, a rule, then the existing types. Two questions that shape
/// turned on were settled 2026-08-11 by creating a probe type in the vendor's own project:</para>
/// <list type="number">
/// <item>A type named <c>Aaa probe</c>, created AFTER both built-ins, appeared FIRST — so the list is
/// <b>sorted</b>, not appended in creation order. The two built-ins alone (<c>Logning</c>,
/// <c>Persienne tilstand</c>) could not tell those apart, being already in order.</item>
/// <item>It was <b>absent</b> from the submenu until a value was added, so the vendor offers only types that
/// have at least one value.</item>
/// </list>
///
/// <para>OpenVisual listed the existing types first in project order and put its create routes last. This test
/// reproduces the vendor's probe experiment: the probe type is created LAST and must still sort FIRST, so a
/// regression to project order fails here rather than hiding behind a fixture whose types happen to be in order.</para>
///
/// <para><c>Ny selvstændig type…</c> has no vendor counterpart and is a REGISTERED deliberate difference
/// (product.md, alignment F-21); it is kept, adjacent to the create route it belongs with.</para>
/// </summary>
public class EnumPickerParityTests : AvaloniaTestBase
{
    /// <summary>The probe type's name. Deliberately NOT the vendor experiment's literal "Aaa probe": Danish
    /// collation treats a leading "Aa" as "Å", which sorts AFTER Z, so that name sorts LAST here and would make a
    /// correctly-sorted list look like an appended one. (That is the registered F-26 difference showing up outside
    /// the Tal/Tæller pair — the vendor, folding differently, put "Aaa probe" first.) "Alfa probe" sorts first
    /// under both collations, so it isolates sorted-vs-appended, which is what this fixture is for.</summary>
    private const string ProbeType = "Alfa probe";

    /// <summary>An unlocked block in programming mode whose project holds a user enum type created AFTER the two
    /// built-ins — the OpenVisual counterpart of the vendor's probe.</summary>
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> WithProbeTypeAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        await harness.Session.AddEnumVariableAsync(section, "Probe", ProbeType, new[] { "Vaerdi 1" });
        Assert.That(harness.Session.Current!.GetEnumeratorTypes(), Does.Contain(ProbeType),
            "fixture precondition: the probe type must exist before the picker can be judged");
        return (harness, vm);
    }

    private static List<string> PickerOn(MainWindowViewModel vm)
    {
        TreeNodeViewModel section = vm.InstallationNodes[0].Children.Single(n => n.NodeKind == "section:internalsettings");
        vm.SelectNode(section);
        return vm.SectionFlyoutItems.Single(i => i.Header == "Enum").Children.Select(c => c.Header).ToList();
    }

    [Test]
    public async Task CreateRoutesComeFirst_ThenTheExistingTypesSorted()
    {
        var (harness, vm) = await WithProbeTypeAsync();
        using var _ = harness;

        List<string> picker = PickerOn(vm);

        Assert.Multiple(() =>
        {
            Assert.That(picker[0], Is.EqualTo("Ny type…"),
                "the vendor leads its enum picker with the create route, and names it 'Ny type...'");
            Assert.That(picker[1], Is.EqualTo("Ny selvstændig type…"),
                "OpenVisual's registered extra route sits with the create route it belongs to, not adrift at the end");
            Assert.That(picker.Skip(2).ToList(),
                Is.EqualTo(new[] { ProbeType, "Logning", "Persienne tilstand" }),
                "the existing types follow, SORTED — the probe was created last and must still come first");
        });
    }

    /// <summary>The discriminating half on its own: creation order and sorted order differ for this fixture, so a
    /// list that merely "looks ordered" cannot pass.</summary>
    [Test]
    public async Task ANewTypeIsSortedIntoPlace_NotAppended()
    {
        var (harness, vm) = await WithProbeTypeAsync();
        using var _ = harness;

        // Identified by EXCLUDING the create routes rather than by position, so this says something whichever end
        // of the picker they sit at — otherwise it passes trivially against the very arrangement it should reject.
        List<string> types = PickerOn(vm)
            .Where(h => h is not ("Ny type…" or "Ny…" or "Ny selvstændig type…")).ToList();

        Assert.That(types.IndexOf(ProbeType), Is.Zero,
            $"'{ProbeType}' was created after both built-ins; appending would put it last");
    }
}
