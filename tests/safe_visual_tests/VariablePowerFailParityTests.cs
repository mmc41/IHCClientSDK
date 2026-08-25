using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-27: the variable properties dialog carries the power-loss
/// <i>Gem aktuel værdi</i> setting, as the reference application's does.
///
/// <para>Measured 2026-08-11 on a <c>Flag</c> in a block's <i>Interne variable</i> section: the vendor's
/// <c>Rediger Flag egenskaber</c> has a <c>Ved strømsvigt</c> group holding <c>Gem aktuel værdi</c>. OpenVisual's
/// dialog had no such control and offered the setting only from the context flyout — so the two applications put
/// the same setting in different PLACES, which is what the long-open F-15 residual was really seeing.</para>
///
/// <para>It was also a gap against OpenVisual's own specification: product.md FR-7.2 promises "per-variable
/// name/note/initial value/<b>persist-on-power-loss</b> properties", and the dialog delivered the first three.</para>
///
/// <para>Scope comes from the FORMAT: the DTD declares <c>backup</c> on every variable type except
/// <c>resource_scene</c>, which is why the engine guard was widened to the same set rather than to "outputs plus
/// flags" (see <c>ValueBackupScopeTests</c>).</para>
/// </summary>
public class VariablePowerFailParityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> WithFlagAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        ElementId variable = (await harness.Session.AddVariableAsync(section, "resource_flag", "Away"))!.Value;
        return (harness, vm, variable);
    }

    /// <summary>The control must ANNOUNCE what ticking it does. Caught live: an <c>AutomationProperties.LabeledBy</c>
    /// pointing at the group heading replaced the checkbox's own name, so automation (and a screen reader) read it as
    /// "Ved strømsvigt" — the group — and the words "Gem aktuel værdi" were nowhere.</summary>
    [Avalonia.Headless.NUnit.AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void PowerFailCheckbox_IsNamedByItsOwnContent_NotByTheGroupHeading()
    {
        var window = new ihc_openvisual.Views.VariablePropertiesWindow();
        CurrentTestWindow = window;
        var box = window.FindControl<Avalonia.Controls.CheckBox>("SaveOnPowerLossBox")!;

        Assert.Multiple(() =>
        {
            Assert.That(box.Content, Is.EqualTo("Gem aktuel værdi"), "the vendor's own words for the control");
            Assert.That(Avalonia.Automation.AutomationProperties.GetLabeledBy(box), Is.Null,
                "a checkbox is named by its content; a LabeledBy here would replace that with the group heading");
        });
    }

    [Test]
    public async Task Dialog_IsOfferedTheCurrentPowerFailSetting()
    {
        var (harness, vm, variable) = await WithFlagAsync();
        using var _ = harness;
        await harness.Session.SetOutputBackupAsync(variable, true);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Away")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.SaveOnPowerLoss, Is.True,
            "the dialog shows what the variable currently does at power loss, as the vendor's does");
    }

    [Test]
    public async Task CommittingTheSetting_PersistsIt()
    {
        var (harness, vm, variable) = await WithFlagAsync();
        using var _ = harness;
        harness.Dialogs.VariablePropertiesResult =
            new VariablePropertiesResult("Away", string.Empty, ResourceInitialValue.None, string.Empty,
                SaveOnPowerLoss: true);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Away")!);

        Assert.That(harness.Session.Current!.FindById(variable)!.GetAttribute("backup"), Is.EqualTo("yes"),
            "committing the dialog writes the power-loss flag through, not just the name and note");
    }

    /// <summary>The other direction, so the wiring is a two-way binding rather than a one-way switch-on.</summary>
    [Test]
    public async Task ClearingTheSetting_PersistsToo()
    {
        var (harness, vm, variable) = await WithFlagAsync();
        using var _ = harness;
        await harness.Session.SetOutputBackupAsync(variable, true);
        harness.Dialogs.VariablePropertiesResult =
            new VariablePropertiesResult("Away", string.Empty, ResourceInitialValue.None, string.Empty,
                SaveOnPowerLoss: false);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Away")!);

        Assert.That(harness.Session.Current!.FindById(variable)!.GetAttribute("backup"), Is.Not.EqualTo("yes"),
            "unticking it must turn the flag off again");
    }
}
