using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Threading;
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

    /// <summary>The project file named on the command line — what "Open with…" / a double-clicked <c>.vis</c> /
    /// <c>ihc_openvisual foo.vis</c> hands the app — or null when it was launched with no file. Set once in
    /// <see cref="Main"/>; the shell opens it instead of the empty starter project (BP-11a).</summary>
    public static string? StartupProjectPath { get; private set; }

    /// <summary>The one file argument out of <paramref name="args"/>: the first argument that is not a switch.
    /// <para>argv is the ONLY route on Windows and Linux — Avalonia's <c>ActivationKind.File</c> is macOS/iOS/
    /// Android-only — so a desktop file association ultimately lands here. Nothing is validated at this point: an
    /// unreadable or non-project path is reported by the normal open-failure dialog, which is where every other
    /// bad path is reported too.</para>
    /// <para>A switch is one starting with <c>-</c>, and only that: <c>/foo.vis</c> is an ordinary absolute path on
    /// Linux and macOS, so the DOS-style <c>/flag</c> spelling cannot be recognised here.</para></summary>
    internal static string? ParseStartupProjectPath(string[] args) =>
        args.FirstOrDefault(a => a.Length > 0 && !a.StartsWith('-'));

    // Initialization code. Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant
    // code before AppMain is called: things aren't initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Order matters: config and the telemetry pipeline first, then hook the ILogger-backed unhandled-error
            // handler (A-25). Startup exceptions before this point are caught by Main's catch below.
            StartupProjectPath = ParseStartupProjectPath(args);
            Config = new AppConfiguration();
            LoggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
                Telemetry.AppServiceName, Telemetry.AppServiceNamespace, Telemetry.ActivitySourceName,
                Config.TelemetryConfig, Config.LoggingConfig);
            // All four documented exception layers, because each catches faults the others cannot see (Avalonia
            // logging review BP-09/QC-03): the DISPATCHER layer for faults inside a dispatcher operation, the
            // UNOBSERVED-TASK layer for dropped tasks, the APPDOMAIN layer for everything else on any thread, and
            // Main's catch below as the last-resort log-and-exit. Note the layers NOT covered by any of these:
            // window-lifecycle handlers (Closing/Closed/Activated) run straight off the window message loop, so each
            // carries its own try/catch (AP-06/WS-11), and on Linux the GLib boundary needs
            // X11PlatformOptions.ExternalGLibMainLoopExceptionLogger (wired in BuildAvaloniaApp).
            AppDomain.CurrentDomain.UnhandledException += TelemetryBootstrap.UnhandledExceptionHandler(
                LoggerFactory.CreateLogger("Ihc.OpenVisual.UnhandledException"));
            // Plain BCL, so it is safe here; the DISPATCHER layer is attached from BuildAvaloniaApp's AfterSetup
            // instead, because reading Dispatcher.UIThread this early would initialize the dispatcher before
            // Avalonia is set up (see the method comment above).
            TaskScheduler.UnobservedTaskException += TelemetryBootstrap.UnobservedTaskExceptionHandler(
                LoggerFactory.CreateLogger("Ihc.OpenVisual.UnobservedTaskException"));

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
            // Before the LoggerFactory below, for the reason Shutdown documents.
            TelemetryBootstrap.Shutdown();
            LoggerFactory?.Dispose();
        }
    }

    /// <summary>The X11 (Linux) platform options, whose only job here is the FIFTH exception route: when Avalonia
    /// controls no run-loop frame, an exception crossing the native GLib boundary cannot be propagated (letting it
    /// escape would corrupt GLib, which knows nothing of managed exceptions), so Avalonia discards it — with no
    /// record at all unless this logger is supplied (review BP-12/QC-04/AP-10). A factory rather than an inline
    /// object so a test can assert the logger is wired without standing up an AppBuilder.
    /// <para>No <c>WaylandPlatformOptions</c> counterpart: the app does not opt into <c>Avalonia.Wayland</c>, and
    /// should not — that backend silently removes AT-SPI2, i.e. all Linux accessibility (accessibility review
    /// BP-16/AP-10). If Wayland is ever adopted, its options need the same logger.</para></summary>
    internal static X11PlatformOptions CreateX11Options(ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger("Ihc.OpenVisual.GLibException");
        return new X11PlatformOptions
        {
            ExternalGLibMainLoopExceptionLogger = ex =>
                logger.LogError(ex, "Exception crossing the GLib main-loop boundary: {Message}", ex.Message),
        };
    }

    /// <summary>The application's own font, embedded in the executable: <c>Avalonia.Fonts.Inter</c> registers its
    /// collection under the <c>fonts:Inter</c> key, and <c>#Inter</c> names the family inside it.</summary>
    internal const string AppFontFamily = "fonts:Inter#Inter";

    /// <summary>
    /// The face for the app's one DENSE, COLUMN-ALIGNED readout — the Problemer list. Inter is a proportional UI
    /// font: excellent for labels, wrong for a findings log, where a rule id and a code read as a column and
    /// digits should line up under each other.
    /// <para>Picked per platform, like <see cref="SymbolFontFallbacks"/> above and for the same reason: the app
    /// embeds no monospace family, and naming one the machine does not have buys nothing. These three ship with
    /// their platforms and all three carry æ/ø/å. If one is somehow absent the font manager falls back to the
    /// app default, which costs the alignment but never the text — a legibility choice, not a correctness one,
    /// which is exactly why it is allowed to depend on the platform where the embedded UI font may not.</para>
    /// </summary>
    internal static string MonoFontFamily =>
        OperatingSystem.IsWindows() ? "Consolas"
        : OperatingSystem.IsMacOS() ? "Menlo"
        : "DejaVu Sans Mono";

    /// <summary>
    /// Registers the embedded Inter font AND makes it the default family. Both halves are needed and only the first
    /// is obvious: <c>WithInterFont()</c> alone merely makes the collection resolvable — the package declares no
    /// default family name — so every control that states no <c>FontFamily</c> would still render in whatever the
    /// platform picked (Segoe UI Variable / SF / a fontconfig guess), which is the portability defect this closes:
    /// identical metrics and identical æ/ø/å on all three desktops, from the font the app ships rather than one it
    /// hopes to find.
    /// <para>Set as the MANAGER default rather than as <c>FontFamily</c> on the shell window, because the app's
    /// code-built dialogs and every popup are their own top levels and inherit nothing from that window.</para>
    /// <para>The fallbacks cover what Inter does not: the symbol/emoji codepoints. They are per-platform because a
    /// fallback naming a family the machine does not have buys nothing.</para>
    /// </summary>
    internal static AppBuilder WithAppFonts(AppBuilder builder) =>
        builder.WithInterFont().With(new FontManagerOptions
        {
            DefaultFamilyName = AppFontFamily,
            FontFallbacks = SymbolFontFallbacks(),
        });

    private static IReadOnlyList<FontFallback> SymbolFontFallbacks()
    {
        string[] families =
            OperatingSystem.IsWindows() ? ["Segoe UI Emoji", "Segoe UI Symbol"]
            : OperatingSystem.IsMacOS() ? ["Apple Color Emoji", "Apple Symbols"]
            : ["Noto Color Emoji", "DejaVu Sans"];
        return families.Select(f => new FontFallback { FontFamily = new FontFamily(f) }).ToArray();
    }

    // Avalonia configuration, don't remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            // Feed the app's OWN ILogger output into the Dev Tools Logs table alongside Avalonia's, so framework
            // and application logs are filterable in one place instead of two disjoint streams (BP-20).
            .WithDeveloperTools(options =>
            {
                if (LoggerFactory is { } factory)
                    options.AddMicrosoftLoggerObservable(factory, LogLevel.Debug);
            })
#endif
            ;
        builder = WithAppFonts(builder);

        if (LoggerFactory is { } loggerFactory && Config is { } config)
        {
            LogLevel avaloniaLevel = config.LoggingConfig.GetValue("LogLevel:Avalonia", LogLevel.Warning);
            LogEventLevel level = AppTelemetryBootstrap.MapFromIlogToAvaloniaLogLevel(avaloniaLevel);
            // The default trace logger must be installed before our sink so our sink can chain to it.
            builder = builder.LogToTrace(level).LogToSink(loggerFactory).With(CreateX11Options(loggerFactory));
            // The DISPATCHER exception layer (BP-09). AfterSetup, not Main: the dispatcher must exist first, and
            // reading Dispatcher.UIThread before Avalonia is initialized would create it too early.
            builder = builder.AfterSetup(_ =>
                Dispatcher.UIThread.UnhandledException += AppTelemetryBootstrap.DispatcherExceptionHandler(
                    loggerFactory.CreateLogger("Ihc.OpenVisual.DispatcherException")));
        }
        else
        {
            builder = builder.LogToTrace();
        }

        return builder;
    }
}
