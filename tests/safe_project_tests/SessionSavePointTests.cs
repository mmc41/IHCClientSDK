using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-16: the dirty-flag / save-point semantics (US-052 + US-004) migrated from the app-level
    /// <c>safe_visual_tests.SavePointTests</c> down onto <see cref="ProjectDocumentSession"/> — dirtiness is a
    /// comparison against the last saved snapshot (<c>MarkSaved</c>), so undoing back onto the save point reads clean
    /// and redoing away reads dirty. Controller-free, no ShellHarness. The app-level test keeps only its UI concern
    /// (no close-prompt when clean).
    /// </summary>
    public class SessionSavePointTests : SessionCommandFixture
    {
        [Test]   // from SavePointTests.Undo_BackToSavedState_IsNotDirty
        public async Task Undo_BackToSavedState_IsNotDirty()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            session.MarkSaved(session.Current!);
            Assert.That(session.IsDirty, Is.False, "precondition: a freshly saved project is clean");

            session.Apply(new AddLocality("Alpha"));
            Assert.That(session.IsDirty, Is.True, "precondition: the edit made it dirty");

            session.Undo();
            Assert.That(session.IsDirty, Is.False, "undoing back to the saved snapshot is clean");
        }

        [Test]   // from SavePointTests.Redo_AwayFromSavedState_IsDirtyAgain
        public async Task Redo_AwayFromSavedState_IsDirtyAgain()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            session.MarkSaved(session.Current!);
            session.Apply(new AddLocality("Alpha"));
            session.Undo();

            session.Redo();
            Assert.That(session.IsDirty, Is.True, "the redone edit is again not on the save point");
        }

        [Test]   // from SavePointTests.Save_MovesTheSavePoint_SoUndoingAwayFromItIsDirty
        public async Task Save_MovesTheSavePoint_SoUndoingAwayFromItIsDirty()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            session.Apply(new AddLocality("Alpha"));
            session.MarkSaved(session.Current!);   // the edited project is now what the file holds
            Assert.That(session.IsDirty, Is.False, "precondition: saving the edit made it clean");

            session.Undo();
            Assert.That(session.IsDirty, Is.True, "undoing to the pre-edit snapshot now differs from the save point");
        }

        [Test]   // from SavePointTests.Undo_BackToInitialStateOfNeverSavedProject_IsNotDirty
        public async Task Undo_BackToInitialState_IsNotDirty()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            Assert.That(session.IsDirty, Is.False, "precondition: a freshly opened project is clean");

            session.Apply(new AddLocality("Alpha"));
            session.Undo();
            Assert.That(session.IsDirty, Is.False, "back to the state the project started in");
        }

        [Test]   // from SavePointTests.UndoPastSavePoint_IsDirty_ThenRedoBackToSavePoint_IsClean
        public async Task UndoPastSavePoint_IsDirty_ThenRedoBackToSavePoint_IsClean()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            session.Apply(new AddLocality("A"));          // → S1
            session.MarkSaved(session.Current!);          // save point = S1 (mid-history, not the initial snapshot)
            session.Apply(new AddLocality("B"));          // → S2
            Assert.That(session.IsDirty, Is.True, "precondition: edit B is not on the save point");

            session.Undo();                                // → S1, the save point
            Assert.That(session.IsDirty, Is.False, "undo onto the save point is clean");
            session.Undo();                                // → S0, below the save point
            Assert.That(session.IsDirty, Is.True, "undoing past the save point is dirty again");
            session.Redo();                                // → S1, back onto the save point
            Assert.That(session.IsDirty, Is.False, "redoing back to the save point restores the clean state");
        }
    }
}
