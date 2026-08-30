using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// What a whole-project validation run reports about itself.
    ///
    /// The run is the most expensive thing validation does and it sat inside the caller's span with no shape
    /// of its own. Rules-run and findings-emitted are the two numbers that make one run comparable with
    /// another. Per-rule timing answers "WHICH rule is slow?" and is opt-in, because a whole-project run
    /// executes the entire rule set - a span per rule per run is an investigation cost, not a standing one.
    /// </summary>
    public class ValidationEngineTelemetryTests
    {
        private static Project Fixture() =>
            new ProjectAppService(TestSetup.Settings).Load("testdata/projects/Project1-SimpelWired.vis")
                .GetAwaiter().GetResult();

        [Test]
        public void TheRunReportsHowManyRulesRanAndHowMuchTheyFound()
        {
            using (TelemetryCapture spansCapture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "WholeProjectValidator.Validate", "WholeProjectValidator.Rule" }))
            {
                new WholeProjectValidator(ProjectRules.Registered)
                    .Validate(Fixture(), ValidationProfile.Categorized);

                Activity run = spansCapture.Spans.Single(s => s.OperationName == "WholeProjectValidator.Validate");
                Assert.Multiple(() =>
                {
                    Assert.That((int)run.GetTagItem("ihc.validation.rules_run")!, Is.GreaterThan(0),
                        "how many rules the profile actually admitted - not how many are registered");
                    Assert.That(run.GetTagItem("ihc.validation.findings_emitted"), Is.Not.Null,
                        "an absent count and a count of zero must not look the same");
                });
            }
        }

        /// <summary>The gate's assertion, both halves.</summary>
        [Test]
        public void PerRuleSpansAreAbsentByDefault_AndPresentWithTheSwitchOn()
        {
            Project project = Fixture();

            using (TelemetryCapture offSpansCapture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "WholeProjectValidator.Validate", "WholeProjectValidator.Rule" }))
            {
                new WholeProjectValidator(ProjectRules.Registered)
                    .Validate(project, ValidationProfile.Categorized);
                Assert.That(offSpansCapture.Spans.Any(s => s.OperationName == "WholeProjectValidator.Rule"), Is.False,
                    "a span per rule by default is the unaffordable cost the switch exists to prevent");
            }

            using (TelemetryCapture onSpansCapture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "WholeProjectValidator.Validate", "WholeProjectValidator.Rule" }))
            {
                new WholeProjectValidator(ProjectRules.Registered, perRuleTiming: true)
                    .Validate(project, ValidationProfile.Categorized);

                Activity[] ruleSpans = onSpansCapture.Spans.Where(s => s.OperationName == "WholeProjectValidator.Rule").ToArray();
                Activity run = onSpansCapture.Spans.Single(s => s.OperationName == "WholeProjectValidator.Validate");

                Assert.Multiple(() =>
                {
                    Assert.That(ruleSpans, Is.Not.Empty, "the switch is what makes them appear");
                    Assert.That(ruleSpans.Length, Is.EqualTo((int)run.GetTagItem("ihc.validation.rules_run")!),
                        "one span per rule that ran - no more, no fewer");
                    Assert.That(ruleSpans.All(s => s.GetTagItem("ihc.validation.rule.code") is not null), Is.True,
                        "a per-rule span without the rule's code cannot answer which rule is slow");
                    Assert.That(ruleSpans.All(s => s.Parent == run), Is.True,
                        "each rule is a child of the run it belongs to");
                });
            }
        }

        /// <summary>
        /// The default reaches the ENGINE, not just the constructor: a service built without a telemetry
        /// configuration must produce no per-rule spans, which is what a shipped installation looks like.
        /// </summary>
        [Test]
        public async Task AServiceWithNoTelemetryConfiguration_ProducesNoPerRuleSpans()
        {
            using (TelemetryCapture spansCapture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "WholeProjectValidator.Validate", "WholeProjectValidator.Rule" }))
            {
                var app = new ProjectAppService(TestSetup.Settings);
                Project project = await app.Load("testdata/projects/Project1-SimpelWired.vis");

                app.ValidateCategorized(project);

                Assert.Multiple(() =>
                {
                    Assert.That(spansCapture.Spans.Any(s => s.OperationName == "WholeProjectValidator.Validate"), Is.True,
                        "the run itself is always reported");
                    Assert.That(spansCapture.Spans.Any(s => s.OperationName == "WholeProjectValidator.Rule"), Is.False,
                        "per-rule spans stay off unless a host asks for them");
                });
            }
        }

        [Test]
        public async Task AServiceConfiguredForPerRuleTiming_ProducesThem()
        {
            using (TelemetryCapture spansCapture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "WholeProjectValidator.Validate", "WholeProjectValidator.Rule" }))
            {
                var app = new ProjectAppService(
                    TestSetup.Settings, new TelemetryConfiguration { PerRuleValidationTiming = true });
                Project project = await app.Load("testdata/projects/Project1-SimpelWired.vis");

                app.ValidateCategorized(project);

                Assert.That(spansCapture.Spans.Any(s => s.OperationName == "WholeProjectValidator.Rule"), Is.True,
                    "the configuration reaches the engine, not merely the constructor");
            }
        }
    }
}
