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
/// T056 — the device-settings family reaching a field at last.
///
/// <para>Every rule in that family reports the PRODUCT, and that is right for the reader: a dimmer whose range
/// is inverted is a fact about that dimmer, and the product is the row the tree draws. But the repair is one
/// field of one <c>dimmer_setting_*</c> child the tree does not draw at all — so anchoring and repairing part
/// company, and no declared target could bridge it. The rule says the second half per occurrence instead.</para>
///
/// <para>This walks the real thing: a finding produced by the ENGINE, carrying the fix its rule emitted, through
/// the planner to a focused control. Building the fix location by hand here would have tested T054's mechanism
/// a second time and this family not at all.</para>
/// </summary>
public class DeviceSettingsRouteTests
{
    /// <summary>
    /// A dimmer whose minimum level is at its maximum — the <c>dev-dimmer-range-inverted</c> shape — on a
    /// product whose dialog actually composes those fields.
    ///
    /// <para>Both halves of that sentence were learned by measuring. The corpus already holds an inverted
    /// dimmer, but its product's preset composes no dimmer fields, so the route honestly stops at the dialog
    /// there; and the FADE-rate rules cannot be authored at all, because the field refuses a zero (200–60000).
    /// An equal pair of bounds is inside every bound the dialog enforces, so this one condition can be reached
    /// the way an installer would reach it.</para>
    /// </summary>
    private static async Task<(ShellHarness Harness, MainWindowViewModel Vm, ElementId Product,
        ValidationFinding Finding)> WithInvertedDimmerAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;

        // Whichever catalog product's dialog offers the two bound fields. Naming one would pin this fixture to
        // a catalog detail it does not care about — and picking a product that merely CARRIES the elements is
        // what this first got wrong, since only some presets compose them.
        ElementId product = default;
        foreach (ProductDefinition candidate in harness.ProjectService.GetAvailableProducts())
        {
            if (await harness.Session.AddProductAsync(locality, candidate.ProductIdentifier) is not { } placed)
                continue;
            if (Bound(harness, placed, "dimmer_setting_minimum_value") is not null
                && Bound(harness, placed, "dimmer_setting_maximum_value") is not null)
            {
                product = placed;
                break;
            }
            await harness.Session.DeleteNodeAsync(placed);
        }
        Assert.That(product, Is.Not.EqualTo(default(ElementId)),
            "precondition: some catalog product offers both dimmer bound fields");

        // Equal bounds: a range from 50 to 50 has no room to dim in, which is exactly what the row says.
        foreach (string tag in new[] { "dimmer_setting_minimum_value", "dimmer_setting_maximum_value" })
        {
            EditOutcome outcome = await harness.Session.ApplyAsync(
                harness.Session.Commands.ApplyProductDialogVisit(
                    harness.Session.Current!, product,
                    [new ProductDialogEdit(Bound(harness, product, tag)!.Value, "value", "50")], []));
            Assert.That(outcome.Status, Is.Not.EqualTo(EditStatus.Refused),
                $"precondition: storing 50 in {tag} — {outcome.Reason}");
        }

        ValidationFinding finding = harness.ProjectService.ValidateStructured(harness.Session.Current!)
            .First(f => f.Problem.Code.Value == "dev-dimmer-range-inverted"
                && f.Primary?.Element == product);
        return (harness, vm, product, finding);
    }

    /// <summary>The id of a setting element the product's dialog composes a value field for, or null.</summary>
    private static ElementId? Bound(ShellHarness harness, ElementId product, string tag)
    {
        Project project = harness.Session.Current!;
        return project.FindById(product)!.DescendantsAndSelf()
            .Where(e => e.Tag == tag && e.Id is not null)
            .Select(e => e.Id!.Value)
            .FirstOrDefault(id => harness.Session.GetProductDialog(project, product).AllFields
                .Any(f => f.Target == id && f.Attribute == "value")) is { } found && found != default
            ? found
            : null;
    }

    /// <summary>
    /// THE PAYOFF. The engine's own finding is anchored on the PRODUCT — which is where this whole family
    /// anchors — and its fix location takes the route on to the field the value is edited in.
    /// </summary>
    [Test]
    public async Task ADeviceSettingsFindingReachesTheFieldItIsRepairedIn()
    {
        (ShellHarness harness, _, ElementId product, ValidationFinding finding) = await WithInvertedDimmerAsync();
        using var _h = harness;
        ProblemNavigationPlanner planner = ProblemsTestData.Planner(harness);
        Project project = harness.Session.Current!;

        NavigationPlan asAnchored = planner.Plan(
            project, finding.Primary?.Element, finding.TargetAttribute, finding.Problem.Code);
        NavigationPlan asEmitted = planner.Plan(
            project, finding.Primary?.Element, finding.TargetAttribute, finding.Problem.Code, finding.Fix);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Fix, Is.Not.Null, "the rule said where this occurrence is repaired");
            Assert.That(project.FindById(finding.Fix!.Value.Element)?.Tag,
                Is.EqualTo("dimmer_setting_minimum_value"),
                "the MINIMUM: an inverted range is repaired by moving one bound, and the minimum is the one "
                + "that was raised past the other");
            Assert.That(finding.Fix?.Attribute, Is.EqualTo("value"),
                "and the attribute the setting stores its value in — the pair the dialog binds its field to");
            Assert.That(asAnchored.Kind, Is.EqualTo(NavigationKind.Dialog),
                "without the fix location the route can only reach the dialog: the product descriptor has no "
                + "field for the PRODUCT plus this attribute, which is the gap the family had");
            Assert.That(asEmitted.Kind, Is.EqualTo(NavigationKind.Field),
                "with it, the route reaches the control the value is edited in");
            Assert.That(asEmitted.Reveal, Is.EqualTo(product),
                "and the tree still lands on the product: the setting has no row of its own");
        });
    }

    /// <summary>Activating it opens the product's dialog on that field, with nothing stepped into.</summary>
    [Test]
    public async Task ActivatingItOpensTheProductDialogOnThatField()
    {
        // The view-model the FIXTURE built: creating a second one re-initializes the session onto a fresh
        // project, and the finding's element would no longer exist to route to.
        (ShellHarness harness, MainWindowViewModel vm, ElementId product, ValidationFinding finding) =
            await WithInvertedDimmerAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness).Plan(
            harness.Session.Current!, finding.Primary?.Element, finding.TargetAttribute,
            finding.Problem.Code, finding.Fix);
        await vm.Problems.ActivateRowAsync(new ProblemRowViewModel(
            finding, finding.Primary?.Element, "Dæmper", plan.Kind, "dev-dimmer-range-inverted@x"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field), "precondition: the row promised a field");
            Assert.That(harness.Dialogs.EditProductDialogCalls, Is.EqualTo(1));
            Assert.That(harness.Dialogs.LastProductDialogOptions?.FocusAutomationId, Does.StartWith("dlg."),
                "the dialog opened ON the field rather than merely open");
            Assert.That(harness.Dialogs.SteppedInto, Is.Empty,
                "and stepped into nothing: since US-015 was rewritten these are ordinary fields of this dialog");
        });
    }
}
