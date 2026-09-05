using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests;

/// <summary>
/// Lifecycle behaviour of <see cref="ProjectWorkflow"/> (US-002/003/004/064): new/open/save/close, the dirty flag,
/// the single-project constraint and the save prompt — all headless and file-only.
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
            Assert.That(harness.Session.DocumentName, Is.EqualTo("unavngivet"));
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

        bool saved = (await harness.Session.SaveAsAsync()).IsOk;

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
        bool saved = (await harness.Session.SaveAsync()).IsOk;

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

        bool result = (await harness.Session.NewAsync()).IsOk;

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

        bool result = (await harness.Session.NewAsync()).IsOk;

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(harness.Session.IsDirty, Is.False);
            Assert.That(harness.Session.DocumentName, Is.EqualTo("unavngivet"));
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
        Assert.That(harness.Session.DocumentName, Is.EqualTo("unavngivet"));

        bool opened = (await harness.Session.OpenAsync(path)).IsOk;

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

        bool ok = (await harness.Session.SaveFunctionBlockAsync(fbId, badPath, "Doomed", "note")).IsOk;

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

        bool ok = (await harness.Session.SaveFunctionBlockAsync(fbId, path, "Reusable", "note")).IsOk;

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(File.Exists(path), Is.True, "the .ifb was written");
            Assert.That(harness.Session.Current!.FindById(fbId)!.GetAttribute("locked"), Is.EqualTo("yes"),
                "the in-project block became a locked library instance");
            Assert.That(harness.Session.Version, Is.EqualTo(versionBefore + 1),
                "the transform committed as exactly one step");
        });

        bool undone = await harness.Session.UndoAsync() is { Status: EditStatus.Committed };

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

        bool undone = await harness.Session.UndoAsync() is { Status: EditStatus.Committed };
        Assert.Multiple(() =>
        {
            Assert.That(undone, Is.True);
            Assert.That(harness.Session.LastChange, Is.Not.Null,
                "undo surfaces its change set so the reconciler can work in place (crudarch G3)");
            Assert.That(harness.Session.LastChange!.Origin, Is.EqualTo("undo"));
        });

        bool redone = await harness.Session.RedoAsync() is { Status: EditStatus.Committed };
        Assert.Multiple(() =>
        {
            Assert.That(redone, Is.True);
            Assert.That(harness.Session.LastChange, Is.Not.Null, "redo surfaces its change set (crudarch G3)");
            Assert.That(harness.Session.LastChange!.Origin, Is.EqualTo("redo"));
        });
    }

    [Test]
    public async Task Close_ReturnsToAFreshEmptyProject()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        harness.Dialogs.SavePath = harness.TempPath("closed.vis");
        await harness.Session.SaveAsAsync();

        // Not dirty, so no save prompt.
        bool closed = (await harness.Session.CloseAsync()).IsOk;

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.True);
            Assert.That(harness.Session.FilePath, Is.Null, "a close leaves no file behind");
            Assert.That(harness.Session.DocumentName, Is.EqualTo("unavngivet"));
            Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(10), "the standard empty project is opened");
        });
    }

    [Test]
    public async Task Save_MarksTheDocumentClean()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        harness.Dialogs.SavePath = harness.TempPath("saved.vis");

        await harness.Session.AddLocalityAsync();
        Assert.That(harness.Session.IsDirty, Is.True, "precondition: changes were recorded");

        bool saved = (await harness.Session.SaveAsAsync()).IsOk;

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(harness.Session.IsDirty, Is.False);
            Assert.That(harness.Session.FilePath, Is.EqualTo(harness.TempPath("saved.vis")));
        });
    }
}
