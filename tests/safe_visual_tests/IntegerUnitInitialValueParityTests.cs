using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-41: the INTEGER-valued unit types — <c>resource_light</c> (Lys) and <c>resource_light_level</c>
/// (Lysniveau) — get their initial-value editor.
///
/// <para>The reference application uses two numeric controls: a whole-number field (id 217) for Tal, Tæller, Lys
/// and Lysniveau, and a decimal one (id 501) for the unit family. What settles which types are genuinely integers
/// is not that field, though, but the DTD default and the saved bytes: these two declare <c>inivalue CDATA "0"</c>
/// and the reference application stored <c>inivalue="42"</c> for a Lys (measured 2026-08-11).</para>
///
/// <para><b>W and Wh are deliberately absent.</b> Their field also shows a whole number, and turn 32 grouped them
/// here on that evidence — wrongly. They declare <c>"0.00"</c> and serialise as <c>43.00</c> / <c>7.00</c>, so they
/// belong to <see cref="DecimalDialogParityTests"/>. That is F-44, and it is why the dialog's appearance is not
/// evidence of the stored representation: only the bytes are.</para>
///
/// <para>The UNIT is never in the dialog: the field is the bare number and the unit belongs to the tree row
/// (<c>42 Lux</c>, <c>42%</c>).</para>
/// </summary>
public class IntegerUnitInitialValueParityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> WithVariableAsync(string tag)
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        ElementId variable = (await harness.Session.AddVariableAsync(section, tag, "Probe"))!.Value;
        return (harness, vm, variable);
    }

    [TestCase("resource_light")]
    [TestCase("resource_light_level")]
    public async Task Dialog_OffersANumberInitialValue(string tag)
    {
        var (harness, vm, _) = await WithVariableAsync(tag);
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Probe")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Kind,
            Is.EqualTo(ResourceValueKind.Number),
            $"{tag}: the reference application offers a whole-number field for this type");
    }

    /// <summary>A bare integer, with no fraction — the difference from the decimal family, whose types write
    /// <c>42.00</c> for the same value.</summary>
    [TestCase("resource_light")]
    [TestCase("resource_light_level")]
    public async Task CommittingAValue_PersistsItAsABareInteger(string tag)
    {
        var (harness, vm, variable) = await WithVariableAsync(tag);
        using var _1 = harness;
        harness.Dialogs.VariablePropertiesResult = new VariablePropertiesResult(
            "Probe", string.Empty, ResourceInitialValue.OfNumber(42), string.Empty);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Probe")!);

        Assert.That(harness.Session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.EqualTo("42"));
    }

    /// <summary>The row must FOLLOW the value — the F-43 lesson, applied per type as each is added. The unit
    /// belongs to the row, and each type spaces it differently (measured), so the expectations are per type
    /// rather than derived.</summary>
    [TestCase("resource_light", "42 Lux")]
    [TestCase("resource_light_level", "42%")]
    public async Task TheTreeRowFollowsTheStoredValue(string tag, string expected)
    {
        var (harness, vm, variable) = await WithVariableAsync(tag);
        using var _1 = harness;

        await harness.Session.ApplyAsync(new SetResourceInitialValue(variable, ResourceInitialValue.OfNumber(42)));

        Assert.That(TreeNodes.FindPin(vm.InstallationNodes, "Probe")!.DisplayName, Does.Contain(expected),
            $"{tag}: the row renders the stored value with its own unit spacing");
    }
}
