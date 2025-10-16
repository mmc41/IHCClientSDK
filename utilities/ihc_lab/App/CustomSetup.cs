using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Logging;
using IhcLab;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using static IhcLab.Program;

namespace IhcLab;

public static class CustomSetup
{
    /// <summary>
    /// ILogSink which logs to both ILogger and another default sink.
    /// </summary>
    public class ChainedILoggerSink : ILogSink
    {
        private readonly LogEventLevel level = LogEventLevel.Warning;

        private readonly ILogger<ChainedILoggerSink> iLogger;

        private readonly ILogSink? forwardSink;

        public ChainedILoggerSink(ILoggerFactory logFactory, ILogSink? forwardSink, LogEventLevel level)
        {
            iLogger = logFactory.CreateLogger<ChainedILoggerSink>();
            this.level = level;
            this.forwardSink = forwardSink;
        }

        public bool IsEnabled(LogEventLevel level, string area)
        {
            // All areas enabled - no filtering here.
            var logLevel = MapFromAvaloniaLogToILogLevel(level);
            return iLogger.IsEnabled(logLevel);
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            var logLevel = MapFromAvaloniaLogToILogLevel(level);
            iLogger.Log(logLevel, "[{Area}] {Source}: {Message}", area, source?.GetType().Name ?? "Unknown", messageTemplate);
            forwardSink?.Log(level, area, source, messageTemplate);
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            var logLevel = MapFromAvaloniaLogToILogLevel(level);
            
            // Combine metadata with the original template and property values
            var combinedTemplate = "[{Area}] {Source}: " + messageTemplate;
            var combinedValues = new object?[propertyValues.Length + 2];
            combinedValues[0] = area;
            combinedValues[1] = source?.GetType().Name ?? "Unknown";
            Array.Copy(propertyValues, 0, combinedValues, 2, propertyValues.Length);

            iLogger.Log(logLevel, combinedTemplate, combinedValues);
            forwardSink?.Log(level, area, source, messageTemplate, propertyValues);
        }
    }
    
    public static ILoggerFactory SetupTelemetryAndLoggingFactory(Configuration configuration)
    {
        if (string.IsNullOrEmpty(configuration.telemetryConfig.Host) || string.IsNullOrEmpty(configuration.telemetryConfig.Authentication)) {
            return Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        }

        string logsEndpoint = $"{configuration.telemetryConfig.Host}/api/{configuration.telemetryConfig.Organization}/v1/logs";
        string tracingEndpoint = $"{configuration.telemetryConfig.Host}/api/{configuration.telemetryConfig.Organization}/v1/traces";
        string metricsEndpoint = $"{configuration.telemetryConfig.Host}/api/{configuration.telemetryConfig.Organization}/v1/metrics";
        string headers = $"Authorization={configuration.telemetryConfig.Authentication}," +
                         $"stream-name={configuration.telemetryConfig.Stream}," +
                         $"organization={configuration.telemetryConfig.Organization}";


        // Create a logger for our application which delegates to Telemetry:
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(loggingOpts =>
            {
                loggingOpts.IncludeFormattedMessage = true;
                loggingOpts.IncludeScopes = true;
                loggingOpts.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: IhcLab.Telemetry.AppServiceName));

                loggingOpts.AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(logsEndpoint);
                    opts.Headers = headers;
                    opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                });
            });
    
            builder.AddConfiguration(configuration.loggingConfig);
        });

        // Setup tracing for our application 
        var telmetryTracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetErrorStatusOnException(true)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: IhcLab.Telemetry.AppServiceName))
            .AddSource(Ihc.Telemetry.ActivitySourceName, IhcLab.Telemetry.ActivitySourceName)
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(tracingEndpoint);
                opts.Headers = headers;
                opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            }).Build();

        return loggerFactory;
    }
    

    public static AppBuilder LogToSink(this AppBuilder builder, ILoggerFactory logFactory, LogEventLevel level = LogEventLevel.Warning)
    {       
        Logger.Sink = new ChainedILoggerSink(logFactory, Logger.Sink, level);
        return builder;
    }

    public static void UnhandledExceptionHandler(object source, UnhandledExceptionEventArgs args)
    {
        var ex = (Exception)args.ExceptionObject;
        Trace.WriteLine(ex.Message);

        var activity = Activity.Current;

        while (activity != null)
        {
            activity.AddException(ex);
            activity.Dispose();
            activity = activity.Parent;
        }
    }

    public static LogEventLevel MapFromIlogToAvaloniaLogLevel(LogLevel logLevel)
    {
        return logLevel switch
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
    }

     public static LogLevel MapFromAvaloniaLogToILogLevel(LogEventLevel level)
        {
            return level switch
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
}
