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
/// Alignment F-41 (tmp/align-campaign-2026-08-10.md), first slice: a <b>Helligdag</b> variable has an editable
/// initial value.
///
/// <para>Censused 2026-08-11 across all 18 value types in the reference application's variable dialog: EVERY one
/// offers an initial-value editor, in one of four shapes — an enumerated combo (Ugedag, Flag, Helligdag), a
/// numeric box (Tal, Tæller, kW/kWh/W/Wh, Kommatal, Fugtighed, Lys, Lysniveau, Temperatur), a time picker
/// (Tidspunkt, Timer, Timertid) and a date picker (Dato). OpenVisual offered one for five types and
/// <c>ResourceValueKind.None</c> — no editor at all — for the other thirteen.</para>
///
/// <para><c>Helligdag</c> is the slice that needs no new editor: the vendor's combo holds exactly
/// <c>OFF</c>/<c>ON</c> (read from the live dialog), the DTD declares <c>inivalue (on | off)</c> on
/// <c>resource_holiday</c> just as it does on <c>resource_flag</c>, and the engine's bool writer is
/// tag-agnostic — so the type simply was not listed.</para>
///
/// <para>The other twelve need editors this dialog does not have yet (a weekday list, a date picker, decimals
/// with units) and are tracked as the rest of F-41.</para>
/// </summary>
public class HolidayInitialValueParityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> WithHolidayAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        ElementId variable = (await harness.Session.AddVariableAsync(section, "resource_holiday", "Ferie"))!.Value;
        return (harness, vm, variable);
    }

    [Test]
    public async Task Dialog_OffersABoolInitialValue()
    {
        var (harness, vm, _) = await WithHolidayAsync();
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Ferie")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Kind,
            Is.EqualTo(ResourceValueKind.Bool),
            "the vendor's Helligdag dialog offers an OFF/ON initial value, and the format declares inivalue (on|off)");
    }

    [Test]
    public async Task CommittingTheInitialValue_PersistsIt()
    {
        var (harness, vm, variable) = await WithHolidayAsync();
        using var _1 = harness;
        harness.Dialogs.VariablePropertiesResult =
            new VariablePropertiesResult("Ferie", string.Empty, ResourceInitialValue.OfBool(true), string.Empty);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Ferie")!);

        Assert.That(harness.Session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.EqualTo("on"),
            "the bool writer is tag-agnostic, so listing the type is all that was missing");
    }

    /// <summary>The pre-fill reads back too, so the dialog shows what the variable currently starts at rather
    /// than always OFF.</summary>
    [Test]
    public async Task Dialog_ShowsTheStoredInitialValue()
    {
        var (harness, vm, variable) = await WithHolidayAsync();
        using var _1 = harness;
        await harness.Session.ApplyAsync(new SetResourceInitialValue(variable, ResourceInitialValue.OfBool(true)));

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Ferie")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Bool, Is.True);
    }
}
