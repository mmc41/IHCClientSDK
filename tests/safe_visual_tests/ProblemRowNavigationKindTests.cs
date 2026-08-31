using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace safe_visual_tests;

/// <summary>
/// What a <i>Problemer</i> row promises BEFORE the click. The row no longer answers the yes/no question "does a
/// click go anywhere"; it names the destination it has, so the honesty the dimmed cell used to carry alone is now
/// carried by a hint that says WHICH element the tree will land on.
/// <para><c>Ancestor</c> is a member in its own right rather than a shade of <c>Tree</c>: a settings finding lands
/// on the product, and a row that said "the element" would be promising something it does not do.</para>
/// <para><b>FINDING rows only.</b> Every claim below is about a row projected from a validation finding. An
/// internal-error row has no <c>NavigationKind</c> and never consults the planner: it is about the tool, not
/// about the project, so it has no element to land on and nothing to promise. Its activation is asserted in
/// <c>ProblemsActivationGestureTests</c> instead, where the gesture opens a dialog rather than a route.</para>
/// </summary>
public class ProblemRowNavigationKindTests
{
    private static Project BuildProject()
    {
        static ProjectElement Element(string tag, string id, params ProjectElement[] children) =>
            ProjectElement.Create(tag, ElementId.ParseOrNull(id), [], children);

        ProjectElement product = Element("product_dataline", "_0x100",
            Element("dataline_input", "_0x101"),
            Element("dimmer_settings", "_0x106", Element("dimmer_setting_fade_rate_up", "_0x107")));

        return new Project(Element("project", "_0x0",
            Element("groups", "_0x1", Element("group", "_0x2", product))));
    }

    private static ProblemRowViewModel Row(Project project, string? locator, ElementId? element)
    {
        var finding = new ValidationFinding(
            new Problem(new ProblemCode("doc-name-empty"), "Navnet på produktet mangler.",
                EquatableArray<ProblemArgument>.Empty),
            ValidationSeverity.Warning, ValidationCategory.Documentation,
            locator is null ? null : new FindingLocation(locator, element, null),
            EquatableArray<FindingLocation>.Empty);
        return ProblemsPanelViewModel.ToRow(
            finding, project, ProblemsPanelViewModel.IndexById(project), Planner);
    }

    private static ElementId Id(string token) => ElementId.ParseOrNull(token)!.Value;

    /// <summary>The synthetic product composes no dialog, so nothing here can claim a field.</summary>
    private static ProblemNavigationPlanner Planner { get; } =
        ProblemNavigationPlanner.Over((_, _) => new Ihc.Vis.Products.ProductDialogDescriptor("", []));

    [Test]
    public void AnElementWithItsOwnRow_PromisesTheTree()
    {
        Project project = BuildProject();
        ProblemRowViewModel row = Row(project, "_0x2", Id("_0x2"));

        Assert.Multiple(() =>
        {
            Assert.That(row.NavigationKind, Is.EqualTo(NavigationKind.Tree),
                "a locality is neither a pin nor a product, so nothing deeper is claimed for it");
            Assert.That(row.NavigationHint, Does.Contain("træet"));
        });
    }

    /// <summary>
    /// A terminal has a tree row of its own, and the row still does not promise <c>Tree</c>: the fix for a
    /// terminal's documentation lives in the product's dialog, so the route goes deeper. The kind says so before
    /// the click rather than surprising the user after it.
    /// </summary>
    [Test]
    public void ATerminal_PromisesTheDialogItsFixActuallyLivesIn()
    {
        Project project = BuildProject();
        ProblemRowViewModel row = Row(project, "_0x101", Id("_0x101"));

        Assert.That(row.NavigationKind, Is.EqualTo(NavigationKind.Dialog),
            "the synthetic product composes no dialog fields, so Field cannot be claimed — but the destination "
            + "is still the product's dialog rather than the tree");
    }

    /// <summary>
    /// A setting inside a <c>*_settings</c> container has no row of its own, and the row does not pretend it
    /// does: it promises the PRODUCT's dialog, which is where such a value is edited.
    /// </summary>
    [Test]
    public void AnElementTheTreeDoesNotDraw_PromisesTheDialogItIsEditedIn()
    {
        Project project = BuildProject();
        ProblemRowViewModel row = Row(project, "_0x107", Id("_0x107"));

        Assert.That(row.NavigationKind, Is.EqualTo(NavigationKind.Dialog));
    }

    [Test]
    public void ARowWithNoElement_PromisesNothing()
    {
        Project project = BuildProject();
        ProblemRowViewModel row = Row(project, "utcs_project", null);

        Assert.That(row.NavigationKind, Is.EqualTo(NavigationKind.None));
    }

