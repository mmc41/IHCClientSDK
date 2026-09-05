using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// M2/D02 (T012): <see cref="ProjectAppService"/>'s public methods run inside
    /// <c>AppServiceBase.RunTraced</c>, so each emits an activity named <c>&lt;ServiceType&gt;.&lt;method&gt;</c>
    /// and — when the body throws — tags the activity's status <see cref="ActivityStatusCode.Error"/>. That is the
    /// StartActivity + try/catch + SetError scaffold, defined once, instead of copied into every method body.
    /// </summary>
    public class RunTracedTests
    {
        /// <summary>
        /// The FAULT PORT (D16): an unexpected fault escaping a traced operation is reported before the original
        /// exception continues. Both halves matter and neither implies the other — a channel that reported and
        /// swallowed would silently change every caller's contract, and one that rethrew without reporting is
        /// what this exists to replace.
        /// </summary>
        [Test]
        public void AFaultingOperation_ReportsToThePort_AndStillThrowsTheOriginalException()
        {
            List<Ihc.Vis.Problems.InternalError> reported = [];
            var app = new ProjectAppService(TestSetup.Settings, reported.Add);
            string missing = "does-not-exist-" + nameof(AFaultingOperation_ReportsToThePort_AndStillThrowsTheOriginalException) + ".vis";

            System.Exception thrown = Assert.CatchAsync(async () => await app.Load(missing))!;

            Assert.Multiple(() =>
            {
                Assert.That(reported, Has.Count.EqualTo(1), "the fault reached the port exactly once");
                Ihc.Vis.Problems.InternalError fault = reported[0];
                Assert.That(fault.Code.Value, Is.EqualTo("internal.unexpected"));
                Assert.That(fault.Origin, Is.EqualTo(Ihc.Vis.Problems.InternalErrorOrigin.Sdk));
                Assert.That(fault.Message, Is.EqualTo("Uventet fejl under 'Load'."),
                    "the {operation} slot binds from the operation name RunTraced already takes — no site "
                    + "passes a duplicated literal");
                Assert.That(fault.Detail, Does.Contain(missing),
                    "the exception is captured as text, so the detail still names what could not be found");
                Assert.That(thrown.Message, Is.EqualTo(fault.Diagnostic),
                    "and the caller's own exception is what surfaced, unchanged");
            });
        }

        /// <summary>Without a port there is nothing to report to, and the operation behaves exactly as before.</summary>
        [Test]
        public void AFaultingOperation_WithNoPort_StillThrows()
        {
            var app = new ProjectAppService(TestSetup.Settings);

            Assert.CatchAsync(async () => await app.Load("does-not-exist-" + nameof(AFaultingOperation_WithNoPort_StillThrows) + ".vis"));
        }

        /// <summary>
        /// FAIL-OPEN: a port that throws must not turn a reportable fault into a second, worse one on top of the
        /// caller's original. The caller still sees its own exception, not the port's.
        /// </summary>
        [Test]
        public void APortThatThrows_DoesNotReplaceTheCallersOwnException()
        {
            var app = new ProjectAppService(TestSetup.Settings,
                _ => throw new System.InvalidOperationException("the port is broken"));

            System.Exception thrown = Assert.CatchAsync(
                async () => await app.Load("does-not-exist-" + nameof(APortThatThrows_DoesNotReplaceTheCallersOwnException) + ".vis"))!;

            Assert.That(thrown.Message, Does.Not.Contain("the port is broken"),
                "the port's own failure is dropped; the caller's work reports what actually went wrong with it");
        }

        [Test]
        public void ProjectAppServiceMethods_EmitNamedActivity_AndTagErrorWhenBodyThrows()
        {
            var exported = new List<(string Name, ActivityStatusCode Status)>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == Telemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => exported.Add((activity.OperationName, activity.Status)),
            };
            ActivitySource.AddActivityListener(listener);

            var app = new ProjectAppService(TestSetup.Settings);

            // A successful call emits an activity named for the method.
            app.GetAvailableProducts();

            // A body that throws (a missing file inside Load's RunTracedAsync) tags the activity Error and rethrows.
            Assert.CatchAsync(async () => await app.Load("does-not-exist-" + nameof(RunTracedTests) + ".vis"));

            Assert.Multiple(() =>
            {
                Assert.That(exported.Any(a => a.Name == "ProjectAppService.GetAvailableProducts"), Is.True,
                    "a successful method emits an activity named <service>.<method>");
                (string Name, ActivityStatusCode Status) load = exported.Last(a => a.Name == "ProjectAppService.Load");
                Assert.That(load.Status, Is.EqualTo(ActivityStatusCode.Error),
                    "a throwing body is tagged Error via RunTraced's SetError, not left Unset");
            });
        }

        /// <summary>
        /// The app-side half of the same pin as
        /// <c>UserManagerServiceTelemetryTests.GetUsers_SpanDoesNotSquatTheResourceLevelServiceKeys</c>:
        /// <c>service.name</c> is a Resource-level key describing the process, and a span that sets it to the
        /// service class name collides with the host's value in the backend. The span name already says
        /// <c>&lt;Service&gt;.&lt;operation&gt;</c>, so <c>service.operation</c> added nothing either.
        /// </summary>
        [Test]
        public void ProjectAppServiceSpans_DoNotSquatTheResourceLevelServiceKeys()
        {
            var tags = new Dictionary<string, object?>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == Telemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName == "ProjectAppService.GetAvailableProducts")
                    {
                        foreach (var tag in activity.TagObjects)
                        {
                            tags[tag.Key] = tag.Value;
                        }
                    }
                },
            };
            ActivitySource.AddActivityListener(listener);

            new ProjectAppService(TestSetup.Settings).GetAvailableProducts();

            Assert.Multiple(() =>
            {
                Assert.That(tags, Is.Not.Empty, "the span must still be produced and still carry its own tags");
                Assert.That(tags, Does.Not.ContainKey("service.name"));
                Assert.That(tags, Does.Not.ContainKey("service.operation"));
            });
        }

        /// <summary>
        /// Routing the scaffold through the instrumentation core must be behaviour-PRESERVING for the things
        /// callers and dashboards already depend on - the span's name and its Error-on-throw status - while
        /// adding what the core guarantees: the normalized error type, and a metric recorded from the same
        /// operation so the two signals cannot disagree.
        /// </summary>
        [Test]
        public void RunTraced_OverTheCore_KeepsTheNameAndErrorStatus_AndNowRecordsTheOutcomeOnBothSignals()
        {
            // Concurrent collections and a name filter: the listener is process-wide, so every other fixture's
            // spans on this source arrive here too, on their own threads. A plain List would be written
            // concurrently - which is a flaky test rather than a failing one, and far harder to diagnose.
            var spans = new System.Collections.Concurrent.ConcurrentBag<Activity>();
            var points = new System.Collections.Concurrent.ConcurrentQueue<(double Value, Dictionary<string, object?> Tags)>();

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == Telemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName.StartsWith("TracedProbe.", System.StringComparison.Ordinal))
                    {
                        spans.Add(activity);
                    }
                },
            };
            ActivitySource.AddActivityListener(listener);

            using var meter = new Meter("Ihc.RunTracedTests." + System.Guid.NewGuid().ToString("N"));
            Histogram<double> duration = meter.CreateHistogram<double>("ihc.test.runtraced.duration", unit: "s");
            using var meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter == meter) l.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            {
                var dict = new Dictionary<string, object?>();
                foreach (KeyValuePair<string, object?> tag in tags) dict[tag.Key] = tag.Value;
                points.Enqueue((value, dict));
            });
            meterListener.Start();

            var probe = new TracedProbe(MetricBinding.For(duration));

            probe.Succeed();
            Assert.Throws<System.IO.FileNotFoundException>(() => probe.Fail());

            Activity success = spans.Single(s => s.OperationName == "TracedProbe.Succeed");
            Activity failure = spans.Single(s => s.OperationName == "TracedProbe.Fail");

            Assert.Multiple(() =>
            {
                Assert.That(success.Status, Is.EqualTo(ActivityStatusCode.Unset), "a success is not an error");
                Assert.That(failure.Status, Is.EqualTo(ActivityStatusCode.Error),
                    "the Error-on-throw behaviour the scaffold already had must survive the migration");
                Assert.That(failure.GetTagItem("error.type"), Is.EqualTo("System.IO.FileNotFoundException"),
                    "the whole point of routing through the core: the error-type policy now applies here too");

                var recorded = points.ToArray();
                Assert.That(recorded, Has.Length.EqualTo(2), "one point per operation, success and failure alike");
                Assert.That(recorded[0].Tags["ihc.operation.status"], Is.EqualTo("ok"));
                Assert.That(recorded[1].Tags["ihc.operation.status"], Is.EqualTo("failed"));
                Assert.That(recorded[1].Tags["error.type"], Is.EqualTo(failure.GetTagItem("error.type")),
                    "span and metric must carry the same error type or they cannot be joined");
            });
        }

        /// <summary>A minimal <see cref="AppServiceBase"/> so the scaffold is exercised without a real service.</summary>
        private sealed class TracedProbe : AppServiceBase
        {
            private readonly MetricBinding binding;

            public TracedProbe(MetricBinding binding) => this.binding = binding;

            public int Succeed() => RunTraced(nameof(Succeed), _ => 1, binding);

            public int Fail() => RunTraced<int>(nameof(Fail),
                _ => throw new System.IO.FileNotFoundException("missing"), binding);
        }
    }
}
