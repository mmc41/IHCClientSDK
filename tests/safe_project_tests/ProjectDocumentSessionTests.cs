using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-4: the ProjectDocumentSession apply/undo/redo pipeline — committed/no-change/refused/failed
    /// outcomes, labelled undo/redo with save-point-aware dirty tracking, the base-version guard, the save-race fix,
    /// and the D12 thread-affinity guard. Exercised with minimal in-test commands over a real oracle project.
    /// </summary>
    public class ProjectDocumentSessionTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static async Task<(ProjectDocumentSession Session, ElementId Locality, string OldName)> OpenWithLocality()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement group = project.Groups.First();
            var session = new ProjectDocumentSession();
            session.Open(project);
            return (session, group.Id!.Value, group.GetAttribute("name") ?? "");
        }

        // ---- minimal in-test commands ----

        private sealed record RenameLocality(ElementId Id, string NewName) : ProjectCommand
        {
            internal override string Describe(Project project) =>
                "Omdøb " + (project.FindById(Id)?.GetAttribute("name") ?? "?");
            internal override EditVerdict Evaluate(EditContext context) =>
                context.Index.FindById(Id) is not null ? EditVerdict.Allow : EditVerdict.Refuse(EditRefusalCodes.TargetMissing, "no such element");
            internal override void Execute(ProjectEditor editor)
            {
                if (editor.TryResolve(Id, out ElementRef? handle))
                {
                    handle.SetAttribute("name", NewName);
                }
            }
        }

        private sealed record NoOpCommand : ProjectCommand
        {
            internal override string Describe(Project project) => "No-op";
            internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;
            internal override void Execute(ProjectEditor editor) { }
        }

        private sealed record AlwaysRefuse(string Reason) : ProjectCommand
        {
            internal override string Describe(Project project) => "Refuse";
            internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Refuse(EditRefusalCodes.TargetMissing, Reason);
            internal override void Execute(ProjectEditor editor) => throw new InvalidOperationException("must not run");
        }

        /// <summary>A command whose Execute throws — the only way to reach the session's engine-fault exit.
        /// INTERNAL rather than private because more than one fixture needs a failing edit, and a second
        /// copy of it would be a second definition of what "the engine broke" means.</summary>
        internal sealed record ThrowingCommand(bool AsRefusal) : ProjectCommand
        {
            internal override string Describe(Project project) => "Throw";
            internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;
            internal override void Execute(ProjectEditor editor)
            {
                if (AsRefusal)
                {
                    throw new EditRefusedException("deep refusal");
                }
                throw new InvalidOperationException("engine boom");
            }
        }

        private sealed record RenameReturningId(ElementId Id, string NewName) : ProjectCommand<ElementId>
        {
            internal override string Describe(Project project) => "Rename returning id";
            internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;
            internal override ElementId ExecuteCore(ProjectEditor editor)
            {
                if (editor.TryResolve(Id, out ElementRef? handle))
                {
                    handle.SetAttribute("name", NewName);
                }
                return Id;
            }
        }

        // ---- tests ----

        [Test]
        public async Task Apply_Commits_BumpsVersion_AndRaisesChanged()
        {
            (ProjectDocumentSession session, ElementId loc, _) = await OpenWithLocality();
            int before = session.Version;
            ProjectChangeSet? raised = null;
            session.Changed += (_, e) => raised = e.Changes;

            EditOutcome outcome = session.Apply(new RenameLocality(loc, "Renamed Room"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Version, Is.EqualTo(before + 1));
                Assert.That(session.Current!.FindById(loc)!.GetAttribute("name"), Is.EqualTo("Renamed Room"));
                Assert.That(session.CanUndo, Is.True);
                Assert.That(raised, Is.Not.Null, "Changed fired with a change set");
            });
        }

        [Test]
        public async Task Apply_NoOp_ReturnsNoChange_HistoryUntouched()
        {
            (ProjectDocumentSession session, _, _) = await OpenWithLocality();
            int before = session.Version;

            EditOutcome outcome = session.Apply(new NoOpCommand());

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.NoChange));
                Assert.That(session.Version, Is.EqualTo(before), "a no-op does not bump the version");
                Assert.That(session.CanUndo, Is.False, "a no-op does not enter history");
            });
        }

        [Test]
        public async Task UndoRedo_RestoreSnapshotsAndLabels()
        {
            (ProjectDocumentSession session, ElementId loc, string oldName) = await OpenWithLocality();
            session.Apply(new RenameLocality(loc, "New Name"));

            Assert.That(session.UndoLabel, Is.EqualTo("Omdøb " + oldName), "the label used the pre-edit name (D10)");

            session.Undo();
            Assert.Multiple(() =>
            {
                Assert.That(session.Current!.FindById(loc)!.GetAttribute("name"), Is.EqualTo(oldName), "undo restores the old name");
                Assert.That(session.CanRedo, Is.True);
                Assert.That(session.RedoLabel, Is.EqualTo("Omdøb " + oldName));
            });

            session.Redo();
            Assert.That(session.Current!.FindById(loc)!.GetAttribute("name"), Is.EqualTo("New Name"), "redo re-applies it");
        }

        [Test]
        public async Task NewEdit_AfterUndo_ClearsRedo()
        {
            (ProjectDocumentSession session, ElementId loc, _) = await OpenWithLocality();
            session.Apply(new RenameLocality(loc, "A"));
            session.Undo();
            Assert.That(session.CanRedo, Is.True);

            session.Apply(new RenameLocality(loc, "B"));

            Assert.That(session.CanRedo, Is.False, "a fresh edit after undo clears the redo stack");
        }

        [Test]
        public async Task Undo_EmptyHistory_IsNoChange()
        {
            (ProjectDocumentSession session, _, _) = await OpenWithLocality();

            EditOutcome outcome = session.Undo();

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.NoChange));
        }

        [Test]
        public async Task BaseVersionMismatch_IsRefused()
        {
            (ProjectDocumentSession session, ElementId loc, _) = await OpenWithLocality();

            EditOutcome outcome = session.Apply(new RenameLocality(loc, "X"), baseVersion: session.Version + 5);

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(session.CanUndo, Is.False, "a refused stale commit changes nothing");
            });
        }

        [Test]
        public async Task NegativeVerdict_IsRefused_WithReason()
        {
            (ProjectDocumentSession session, _, _) = await OpenWithLocality();

            EditOutcome outcome = session.Apply(new AlwaysRefuse("not allowed here"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Is.EqualTo("not allowed here"));
            });
        }

        [Test]
        public async Task EngineException_IsFailed_ButEditRefusedException_IsRefused()
        {
            (ProjectDocumentSession session, _, _) = await OpenWithLocality();

            EditOutcome failed = session.Apply(new ThrowingCommand(AsRefusal: false));
            EditOutcome refused = session.Apply(new ThrowingCommand(AsRefusal: true));

            Assert.Multiple(() =>
            {
                Assert.That(failed.Status, Is.EqualTo(EditStatus.Failed), "a plain exception is a failure");
                Assert.That(failed.Reason, Is.EqualTo("engine boom"), "the message is preserved");
                Assert.That(refused.Status, Is.EqualTo(EditStatus.Refused), "EditRefusedException is a refusal");
            });
        }

        /// <summary>
        /// A faulted preview carries the captured fault, so a host can report the engine break without being
        /// handed the exception. The sentence is the PREVIEW's own, not the edit's: a preview commits nothing,
        /// so "the change was not saved" would describe a change that was never going to be made.
        /// </summary>
        [Test]
        public async Task Preview_ThatFaults_CarriesTheCapturedFaultAndItsOwnSentence()
        {
            (ProjectDocumentSession session, _, _) = await OpenWithLocality();

            PreviewOutcome faulted = session.Preview(new ThrowingCommand(AsRefusal: false));
            PreviewOutcome refused = session.Preview(new AlwaysRefuse("nope"));

            Assert.Multiple(() =>
            {
                Assert.That(faulted.Fault, Is.Not.Null);
                Assert.That(faulted.Fault!.Code.Value, Is.EqualTo("internal.preview-failed"),
                    "its OWN code, not the edit's — the two say different things about what the project is now");
                Assert.That(faulted.Fault.Message, Does.Contain("Projektet er uændret"),
                    "and its own sentence, which is the whole reason for a second code");
                Assert.That(faulted.Fault.Detail, Does.Contain("engine boom"),
                    "captured as text at the catch, where the exception still exists");
                Assert.That(faulted.Reason, Is.EqualTo("engine boom"),
                    "Reason still carries the engine's English message, unchanged");
                Assert.That(refused.Fault, Is.Null, "a refusal is the rules working and carries no fault");
            });
        }

        // M8/D05: the Preview mirror of the above. A bare `catch { return null; }` used to conflate an unexpected
        // engine FAULT with a legitimate refuse / no-change (all null); the typed PreviewOutcome distinguishes them.
        [Test]
        public async Task Preview_DistinguishesEngineFault_FromRefuseAndNoChange()
        {
            (ProjectDocumentSession session, _, _) = await OpenWithLocality();
            int before = session.Version;

            PreviewOutcome change = session.Preview(new AddLocality("Preview Loc"));
            PreviewOutcome noChange = session.Preview(new NoOpCommand());
            PreviewOutcome refused = session.Preview(new AlwaysRefuse("nope"));
            PreviewOutcome deepRefused = session.Preview(new ThrowingCommand(AsRefusal: true));
            PreviewOutcome faulted = session.Preview(new ThrowingCommand(AsRefusal: false));

            Assert.Multiple(() =>
            {
                Assert.That(change.Status, Is.EqualTo(PreviewStatus.WouldChange));
                Assert.That(change.Changes, Is.Not.Null, "a real change previews its delta");
                Assert.That(noChange.Status, Is.EqualTo(PreviewStatus.NoChange), "a no-op previews NoChange, not a fault");
                Assert.That(refused.Status, Is.EqualTo(PreviewStatus.Refused));
                Assert.That(refused.Reason, Is.EqualTo("nope"), "the refusal reason is preserved");
                Assert.That(deepRefused.Status, Is.EqualTo(PreviewStatus.Refused), "a deep-guard EditRefusedException is a refuse, not a fault");
                // the crux (D05/M8): an unexpected engine fault is its OWN status, not the swallowed null of a refuse/no-change
                Assert.That(faulted.Status, Is.EqualTo(PreviewStatus.Faulted), "an unexpected engine exception is a fault");
                Assert.That(faulted.Reason, Is.EqualTo("engine boom"), "the fault message is surfaced, not swallowed");
                Assert.That(faulted.Status, Is.Not.EqualTo(refused.Status).And.Not.EqualTo(noChange.Status),
                    "a fault is distinct from a refuse and a no-change (the bare-catch bug M8 fixed)");
                Assert.That(session.Version, Is.EqualTo(before), "preview commits nothing");
            });
        }

        [Test]
        public async Task Apply_Generic_SurfacesTheProducedValue()
        {
            (ProjectDocumentSession session, ElementId loc, _) = await OpenWithLocality();

            EditOutcome<ElementId> outcome = session.Apply(new RenameReturningId(loc, "Y"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(outcome.Value, Is.EqualTo(loc));
            });
        }

        [Test]
        public async Task SaveRace_MarkSavedWithTheWrittenSnapshot_KeepsDirtyWhenAnEditLanded()
        {
            (ProjectDocumentSession session, ElementId loc, _) = await OpenWithLocality();
            session.Apply(new RenameLocality(loc, "First"));
            Project written = session.Current!;                 // the snapshot a save would capture
            session.Apply(new RenameLocality(loc, "Second"));   // an edit lands "during" the save

            session.MarkSaved(written);

            Assert.That(session.IsDirty, Is.True, "the save wrote an older snapshot, so the project is still dirty");

            session.MarkSaved(session.Current!);
            Assert.That(session.IsDirty, Is.False, "marking the actual current snapshot saved clears dirty");
        }

        [Test]
        public async Task Dirty_ClearsWhenUndoCrossesTheSavePoint()
        {
            (ProjectDocumentSession session, ElementId loc, _) = await OpenWithLocality();   // opened clean
            Assert.That(session.IsDirty, Is.False, "precondition: a freshly opened project is clean");
            session.Apply(new RenameLocality(loc, "Edited"));
            Assert.That(session.IsDirty, Is.True);

            session.Undo();

            Assert.That(session.IsDirty, Is.False, "undoing back to the opened snapshot is clean again");
        }

        // The former ThreadAffinity_ApplyAndMarkSavedFromANonOwnerThread_Throw test is deliberately GONE: the
        // session is lock-serialized now (crudarch D04), so off-thread calls succeed instead of throwing —
        // SessionThreadingContractTests holds the replacement contract (no-throw reads + events on the mutating thread).
    }
}
