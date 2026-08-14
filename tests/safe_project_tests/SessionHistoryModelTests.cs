using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CsCheck;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Model-based tests for <see cref="ProjectDocumentSession"/>'s history. The existing session tests each drive
    /// one short fixed sequence, which is the right shape for pinning a single rule but cannot reach the states
    /// that only appear when the operations INTERLEAVE — a rollback under a redo stack, a save point left behind
    /// by an undo, a redo cleared by an apply three steps later.
    ///
    /// <para>The reference model is deliberately tiny: a list of snapshots with their labels, a redo list, a
    /// current snapshot, a save point and a version counter. It never asks the engine whether an edit was legal
    /// (that is the command's business, and the outcome status tells it), but it does independently remember WHICH
    /// snapshot each history slot holds — so an undo three steps later is checked against a project captured long
    /// before, not against whatever the session happens to return.</para>
    ///
    /// <para>The whole observable surface is compared after every single operation: current content, dirty, both
    /// enablement flags, both labels and the version. Each sequence then ends by draining the undo stack, which is
    /// the only way to observe its DEPTH from outside, and lands back on the project as opened.</para>
    /// </summary>
    public class SessionHistoryModelTests
    {
        private const string BaseProject = "testdata/projects/project3-KompleksWired.vis";

        private static ProjectAppService App => new(TestSetup.Settings);

        // ----- the operation alphabet -----

        private abstract record Op;
        private sealed record AddOp(string Name) : Op;
        private sealed record RenameOp(int Pick, string Name) : Op;
        private sealed record DeleteOp(int Pick) : Op;
        private sealed record ReorderOp(int Pick, int Position) : Op;
        private sealed record UndoOp : Op;
        private sealed record RedoOp : Op;
        private sealed record RollbackOp : Op;
        private sealed record MarkSavedOp : Op;
        private sealed record MarkSavedStaleOp(int Pick) : Op;

        private static readonly Gen<string> NameGen =
            Gen.OneOfConst("abcæø 09".ToCharArray()).Array[1, 5].Select(cs => new string(cs));

        private static readonly Gen<Op> AnyOp = Gen.OneOf(
            NameGen.Select(n => (Op)new AddOp(n)),
            Gen.Select(Gen.Int[0, 20], NameGen, (p, n) => (Op)new RenameOp(p, n)),
            Gen.Int[0, 20].Select(p => (Op)new DeleteOp(p)),
            Gen.Select(Gen.Int[0, 20], Gen.Int[0, 20], (p, pos) => (Op)new ReorderOp(p, pos)),
            Gen.Const((Op)new UndoOp()),
            Gen.Const((Op)new RedoOp()),
            Gen.Const((Op)new RollbackOp()),
            Gen.Const((Op)new MarkSavedOp()),
            Gen.Int[0, 20].Select(p => (Op)new MarkSavedStaleOp(p)));

        private static readonly Gen<Op[]> Sequence = AnyOp.Array[0, 12];

        private static ProjectCommand? EditCommand(Op op, Project project)
        {
            IReadOnlyList<ProjectElement> groups = project.Groups;
            return op switch
            {
                AddOp add => new AddLocality(add.Name),
                RenameOp r when groups.Count > 0 =>
                    new RenameLocality(groups[r.Pick % groups.Count].Id!.Value, r.Name, string.Empty),
                DeleteOp d when groups.Count > 0 => new DeleteLocality(groups[d.Pick % groups.Count].Id!.Value),
                ReorderOp ro when groups.Count > 0 =>
                    new ReorderNode(groups[ro.Pick % groups.Count].Id!.Value, ro.Position % groups.Count),
                _ => null,
            };
        }

        // ----- the reference model -----

        /// <summary>Snapshots plus a cursor plus a save point — the whole of what a document's history is.</summary>
        private sealed class Model
        {
            public readonly List<(Project Snapshot, string Label)> Undo = new();
            public readonly List<(Project Snapshot, string Label)> Redo = new();   // last = top
            public readonly List<Project> Seen = new();
            public Project Current = null!;
            public Project? SavePoint;
            public int Version;

            /// <summary>The retention cap, or null for an unlimited history.</summary>
            public int? Cap;
        }

        /// <summary>The model's half of the bounded policy: an entry added past the cap discards the OLDEST, which
        /// is what makes the deepest reachable undo a fixed distance behind the present rather than the start.</summary>
        private static void Trim(Model model, Counters counters)
        {
            while (model.Cap is { } cap && model.Undo.Count > cap)
            {
                model.Undo.RemoveAt(0);
                counters.Trimmed++;
            }
        }

        /// <summary>Content equality modulo the id allocator's high-water mark: undo restores content but keeps the
        /// allocator raised, and that bookkeeping alone is not an edit. Written out here rather than borrowed from
        /// the session so the model checks the session against an independent notion of "the same project".</summary>
        private static bool SameContent(Project a, Project b) => Bare(a).Equals(Bare(b));

        private static ProjectElement Bare(Project project) =>
            project.Root.GetAttribute("last_unique_id") is null
                ? project.Root
                : project.Root.WithAttribute("last_unique_id", ElementId.NullToken);

        private static long Allocator(Project project) =>
            project.LastUniqueId is { } token && token.StartsWith("_0x", System.StringComparison.Ordinal)
                && long.TryParse(token.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long value)
                ? value
                : 0;

        /// <summary>Every externally visible fact about the session, checked against the model. Returns a failure
        /// description, or null when the session agrees with the model.</summary>
        private static string? Disagreement(ProjectDocumentSession session, Model model, string after)
        {
            bool expectedDirty = !(model.SavePoint is { } savePoint && SameContent(model.Current, savePoint));
            string? expectedUndoLabel = model.Undo.Count > 0 ? model.Undo[^1].Label : null;
            string? expectedRedoLabel = model.Redo.Count > 0 ? model.Redo[^1].Label : null;

            if (!SameContent(session.Current!, model.Current))
            {
                return $"{after}: current snapshot differs from the model's";
            }
            if (session.CanUndo != model.Undo.Count > 0)
            {
                return $"{after}: CanUndo {session.CanUndo}, model depth {model.Undo.Count}";
            }
            if (session.CanRedo != model.Redo.Count > 0)
            {
                return $"{after}: CanRedo {session.CanRedo}, model depth {model.Redo.Count}";
            }
            if (session.UndoLabel != expectedUndoLabel)
            {
                return $"{after}: UndoLabel '{session.UndoLabel}', model '{expectedUndoLabel}'";
            }
            if (session.RedoLabel != expectedRedoLabel)
            {
                return $"{after}: RedoLabel '{session.RedoLabel}', model '{expectedRedoLabel}'";
            }
            if (session.Version != model.Version)
            {
                return $"{after}: Version {session.Version}, model {model.Version}";
            }
            if (session.IsDirty != expectedDirty)
            {
                return $"{after}: IsDirty {session.IsDirty}, model {expectedDirty}";
            }
            return null;
        }

        /// <summary>How often each transition actually fired across a whole sample run. A model-based test whose
        /// sequences all no-op agrees with the model perfectly and proves nothing, so the run asserts that every
        /// transition it claims to cover was genuinely reached.</summary>
        private sealed class Counters
        {
            public int Applied;
            public int Undone;
            public int Redone;
            public int RolledBack;
            public int SavedStale;
            public int DirtyAfterSave;
            public int Trimmed;
        }

        /// <summary>Runs one operation against both the session and the model, returning a disagreement or null.</summary>
        private static string? Step(ProjectDocumentSession session, Model model, Op op, Counters counters)
        {
            switch (op)
            {
                case UndoOp:
                {
                    Project before = model.Current;
                    EditOutcome outcome = session.Undo();
                    if (model.Undo.Count == 0)
                    {
                        if (outcome.Status != EditStatus.NoChange)
                        {
                            return "undo on an empty history must be a no-op";
                        }
                        break;
                    }
                    (Project restored, string label) = model.Undo[^1];
                    model.Undo.RemoveAt(model.Undo.Count - 1);
                    model.Redo.Add((before, label));
                    model.Version++;
                    if (!SameContent(session.Current!, restored))
                    {
                        return "undo did not restore the snapshot the model recorded for that edit";
                    }
                    if (Allocator(session.Current!) < Allocator(before))
                    {
                        return "undo lowered the id allocator's high-water mark";
                    }
                    // The model tracks the SESSION's snapshot from here, since the allocator patch legitimately
                    // makes the restored project a different object with the same content.
                    model.Current = session.Current!;
                    counters.Undone++;
                    break;
                }
                case RedoOp:
                {
                    Project before = model.Current;
                    EditOutcome outcome = session.Redo();
                    if (model.Redo.Count == 0)
                    {
                        if (outcome.Status != EditStatus.NoChange)
                        {
                            return "redo with nothing undone must be a no-op";
                        }
                        break;
                    }
                    (Project restored, string label) = model.Redo[^1];
                    model.Redo.RemoveAt(model.Redo.Count - 1);
                    model.Undo.Add((before, label));
                    Trim(model, counters);
                    model.Version++;
                    if (!SameContent(session.Current!, restored))
                    {
                        return "redo did not restore the snapshot the model recorded for that edit";
                    }
                    model.Current = session.Current!;
                    counters.Redone++;
                    break;
                }
                case RollbackOp:
                {
                    EditOutcome outcome = session.Rollback();
                    if (model.Undo.Count == 0)
                    {
                        if (outcome.Status != EditStatus.NoChange)
                        {
                            return "rollback with nothing to discard must be a no-op";
                        }
                        break;
                    }
                    (Project restored, string _) = model.Undo[^1];
                    model.Undo.RemoveAt(model.Undo.Count - 1);
                    model.Current = restored;
                    model.Version++;
                    // Rollback restores VERBATIM — no allocator patch, no redo entry (US: a cancelled gesture
                    // burns no ids and cannot be redone). Reference equality is the sharpest way to say that.
                    if (!ReferenceEquals(session.Current, restored))
                    {
                        return "rollback did not restore the pre-edit snapshot verbatim";
                    }
                    counters.RolledBack++;
                    break;
                }
                case MarkSavedOp:
                    session.MarkSaved(session.Current!);
                    model.SavePoint = model.Current;
                    break;
                case MarkSavedStaleOp stale:
                {
                    // The race fix: MarkSaved records the snapshot that was WRITTEN, which may already be behind
                    // the current one — an edit that landed during the save must leave the document dirty.
                    Project snapshot = model.Seen[stale.Pick % model.Seen.Count];
                    session.MarkSaved(snapshot);
                    model.SavePoint = snapshot;
                    counters.SavedStale++;
                    if (session.IsDirty)
                    {
                        counters.DirtyAfterSave++;
                    }
                    break;
                }
                default:
                {
                    if (EditCommand(op, session.Current!) is not { } command)
                    {
                        break;
                    }
                    Project before = model.Current;
                    EditOutcome outcome = session.Apply(command);
                    if (outcome.Status != EditStatus.Committed)
                    {
                        if (!ReferenceEquals(session.Current, before))
                        {
                            return "a non-committed apply changed the current snapshot";
                        }
                        break;
                    }
                    model.Undo.Add((before, outcome.Label));
                    Trim(model, counters);
                    model.Redo.Clear();
                    model.Current = session.Current!;
                    model.Seen.Add(model.Current);
                    model.Version++;
                    counters.Applied++;
                    break;
                }
            }
            return Disagreement(session, model, op.GetType().Name);
        }

        /// <summary>
        /// Drains the undo stack, which is the only way to observe its DEPTH from outside: the count of committed
        /// undos must equal the model's depth, and never exceed the cap.
        /// <para>Where it lands is the check that trimming discards oldest-first. With an unlimited history that is
        /// the project as opened; with a bounded one it is the oldest RETAINED snapshot — a policy that dropped the
        /// newest entries instead, or dropped the wrong ones, would leave the drain somewhere else entirely.</para>
        /// </summary>
        private static string? DrainUndo(ProjectDocumentSession session, Model model, Project? mustLandOn)
        {
            Project expected = mustLandOn
                ?? (model.Undo.Count > 0 ? model.Undo[0].Snapshot : model.Current);
            int expectedDepth = model.Undo.Count;
            if (model.Cap is { } cap && expectedDepth > cap)
            {
                return $"model itself kept {expectedDepth} entries past a cap of {cap}";
            }

            int drained = 0;
            while (session.CanUndo)
            {
                if (session.Undo().Status != EditStatus.Committed)
                {
                    return "undo reported nothing to do while CanUndo was true";
                }
                drained++;
                if (drained > expectedDepth)
                {
                    return $"undo stack is deeper than the model's {expectedDepth}";
                }
            }
            if (drained != expectedDepth)
            {
                return $"drained {drained} undos, model held {expectedDepth}";
            }
            return SameContent(session.Current!, expected)
                ? null
                : "draining the undo stack did not land on the oldest snapshot the history still held";
        }

        [Test]
        public async Task History_MatchesTheReferenceModel_OverRandomizedSequences()
        {
            Project opened = await App.Load(BaseProject);
            var counters = new Counters();

            Sequence.Sample(ops =>
            {
                var session = new ProjectDocumentSession();
                session.Open(opened);
                var model = new Model { Current = opened, SavePoint = opened, Version = session.Version };
                model.Seen.Add(opened);

                foreach (Op op in ops)
                {
                    if (Step(session, model, op, counters) is { } disagreement)
                    {
                        throw new AssertionException(disagreement);
                    }
                }
                if (DrainUndo(session, model, mustLandOn: opened) is { } drainFailure)
                {
                    throw new AssertionException(drainFailure);
                }
                return true;
            }, iter: 150, threads: 1);

            Assert.Multiple(() =>
            {
                Assert.That(counters.Applied, Is.GreaterThan(0), "no edit was ever committed");
                Assert.That(counters.Undone, Is.GreaterThan(0), "no undo was ever performed");
                Assert.That(counters.Redone, Is.GreaterThan(0), "no redo was ever performed");
                Assert.That(counters.RolledBack, Is.GreaterThan(0), "no rollback was ever performed");
                Assert.That(counters.SavedStale, Is.GreaterThan(0), "no save point was ever set to an older snapshot");
                Assert.That(counters.DirtyAfterSave, Is.GreaterThan(0),
                    "a stale save point never left the document dirty, so the race the save point guards was not reached");
            });
        }

        /// <summary>
        /// The same harness under <see cref="HistoryPolicy.Bounded"/>. This is where the coverage gap was: trimming
        /// has no behavioural test at all today, only one asserting the policy record stores the integer.
        ///
        /// <para>The caps are chosen by boundary analysis: <c>0</c> is "no undo at all" (every apply trims the entry
        /// it just pushed), <c>1</c> makes every apply trim, <c>2</c> is the smallest cap that still holds a
        /// sequence, and <c>5</c> sits near the length of the generated sequences so runs land on both sides of the
        /// cap. The model trims exactly where the session does — after an apply commits and after a redo — so a
        /// trim that fired at the wrong moment shows up as a depth or landing disagreement.</para>
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        public async Task BoundedHistory_TrimsOldestFirst_WithoutDisturbingRedoOrTheSavePoint(int cap)
        {
            Project opened = await App.Load(BaseProject);
            var counters = new Counters();

            Sequence.Sample(ops =>
            {
                var session = new ProjectDocumentSession(HistoryPolicy.Bounded(cap));
                session.Open(opened);
                var model = new Model
                {
                    Current = opened, SavePoint = opened, Version = session.Version, Cap = cap,
                };
                model.Seen.Add(opened);

                foreach (Op op in ops)
                {
                    if (Step(session, model, op, counters) is { } disagreement)
                    {
                        throw new AssertionException(disagreement);
                    }
                }
                // Landing is derived from the model, not from `opened`: past the cap the oldest reachable state is
                // no longer the file as opened, and that is the point of the policy.
                if (DrainUndo(session, model, mustLandOn: null) is { } drainFailure)
                {
                    throw new AssertionException(drainFailure);
                }
                return true;
            }, iter: 150, threads: 1);

            Assert.Multiple(() =>
            {
                Assert.That(counters.Applied, Is.GreaterThan(0), "no edit was ever committed");
                Assert.That(counters.Trimmed, Is.GreaterThan(0),
                    $"cap {cap} never actually trimmed anything, so the policy was not exercised");
                // A cap of 0 turns history navigation off entirely, not just undo: redo is only ever pushed BY an
                // undo, and an undo needs a non-empty stack, so nothing is redoable either. Measured, not assumed —
                // this detector failed at cap 0 and passes at every other cap.
                Assert.That(counters.Redone, cap == 0 ? Is.Zero : Is.GreaterThan(0),
                    cap == 0
                        ? "a cap of 0 leaves nothing to undo, so nothing can be redone either"
                        : "no redo ran, so trimming was never checked against a live redo stack");
            });
        }
    }
}
