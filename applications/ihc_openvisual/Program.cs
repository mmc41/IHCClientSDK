using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

    /// <summary>
    /// Whether the application was started with <see cref="TestSurfaceArgument"/>. Set once in
    /// <see cref="Main"/>; false in every session a person starts.
    /// </summary>
    /// <remarks>
    /// It gates ONE thing: whether <see cref="Services.AutomationSnapshotPublisher"/> writes the read-only
    /// state snapshot a driver waits on. It is read at the composition root and passed on as a VALUE, so
    /// nothing below the root can branch on it — see that class, and the architecture gate that holds the rule.
    /// </remarks>
    public static bool TestSurfaceEnabled { get; private set; }

    /// <summary>
    /// The switch that turns the test surface on. Admissible behind it: state the application already computes
    /// and discards, where publishing it unconditionally could disturb a user. NOT admissible, ever: anything
    /// that changes what the application DOES — no seed, no reset, no time control, no relaxed validation, no
    /// authentication bypass, no altered persistence. A candidate that would make the same input produce a
    /// different outcome belongs neither behind this switch nor in the product.
    /// </summary>
    internal const string TestSurfaceArgument = "--test";

    /// <summary>Whether <paramref name="args"/> asks for the test surface.</summary>
    /// <remarks>
    /// No change to <see cref="ParseStartupProjectPath"/> is needed or wanted: it already takes the first
    /// argument that is not a switch, so <c>ihc_openvisual foo.vis --test</c> still resolves the file.
    /// </remarks>
    internal static bool ParseTestSurfaceEnabled(string[] args) =>
        args.Contains(TestSurfaceArgument, StringComparer.Ordinal);

    // Initialization code. Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant
    // code before AppMain is called: things aren't initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // The stamp goes first so Launch.Host means what it says — everything before Main, and nothing Main
            // itself did. StartMulticoreJit follows immediately, still ahead of the launch it is meant to cover,
            // and gets a stamp of its OWN: it creates a directory under local application data and starts the
            // profile recorder, which is file IO on a cold or redirected profile folder. Folded into the next
            // phase it would arrive as time spent reading settings, which is the exact misattribution these
            // phases exist to remove.
            DateTime entered = DateTime.UtcNow;
            StartMulticoreJit();
            DateTime accelerated = DateTime.UtcNow;
            // Order matters: config and the telemetry pipeline first, then hook the ILogger-backed unhandled-error
            // handler (A-25). Startup exceptions before this point are caught by Main's catch below.
            StartupProjectPath = ParseStartupProjectPath(args);
            TestSurfaceEnabled = ParseTestSurfaceEnabled(args);
            Config = new AppConfiguration();
            DateTime configured = DateTime.UtcNow;
            LoggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
                Telemetry.AppServiceName, Telemetry.AppServiceNamespace, Telemetry.ActivitySourceName,
                Config.TelemetryConfig, Config.LoggingConfig);
            RecordPreTelemetryPhases(entered, accelerated, configured,
                LoggerFactory.CreateLogger("Ihc.OpenVisual.LaunchPhases"));
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
            TaskScheduler.UnobservedTaskException += UnobservedTaskHandler(
                LoggerFactory.CreateLogger("Ihc.OpenVisual.UnobservedTaskException"));

            // Probe the configured OTLP endpoint so a wrong endpoint/token fails loudly instead of silently
            // dropping all telemetry. Runs in the background; never blocks the workspace from opening. The fault is
            // OBSERVED (a continuation logs it) so a probe exception is not swallowed as an UnobservedTaskException.
            //
            // The RESULT, not just a probe fault. The SDK's ProbeAndReportAsync writes every outcome to
            // Trace.WriteLine and a problem to Console.Error — the right fallback for a host with no logging
            // pipeline, and useless in a WinExe, which has neither a console nor anyone reading trace output. So
            // this application takes the structured door instead and routes the outcome itself.
            ReportSelfCheckAsync(
                LoggerFactory.CreateLogger("Ihc.OpenVisual.TelemetrySelfCheck"), Config.TelemetryConfig)
                .ContinueWith(
                    t => LoggerFactory!.CreateLogger("Ihc.OpenVisual.TelemetrySelfCheck")
                        .LogWarning(t.Exception, "Telemetry self-check faulted"),
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

            StartCatalogWarmup(LoggerFactory.CreateLogger("Ihc.OpenVisual.CatalogWarmup"));

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Main is void, so this is the only way a windowed application can tell its launcher that the run
            // failed. Everything the report DECIDES lives in the member below, where a test can reach it.
            Environment.ExitCode = ReportFatalStartup(ex, LoggerFactory, DefaultLastResortPath(), App.LaunchScope);
        }
        finally
        {
            // Before BOTH of the below, because the warm-up reports through them from a pool thread. A launch
            // that fails early exits here while the catalog is still materializing, and tearing the pipeline
            // down under it loses the Materialize span and points that thread's own fault handler at a disposed
            // logger. On a normal exit the warm-up finished long ago and this costs nothing.
            AwaitCatalogWarmup(CatalogWarmupShutdownLimit);
            // Before the LoggerFactory below, for the reason Shutdown documents.
            TelemetryBootstrap.Shutdown();
            LoggerFactory?.Dispose();
        }
    }

    /// <summary>
    /// Where the runtime keeps the JIT profile <see cref="StartMulticoreJit"/> records — LOCAL application data,
    /// because the profile is keyed to this machine's hardware and to these exact binaries. See
    /// <see cref="Constants.LocalAppDataPath"/> for why that distinction has its own door.
    /// </summary>
    internal static string JitProfileDirectory() => Constants.LocalAppDataPath("jit");

    /// <summary>
    /// Turns on multicore JIT: the runtime records which methods this launch compiles, and on every later launch
    /// compiles them on a background thread ahead of the thread that will call them.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a desktop shell wants it.</b> Most of a launch is first-call compilation — of Avalonia, of the
    /// SDK, of this application — and it is serialized behind the one thread doing the work. The recorded profile
    /// lets a spare core do it in parallel instead. Nothing is precompiled or cached across versions: the profile
    /// names methods, the runtime compiles them itself, and a profile that no longer matches the binaries simply
    /// stops matching and costs nothing.</para>
    /// <para><b>It changes no behaviour.</b> Compiling a method earlier cannot change what the method does, and
    /// the first launch after an install (or with the profile missing, unreadable or stale) merely records a new
    /// one and runs exactly as it did before.</para>
    /// <para>Contained, because a startup accelerator must never be the reason a launch fails: the profile lives
    /// under the user's application data, which can be read-only, full or absent, and none of that is worth an
    /// exit. There is nowhere to report it yet either — this runs before the logging pipeline exists, which is
    /// the whole point of running it here.</para>
    /// <para><b>The catch is UNFILTERED, and that is the point.</b> A filtered one delivers "never fails, for the
    /// types I thought of" — which is not the contract above. The types are not enumerable in advance: a
    /// redirected or policy-restricted application-data folder raises <c>SecurityException</c>, and a future
    /// runtime is free to introduce its own. And the failure class most likely to escape a whitelist is the one
    /// that resolves the path, which is also the one that would break the fallback reporter: <c>Main</c>'s catch
    /// evaluates <see cref="DefaultLastResortPath"/> — the same <see cref="Constants"/> lookup — as an argument,
    /// before <see cref="ReportFatalStartup"/>'s own try, so an escape here would exit the process with no window,
    /// no telemetry, and no breadcrumb either. Nothing this method can throw is worth that.</para>
    /// </remarks>
    internal static void StartMulticoreJit()
    {
        try
        {
            string root = JitProfileDirectory();
            Directory.CreateDirectory(root);
            System.Runtime.ProfileOptimization.SetProfileRoot(root);
            System.Runtime.ProfileOptimization.StartProfile("startup.jitprofile");
        }
#pragma warning disable CA1031 // See the remarks: a startup accelerator may not fail the launch, for ANY reason.
        catch (Exception fault)
#pragma warning restore CA1031
        {
            Trace.WriteLine("Multicore JIT not started: " + fault);
        }
    }

    /// <summary>
    /// Exports the launch phases that ran BEFORE the telemetry pipeline existed, backdated to when they
    /// actually ran.
    /// </summary>
    /// <remarks>
    /// <para>These cannot be recorded as they happen — there is no listener until the pipeline is built — so each
    /// is recorded afterwards with the explicit start and end time of the phase it describes, through
    /// <see cref="Ihc.OperationTelemetry.Record"/>, whose remarks carry why a backdated phase is a root. The
    /// phase before <c>Main</c> is measured from the operating system's own process-start clock, which is the
    /// only record that it happened at all.</para>
    /// <para>Contained for the reason <see cref="StartMulticoreJit"/> is: a measurement may not be the reason the
    /// launch it measures fails. Unlike that one it reports through the pipeline, which exists by now — the
    /// structured door this application takes everywhere else. Contained PER PHASE rather than once around all
    /// four, for the reason given at the calls.</para>
    /// </remarks>
    /// <param name="entered">When <see cref="Main"/>'s first statement ran.</param>
    /// <param name="accelerated">When <see cref="StartMulticoreJit"/> had returned.</param>
    /// <param name="configured">When <c>AppConfiguration</c> had been built.</param>
    /// <param name="logger">Where a failure to record is reported.</param>
    internal static void RecordPreTelemetryPhases(DateTime entered, DateTime accelerated, DateTime configured,
                                                  ILogger logger)
    {
        DateTime ready = DateTime.UtcNow;
        // One contiguous partition of the wall clock, so the four read in order at one indent — but each
        // CONTAINED SEPARATELY, because they do not share a failure mode and one of them can fail alone.
        // Only Host mixes clocks: the operating system's process start against Main's own UtcNow. Its round
        // trip through local time resolves an ambiguous hour — the repeat after a daylight-saving fall back —
        // to standard time, which can date the process start an HOUR AFTER Main was entered. The core rejects
        // a reversed pair, correctly, and recorded together that one refusal would take the three phases that
        // are stamped from a single clock and cannot be wrong down with it.
        RecordPhase(logger, "Host", ReadProcessStartUtc, entered);
        RecordPhase(logger, "Jit", () => entered, accelerated);
        RecordPhase(logger, "Config", () => accelerated, configured);
        RecordPhase(logger, "Telemetry", () => configured, ready);
    }

    /// <summary>Records one launch phase, containing whatever that one phase costs.</summary>
    /// <remarks>
    /// The start is a FUNCTION rather than a value so that reading it is inside the same containment as
    /// recording it: <see cref="ReadProcessStartUtc"/> asks the operating system and can fail on its own, and
    /// a measurement may not be the reason the launch it measures fails.
    /// </remarks>
    private static void RecordPhase(ILogger logger, string phase, Func<DateTime> startedAt, DateTime endedAt)
    {
        try
        {
            AppTelemetryRegistry.For(LaunchPhaseOwner).Record(phase, startedAt(), endedAt);
        }
#pragma warning disable CA1031 // See the remarks: a measurement may not fail the launch, for ANY reason.
        catch (Exception fault)
#pragma warning restore CA1031
        {
            logger.LogWarning(fault, "Launch phase {Phase} not recorded", phase);
        }
    }

    /// <summary>When this process was started, by the operating system's own record of it — the only evidence
    /// that anything happened before <see cref="Main"/>.</summary>
    private static DateTime ReadProcessStartUtc()
    {
        using Process self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime();
    }

    /// <summary>The span-name prefix the pre-telemetry phases share, so they group as one phase of the launch
    /// rather than reading as three unrelated roots.</summary>
    private const string LaunchPhaseOwner = "Launch";

    /// <summary>
    /// Asks the SDK to build what it defers, on a background thread, through the facade's own door.
    /// </summary>
    /// <remarks>
    /// <para><b>Why here.</b> The shell cannot show a menu bar without the component catalog — the composition
    /// root's very first act is to build the product and function-block menus from it — so the ~0.5 s
    /// materialization used to sit squarely on the launch's critical path. Everything between this line and that
    /// first query is work the catalog does not participate in: Avalonia's platform detection, the XAML load,
    /// font registration. Started here it runs alongside them, and the shell's first query finds it built.</para>
    /// <para>WHAT gets built is deliberately not this file's business — it calls
    /// <c>ProjectAppService.Warmup</c> and the SDK decides what that covers. <c>Ihc.Vis.Catalog</c> is banned
    /// in this project for the same reason <c>Ihc.Vis.Io</c> and <c>Ihc.Vis.Editing</c> are, so the door is the
    /// facade's or there is no door.</para>
    /// <para><b>Which work may be forked here at all, and why only work the launch WAITS for is worth forking,</b>
    /// are one rule with one measurement behind it: see the forkability rule in <c>ARCHITECTURE.md</c> ("What may
    /// be forked to overlap Avalonia's start-up"). What is local to this file is the ordering below.</para>
    /// <para><b>It must be started AFTER the telemetry pipeline, and the ordering in <see cref="Main"/> is load
    /// bearing.</b> An <c>ActivitySource</c> with no listener returns a null activity and a histogram with no
    /// collecting <c>MeterProvider</c> records nothing, so a warm-up moved above
    /// <c>SetupTelemetryAndLogging</c> would silently discard both the <c>BuiltInCatalog.Materialize</c> span and
    /// its duration point — leaving the largest single item in a launch unmeasured, and looking exactly like a
    /// launch that never built the catalog. Under the old lazy this could not go wrong, because materialization
    /// necessarily happened later, on the first UI catalog query.</para>
    /// <para><b>The flow suppression is what makes the span a trace ROOT, rather than where this line sits.</b>
    /// <c>Task.Run</c> captures the execution context, <c>Activity.Current</c> with it, and the SDK's spans take
    /// an implicit parent — so a fork moved below anything that opens a scope would silently re-parent half a
    /// second of background work into an operation it outlives, a defect this repository has met before and
    /// cannot see from a passing test. Suppressing the flow makes the answer the same wherever the call moves.
    /// A reader of the launch tree should know the materialization span exists elsewhere under the same run id
    /// rather than conclude the catalog was free.</para>
    /// <para>The fault is caught INSIDE the work item rather than through a continuation, because
    /// <c>ContinueWith(..., OnlyOnFaulted)</c> CANCELS the continuation when the antecedent succeeds — the trap
    /// <c>TaskSupervisor.Fire</c> documents avoiding, one assembly away. Catching in the body also keeps the
    /// work off <c>TaskScheduler.Current</c>, which such a continuation would have bound.</para>
    /// </remarks>
    internal static void StartCatalogWarmup(ILogger logger)
    {
        try
        {
            // Around the FORK only: the flow control must be disposed on the thread that opened it, and the work
            // item has captured its (empty) context by the time Run returns.
            using (System.Threading.ExecutionContext.SuppressFlow())
            {
                // KEPT rather than discarded, and the reason is a lifetime one: this work item reports through
                // the telemetry pipeline and the logger factory, and Main's finally tears both down. A launch
                // that fails early reaches that finally while the catalog is still materializing, so without a
                // handle to wait on there is no way to order the two. AwaitCatalogWarmup below is the other half.
                catalogWarmup = Task.Run(() =>
                {
                    try
                    {
                        Ihc.Vis.ProjectAppService.Warmup();
                    }
#pragma warning disable CA1031 // A warm-up fault is not a user-facing failure; see the remarks for what it is.
                    catch (Exception fault)
#pragma warning restore CA1031
                    {
                        logger.LogWarning(fault, "Catalog warm-up faulted on a background thread");
                    }
                });
            }
        }
        // The FORK ITSELF failing, which is a different thing from the work failing: SuppressFlow throws when
        // flow is already suppressed, and Task.Run allocates. Contained for the reason StartMulticoreJit is —
        // a start-up accelerator must never be why a launch has no window.
#pragma warning disable CA1031 // See above: for ANY reason, per the same contract as the two helpers above.
        catch (Exception fault)
#pragma warning restore CA1031
        {
            logger.LogWarning(fault, "Catalog warm-up could not be started; the catalog builds on first use");
        }
    }

    /// <summary>The warm-up while it is outstanding, so <see cref="AwaitCatalogWarmup"/> can order it against
    /// the pipeline it reports through. Null until one is started, and left set afterwards — a completed task
    /// is what makes the wait a no-op rather than a special case.</summary>
    private static Task? catalogWarmup;

    /// <summary>Test seam: the warm-up handle itself, because a wait that found nothing outstanding cannot
    /// tell a warm-up that finished from one nobody kept.</summary>
    internal static Task? OutstandingCatalogWarmup => catalogWarmup;

    /// <summary>How long a shutdown waits for the warm-up. Generously above the ~550 ms materialization it is
    /// waiting for: the bound is there so a pathological warm-up cannot hold a closing application open, not to
    /// race the normal one.</summary>
    internal static readonly TimeSpan CatalogWarmupShutdownLimit = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Waits for a started catalog warm-up to finish, so it cannot outlive the telemetry pipeline and logger
    /// factory it reports through.
    /// </summary>
    /// <remarks>
    /// <para>On a normal exit there is nothing to wait for — the shell queried the catalog long ago. This is for
    /// the launch that FAILS: <c>Main</c>'s catch runs while a pool thread is still inside the materialization,
    /// and shutting the pipeline down under it would drop the <c>BuiltInCatalog.Materialize</c> span and point
    /// that thread's own fault handler at a disposed logger factory — turning a contained warm-up fault into an
    /// unobserved second exception on the way out.</para>
    /// <para>Reported to <c>Trace</c> and nowhere else, deliberately: this is the last thing that runs before the
    /// pipeline goes down, and on the path it exists for the factory may never have come up at all.</para>
    /// </remarks>
    /// <param name="limit">How long to wait before giving up on it.</param>
    /// <returns>Whether nothing is left outstanding — false only if the wait ran out.</returns>
    internal static bool AwaitCatalogWarmup(TimeSpan limit)
    {
        Task? warmup = catalogWarmup;
        if (warmup is null)
        {
            return true;
        }
        try
        {
            return warmup.Wait(limit);
        }
#pragma warning disable CA1031 // A wait on the way out may not become the failure that gets reported.
        catch (Exception fault)
#pragma warning restore CA1031
        {
            // Only reachable if the work item's own logging threw, which is the case this whole ordering
            // exists to make rare. Waiting is what OBSERVES it either way.
            Trace.WriteLine("Catalog warm-up faulted: " + fault);
            return true;
        }
    }

    /// <summary>The exit code a fatal start-up reports. Any non-zero value would do; what matters is that it is
    /// not zero, which is what the process used to return after failing to start at all.</summary>
    internal const int FatalStartupExitCode = 1;

    /// <summary>
    /// Reports a start-up that could not complete, and answers with the process's exit code.
    ///
    /// <para>Through the wired <see cref="ILogger"/> when the pipeline is up. When it is NOT — the fault was
    /// thrown while building configuration or telemetry, which is the case this exists for — the pipeline will
    /// never come up for this run, so a breadcrumb is written to <paramref name="lastResortPath"/> instead. That
    /// file is the only record such a run will ever have: the application is a <c>WinExe</c> with no console, so
    /// <c>Trace</c> reaches nobody unless a debugger is attached.</para>
    ///
    /// <para>Not the persistence D02 refused: this writes a diagnostic entry, never a <i>Problemer</i> row, and
    /// nothing reads it back into the application.</para>
    /// </summary>
    /// <param name="error">The fault that stopped start-up.</param>
    /// <param name="loggerFactory">The pipeline, or null when it never came up.</param>
    /// <param name="lastResortPath">Where the breadcrumb goes when there is no pipeline.</param>
    /// <param name="launch">
    /// The launch scope, when composition had already opened one. It is normally closed by the window's
    /// <c>Opened</c> handler, which a start-up that failed never reaches — so without this the launch an
    /// operator most needs to find is the only one that exports no span at all. Measured: a fault in the
    /// composition root produced a Critical log record and zero spans for the whole run.
    /// </param>
    /// <returns>The exit code to report.</returns>
    internal static int ReportFatalStartup(Exception error, ILoggerFactory? loggerFactory, string lastResortPath,
                                           Ihc.OperationScope? launch = null)
    {
        // BEFORE the log record, for the reason FailureReport gives about ordering: the outcome first, because
        // what comes after it can be slow, can fail on its own, or can be the thing that is broken.
        if (launch is not null)
        {
            launch.SetOutcome(Ihc.OperationOutcome.Failed(error));
            launch.Dispose();   // Idempotent, so a launch that had already closed keeps the span it exported.
        }
        if (loggerFactory is { } factory)
        {
            factory.CreateLogger("Ihc.OpenVisual.Startup").LogCritical(error, "Fatal startup error");
            return FatalStartupExitCode;
        }
        try
        {
            string? directory = Path.GetDirectoryName(lastResortPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            // Timestamped, and appended rather than replaced, so a start-up that fails every time reads as a
            // pattern instead of overwriting its own evidence. The exception's full text is included because with
            // no pipeline this is where the stack ends its life.
            File.AppendAllText(lastResortPath,
                $"{DateTimeOffset.UtcNow:O} Fatal startup error: {error}{Environment.NewLine}");
        }
        catch (Exception writeFailure) when (writeFailure is IOException or UnauthorizedAccessException
                                                          or NotSupportedException or ArgumentException)
        {
            // A breadcrumb that cannot land must not become the failure that gets reported. Trace is where this
            // ends, which is exactly the dead end the file exists to replace — so both faults go there together.
            Trace.WriteLine("Fatal startup error " + error);
            Trace.WriteLine("...and the last-resort write failed too: " + writeFailure);
        }
        return FatalStartupExitCode;
    }

    /// <summary>
    /// The unobserved-task layer, composed AROUND the shared handler at this application's registration site so
    /// <c>ihc_telemetrybootstrap</c> stays the logging-only component every application shares. The shared half
    /// still logs and still calls <c>SetObserved()</c>; the durable row is this application's addition.
    /// </summary>
    /// <remarks>
    /// <para><b>What this layer stamps is a DISCOVERY time, not a fault time.</b> The event fires when the GC
    /// gets to the faulted task, which may be long after the fault and after any number of user actions.</para>
    /// <para>So a task that faulted under project A can surface inside project B's generation, and the row is
    /// then either missing — the sink cleared on the generation move in between — or misleading, present and
    /// read as being about the open project. <b>No timestamp comparison can detect that</b>: the only clock
    /// reading available here is the discovery, and the fault's own time was never recorded anywhere.</para>
    /// <para>What bounds the population instead is SUPERVISION. A task that is observed never reaches this layer
    /// at all, so what arrives here is by construction the residue nothing else caught — which is why the
    /// supervisor around every fired task is the mitigation and a clock comparison is not.</para>
    /// <para>The row is reported BEFORE the shared handler, which is where <c>SetObserved()</c> happens, so it
    /// exists even if the shared half were to throw on its way to it.</para>
    /// </remarks>
    internal static EventHandler<UnobservedTaskExceptionEventArgs> UnobservedTaskHandler(ILogger logger)
    {
        EventHandler<UnobservedTaskExceptionEventArgs> logAndObserve =
            TelemetryBootstrap.UnobservedTaskExceptionHandler(logger);
        return (sender, args) =>
        {
            ihc_openvisual.Services.TaskSupervisor.Report(
                args.Exception, "TaskScheduler.UnobservedTaskException");
            logAndObserve(sender, args);
        };
    }

    /// <summary>
    /// Runs the telemetry self-check and reports its RESULT — not merely a fault in running it.
    /// </summary>
    /// <remarks>
    /// <para><b>The row is the point.</b> A pipeline that is down is the one condition telemetry cannot report,
    /// because the reporting mechanism is the thing that failed. The log line goes to whatever local providers
    /// are configured; the row is what a person looking at the running application can actually see.</para>
    /// <para>Reported at start-up, so it lands before the first generation is followed and stays for the
    /// session. <c>Disabled</c> and <c>Reachable</c> report nothing: a self-check nobody configured is not a
    /// fault, and a working pipeline is not news.</para>
    /// </remarks>
    internal static async Task ReportSelfCheckAsync(ILogger logger, Ihc.TelemetryConfiguration telemetry)
    {
        Ihc.TelemetrySelfCheckResult result = await Ihc.TelemetrySelfCheck.ProbeAsync(telemetry);
        if (!result.IsProblem)
        {
            logger.LogInformation("Telemetry self-check: {Message}", result.Message);
            return;
        }

        logger.LogWarning("Telemetry self-check: {Message}", result.Message);
        ihc_openvisual.Services.TaskSupervisor.Report(
            ihc_openvisual.Services.HostProblems.TelemetryPipelineDown(result.Message),
            Ihc.Vis.Problems.InternalErrorOrigin.Host,
            $"Ihc.TelemetrySelfCheck.ProbeAsync: {result.Status} — {result.Message}");
    }

    /// <summary>Where the breadcrumb goes: beside the application's own preference files under the user's
    /// application data, NOT beside the executable — an installation directory is frequently not writable by the
    /// account running the app, which is the one condition this file must survive.</summary>
    internal static string DefaultLastResortPath() =>
        Constants.AppDataPath("startup-error.log");

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
            {
                logger.LogError(ex, "Exception crossing the GLib main-loop boundary: {Message}", ex.Message);
                // A durable row as well, with PLATFORM origin and a sentence that claims nothing. Avalonia
                // discards this exception whether or not anyone is listening, so the row is a RECORD and never a
                // recovery: it does not say the action failed (nobody knows which action), and it does not say
                // the app is otherwise fine (nobody knows that either). Worth writing down precisely because
                // this is the one route where nothing else in the app will.
                ihc_openvisual.Services.TaskSupervisor.Report(
                    ihc_openvisual.Services.HostProblems.PlatformFault(),
                    Ihc.Vis.Problems.InternalErrorOrigin.Platform,
                    $"X11PlatformOptions.ExternalGLibMainLoopExceptionLogger: {ex}");
            },
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

    private static FontFallback[] SymbolFontFallbacks()
    {
        string[] families =
            OperatingSystem.IsWindows() ? ["Segoe UI Emoji", "Segoe UI Symbol"]
            : OperatingSystem.IsMacOS() ? ["Apple Color Emoji", "Apple Symbols"]
            : ["Noto Color Emoji", "DejaVu Sans"];
        return families.Select(f => new FontFallback { FontFamily = new FontFamily(f) }).ToArray();
    }

    /// <summary>
    /// Installs Avalonia's two log destinations at the level <c>LogLevel:Avalonia</c> configures: the framework's
    /// own trace logger, and the sink that forwards Avalonia's internal logs into <see cref="ILogger"/> and hence
    /// into OpenTelemetry.
    /// </summary>
    /// <remarks>
    /// BOTH destinations take the level, and that is the whole point of this seam. The forwarding sink defaults to
    /// <see cref="LogEventLevel.Verbose"/> — no floor of its own — and it logs under its own category rather than
    /// an <c>Avalonia.*</c> one, so the <c>LogLevel:Avalonia</c> entry cannot reach it as an ILogger filter either.
    /// Left at the default it forwards every layout pass Avalonia reports, at Information, to the console and to
    /// the telemetry backend, and the configured level silently governs only the trace logger — which nothing
    /// reads. Pinned by <c>AvaloniaLogLevelTests</c>.
    /// </remarks>
    internal static AppBuilder WithAvaloniaLogging(AppBuilder builder, IConfiguration loggingConfig,
        ILoggerFactory loggerFactory)
    {
        LogLevel avaloniaLevel = loggingConfig.GetValue("LogLevel:Avalonia", LogLevel.Warning);
        LogEventLevel level = AppTelemetryBootstrap.MapFromIlogToAvaloniaLogLevel(avaloniaLevel);
        // The default trace logger must be installed before our sink so our sink can chain to it.
        return builder.LogToTrace(level).LogToSink(loggerFactory, level);
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
            builder = WithAvaloniaLogging(builder, config.LoggingConfig, loggerFactory)
                .With(CreateX11Options(loggerFactory));
            // The DISPATCHER exception layer (BP-09). AfterSetup, not Main: the dispatcher must exist first, and
            // reading Dispatcher.UIThread before Avalonia is initialized would create it too early.
            builder = builder.AfterSetup(_ =>
                Dispatcher.UIThread.UnhandledException += AppTelemetryBootstrap.DispatcherExceptionHandler(
                    loggerFactory.CreateLogger("Ihc.OpenVisual.DispatcherException"),
                    // The durable half. Through the same static port every other static-context floor uses, so
                    // it no-ops until the composition root sets it — which is the ordering this layer has always
                    // had: the dispatcher is wired before App exists.
                    fault => ihc_openvisual.Services.TaskSupervisor.Report(
                        fault, "Dispatcher.UnhandledException")));
        }
        else
        {
            builder = builder.LogToTrace();
        }

        return builder;
    }
}
