using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// What a phase span says when the phase THROWS.
    ///
    /// <para>Each of these six operations starts its own scope and disposes it on the way out. Disposal records
    /// whichever outcome the scope was last told about, and the default is success — so a phase that leaves
    /// through a throw without saying so records <c>ihc.edit.status = "ok"</c> and no <c>error.type</c>. The
    /// failure is then invisible in BOTH signals: the span is not Error, the metric point counts as a success,
    /// and a dashboard reading either shows a healthy load, save, report, diff, index or validation run that in
    /// fact blew up. A failure that reports as a success is worse than no instrumentation at all, because it is
    /// evidence pointing the wrong way.</para>
    ///
    /// <para>The three assertions are the same for all six, because the guarantee comes from the core rather
    /// than from each site: status Error, <c>ihc.edit.status = "failed"</c>, and the <c>error.type</c> the
    /// error-type policy resolved — a catalogue code when the exception carried one, an allowlisted CLR type
    /// name when it did not, and <c>_OTHER</c> for everything else.</para>
    /// </summary>
    /// <remarks>
    /// Two of the six are pure functions with no declared failure of their own, so their fault is provoked
    /// rather than natural. That is the point: an operation nobody expects to throw is exactly the one whose
    /// silent success is never noticed.
    /// </remarks>
    [TestFixture]
    public sealed class PhaseFailureTelemetryTests
    {
        /// <summary>A project the serializer accepts: every #REQUIRED attribute of the registry's root block.</summary>
        private static Project Minimal(params (string Name, string Value)[] extraAttrs) =>
            new(new ProjectElement("utcs_project", null,
                [
                    ("version_major", "4"), ("version_minor", "0"),
                    ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x3"),
                    .. extraAttrs,
                ],
                []));

        /// <summary>Runs work that must throw, and hands back the span the failing phase left behind.</summary>
        private static Activity SpanOfFailedPhase(string operationName, Action work)
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(
                Telemetry.ActivitySourceName, spanNames: [operationName]);
            Assert.That(work, Throws.Exception, "the phase has to fail before its span can be asked about failure");
            return capture.Span(operationName);
        }

        // The wire names are literals rather than registry constants on purpose: a test naming an attribute
        // through the same constant the product writes cannot see a rename, which is the change most likely to
        // break a saved query on the backend.
        private static void AssertRecordsTheFailure(Activity span, string expectedErrorType)
        {
            Assert.Multiple(() =>
            {
                Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error),
                    "a phase that threw is an error, and only the span's status says so to a trace view");
                Assert.That(span.GetTagItem("ihc.edit.status"), Is.EqualTo("failed"),
                    "the outcome a metric breakdown groups on — 'ok' here would count the failure as a success");
                Assert.That(span.GetTagItem("error.type"), Is.EqualTo(expectedErrorType),
                    "the normalized identity, so a failure can be told apart from every other failure");
            });
        }

        [Test]
        public void AReadThatRefusesTheContainer_RecordsTheFailureOnItsSpan() =>
            AssertRecordsTheFailure(
                SpanOfFailedPhase("ProjectReader.Read",
                    () => ProjectReader.Read(new byte[] { 0x1F, 0x8B, 0x08, 0x00 })),
                "load-gzip");

        [Test]
        public void ASerializeThatCannotEncodeTheProject_RecordsTheFailureOnItsSpan() =>
            AssertRecordsTheFailure(
                SpanOfFailedPhase("ProjectSerializer.Serialize",
                    () => ProjectSerializer.Serialize(Minimal(("icon", "€")))),
                "attr-latin1");

        [Test]
        public void AReportAskedForAnUnsupportedMimetype_RecordsTheFailureOnItsSpan() =>
            AssertRecordsTheFailure(
                SpanOfFailedPhase("ReportGenerator.Generate",
                    () => ReportGenerator.Generate(Minimal(), ReportKind.Functions, ReportMode.Standard,
                        "application/pdf", null, DateTimeOffset.UnixEpoch)),
                "System.ArgumentException");

        [Test]
        public void ADiffThatFaults_RecordsTheFailureOnItsSpan() =>
            AssertRecordsTheFailure(
                SpanOfFailedPhase("ProjectChangeSet.Diff",
                    () => ProjectChangeSet.Diff(null!, Minimal(), 1, 2, "test", "test")),
                "_OTHER");

        [Test]
        public void AnIndexBuildThatFaults_RecordsTheFailureOnItsSpan() =>
            AssertRecordsTheFailure(
                SpanOfFailedPhase("ProjectIndex.Build", () => ProjectIndex.Build(null!)),
                "_OTHER");

        /// <summary>
        /// Under the diagnostic rethrow policy a throwing rule aborts the run, which is what that policy is
        /// for — and an aborted run must not be the one the trace records as complete.
        /// </summary>
        [Test]
        public void AValidationRunAbortedByAThrowingRule_RecordsTheFailureOnItsSpan()
        {
            ProblemCatalogEntry entry = new(
                new ProblemCode("addr-unassigned"), ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing, CatalogDisposition.Warning, RuleKind.UserContentRule,
                RuleFaces.WholeProject, default, FindingShape.OnePerOccurrence, default, "Label");
            ProblemCatalog catalog = ProblemCatalog.From(ImmutableArray.Create(entry));
            RuleSet rules = RuleSet.Create(catalog,
                [new RuleBuilder(entry).Inspect(_ => throw new InvalidOperationException("boom")).Build()]);
            WholeProjectValidator validator = new(rules, perRuleTiming: false);
            ValidationProfile diagnostic = ValidationProfile.ProjectOnly with
            {
                FailurePolicy = RuleFailurePolicy.Rethrow,
            };

            AssertRecordsTheFailure(
                SpanOfFailedPhase("WholeProjectValidator.Validate",
                    () => validator.Validate(Minimal(), diagnostic)),
                "System.InvalidOperationException");
        }

        /// <summary>
        /// The per-rule child span, which the opt-in timing exists to make comparable. Under the default
        /// report-and-continue policy the RUN succeeds — the throwing rule contributes an
        /// <c>internal.rule-failed</c> FAULT, on its own channel and never as a finding, and the pass carries
        /// on — so the run's own span is the wrong place to look, and the rule's span is the only one that can
        /// say which rule misbehaved.
        /// </summary>
        [Test]
        public void AThrowingRuleUnderPerRuleTiming_RecordsTheFailureOnTheRuleSpan()
        {
            ProblemCatalogEntry entry = new(
                new ProblemCode("addr-unassigned"), ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing, CatalogDisposition.Warning, RuleKind.UserContentRule,
                RuleFaces.WholeProject, default, FindingShape.OnePerOccurrence, default, "Label");
            ProblemCatalog catalog = ProblemCatalog.From(ImmutableArray.Create(entry));
            RuleSet rules = RuleSet.Create(catalog,
                [new RuleBuilder(entry).Inspect(_ => throw new InvalidOperationException("boom")).Build()]);
            WholeProjectValidator validator = new(rules, perRuleTiming: true);

            using TelemetryCapture capture = TelemetryCapture.Listen(
                Telemetry.ActivitySourceName,
                spanNames: ["WholeProjectValidator.Rule", "WholeProjectValidator.Validate"]);

            validator.Validate(Minimal(), ValidationProfile.ProjectOnly);

            Activity rule = capture.Span("WholeProjectValidator.Rule");
            Activity run = capture.Span("WholeProjectValidator.Validate");
            Assert.Multiple(() =>
            {
                Assert.That(run.GetTagItem("ihc.edit.status"), Is.EqualTo("ok"),
                    "report-and-continue means the RUN did what it was asked to do");
                Assert.That(rule.GetTagItem("ihc.edit.status"), Is.EqualTo("failed"),
                    "the rule did not, and the child span is the only place that fact fits");
                Assert.That(rule.GetTagItem("error.type"), Is.EqualTo("System.InvalidOperationException"));
            });
        }
    }
}
