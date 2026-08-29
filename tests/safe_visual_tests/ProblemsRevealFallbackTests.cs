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
/// The panel reads the answer the reveal gives it. Selecting a row used to call the reveal and drop its bool on
/// the floor, so a finding on an element the tree does not draw looked like a click that did nothing.
/// <para>The fallback lands HERE and not inside the reveal itself: the reveal's other caller is the link jump,
/// where landing on the opposite end's PARENT would be a wrong answer rather than a courtesy.</para>
/// </summary>
public class ProblemsRevealFallbackTests
{
    /// <summary>A panel over a real session, with the reveal and the status line recorded rather than rendered.</summary>
    private sealed class Rig : System.IDisposable
    {
        public FakeTimeProvider Clock { get; } = new();
        public ShellHarness Harness { get; }
        public ProblemsPanelViewModel Panel { get; }

        /// <summary>Every id the panel asked to reveal, in order.</summary>
        public List<ElementId> Revealed { get; } = [];

        /// <summary>The ids the reveal REFUSES — the tree has no row for them.</summary>
        public HashSet<ElementId> Unrevealable { get; } = [];

        public List<string> Status { get; } = [];

        public Rig()
        {
            Harness = ShellHarness.Create(Clock);
            var validation = new ValidationMonitor(Harness.Session, _ => ImmutableArray<ValidationFinding>.Empty);
            Panel = new ProblemsPanelViewModel(Harness.Session, validation,
                reveal: id =>
                {
                    Revealed.Add(id);
                    return !Unrevealable.Contains(id);
                },
                setStatus: Status.Add);
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
        rig.Unrevealable.Add(setting);

        rig.Panel.SelectedRow = Row(setting, NavigationKind.Ancestor);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Revealed, Is.EqualTo(new[] { setting, product }).AsCollection,
                "the element is tried first, and only its refusal sends the panel up to the product");
            Assert.That(rig.Status, Is.Empty, "landing somewhere is not a failure to report");
        });
    }

    [Test]
    public async Task WhenNothingAboveHasARowEither_TheStatusLineSaysSo()
    {
        using Rig rig = new();
        (ElementId setting, ElementId product) = await SettingInsideAProductAsync(rig);
        rig.Unrevealable.Add(setting);
        rig.Unrevealable.Add(product);

        rig.Panel.SelectedRow = Row(setting, NavigationKind.Ancestor);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Status, Is.EqualTo(new[] { "Elementet vises ikke i træet." }).AsCollection);
            Assert.That(rig.Revealed, Does.Contain(product), "the ancestor was genuinely attempted");
        });
    }

    [Test]
    public async Task ARowThatRevealsNormally_ReportsNothingAndWalksNowhere()
    {
        using Rig rig = new();
        (ElementId _, ElementId product) = await SettingInsideAProductAsync(rig);

        rig.Panel.SelectedRow = Row(product, NavigationKind.Tree);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Revealed, Is.EqualTo(new[] { product }).AsCollection);
            Assert.That(rig.Status, Is.Empty);
        });
    }

    [Test]
    public async Task ARowWithNoElementAtAll_RevealsNothingAndReportsNothing()
    {
        using Rig rig = new();
        await SettingInsideAProductAsync(rig);

        rig.Panel.SelectedRow = Row(null, NavigationKind.None);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Revealed, Is.Empty);
            Assert.That(rig.Status, Is.Empty,
                "the row already says before the click that it points at no single element");
        });
    }
}
