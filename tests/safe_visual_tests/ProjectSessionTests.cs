using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;

namespace safe_visual_tests;

/// <summary>
/// Lifecycle behaviour of <see cref="ProjectSession"/> (US-002/003/004/005/064): new/open/save, the dirty flag,
/// the single-project constraint, the save prompt, and the crash-recovery backup — all headless and file-only.
/// </summary>
public class ProjectSessionTests
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
        harness.Session.MarkChanged();
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
        harness.Session.MarkChanged();
        bool saved = await harness.Session.SaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(harness.Session.IsDirty, Is.False);
            Assert.That(harness.Session.DocumentName, Is.EqualTo("proj.vis"));
        });
    }

    [Test]
    public async Task MarkChanged_SetsDirty_AndSaveClearsIt()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        harness.Dialogs.SavePath = harness.TempPath("proj.vis");

        harness.Session.MarkChanged();
        Assert.That(harness.Session.IsDirty, Is.True);

        await harness.Session.SaveAsync();
        Assert.That(harness.Session.IsDirty, Is.False);
    }

    [Test]
    public async Task New_WithUnsavedChanges_Cancelled_KeepsCurrentProject()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        harness.Session.MarkChanged();
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
        harness.Session.MarkChanged();
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

    [Test]
    public async Task AutoBackup_OnEveryNthChange_WritesRecoveryFile()
    {
        using var harness = ShellHarness.Create(changeThreshold: 3);
        await harness.Session.StartAsync();

        await harness.Session.MarkChangedAsync();
        await harness.Session.MarkChangedAsync();
        Assert.That(harness.Backup.HasRecovery(), Is.False, "no backup before the threshold is reached");

        await harness.Session.MarkChangedAsync();
        Assert.That(harness.Backup.HasRecovery(), Is.True, "the 3rd change triggers a recovery backup");
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
        await harness.Session.MarkChangedAsync();
        await harness.Session.MarkChangedAsync();
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
