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
/// Alignment F-41, second slice: a <b>Timertid</b> (<c>resource_timertime</c>) has an editable initial value.
///
/// <para>Measured 2026-08-11 across all three time-family types in the reference application's dialogs:
/// <c>Tidspunkt</c> shows <c>00.00.00</c>, and <c>Timer</c> and <c>Timertid</c> both show
/// <c>00:00:00,000</c>. OpenVisual mapped the first two to its time editor and left <c>Timertid</c> at
/// <c>ResourceValueKind.None</c> — no editor at all — so its initial value could not be set.</para>
///
/// <para>This is the slice that needed no new control: the Time kind and its h/m/s/ms editor already existed,
/// and the engine's writer now sets milliseconds for both millisecond-carrying types
/// (<c>TimerTimeInitialValueTests</c>).</para>
/// </summary>
public class TimerTimeDialogParityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> WithTimerTimeAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        ElementId variable = (await harness.Session.AddVariableAsync(section, "resource_timertime", "Pulstid"))!.Value;
        return (harness, vm, variable);
    }

    [Test]
    public async Task Dialog_OffersATimeInitialValue()
    {
        var (harness, vm, _) = await WithTimerTimeAsync();
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Pulstid")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Kind,
            Is.EqualTo(ResourceValueKind.Time),
            "the vendor's Timertid dialog shows 00:00:00,000 — the same editor as a Timer");
    }

    [Test]
    public async Task CommittingTheInitialValue_PersistsAllFourFields()
    {
        var (harness, vm, variable) = await WithTimerTimeAsync();
        using var _1 = harness;
        harness.Dialogs.VariablePropertiesResult = new VariablePropertiesResult(
            "Pulstid", string.Empty, ResourceInitialValue.OfTime(1, 2, 3, 456), string.Empty);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Pulstid")!);

        ProjectElement written = harness.Session.Current!.FindById(variable)!;
        Assert.Multiple(() =>
        {
            Assert.That(written.GetAttribute("hour"), Is.EqualTo("1"));
            Assert.That(written.GetAttribute("minute"), Is.EqualTo("2"));
            Assert.That(written.GetAttribute("second"), Is.EqualTo("3"));
            Assert.That(written.GetAttribute("millisecond"), Is.EqualTo("456"),
                "the millisecond the dialog collects must not be dropped on the way to the file");
        });
    }

    [Test]
    public async Task Dialog_ShowsTheStoredInitialValue()
    {
        var (harness, vm, variable) = await WithTimerTimeAsync();
        using var _1 = harness;
        await harness.Session.ApplyAsync(new SetResourceInitialValue(variable, ResourceInitialValue.OfTime(4, 5, 6, 7)));

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Pulstid")!);

        ResourceInitialValue? shown = harness.Dialogs.LastVariablePropertiesInput?.Current;
        Assert.Multiple(() =>
        {
            Assert.That(shown?.Hour, Is.EqualTo(4));
            Assert.That(shown?.Minute, Is.EqualTo(5));
            Assert.That(shown?.Second, Is.EqualTo(6));
            Assert.That(shown?.Millisecond, Is.EqualTo(7), "the stored millisecond is read back, not reset to 0");
        });
    }
}
