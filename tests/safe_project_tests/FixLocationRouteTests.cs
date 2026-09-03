using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests;

/// <summary>
/// T054 — the per-occurrence FIX LOCATION, and what a host does with one.
///
/// <para>An entry's target is a statement about the ROW: every occurrence is about this attribute of this kind
/// of element. Two families cannot be described that way — one reads whichever attribute the schema rejected,
/// so it differs per occurrence; the other reports the product that owns a setting, so the element to repair is
/// a child of the one the reader is shown. Both can only say it per emission, and the planner prefers what the
/// emission said.</para>
///
/// <para>The pair of claims is what matters: the override WINS where it is present, and where it is absent
/// nothing changes at all. A mechanism that quietly altered the ordinary case would be a worse bargain than the
/// gap it closes.</para>
/// </summary>
public class FixLocationRouteTests
{
    private const string TemperatureSensor = "_0x2124";

    private static async Task<(ShellHarness Harness, ElementId Product, ElementId Setting, ElementId Pin)>
        WithSensorAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        ElementId product = (await harness.Session.AddProductAsync(locality, TemperatureSensor))!.Value;
        ProjectElement element = harness.Session.Current!.FindById(product)!;
        ElementId setting = new ProductView(harness.Session.Current!, element).SettingElements.First().Id!.Value;
        ElementId pin = element.DescendantsAndSelf().First(e => e.Kind == ElementKind.DatalinePin).Id!.Value;
        return (harness, product, setting, pin);
    }


    /// <summary>
    /// THE OVERRIDE. A finding anchored on the PRODUCT — which is where its family always anchors — but whose
    /// emission named the setting and its value reaches the constant editor, not the product's dialog.
    /// </summary>
    [Test]
    public async Task AFixLocationOnAChildElementRoutesToThatChild()
    {
        (ShellHarness harness, ElementId product, ElementId setting, _) = await WithSensorAsync();
        using var _h = harness;

        NavigationPlan withoutFix = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, product, "inivalue", new ProblemCode("dev-setting-default"));
        NavigationPlan withFix = ProblemsTestData.Planner(harness).Plan(
            harness.Session.Current!, product, "inivalue", new ProblemCode("dev-setting-default"),
            new FixLocation(setting, "inivalue"));

        Assert.Multiple(() =>
        {
            Assert.That(withoutFix.Kind, Is.EqualTo(NavigationKind.Dialog),
                "precondition: anchored on the product, the route can only reach that dialog — the product "
                + "descriptor has no 'inivalue' field, so this is the gap the mechanism closes");
            Assert.That(withFix.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(withFix.Dialog?.Site, Is.EqualTo(setting),
                "the route is about the element the EMISSION named, not the one the finding is anchored to");
            Assert.That(withFix.Dialog?.Owner, Is.EqualTo(product),
                "whose dialog is still the product's — the child is a sub-item inside it");
        });
    }

    /// <summary>
    /// The attribute half, alone. A fix location that names only an element keeps the entry's declared
    /// attribute — which is what lets a family override one without restating the other.
    /// </summary>
    [Test]
    public async Task AFixLocationWithNoAttributeKeepsTheDeclaredOne()
    {
        (ShellHarness harness, ElementId product, _, ElementId pin) = await WithSensorAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness).Plan(
            harness.Session.Current!, product, "cable_colour", new ProblemCode("doc-cable-colour"),
            new FixLocation(pin, null));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Dialog?.Site, Is.EqualTo(pin), "the element came from the fix location");
            Assert.That(plan.Dialog?.Attribute, Is.EqualTo("cable_colour"),
                "and the attribute from the entry, because the emission did not restate it");
        });
    }

    /// <summary>
    /// THE OTHER HALF: with no fix location the plan is byte-for-byte the one the declaration produced. This is
    /// the claim that makes the mechanism safe to add to a shipped engine.
    /// </summary>
    [Test]
    public async Task WithNoFixLocationTheRouteIsUnchanged()
    {
        (ShellHarness harness, ElementId product, ElementId setting, ElementId pin) = await WithSensorAsync();
        using var _h = harness;
        ProblemNavigationPlanner planner = ProblemsTestData.Planner(harness);
        Project project = harness.Session.Current!;

        Assert.Multiple(() =>
        {
            foreach ((ElementId site, string? attribute, string code) in new[]
            {
                (product, "position", "doc-position"),
                (product, (string?)null, "doc-not-linked"),
                (pin, "cable_colour", "doc-cable-colour"),
                (setting, "inivalue", "dev-setting-default"),
            })
            {
                Assert.That(
                    planner.Plan(project, site, attribute, new ProblemCode(code), fix: null),
                    Is.EqualTo(planner.Plan(project, site, attribute, new ProblemCode(code))),
                    $"{code}: passing no fix location must be the same call as not passing one");
            }
        });
    }

    /// <summary>
    /// And the finding carries it, so the panel can hand it over — the carried-fact pattern one level down.
    /// A finding built without one answers null, which is what every rule in the engine produces today.
    /// </summary>
    [Test]
    public void AFindingCarriesItsFixLocationAndDefaultsToNone()
    {
        ValidationFinding plain = Finding(fix: null);
        ValidationFinding located = Finding(new FixLocation(new ElementId(3, 0), "inivalue"));

        Assert.Multiple(() =>
        {
            Assert.That(plain.Fix, Is.Null, "the ordinary case: the entry's declaration speaks");
            Assert.That(located.Fix?.Element, Is.EqualTo(new ElementId(3, 0)));
            Assert.That(located.Fix?.Attribute, Is.EqualTo("inivalue"));
        });
    }

    private static ValidationFinding Finding(FixLocation? fix) =>
        new(new Problem(new ProblemCode("dev-setting-default"), "Besked", EquatableArray<ProblemArgument>.Empty),
            ValidationSeverity.Warning, ValidationCategory.DeviceSettings,
            new FindingLocation("_0x1", new ElementId(1, 0), null),
            EquatableArray<FindingLocation>.Empty)
        { Fix = fix };
}
