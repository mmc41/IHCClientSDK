using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
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
/// T045 — the route into the two SCENE dialogs: a product's <i>Scenarier</i> container and one membership's
/// value editor.
///
/// <para>The ordering is the substance. A scenes container and its members live UNDER a product, so the
/// planner's "any element inside a product" class would have claimed them and opened the product's dialog —
/// which is not where a scene value is edited. The scenes class therefore sits above it, and a test here
/// measures that rather than trusting the reading.</para>
///
/// <para>A SHUTTER member is the deliberate exception: no dialog edits one, so it keeps its tree row and
/// nothing more — the same judgement the node dispatch already makes.</para>
/// </summary>
public class SceneRouteTests : AvaloniaTestBase
{

    /// <summary>A product with a scenes container, and a block whose scene pin is wired into it.</summary>
    private static async Task<(ShellHarness Harness, MainWindowViewModel Vm, ElementId Scenes, ElementId Member)>
        WithSceneAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        // The locality the INSTALLATION tree draws, so the product and its scenes row are on screen —
        // Groups.First() can be a group the installation pane does not show.
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;

        // Whichever catalog product actually BRINGS a Scenarier container — the set is small and naming one
        // would pin this fixture to a catalog detail it does not care about.
        foreach (ProductDefinition candidate in harness.ProjectService.GetAvailableProducts())
        {
            if (await harness.Session.AddProductAsync(locality, candidate.ProductIdentifier) is not { } placed)
                continue;
            if (harness.Session.Current!.FindById(placed)!.DescendantsAndSelf()
                    .FirstOrDefault(e => e.IsScenesContainer) is { } scenes)
            {
                ProjectElement? member = scenes.Children.FirstOrDefault(c => c.IsSceneMember);
                return (harness, vm, scenes.Id!.Value, member?.Id ?? default);
            }
            await harness.Session.DeleteNodeAsync(placed);
        }
        throw new InvalidOperationException("no catalog product carries a scenes container");
    }

    // ── the planner half ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A note finding on the container reaches its own dialog's one editable field — NOT the product's, which is
    /// what the inside-a-product class would have given it.
    /// </summary>
    [Test]
    public async Task ASceneContainersNoteFinding_ReachesItsOwnDialog()
    {
        (ShellHarness harness, _, ElementId scenes, _) = await WithSceneAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, scenes, "note", new ProblemCode("doc-note-missing"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Reveal, Is.EqualTo(
                    ProjectTreeProjector.HasRow(harness.Session.Current!, scenes)
                        ? scenes
                        : ProjectTreeProjector.NearestRowBearingAncestor(harness.Session.Current!, scenes)),
                "the reveal follows the TREE — the container's own row where it is drawn, its product where an "
                + "empty one is not, because revealing a node the tree has no row for is a dead end");
            Assert.That(plan.Dialog?.Owner, Is.EqualTo(scenes), "its own dialog, not the product's");
            Assert.That(plan.Dialog?.Site, Is.EqualTo(scenes), "and no sub-item to select");
        });
    }

    /// <summary>An attribute no scene dialog renders degrades rather than promising a field.</summary>
    [Test]
    public async Task AnAttributeNoSceneDialogRenders_DegradesToTheDialog()
    {
        (ShellHarness harness, _, ElementId scenes, _) = await WithSceneAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, scenes, "icon", new ProblemCode("doc-note-missing"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog));
            Assert.That(plan.Dialog?.Attribute, Is.Null);
        });
    }

    /// <summary>And the whole route: the container's dialog opens, not the product's.</summary>
    [Test]
    public async Task ActivatingASceneContainerFinding_OpensTheScenesDialog()
    {
        (ShellHarness harness, MainWindowViewModel vm, ElementId scenes, _) = await WithSceneAsync();
        using var _h = harness;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, scenes, "note", new ProblemCode("doc-note-missing"));
        await vm.Problems.ActivateRowAsync(new ProblemRowViewModel(
            new ValidationFinding(
                new Problem(new ProblemCode("doc-note-missing"), "Besked", EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Warning, ValidationCategory.Documentation,
                new FindingLocation(scenes.ToToken(), scenes, null),
                EquatableArray<FindingLocation>.Empty) { TargetAttribute = "note" },
            scenes, "Scenarier", plan.Kind, "doc-note-missing@x"));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditSceneContainerCalls, Is.EqualTo(1), "the scenes dialog opened");
            Assert.That(harness.Dialogs.EditProductDialogCalls, Is.Zero,
                "and the product's did not — which is what the class ordering exists to guarantee");
        });
    }

    // ── the focus half ───────────────────────────────────────────────────────────────────────────────────

    private static SceneValueWindow Opened(bool isDimmer, SceneDialogField? focus)
    {
        SceneValueWindow window = new() { Title = "Scenarie" };
        window.Populate(new SceneValueInput(
            "Scenarie", isDimmer, On: true, LevelPercent: 50, RampMinutes: 0, RampSeconds: 5,
            SceneValue.LevelConstraint, SceneValue.RampPartConstraint, Focus: focus));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Control Named(Window window, string name) => window.FindControl<Control>(name)!;

    /// <summary>Each member key lands on its own control, on the variant that has it.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EveryMemberFieldKeyFocusesItsOwnControl()
    {
        (SceneDialogField Key, bool Dimmer, string Control)[] cases =
        [
            (SceneDialogField.State, false, "StateCombo"),
            (SceneDialogField.Level, true, "LevelBox"),
            (SceneDialogField.RampTime, true, "RampMinutesBox"),
        ];

        Assert.Multiple(() =>
        {
            foreach ((SceneDialogField key, bool dimmer, string control) in cases)
            {
                SceneValueWindow window = Opened(dimmer, key);
                CurrentTestWindow = window;
                Assert.That(Named(window, control).IsKeyboardFocusWithin, Is.True, key.ToString());
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    /// <summary>
    /// A key belonging to the OTHER variant focuses nothing of its own — a relay member has no level box — and
    /// the dialog opens on the field it does have.
    /// </summary>
    [AvaloniaTest]
    public void AKeyFromTheOtherVariant_FallsBackToTheFieldThisOneHas()
    {
        SceneValueWindow window = Opened(isDimmer: false, SceneDialogField.Level);
        CurrentTestWindow = window;

        Assert.Multiple(() =>
        {
            Assert.That(window.FocusTarget(SceneDialogField.Level), Is.Null);
            Assert.That(Named(window, "StateCombo").IsKeyboardFocusWithin, Is.True);
        });
    }

    /// <summary>The container's key is not this window's, and saying so is the point of returning null.</summary>
    [AvaloniaTest]
    public void TheContainersNoteKey_IsNotAFieldOfTheValueEditor()
    {
        SceneValueWindow window = Opened(isDimmer: true, SceneDialogField.Note);
        CurrentTestWindow = window;

        Assert.Multiple(() =>
        {
            Assert.That(window.FocusTarget(SceneDialogField.Note), Is.Null);
            Assert.That(Named(window, "LevelBox").IsKeyboardFocusWithin, Is.True, "so it opens on its own first value");
        });
    }

    /// <summary>An ordinary open — no route — still lands on the variant's value field.</summary>
    [AvaloniaTest]
    public void WithNoRoutedField_ADimmerOpensOnItsLevel()
    {
        SceneValueWindow window = Opened(isDimmer: true, focus: null);
        CurrentTestWindow = window;

        Assert.That(Named(window, "LevelBox").IsKeyboardFocusWithin, Is.True);
    }
}
