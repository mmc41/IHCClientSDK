#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
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
        /// <c>Dispatcher.UIThread.UnhandledException += DispatcherExceptionHandler(logger, report);</c></summary>
        /// <remarks>
        /// <para><b>This handler sets <c>Handled</c>, and doing so is a TRADE, not a safety judgement</b> — a
        /// process whose state is guaranteed clean, for a fault the user can actually report. ADR-001 records
        /// the trade, what bounds the damage after resuming, and the position it reverses; it is the one place
        /// that argument lives.</para>
        /// <para><b>Cancellation is answered FIRST, and it is the one thing this handler marks handled
        /// unconditionally.</b> An application that cancels routinely — a debounced background pass swapping its
        /// token source on every generation, say — can let an <see cref="OperationCanceledException"/> escape on
        /// the UI thread through an <c>async void</c> handler or a dispatcher continuation, where there is no
        /// boundary to tell it apart from a fault. Escalating that, or recording it as a fault, would turn the
        /// cancellation machinery working exactly as designed into a stream of reported errors.
        /// <see cref="System.Threading.Tasks.TaskCanceledException"/> derives from
        /// <see cref="OperationCanceledException"/>, so the wider type covers the framework's own cancellation as
        /// well as any the application raises itself.</para>
        /// <para><b>The breaker decides BEFORE the flag is set, because <c>Handled</c> is set-once-true
        /// (BP-08).</b> A sink that de-duplicates is too late: by the time a repeat is recognised the fault has
        /// already been swallowed and nothing downstream can escalate it. So the count is taken here, inline,
        /// and the <see cref="BreakerLimit"/>-th and later occurrences of one fault identity are left unhandled
        /// for the framework to escalate as it always did. Without that, a fault raised on every repaint yields
        /// an application that neither works nor dies.</para>
        /// <para><see cref="TelemetryBootstrap.UnhandledExceptionHandler"/> still owns the terminal path, which
        /// is why this one does not tear down the Activity chain — doing it in both would dispose it twice.</para>
        /// </remarks>
        /// <param name="logger">Where the English diagnostic goes.</param>
        /// <param name="report">
        /// Where a durable, user-visible record goes, or null for a host that has none. Optional so a host with
        /// no such surface still gets the survivability and the log line.
        /// </param>
        public static DispatcherUnhandledExceptionEventHandler DispatcherExceptionHandler(
            ILogger logger, Action<Exception>? report = null)
        {
            // Per-handler, not static: one registration is one application run, and a test that registers its own
            // must not inherit another test's counts. Ordinal, because a fault identity is a type name and a
            // frame, never anything a locale can re-spell.
            System.Collections.Generic.Dictionary<string, int> seen = new(StringComparer.Ordinal);

            return (_, args) =>
            {
                if (IsCancellation(args.Exception))
                {
                    // Handled, and nothing recorded: the operation was cancelled, which is not a failure of
                    // anything. Recording it would report the design working.
                    args.Handled = true;
                    return;
                }

                LogDispatcherException(logger, args.Exception);

                string identity = FaultIdentity(args.Exception);
                int occurrences = seen.TryGetValue(identity, out int before) ? before + 1 : 1;
                seen[identity] = occurrences;
                if (occurrences >= BreakerLimit)
                {
                    // OPEN. Handled stays false, so the framework escalates exactly as it did before this
                    // handler set the flag at all — the app dies, which is the right answer once it has proven
                    // it cannot make progress.
                    return;
                }

                report?.Invoke(args.Exception);
                args.Handled = true;
            };
        }

        /// <summary>
        /// How many occurrences of ONE fault identity are survived before the breaker opens.
        /// </summary>
        /// <remarks>
        /// Three, chosen against a repeating-fault test rather than picked as a round number. One would make the
        /// feature almost pointless — the commonest real case is a fault a user can retry past, and a breaker
        /// that opens on the second occurrence turns a recoverable session into a crash. A large N is worse in
        /// the other direction: the failure this bounds is a fault raised on every repaint, where the difference
        /// between ten survivals and three is ten rows nobody wants and a longer wait for the crash that ends
        /// it. Three survives the retry and still dies quickly under a repaint storm.
        /// </remarks>
        public const int BreakerLimit = 3;

        /// <summary>
        /// What counts as the SAME fault for the breaker: the exception's type and the top frame of its stack.
        /// </summary>
        /// <remarks>
        /// <para>The type alone is too coarse — an <c>InvalidOperationException</c> from two unrelated places
        /// would share a budget, so the second site could be denied its first survival by the first site's
        /// history. The whole stack is too fine: an identical fault reached through two different call paths
        /// would get a fresh budget each time, which is exactly the repaint storm the breaker exists to stop.
        /// The type plus where it was RAISED is the pairing that answers "is this the same thing going wrong
        /// again?".</para>
        /// <para>An exception with no stack — never thrown, or thrown across a boundary that discarded it —
        /// falls back to its type, which is all there is to go on.</para>
        /// </remarks>
        public static string FaultIdentity(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            string type = exception.GetType().FullName ?? exception.GetType().Name;
            // SLICED, not split: the identity needs the top frame only, and the path this bounds is a fault
            // raised on every repaint — formatting the whole trace into an array of per-frame strings to keep
            // one of them is the cost paid most often here.
            if (exception.StackTrace is not { Length: > 0 } stack)
            {
                return type;
            }
            int newline = stack.IndexOf('\n');
            ReadOnlySpan<char> top = (newline < 0 ? stack.AsSpan() : stack.AsSpan(0, newline)).Trim();
            return top.IsEmpty ? type : $"{type}|{top}";
        }

        /// <summary>
        /// Whether a dispatcher fault is a cancellation rather than a failure.
        /// </summary>
        /// <remarks>
        /// Unwraps an <see cref="AggregateException"/> that carries nothing but cancellations, because a
        /// continuation can present one that way and a reader has no interest in the difference. A mixed
        /// aggregate is NOT a cancellation: something in it genuinely failed.
        /// </remarks>
        public static bool IsCancellation(Exception? exception) => exception switch
        {
            null => false,
            OperationCanceledException => true,
            AggregateException aggregate =>
                aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsCancellation),
            _ => false,
        };

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
