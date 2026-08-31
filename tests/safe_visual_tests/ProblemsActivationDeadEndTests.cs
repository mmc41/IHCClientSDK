using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace safe_visual_tests;

/// <summary>
/// What ACTIVATING a row does when the route turns out to lead nowhere. The panel owns the sentence — the shell
/// owns where a sentence is shown — and it is said only where the row could not say it first.
/// <para>The dead end is a real state rather than an error: a finding is a fact as of the run it came from, so
/// the element it names can be gone by the time the installer reaches for it. Saying so is the difference
/// between that and a gesture that looks broken.</para>
/// </summary>
public class ProblemsActivationDeadEndTests
{
    /// <summary>A panel over a real session, with the activation and the status line recorded rather than run.</summary>
    private sealed class Rig : System.IDisposable
    {
        public FakeTimeProvider Clock { get; } = new();
        public ShellHarness Harness { get; }
        public ProblemsPanelViewModel Panel { get; }

        /// <summary>Every plan the panel handed on, in order.</summary>
        public List<NavigationPlan> Activated { get; } = [];

        public List<string> Status { get; } = [];

        public Rig()
        {
            Harness = ShellHarness.Create(Clock);
            var validation = new ValidationMonitor(Harness.Session, _ => StructuredValidationResult.Empty);
            Panel = new ProblemsPanelViewModel(Harness.Session, validation,
                setStatus: Status.Add,
                activate: plan => { Activated.Add(plan); return Task.CompletedTask; });
        }

        public void Dispose()
        {
            Panel.Dispose();
            Harness.Dispose();
        }
    }

    private static ProblemRowViewModel Row(ElementId? element, NavigationKind kind) =>
        new(new ValidationFinding(
                new Problem(new ProblemCode("doc-name-empty"), "Navnet mangler.",
                    EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Warning, ValidationCategory.Documentation,
                new FindingLocation("_0x1", element, null), EquatableArray<FindingLocation>.Empty),
            element, "navn", kind, "doc-name-empty@_0x1");

    /// <summary>Opens the complex fixture and returns a setting element and the product that holds it.</summary>
    private static async Task<(ElementId Setting, ElementId Product)> SettingInsideAProductAsync(Rig rig)
    {
        await rig.Harness.Session.OpenAsync(ProblemsTestData.FixturePath("project3-KompleksWired.vis"));
        Project project = rig.Harness.Session.Current!;
        ProjectElement container = project.Root.DescendantsAndSelf()
            .First(e => e.Tag.EndsWith("_settings") && e.Children.Count > 0);
        ProjectElement setting = container.Children[0];
        ProjectElement product = project.FindParent(container.Id!.Value)!;
        Assert.That(ProjectTreeProjector.HasRow(project, setting.Id!.Value), Is.False,
            "precondition: the fixture's setting genuinely has no row of its own");
        return (setting.Id!.Value, product.Id!.Value);
    }

    [Test]
    public async Task ARowWhoseElementHasNoRow_RevealsTheNearestAncestorThatHasOne()
    {
        using Rig rig = new();
        (ElementId setting, ElementId product) = await SettingInsideAProductAsync(rig);

        await rig.Panel.ActivateRowAsync(Row(setting, NavigationKind.Dialog));

        Assert.Multiple(() =>
        {
            Assert.That(rig.Activated.Single().Reveal, Is.EqualTo(product),
                "the element the tree does not draw routes to the one above it that it does — which is where "
                + "the value is edited anyway");
            Assert.That(rig.Status, Is.Empty, "landing somewhere is not a failure to report");
        });
    }

    [Test]
    public async Task ARowWhoseElementIsGoneSinceTheRun_SaysSoRatherThanLookingLikeADeadGesture()
    {
        using Rig rig = new();
        await SettingInsideAProductAsync(rig);

        // An id no element in the document carries. A finding is a fact as of its run, so this is the ordinary
        // shape of a stale row rather than a corrupt one.
        await rig.Panel.ActivateRowAsync(Row(new ElementId(0x7FFFFFF, 0x11), NavigationKind.Tree));

        Assert.Multiple(() =>
        {
            Assert.That(rig.Status, Is.EqualTo(new[] { ProblemsPanelViewModel.DeadEndStatus }).AsCollection);
            Assert.That(rig.Activated.Single().Kind, Is.EqualTo(NavigationKind.None),
                "and the plan is handed on stated as empty, rather than the activation being skipped");
        });
    }

    [Test]
    public async Task ARowThatRoutesNormally_ReportsNothing()
    {
        using Rig rig = new();
        (ElementId _, ElementId product) = await SettingInsideAProductAsync(rig);

        await rig.Panel.ActivateRowAsync(Row(product, NavigationKind.Tree));

        Assert.Multiple(() =>
        {
            Assert.That(rig.Activated.Single().Reveal, Is.EqualTo(product));
            Assert.That(rig.Status, Is.Empty);
        });
    }

    [Test]
    public async Task ARowWithNoElementAtAll_ReportsNothing()
    {
        using Rig rig = new();
        await SettingInsideAProductAsync(rig);

        await rig.Panel.ActivateRowAsync(Row(null, NavigationKind.None));

        Assert.That(rig.Status, Is.Empty,
            "the row already says before the gesture that it points at no single element, so a sentence "
            + "afterwards would only repeat it");
    }
}
