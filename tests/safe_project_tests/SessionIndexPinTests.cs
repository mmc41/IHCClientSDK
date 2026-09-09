using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Ihc.Tests.Shared;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The session's index bookkeeping: one build per commit, and a refusal — before anything moves — when the
    /// pinned index and the open snapshot have drifted apart.
    ///
    /// <para>The pin exists for speed, not for answers: every gate, probe and diff resolves the index through the
    /// snapshot it holds, so losing the pin costs a rebuild per gesture and changes no verdict. That is exactly
    /// what makes it invisible without a test — hence the build-count net below. What the belt catches is the one
    /// bookkeeping defect the design cannot foreclose: the snapshot replaced behind <c>SetCurrent</c>'s back, so
    /// the session pins maps of a tree it no longer serves.</para>
    /// </summary>
    /// <remarks>
    /// The drifted state is produced by REFLECTION on the private <c>_current</c> field, and that is the honest
    /// way to get it: the public API cannot reach it, and the alternative — an internal
    /// <c>ReplaceCurrentUnchecked</c> door — would put in product code exactly the bypass the belt exists to
    /// catch. The helper asserts the field resolved, naming this file, so a rename fails loudly instead of
    /// leaving a test that passes over a state it never produced.
    /// </remarks>
    public class SessionIndexPinTests : SessionCommandFixture
    {
        private const string ApplySpan = "ProjectDocumentSession.Apply";
        private const string BuildSpan = "ProjectIndex.Build";

        /// <summary>
        /// Swaps the session's snapshot for a re-rooted copy the pin does not cover — the bookkeeping drift the
        /// belt exists for, which nothing on the public surface can produce.
        /// </summary>
        private static void ReplaceCurrentBehindTheSession(IProjectDocument session)
        {
            FieldInfo field = typeof(ProjectDocumentSession)
                .GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.That(field, Is.Not.Null,
                "ProjectDocumentSession._current was renamed; SessionIndexPinTests reaches it by name");

            var current = (Project)field.GetValue(session)!;
            field.SetValue(session, current with
            {
                Root = current.Root.WithAttribute("last_unique_id", "_0x7fffff"),
            });
        }

        /// <summary>
        /// The regression net for a lost pin. A commit indexes the NEW snapshot and nothing else: the pre-edit
        /// tree was pinned, the diff is handed both sides, and <c>SetCurrent</c>'s read hits the memo the diff
        /// just filled. A session that stops pinning, or a gate that starts resolving its own index, moves this
        /// number — which is the only way either becomes visible, since neither changes an answer.
        /// </summary>
        [Test]
        public async Task Apply_BuildsTheNewSnapshotsIndexExactlyOnce()
        {
            IProjectDocument session = Session(await Load("project3-KompleksWired.vis"));

            // Started AFTER the open, so the opening snapshot's own build stays outside the window.
            using TelemetryCapture capture = TelemetryCapture.Listen(
                Telemetry.ActivitySourceName, spanNames: [BuildSpan]);

            EditOutcome outcome = session.Apply(new AddLocality("Ny lokalitet"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed), "the edit committed");
                Assert.That(capture.SpansNamed(BuildSpan).Count, Is.EqualTo(1),
                    "one index per commit — the new snapshot's, and no re-walk of the tree the session already held");
            });
        }

        /// <summary>
        /// The same net on the READ side. The delete gate is what a host re-asks on every selection change, and it
        /// classifies deletability off the snapshot's index — so inside a session that read must be a memo hit, not
        /// a build. A gate that resolves its own index answers identically and only costs a walk per gesture, which
        /// is exactly why nothing but this count makes it visible.
        /// </summary>
        [Test]
        public async Task CanApply_DeleteGate_BuildsNoIndex()
        {
            Project project = await Load("project3-KompleksWired.vis");
            (ProjectElement product, ProjectElement pin) =
                CatalogPinDeleteGuardTests.PickProductAndCatalogPin(project);
            ProjectElement inner = CatalogPinDeleteGuardTests.PickNodeInsideLockedBlock(project);
            IProjectDocument session = Session(project);

            // Started AFTER the open and after the picks, so the opening snapshot's own build stays outside the window.
            using TelemetryCapture capture = TelemetryCapture.Listen(
                Telemetry.ActivitySourceName, spanNames: [BuildSpan]);

            EditVerdict pinVerdict = session.CanApply(new DeleteNode(pin.Id!.Value, Cascade: false));
            EditVerdict innerVerdict = session.CanApply(new DeleteNode(inner.Id!.Value, Cascade: false));
            EditVerdict productVerdict = session.CanApply(new DeleteNode(product.Id!.Value, Cascade: false));

            Assert.Multiple(() =>
            {
                Assert.That(pinVerdict.Code, Is.EqualTo(EditRefusalCodes.DeletionRefusedCatalogPin),
                    "the gate still names the catalog-pin rule");
                Assert.That(innerVerdict.Code, Is.EqualTo(EditRefusalCodes.DeletionRefusedLockedBlock),
                    "and the locked-block rule");
                Assert.That(productVerdict.Ok, Is.True, "while a product is still offered for delete");
                Assert.That(capture.SpansNamed(BuildSpan).Count, Is.EqualTo(0),
                    "the gate resolves through the pinned snapshot — a gate that builds its own index moves this");
            });
        }

        /// <summary>
        /// The belt on the edit door, on both halves of what it is worth: the session refuses, and the refusal is
        /// visible. Nothing has moved when it throws — the version, the history and the snapshot are as they were
        /// — and the door's own span carries the failure, which is what a person watching the trace sees. In a
        /// host the same exception reaches the view-model catch-all, which files it as an internal-error row.
        /// </summary>
        [Test]
        public async Task Apply_WhenTheSnapshotWasReplacedBehindTheSession_RefusesBeforeAnythingMoves()
        {
            IProjectDocument session = Session(await Load("project3-KompleksWired.vis"));
            session.Apply(new AddLocality("Første lokalitet"));
            int versionBefore = session.Version;
            ReplaceCurrentBehindTheSession(session);
            Project replaced = session.Current!;

            using TelemetryCapture capture = TelemetryCapture.Listen(
                Telemetry.ActivitySourceName, spanNames: [ApplySpan]);

            Assert.That(() => session.Apply(new AddLocality("Ny lokalitet")),
                Throws.InvalidOperationException,
                "a session whose bookkeeping has drifted refuses to move rather than rebuild in silence");

            Assert.Multiple(() =>
            {
                Assert.That(session.Version, Is.EqualTo(versionBefore), "the version did not move");
                Assert.That(session.Current, Is.SameAs(replaced), "the document did not move");
                Assert.That(session.RedoLabel, Is.Null, "no redo entry was minted");
            });

            Activity span = capture.Span(ApplySpan);
            Assert.Multiple(() =>
            {
                Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error),
                    "the door's span is what a trace view reads");
                Assert.That(span.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
                Assert.That(span.GetTagItem("error.type"), Is.EqualTo("System.InvalidOperationException"));
                Assert.That(span.Events.Any(e => e.Name == "exception"), Is.True,
                    "the exception travels with the span, so the stack is on the trace and not only in a log");
            });
        }

        /// <summary>
        /// The same belt on the history doors, which share one <c>HistoryStep</c> skeleton with Redo and Rollback.
        /// It runs first inside the lock, so a drifted session refuses with both stacks untouched — an undo that
        /// popped its entry and then threw would lose the edit outright.
        /// </summary>
        [Test]
        public async Task Undo_WhenTheSnapshotWasReplacedBehindTheSession_RefusesWithTheHistoryUntouched()
        {
            IProjectDocument session = Session(await Load("project3-KompleksWired.vis"));
            session.Apply(new AddLocality("Ny lokalitet"));
            string? undoLabel = session.UndoLabel;
            int versionBefore = session.Version;
            ReplaceCurrentBehindTheSession(session);

            Assert.That(session.Undo, Throws.InvalidOperationException,
                "the history doors refuse on the same bookkeeping invariant the edit door does");

            Assert.Multiple(() =>
            {
                Assert.That(session.UndoLabel, Is.EqualTo(undoLabel), "the undo stack was not popped");
                Assert.That(session.CanRedo, Is.False, "and nothing was pushed onto the redo stack");
                Assert.That(session.Version, Is.EqualTo(versionBefore), "the version did not move");
            });
        }
    }
}
