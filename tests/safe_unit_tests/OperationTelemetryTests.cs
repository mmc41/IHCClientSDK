using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The instrumentation core: the one seam that mints a span, times an operation and records its
    /// instruments, so every later seam inherits the same guarantees instead of restating them.
    ///
    /// What is pinned here is precisely what a hand-written copy gets wrong: that a refusal is not an error,
    /// that the duration comes from a monotonic clock rather than the span (which is null when tracing is
    /// off), that a metric point is recorded while the span is still live so it can carry an exemplar, and
    /// that a fault in the telemetry itself never reaches the caller's operation.
    /// </summary>
    [TestFixture]
    public class OperationTelemetryTests
    {
        private const string ScopeName = "Ihc.CoreTests";

        private sealed class Harness : IDisposable
        {
            public TelemetrySurface Surface { get; }
            public OperationTelemetry Telemetry { get; }
            public Histogram<double> Duration { get; }
            public Counter<long> Count { get; }
            public MetricBinding Binding { get; }

            public List<Activity> Spans { get; } = new();
            public List<(string Instrument, double Value, Dictionary<string, object?> Tags)> Points { get; } = new();

            private readonly ActivityListener? activityListener;
            private readonly MeterListener meterListener;

            public Harness(bool tracingEnabled = true)
            {
                // A distinct scope per harness keeps concurrent fixtures from reading each other's signal.
                Surface = new TelemetrySurface(ScopeName + "." + Guid.NewGuid().ToString("N"));
                Telemetry = new OperationTelemetry(Surface, "TestOwner");
                Duration = Surface.Meter.CreateHistogram<double>("ihc.test.duration", unit: "s");
                Count = Surface.Meter.CreateCounter<long>("ihc.test.count", unit: "{operation}");
                Binding = MetricBinding.For(Duration, Count);

                if (tracingEnabled)
                {
                    activityListener = new ActivityListener
                    {
                        ShouldListenTo = source => source.Name == Surface.ActivitySource.Name,
                        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                        ActivityStopped = Spans.Add,
                    };
                    ActivitySource.AddActivityListener(activityListener);
                }

                meterListener = new MeterListener
                {
                    InstrumentPublished = (instrument, listener) =>
                    {
                        if (instrument.Meter == Surface.Meter) listener.EnableMeasurementEvents(instrument);
                    }
                };
                meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
                meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
                meterListener.Start();
            }

            private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            {
                var dict = new Dictionary<string, object?>();
                foreach (KeyValuePair<string, object?> tag in tags) dict[tag.Key] = tag.Value;
                Points.Add((instrument.Name, value, dict));
            }

            public (string Instrument, double Value, Dictionary<string, object?> Tags) Point(string name) =>
                Points.Find(p => p.Instrument == name);

            public void Dispose()
            {
                meterListener.Dispose();
                activityListener?.Dispose();
                Surface.Dispose();
            }
        }

        // S8 test 1: the shape every seam inherits - name, timing and a recorded point.
        [Test]
        public void Run_NamesTheSpanOwnerDotOperation_AndRecordsItsDuration()
        {
            using var h = new Harness();

            int result = h.Telemetry.Run("DoThing", _ => 42, h.Binding);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(42), "the core returns the body's value untouched");
                Assert.That(h.Spans, Has.Count.EqualTo(1));
                Assert.That(h.Spans[0].OperationName, Is.EqualTo("TestOwner.DoThing"));
                Assert.That(h.Point("ihc.test.duration").Value, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(h.Point("ihc.test.duration").Tags["ihc.operation.status"], Is.EqualTo("ok"));
            });
        }

        [Test]
        public void Run_WithACounterBinding_RecordsExactlyOneCountPerOperation()
        {
            using var h = new Harness();

            h.Telemetry.Run("DoThing", _ => 1, h.Binding);

            List<(string Instrument, double Value, Dictionary<string, object?> Tags)> counts =
                h.Points.FindAll(p => p.Instrument == "ihc.test.count");
            Assert.Multiple(() =>
            {
                Assert.That(counts, Has.Count.EqualTo(1), "one operation is one occurrence, never zero or two");
                Assert.That(counts[0].Value, Is.EqualTo(1));
            });
        }

        // S8 test 3: a refusal is not a failure. This is the distinction the hand-written copies lost.
        [Test]
        public void Run_WhenTheOutcomeIsRefused_LeavesTheSpanUnsetAndCarriesTheCode()
        {
            using var h = new Harness();

            h.Telemetry.Run("DoThing", _ => "refused", h.Binding,
                classify: _ => OperationOutcome.Refused("edit.locked"));

            Activity span = h.Spans[0];
            Assert.Multiple(() =>
            {
                Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Unset),
                    "a refusal is the system working as designed, not an error");
                Assert.That(span.GetTagItem("ihc.operation.status"), Is.EqualTo("refused"));
                Assert.That(span.GetTagItem("ihc.problem.code"), Is.EqualTo("edit.locked"));
                Assert.That(span.GetTagItem("error.type"), Is.Null, "a refusal has no error type");
                Assert.That(h.Point("ihc.test.duration").Tags["ihc.operation.status"], Is.EqualTo("refused"),
                    "the metric dimension must agree with the span, or the two cannot be joined");
            });
        }

        [Test]
        public void Run_WhenTheBodyThrows_MarksTheSpanErrorAndRethrows()
        {
            using var h = new Harness();

            Assert.Throws<InvalidOperationException>(() =>
                h.Telemetry.Run<int>("DoThing", _ => throw new InvalidOperationException("boom"), h.Binding));

            Activity span = h.Spans[0];
            Assert.Multiple(() =>
            {
                Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error));
                Assert.That(span.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
                Assert.That(h.Point("ihc.test.duration").Tags["ihc.operation.status"], Is.EqualTo("failed"),
                    "a failed operation is still timed - excluding it would flatter every latency graph");
            });
        }

        /// <summary>
        /// The reason the duration may not come from <see cref="Activity.Duration"/>: with tracing off there is
        /// no Activity at all, and the metric must keep working. This is S4.2's requirement, and it is the one
        /// a hand-written copy silently breaks the moment tracing is disabled in production.
        /// </summary>
        [Test]
        public void Run_WithTracingDisabled_StillRecordsTheHistogram_AndProducesNoSpan()
        {
            using var h = new Harness(tracingEnabled: false);

            int result = h.Telemetry.Run("DoThing", scope =>
            {
                Assert.That(scope.Activity, Is.Null, "no listener means StartActivity returns null");
                return 7;
            }, h.Binding);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(7));
                Assert.That(h.Spans, Is.Empty);
                Assert.That(h.Points.FindAll(p => p.Instrument == "ihc.test.duration"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task RunAsync_BehavesAsTheSynchronousForm()
        {
            using var h = new Harness();

            int result = await h.Telemetry.RunAsync("DoThingAsync", async _ =>
            {
                await Task.Yield();
                return 5;
            }, h.Binding);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(5));
                Assert.That(h.Spans[0].OperationName, Is.EqualTo("TestOwner.DoThingAsync"));
                Assert.That(h.Point("ihc.test.duration").Tags["ihc.operation.status"], Is.EqualTo("ok"));
            });
        }

        [Test]
        public void Start_TakesTheActivityKindAndItsCreationTimeLinks()
        {
            using var h = new Harness();
            using var parent = new Activity("external").Start();
            var link = new ActivityLink(parent.Context);

            using (OperationScope scope = h.Telemetry.Start("Fetch", ActivityKind.Client, h.Binding, new[] { link }))
            {
                Assert.That(scope.Activity, Is.Not.Null);
            }

            Activity span = h.Spans[0];
            Assert.Multiple(() =>
            {
                Assert.That(span.Kind, Is.EqualTo(ActivityKind.Client));
                Assert.That(span.Links, Is.Not.Empty, "a link must be supplied at creation - it cannot be added later");
            });
        }

        /// <summary>
        /// Fail-open. Instrumentation is never the point of the operation, so a fault in the scaffold must not
        /// become the caller's problem - here a classifier that throws.
        /// </summary>
        [Test]
        public void Run_WhenTheTelemetryItselfFaults_TheOperationStillSucceeds()
        {
            using var h = new Harness();

            int result = h.Telemetry.Run("DoThing", _ => 99, h.Binding,
                classify: _ => throw new InvalidOperationException("a broken classifier"));

            Assert.That(result, Is.EqualTo(99), "the body's result must survive a fault in the instrumentation");
        }

        // --- error.type normalization policy ------------------------------------------------------
        //
        // error.type is a metric DIMENSION as well as a span attribute, so its cardinality is a cost: an
        // unbounded value multiplies every series it appears in. The policy is therefore a descending
        // ladder of bounded sources, ending at a single catch-all rather than at whatever a CLR type or an
        // exception message happened to say.

        [Test]
        public void ErrorType_Tier1_PrefersTheProblemCodeCarriedByTheException()
        {
            using var h = new Harness();

            Assert.Throws<Ihc.Vis.Problems.RefusedWriteException>(() => h.Telemetry.Run<int>("DoThing",
                _ => throw new Ihc.Vis.Problems.RefusedWriteException(
                    Ihc.Vis.Io.SaveRefusalCodes.TargetUnwritable, "the target could not be written"),
                h.Binding));

            Assert.That(h.Spans[0].GetTagItem("error.type"), Is.EqualTo("save-target-unwritable"),
                "a coded failure names itself; the CLR type is an implementation detail of how it travelled");
        }

        [Test]
        public void ErrorType_Tier2_UsesACallerSuppliedCode_SuchAsAnHttpStatus()
        {
            using var h = new Harness();

            h.Telemetry.Run("DoThing", _ => "unused", h.Binding,
                classify: _ => OperationOutcome.FailedWith("500"));

            Assert.Multiple(() =>
            {
                Assert.That(h.Spans[0].GetTagItem("error.type"), Is.EqualTo("500"));
                Assert.That(h.Spans[0].Status, Is.EqualTo(ActivityStatusCode.Error));
            });
        }

        [Test]
        public void ErrorType_Tier3_UsesTheClrTypeNameWhenItIsAllowlisted()
        {
            using var h = new Harness();

            Assert.Throws<System.IO.FileNotFoundException>(() =>
                h.Telemetry.Run<int>("DoThing", _ => throw new System.IO.FileNotFoundException("nope"), h.Binding));

            Assert.That(h.Spans[0].GetTagItem("error.type"), Is.EqualTo("System.IO.FileNotFoundException"));
        }

        [Test]
        public void ErrorType_Tier4_FallsBackToOther_ForAnUnknownExceptionType()
        {
            using var h = new Harness();

            Assert.Throws<UnknownFailure>(() =>
                h.Telemetry.Run<int>("DoThing", _ => throw new UnknownFailure("a very specific message"), h.Binding));

            Assert.Multiple(() =>
            {
                Assert.That(h.Spans[0].GetTagItem("error.type"), Is.EqualTo("_OTHER"),
                    "an unlisted type must not become a new dimension value on every deployment");
                Assert.That(h.Spans[0].GetTagItem("error.type")?.ToString(), Does.Not.Contain("very specific"),
                    "error.type is NEVER derived from an exception message, on any path");
            });
        }

        private sealed class UnknownFailure : Exception
        {
            public UnknownFailure(string message) : base(message) { }
        }

        /// <summary>
        /// The cross-signal check. If the span says one error type and the histogram says another, a latency
        /// graph broken down by error type cannot be reconciled with the traces behind it - and the
        /// discrepancy is invisible in each signal on its own.
        /// </summary>
        [Test]
        public void ErrorType_IsTheSameOnTheSpanAndTheHistogram_AndAbsentOnSuccess()
        {
            using (var failing = new Harness())
            {
                Assert.Throws<System.IO.FileNotFoundException>(() =>
                    failing.Telemetry.Run<int>("DoThing", _ => throw new System.IO.FileNotFoundException(), failing.Binding));

                object? onSpan = failing.Spans[0].GetTagItem("error.type");
                object? onMetric = failing.Point("ihc.test.duration").Tags["error.type"];
                Assert.Multiple(() =>
                {
                    Assert.That(onSpan, Is.EqualTo("System.IO.FileNotFoundException"));
                    Assert.That(onMetric, Is.EqualTo(onSpan), "the two signals must agree or they cannot be joined");
                });
            }

            using var ok = new Harness();
            ok.Telemetry.Run("DoThing", _ => 1, ok.Binding);
            Assert.Multiple(() =>
            {
                Assert.That(ok.Spans[0].GetTagItem("error.type"), Is.Null);
                Assert.That(ok.Point("ihc.test.duration").Tags.ContainsKey("error.type"), Is.False,
                    "a successful operation carries no error dimension, so the success series stays one series");
            });
        }

        [Test]
        public void Scope_CarriesCallerSuppliedMetricDimensions()
        {
            using var h = new Harness();

            h.Telemetry.Run("DoThing", scope =>
            {
                scope.AddMetricTag("ihc.service", "ControllerService");
                return 0;
            }, h.Binding);

            Assert.That(h.Point("ihc.test.duration").Tags["ihc.service"], Is.EqualTo("ControllerService"));
        }
    }
}
