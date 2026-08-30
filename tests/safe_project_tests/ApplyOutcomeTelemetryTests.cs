using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// What one applied edit reports about itself.
    ///
    /// <c>ApplyInternal</c> has nine outcome-producing exits and used to tag telemetry at three of them, in two
    /// different ways: the two catch arms called <c>SetError</c> (leaving the span Error) while the three
    /// precondition refusals returned quietly (leaving it Unset) - for outcomes that are the same KIND of thing.
    /// A refusal is the rules working; only a failure is an error. Classifying the returned outcome once is what
    /// makes that true at every exit rather than at the ones somebody remembered.
    /// </summary>
    public class ApplyOutcomeTelemetryTests
    {
        private static IProjectDocument OpenSession()
        {
            var app = new ProjectAppService(TestSetup.Settings);
            Project project = app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            return app.OpenDocument(project);
        }

        private static ElementId FirstLocality(IProjectDocument document) =>
            document.Current!.Groups[0].Id!.Value;

        [Test]
        public void ACommittedEdit_ReportsOkWithItsChangeSetCounts()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectDocumentSession.Apply" },
                instruments: new[] { "ihc.edit.apply", "ihc.edit.apply.duration" });
            IProjectDocument session = OpenSession();

            EditOutcome outcome = session.Apply(new AddLocality("Ny lokalitet"));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
            Activity span = capture.Span("ProjectDocumentSession.Apply");
            Assert.Multiple(() =>
            {
                Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Unset));
                Assert.That(span.GetTagItem("ihc.edit.status"), Is.EqualTo("ok"));
                Assert.That(span.GetTagItem("ihc.edit.added_count"), Is.EqualTo(1),
                    "a committed edit says how much it changed, which is what makes an outsized one visible");
                Assert.That(span.GetTagItem("ihc.edit.removed_count"), Is.EqualTo(0));
                Assert.That(span.GetTagItem("ihc.edit.changed_count"), Is.Not.Null);
                Assert.That(span.GetTagItem("ihc.problem.code"), Is.Null, "a success has no problem code");
            });
        }

        /// <summary>The counter and the histogram must agree with the span, or a metric cannot be traced back.</summary>
        [Test]
        public void ACommittedEdit_RecordsBothInstrumentsWithTheMatchingStatus()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectDocumentSession.Apply" },
                instruments: new[] { "ihc.edit.apply", "ihc.edit.apply.duration" });
            IProjectDocument session = OpenSession();

            session.Apply(new AddLocality("Ny lokalitet"));

            IReadOnlyList<CapturedPoint> points = capture.Points;
            Assert.Multiple(() =>
            {
                Assert.That(points.Count(p => p.Instrument == "ihc.edit.apply"), Is.EqualTo(1),
                    "one applied edit is one occurrence");
                Assert.That(points.Count(p => p.Instrument == "ihc.edit.apply.duration"), Is.EqualTo(1));
                foreach (CapturedPoint point in points)
                {
                    Assert.That(point.Tag("ihc.edit.status"), Is.EqualTo("ok"), point.Instrument);
                    Assert.That(point.Tag("ihc.edit.command"), Is.Not.Null, point.Instrument);
                }
            });
        }

        [Test]
        public void ANoChangeEdit_IsOk_NotARefusal()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectDocumentSession.Apply" },
                instruments: new[] { "ihc.edit.apply", "ihc.edit.apply.duration" });
            IProjectDocument session = OpenSession();
            ElementId locality = FirstLocality(session);
            string existing = session.Current!.FindById(locality)!.GetAttribute("name")!;
            string note = session.Current!.FindById(locality)!.GetAttribute("note") ?? string.Empty;

            // Renaming to the name it already has produces an identical project: a no-op, not a refusal.
            EditOutcome outcome = session.Apply(new RenameLocality(locality, existing, note));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.NoChange));
            Assert.Multiple(() =>
            {
                Assert.That(capture.Span("ProjectDocumentSession.Apply").GetTagItem("ihc.edit.status"), Is.EqualTo("ok"),
                    "producing no change is the command working, not declining");
                Assert.That(capture.Span("ProjectDocumentSession.Apply").Status, Is.EqualTo(ActivityStatusCode.Unset));
            });
        }

        /// <summary>Precondition refusal 1 of 3: no project open.</summary>
        [Test]
        public void ARefusalWithNoProjectOpen_IsRefusedAndCarriesItsCode()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectDocumentSession.Apply" },
                instruments: new[] { "ihc.edit.apply", "ihc.edit.apply.duration" });
            IProjectDocument session = OpenSession();
            session.Close();   // the document is now empty, which is the condition this refusal names

            EditOutcome outcome = session.Apply(new AddLocality("Ny lokalitet"));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
            AssertRefused(capture.Span("ProjectDocumentSession.Apply"), outcome);
        }

        /// <summary>Precondition refusal 2 of 3: a stale base version.</summary>
        [Test]
        public void ARefusalOnAStaleBaseVersion_IsRefusedAndCarriesItsCode()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectDocumentSession.Apply" },
                instruments: new[] { "ihc.edit.apply", "ihc.edit.apply.duration" });
            IProjectDocument session = OpenSession();

            EditOutcome outcome = session.Apply(new AddLocality("Ny lokalitet"), baseVersion: 9999);

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
            AssertRefused(capture.Span("ProjectDocumentSession.Apply"), outcome);
        }

        /// <summary>Precondition refusal 3 of 3: the command's own legality verdict.</summary>
        [Test]
        public void ARefusalFromTheCommandsVerdict_IsRefusedAndCarriesItsCode()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectDocumentSession.Apply" },
                instruments: new[] { "ihc.edit.apply", "ihc.edit.apply.duration" });
            IProjectDocument session = OpenSession();

            // A command targeting an id no element carries is refused by the existence guard in Evaluate,
            // before anything is produced.
            EditOutcome outcome = session.Apply(new RenameLocality(new ElementId(9_999_999, 0), "x", string.Empty));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                "a command targeting a missing element is refused by the legality gate");
            AssertRefused(capture.Span("ProjectDocumentSession.Apply"), outcome);
        }

        private static void AssertRefused(Activity span, EditOutcome outcome)
        {
            Assert.Multiple(() =>
            {
                Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Unset),
                    "a refusal is the rules working - marking it Error makes a healthy session look broken");
                Assert.That(span.GetTagItem("ihc.edit.status"), Is.EqualTo("refused"));
                Assert.That(span.GetTagItem("ihc.problem.code"), Is.EqualTo(outcome.Code.Value),
                    "the span carries the SAME code the caller was given");
                Assert.That(span.GetTagItem("error.type"), Is.Null, "a refusal is not an error and has no error type");
            });
        }
    }
}
