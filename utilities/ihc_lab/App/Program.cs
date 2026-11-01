using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ihc.Stubbing;
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

    /// <summary>
    /// Whether the controller-stub switch made this a stub process. Set once in Main() and true for the rest of
    /// the run, because the SDK stays latched to stubs whatever endpoint is entered later, and the title must not
    /// claim otherwise.
    /// </summary>
    public static bool controllerStubProcess { get; set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    /// <summary>The exit code of a start-up that could not complete.</summary>
    public const int FatalStartupExitCode = 1;

    /// <summary>
    /// Applies the controller-stub switch to the configuration Lab's own loader produced, and rebuilds that
    /// configuration around the settings every SDK service is to be built from. <see cref="Main"/> only calls it.
    /// </summary>
    /// <param name="args">Lab's arguments.</param>
    /// <param name="loaded">The configuration <see cref="Configuration()"/> read.</param>
    /// <returns>
    /// The configuration to run with, the arguments without the switch, and whether the switch made this a stub
    /// process, which stays true for the rest of the run whatever endpoint is entered later.
    /// </returns>
    /// <exception cref="Ihc.ErrorWithCodeException">A refused switch, which must stop Lab from starting.</exception>
    public static (Configuration Config, IReadOnlyList<string> RemainingArgs, bool ControllerStubProcess) Compose(
        IReadOnlyList<string> args, Configuration loaded)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(loaded);
        (Ihc.IhcSettings effective, IReadOnlyList<string> remaining) = ControllerStubs.ApplySwitch(args, loaded.ihcSettings);
        // The switch, not the endpoint: only an applied switch holds the latch that refuses real endpoints, and a
        // stub endpoint read from the settings file holds nothing.
        return (new Configuration(loaded, effective), remaining, ControllerStubs.SwitchRequested(args));
    }

    /// <summary>
    /// Reports a start-up that could not complete and answers with the exit code. Critical through the logging
    /// pipeline when it exists, so a WinExe with no console still leaves the failure where its telemetry goes; a
    /// trace line either way, for a failure before the pipeline existed.
    /// </summary>
    public static int ReportFatalStartup(Exception error, ILoggerFactory? factory)
    {
        Trace.WriteLine("Fatal error " + error);
        factory?.CreateLogger("IhcLab.Startup").LogCritical(error, "Lab could not start");
        return FatalStartupExitCode;
    }

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // First setup logging and telemetry. Note that this goes against above advice but seems to work.
            // In case of trouble first move some of the telemtry setup to mainwindow or so.
            var loaded = new Configuration();
            loggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
                Telemetry.AppServiceName, Telemetry.AppServiceNamespace, Telemetry.ActivitySourceName,
                loaded.telemetryConfig, loaded.loggingConfig, ControllerStubs.SwitchRequested(args));
            // After the pipeline, so a refused switch is logged and the stub's identity check traced; before anything
            // reads the controller settings or the arguments.
            (config, IReadOnlyList<string> remaining, bool stubProcess) = Compose(args, loaded);
            controllerStubProcess = stubProcess;
            // Registered after the logger factory exists so the fatal exception is recorded through ILogger (hence
            // OTLP-exported), not a bare Trace write; an exception before this point is caught by Main's catch below.
            AppDomain.CurrentDomain.UnhandledException += TelemetryBootstrap.UnhandledExceptionHandler(
                loggerFactory.CreateLogger("IhcLab.UnhandledException"));

            // Probe the configured OTLP endpoint so a wrong endpoint/token fails loudly instead of
            // silently dropping all telemetry. Runs in the background; does not block startup.
            _ = Ihc.TelemetrySelfCheck.ProbeAndReportAsync(config.telemetryConfig);

            // throw new Exception("bla during startup");

            // Default init by Avalonia template.
            BuildAvaloniaApp().StartWithClassicDesktopLifetime([.. remaining]);
        } catch (Exception ex)
        {
            // Main is void, so the exit code is the only way Lab can tell its launcher that the run failed.
            Environment.ExitCode = ReportFatalStartup(ex, loggerFactory);
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
            .LogToTrace(AvaloniaTelemetryBootstrap.MapFromIlogToAvaloniaLogLevel(logLevel)) // Important that this default logger (if present) is before our own LogToSink which will forward to it.
            .LogToSink(loggerFactory, AvaloniaTelemetryBootstrap.MapFromIlogToAvaloniaLogLevel(logLevel)); // Install log forwarder to ilogger which is setup to forward to opentel.
    }
}
