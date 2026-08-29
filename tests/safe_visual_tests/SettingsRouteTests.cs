using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

namespace safe_visual_tests;

/// <summary>
/// T047 — a finding about a product's configurable CONSTANT, now that the editor for one exists.
///
/// <para>This is the same defect the terminals had, in a second place. A setting is an <i>Indstillinger</i> grid
/// ROW, not a field of the product's composed descriptor, so asking that descriptor whether it offers
/// <c>inivalue</c> answered no and every settings route degraded to dialog-level — plausible, and one gesture
/// short of the value the finding was about.</para>
///
/// <para>The route now ends where the vendor's own gesture does: the product dialog, that row selected, the
/// <i>Rediger konstant</i> editor stacked on it with its value field focused. There is no tree fallback to
/// degrade to — the grid is the ONLY surface reaching these values, since a flagged setting has no tree row.</para>
/// </summary>
public class SettingsRouteTests
{
    private const string TemperatureSensor = "_0x2124";

    private static async Task<(ShellHarness Harness, MainWindowViewModel Vm, ElementId Product, ElementId Setting)>
        WithSensorAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        ElementId product = (await harness.Session.AddProductAsync(locality, TemperatureSensor))!.Value;
        ProjectElement setting = new ProductView(harness.Session.Current!,
            harness.Session.Current!.FindById(product)!).SettingElements.First();
        return (harness, vm, product, setting.Id!.Value);
    }


    /// <summary>The plan reaches the field, through the product that owns the dialog.</summary>
    [Test]
    public async Task ASettingsFindingReachesTheValueField()
    {
        (ShellHarness harness, _, ElementId product, ElementId setting) = await WithSensorAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, setting, "inivalue", new ProblemCode("dev-setting-default"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Reveal, Is.EqualTo(product),
                "the PRODUCT — a flagged setting has no tree row of its own");
            Assert.That(plan.Dialog?.Owner, Is.EqualTo(product), "which owns the dialog");
            Assert.That(plan.Dialog?.Site, Is.EqualTo(setting), "and the setting is the sub-item inside it");
            Assert.That(plan.Dialog?.Attribute, Is.EqualTo("inivalue"));
        });
    }

    /// <summary>
    /// An attribute the constant editor does not edit degrades. The editor has one field; a route that promised
    /// a second would be promising a control that does not exist.
    /// </summary>
    [Test]
    public async Task AnAttributeTheConstantEditorDoesNotEdit_DegradesToTheDialog()
    {
        (ShellHarness harness, _, _, ElementId setting) = await WithSensorAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, setting, "note", new ProblemCode("dev-setting-default"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog));
            Assert.That(plan.Dialog?.Attribute, Is.Null);
        });
    }

    /// <summary>
    /// THE ROUTE CARRIED OUT: the product dialog opens with that row selected and steps into it, so the editor
    /// is open on the constant the finding was about.
    /// </summary>
    [Test]
    public async Task ActivatingASettingsFinding_OpensTheConstantEditorOnThatRow()
    {
        (ShellHarness harness, MainWindowViewModel vm, ElementId product, ElementId setting) =
            await WithSensorAsync();
        using var _h = harness;
        harness.Dialogs.ConstantResult = null;   // read and dismissed; nothing is edited

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, setting, "inivalue", new ProblemCode("dev-setting-default"));
        await vm.Problems.ActivateRowAsync(new ProblemRowViewModel(
            new ValidationFinding(
                new Problem(new ProblemCode("dev-setting-default"), "Besked",
                    EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Warning, ValidationCategory.DeviceSettings,
                new FindingLocation(setting.ToToken(), setting, null),
                EquatableArray<FindingLocation>.Empty) { TargetAttribute = "inivalue" },
            setting, "Kalibrering", plan.Kind, "dev-setting-default@x"));

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedInstallationNode?.ElementId, Is.EqualTo(product),
                "the tree lands on the product");
            Assert.That(harness.Dialogs.LastProductDialogOptions?.SelectSettingId,
                Is.EqualTo(setting.ToToken()), "the dialog opens with that Indstillinger row selected");
            Assert.That(harness.Dialogs.LastProductDialogOptions?.SelectTerminalPin, Is.Null,
                "and NOT as a terminal — the two grids are different lists");
            Assert.That(harness.Dialogs.SteppedInto.Select(a => a.Kind),
                Does.Contain(DialogWidgetKind.SettingsGrid), "it steps into the settings grid");
            Assert.That(harness.Dialogs.EditConstantCalls, Is.EqualTo(1),
                "and the Rediger konstant editor is what opened");
            Assert.That(harness.Dialogs.LastConstantInput?.Setting, Is.EqualTo(setting),
                "on the constant the finding was about");
        });
    }

    /// <summary>
    /// A dialog-level settings route selects the row and steps into nothing — the same shape a terminal route of
    /// that depth has, so a finding whose fix is not the value still lands on the right row.
    /// </summary>
    [Test]
    public async Task ADialogLevelSettingsRoute_SelectsTheRowAndOpensNoEditor()
    {
        (ShellHarness harness, MainWindowViewModel vm, _, ElementId setting) = await WithSensorAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, setting, "note", new ProblemCode("dev-setting-default"));
        await vm.Problems.ActivateRowAsync(new ProblemRowViewModel(
            new ValidationFinding(
                new Problem(new ProblemCode("dev-setting-default"), "Besked",
                    EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Warning, ValidationCategory.DeviceSettings,
                new FindingLocation(setting.ToToken(), setting, null),
                EquatableArray<FindingLocation>.Empty) { TargetAttribute = "note" },
            setting, "Kalibrering", plan.Kind, "dev-setting-default@x"));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProductDialogOptions?.SelectSettingId,
                Is.EqualTo(setting.ToToken()));
            Assert.That(harness.Dialogs.EditConstantCalls, Is.Zero, "nothing was stepped into");
        });
    }
}
