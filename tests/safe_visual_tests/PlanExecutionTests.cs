using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>
/// A route CARRIED OUT: the plan the row promised, executed to the control the fix is made in.
///
/// <para>The worked example is the one the whole feature was specified around — a missing cable colour on a
/// terminal. The tree lands on the product, the product dialog opens with that terminal's row selected, the pin
/// editor stacks on top of it, and the caret is in Ledningsfarve. Four seams, one gesture.</para>
///
/// <para>Every case here asserts the destination the PLAN promised, not a destination written down twice: the
/// plan is what the row's tooltip was computed from, so a route that arrived somewhere else would be a row that
/// lied.</para>
/// </summary>
public class PlanExecutionTests
{
    private static async Task<(ShellHarness Harness, MainWindowViewModel Vm, ElementId Product, ElementId Pin)>
        ProductWithTerminalsAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ProductDefinition definition = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        ElementId product = (await harness.Session.AddProductAsync(
            vm.InstallationNodes[0].Children[0].ElementId!.Value, definition.ProductIdentifier))!.Value;
        ProjectElement pin = harness.Session.Current!.FindById(product)!.DescendantsAndSelf()
            .First(e => e.Kind == ElementKind.DatalinePin);
        return (harness, vm, product, pin.Id!.Value);
    }


    /// <summary>
    /// THE WORKED EXAMPLE. A missing cable colour on a terminal, activated in the panel, ends with the caret in
    /// the pin editor's Ledningsfarve box — having selected the product in the tree and the terminal's row in the
    /// product dialog on the way.
    /// </summary>
    [Test]
    public async Task ACableColourFindingLandsInTheLedningsfarveField()
    {
        var (harness, vm, product, pin) = await ProductWithTerminalsAsync();
        using var _ = harness;
        harness.Dialogs.PinPropertiesResult = null;   // the installer reads and cancels; nothing is edited

        await ProblemsTestData.ActivateAsync(harness, vm, pin, "cable_colour", "doc-cable-colour");

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedInstallationNode?.ElementId, Is.EqualTo(product),
                "the tree lands on the PRODUCT — the pin's row exists, but the product owns the dialog");
            Assert.That(harness.Dialogs.LastProductDialogOptions?.SelectTerminalPin, Is.EqualTo(pin.ToToken()),
                "the product dialog opens with that terminal's row selected");
            Assert.That(harness.Dialogs.SteppedInto.Select(a => a.Target), Does.Contain(pin),
                "and steps into it, as the installer pressing Konfigurer would");
            Assert.That(harness.Dialogs.LastPinPropertiesInput?.Focus, Is.EqualTo(PinDialogField.CableColour),
                "with the caret in Ledningsfarve — the field the finding is about");
        });
    }

    /// <summary>
    /// A finding about a field on the product itself opens that dialog focused on the field, with nothing stepped
    /// into: the value is on this dialog, so there is no sub-item to select.
    /// </summary>
    [Test]
    public async Task AProductFieldFindingOpensTheDialogFocusedOnThatField()
    {
        var (harness, vm, product, _) = await ProductWithTerminalsAsync();
        using var _h = harness;

        await ProblemsTestData.ActivateAsync(harness, vm, product, "position", "doc-position");

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedInstallationNode?.ElementId, Is.EqualTo(product));
            Assert.That(harness.Dialogs.LastProductDialogOptions?.FocusAutomationId,
                Does.StartWith("dlg."), "the dialog opens ON the field, not merely open");
            Assert.That(harness.Dialogs.SteppedInto, Is.Empty, "and steps into nothing");
        });
    }

    /// <summary>
    /// A route that promised only the DIALOG opens it and focuses nothing. This is the degradation working end to
    /// end: <c>product_identifier</c> is a real attribute the dialog renders read-only, and a route that focused
    /// it would put the caret in a box the installer cannot type in.
    /// </summary>
    [Test]
    public async Task ADialogLevelRouteOpensTheDialogWithoutClaimingAField()
    {
        var (harness, vm, product, _) = await ProductWithTerminalsAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, product, "product_identifier", new ProblemCode("migration"));
        Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog), "precondition: the planner degraded it");

        await vm.Problems.ActivateRowAsync(ProblemsTestData.RowFor(plan, product, "product_identifier", "migration"));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditProductDialogCalls, Is.EqualTo(1), "the dialog opened");
            Assert.That(harness.Dialogs.LastProductDialogOptions?.FocusAutomationId, Is.Null,
                "and named no field — the row never promised one");
        });
    }

    /// <summary>A tree-only route reveals and opens nothing at all.</summary>
    [Test]
    public async Task ATreeOnlyRouteOpensNoDialog()
    {
        var (harness, vm, _, pin) = await ProductWithTerminalsAsync();
        using var _h = harness;

        await ProblemsTestData.ActivateAsync(harness, vm, pin, null, "doc-not-linked");

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedInstallationNode?.ElementId, Is.EqualTo(pin),
                "the TERMINAL row, where the link gesture is made");
            Assert.That(harness.Dialogs.EditProductDialogCalls, Is.Zero,
                "and no modal stands between the installer and it");
        });
    }

    /// <summary>A route that leads nowhere opens nothing and says so.</summary>
    [Test]
    public async Task ARouteThatLeadsNowhereOpensNothing()
    {
        var (harness, vm, _, _) = await ProductWithTerminalsAsync();
        using var _h = harness;

        await ProblemsTestData.ActivateAsync(harness, vm, null, null, "doc-project-info-blank");

        Assert.That(harness.Dialogs.EditProductDialogCalls, Is.Zero);
    }
}
