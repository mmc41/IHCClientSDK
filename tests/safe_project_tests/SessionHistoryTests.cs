using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-16: the multi-level undo/redo history semantics (US-052) migrated from the app-level
    /// <c>safe_visual_tests.EditHistoryTests</c> down onto <see cref="ProjectDocumentSession"/> — empty-history
    /// no-op, redo invalidated by a new edit, a fresh Open resets history, a cascading delete reverses as one step,
    /// an authoring edit round-trips, and the ⭐ unlock→undo standing regression (re-lock + session stays alive).
    /// Controller-free, no ShellHarness. The app-level file keeps only its UI concerns (pane refresh, E14 labels/menu).
    /// </summary>
    public class SessionHistoryTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        private static bool IsLocked(ProjectDocumentSession session, ElementId id) =>
            session.Current!.FindById(id)!.GetAttribute("locked") == "yes";

        [Test]   // from EditHistoryTests.Undo_EmptyHistory_IsNoOp
        public async Task Undo_EmptyHistory_IsNoOp()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            Project before = session.Current!;

            EditOutcome undone = session.Undo();

            Assert.Multiple(() =>
            {
                Assert.That(undone.Status, Is.EqualTo(EditStatus.NoChange), "a fresh history has nothing to undo");
                Assert.That(session.Current, Is.SameAs(before), "the project is unchanged");
            });
        }

        [Test]   // from EditHistoryTests.NewEdit_AfterUndo_ClearsRedo
        public async Task NewEdit_AfterUndo_ClearsRedo()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            session.Apply(new AddLocality("Alpha"));
            session.Undo();
            Assert.That(session.CanRedo, Is.True);

            session.Apply(new AddLocality("Beta"));   // a new edit after the undo

            Assert.That(session.CanRedo, Is.False, "the undone change can no longer be redone");
        }

        [Test]   // from EditHistoryTests.NewProject_ResetsHistory
        public async Task Open_ResetsHistory()
        {
            // Both projects are loaded up front: the session is thread-affine, so no await may sit between session
            // calls (a resumed continuation on another threadpool thread would trip VerifyAccess).
            Project first = await Load("project3-KompleksWired.vis");
            Project second = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(first);
            session.Apply(new AddLocality("Alpha"));
            Assert.That(session.CanUndo, Is.True);

            session.Open(second);   // a fresh open starts an empty history

            Assert.That(session.CanUndo, Is.False, "a freshly opened project has no edit history");
        }

        [Test]   // from EditHistoryTests.Undo_CascadingDelete_ReversesAsOneStep
        public async Task Undo_CascadingDelete_ReversesAsOneStep()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement group = project.Groups.First(g => g.ChildrenOrEmpty().Any());
            ElementId loc = group.Id!.Value;
            int contentBefore = group.ChildrenOrEmpty().Count();
            ProjectDocumentSession session = Session(project);

            session.Apply(new DeleteLocality(loc));
            Assert.That(session.Current!.FindById(loc), Is.Null, "the locality and its contents are gone");

            session.Undo();

            ProjectElement? restored = session.Current!.FindById(loc);
            Assert.Multiple(() =>
            {
                Assert.That(restored, Is.Not.Null, "one undo restores the locality");
                Assert.That(restored!.ChildrenOrEmpty().Count(), Is.EqualTo(contentBefore),
                    "and its contents — the cascade is reversed as a unit");
            });
        }

        [Test]   // from EditHistoryTests.Undo_Redo_SpansEditingEpics (variable authoring)
        public async Task Undo_Redo_VariableAuthoring_RoundTrips()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId fbId = project.Root.Descendants().First(e => e.Tag == "functionblock" && e.Id is not null).Id!.Value;
            ProjectDocumentSession session = Session(project);
            session.Apply(new UnlockFunctionBlock(fbId, "Test Installer", new DateOnly(2026, 1, 1)));   // setup: project3's blocks are locked; unlock to author

            session.Apply(new AddVariable(fbId, "settings", "resource_flag", "Away"));
            int afterAdd = CountFlags(session, fbId);
            session.Undo();
            int afterUndo = CountFlags(session, fbId);
            session.Redo();
            int afterRedo = CountFlags(session, fbId);

            Assert.Multiple(() =>
            {
                Assert.That(afterAdd, Is.EqualTo(afterUndo + 1), "the variable was authored");
                Assert.That(afterRedo, Is.EqualTo(afterAdd), "undo removes it, redo restores it");
            });
        }

        // ⭐ standing regression (US-020 + US-052): unlock a library function block, undo, and it must re-lock while
        // the session keeps accepting edits. IHC Visual crashes on this exact gesture; the SDK session must not.
        [Test]   // from EditHistoryTests.Unlock_ThenUndo_ReLocksBlock_AndSessionStaysAlive
        public async Task Unlock_ThenUndo_ReLocksBlock_AndSessionStaysAlive()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId fbId = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes" && e.Id is not null).Id!.Value;
            ProjectDocumentSession session = Session(project);
            Assert.That(IsLocked(session, fbId), Is.True, "precondition: a library function block starts locked");

            session.Apply(new UnlockFunctionBlock(fbId, "Test Installer", new DateOnly(2026, 1, 1)));
            Assert.That(IsLocked(session, fbId), Is.False, "precondition: unlock cleared the lock");

            EditOutcome undone = session.Undo();

            Assert.Multiple(() =>
            {
                Assert.That(undone.Status, Is.EqualTo(EditStatus.Committed), "the unlock was a reversible history entry");
                Assert.That(IsLocked(session, fbId), Is.True, "one undo re-locks the block");
            });

            // The session is still alive — it keeps committing edits after the undo (this is where the vendor crashes).
            session.Apply(new AddLocality("After"));
            Assert.That(session.CanUndo, Is.True, "the session still commits edits after the unlock-undo");
        }

        private static int CountFlags(ProjectDocumentSession session, ElementId fbId) =>
            session.Current!.FindById(fbId)!.FindChild("settings")!.ChildrenOrEmpty().Count(c => c.Tag == "resource_flag");
    }
}
