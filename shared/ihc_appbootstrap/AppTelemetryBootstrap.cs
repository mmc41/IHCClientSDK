#nullable enable
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ihc.Bootstrap
{
    /// <summary>
    /// The one start-up logging/telemetry pipeline shared by the IHC desktop apps (review S2 / T035). Establishes a
    /// single <see cref="ILoggerFactory"/> (local console/debug always, plus an OTLP log exporter when a logs endpoint
    /// is configured) and a single <see cref="OpenTelemetry.Trace.TracerProvider"/> (when a traces endpoint is
    /// configured), registering both the app and SDK activity sources so app and SDK spans share one trace; bridges
    /// Avalonia's internal logs into the same pipeline via <see cref="LogToSink"/>; and attaches unhandled exceptions
    /// to the active trace via <see cref="UnhandledExceptionHandler"/>. The startup connectivity self-check itself runs
    /// from each app's entrypoint (<see cref="Ihc.TelemetrySelfCheck.ProbeAndReportAsync"/>).
    /// </summary>
    /// <remarks>
    /// Formed by merging the two apps' previously duplicated <c>AppSetup</c>. Where they diverged: <see cref="LogToSink"/>
    /// keeps ihc_lab's level-parameterized shape (its <c>minLevel</c> is a live forwarding floor, defaulting to
    /// <see cref="LogEventLevel.Verbose"/> so an app that omits it forwards everything its ILogger permits — matching
    /// OpenVisual's prior no-floor behavior). The unhandled-exception handler adopts OpenVisual's ILogger-based form
    /// (US-063/A-25) rather than ihc_lab's bare <see cref="Trace"/> write, because a fatal exception that never reaches
    /// the ILogger pipeline is never OTLP-exported — the whole point of the instrumentation.
    /// </remarks>
    public static class AppTelemetryBootstrap
    {
        /// <summary>An <see cref="ILogSink"/> that forwards Avalonia's internal logs into <see cref="ILogger"/>
        /// (hence OpenTelemetry) while chaining to any previously installed sink. Forwarding is floored at
        /// <paramref name="minLevel"/> (and further gated by the ILogger's own configured level).</summary>
        public sealed class ChainedILoggerSink : ILogSink
        {
            private readonly ILogger<ChainedILoggerSink> iLogger;
            private readonly ILogSink? forwardSink;
            private readonly LogEventLevel minLevel;

            public ChainedILoggerSink(ILoggerFactory logFactory, ILogSink? forwardSink, LogEventLevel minLevel)
            {
                iLogger = logFactory.CreateLogger<ChainedILoggerSink>();
                this.forwardSink = forwardSink;
                this.minLevel = minLevel;
            }

            public bool IsEnabled(LogEventLevel level, string area) =>
                level >= minLevel && iLogger.IsEnabled(MapFromAvaloniaLogToILogLevel(level));

            public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
            {
                iLogger.Log(MapFromAvaloniaLogToILogLevel(level), "[{Area}] {Source}: {Message}",
                    area, source?.GetType().Name ?? "Unknown", messageTemplate);
                forwardSink?.Log(level, area, source, messageTemplate);
            }

            public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
            {
                string combinedTemplate = "[{Area}] {Source}: " + messageTemplate;
                var combinedValues = new object?[propertyValues.Length + 2];
                combinedValues[0] = area;
                combinedValues[1] = source?.GetType().Name ?? "Unknown";
                Array.Copy(propertyValues, 0, combinedValues, 2, propertyValues.Length);
                // CA2254: this is the Avalonia-to-ILogger bridge. The template IS the payload - it arrives from
                // the framework's own log call - so it varies by construction and cannot be a constant here.
#pragma warning disable CA2254
                iLogger.Log(MapFromAvaloniaLogToILogLevel(level), combinedTemplate, combinedValues);
#pragma warning restore CA2254
                forwardSink?.Log(level, area, source, messageTemplate, propertyValues);
            }
        }

        /// <summary>The app's TracerProvider, kept alive for the process lifetime (not GC'd; disposed to flush on
        /// shutdown). A desktop app runs a single bootstrap per process, so a static anchor is correct.</summary>
        public static TracerProvider? TracerProvider { get; private set; }

        /// <summary>Builds the shared logger factory (and, when a traces endpoint is configured, the process
        /// TracerProvider) from an app's service identity and its telemetry/logging configuration. The service
        /// version is read from the running entry assembly via <see cref="GetAppVersionStr"/>.</summary>
        public static ILoggerFactory SetupTelemetryAndLogging(
            string serviceName, string serviceNamespace, string appActivitySourceName,
            TelemetryConfiguration telemetry, IConfiguration loggingConfig)
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                // Local logging is always available so the app runs normally with no telemetry configured.
                builder.AddConfiguration(loggingConfig);
                builder.AddDebug();
                builder.AddConsole();

                if (!string.IsNullOrEmpty(telemetry.Logs))
                {
                    builder.AddOpenTelemetry(loggingOpts =>
                    {
                        loggingOpts.IncludeFormattedMessage = true;
                        loggingOpts.IncludeScopes = true;
                        loggingOpts.SetResourceBuilder(BuildResource(serviceName, serviceNamespace));
                        loggingOpts.AddOtlpExporter(opts => ConfigureOtlp(opts, telemetry.Logs, telemetry.Headers));
                    });
                }
            });

            // The TracerProvider must be kept alive for the app lifetime and disposed on shutdown; otherwise it can be
            // GC'd (silently stopping export) and never flushes pending spans. Anchored in the static above.
            if (!string.IsNullOrEmpty(telemetry.Traces))
            {
                TracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetErrorStatusOnException(true)
                    .SetResourceBuilder(BuildResource(serviceName, serviceNamespace))
                    .AddSource(Ihc.Telemetry.ActivitySourceName, appActivitySourceName)
                    .AddOtlpExporter(opts => ConfigureOtlp(opts, telemetry.Traces, telemetry.Headers))
                    .Build();
            }

            return loggerFactory;
        }

        private static ResourceBuilder BuildResource(string serviceName, string serviceNamespace) =>
            ResourceBuilder.CreateDefault().AddService(
                serviceName: serviceName,
                serviceNamespace: serviceNamespace,
                serviceVersion: GetAppVersionStr());

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

        /// <summary>Installs the Avalonia→ILogger forwarding sink (chained ahead of any existing sink). Avalonia
        /// internal logs at or above <paramref name="minLevel"/> are forwarded, subject to the ILogger's own level;
        /// the default <see cref="LogEventLevel.Verbose"/> imposes no floor of its own.</summary>
        public static AppBuilder LogToSink(this AppBuilder builder, ILoggerFactory logFactory,
            LogEventLevel minLevel = LogEventLevel.Verbose)
        {
            Logger.Sink = new ChainedILoggerSink(logFactory, Logger.Sink, minLevel);
            return builder;
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

        /// <summary>Builds the <see cref="Dispatcher.UnhandledException"/> handler — the SECOND of the four documented
        /// exception layers (Avalonia logging review BP-09). It is the only route to a fault raised inside a dispatcher
        /// operation: the dispatcher decides what to do with such a fault before it could ever reach an
        /// <see cref="AppDomain.UnhandledException"/> handler. Register it once the logger factory exists:
        /// <c>Dispatcher.UIThread.UnhandledException += DispatcherExceptionHandler(logger);</c>
        /// <para>Deliberately does NOT set <c>Handled</c> (review WS-05/AP-07): resuming a UI thread whose operation
        /// faulted continues on possibly corrupt state, and the flag is set-once-true (BP-08), so marking it here could
        /// never be overruled later. This handler observes; the framework still escalates, and the AppDomain handler
        /// below owns the terminal path (which is also why this one does not tear down the Activity chain — doing it
        /// in both would dispose it twice).</para></summary>
        public static DispatcherUnhandledExceptionEventHandler DispatcherExceptionHandler(ILogger logger) =>
            (_, args) => LogDispatcherException(logger, args.Exception);

        // The handler body, callable directly so a test can assert against real logged output (ILogger is never mocked).
        public static void LogDispatcherException(ILogger logger, Exception ex)
        {
            logger.LogCritical(ex, "Unhandled dispatcher exception: {Message}", ex.Message);
            Activity.Current?.AddException(ex);
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

        public static LogEventLevel MapFromIlogToAvaloniaLogLevel(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            LogLevel.None => LogEventLevel.Fatal,
            _ => LogEventLevel.Warning
        };

        public static LogLevel MapFromAvaloniaLogToILogLevel(LogEventLevel level) => level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.None
        };

        /// <summary>The running application's version, read from the entry-assembly metadata set in the csproj
        /// (<c>&lt;Version&gt;</c>/<c>&lt;FileVersion&gt;</c>). The SDK version is a separate concept
        /// (<see cref="Ihc.VersionInfo"/>); an About dialog can show both.</summary>
        public static string GetAppVersionStr()
        {
            Assembly? assembly = Assembly.GetEntryAssembly();
            string? fileVersion = assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            return fileVersion ?? assembly?.GetName().Version?.ToString() ?? "Unknown";
        }
    }
}
