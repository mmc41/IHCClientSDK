using System.Threading.Tasks;

namespace Ihc.Vis.Tests;

/// <summary>US-052 / E14 — the app-level (UI) face of undo/redo: both panes reflect an undo/redo, and the status
/// bar + Edit menu name the action. The undo/redo/history <b>semantics</b> (empty-history no-op, redo invalidation,
/// Open resets history, cascading-delete one-step, authoring round-trip, the unlock→undo standing regression) now
/// live in <c>safe_project_tests.SessionHistoryTests</c> (against <c>ProjectDocumentSession</c>, W2-16).</summary>
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
            Assert.That(vm.StatusText, Is.EqualTo("Fortrød: Indsæt lokalitet"), "the undo status names the action (E14)");
        });

        await vm.RedoCommand.ExecuteAsync(null);
        Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(groupsBefore + 1), "redo re-applies the insert");
    }

    // E14 (W2-14): the history names its actions — the session reports the label of the edit undo/redo would touch,
    // and the VM surfaces it in the status text.
    [Test]
    public async Task History_NamesTheActionToUndoAndRedo()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddLocalityAsync();   // command label: "Indsæt lokalitet"

        Assert.That(harness.Session.UndoLabel, Is.EqualTo("Indsæt lokalitet"), "the session names the edit to undo");

        await vm.UndoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Does.Contain("Indsæt lokalitet"), "the undo status names the action");
            Assert.That(harness.Session.UndoLabel, Is.Null, "nothing left to undo");
            Assert.That(harness.Session.RedoLabel, Is.EqualTo("Indsæt lokalitet"), "and redo names the same action");
        });
    }

    // E14 (W2-14): the Edit ▸ Undo/Redo menu headers name the action, and fall back to bare "Undo"/"Redo" when empty.
    [Test]
    public async Task EditMenu_UndoRedoHeaders_NameTheAction()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Assert.That(vm.UndoMenuHeader, Is.EqualTo("_Fortryd"), "no history yet → bare header");

        await harness.Session.AddLocalityAsync();
        Assert.That(vm.UndoMenuHeader, Does.Contain("Indsæt lokalitet"), "the Undo header names the action");

        await vm.UndoCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(vm.UndoMenuHeader, Is.EqualTo("_Fortryd"), "nothing left to undo → bare header");
            Assert.That(vm.RedoMenuHeader, Does.Contain("Indsæt lokalitet"), "the Redo header names the re-applyable action");
        });
    }
}
