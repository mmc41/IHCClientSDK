using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Logging;
using Ihc;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ihc_openvisual.Configuration;

/// <summary>
/// Start-up composition of the shared logging/telemetry pipeline (US-063), ported from the proven
/// <c>ihc_lab</c> bootstrap. Establishes one <see cref="ILoggerFactory"/> (local console/debug always, plus
/// an OTLP log exporter when a logs endpoint is configured) and one <see cref="OpenTelemetry.Trace.TracerProvider"/>
/// (when a traces endpoint is configured), registering both the app and SDK activity sources; bridges
/// Avalonia's internal logs into the same pipeline; and attaches unhandled exceptions to the active trace.
/// The startup connectivity self-check itself runs from the entrypoint via
/// <see cref="Ihc.TelemetrySelfCheck.ProbeAndReportAsync"/>.
/// </summary>
public static class AppSetup
{
    /// <summary>An <see cref="ILogSink"/> that forwards Avalonia's internal logs into <see cref="ILogger"/>
    /// (hence OpenTelemetry) while chaining to any previously installed sink.</summary>
    public sealed class ChainedILoggerSink : ILogSink
    {
        private readonly ILogger<ChainedILoggerSink> iLogger;
        private readonly ILogSink? forwardSink;

        public ChainedILoggerSink(ILoggerFactory logFactory, ILogSink? forwardSink)
        {
            iLogger = logFactory.CreateLogger<ChainedILoggerSink>();
            this.forwardSink = forwardSink;
        }

        public bool IsEnabled(LogEventLevel level, string area) =>
            iLogger.IsEnabled(MapFromAvaloniaLogToILogLevel(level));

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
            iLogger.Log(MapFromAvaloniaLogToILogLevel(level), combinedTemplate, combinedValues);
            forwardSink?.Log(level, area, source, messageTemplate, propertyValues);
        }
    }

    /// <summary>The app's TracerProvider, kept alive for the process lifetime (not GC'd; disposed to flush on shutdown).</summary>
    public static TracerProvider? TracerProvider { get; private set; }

    public static ILoggerFactory SetupTelemetryAndLoggingFactory(AppConfiguration configuration)
    {
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            // Local logging is always available so the app runs normally with no telemetry configured.
            builder.AddConfiguration(configuration.LoggingConfig);
            builder.AddDebug();
            builder.AddConsole();

            if (!string.IsNullOrEmpty(configuration.TelemetryConfig.Logs))
            {
                builder.AddOpenTelemetry(loggingOpts =>
                {
                    loggingOpts.IncludeFormattedMessage = true;
                    loggingOpts.IncludeScopes = true;
                    loggingOpts.SetResourceBuilder(BuildResource());
                    loggingOpts.AddOtlpExporter(opts => ConfigureOtlp(opts, configuration.TelemetryConfig.Logs, configuration.TelemetryConfig.Headers));
                });
            }
        });

        // The TracerProvider must be kept alive for the app lifetime and disposed on shutdown; otherwise it
        // can be GC'd (silently stopping export) and never flushes pending spans. Anchored in a static.
        if (!string.IsNullOrEmpty(configuration.TelemetryConfig.Traces))
        {
            TracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetErrorStatusOnException(true)
                .SetResourceBuilder(BuildResource())
                .AddSource(Ihc.Telemetry.ActivitySourceName, Telemetry.ActivitySourceName)
                .AddOtlpExporter(opts => ConfigureOtlp(opts, configuration.TelemetryConfig.Traces, configuration.TelemetryConfig.Headers))
                .Build();
        }

        return loggerFactory;
    }

    private static ResourceBuilder BuildResource() =>
        ResourceBuilder.CreateDefault().AddService(
            serviceName: Telemetry.AppServiceName,
            serviceNamespace: Telemetry.AppServiceNamespace,
            serviceVersion: VersionInfo.GetAppVersionStr());

    private static void ConfigureOtlp(OpenTelemetry.Exporter.OtlpExporterOptions opts, string endpoint, string headers)
    {
        opts.Endpoint = new Uri(endpoint);
        if (!string.IsNullOrEmpty(headers))
            opts.Headers = headers;
        opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
    }

    public static AppBuilder LogToSink(this AppBuilder builder, ILoggerFactory logFactory)
    {
        Logger.Sink = new ChainedILoggerSink(logFactory, Logger.Sink);
        return builder;
    }

    /// <summary>Attaches an unhandled exception to the whole active <see cref="Activity"/> chain so it is
    /// captured in diagnostics rather than vanishing silently (US-063).</summary>
    public static void UnhandledExceptionHandler(object source, UnhandledExceptionEventArgs args)
    {
        var ex = (Exception)args.ExceptionObject;
        Trace.WriteLine(ex.Message);

        Activity? activity = Activity.Current;
        while (activity != null)
        {
            activity.AddException(ex);
            Activity? parent = activity.Parent;
            activity.Dispose();
            activity = parent;
        }
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
}
