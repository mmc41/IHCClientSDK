using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-41, fourth slice: a <b>Dato</b> (<c>resource_date</c>) has an editable initial value.
///
/// <para>Measured 2026-08-11: the reference application's <c>Rediger Dato egenskaber</c> shows a picker reading
/// <c>01 January</c> — day and month, no year — and its tree row renders <c>Dato = 01:01</c>. OpenVisual had no
/// editor for the type at all.</para>
///
/// <para>The year is stored (<c>#REQUIRED</c>) but never offered, so an edit must leave it untouched;
/// <c>DateInitialValueTests</c> pins that on the engine side.</para>
/// </summary>
public class DateDialogParityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> WithDateAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        ElementId variable = (await harness.Session.AddVariableAsync(section, "resource_date", "Mærkedag"))!.Value;
        return (harness, vm, variable);
    }

    [Test]
    public async Task Dialog_OffersADateInitialValue()
    {
        var (harness, vm, _) = await WithDateAsync();
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Mærkedag")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Kind,
            Is.EqualTo(ResourceValueKind.Date), "a date's editable value is its day and month");
    }

    [Test]
    public async Task CommittingADate_PersistsDayAndMonth()
    {
        var (harness, vm, variable) = await WithDateAsync();
        using var _1 = harness;
        harness.Dialogs.VariablePropertiesResult = new VariablePropertiesResult(
            "Mærkedag", string.Empty, ResourceInitialValue.OfDate(24, 12), string.Empty);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Mærkedag")!);

        ProjectElement written = harness.Session.Current!.FindById(variable)!;
        Assert.Multiple(() =>
        {
            Assert.That(written.GetAttribute("day"), Is.EqualTo("24"));
            Assert.That(written.GetAttribute("month"), Is.EqualTo("12"));
        });
    }

    [Test]
    public async Task AStoredDate_ReadsBack()
    {
        var (harness, vm, variable) = await WithDateAsync();
        using var _1 = harness;
        await harness.Session.ApplyAsync(new SetResourceInitialValue(variable, ResourceInitialValue.OfDate(5, 9)));

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Mærkedag")!);

        ResourceInitialValue? shown = harness.Dialogs.LastVariablePropertiesInput?.Current;
        Assert.Multiple(() =>
        {
            Assert.That(shown?.Day, Is.EqualTo(5));
            Assert.That(shown?.Month, Is.EqualTo(9));
        });
    }

    /// <summary>The tree row must FOLLOW the value, not merely render a default — the F-43 lesson applied to the
    /// type being added, since a formatter that ignores its input looks correct on a fresh variable.</summary>
    [Test]
    public async Task TheTreeRowFollowsTheStoredDate()
    {
        var (harness, vm, variable) = await WithDateAsync();
        using var _1 = harness;

        await harness.Session.ApplyAsync(new SetResourceInitialValue(variable, ResourceInitialValue.OfDate(24, 12)));

        Assert.That(TreeNodes.FindPin(vm.InstallationNodes, "Mærkedag")!.DisplayName,
            Does.Contain("24:12"), "the reference application renders dd:MM and its row follows the value");
    }

    /// <summary>The editor shows day and month and yields them back — the year is never on screen.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheEditorOffersDayAndMonthOnly()
    {
        var window = new VariablePropertiesWindow();
        CurrentTestWindow = window;
        window.Populate(new VariablePropertiesInput("Rediger Mærkedag egenskaber", "Mærkedag", "",
            ResourceInitialValue.OfDate(24, 12)));

        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<StackPanel>("DatePanel")!.IsVisible, Is.True);
            Assert.That(window.FindControl<TextBox>("DayBox")!.Text, Is.EqualTo("24"));
            Assert.That(window.FindControl<TextBox>("MonthBox")!.Text, Is.EqualTo("12"));
            Assert.That(window.ReadValue().Day, Is.EqualTo(24));
            Assert.That(window.ReadValue().Month, Is.EqualTo(12));
        });
    }
}
