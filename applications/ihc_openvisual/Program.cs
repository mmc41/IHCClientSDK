using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Logging;
using ihc_openvisual.Configuration;
using Ihc.Bootstrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual;

internal sealed class Program
{
    /// <summary>Configuration loaded once in <see cref="Main"/>; immutable thereafter.</summary>
    public static AppConfiguration? Config { get; private set; }

    /// <summary>The shared logger factory (OpenTelemetry-wired); set once in <see cref="Main"/>.</summary>
    public static ILoggerFactory? LoggerFactory { get; private set; }

    /// <summary>True when launched with <c>--skip-recovery</c> (alias <c>--no-recover</c>): the crash-recovery
    /// prompt is bypassed so an unattended UI-automation session opens a deterministic fresh project. Set once
    /// in <see cref="Main"/>.</summary>
    public static bool SkipRecovery { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant
    // code before AppMain is called: things aren't initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Order matters: config and the telemetry pipeline first, then hook the ILogger-backed unhandled-error
            // handler (A-25). Startup exceptions before this point are caught by Main's catch below.
            SkipRecovery = args.Any(a =>
                string.Equals(a, "--skip-recovery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--no-recover", StringComparison.OrdinalIgnoreCase));
            Config = new AppConfiguration();
            LoggerFactory = AppTelemetryBootstrap.SetupTelemetryAndLogging(
                Telemetry.AppServiceName, Telemetry.AppServiceNamespace, Telemetry.ActivitySourceName,
                Config.TelemetryConfig, Config.LoggingConfig);
            AppDomain.CurrentDomain.UnhandledException += AppTelemetryBootstrap.UnhandledExceptionHandler(
                LoggerFactory.CreateLogger("Ihc.OpenVisual.UnhandledException"));

            // Probe the configured OTLP endpoint so a wrong endpoint/token fails loudly instead of silently
            // dropping all telemetry. Runs in the background; never blocks the workspace from opening. The fault is
            // OBSERVED (a continuation logs it) so a probe exception is not swallowed as an UnobservedTaskException.
            Ihc.TelemetrySelfCheck.ProbeAndReportAsync(Config.TelemetryConfig).ContinueWith(
                t => LoggerFactory!.CreateLogger("Ihc.OpenVisual.TelemetrySelfCheck")
                    .LogWarning(t.Exception, "Telemetry self-check faulted"),
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Route the fatal startup error through the wired ILogger (OTLP-exported) when it is up; fall back to
            // Trace only for an exception thrown BEFORE the logger factory was created.
            if (LoggerFactory is { } loggerFactory)
                loggerFactory.CreateLogger("Ihc.OpenVisual.Startup").LogCritical(ex, "Fatal startup error");
            else
                Trace.WriteLine("Fatal error " + ex);
        }
        finally
        {
            // Flush and release telemetry on shutdown so the final batch of spans/logs is exported.
            AppTelemetryBootstrap.TracerProvider?.Dispose();
            LoggerFactory?.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont();

        if (LoggerFactory is { } loggerFactory && Config is { } config)
        {
            LogLevel avaloniaLevel = config.LoggingConfig.GetValue("LogLevel:Avalonia", LogLevel.Warning);
            LogEventLevel level = AppTelemetryBootstrap.MapFromIlogToAvaloniaLogLevel(avaloniaLevel);
            // The default trace logger must be installed before our sink so our sink can chain to it.
            builder = builder.LogToTrace(level).LogToSink(loggerFactory);
        }
        else
        {
            builder = builder.LogToTrace();
        }

        return builder;
    }
}
