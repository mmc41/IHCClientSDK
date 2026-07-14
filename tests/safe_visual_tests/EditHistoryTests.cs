using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>US-052: multi-level undo/redo — every project-mutating edit is reversible and re-applicable, redo is
/// invalidated by a new edit, an empty history is a no-op, and a cascading delete reverses as one step.</summary>
public class EditHistoryTests
{
    [Test]
    public async Task Undo_Redo_LocalityInsert_ReflectedInBothPanes()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int groupsBefore = harness.Session.Current!.Groups.Count;
        int paneBefore = vm.InstallationNodes[0].Children.Count;

        await harness.Session.AddLocalityAsync();
        Assert.That(harness.Session.CanUndo, Is.True, "the insert entered the history");

        await vm.UndoCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(groupsBefore), "undo removes the inserted locality");
            Assert.That(vm.InstallationNodes[0].Children.Count, Is.EqualTo(paneBefore), "the Installation pane reflects the undo");
            Assert.That(vm.FunctionNodes[0].Children.Count, Is.EqualTo(paneBefore), "the Functions pane reflects it identically");
            Assert.That(harness.Session.CanRedo, Is.True);
            Assert.That(vm.StatusText, Is.EqualTo("Undid the last change."));
        });

        await vm.RedoCommand.ExecuteAsync(null);
        Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(groupsBefore + 1), "redo re-applies the insert");
    }

    [Test]
    public async Task Undo_EmptyHistory_IsNoOp()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var before = harness.Session.Current;

        var undone = await harness.Session.UndoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(undone, Is.False, "a fresh project has nothing to undo");
            Assert.That(harness.Session.Current, Is.SameAs(before), "the project is unchanged");
        });
    }

    [Test]
    public async Task NewEdit_AfterUndo_ClearsRedo()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddLocalityAsync();
        await harness.Session.UndoAsync();
        Assert.That(harness.Session.CanRedo, Is.True);

        await harness.Session.AddLocalityAsync();   // a new edit after the undo

        Assert.That(harness.Session.CanRedo, Is.False, "the undone change can no longer be redone");
    }

    [Test]
    public async Task Undo_CascadingDelete_ReversesAsOneStep()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);   // make the locality non-empty
        harness.Dialogs.ConfirmResult = true;                    // confirm the non-empty delete
        await harness.Session.DeleteLocalityAsync(loc);
        Assert.That(harness.Session.Current!.FindById(loc), Is.Null, "the locality (and its block) are gone");

        var undone = await harness.Session.UndoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(undone, Is.True);
            var restored = harness.Session.Current!.FindById(loc);
            Assert.That(restored, Is.Not.Null, "one undo restores the locality");
            Assert.That(restored!.ChildrenOrEmpty().Any(c => c.Tag == "functionblock"), Is.True,
                "and its function block — the cascade is reversed as a unit");
        });
    }

    // US-052: undo/redo spans editing epics — here a function-block variable authoring edit (E7).
    [Test]
    public async Task Undo_Redo_SpansEditingEpics()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var block = harness.Session.Current!.FindById(loc)!.ChildrenOrEmpty().First(c => c.Tag == "functionblock");
        var settings = block.FindChild("settings")!.Id!.Value;
        await harness.Session.AddVariableAsync(settings, "resource_flag", "Away");

        await harness.Session.UndoAsync();
        var afterUndo = harness.Session.Current!.FindById(settings)!.ChildrenOrEmpty().Count(c => c.Tag == "resource_flag");
        await harness.Session.RedoAsync();
        var afterRedo = harness.Session.Current!.FindById(settings)!.ChildrenOrEmpty().Count(c => c.Tag == "resource_flag");

        Assert.Multiple(() =>
        {
            Assert.That(afterUndo, Is.EqualTo(0), "undo removes the authored variable");
            Assert.That(afterRedo, Is.EqualTo(1), "redo restores it");
        });
    }

    // US-052: a load (New) starts a fresh, empty edit history.
    [Test]
    public async Task NewProject_ResetsHistory()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddLocalityAsync();
        Assert.That(harness.Session.CanUndo, Is.True);

        await harness.Session.NewAsync();

        Assert.That(harness.Session.CanUndo, Is.False, "a new project has no edit history");
    }
}
