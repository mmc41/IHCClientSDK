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
/// Alignment F-41, third slice: an <b>Ugedag</b> (<c>resource_weekday</c>) has an editable initial value.
///
/// <para>Measured 2026-08-11: the reference application's <c>Rediger Ugedag egenskaber</c> offers a combo of
/// <c>Mandag, Tirsdag, Onsdag, Torsdag, Fredag, Lørdag, Søndag</c>, in that order. The DTD stores the choice as
/// an enumerated token, <c>inivalue (monday | … | sunday) "monday"</c>. OpenVisual had no editor for the type at
/// all.</para>
///
/// <para>The engine keeps the TOKEN and the app owns the Danish labels, so the file never depends on how a label
/// is spelled. Note the format's omit-if-default rule: <c>monday</c> is written as an ABSENT attribute
/// (<c>WeekdayInitialValueTests</c>), so an absent <c>inivalue</c> must read back as Mandag — not as "no value".</para>
/// </summary>
public class WeekdayDialogParityTests : AvaloniaTestBase
{
    private static readonly string[] VendorLabels =
        ["Mandag", "Tirsdag", "Onsdag", "Torsdag", "Fredag", "Lørdag", "Søndag"];

    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> WithWeekdayAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        ElementId variable = (await harness.Session.AddVariableAsync(section, "resource_weekday", "Dag"))!.Value;
        return (harness, vm, variable);
    }

    [Test]
    public async Task Dialog_OffersAChoiceInitialValue()
    {
        var (harness, vm, _) = await WithWeekdayAsync();
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Dag")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Kind,
            Is.EqualTo(ResourceValueKind.Choice), "a weekday's initial value is one of seven tokens");
    }

    /// <summary>An unset weekday reads as Mandag, because the format omits the default rather than storing it.</summary>
    [Test]
    public async Task AnUnsetWeekday_ReadsAsTheDefaultToken()
    {
        var (harness, vm, _) = await WithWeekdayAsync();
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Dag")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Token, Is.EqualTo("monday"),
            "an absent inivalue is the DTD default, not an empty value");
    }

    [Test]
    public async Task CommittingAChoice_PersistsTheToken()
    {
        var (harness, vm, variable) = await WithWeekdayAsync();
        using var _1 = harness;
        harness.Dialogs.VariablePropertiesResult = new VariablePropertiesResult(
            "Dag", string.Empty, ResourceInitialValue.OfChoice("thursday"), string.Empty);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Dag")!);

        Assert.That(harness.Session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.EqualTo("thursday"));
    }

    [Test]
    public async Task AStoredChoice_ReadsBack()
    {
        var (harness, vm, variable) = await WithWeekdayAsync();
        using var _1 = harness;
        await harness.Session.ApplyAsync(new SetResourceInitialValue(variable, ResourceInitialValue.OfChoice("saturday")));

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Dag")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Token, Is.EqualTo("saturday"));
    }

    /// <summary>The dialog shows the vendor's seven labels in the vendor's order, and returns the TOKEN behind the
    /// one picked — so the file carries <c>thursday</c> however "Torsdag" is spelled on screen.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheEditorListsTheVendorsSevenDays_AndYieldsTheirTokens()
    {
        var window = new VariablePropertiesWindow();
        CurrentTestWindow = window;
        window.Populate(new VariablePropertiesInput("Rediger Dag egenskaber", "Dag", "",
            ResourceInitialValue.OfChoice("wednesday")));

        var combo = window.FindControl<ComboBox>("ChoiceBox")!;
        Assert.Multiple(() =>
        {
            Assert.That(combo.IsVisible, Is.True, "a weekday's editor is the choice combo");
            Assert.That(combo.Items.Cast<object?>().Select(i => i?.ToString()), Is.EqualTo(VendorLabels),
                "the reference application's own day names, in its own order");
            Assert.That(combo.SelectedIndex, Is.EqualTo(2), "wednesday is the third day");
            Assert.That(window.ReadValue().Token, Is.EqualTo("wednesday"),
                "the token travels to the file, not the label");
        });
    }
}
