using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Services;

namespace safe_visual_tests;

/// <summary>
/// The dirty flag tracks the saved state rather than latching (US-052 + US-004): a project that has been edited
/// and then undone back to what is on disk is not modified, and must not provoke a "save changes?" prompt on
/// close. Dirtiness is a comparison against the last saved snapshot, not a one-way flag.
/// </summary>
public class SavePointTests
{
    /// <summary>Saves the session to a temp file so there is a real save point on disk.</summary>
    private static async Task<string> SaveToTempAsync(ShellHarness harness, string name)
    {
        string path = harness.TempPath(name);
        harness.Dialogs.SavePath = path;
        await harness.Session.SaveAsAsync();
        return path;
    }

    /// <summary>The defect: undo restored the saved project but left IsDirty latched at true.</summary>
    [Test]
    public async Task Undo_BackToSavedState_IsNotDirty()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await SaveToTempAsync(harness, "savepoint.vis");
        Assert.That(harness.Session.IsDirty, Is.False, "precondition: a freshly saved project is clean");

        await harness.Session.AddLocalityAsync();
        Assert.That(harness.Session.IsDirty, Is.True, "precondition: the edit made it dirty");

        await harness.Session.UndoAsync();

        Assert.That(harness.Session.IsDirty, Is.False,
            "undoing back to the saved snapshot leaves a project identical to the file on disk");
    }

    /// <summary>Redoing away from the saved state must make it dirty again — the flag tracks, it does not just clear.</summary>
    [Test]
    public async Task Redo_AwayFromSavedState_IsDirtyAgain()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await SaveToTempAsync(harness, "savepoint.vis");
        await harness.Session.AddLocalityAsync();
        await harness.Session.UndoAsync();

        await harness.Session.RedoAsync();

        Assert.That(harness.Session.IsDirty, Is.True, "the redone edit is again not on disk");
    }

    /// <summary>The user-visible consequence: no spurious save prompt when closing an undone-back-to-saved project.</summary>
    [Test]
    public async Task Close_AfterUndoBackToSavedState_DoesNotPromptToSave()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await SaveToTempAsync(harness, "savepoint.vis");
        await harness.Session.AddLocalityAsync();
        await harness.Session.UndoAsync();
        harness.Dialogs.SaveChangesResult = SaveChangesResult.Cancel;

        bool proceeded = await harness.Session.NewAsync();

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.ConfirmSaveCalls, Is.EqualTo(0),
                "a project matching the file on disk has nothing to prompt about");
            Assert.That(proceeded, Is.True);
        });
    }

    /// <summary>
    /// Saving mid-history moves the save point: the state that is now on disk is the clean one, and undoing away
    /// from it is dirty even though that older snapshot was itself saved earlier.
    /// </summary>
    [Test]
    public async Task Save_MovesTheSavePoint_SoUndoingAwayFromItIsDirty()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        await SaveToTempAsync(harness, "savepoint.vis");
        await harness.Session.AddLocalityAsync();

        // The edited project is now the one on disk.
        await harness.Session.SaveAsync();
        Assert.That(harness.Session.IsDirty, Is.False, "precondition: saving the edit made it clean");

        await harness.Session.UndoAsync();

        Assert.That(harness.Session.IsDirty, Is.True,
            "undoing to the pre-edit snapshot now differs from the file on disk");
    }

    /// <summary>A never-saved project starts clean, so undoing back to its initial state is clean too.</summary>
    [Test]
    public async Task Undo_BackToInitialStateOfNeverSavedProject_IsNotDirty()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        Assert.That(harness.Session.IsDirty, Is.False, "precondition: a new project starts clean");

        await harness.Session.AddLocalityAsync();
        await harness.Session.UndoAsync();

        Assert.That(harness.Session.IsDirty, Is.False, "back to the state the project started in");
    }
}
