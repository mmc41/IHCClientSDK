using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc;
using Ihc.Soap.Resourceinteraction;
using NUnit.Framework;
// Ihc.ResourceInteractionService (the SDK service) and Ihc.Soap.Resourceinteraction.ResourceInteractionService
// (the generated SOAP contract it wraps) differ only by namespace; alias the contract to keep the two apart.
using SoapContract = Ihc.Soap.Resourceinteraction.ResourceInteractionService;

namespace Ihc.Tests
{
    /// <summary>
    /// Telemetry lifetime of the long-polling streams (<c>GetResourceValueChanges</c>).
    ///
    /// These methods return a lazy <see cref="IAsyncEnumerable{T}"/>: an async iterator's body does not run when
    /// the method is called, only when the consumer iterates. A span opened with <c>using</c> around the *call*
    /// is therefore already stopped and exported before the polling loop reports anything, so the loop's errors
    /// land on a dead span. The suite pins that the span stays live for as long as the stream it describes.
    /// </summary>
    [TestFixture]
    public class StreamingTelemetryTests
    {
        /// <summary>
        /// Records what an exporter would receive. Status is snapshotted *at stop time* on purpose: an
        /// <see cref="Activity"/> stays mutable after <c>Stop()</c>, so a listener holding the live object would
        /// observe late writes that a real exporter — which has already been handed the span — never sees.
        /// Asserting on the live object hides exactly the defect under test.
        /// </summary>
        private sealed class ExportedSpans : IDisposable
        {
            private readonly ActivityListener listener;

            public List<(string Operation, ActivityStatusCode StatusAtStop)> Exported { get; } = new();

            public ExportedSpans()
            {
                listener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name == Telemetry.ActivitySourceName,
                    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                    ActivityStopped = activity => Exported.Add((activity.OperationName, activity.Status))
                };
                ActivitySource.AddActivityListener(listener);
            }

            public ActivityStatusCode StatusOf(string operation) =>
                Exported.Single(e => e.Operation.EndsWith("." + operation, StringComparison.Ordinal)).StatusAtStop;

            public bool AnyExported(string operation) =>
                Exported.Any(e => e.Operation.EndsWith("." + operation, StringComparison.Ordinal));

            public void Dispose() => listener.Dispose();
        }

        private const string GetChanges = nameof(IResourceInteractionService.GetResourceValueChanges);

        private static Ihc.ResourceInteractionService NewService(SoapContract soap)
        {
            var auth = A.Fake<IAuthenticationService>();
            A.CallTo(() => auth.IhcSettings).Returns(new IhcSettings { Endpoint = "http://unit.test.local" });
            return new Ihc.ResourceInteractionService(auth, soap);
        }

        /// <summary>
        /// A SOAP layer that subscribes happily, then fails the long poll. When <paramref name="stopAfterFirstPoll"/>
        /// is supplied it is cancelled as the first poll fails, so the loop exits on the first error instead of
        /// spending its ten-failure retry budget (~38s of backoff).
        /// </summary>
        private static SoapContract PollingFailsSoap(string message, CancellationTokenSource? stopAfterFirstPoll = null)
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.enableRuntimeValueNotificationsAsync(A<inputMessageName4>._))
                .Returns(Task.FromResult(new outputMessageName4(Array.Empty<WSResourceValueEnvelope>())));
            A.CallTo(() => soap.disableRuntimeValueNotifactionsAsync(A<inputMessageName5>._))
                .Returns(Task.FromResult(new outputMessageName5(true)));
            A.CallTo(() => soap.waitForResourceValueChangesAsync(A<inputMessageName8>._))
                .Throws(() =>
                {
                    stopAfterFirstPoll?.Cancel();
                    return new InvalidOperationException(message);
                });
            return soap;
        }

        /// <summary>
        /// The defect. The polling loop swallows its first ten failures by design (it retries), so telemetry is
        /// the only thing that can report them — and an operator sees nothing if the span is already exported.
        /// </summary>
        [Test]
        public async Task GetResourceValueChanges_PollingError_IsRecordedOnTheSpanBeforeItIsExported()
        {
            using var spans = new ExportedSpans();
            using var cts = new CancellationTokenSource();
            var service = NewService(PollingFailsSoap("controller refused the long poll", cts));

            try
            {
                await foreach (ResourceValue _ in service.GetResourceValueChanges(new[] { 1 }, cts.Token))
                {
                }
            }
            catch (OperationCanceledException) { /* expected: the loop is cancelled once the first poll fails */ }

            Assert.That(spans.StatusOf(GetChanges), Is.EqualTo(ActivityStatusCode.Error),
                "the polling loop's failure must be on the stream's span while it is still live, so it reaches the exporter");
        }

        /// <summary>
        /// The mechanism behind the defect, pinned directly: a span that is already stopped when the stream is
        /// handed back cannot describe iteration that has not happened yet.
        /// </summary>
        [Test]
        public void GetResourceValueChanges_DoesNotExportItsSpan_BeforeTheStreamIsIterated()
        {
            using var spans = new ExportedSpans();
            var service = NewService(PollingFailsSoap("unused"));

            _ = service.GetResourceValueChanges(new[] { 1 });

            Assert.That(spans.AnyExported(GetChanges), Is.False,
                "constructing the stream must not open and close a span describing work not yet done");
        }

        /// <summary>
        /// A non-streaming sibling for contrast: its span legitimately closes when the awaited call returns, so
        /// the streaming fix must not disturb the ordinary request/response shape.
        /// </summary>
        [Test]
        public void WaitForResourceValueChanges_NonStreaming_StillRecordsItsErrorOnItsOwnSpan()
        {
            using var spans = new ExportedSpans();
            var service = NewService(PollingFailsSoap("controller refused the long poll"));

            Assert.ThrowsAsync<InvalidOperationException>(() => service.WaitForResourceValueChanges(1));

            Assert.That(spans.StatusOf(nameof(IResourceInteractionService.WaitForResourceValueChanges)),
                Is.EqualTo(ActivityStatusCode.Error));
        }
    }
}