    [Test]
    public void AVanishedElement_PromisesNothing()
    {
        Project project = BuildProject();
        ProblemRowViewModel row = Row(project, "_0xdead", Id("_0xdead"));

        Assert.That(row.NavigationKind, Is.EqualTo(NavigationKind.None),
            "an id the snapshot no longer holds has neither a row nor an ancestor with one");
    }

    /// <summary>
    /// Each kind says something different, in Danish, and never nothing.
    /// <para>Built through the ROW rather than through the projection, deliberately: the projection can only
    /// produce the kinds the panel currently assigns, so a hint added for a kind it does not yet assign would
    /// go unexercised exactly while it is newest.</para>
    /// </summary>
    [Test]
    public void EveryKindHasItsOwnHint()
    {
        Dictionary<NavigationKind, string> hints = System.Enum.GetValues<NavigationKind>()
            .ToDictionary(kind => kind, kind => RowOfKind(kind).NavigationHint);

        Assert.Multiple(() =>
        {
            Assert.That(hints.Values.Distinct().Count(), Is.EqualTo(hints.Count),
                "the hints differ, so the row genuinely says WHICH destination it has");
            Assert.That(hints.Values, Is.All.Not.Empty);
        });
    }

    /// <summary>A row carrying exactly this kind, whatever the panel would have assigned.</summary>
    private static ProblemRowViewModel RowOfKind(NavigationKind kind) =>
        new(new ValidationFinding(
                new Problem(new ProblemCode("doc-name-empty"), "Navnet på produktet mangler.",
                    EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Warning, ValidationCategory.Documentation,
                new FindingLocation("_0x101", Id("_0x101"), null), EquatableArray<FindingLocation>.Empty),
            Id("_0x101"), "navn", kind, "doc-name-empty@_0x101");

    /// <summary>
    /// THE ONE-RESOLVER PROPERTY: what a row PROMISES is what the planner would DO. The row's kind is not a
    /// second derivation that happens to agree today — it is the planner's own answer, so the two cannot drift.
    /// <para>Checked across every route class, including the two the panel could not claim at all until the row
    /// started asking the planner.</para>
    /// </summary>
    [Test]
    public void ARowsPromisedKindIsThePlannersOwnAnswerForEveryRouteClass()
    {
        using ShellHarness harness = ShellHarness.Create();
        Project project = harness.ProjectService
            .Load(ProblemsTestData.FixturePath("Project6-Errors.vis"))
            .GetAwaiter().GetResult();
        ProblemNavigationPlanner planner = ProblemsTestData.Planner(harness.ProjectService);
        Dictionary<ElementId, ProjectElement?> byId = ProblemsPanelViewModel.IndexById(project);

        // .Findings, not the whole structured result: a fault is not a finding and would have no row to plan.
        var rows = harness.ProjectService.ValidateStructured(project).Findings
            .Select(f => (Finding: f, Row: ProblemsPanelViewModel.ToRow(f, project, byId, planner)))
            .ToList();

        Assert.Multiple(() =>
        {
            foreach ((ValidationFinding finding, ProblemRowViewModel row) in rows)
            {
                NavigationKind planned = planner
                    .Plan(project, row.Element, finding.TargetAttribute, finding.Code).Kind;
                Assert.That(row.NavigationKind, Is.EqualTo(planned), row.OccurrenceId);
            }

            // Not vacuous: the fixture must exercise more than one route class, or agreement proves nothing.
            Assert.That(rows.Select(r => r.Row.NavigationKind).Distinct().Count(), Is.GreaterThan(1),
                "the corpus reaches several route classes: " + string.Join(", ",
                    rows.Select(r => r.Row.NavigationKind).Distinct().OrderBy(k => k)));
        });
    }

    /// <summary>
    /// The element cell is drawn full when the click lands somewhere and dimmed when it does not. The cell shows
    /// the element's NAME, which is a fact either way — WHERE the click goes is what the hint carries, so no third
    /// opacity is invented for the ancestor case.
    /// </summary>
    [Test]
    public void TheElementCellIsDimmedOnlyWhenTheClickGoesNowhere()
    {
        Project project = BuildProject();
        ProblemRowViewModel tree = Row(project, "_0x101", Id("_0x101"));
        ProblemRowViewModel ancestor = Row(project, "_0x107", Id("_0x107"));
        ProblemRowViewModel none = Row(project, "utcs_project", null);

        Assert.Multiple(() =>
        {
            Assert.That(ancestor.ElementEmphasis, Is.EqualTo(tree.ElementEmphasis));
            Assert.That(none.ElementEmphasis, Is.LessThan(tree.ElementEmphasis));
        });
    }
}
