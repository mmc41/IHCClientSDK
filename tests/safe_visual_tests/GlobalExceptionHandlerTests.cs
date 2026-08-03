using System;
using System.Threading.Tasks;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Ihc.Bootstrap;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The four documented exception layers (Avalonia logging review BP-09 / QC-03). Two were already wired —
/// <see cref="AppDomain.UnhandledException"/> and the <c>try/catch</c> around the lifetime start in
/// <c>Program.Main</c>; these cover the two that were not: the <b>dispatcher</b> layer (which
/// <c>Dispatcher.UIThread.UnhandledException</c> is the only route to — an
/// <see cref="AppDomain"/> handler cannot see a dispatcher operation's fault before the dispatcher decides what
/// to do with it) and the <b>unobserved task</b> layer.
/// <para>The dispatcher handler must LOG WITHOUT HANDLING (review WS-05/AP-07): silently resuming a UI thread
/// whose operation faulted continues on possibly corrupt state, and <c>Handled</c> is set-once-true (BP-08), so a
/// handler that sets it can never be overruled by a later one.</para>
/// </summary>
public class GlobalExceptionHandlerTests
{
    /// <summary>The dispatcher layer: a faulted dispatcher operation reaches ILogger (hence OTLP), and the
    /// exception is still allowed to propagate — the handler observes, it does not swallow.</summary>
    [AvaloniaTest]
    public void DispatcherException_IsLoggedAndNotSwallowed()
    {
        var logger = new CapturingLogger();
        DispatcherUnhandledExceptionEventHandler handler = AppTelemetryBootstrap.DispatcherExceptionHandler(logger);
        Dispatcher.UIThread.UnhandledException += handler;
        try
        {
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("dispatcher-boom-42"));

            // Still throws => the handler left Handled false, so the framework's own escalation still runs.
            Assert.That(() => Dispatcher.UIThread.RunJobs(), Throws.Exception.Message.Contains("dispatcher-boom-42"),
                "the handler observes the fault but does not mark it handled (WS-05)");
            Assert.That(logger.Messages, Has.Some.Contains("dispatcher-boom-42"),
                "the dispatcher fault reaches the ILogger pipeline");
            Assert.That(logger.Messages, Has.Some.Contains("Critical"), "and at a critical level");
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= handler;
        }
    }

    /// <summary>The unobserved-task layer: the fault is recorded and OBSERVED, so a dropped task cannot take the
    /// process down later from the finalizer thread. Logged at Warning, not Critical — per the review this event is
    /// a leak detector rather than a primary error path (WS-06), and it fires arbitrarily late.</summary>
    [Test]
    public void UnobservedTaskException_IsLoggedAndObserved()
    {
        var logger = new CapturingLogger();
        var args = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException("unobserved-boom-42")));

        AppTelemetryBootstrap.LogUnobservedTaskException(logger, args);

        Assert.Multiple(() =>
        {
            Assert.That(logger.Messages, Has.Some.Contains("unobserved-boom-42"),
                "the dropped task's fault reaches the ILogger pipeline");
            Assert.That(args.Observed, Is.True,
                "and is marked observed, so it never re-surfaces as a process-killing finalizer-thread throw");
        });
    }

    /// <summary>The Linux boundary the four managed layers cannot reach (review BP-12/QC-04/AP-10). When Avalonia
    /// controls no run-loop frame, an exception crossing the native GLib boundary is DISCARDED with no record —
    /// which would silently hollow out this app's crash telemetry on Linux. Asserted against the options factory
    /// rather than AppBuilder internals, so it runs on any CI OS.</summary>
    [Test]
    public void X11Options_CarryAnExternalGLibExceptionLogger()
    {
        var factory = new CapturingLoggerFactory();

        Action<Exception>? glibLogger = ihc_openvisual.Program.CreateX11Options(factory).ExternalGLibMainLoopExceptionLogger;

        Assert.That(glibLogger, Is.Not.Null, "the X11 options carry a GLib exception logger");
        glibLogger!(new InvalidOperationException("glib-boom-42"));
        Assert.That(factory.Messages, Has.Some.Contains("glib-boom-42"),
            "and it routes the otherwise-discarded exception into the ILogger pipeline");
    }
}
