using Avalonia;
using System;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Diagnostics;
using Avalonia.Logging;
using IhcLab;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace IhcLab;

class Program
{
    public static Configuration? config = null;
    public static ILoggerFactory? loggerFactory = null;
     
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
            AppDomain.CurrentDomain.UnhandledException += CustomSetup.UnhandledExceptionHandler;
            config = new Configuration();
            loggerFactory = CustomSetup.SetupTelemetryAndLoggingFactory(config);

            // throw new Exception("bla during startup");

            // Default init by Avalonia template.
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        } catch (Exception ex)
        {
            Trace.WriteLine("Fatal error " + ex);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() {
        if (loggerFactory == null)
            throw new Exception("loggerFactory not set");
        if (config == null)
            throw new Exception("config not set");

        LogLevel logLevel = config.loggingConfig.GetValue<LogLevel>("LogLevel:Avalonia", LogLevel.Trace);

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(CustomSetup.MapFromIlogToAvaloniaLogLevel(logLevel)) // Important that this default logger (if present) is before our own LogToSink which will forward to it.
            .LogToSink(loggerFactory, CustomSetup.MapFromIlogToAvaloniaLogLevel(logLevel)); // Install log forwarder to ilogger which is setup to forward to opentel.
    }
}
