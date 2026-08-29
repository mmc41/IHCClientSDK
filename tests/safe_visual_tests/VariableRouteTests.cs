using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
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
/// T043 — the route into a VARIABLE's own editor: an ordinary function-block resource or an enum one.
///
/// <para>The class has to be decided before the row test the planner falls through to. A variable HAS a tree
/// row, so without it every variable finding was tree-level — the installer landed on the row with the field
/// they came for still one gesture away, and the row had promised no more than that.</para>
///
/// <para>The focus half is asserted control by control rather than through one representative key, because the
/// value keys resolve through the variable's KIND: the six value panels are mutually exclusive, and a map that
/// named a control would land the caret in a hidden panel for every type it guessed wrong.</para>
/// </summary>
public class VariableRouteTests : AvaloniaTestBase
{
    // ── the planner half ─────────────────────────────────────────────────────────────────────────────────

    private static (Project Project, ElementId Variable) ProjectWithVariable(string tag = "resource_integer")
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId locality = project.Groups.First().Id!.Value;
        ElementId block = session.Apply(
            app.Commands.AddEmptyFunctionBlock(session.Current!, locality, "Blok")).Value;
        ElementId section = session.Current!.FindById(block)!.Children
            .First(c => c.Tag is "settings" or "variables").Id!.Value;
        session.Apply(app.Commands.AddVariable(session.Current!, section, tag, "Måler")!);
        ElementId variable = session.Current!.FindById(block)!.Descendants()
            .First(e => e.Tag == tag && e.GetAttribute("name") == "Måler").Id!.Value;
        return (session.Current!, variable);
    }

    private static ProblemNavigationPlanner Planner() =>
        ProblemNavigationPlanner.Over((p, id) => new ProjectAppService(new IhcSettings()).GetProductDialog(p, id));

    [TestCase("name")]
    [TestCase("note")]
    [TestCase("note-2")]
    [TestCase("inivalue")]
    [TestCase("backup")]
    public void AVariableFindingNamingAField_ReachesThatField(string attribute)
    {
        (Project project, ElementId variable) = ProjectWithVariable();

        NavigationPlan plan = Planner().Plan(project, variable, attribute, new ProblemCode("doc-note-missing"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Reveal, Is.EqualTo(variable), "the variable's own row, not its block's");
            Assert.That(plan.Dialog?.Owner, Is.EqualTo(variable), "its own dialog owns the field");
            Assert.That(plan.Dialog?.Site, Is.EqualTo(variable), "no sub-item to step into");
            Assert.That(plan.Dialog?.Attribute, Is.EqualTo(attribute));
        });
    }

    /// <summary>
    /// A time variable stores its value in four attributes and a date in two. A finding about any of them is a
    /// finding about the ONE value group the installer edits, so each reaches the field rather than degrading.
    /// </summary>
    [TestCase("hour")]
    [TestCase("minute")]
    [TestCase("second")]
    [TestCase("millisecond")]
    [TestCase("day")]
    [TestCase("month")]
    public void APartOfATypedValue_ReachesTheValueField(string attribute)
    {
        (Project project, ElementId variable) = ProjectWithVariable("resource_timer");

        NavigationPlan plan = Planner().Plan(project, variable, attribute, new ProblemCode("doc-note-missing"));

        Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
    }

    /// <summary>
    /// An attribute the editor does not render degrades to the dialog — the same honesty rule the product route
    /// follows. It opens the variable's dialog and promises nothing more.
    /// </summary>
    [Test]
    public void AnAttributeTheEditorDoesNotRender_DegradesToTheDialog()
    {
        (Project project, ElementId variable) = ProjectWithVariable();

        NavigationPlan plan = Planner().Plan(project, variable, "icon", new ProblemCode("doc-note-missing"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog));
            Assert.That(plan.Dialog?.Attribute, Is.Null, "and names no field, so nothing is focused");
            Assert.That(plan.Reveal, Is.EqualTo(variable), "the tree still lands on it");
        });
    }

    /// <summary>
    /// A finding about the variable that names NO attribute stops at its tree row, and opens nothing.
    ///
    /// <para>Corrected in T051 after a live run: <c>logic-variable-write-only</c> is a finding about a variable
    /// that is written and never read, and its repair is to edit the PROGRAM. Its editor holds a name, a note
    /// and an initial value, none of which is the fix — so a dialog there is a modal to dismiss before the
    /// installer can do what the finding asked. The same rule the locality/block class follows: an element that
    /// owns its tree row keeps attribute-less findings on that row.</para>
    /// </summary>
    [Test]
    public void AVariableFindingWithNoAttribute_StopsAtItsTreeRow()
    {
        (Project project, ElementId variable) = ProjectWithVariable();

        NavigationPlan plan = Planner().Plan(project, variable, null, new ProblemCode("logic-variable-write-only"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Tree));
            Assert.That(plan.Reveal, Is.EqualTo(variable));
            Assert.That(plan.Dialog, Is.Null, "nothing opens — the repair is in the program, not in this editor");
        });
    }

    // ── the focus half ───────────────────────────────────────────────────────────────────────────────────

    private static VariablePropertiesWindow Opened(
        ResourceInitialValue value, VariableDialogField? focus)
    {
        VariablePropertiesWindow window = new() { Title = "Rediger Måler egenskaber" };
        window.Populate(new VariablePropertiesInput(
            "Rediger Måler egenskaber", "Måler", "note", value, Focus: focus));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Control Named(Window window, string name) => window.FindControl<Control>(name)!;

    /// <summary>
    /// Every key lands on its own control — one case per key, so a map entry cannot be silently absent, and
    /// every value KIND separately, because that is what <c>InitialValue</c> resolves through.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EveryFieldKeyFocusesItsOwnControl()
    {
        (VariableDialogField Key, ResourceInitialValue Value, string Control)[] cases =
        [
            (VariableDialogField.Name, ResourceInitialValue.None, "NameBox"),
            (VariableDialogField.Note, ResourceInitialValue.None, "NoteBox"),
            (VariableDialogField.HelpNote, ResourceInitialValue.None, "HelpNoteBox"),
            (VariableDialogField.Backup, ResourceInitialValue.None, "SaveOnPowerLossBox"),
            (VariableDialogField.InitialValue, ResourceInitialValue.OfNumber(3), "NumberBox"),
            (VariableDialogField.InitialValue, ResourceInitialValue.OfBool(true), "BoolBox"),
            (VariableDialogField.InitialValue, ResourceInitialValue.OfDecimal(1.5), "DecimalBox"),
            (VariableDialogField.InitialValue, ResourceInitialValue.OfTime(1, 2, 3, 4), "HourBox"),
            (VariableDialogField.InitialValue, ResourceInitialValue.OfDate(1, 2), "DayBox"),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(cases.Select(c => c.Key).Distinct(),
                Is.EquivalentTo(System.Enum.GetValues<VariableDialogField>()),
                "every declared key is exercised — one added without a map entry would slip through otherwise");

            foreach ((VariableDialogField key, ResourceInitialValue value, string control) in cases)
            {
                VariablePropertiesWindow window = Opened(value, key);
                CurrentTestWindow = window;
                Assert.That(Named(window, control).IsFocused, Is.True, $"{key} on a {value.Kind} variable");
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    /// <summary>
    /// A key whose control sits in a HIDDEN panel focuses nothing of its own: the dialog falls back to the name,
    /// where every ordinary Egenskaber has always opened. A caret in an invisible panel would be a route that
    /// reported success and left the installer nowhere.
    /// </summary>
    [AvaloniaTest]
    public void AValueKeyOnAVariableWithNoValue_FallsBackToTheName()
    {
        VariablePropertiesWindow window = Opened(ResourceInitialValue.None, VariableDialogField.InitialValue);
        CurrentTestWindow = window;

        Assert.Multiple(() =>
        {
            Assert.That(window.FocusTarget(VariableDialogField.InitialValue), Is.Null,
                "there is no value control to land in");
            Assert.That(Named(window, "NameBox").IsFocused, Is.True, "so the dialog opens where it always did");
        });
    }

    /// <summary>An ordinary Egenskaber — no route, no key — still opens on the name.</summary>
    [AvaloniaTest]
    public void WithNoRoutedField_TheDialogOpensOnTheName()
    {
        VariablePropertiesWindow window = Opened(ResourceInitialValue.OfNumber(3), focus: null);
        CurrentTestWindow = window;

        Assert.That(Named(window, "NameBox").IsFocused, Is.True);
    }

    /// <summary>And the whole route: activating a variable finding opens its editor on the field.</summary>
    [Test]
    public async Task ActivatingAVariableFinding_OpensItsEditorOnTheField()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId locality = harness.Session.Current!.Groups.First().Id!.Value;
        ElementId block = (await harness.Session.ApplyAsync(
            harness.Session.Commands.AddEmptyFunctionBlock(harness.Session.Current!, locality, "Blok"))).Value;
        ElementId section = harness.Session.Current!.FindById(block)!.Children
            .First(c => c.Tag is "settings" or "variables").Id!.Value;
        await harness.Session.ApplyAsync(harness.Session.Commands
            .AddVariable(harness.Session.Current!, section, "resource_integer", "Måler")!);
        ElementId variable = harness.Session.Current!.FindById(block)!.Descendants()
            .First(e => e.GetAttribute("name") == "Måler").Id!.Value;

        NavigationPlan plan = ProblemsTestData.Planner(harness)
            .Plan(harness.Session.Current!, variable, "note", new ProblemCode("doc-note-missing"));
        await vm.Problems.ActivateRowAsync(new ProblemRowViewModel(
            new ValidationFinding(
                new Problem(new ProblemCode("doc-note-missing"), "Besked", EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Warning, ValidationCategory.Documentation,
                new FindingLocation(variable.ToToken(), variable, null),
                EquatableArray<FindingLocation>.Empty) { TargetAttribute = "note" },
            variable, "Måler", plan.Kind, "doc-note-missing@x"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field), "precondition: the row promised a field");
            Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Focus, Is.EqualTo(VariableDialogField.Note),
                "and the editor was asked for exactly that field");
        });
    }
}
