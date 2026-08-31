#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace Ihc
{
    /// <summary>How an instrumented operation ended.</summary>
    public enum OperationStatus
    {
        /// <summary>The operation did what it was asked to do.</summary>
        Ok,

        /// <summary>
        /// The operation declined, by design and with a reason. NOT an error: a refused edit is the rules
        /// working, and marking it Error makes a healthy session look broken and hides the real failures.
        /// </summary>
        Refused,

        /// <summary>The operation could not complete. This alone sets the span's status to Error.</summary>
        Failed,
    }

    /// <summary>
    /// The result an instrumented operation reports. Three cases, because two is the mistake: collapsing
    /// <see cref="OperationStatus.Refused"/> into failure is what makes a refusal indistinguishable from a
    /// defect, and collapsing it into success loses the reason entirely.
    /// </summary>
    public readonly struct OperationOutcome : IEquatable<OperationOutcome>
    {
        private OperationOutcome(OperationStatus status, string? problemCode, Exception? exception)
        {
            Status = status;
            ProblemCode = problemCode;
            Exception = exception;
        }

        /// <summary>Which of the three cases this is.</summary>
        public OperationStatus Status { get; }

        /// <summary>The refusal's coded reason, or null when the outcome is not a refusal.</summary>
        public string? ProblemCode { get; }

        /// <summary>The failure's exception when there was one; null otherwise.</summary>
        public Exception? Exception { get; }

        /// <summary>The operation succeeded.</summary>
        public static OperationOutcome Ok { get; } = new(OperationStatus.Ok, null, null);

        /// <summary>The operation declined for the given coded reason.</summary>
        /// <param name="problemCode">The catalogue code naming the refusal.</param>
        public static OperationOutcome Refused(string problemCode) =>
            new(OperationStatus.Refused, problemCode, null);

        /// <summary>The operation could not complete, because of <paramref name="exception"/>.</summary>
        public static OperationOutcome Failed(Exception exception) =>
            new(OperationStatus.Failed, null, exception);

        /// <summary>The operation could not complete, with a coded reason rather than an exception.</summary>
        /// <param name="problemCode">The catalogue code naming the failure.</param>
        public static OperationOutcome FailedWith(string problemCode) =>
            new(OperationStatus.Failed, problemCode, null);

        /// <inheritdoc/>
        public bool Equals(OperationOutcome other) =>
            Status == other.Status && ProblemCode == other.ProblemCode && Exception == other.Exception;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is OperationOutcome other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Status, ProblemCode, Exception);

        /// <summary>Equality operator.</summary>
        public static bool operator ==(OperationOutcome left, OperationOutcome right) => left.Equals(right);

        /// <summary>Inequality operator.</summary>
        public static bool operator !=(OperationOutcome left, OperationOutcome right) => !left.Equals(right);
    }

    /// <summary>
    /// One layer's instrumentation scope: its <see cref="System.Diagnostics.ActivitySource"/> and its
    /// <see cref="System.Diagnostics.Metrics.Meter"/> under a single name, so a host registers the layer once
    /// and its two signals cannot drift into separate identities.
    /// <para>The surface owns no instruments. Instruments are declared by the layer's registry, which
    /// constructs them from this surface's meter.</para>
    /// </summary>
    public sealed class TelemetrySurface : IDisposable
    {
        private readonly bool ownsComponents;

        /// <summary>Creates the surface for one instrumentation scope.</summary>
        /// <param name="name">The scope name, e.g. the SDK's or the host's.</param>
        /// <param name="version">Optional scope version.</param>
        public TelemetrySurface(string name, string? version = null)
        {
            ActivitySource = new ActivitySource(name, version);
            Meter = new Meter(name, version);
            ownsComponents = true;
        }

        /// <summary>
        /// Adopts an activity source and meter the caller already owns, so a layer that published its
        /// <see cref="System.Diagnostics.ActivitySource"/> before this core existed keeps ONE source rather
        /// than gaining a second under the same name. The surface does not dispose what it did not create.
        /// </summary>
        public TelemetrySurface(ActivitySource activitySource, Meter meter)
        {
            ActivitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
            Meter = meter ?? throw new ArgumentNullException(nameof(meter));
            ownsComponents = false;
        }

        /// <summary>The layer's activity source.</summary>
        public ActivitySource ActivitySource { get; }

        /// <summary>The layer's meter.</summary>
        public Meter Meter { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!ownsComponents)
            {
                return;
            }
            ActivitySource.Dispose();
            Meter.Dispose();
        }
    }

    /// <summary>
    /// The instruments one operation records into: a duration histogram, an occurrence counter, or both.
    /// <para>Both are optional and both are recorded through the same door, because a seam that wants only a
    /// count (an edit applied, a command invoked, a problem raised) and one that wants only a latency are the
    /// same shape of decision - and a caller that had to reach past the core for one of them would be
    /// recording outside the guarantees the core exists to provide.</para>
    /// </summary>
    public sealed class MetricBinding
    {
        private MetricBinding(Histogram<double>? duration, Counter<long>? occurrences)
        {
            Duration = duration;
            Occurrences = occurrences;
        }

        /// <summary>Record nothing; the operation is traced only.</summary>
        public static MetricBinding None { get; } = new(null, null);

        /// <summary>Binds a duration histogram, an occurrence counter, or both.</summary>
        /// <param name="duration">Histogram recording the operation's duration in SECONDS.</param>
        /// <param name="occurrences">Counter incremented once per operation.</param>
        public static MetricBinding For(Histogram<double>? duration = null, Counter<long>? occurrences = null) =>
            new(duration, occurrences);

        internal Histogram<double>? Duration { get; }

        internal Counter<long>? Occurrences { get; }

        internal bool RecordsAnything => Duration is not null || Occurrences is not null;

        /// <summary>Whether a MeterProvider is currently collecting either bound instrument.</summary>
        internal bool IsCollecting => Duration?.Enabled == true || Occurrences?.Enabled == true;
    }

    /// <summary>
    /// Normalizes a failure to its <c>error.type</c>, in one place for both signals.
    ///
    /// <para><c>error.type</c> is a metric DIMENSION as well as a span attribute, so its cardinality is a
    /// running cost: every distinct value multiplies the series it appears in. The policy is therefore a
    /// descending ladder of BOUNDED sources ending at one catch-all, rather than whatever a CLR type name
    /// happened to be - and never, on any path, an exception's message, which is unbounded by construction
    /// and routinely carries a path or an identifier.</para>
    /// </summary>
    internal static class ErrorTypePolicy
    {
        /// <summary>The convention's catch-all for a failure with no bounded identity of its own.</summary>
        internal const string Other = "_OTHER";

        /// <summary>
        /// Exception types whose names may be used verbatim. An allowlist rather than "any CLR type" because
        /// the set of types a dependency can throw is not knowable in advance, and one unexpected type would
        /// otherwise mint a new dimension value in production.
        /// </summary>
        private static readonly HashSet<string> AllowedClrTypes = new(StringComparer.Ordinal)
        {
            "System.IO.FileNotFoundException",
            "System.IO.DirectoryNotFoundException",
            "System.IO.PathTooLongException",
            "System.IO.IOException",
            "System.UnauthorizedAccessException",
            "System.TimeoutException",
            "System.OperationCanceledException",
            "System.Threading.Tasks.TaskCanceledException",
            "System.Net.Http.HttpRequestException",
            "System.Net.Sockets.SocketException",
            "System.Xml.XmlException",
            "System.FormatException",
            "System.InvalidOperationException",
            "System.ArgumentException",
            "System.ArgumentNullException",
            "System.ArgumentOutOfRangeException",
            "System.NotSupportedException",
            "System.ObjectDisposedException",
        };

        /// <summary>The <c>error.type</c> for an outcome, or null when the outcome is not a failure.</summary>
        internal static string? Resolve(OperationOutcome outcome)
        {
            if (outcome.Status != OperationStatus.Failed)
            {
                return null;
            }

            // Tier 1 and 2: an identity the failure declared for itself - a catalogue code, or the protocol
            // status code an HTTP seam supplies through FailedWith.
            if (!string.IsNullOrEmpty(outcome.ProblemCode))
            {
                return outcome.ProblemCode;
            }

            if (outcome.Exception is null)
            {
                return Other;
            }

            // Tier 1 again, via the exception: the same chain-AND-aggregate probing the session layer does.
            // Testing only the chain is the half-honoured contract the widened interface exists to prevent.
            if (outcome.Exception is Ihc.Vis.Problems.IProblemCarrier carrier)
            {
                if (carrier.Problems is { } chain)
                {
                    return chain.Cause.Code.Value;
                }
                if (carrier.Aggregate is { } aggregate)
                {
                    return aggregate.Head.Code.Value;
                }
            }

            // Tier 3: a known CLR type. Tier 4: everything else.
            string? typeName = outcome.Exception.GetType().FullName;
            return typeName is not null && AllowedClrTypes.Contains(typeName) ? typeName : Other;
        }
    }

    /// <summary>
    /// One instrumented operation in flight. Disposing it records the outcome on the span and the bound
    /// instruments, in that order and while the span is still live.
    /// </summary>
    /// <remarks>
    /// <para>The scope tolerates a null <see cref="Activity"/> throughout. With tracing disabled
    /// <c>StartActivity</c> returns null, and the metrics must keep working regardless - which is also why the
    /// duration is measured from <see cref="Stopwatch.GetTimestamp"/> rather than read from the span.</para>
    ///
    /// <para><b>The duration spans the WHOLE using-block, including time a user spent looking at a dialog.</b>
    /// A host that reports a failure before the scope is disposed - which is the ordinary shape, because the
    /// report belongs inside the try/catch that caught the failure - bills that modal's lifetime to this
    /// operation. Two costs follow, and both are accepted rather than worked around:</para>
    /// <list type="bullet">
    /// <item><b>A failing operation's duration and histogram point include the installer reading a dialog.</b>
    /// A support query reading p95 on an apply or a workflow histogram is reading think-time on the failure
    /// path, and the number is otherwise indistinguishable from slow work. It is recorded here because a
    /// reader of the metric has no other way to know.</item>
    /// <item><b>A process that exits while a modal is open records NO span at all.</b> The scope never
    /// disposes, so the span and the metric point are lost - not delayed, lost. This is the half that loses
    /// data rather than distorting it, and it bites hardest where it matters most: a fatal fault whose dialog
    /// is on screen when the app goes down leaves the log record as its only survivor.</item>
    /// </list>
    /// <para>Two alternatives were considered and neither taken: disposing before awaiting the dialog, which
    /// would have fixed the second cost but split one operation's own outcome away from its span; and a
    /// suspend/resume pair on this type, which would have put a host's presentation concern into the SDK's
    /// measurement contract. Documenting the cost is cheaper than either, and honest.</para>
    /// </remarks>
    public sealed class OperationScope : IDisposable
    {
        private readonly MetricBinding metrics;
        private readonly long startTimestamp;
        private List<KeyValuePair<string, object?>>? extraTags;
        private OperationOutcome outcome = OperationOutcome.Ok;
        private bool disposed;

        internal OperationScope(Activity? activity, MetricBinding metrics)
        {
            Activity = activity;
            this.metrics = metrics;
            startTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>The span, or null when nothing is listening. Callers must tolerate null.</summary>
        public Activity? Activity { get; }

        /// <summary>Declares how the operation ended. The last call before disposal wins.</summary>
        public void SetOutcome(OperationOutcome value) => outcome = value;

        /// <summary>
        /// Adds a dimension to every instrument this scope records. Span-only attributes go on
        /// <see cref="Activity"/> instead - a metric dimension multiplies series, so it is a deliberate choice
        /// rather than a copy of everything the span carries.
        /// </summary>
        public void AddMetricTag(string key, object? value)
        {
            extraTags ??= new List<KeyValuePair<string, object?>>();
            extraTags.Add(new KeyValuePair<string, object?>(key, value));
        }

        /// <summary>
        /// Records <paramref name="key"/> on BOTH the span and every instrument this scope records - for a fact
        /// that is a span attribute AND a metric dimension, which is the only way a metric point can be joined
        /// back to the spans it came from. A site that writes only one half makes the two unjoinable, silently,
        /// so the pair belongs behind one door rather than at each call site.
        /// </summary>
        public void AddSharedTag(string key, object? value)
        {
            Activity?.SetTag(key, value);
            AddMetricTag(key, value);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            // Fail-open: instrumentation is never the point of the operation, so nothing here may escape.
            try
            {
                double elapsedSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
                ApplyOutcomeToSpan();
                RecordInstruments(elapsedSeconds);
            }
            catch (Exception)
            {
                // Deliberately swallowed. A broken tag or a disposed meter must not fail the caller's work.
            }
            finally
            {
                Activity?.Dispose();
            }
        }

        private void ApplyOutcomeToSpan()
        {
            if (Activity is null)
            {
                return;
            }

            Activity.SetTag(SdkTelemetryRegistry.Attributes.OperationStatus, StatusText(outcome.Status));

            if (outcome.ProblemCode is not null)
            {
                Activity.SetTag(SdkTelemetryRegistry.Attributes.ProblemCode, outcome.ProblemCode);
            }

            // Error is reserved for Failed. A refusal keeps the status Unset so a healthy session that
            // declines something does not read as a broken one.
            if (outcome.Status == OperationStatus.Failed)
            {
                Activity.SetTag(SdkTelemetryRegistry.Attributes.ErrorType, ErrorTypePolicy.Resolve(outcome));
                Activity.SetStatus(ActivityStatusCode.Error, outcome.Exception?.Message);
                if (outcome.Exception is not null)
                {
                    Activity.AddException(outcome.Exception);
                }
            }
        }

        private void RecordInstruments(double elapsedSeconds)
        {
            // Two gates, not one. RecordsAnything asks whether instruments were BOUND; Enabled asks whether a
            // MeterProvider is actually collecting them. Without the second, every disposal still builds the tag
            // list and re-resolves the error type for a Record call that discards them - on a path that runs
            // once per SOAP call, per edit and per command invocation.
            if (!metrics.RecordsAnything || !metrics.IsCollecting)
            {
                return;
            }

            var tags = new TagList { { SdkTelemetryRegistry.Attributes.OperationStatus, StatusText(outcome.Status) } };

            // The SAME value the span carries, from the same call. Two independent derivations would be two
            // things to keep in step, and a disagreement is invisible in either signal on its own.
            if (ErrorTypePolicy.Resolve(outcome) is { } errorType)
            {
                tags.Add(SdkTelemetryRegistry.Attributes.ErrorType, errorType);
            }

            if (extraTags is not null)
            {
                foreach (KeyValuePair<string, object?> tag in extraTags)
                {
                    tags.Add(tag.Key, tag.Value);
                }
            }

            // Recorded before the span is disposed, so an exemplar can attach the trace that produced the
            // point - which is what turns "the 99th percentile got worse" into a specific trace to open.
            metrics.Duration?.Record(elapsedSeconds, tags);
            metrics.Occurrences?.Add(1, tags);
        }

        private static string StatusText(OperationStatus status) => status switch
        {
            OperationStatus.Ok => SdkTelemetryRegistry.Values.StatusOk,
            OperationStatus.Refused => SdkTelemetryRegistry.Values.StatusRefused,
            OperationStatus.Failed => SdkTelemetryRegistry.Values.StatusFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown operation status"),
        };
    }

    /// <summary>
    /// The one seam that mints a span and records an operation's instruments, for one owner within a layer.
    ///
    /// <para>Two shapes, because two shapes are genuinely needed. <b>Shape A</b> (<see cref="Run{T}"/> /
    /// <see cref="RunAsync{T}"/>) wraps a delegate and is what an ordinary operation uses. <b>Shape B</b>
    /// (<see cref="Start"/>) hands back the scope for a site whose work does not fit inside one delegate -
    /// a handler that must return a value it is still building, or a span needing a non-default
    /// <see cref="ActivityKind"/> or creation-time links.</para>
    ///
    /// <para>Both guarantee the same things: the span is named <c>&lt;Owner&gt;.&lt;operation&gt;</c>, the
    /// duration is measured on a monotonic clock and works with tracing off, instruments are recorded while
    /// the span is live, only a failure sets status Error, and a fault in the instrumentation never reaches
    /// the caller.</para>
    /// </summary>
    public sealed class OperationTelemetry
    {
        private readonly TelemetrySurface surface;
        private readonly string owner;

        /// <summary>Creates the telemetry entry point for one owner.</summary>
        /// <param name="surface">The layer's activity source and meter.</param>
        /// <param name="owner">
        /// The name spans are prefixed with, normally the owning type's name. EMPTY means no prefix, for the
        /// one case where a span's name is fixed by a semantic convention rather than by its owner: an HTTP
        /// client span is named for its method, and a backend's HTTP views key on exactly that.
        /// </param>
        public OperationTelemetry(TelemetrySurface surface, string owner)
            : this(surface, owner, null)
        {
        }

        /// <summary>
        /// The same, with a FAULT REPORTER: an exception escaping one of the <c>Run</c> shapes is handed to
        /// <paramref name="onFault"/> before it continues on its way.
        /// </summary>
        /// <remarks>
        /// Reported from the catch that already runs, rather than from a second catch an owner wraps around
        /// these calls. One frame reports for every owner of the core, and an owner cannot forget to.
        /// <para>
        /// FAIL-OPEN. A reporter that throws must not turn a reportable fault into a second one raised in place
        /// of the first — the caller's own exception is what the caller is owed, unchanged.
        /// </para>
        /// </remarks>
        /// <param name="surface">The layer's activity source and meter.</param>
        /// <param name="owner">As above.</param>
        /// <param name="onFault">Receives the operation name and the escaping exception, or null to report
        /// nowhere.</param>
        public OperationTelemetry(TelemetrySurface surface, string owner, Action<string, Exception>? onFault)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.onFault = onFault;
        }

        private readonly Action<string, Exception>? onFault;

        private void Report(string operation, Exception failure)
        {
            if (onFault is null)
            {
                return;
            }
            try
            {
                onFault(operation, failure);
            }
            catch (Exception)
            {
                // See the fail-open note on the constructor.
            }
        }

        private string SpanName(string operation) =>
            owner.Length == 0 ? operation : $"{owner}.{operation}";

        /// <summary>
        /// Starts an operation and hands back its scope (Shape B). The caller owns disposal, and should
        /// declare the outcome through <see cref="OperationScope.SetOutcome"/> before disposing.
        /// </summary>
        /// <param name="operation">Operation name; the span becomes <c>&lt;Owner&gt;.&lt;operation&gt;</c>.</param>
        /// <param name="kind">The span kind.</param>
        /// <param name="metrics">Instruments to record, or null for none.</param>
        /// <param name="links">Links, which can only be supplied when the span is created.</param>
        public OperationScope Start(string operation, ActivityKind kind = ActivityKind.Internal,
            MetricBinding? metrics = null, IEnumerable<ActivityLink>? links = null) =>
            new(StartSpan(operation, kind, links), metrics ?? MetricBinding.None);

        /// <summary>
        /// Mints the span and NOTHING else, for a legacy helper whose shipped signature returns a bare
        /// <see cref="Activity"/>. Such a helper cannot own a scope: its caller owns the <c>using</c>, so the
        /// helper is not present when the operation ends and could never record an outcome or a duration.
        /// Handing it a scope it will not dispose would be worse than not making one.
        /// </summary>
        internal Activity? StartSpan(string operation, ActivityKind kind = ActivityKind.Internal,
            IEnumerable<ActivityLink>? links = null) =>
            surface.ActivitySource.StartActivity(SpanName(operation), kind, default(ActivityContext), links: links);

        /// <summary>Runs <paramref name="body"/> inside an instrumented operation (Shape A).</summary>
        /// <param name="operation">Operation name.</param>
        /// <param name="body">The work, which receives the scope.</param>
        /// <param name="metrics">Instruments to record, or null for none.</param>
        /// <param name="classify">
        /// Derives the outcome from the returned value, so a body with many outcome-producing exits is
        /// classified once over what it returned rather than tagged at every exit.
        /// </param>
        public T Run<T>(string operation, Func<OperationScope, T> body,
            MetricBinding? metrics = null, Func<T, OperationOutcome>? classify = null)
        {
            using OperationScope scope = Start(operation, ActivityKind.Internal, metrics);
            try
            {
                T result = body(scope);
                Classify(scope, result, classify);
                return result;
            }
            catch (Exception ex)
            {
                scope.SetOutcome(OperationOutcome.Failed(ex));
                Report(operation, ex);
                throw;
            }
        }

        /// <summary>Runs a void <paramref name="body"/> inside an instrumented operation (Shape A).</summary>
        public void Run(string operation, Action<OperationScope> body, MetricBinding? metrics = null)
        {
            using OperationScope scope = Start(operation, ActivityKind.Internal, metrics);
            try
            {
                body(scope);
            }
            catch (Exception ex)
            {
                scope.SetOutcome(OperationOutcome.Failed(ex));
                Report(operation, ex);
                throw;
            }
        }

        /// <summary>The asynchronous <see cref="Run{T}"/>.</summary>
        public async Task<T> RunAsync<T>(string operation, Func<OperationScope, Task<T>> body,
            MetricBinding? metrics = null, Func<T, OperationOutcome>? classify = null)
        {
            using OperationScope scope = Start(operation, ActivityKind.Internal, metrics);
            try
            {
                T result = await body(scope).ConfigureAwait(false);
                Classify(scope, result, classify);
                return result;
            }
            catch (Exception ex)
            {
                scope.SetOutcome(OperationOutcome.Failed(ex));
                Report(operation, ex);
                throw;
            }
        }

        /// <summary>The asynchronous void <see cref="Run(string, Action{OperationScope}, MetricBinding)"/>.</summary>
        public async Task RunAsync(string operation, Func<OperationScope, Task> body, MetricBinding? metrics = null)
        {
            using OperationScope scope = Start(operation, ActivityKind.Internal, metrics);
            try
            {
                await body(scope).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                scope.SetOutcome(OperationOutcome.Failed(ex));
                Report(operation, ex);
                throw;
            }
        }

        // Fail-open: a classifier is caller code, and a fault in it must not turn a successful operation
        // into a thrown one. The operation then reports as Ok, which is what it was before classification.
        private static void Classify<T>(OperationScope scope, T result, Func<T, OperationOutcome>? classify)
        {
            if (classify is null)
            {
                return;
            }
            try
            {
                scope.SetOutcome(classify(result));
            }
            catch (Exception)
            {
                // Deliberately swallowed; see above.
            }
        }
    }
}
