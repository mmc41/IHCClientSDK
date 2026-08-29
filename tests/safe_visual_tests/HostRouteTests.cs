using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace safe_visual_tests;

/// <summary>
/// T046 — the whole-project findings, which name no element at all.
///
/// <para>Everything else in the planner routes from an element. <i>Every masthead is blank</i> is about the
/// PROJECT, so the only thing left to route from is the finding's code — which is what an id is for, a grouping
/// key. The table is small, literal and lives in the host, because these are the host's own windows and the GUI
/// may not read the catalogue to ask about them.</para>
///
/// <para>The other half matters as much: a CAPACITY finding gets NO destination, and says so before the click.
/// Too many modules is a fact about the whole installation with no single field behind it, and the module map is
/// read-only — opening it would move the installer without helping them.</para>
/// </summary>
public class HostRouteTests
{
    private static ProblemNavigationPlanner Planner() =>
        ProblemNavigationPlanner.Over((p, id) => new ProjectAppService(new IhcSettings()).GetProductDialog(p, id));

    private static Project Empty() =>
        new ProjectAppService(new IhcSettings()).CreateNew(new ProjectDetails("P", "I", "DK"));

    private static NavigationPlan PlanFor(string code) =>
        Planner().Plan(Empty(), site: null, targetAttribute: null, new ProblemCode(code));

    /// <summary>The project-information rows have a window, and the row may promise it.</summary>
    [Test]
    public void AProjectInfoFinding_RoutesToTheProjectInformationWindow()
    {
        NavigationPlan plan = PlanFor("doc-project-info-blank");

        Assert.Multiple(() =>
        {
            Assert.That(plan.Host, Is.EqualTo(HostRoute.ProjectInfo));
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog),
                "a window, not a field — the finding is that ALL THREE mastheads are blank, so naming one "
                + "would be a promise about a single box the finding was never about");
            Assert.That(plan.Reveal, Is.Null, "there is no element, so no tree leg");
            Assert.That(plan.Dialog, Is.Null, "and no element-addressed dialog either");
        });
    }

    /// <summary>
    /// A capacity finding routes NOWHERE, and the row says so before the click. This is the honest half of the
    /// table — the half a route that opened the read-only module map would have hidden.
    /// </summary>
    [TestCase("capacity-modules-exceeded")]
    [TestCase("capacity-wireless-exceeded")]
    [TestCase("capacity-input-addresses")]
    public void ACapacityFinding_ReportsNoDestination(string code)
    {
        NavigationPlan plan = PlanFor(code);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Host, Is.EqualTo(HostRoute.None));
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.None));
            Assert.That(plan.Reveal, Is.Null);
        });
    }

    /// <summary>
    /// And the ROW built from that plan says it before the click — the panel's hint, not a status line the
    /// installer only meets after clicking and getting nothing.
    /// </summary>
    [Test]
    public void ACapacityRowSaysItLeadsNowhereBeforeTheClick()
    {
        ProblemRowViewModel row = Row("capacity-modules-exceeded", NavigationKind.None);
        ProblemRowViewModel info = Row("doc-project-info-blank", NavigationKind.Dialog);

        Assert.Multiple(() =>
        {
            Assert.That(row.NavigationHint, Is.Not.Empty, "a row with no destination still explains itself");
            Assert.That(row.NavigationHint, Is.Not.EqualTo(info.NavigationHint),
                "and does not read like one that has a window to open");
            Assert.That(info.NavigationHint, Does.Not.Contain("træet"),
                "nor does the routed one promise a tree leg it has no element for");
        });
    }

    private static ProblemRowViewModel Row(string code, NavigationKind kind) =>
        new(new ValidationFinding(
                new Problem(new ProblemCode(code), "Besked", EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Error, ValidationCategory.ProjectStructure,
                new FindingLocation("utcs_project", null, null),
                EquatableArray<FindingLocation>.Empty),
            null, "Projekt", kind, $"{code}@x");

    /// <summary>Activating one really opens the window — the plan carried out, not merely computed.</summary>
    [Test]
    public async Task ActivatingAProjectInfoFinding_OpensTheProjectInformationDialog()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.Problems.ActivateRowAsync(Row("doc-project-info-blank", NavigationKind.Dialog));

        Assert.That(harness.Dialogs.EditProjectInfoCalls, Is.EqualTo(1));
    }

    /// <summary>And activating a capacity row opens nothing at all.</summary>
    [Test]
    public async Task ActivatingACapacityFinding_OpensNothing()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.Problems.ActivateRowAsync(Row("capacity-modules-exceeded", NavigationKind.None));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditProjectInfoCalls, Is.Zero);
            Assert.That(harness.Dialogs.EditProductDialogCalls, Is.Zero);
        });
    }
}
