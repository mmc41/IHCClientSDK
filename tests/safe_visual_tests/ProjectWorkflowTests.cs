using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;

namespace safe_visual_tests;

/// <summary>
/// Lifecycle behaviour of <see cref="ProjectWorkflow"/> (US-002/003/004/005/064): new/open/save, the dirty flag,
/// the single-project constraint, the save prompt, and the crash-recovery backup — all headless and file-only.
/// </summary>
public class ProjectWorkflowTests
{
    [Test]
    public async Task StartAsync_CleanState_OpensStandardEmptyProjectWithTenLocalities()
    {
        using var harness = ShellHarness.Create();

        await harness.Session.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current, Is.Not.Null);
            Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(10), "the standard empty project has ten default rooms");
            Assert.That(harness.Session.DocumentName, Is.EqualTo("Untitled"));
            Assert.That(harness.Session.IsDirty, Is.False);
        });
    }

    [Test]
    public async Task SaveAs_WritesFile_SetsDocumentName_AndClearsDirty()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await harness.Session.AddLocalityAsync();
        string path = harness.TempPath("StandardHouse_1.vis");
        harness.Dialogs.SavePath = path;

        bool saved = await harness.Session.SaveAsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(harness.Session.DocumentName, Is.EqualTo("StandardHouse_1.vis"));
            Assert.That(harness.Session.IsDirty, Is.False);
        });
    }

    [Test]
    public async Task Save_AfterNamed_ReusesExistingFile_WithoutPicker()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        harness.Dialogs.SavePath = harness.TempPath("proj.vis");
        await harness.Session.SaveAsAsync();

        // No SavePath is offered now; a plain Save must go to the already-known file.
        harness.Dialogs.SavePath = null;
        await harness.Session.AddLocalityAsync();
        bool saved = await harness.Session.SaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(harness.Session.IsDirty, Is.False);
            Assert.That(harness.Session.DocumentName, Is.EqualTo("proj.vis"));
        });
    }

    [Test]
    public async Task Edit_SetsDirty_AndSaveClearsIt()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        harness.Dialogs.SavePath = harness.TempPath("proj.vis");

        await harness.Session.AddLocalityAsync();
        Assert.That(harness.Session.IsDirty, Is.True);

        await harness.Session.SaveAsync();
        Assert.That(harness.Session.IsDirty, Is.False);
    }

    [Test]
    public async Task New_WithUnsavedChanges_Cancelled_KeepsCurrentProject()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await harness.Session.AddLocalityAsync();
        var before = harness.Session.Current;
        harness.Dialogs.SaveChangesResult = SaveChangesResult.Cancel;

        bool result = await harness.Session.NewAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(harness.Session.Current, Is.SameAs(before));
            Assert.That(harness.Session.IsDirty, Is.True, "cancelling the prompt leaves the unsaved project intact");
            Assert.That(harness.Dialogs.ConfirmSaveCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task New_WithUnsavedChanges_Discarded_CreatesFreshProject()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await harness.Session.AddLocalityAsync();
        harness.Dialogs.SaveChangesResult = SaveChangesResult.Discard;

        bool result = await harness.Session.NewAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(harness.Session.IsDirty, Is.False);
            Assert.That(harness.Session.DocumentName, Is.EqualTo("Untitled"));
            Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Open_ReplacesCurrentProject_AndRecordsRecent()
    {
        using var harness = ShellHarness.Create();
        string path = harness.TempPath("existing.vis");

        // Author a file, then return to a fresh Untitled project so opening is a genuine replacement.
        await harness.Session.StartAsync();
        harness.Dialogs.SavePath = path;
        await harness.Session.SaveAsAsync();
        await harness.Session.NewAsync();
        Assert.That(harness.Session.DocumentName, Is.EqualTo("Untitled"));

        bool opened = await harness.Session.OpenAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(opened, Is.True);
            Assert.That(harness.Session.DocumentName, Is.EqualTo("existing.vis"));
            Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(10));
            Assert.That(harness.Session.IsDirty, Is.False);
            Assert.That(harness.Recent.Items, Does.Contain(Path.GetFullPath(path)));
        });
    }

    // crudarch T009 (D05 failure ordering): the export runs FIRST, so a failed .ifb write leaves the document
    // completely untouched — same snapshot, still clean, no history entry, no version bump.
    [Test]
    public async Task SaveFunctionBlock_FailedExport_LeavesDocumentUntouched()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var loc = harness.Session.Current!.Groups[0].Id!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        harness.Dialogs.SavePath = harness.TempPath("clean.vis");
        await harness.Session.SaveAsAsync();   // start from a clean save point so "still not dirty" is crisp
        var currentBefore = harness.Session.Current;
        int versionBefore = harness.Session.Version;
        string? undoLabelBefore = harness.Session.UndoLabel;
        string badPath = harness.TempPath(Path.Combine("no-such-dir", "out.ifb"));   // export must fail

        bool ok = await harness.Session.SaveFunctionBlockAsync(fbId, badPath, "Doomed", "note");

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False, "a failed export reports failure");
            Assert.That(File.Exists(badPath), Is.False, "nothing was written");
            Assert.That(harness.Session.Current, Is.SameAs(currentBefore), "the document snapshot is untouched");
            Assert.That(harness.Session.IsDirty, Is.False, "the document is still clean");
            Assert.That(harness.Session.Version, Is.EqualTo(versionBefore), "no commit happened");
            Assert.That(harness.Session.UndoLabel, Is.EqualTo(undoLabelBefore), "no history entry was added");
        });
    }

    // crudarch T009 (D05 success shape): the save-to-library transform is exactly ONE undoable step, and one
    // undo restores the prior unlocked block.
    [Test]
    public async Task SaveFunctionBlock_Success_IsOneUndoableStep_UndoRestoresUnlockedBlock()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        var loc = harness.Session.Current!.Groups[0].Id!.Value;
        // An EMPTY block inserts editable (US-019) — the natural US-021 subject; a catalog library block would
        // already be locked, making "undo restores the unlocked block" vacuous.
        var fbId = (await harness.Session.AddEmptyFunctionBlockAsync(loc))!.Value;
        Assert.That(harness.Session.Current!.FindById(fbId)!.GetAttribute("locked"), Is.Not.EqualTo("yes"),
            "precondition: the authored block starts unlocked");
        int versionBefore = harness.Session.Version;
        string path = harness.TempPath("Reusable.ifb");

        bool ok = await harness.Session.SaveFunctionBlockAsync(fbId, path, "Reusable", "note");

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(File.Exists(path), Is.True, "the .ifb was written");
            Assert.That(harness.Session.Current!.FindById(fbId)!.GetAttribute("locked"), Is.EqualTo("yes"),
                "the in-project block became a locked library instance");
            Assert.That(harness.Session.Version, Is.EqualTo(versionBefore + 1),
                "the transform committed as exactly one step");
        });

        bool undone = await harness.Session.UndoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(undone, Is.True);
            Assert.That(harness.Session.Current!.FindById(fbId)!.GetAttribute("locked"), Is.Not.EqualTo("yes"),
                "one undo restores the prior unlocked block");
        });
    }

    // What the WORKFLOW layer owns for the two reorder probes: the closed-document answer (its own null guard) and
    // that an open document is actually forwarded to. Which pairs/deltas reorder — and that the port agrees with the
    // gateway — is the SDK's rule, owned by ProjectDocumentPortTests' case matrices, not restated here (review F19).
    [Test]
    public async Task ReorderProbes_FalseWhenClosed_ThenDelegateToTheDocument()
    {
        using var harness = ShellHarness.Create();
        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.CanReorderNode(default, default), Is.False,
                "before any project is open the drag-over probe answers false");
            Assert.That(harness.Session.CanReorder(default, -1), Is.False,
                "…and so does the delta probe the Move up/down gates use");
        });

        await harness.Session.StartAsync();
        var project = harness.Session.Current!;
        Ihc.Vis.Model.ElementId g0 = project.Groups[0].Id!.Value;
        Ihc.Vis.Model.ElementId g1 = project.Groups[1].Id!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.CanReorderNode(g0, g1), Is.True,
                "with a document open the drag-over probe is forwarded to it");
            Assert.That(harness.Session.CanReorder(g1, -1), Is.True,
                "…as is the delta probe");
        });
    }

    [Test]
    public async Task UndoAndRedo_SurfaceLastChange_ForInPlaceReconciliation()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await harness.Session.AddLocalityAsync();

        bool undone = await harness.Session.UndoAsync();
        Assert.Multiple(() =>
        {
            Assert.That(undone, Is.True);
            Assert.That(harness.Session.LastChange, Is.Not.Null,
                "undo surfaces its change set so the reconciler can work in place (crudarch G3)");
            Assert.That(harness.Session.LastChange!.Origin, Is.EqualTo("undo"));
        });

        bool redone = await harness.Session.RedoAsync();
        Assert.Multiple(() =>
        {
            Assert.That(redone, Is.True);
            Assert.That(harness.Session.LastChange, Is.Not.Null, "redo surfaces its change set (crudarch G3)");
            Assert.That(harness.Session.LastChange!.Origin, Is.EqualTo("redo"));
        });
    }

    [Test]
    public async Task AutoBackup_OnEveryNthChange_WritesRecoveryFile()
    {
        using var harness = ShellHarness.Create(changeThreshold: 3);
        await harness.Session.StartAsync();

        await harness.Session.AddLocalityAsync();
        await harness.Session.AddLocalityAsync();
        Assert.That(harness.Backup.HasRecovery(), Is.False, "no backup before the threshold is reached");

        await harness.Session.AddLocalityAsync();
        Assert.That(harness.Backup.HasRecovery(), Is.True, "the 3rd change triggers a recovery backup");
    }

    // Automation needs a deterministic launch: the --skip-recovery flag must open a fresh project and discard
    // any stale crash backup WITHOUT ever showing the "Recover project?" dialog that would otherwise block an
    // unattended (UI-automation) session.
    [Test]
    public async Task StartAsync_SkipRecovery_DiscardsBackup_WithoutPrompting()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await harness.Session.AutoBackupAsync();
        Assert.That(harness.Backup.HasRecovery(), Is.True, "precondition: a crash backup exists");

        // A fresh session over the same backup directory (as if the app relaunched), started with recovery skipped.
        using var restarted = ShellHarness.Restart(harness.TempDir);
        restarted.Dialogs.ConfirmResult = true;   // it would recover if it were ever asked

        await restarted.Session.StartAsync(skipRecovery: true);

        Assert.Multiple(() =>
        {
            Assert.That(restarted.Dialogs.ConfirmCalls, Is.EqualTo(0), "the recovery dialog is never shown");
            Assert.That(restarted.Backup.HasRecovery(), Is.False, "the stale crash backup is discarded up front");
            Assert.That(restarted.Session.DocumentName, Is.EqualTo("Untitled"), "a fresh empty project is opened");
            Assert.That(restarted.Session.Current!.Groups.Count, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Close_DeletesRecoveryBackup()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await harness.Session.AutoBackupAsync();
        Assert.That(harness.Backup.HasRecovery(), Is.True);

        // Not dirty, so no save prompt; a clean close discards the crash backup.
        bool closed = await harness.Session.CloseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.True);
            Assert.That(harness.Backup.HasRecovery(), Is.False);
        });
    }

    [Test]
    public async Task Save_ClearsChangeCountAndDeletesRecoveryBackup()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        harness.Dialogs.SavePath = harness.TempPath("saved.vis");

        // Accumulate changes and a crash backup, then save: the save persists the work, so the stale
        // recovery backup must be discarded and the change counter reset (matching New/Open/Close).
        await harness.Session.AddLocalityAsync();
        await harness.Session.AddLocalityAsync();
        await harness.Session.AutoBackupAsync();
        Assert.That(harness.Backup.HasRecovery(), Is.True, "precondition: a crash backup exists");
        Assert.That(harness.Session.ChangeCount, Is.GreaterThan(0), "precondition: changes were recorded");

        bool saved = await harness.Session.SaveAsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(harness.Session.IsDirty, Is.False);
            Assert.That(harness.Session.ChangeCount, Is.EqualTo(0), "a save resets the change counter");
            Assert.That(harness.Backup.HasRecovery(), Is.False, "a save discards the now-stale crash backup");
        });
    }

    [Test]
    public async Task Start_WithExistingBackup_Confirmed_RecoversAsDirty()
    {
        using var harness = ShellHarness.Create();
        // Simulate a crash: write a recovery backup, then abandon the session without a clean close.
        await harness.Session.StartAsync();
        await harness.Session.AutoBackupAsync();
        harness.Session.Dispose();

        using var restart = ShellHarness.Restart(harness.TempDir);
        restart.Dialogs.ConfirmResult = true;
        await restart.Session.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restart.Session.Current, Is.Not.Null);
            Assert.That(restart.Session.Current!.Groups.Count, Is.EqualTo(10));
            Assert.That(restart.Session.IsDirty, Is.True, "a recovered project starts dirty so the user can re-save it");
        });
    }

    [Test]
    public async Task Start_WithExistingBackup_Declined_DiscardsBackupAndStartsEmpty()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await harness.Session.AutoBackupAsync();
        harness.Session.Dispose();

        using var restart = ShellHarness.Restart(harness.TempDir);
        restart.Dialogs.ConfirmResult = false;
        await restart.Session.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restart.Session.IsDirty, Is.False);
            Assert.That(restart.Session.DocumentName, Is.EqualTo("Untitled"));
            Assert.That(restart.Backup.HasRecovery(), Is.False, "declining recovery discards the backup");
        });
    }
}
