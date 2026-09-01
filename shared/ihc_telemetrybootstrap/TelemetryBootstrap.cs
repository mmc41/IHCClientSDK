using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ihc.Bootstrap
{
    /// <summary>
    /// The start-up telemetry pipeline every IHC host shares, with nothing in it that knows about a UI toolkit.
    /// Establishes a single <see cref="ILoggerFactory"/> (local console/debug always, plus an OTLP log exporter
    /// when a logs endpoint is configured), a single <see cref="OpenTelemetry.Trace.TracerProvider"/> and a
    /// single <see cref="OpenTelemetry.Metrics.MeterProvider"/> when their endpoints are configured, registering
    /// both the app and SDK scopes so app and SDK signals share one identity; and attaches process-wide
    /// unhandled exceptions to the active trace. The startup connectivity self-check runs from each host's
    /// entrypoint (<see cref="Ihc.TelemetrySelfCheck.ProbeAndReportAsync"/>).
    /// </summary>
    /// <remarks>
    /// Split out of <c>AppTelemetryBootstrap</c> so a console utility gets the providers without Avalonia (R7).
    /// The Avalonia-only extras — the log sink, the <c>AppBuilder</c> hook, the dispatcher handler and the two
    /// level maps — stay in <c>ihc_appbootstrap</c>, which references this. The dividing line is what a
    /// headless host can use: everything here runs with no windowing system present.
    /// </remarks>
    public static class TelemetryBootstrap
    {
        /// <summary>The host's TracerProvider, kept alive for the process lifetime (not GC'd; disposed to flush on
        /// shutdown). A host runs a single bootstrap per process, so a static anchor is correct.</summary>
        public static TracerProvider? TracerProvider { get; private set; }

        /// <summary>The host's MeterProvider, anchored for the same reason as <see cref="TracerProvider"/>: a
        /// collected-but-unrooted provider stops exporting when it is GC'd, and never flushes what it holds.
        /// Null when no metrics endpoint is configured.</summary>
        public static MeterProvider? MeterProvider { get; private set; }

        /// <summary>
        /// Flushes and releases both providers. Every host must do this before disposing its
        /// <see cref="ILoggerFactory"/>: disposing a provider is what flushes it, a session shorter than one
        /// metric export interval would otherwise end having sent nothing at all, and any problem the flush
        /// reports needs the logging pipeline still standing to be heard.
        /// <para>Here rather than in each host, because the ORDER is the rule and a rule restated at five call
        /// sites is a rule that will eventually be restated wrongly at one of them.</para>
        /// </summary>
        public static void Shutdown()
        {
            TracerProvider?.Dispose();
            TracerProvider = null;
            MeterProvider?.Dispose();
            MeterProvider = null;
        }

        /// <summary>Builds the shared logger factory and, for each signal whose endpoint is configured, its
        /// provider. An unconfigured signal leaves its provider null - and a previously built one is disposed
        /// first, so calling this twice in one process cannot leak a live exporter or leave a stale anchor.
        /// The service version is read from the running entry assembly via <see cref="GetAppVersionStr"/>.</summary>
        /// <param name="appScopeName">The host's own instrumentation-scope name, used for BOTH its
        /// ActivitySource and its Meter - one scope, both signals, as for the SDK.</param>
        public static ILoggerFactory SetupTelemetryAndLogging(
            string serviceName, string serviceNamespace, string appScopeName,
            TelemetryConfiguration telemetry, IConfiguration loggingConfig)
        {
            ApplyDefaultAttributeLimits();
            // ONE resource for all three signals: CreateDefault re-runs the default detectors on every
            // call, and three resources that merely happen to agree are three things to keep in step.
            ResourceBuilder resource = BuildResource(serviceName, serviceNamespace, telemetry.Environment);

            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                // Local logging is always available so the app runs normally with no telemetry configured.
                builder.AddConfiguration(loggingConfig);
                // Carries the ambient trace into the LOCAL providers' scopes. The OTLP log exporter reads
                // TraceId/SpanId straight off Activity.Current and never needed this; Console and Debug did,
                // so without it a console line could not be tied to the span it came from.
                builder.Configure(options =>
                    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId);
                builder.AddDebug();
                builder.AddConsole();

                if (!string.IsNullOrEmpty(telemetry.Logs))
                {
                    builder.AddOpenTelemetry(loggingOpts =>
                    {
                        loggingOpts.IncludeFormattedMessage = true;
                        loggingOpts.IncludeScopes = true;
                        loggingOpts.SetResourceBuilder(resource);
                        loggingOpts.AddOtlpExporter(opts => ConfigureOtlp(opts, telemetry.Logs, telemetry.Headers));
                    });
                }
            });

            // Each provider must be kept alive for the app lifetime and disposed on shutdown; otherwise it can be
            // GC'd (silently stopping export) and never flushes what it holds. Anchored in the statics above.
            // Assigned unconditionally so an unconfigured signal REPLACES any previous provider rather than
            // leaving a stale one live.
            TracerProvider?.Dispose();
            TracerProvider = string.IsNullOrEmpty(telemetry.Traces) ? null
                : Sdk.CreateTracerProviderBuilder()
                    .SetErrorStatusOnException(true)
                    .SetResourceBuilder(resource)
                    .AddSource(Ihc.Telemetry.ActivitySourceName, appScopeName)
                    .AddOtlpExporter(opts => ConfigureOtlp(opts, telemetry.Traces, telemetry.Headers))
                    .Build();

            MeterProvider?.Dispose();
            MeterProvider = string.IsNullOrEmpty(telemetry.Metrics) ? null
                : Sdk.CreateMeterProviderBuilder()
                    .SetResourceBuilder(resource)
                    .AddMeter(Ihc.Telemetry.MeterName, appScopeName)
                    // The metric-to-trace join, and it must be asked for: the SPECIFICATION's default is
                    // trace_based but the .NET SDK's is not - it leaves the filter unset, which attaches no
                    // exemplars at all. The instrumentation core already pays for this at every measurement by
                    // recording instruments BEFORE the activity is disposed, so each point is exemplar-eligible;
                    // without this line that cost buys nothing and a latency spike on a histogram cannot be
                    // followed to the trace that produced it.
                    .SetExemplarFilter(ExemplarFilterType.TraceBased)
                    .ConfigureDurationHistogramViews()
                    .AddOtlpExporter((exporterOpts, readerOpts) =>
                    {
                        ConfigureOtlp(exporterOpts, telemetry.Metrics, telemetry.Headers);
                        // Delta, not the exporter's Cumulative default: OpenObserve stores each export as a
                        // row, so a cumulative series re-states its running total every interval and any
                        // sum over a window double-counts. Measured against the collector before choosing.
                        readerOpts.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                        readerOpts.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                            MetricExportIntervalMilliseconds;
                    })
                    .Build();

            return loggerFactory;
        }

        /// <summary>
        /// Bucket boundaries for every duration histogram, in SECONDS.
        ///
        /// <para>A histogram registered without a view inherits OpenTelemetry's default boundaries, which run
        /// 0 to 10000 and are unitless - intended for counts and request sizes. Every duration here is
        /// second-scale, so under the default set essentially every measurement lands in the first bucket and
        /// every percentile reads the same: a latency graph that cannot get worse, which is the most
        /// dangerous shape a graph can have.</para>
        ///
        /// <para>Explicit rather than Base2 exponential, which the acceptance spike measured and rejected:
        /// the collector ingests exponential histograms but stores a bucket INDEX in its boundary column with
        /// no scale exported anywhere, so the distribution cannot be reconstructed by any query - and it
        /// produced roughly ten times the rows for the same measurements.</para>
        /// </summary>
        private static readonly double[] DurationBucketBoundariesSeconds =
            { 0.01, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 30 };

        /// <summary>
        /// Applies <see cref="DurationBucketBoundariesSeconds"/> to every duration histogram of every meter.
        ///
        /// <para>Matched by NAME rather than instrument by instrument, because the bootstrap cannot see
        /// either layer's registry - both are internal to their own assembly, and one of them is in an
        /// assembly that references this one. The convention that a duration histogram's name ends in
        /// <c>.duration</c> is what makes the wildcard exact, and it means a histogram added later inherits
        /// the boundaries instead of silently inheriting the wrong default.</para>
        /// </summary>
        public static MeterProviderBuilder ConfigureDurationHistogramViews(this MeterProviderBuilder builder) =>
            builder.AddView("*.duration", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = DurationBucketBoundariesSeconds,
            });

        /// <summary>
        /// Ceiling on a single exported span-attribute value, in characters.
        ///
        /// OpenTelemetry .NET applies NO limit unless one is configured, so today a tag holding a serialized
        /// project or a SOAP envelope would leave the process whole. The value is chosen with headroom over
        /// what the app actually emits - a measured run's widest attribute was under a hundred characters -
        /// so this truncates nothing that exists today and exists as a backstop against the pathological case.
        /// </summary>
        private const string SpanAttributeValueLengthLimit = "4096";

        /// <summary>The specification's default attribute count, stated explicitly rather than assumed.</summary>
        private const string SpanAttributeCountLimit = "128";

        /// <summary>
        /// How often metrics are exported. The SDK default is 60 s, which is most of a short desktop session:
        /// a user who opens a project, makes an edit and closes would have every metric arrive in one lump at
        /// shutdown, if at all. Frequent enough that a running session is observable, rare enough to be cheap.
        /// </summary>
        private const int MetricExportIntervalMilliseconds = 15_000;

        /// <summary>
        /// Publishes the attribute limits the OTLP exporter reads when a provider is built. They are exporter
        /// configuration rather than provider API in this SDK, and an operator's own setting outranks ours -
        /// hence set-if-absent rather than assignment.
        /// </summary>
        private static void ApplyDefaultAttributeLimits()
        {
            SetIfUnset("OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", SpanAttributeValueLengthLimit);
            SetIfUnset("OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", SpanAttributeCountLimit);

            static void SetIfUnset(string variable, string value)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
                {
                    Environment.SetEnvironmentVariable(variable, value);
                }
            }
        }

        /// <summary>
        /// The value <see cref="TelemetryConfiguration.Environment"/> falls back to. An unlabelled record is
        /// worse than a conservatively labelled one: an unconfigured machine is by definition not a declared
        /// deployment, so it reports as development rather than as nothing.
        /// </summary>
        private const string DefaultEnvironment = "development";

        /// <summary>
        /// The per-PROCESS instance id, minted once. OpenTelemetry would otherwise auto-generate one per
        /// resource, giving the tracer and meter providers different ids for the same run - and every
        /// "did MY run do this?" query scopes on exactly this field.
        /// </summary>
        private static readonly string ServiceInstanceId = Guid.NewGuid().ToString();

        private static ResourceBuilder BuildResource(string serviceName, string serviceNamespace, string environment) =>
            ResourceBuilder.CreateDefault()
                .AddService(
                    serviceName: serviceName,
                    serviceNamespace: serviceNamespace,
                    serviceVersion: GetAppVersionStr(),
                    serviceInstanceId: ServiceInstanceId)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment.name",
                        string.IsNullOrWhiteSpace(environment) ? DefaultEnvironment : environment),
                });

        private static void ConfigureOtlp(OtlpExporterOptions opts, string endpoint, string headers)
        {
            // A malformed/blank configured endpoint must not crash startup (new Uri throws): keep the exporter's default
            // endpoint in that case. The telemetry self-check surfaces a wrong-but-well-formed endpoint at runtime.
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
                opts.Endpoint = uri;
            if (!string.IsNullOrEmpty(headers))
                opts.Headers = headers;
            opts.Protocol = OtlpExportProtocol.HttpProtobuf;
        }

        /// <summary>Builds the <see cref="AppDomain.UnhandledException"/> handler (US-063/A-25): the least-recoverable
        /// failure is recorded through <paramref name="logger"/> — the same <see cref="ILogger"/> pipeline as
        /// command-scoped errors, not a bare <c>Trace</c> — and attached to the whole active <see cref="Activity"/>
        /// chain so it is captured in diagnostics rather than vanishing silently. Register it after the logger factory
        /// exists: <c>AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler(logger);</c></summary>
        public static UnhandledExceptionEventHandler UnhandledExceptionHandler(ILogger logger) =>
            (_, args) => LogUnhandledException(logger, (Exception)args.ExceptionObject);

        // The handler body, callable directly so a test can assert against real logged output (ILogger is never mocked).
        public static void LogUnhandledException(ILogger logger, Exception ex)
        {
            logger.LogCritical(ex, "Unhandled exception: {Message}", ex.Message);

            Activity? activity = Activity.Current;
            while (activity != null)
            {
                activity.AddException(ex);
                Activity? parent = activity.Parent;
                activity.Dispose();
                activity = parent;
            }
        }

        /// <summary>Builds the <see cref="TaskScheduler.UnobservedTaskException"/> handler — the THIRD documented
        /// exception layer (BP-09). Register it once the logger factory exists:
        /// <c>TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler(logger);</c></summary>
        public static EventHandler<UnobservedTaskExceptionEventArgs> UnobservedTaskExceptionHandler(ILogger logger) =>
            (_, args) => LogUnobservedTaskException(logger, args);

        /// <summary>Records a dropped task's fault and marks it observed. Warning, not Critical: this event fires on the
        /// finalizer thread at an arbitrary later time, so it is a LEAK DETECTOR rather than a primary error path
        /// (review WS-06) — anything that needs timely reporting must be awaited. Observing it stops the fault
        /// re-surfacing as a process-killing finalizer throw.</summary>
        public static void LogUnobservedTaskException(ILogger logger, UnobservedTaskExceptionEventArgs args)
        {
            logger.LogWarning(args.Exception, "Unobserved task exception: {Message}", args.Exception?.Message);
            args.SetObserved();
        }

        /// <summary>The running application's version, read from the entry-assembly metadata set in the csproj
        /// (<c>&lt;Version&gt;</c>/<c>&lt;FileVersion&gt;</c>). The SDK version is a separate concept
        /// (<see cref="Ihc.VersionInfo"/>); an About dialog can show both.</summary>
        public static string GetAppVersionStr() => AppVersion;

        // Resolved once. The entry assembly cannot change during a process, and this is read from the resource
        // builder, both registries' static initializers and the two About dialogs - each an attribute lookup.
        private static readonly string AppVersion = ReadAppVersion();

        private static string ReadAppVersion()
        {
            Assembly? assembly = Assembly.GetEntryAssembly();
            string? fileVersion = assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            return fileVersion ?? assembly?.GetName().Version?.ToString() ?? "Unknown";
        }
    }
}
