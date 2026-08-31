using System;
using System.Collections.Generic;
using System.Linq;
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
/// whose operation faulted continued on possibly corrupt state.
/// <para><b>That position is REVERSED, and the reversal is a trade rather than a safety claim.</b> A dispatcher
/// fault is now handled, recorded durably, and survived — because a UI-thread fault is the commonest way this
/// application dies and, with nothing persisted across the crash, otherwise the one class of fault nobody can
/// describe afterwards. The state after resuming may genuinely be inconsistent; what bounds the damage is that
/// the document itself is an immutable value, and that the breaker below bounds how long a corrupted session
/// keeps producing rows.</para>
/// <para><c>Handled</c> is set-once-true (BP-08), so the breaker has to decide BEFORE the flag is set: a sink
/// that de-duplicates would recognise the repeat only after the fault had already been swallowed.</para>
/// </summary>
public class GlobalExceptionHandlerTests
{
    /// <summary>
    /// The dispatcher layer: a faulted dispatcher operation reaches ILogger (hence OTLP), leaves a DURABLE row,
    /// and the application SURVIVES it.
    /// </summary>
    /// <remarks>
    /// The name states the swallow, because the swallow is the deliberate trade ADR-001 records. A test whose
    /// name asserted the opposite invariant would be worse than no test at all.
    /// </remarks>
    [AvaloniaTest]
    public void DispatcherException_IsLoggedRecordedAndSurvived()
    {
        using SupervisedFaults faults = SupervisedFaults.Capture();
        var logger = new CapturingLogger();
        DispatcherUnhandledExceptionEventHandler handler = AppTelemetryBootstrap.DispatcherExceptionHandler(
            logger, fault => ihc_openvisual.Services.TaskSupervisor.Report(fault, "Dispatcher.UnhandledException"));
        Dispatcher.UIThread.UnhandledException += handler;
        try
        {
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("dispatcher-boom-42"));

            Assert.Multiple(() =>
            {
                Assert.That(() => Dispatcher.UIThread.RunJobs(), Throws.Nothing,
                    "SURVIVED: the fault no longer takes the process down (D03/D14)");
                Assert.That(logger.Messages, Has.Some.Contains("dispatcher-boom-42"),
                    "the dispatcher fault still reaches the ILogger pipeline");
                Assert.That(logger.Messages, Has.Some.Contains("Critical"), "and at a critical level");
                Assert.That(faults.Rows.Single().Detail, Does.Contain("dispatcher-boom-42"),
                    "and it leaves a durable row — surviving without a record would be the worse half of the "
                    + "trade with none of the benefit");
            });
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= handler;
        }
    }

    /// <summary>
    /// THE BREAKER (D21). The same fault repeating is survived <see cref="AppTelemetryBootstrap.BreakerLimit"/>
    /// minus one times and then escalated, so a fault raised on every repaint cannot leave an application that
    /// neither works nor dies.
    /// </summary>
    /// <remarks>
    /// The decision it guards is made BEFORE <c>Handled</c> is set, which is why it lives in the handler and not
    /// in the sink: <c>Handled</c> is set-once-true, so a de-duplicating sink would recognise the repeat only
    /// after the fault had already been swallowed and could no longer escalate it.
    /// </remarks>
    [AvaloniaTest]
    public void TheBreakerEscalatesTheNthRepeatOfOneFault()
    {
        var logger = new CapturingLogger();
        DispatcherUnhandledExceptionEventHandler handler =
            AppTelemetryBootstrap.DispatcherExceptionHandler(logger);
        Dispatcher.UIThread.UnhandledException += handler;
        try
        {
            void Raise() => Dispatcher.UIThread.Post(() => throw new InvalidOperationException("repeating-42"));

            Assert.Multiple(() =>
            {
                for (int survived = 1; survived < AppTelemetryBootstrap.BreakerLimit; survived++)
                {
                    Raise();
                    Assert.That(() => Dispatcher.UIThread.RunJobs(), Throws.Nothing,
                        $"occurrence {survived} is under the limit and is survived");
                }

                Raise();
                Assert.That(() => Dispatcher.UIThread.RunJobs(),
                    Throws.Exception.Message.Contains("repeating-42"),
                    "the breaker is OPEN: the framework escalates exactly as it did before this handler set the "
                    + "flag at all");
            });
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= handler;
        }
    }

    /// <summary>
    /// The budget is PER FAULT IDENTITY, so one site exhausting it cannot deny another its first survival.
    /// </summary>
    [Test]
    public void TheFaultIdentityIsTheTypeAndWhereItWasRaised()
    {
        static Exception Thrown(Action act)
        {
            try
            {
                act();
                throw new InvalidOperationException("the probe did not throw");
            }
            catch (Exception caught)
            {
                return caught;
            }
        }

        static void SiteA() => throw new InvalidOperationException("x");
        static void SiteB() => throw new InvalidOperationException("x");

        Exception a1 = Thrown(SiteA);
        Exception a2 = Thrown(SiteA);
        Exception b = Thrown(SiteB);

        Assert.Multiple(() =>
        {
            Assert.That(AppTelemetryBootstrap.FaultIdentity(a1),
                Is.EqualTo(AppTelemetryBootstrap.FaultIdentity(a2)),
                "the same thing going wrong again is the same identity");
            Assert.That(AppTelemetryBootstrap.FaultIdentity(b),
                Is.Not.EqualTo(AppTelemetryBootstrap.FaultIdentity(a1)),
                "the same TYPE from a different site is not — sharing a budget would let one site's history "
                + "deny another its first survival");
            Assert.That(AppTelemetryBootstrap.FaultIdentity(new InvalidOperationException("never thrown")),
                Is.EqualTo(typeof(InvalidOperationException).FullName),
                "an exception with no stack falls back to its type, which is all there is to go on");
        });
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

        TelemetryBootstrap.LogUnobservedTaskException(logger, args);

        Assert.Multiple(() =>
        {
            Assert.That(logger.Messages, Has.Some.Contains("unobserved-boom-42"),
                "the dropped task's fault reaches the ILogger pipeline");
            Assert.That(args.Observed, Is.True,
                "and is marked observed, so it never re-surfaces as a process-killing finalizer-thread throw");
        });
    }

    /// <summary>
    /// This application composes a DURABLE ROW around the shared handler, and must not lose either half the
    /// shared one provides: the log line, and the <c>SetObserved()</c> that stops a dropped task taking the
    /// process down later from the finalizer thread.
    /// </summary>
    /// <remarks>
    /// All three asserted together on purpose. The composition is the kind of change that quietly drops one of
    /// the wrapped behaviours — the wrapper looks right, the wrapped call is missing, and nothing says so until
    /// a process dies months later.
    /// </remarks>
    [Test]
    public void TheComposedUnobservedTaskHandlerLogsObservesAndLeavesARow()
    {
        using SupervisedFaults faults = SupervisedFaults.Capture();
        try
        {
            var logger = new CapturingLogger();
            var args = new UnobservedTaskExceptionEventArgs(
                new AggregateException(new InvalidOperationException("unobserved-boom-43")));

            ihc_openvisual.Program.UnobservedTaskHandler(logger)(null, args);

            Assert.Multiple(() =>
            {
                Assert.That(logger.Messages, Has.Some.Contains("unobserved-boom-43"),
                    "the shared handler's log line survives the composition");
                Assert.That(args.Observed, Is.True,
                    "and so does its SetObserved() — without it a dropped task kills the process later");
                Ihc.Vis.Problems.InternalError row = faults.Rows.Single();
                Assert.That(row.Origin, Is.EqualTo(Ihc.Vis.Problems.InternalErrorOrigin.Host));
                Assert.That(row.Detail, Does.Contain("TaskScheduler.UnobservedTaskException"),
                    "named by the layer that discovered it");
                Assert.That(row.Detail, Does.Contain("unobserved-boom-43"));
                Assert.That(row.Detail, Does.Not.Contain("AggregateException"),
                    "the LONE inner fault, not its wrapper: the wrapper hides the type a reader wants");
            });
        }
        finally
        {
        }
    }

    /// <summary>
    /// A CANCELLATION on the dispatcher is handled and recorded nowhere. The application cancels routinely — the
    /// validation worker swaps its token source on every generation — and a cancellation that escapes on the UI
    /// thread, through an async void handler or a dispatcher continuation, has no boundary to tell it apart from
    /// a fault. Reporting it would fill the panel with rows produced by the debounce working as designed.
    /// </summary>
    /// <remarks>
    /// Driven through the REAL dispatcher, like the fault case above: DispatcherUnhandledExceptionEventArgs has
    /// no public constructor, and a hand-built stand-in would be testing a shape rather than the wiring.
    /// <para>This arm lands BEFORE the handler starts marking anything else handled, and the order is
    /// deliberate: turning Handled on first and adding the cancellation arm afterwards is precisely the
    /// configuration that fills the sink with non-faults.</para>
    /// </remarks>
    [AvaloniaTest]
    public void ADispatcherCancellationIsHandledAndRecordsNothing()
    {
        using SupervisedFaults faults = SupervisedFaults.Capture();
        var logger = new CapturingLogger();
        DispatcherUnhandledExceptionEventHandler handler =
            AppTelemetryBootstrap.DispatcherExceptionHandler(logger);
        Dispatcher.UIThread.UnhandledException += handler;
        try
        {
            Dispatcher.UIThread.Post(() => throw new TaskCanceledException("a debounced pass was abandoned"));

            Assert.Multiple(() =>
            {
                Assert.That(() => Dispatcher.UIThread.RunJobs(), Throws.Nothing,
                    "the framework must not escalate a cancellation the app performs on purpose");
                Assert.That(faults.Rows, Is.Empty, "and no row: nothing failed");
                Assert.That(logger.Messages, Is.Empty,
                    "not even a Critical log line — a cancelled operation is not an error to read about");
            });
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= handler;
        }
    }

    /// <summary>
    /// TaskCanceledException is the framework's own; OperationCanceledException is the one the worker raises.
    /// Both must be recognised, and an aggregate of nothing but cancellations too — a continuation can present
    /// one that way. A MIXED aggregate is not a cancellation: something in it genuinely failed.
    /// </summary>
    [Test]
    public void CancellationIsRecognisedThroughItsUsualDisguises()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AppTelemetryBootstrap.IsCancellation(new OperationCanceledException()), Is.True);
            Assert.That(AppTelemetryBootstrap.IsCancellation(new TaskCanceledException()), Is.True,
                "derives from the wider type");
            Assert.That(AppTelemetryBootstrap.IsCancellation(
                new AggregateException(new TaskCanceledException())), Is.True);
            Assert.That(AppTelemetryBootstrap.IsCancellation(
                new AggregateException(new TaskCanceledException(), new InvalidOperationException("real"))),
                Is.False, "something in it genuinely failed");
            Assert.That(AppTelemetryBootstrap.IsCancellation(new InvalidOperationException("real")), Is.False);
            Assert.That(AppTelemetryBootstrap.IsCancellation(new AggregateException()), Is.False,
                "an EMPTY aggregate asserts nothing about cancellation");
        });
    }

    /// <summary>
    /// The GLib boundary leaves a durable row too, with PLATFORM origin — and its sentence must not claim the
    /// app recovered. Avalonia discards this exception either way, so the row is a RECORD and never a recovery.
    /// </summary>
    /// <remarks>
    /// The wording is asserted, not just the code, because the wording IS the requirement. The shell's own
    /// catch-all sentence — "Handlingen kunne ikke gennemføres" — would have been free to reuse here and would
    /// have told the reader two things nobody knows: which action failed, and that the rest is fine.
    /// </remarks>
    [Test]
    public void TheGLibBoundaryLeavesAPlatformRowThatClaimsNoRecovery()
    {
        using SupervisedFaults faults = SupervisedFaults.Capture();
        try
        {
            var options = ihc_openvisual.Program.CreateX11Options(new CapturingLoggerFactory());

            options.ExternalGLibMainLoopExceptionLogger!(new InvalidOperationException("glib-boom-42"));

            Ihc.Vis.Problems.InternalError row = faults.Rows.Single();
            Assert.Multiple(() =>
            {
                Assert.That(row.Origin, Is.EqualTo(Ihc.Vis.Problems.InternalErrorOrigin.Platform),
                    "not Host and not Sdk: what failed is neither ours nor the engine's");
                Assert.That(row.Code.Value, Is.EqualTo("app.openvisual.platform-fault"));
                Assert.That(row.Message,
                    Is.EqualTo(ihc_openvisual.Services.HostProblems.PlatformFault().Message),
                    "rendered WHOLE from the catalogue");
                Assert.That(row.Message, Is.Not.EqualTo(
                    ihc_openvisual.Services.HostProblems.Unexpected(new InvalidOperationException("x")).Message),
                    "and NOT the shell's catch-all sentence, which would claim one action failed and the rest "
                    + "of the app is fine — neither of which anyone knows here");
                Assert.That(row.Detail, Does.Contain("glib-boom-42"));
                Assert.That(row.Detail, Does.Contain("GLib"), "named by the boundary that discarded it");
            });
        }
        finally
        {
        }
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
