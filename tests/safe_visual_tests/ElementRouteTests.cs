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
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

namespace safe_visual_tests;

/// <summary>
/// T044 — the route into the plain element Properties dialog, which a LOCALITY, a FUNCTION BLOCK and a
/// <i>Betingelser</i> group all share.
///
/// <para>All three are drawn in the tree, so like the variables they had to be classified before the planner's
/// row test or every one of their findings would have stopped at the row.</para>
///
/// <para>The interesting case is <c>master_*</c>. A library block's provenance IS shown in this dialog — five
/// boxes below the editable pair — but every one of them is greyed, so a route that focused one would land the
/// caret where nothing can be typed while the row had promised a field. Those attributes therefore reach
/// <see cref="NavigationKind.Dialog"/>, and that is asserted here rather than left to the allowlist.</para>
/// </summary>
public class ElementRouteTests : AvaloniaTestBase
{
    private static (Project Project, ElementId Locality, ElementId Block, ElementId Conditions) Elements()
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId locality = project.Groups.First().Id!.Value;
        ElementId block = session.Apply(
            app.Commands.AddEmptyFunctionBlock(session.Current!, locality, "Blok")).Value;
        // A Betingelser group comes with a SUB-PROGRAM, so the block needs a program and a sub-program inside it
        // before there is anything of that kind to route to.
        ElementId programs = session.Current!.FindById(block)!.DescendantsAndSelf()
            .First(e => e.Tag == "programs").Id!.Value;
        session.Apply(app.Commands.AddProgram(session.Current!, programs, "Program"));
        ElementId actions = session.Current!.FindById(block)!.DescendantsAndSelf()
            .First(e => e.Tag == "actions").Id!.Value;
        session.Apply(app.Commands.AddSubProgram(session.Current!, actions));
        ProjectElement conditions = session.Current!.FindById(block)!.DescendantsAndSelf()
            .First(e => e.Tag == "conditions");
        return (session.Current!, locality, block, conditions.Id!.Value);
    }

    private static ProblemNavigationPlanner Planner() =>
        ProblemNavigationPlanner.Over((p, id) => new ProjectAppService(new IhcSettings()).GetProductDialog(p, id));

    private static NavigationPlan PlanFor(Project project, ElementId id, string? attribute) =>
        Planner().Plan(project, id, attribute, new ProblemCode("doc-note-missing"));

    // ── the planner half ─────────────────────────────────────────────────────────────────────────────────

    [TestCase("name")]
    [TestCase("note")]
    public void ALocalityFindingNamingAField_ReachesThatField(string attribute)
    {
        (Project project, ElementId locality, _, _) = Elements();

        NavigationPlan plan = PlanFor(project, locality, attribute);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Reveal, Is.EqualTo(locality));
            Assert.That(plan.Dialog?.Attribute, Is.EqualTo(attribute));
        });
    }

    [TestCase("name")]
    [TestCase("note")]
    public void AFunctionBlockFindingNamingAField_ReachesThatField(string attribute)
    {
        (Project project, _, ElementId block, _) = Elements();

        Assert.That(PlanFor(project, block, attribute).Kind, Is.EqualTo(NavigationKind.Field));
    }

    /// <summary>The operator is stored as <c>type</c>, which is the whole reason the attribute map exists.</summary>
    [Test]
    public void AConditionsLogicFinding_ReachesTheLogicField()
    {
        (Project project, _, _, ElementId conditions) = Elements();

        NavigationPlan plan = PlanFor(project, conditions, "type");

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Dialog?.Attribute, Is.EqualTo("type"));
        });
    }

    /// <summary>
    /// THE READ-ONLY CASE. Every provenance attribute degrades, and the hop names no field — so the dialog
    /// opens and focuses nothing rather than putting the caret in a greyed box.
    /// </summary>
    [TestCase("master_type")]
    [TestCase("master_version")]
    [TestCase("master_name")]
    [TestCase("master_note")]
    public void AProvenanceFinding_DegradesToTheDialog(string attribute)
    {
        (Project project, _, ElementId block, _) = Elements();

        NavigationPlan plan = PlanFor(project, block, attribute);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog),
                "the provenance group is read-only, so no route may promise a field in it");
            Assert.That(plan.Dialog?.Attribute, Is.Null, "and nothing is focused");
            Assert.That(plan.Reveal, Is.EqualTo(block), "the tree still lands on the block");
        });
    }

    // ── the focus half ───────────────────────────────────────────────────────────────────────────────────

    private static PropertiesWindow Opened(ElementDialogField? focus, bool? conditionsOr = null)
    {
        PropertiesWindow window = new() { Title = "Rediger Stue egenskaber" };
        window.Populate("Stue", "note", conditionsOr: conditionsOr, focus: focus);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Control Named(Window window, string name) => window.FindControl<Control>(name)!;

    /// <summary>Every key lands on its own control — one case per key, so a missing map entry cannot slip past.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EveryFieldKeyFocusesItsOwnControl()
    {
        (ElementDialogField Key, string Control, bool? Logic)[] cases =
        [
            (ElementDialogField.Name, "NameBox", null),
            (ElementDialogField.Note, "NoteBox", null),
            // The logic field exists only on a Betingelser group, so its case has to build one.
            (ElementDialogField.Logic, "LogicBox", false),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(cases.Select(c => c.Key), Is.EquivalentTo(System.Enum.GetValues<ElementDialogField>()),
                "every declared key is exercised — one added without a map entry would slip through otherwise");

            foreach ((ElementDialogField key, string control, bool? logic) in cases)
            {
                PropertiesWindow window = Opened(key, logic);
                CurrentTestWindow = window;
                Assert.That(Named(window, control).IsFocused, Is.True, key.ToString());
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    /// <summary>
    /// The logic key on an element that has no logic field focuses nothing of its own and the dialog opens on
    /// the name — the panel is hidden for everything but a <i>Betingelser</i> group.
    /// </summary>
    [AvaloniaTest]
    public void TheLogicKeyWhereThereIsNoLogicField_FallsBackToTheName()
    {
        PropertiesWindow window = Opened(ElementDialogField.Logic, conditionsOr: null);
        CurrentTestWindow = window;

        Assert.Multiple(() =>
        {
            Assert.That(window.FocusTarget(ElementDialogField.Logic), Is.Null);
            Assert.That(Named(window, "NameBox").IsFocused, Is.True);
        });
    }

    /// <summary>An ordinary Egenskaber — no route, no key — still opens on the name.</summary>
    [AvaloniaTest]
    public void WithNoRoutedField_TheDialogOpensOnTheName()
    {
        PropertiesWindow window = Opened(focus: null);
        CurrentTestWindow = window;

        Assert.That(Named(window, "NameBox").IsFocused, Is.True);
    }

    /// <summary>
    /// And a provenance box is not focusable even if a key could name one: the whole group is disabled, which is
    /// the fact the degradation above rests on.
    /// </summary>
    [AvaloniaTest]
    public void TheProvenanceBoxesAreNotEditable()
    {
        PropertiesWindow window = new() { Title = "Funktionsblok egenskaber" };
        window.Populate("Blok", "note", new LibraryOrigin("Bib", "3", "1.0", "01/01/2024", "MC"));
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            foreach (string box in new[]
                { "OriginNameBox", "OriginNumberBox", "OriginVersionBox", "OriginCreatedBox", "OriginDeveloperBox" })
            {
                Assert.That(Named(window, box).IsEnabled, Is.False, box);
            }
        });
    }

    // ── the whole route ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Activating a block's name finding opens its dialog on the name.</summary>
    [Test]
    public async Task ActivatingABlockFinding_OpensItsDialogOnTheField()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId locality = harness.Session.Current!.Groups.First().Id!.Value;
        ElementId block = (await harness.Session.ApplyAsync(
            harness.Session.Commands.AddEmptyFunctionBlock(harness.Session.Current!, locality, "Blok"))).Value;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, block, "note", new ProblemCode("doc-note-missing"));
        await vm.Problems.ActivateRowAsync(new ProblemRowViewModel(
            new ValidationFinding(
                new Problem(new ProblemCode("doc-note-missing"), "Besked", EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Warning, ValidationCategory.Documentation,
                new FindingLocation(block.ToToken(), block, null),
                EquatableArray<FindingLocation>.Empty) { TargetAttribute = "note" },
            block, "Blok", plan.Kind, "doc-note-missing@x"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field), "precondition: the row promised a field");
            Assert.That(harness.Dialogs.EditPropertiesCalls, Is.EqualTo(1));
            Assert.That(harness.Dialogs.LastPropertiesFocus, Is.EqualTo(ElementDialogField.Note));
        });
    }
}
