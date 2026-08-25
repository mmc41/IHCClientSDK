using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-20: the ORDER of a block section's programming-mode flyout.
///
/// <para>Measured 2026-08-11 on an unlocked block, all four of its sections:</para>
/// <list type="table">
/// <item><term><c>Input</c></term><description><c>Indgang</c> FIRST and alone, then the value types</description></item>
/// <item><term><c>Output</c></term><description><c>Udgang</c> first and alone, then the value types (plus
/// <c>Scenarie</c>, OpenVisual's registered omission)</description></item>
/// <item><term><c>Indstillinger</c></term><description>value types only — no leading entry</description></item>
/// <item><term><c>Interne variable</c></term><description>value types only — no leading entry</description></item>
/// </list>
///
/// <para>So the rule needs no section→type table: whichever SIGNAL type the section accepts leads the list, and a
/// section that accepts none simply has no leading entry. OpenVisual sorted the signal type inline among the value
/// types instead (<c>Indgang</c> between <c>Helligdag</c> and <c>Kommatal</c>).</para>
///
/// <para><b>The collation is deliberately NOT the vendor's.</b> The vendor lists <c>Tæller</c> before <c>Tal</c> —
/// impossible under da-DK, which sorts Æ after Z, and exactly what folding æ to "ae" gives; re-measured 2026-08-11
/// against its full observed sequence, which an invariant comparer reproduces and da-DK and ordinal do not. Sorting
/// it correctly instead is a REGISTERED deliberate difference (product.md, alignment F-26), so these tests pin the
/// Danish order and the <c>Tal</c>-before-<c>Tæller</c> cell that expresses it. The separators the vendor draws are
/// registered too (F-27); this flyout carries none by decision, not by omission.</para>
/// </summary>
public class SectionFlyoutOrderParityTests : AvaloniaTestBase
{
    /// <summary>An unlocked block in PROGRAMMING mode — the mode whose flyout carries the palette at all (F-16).</summary>
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> ProgrammingModeAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        return (harness, vm);
    }

    private static List<string> FlyoutOn(MainWindowViewModel vm, string sectionKind)
    {
        TreeNodeViewModel section = vm.InstallationNodes[0].Children.Single(n => n.NodeKind == sectionKind);
        vm.SelectNode(section);
        return vm.SectionFlyoutItems.Select(i => i.Header).ToList();
    }

    /// <summary>Danish collation, spelled out here rather than imported so that swapping the production comparer
    /// for one which merely happens to agree on today's labels still fails.</summary>
    private static readonly System.StringComparer RegisteredOrder =
        System.StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), ignoreCase: true);

    /// <summary>
    /// The precondition every ordering assertion in this file rests on, and the one thing they cannot check
    /// themselves: that <c>da-DK</c> really resolves to Danish collation.
    /// <para>Both <see cref="RegisteredOrder"/> above and the production <see cref="DisplayOrder.Danish"/> are
    /// <c>StringComparer.Create(da-DK)</c>. Make the host globalization-invariant — an
    /// <c>InvariantGlobalization=true</c> anywhere in the build, or an ICU-less container image — and
    /// <c>GetCultureInfo("da-DK")</c> degrades to invariant, so BOTH degrade together and every comparison in this
    /// file keeps agreeing with itself while the shipped app orders æ/ø/å by code point. Neither ordering test can
    /// see that, because each compares a production order against a comparer that broke the same way.</para>
    /// <para>Pinned on the two facts an ordinal or invariant comparer cannot reproduce. Measured identical on
    /// Windows 11 and Ubuntu 24.04 (2026-08-17) — .NET has used ICU on Windows too since .NET 5, so this is a
    /// configuration tripwire, not a per-OS difference.</para>
    /// </summary>
    [Test]
    public void DanishCollation_IsReallyDanish_NotSilentlyInvariant()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DisplayOrder.Danish.Compare("Aarhus", "Zebra"), Is.GreaterThan(0),
                "Danish collates 'aa' as 'å', which sorts after z — ordinal and invariant both put Aarhus first");
            Assert.That(DisplayOrder.Danish.Compare("Kaal", "Kål"), Is.Zero,
                "and treats the two spellings of the same word as equal");
        });
    }

    private static void AssertValueTypesAreInRegisteredOrder(List<string> types)
    {
        Assert.That(types, Is.EqualTo(types.OrderBy(t => t, RegisteredOrder).ToList()),
            "the value types are in correct Danish collation");
        // The one pair in this catalog where the registered difference is visible: the vendor puts Tæller first.
        // Asserted by name so that "fixing" this list towards the vendor — as a later alignment turn nearly did —
        // fails loudly here instead of silently retiring a documented enhancement.
        if (types.Contains("Tæller") && types.Contains("Tal"))
        {
            Assert.That(types.IndexOf("Tal"), Is.LessThan(types.IndexOf("Tæller")),
                "æ sorts after z in Danish, so Tal precedes Tæller — the registered F-26 difference from the "
                + "vendor, which folds æ to 'ae' and lists them the other way round");
        }
    }

    [TestCase("section:inputs", "Indgang")]
    [TestCase("section:outputs", "Udgang")]
    public async Task SignalSection_LeadsWithItsOwnType_ThenTheValueTypes(string sectionKind, string signalType)
    {
        var (harness, vm) = await ProgrammingModeAsync();
        using var _ = harness;

        List<string> headers = FlyoutOn(vm, sectionKind);

        Assert.Multiple(() =>
        {
            Assert.That(headers.First(), Is.EqualTo(signalType),
                $"the vendor leads a {sectionKind} flyout with {signalType}, set off from the value types");
            Assert.That(headers.Last(), Is.EqualTo("Egenskaber…"), "Egenskaber closes the flyout");
            AssertValueTypesAreInRegisteredOrder(headers.Skip(1).Take(headers.Count - 2).ToList());
        });
    }

    /// <summary>
    /// The registered F-27 difference: this flyout draws <b>no separators</b>, where the reference application sets
    /// its leading signal type off with a thin rule, draws another before <i>Egenskaber</i>, and a third under
    /// <i>Ny type…</i> in the <c>Enum</c> submenu. Its members and their order are otherwise the same, so the
    /// difference is exactly this — every row is an operable member, and no row is a rule.
    /// <para>Asserted over the whole flyout tree, so the <c>Enum</c> submenu is covered without being named: the
    /// third of the reference application's rules is in there, and hardcoding the submenu's label would stop
    /// checking it the day the label changed.</para>
    /// </summary>
    [TestCase("section:inputs")]
    [TestCase("section:outputs")]
    [TestCase("section:settings")]
    [TestCase("section:internalsettings")]
    public async Task SectionFlyout_DrawsNoSeparators(string sectionKind)
    {
        var (harness, vm) = await ProgrammingModeAsync();
        using var _ = harness;

        FlyoutOn(vm, sectionKind);

        Assert.Multiple(() =>
        {
            foreach (ProductMenuItemViewModel item in Descend(vm.SectionFlyoutItems))
            {
                Assert.That(item.Header, Is.Not.Empty,
                    "a headerless row is how a separator would appear here — this flyout carries none by decision");
                Assert.That(item.Command is not null || item.Children.Count > 0, Is.True,
                    $"'{item.Header}' does nothing and opens nothing, so it is a rule rather than a member");
            }
        });
    }

    private static IEnumerable<ProductMenuItemViewModel> Descend(IEnumerable<ProductMenuItemViewModel> items)
    {
        foreach (ProductMenuItemViewModel item in items)
        {
            yield return item;
            foreach (ProductMenuItemViewModel child in Descend(item.Children))
                yield return child;
        }
    }

    [TestCase("section:settings")]
    [TestCase("section:internalsettings")]
    public async Task ValueSection_HasNoLeadingEntry(string sectionKind)
    {
        var (harness, vm) = await ProgrammingModeAsync();
        using var _ = harness;

        List<string> headers = FlyoutOn(vm, sectionKind);

        Assert.Multiple(() =>
        {
            Assert.That(headers, Does.Not.Contain("Indgang"), "a value section accepts no input signal");
            Assert.That(headers, Does.Not.Contain("Udgang"), "a value section accepts no output signal");
            Assert.That(headers.Last(), Is.EqualTo("Egenskaber…"), "Egenskaber closes the flyout");
            AssertValueTypesAreInRegisteredOrder(headers.Take(headers.Count - 1).ToList());
        });
    }
}
