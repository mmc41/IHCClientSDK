using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;

namespace Ihc.Tests.Shared
{
    /// <summary>One measurement a <see cref="TelemetryCapture"/> observed, with its dimensions flattened.</summary>
    /// <param name="Instrument">The instrument's name.</param>
    /// <param name="Value">The value recorded, widened to double so a counter and a histogram read alike.</param>
    /// <param name="Tags">The measurement's dimensions.</param>
    internal readonly record struct CapturedPoint(
        string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)
    {
        /// <summary>One dimension's value, or null when the measurement did not carry it.</summary>
        internal object? Tag(string key) => Tags.TryGetValue(key, out object? value) ? value : null;
    }

    /// <summary>
    /// Collects the spans and metric points one instrumentation scope emits, for the duration of a test.
    ///
    /// <para>Shared because a telemetry test is always the same three steps - attach a listener, run the
    /// operation, assert on what arrived - and only the third is about the thing under test. Every fixture
    /// re-authoring the first step means a change to HOW signal is captured (an ordering fix, a new tag shape,
    /// a leaked listener) has to be made once per fixture, and the one that is missed fails silently by
    /// observing nothing.</para>
    ///
    /// <para>Deliberately NOT a base class: a fixture may need two captures, or none, and inheritance would
    /// make the capture something a test IS rather than something it uses.</para>
    /// </summary>
    /// <remarks>
    /// The wire names are passed as LITERALS rather than as registry constants, and that is on purpose: a test
    /// that names the attribute through the same constant the product writes cannot see a rename, which is the
    /// change most likely to break a saved query on the backend.
    /// </remarks>
    internal sealed class TelemetryCapture : IDisposable
    {
        private readonly ConcurrentQueue<Activity> spans = new();
        private readonly ConcurrentQueue<CapturedPoint> points = new();
        private readonly ActivityListener? activityListener;
        private readonly MeterListener meterListener;

        private TelemetryCapture(string scopeName, string[]? spanNames, string? spanPrefix,
            string[]? instruments, bool listenToSpans)
        {
            if (listenToSpans)
            {
                activityListener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name == scopeName,
                    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                    ActivityStopped = activity =>
                    {
                        bool wanted =
                            (spanNames is null && spanPrefix is null)
                            || (spanNames is not null && Array.IndexOf(spanNames, activity.OperationName) >= 0)
                            || (spanPrefix is not null
                                && activity.OperationName.StartsWith(spanPrefix, StringComparison.Ordinal));
                        if (wanted)
                        {
                            spans.Enqueue(activity);
                        }
                    },
                };
                ActivitySource.AddActivityListener(activityListener);
            }

            meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    // By instrument NAME, not by meter identity: the scope's meter is a static built on first
                    // touch, so a fixture cannot hold the instance to compare against.
                    if (instrument.Meter.Name == scopeName
                        && (instruments is null || Array.IndexOf(instruments, instrument.Name) >= 0))
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            meterListener.SetMeasurementEventCallback<double>((i, v, tags, _) => Record(i, v, tags));
            meterListener.SetMeasurementEventCallback<long>((i, v, tags, _) => Record(i, v, tags));
            meterListener.Start();
        }

        /// <summary>Starts capturing everything one scope emits.</summary>
        /// <param name="scopeName">The instrumentation scope, i.e. the ActivitySource and Meter name.</param>
        /// <param name="spanNames">Operation names to keep, or null for every span in the scope.</param>
        /// <param name="spanPrefix">
        /// Keep spans whose operation name starts with this, for a fixture that wants one owner's spans without
        /// naming each - an owner gaining an operation should widen the capture, not silently escape it.
        /// </param>
        /// <param name="instruments">Instrument names to keep, or null for every instrument in the scope.</param>
        internal static TelemetryCapture Listen(string scopeName, string[]? spanNames = null,
            string? spanPrefix = null, string[]? instruments = null) =>
            new(scopeName, spanNames, spanPrefix, instruments, listenToSpans: true);

        /// <summary>
        /// Captures metrics only, leaving tracing OFF - the state a host with no traces endpoint runs in, and
        /// the one where a scope that read its duration off the span rather than off a monotonic clock would
        /// silently record nothing.
        /// </summary>
        internal static TelemetryCapture ListenWithTracingDisabled(string scopeName, string[]? instruments = null) =>
            new(scopeName, null, null, instruments, listenToSpans: false);

        /// <summary>Every captured span, in the order the operations ENDED.</summary>
        internal IReadOnlyList<Activity> Spans => spans.ToArray();

        /// <summary>Every captured measurement, oldest first.</summary>
        internal IReadOnlyList<CapturedPoint> Points => points.ToArray();

        /// <summary>The one span with this operation name; fails the test when there is not exactly one.</summary>
        internal Activity Span(string operationName) =>
            spans.Single(s => s.OperationName == operationName);

        /// <summary>
        /// A span's tags as one text, for a fixture asserting that a value IS or is NOT among what an
        /// exporter would ship. Shared because asserting over the joined text rather than over one tag is
        /// what makes a redaction test see a credential that moved to a different tag.
        /// </summary>
        internal static string TagText(Activity span)
        {
            ArgumentNullException.ThrowIfNull(span);
            return string.Join("\n", span.TagObjects.Select(t => $"{t.Key}={t.Value}"));
        }

        /// <summary>Every span with this operation name, in the order they started.</summary>
        internal IReadOnlyList<Activity> SpansNamed(string operationName) =>
            spans.Where(s => s.OperationName == operationName).OrderBy(s => s.StartTimeUtc).ToArray();

        /// <summary>The first measurement of this instrument, or null when it recorded none.</summary>
        internal CapturedPoint? Point(string instrument) =>
            points.Cast<CapturedPoint?>().FirstOrDefault(p => p!.Value.Instrument == instrument);

        /// <summary>Every measurement of this instrument, oldest first.</summary>
        internal IReadOnlyList<CapturedPoint> PointsOf(string instrument) =>
            points.Where(p => p.Instrument == instrument).ToArray();

        /// <inheritdoc/>
        public void Dispose()
        {
            activityListener?.Dispose();
            meterListener.Dispose();
        }

        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            // Copied out of the span before it goes: the callback's tags are a stack buffer that does not
            // outlive the call, so anything the test reads later must be materialized here.
            var dimensions = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                dimensions[tag.Key] = tag.Value;
            }
            points.Enqueue(new CapturedPoint(instrument.Name, value, dimensions));
        }
    }
}
