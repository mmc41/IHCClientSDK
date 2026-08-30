#nullable enable
using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace Ihc.Bootstrap
{
    /// <summary>
    /// The Avalonia-only half of the shared host bootstrap: bridging Avalonia's internal logs into the
    /// <see cref="ILogger"/> pipeline via <see cref="LogToSink"/>, the dispatcher exception layer, and the two
    /// level maps between Avalonia's <see cref="LogEventLevel"/> and <see cref="LogLevel"/>.
    /// </summary>
    /// <remarks>
    /// The providers, the OTLP wiring, the resource and the process-wide exception handlers are NOT here —
    /// they are toolkit-neutral and live in <see cref="TelemetryBootstrap"/>, which a console utility can
    /// reference without Avalonia (R7). An Avalonia app calls both: the neutral one to build its pipeline,
    /// this one to attach the toolkit to it.
    /// <para>Formed by merging the two apps' previously duplicated <c>AppSetup</c>. Where they diverged:
    /// <see cref="LogToSink"/> keeps ihc_lab's level-parameterized shape (its <c>minLevel</c> is a live
    /// forwarding floor, defaulting to <see cref="LogEventLevel.Verbose"/> so an app that omits it forwards
    /// everything its ILogger permits — matching OpenVisual's prior no-floor behavior).</para>
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

        /// <summary>Installs the Avalonia→ILogger forwarding sink (chained ahead of any existing sink). Avalonia
        /// internal logs at or above <paramref name="minLevel"/> are forwarded, subject to the ILogger's own level;
        /// the default <see cref="LogEventLevel.Verbose"/> imposes no floor of its own.</summary>
        public static AppBuilder LogToSink(this AppBuilder builder, ILoggerFactory logFactory,
            LogEventLevel minLevel = LogEventLevel.Verbose)
        {
            Logger.Sink = new ChainedILoggerSink(logFactory, Logger.Sink, minLevel);
            return builder;
        }

        /// <summary>Builds the <see cref="Dispatcher.UnhandledException"/> handler — the SECOND of the four documented
        /// exception layers (Avalonia logging review BP-09). It is the only route to a fault raised inside a dispatcher
        /// operation: the dispatcher decides what to do with such a fault before it could ever reach an
        /// <see cref="AppDomain.UnhandledException"/> handler. Register it once the logger factory exists:
        /// <c>Dispatcher.UIThread.UnhandledException += DispatcherExceptionHandler(logger);</c>
        /// <para>Deliberately does NOT set <c>Handled</c> (review WS-05/AP-07): resuming a UI thread whose operation
        /// faulted continues on possibly corrupt state, and the flag is set-once-true (BP-08), so marking it here could
        /// never be overruled later. This handler observes; the framework still escalates, and
        /// <see cref="TelemetryBootstrap.UnhandledExceptionHandler"/> owns the terminal path (which is also why this
        /// one does not tear down the Activity chain — doing it in both would dispose it twice).</para></summary>
        public static DispatcherUnhandledExceptionEventHandler DispatcherExceptionHandler(ILogger logger) =>
            (_, args) => LogDispatcherException(logger, args.Exception);

        // The handler body, callable directly so a test can assert against real logged output (ILogger is never mocked).
        public static void LogDispatcherException(ILogger logger, Exception ex)
        {
            logger.LogCritical(ex, "Unhandled dispatcher exception: {Message}", ex.Message);
            Activity.Current?.AddException(ex);
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
}
