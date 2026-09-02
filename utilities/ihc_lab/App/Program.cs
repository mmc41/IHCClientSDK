using Avalonia;
using System;
using System.Diagnostics;
using Avalonia.Logging;
using IhcLab;
using Ihc.Bootstrap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace IhcLab;

public class Program
{
    /// <summary>
    /// Application configuration loaded at startup.
    /// IMPORTANT: Initialized once in Main() and should be treated as immutable thereafter.
    /// Do not modify after initialization to avoid race conditions and unpredictable behavior.
    /// </summary>
    public static Configuration? config { get; set; }

    /// <summary>
    /// Logger factory for the application, configured with OpenTelemetry.
    /// IMPORTANT: Initialized once in Main() and should be treated as immutable thereafter.
    /// Do not modify after initialization to avoid race conditions and unpredictable behavior.
    /// </summary>
    public static ILoggerFactory? loggerFactory { get; set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // First setup logging and telemetry. Note that this goes against above advice but seems to work.
            // In case of trouble first move some of the telemtry setup to mainwindow or so.
            config = new Configuration();
            loggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
                Telemetry.AppServiceName, Telemetry.AppServiceNamespace, Telemetry.ActivitySourceName,
                config.telemetryConfig, config.loggingConfig);
            // Registered after the logger factory exists so the fatal exception is recorded through ILogger (hence
            // OTLP-exported), not a bare Trace write; an exception before this point is caught by Main's catch below.
            AppDomain.CurrentDomain.UnhandledException += TelemetryBootstrap.UnhandledExceptionHandler(
                loggerFactory.CreateLogger("IhcLab.UnhandledException"));

            // Probe the configured OTLP endpoint so a wrong endpoint/token fails loudly instead of
            // silently dropping all telemetry. Runs in the background; does not block startup.
            _ = Ihc.TelemetrySelfCheck.ProbeAndReportAsync(config.telemetryConfig);

            // throw new Exception("bla during startup");

            // Default init by Avalonia template.
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        } catch (Exception ex)
        {
            Trace.WriteLine("Fatal error " + ex);
        }
        finally
        {
            // Before the LoggerFactory below, for the reason Shutdown documents.
            TelemetryBootstrap.Shutdown();
            loggerFactory?.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() {
        if (loggerFactory == null)
            throw new InvalidOperationException("loggerFactory not set");
        if (config == null)
            throw new InvalidOperationException("config not set");

        LogLevel logLevel = config.loggingConfig.GetValue<LogLevel>("LogLevel:Avalonia", LogLevel.Trace);

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(AppTelemetryBootstrap.MapFromIlogToAvaloniaLogLevel(logLevel)) // Important that this default logger (if present) is before our own LogToSink which will forward to it.
            .LogToSink(loggerFactory, AppTelemetryBootstrap.MapFromIlogToAvaloniaLogLevel(logLevel)); // Install log forwarder to ilogger which is setup to forward to opentel.
    }
}
