using System.Threading.Tasks;
using ihc_openvisual.Services;

namespace safe_visual_tests;

/// <summary>
/// The app-level consequence of the save-point dirty tracking (US-052 + US-004): a project edited then undone back
/// to what is on disk provokes no "save changes?" prompt on close. The dirty/save-point <b>semantics</b> now live
/// in <c>safe_project_tests.SessionSavePointTests</c> (against <c>ProjectDocumentSession</c>, W2-16); this retains
/// only the UI-level wiring — the close flow consults the dirty flag, not a latch.
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
}
