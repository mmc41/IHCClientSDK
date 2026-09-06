using System;
using System.Diagnostics;
using System.Linq;

using Ihc.Tests.Shared;
using Ihc.Vis.Model;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// A rule that CRASHES is a defect in the tool, not a statement about the user's file, so the run keeps its
    /// verdict and reports the crash on its second channel — <c>StructuredValidationResult.Faults</c>. That
    /// contract is settled and is not what these tests are about.
    ///
    /// <para>What they are about is whether a reader of the TRACE can see it. The run's span carried
    /// <c>rules_run</c> and <c>findings_emitted</c> and nothing else, and the per-rule span that names the
    /// misbehaving rule exists only behind an opt-in that is off by default — so on an ordinary run a crashed
    /// rule left no mark on any span at all. A run that lost a rule and one that ran cleanly produced the same
    /// trace, which is the one thing a trace must not do.</para>
    /// </summary>
    [TestFixture]
    public sealed class CrashedRuleTelemetryTests
    {
        private const string RunSpan = "WholeProjectValidator.Validate";

        private static Project OneElement() =>
            new(Node("utcs_project", null, [], Node("dataline_input", "_0x2a", [])));

        /// <summary>A rule set of exactly one rule, whose inspection throws.</summary>
        private static RuleSet CrashingRule(Exception failure)
        {
            ProblemCatalogEntry entry = new(
                new ProblemCode("addr-unassigned"), ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing, CatalogDisposition.Warning, RuleKind.UserContentRule,
                RuleFaces.WholeProject, default, FindingShape.OnePerOccurrence, default, "Label");
            return RuleSet.Create(
                ProblemCatalog.From([entry]),
                [new RuleBuilder(entry).Inspect(_ => throw failure).Build()]);
        }

        /// <summary>
        /// One run over the one-element project, with its span picked out of the process-wide capture. Every
        /// case here is the same run with a different rule set, so the capture, the probe and the span lookup
        /// are the fixture's, not each test's.
        /// </summary>
        /// <remarks>
        /// The capture is closed before the span is returned, which is safe and deliberate: an
        /// <see cref="Activity"/> is a plain object once it has stopped, so its tags and events outlive the
        /// listener that collected it — while leaving the listener open would have every later case in the
        /// assembly emit into it.
        /// </remarks>
        private static (StructuredValidationResult Result, Activity Run) Run(RuleSet rules)
        {
            using TelemetryCapture spans = TelemetryCapture.Listen(
                Ihc.Telemetry.ActivitySourceName, spanNames: [RunSpan]);
            using TraceProbe probe = TraceProbe.Start();

            StructuredValidationResult result =
                new WholeProjectValidator(rules).Validate(OneElement(), ValidationProfile.ProjectOnly);
            return (result, probe.Span(spans, RunSpan));
        }

        [Test]
        public void ACrashedRule_IsCarriedByTheRunsOwnSpan()
        {
            (StructuredValidationResult result, Activity run) =
                Run(CrashingRule(new InvalidOperationException("the rule read something that was not there")));

            Assert.Multiple(() =>
            {
                Assert.That(result.Faults, Is.Not.Empty, "the fault channel still reports it");
                Assert.That(run.Status, Is.EqualTo(ActivityStatusCode.Unset),
                    "the RUN succeeded — a crashed rule does not fail it");
                Assert.That(run.GetTagItem("ihc.validation.rules_faulted"), Is.EqualTo(1),
                    "but the run has to say it lost one, or a reader counts its findings as complete");
                Assert.That(run.Events.Any(e => e.Name == "exception"), Is.True,
                    "and it has to say WHAT was lost");
            });
        }

        [Test]
        public void TheEventNamesTheRuleThatCrashed()
        {
            (_, Activity run) = Run(CrashingRule(new InvalidOperationException("boom")));

            ActivityEvent fault = run.Events.First(e => e.Name == "exception");
            Assert.That(
                fault.Tags.Any(t => t.Key == "ihc.validation.rule.code" && (t.Value as string) == "addr-unassigned"),
                Is.True,
                "which rule misbehaved is the only thing that makes the event actionable");
        }

        /// <summary>
        /// The policy is <c>ReportAndContinue</c>, and recording the crash must not be what breaks it. Putting
        /// the crash on the span READS the exception — the event carries its message and stack — so a rule that
        /// throws something which cannot describe itself would, unguarded, have its report leave through
        /// <c>Telemetry.Run</c>: the run marked Failed and the exception rethrown, failing the validation over a
        /// crash the policy had already contained.
        /// </summary>
        [Test]
        public void ARuleCrashingWithAnUndescribableFault_StillLeavesTheRunItsVerdict()
        {
            (StructuredValidationResult result, Activity run) = Run(CrashingRule(new UnreadableException()));

            Assert.Multiple(() =>
            {
                Assert.That(result.Faults, Is.Not.Empty,
                    "the fault channel still carries it — the row is what must survive");
                Assert.That(run.Status, Is.EqualTo(ActivityStatusCode.Unset),
                    "and the run still succeeded: a contained crash does not become the run's failure because "
                    + "recording it was hard");
                Assert.That(run.GetTagItem("ihc.validation.rules_faulted"), Is.EqualTo(1));
            });
        }

        [Test]
        public void ACleanRunSaysNothingWentWrong()
        {
            (_, Activity run) = Run(ProjectRules.Registered);

            Assert.Multiple(() =>
            {
                Assert.That(run.Events.Any(), Is.False);
                Assert.That(run.GetTagItem("ihc.validation.rules_faulted"), Is.Null,
                    "the tag is present only when there is something to report");
            });
        }
    }
}
