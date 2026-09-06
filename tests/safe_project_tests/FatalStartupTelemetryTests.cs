using System;
using System.Diagnostics;
using System.Linq;
using ihc_openvisual;
using ihc_openvisual.Configuration;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The launch is one operation and it owns one span, opened at the top of the composition root and closed by the
/// window's <c>Opened</c> handler once there is a document to look at. A start-up that FAILS never reaches that
/// handler — so the launch an operator most needs to find was the only launch that exported no span at all.
///
/// <para>Measured, not argued: a fault injected into the composition root produced a Critical log record in the
/// collector and <b>zero</b> spans for the whole run. The log record carries the trace id, so the two are
/// joinable — but only for someone who already knows to leave the trace view and go looking, which is the
/// opposite of what a trace tree is for.</para>
///
/// <para>Closed HERE, in the reporter, rather than at the composition root's catch: this is the one place every
/// fatal start-up passes through, wherever in <c>Main</c> it was raised, and it is the member a test can reach —
/// the same reason the rest of the report's decisions live in it.</para>
/// </summary>
[TestFixture]
public class FatalStartupTelemetryTests
{
    private const string ScratchPrefix = "ihc_ov_fatalspan_";

    private const string LaunchSpan = "App.Startup";

    /// <summary>
    /// Reports <paramref name="failure"/> against a launch the caller shapes, and hands back the exit code and
    /// the launch's span. The capture, the probe and the throwaway log path are the same in every case here;
    /// what differs is only what state the launch was left in.
    /// </summary>
    /// <param name="failure">The fault to report.</param>
    /// <param name="prepare">What happens to the launch before the report — closing it, or nothing.</param>
    private static (int ExitCode, Activity Span) ReportAgainstALaunch(
        Exception failure, Action<Ihc.OperationScope>? prepare = null)
    {
        using ScratchDir dir = new(ScratchPrefix);
        using TelemetryCapture spans = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanNames: [LaunchSpan]);
        using TraceProbe probe = TraceProbe.Start();

        Ihc.OperationTelemetry telemetry = new(AppTelemetryRegistry.Surface, "App");
        Ihc.OperationScope launch = telemetry.Start("Startup");
        prepare?.Invoke(launch);

        int exitCode = Program.ReportFatalStartup(
            failure, new CapturingLoggerFactory(), dir.File("startup-error.log"), launch);
        return (exitCode, probe.Span(spans, LaunchSpan));
    }

    [Test]
    public void AFatalStartup_ClosesTheLaunchSpanAsAFailure()
    {
        (int exitCode, Activity span) =
            ReportAgainstALaunch(new InvalidOperationException("the object graph could not be built"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(Program.FatalStartupExitCode));
            Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error),
                "a launch that died has to be findable in the trace, not only in the log stream");
            Assert.That(span.GetTagItem("ihc.operation.status"), Is.EqualTo("failed"));
            Assert.That(span.Events.Any(e => e.Name == "exception"), Is.True,
                "and it has to say WHAT killed it");
        });
    }

    /// <summary>
    /// A launch that had already closed keeps the span it exported. <c>App.LaunchScope</c> stays set after a
    /// normal launch, so a fault arriving later reaches the reporter with a scope that is finished — and must
    /// not re-time, re-report or reopen what a successful launch already recorded.
    /// </summary>
    [Test]
    public void AnAlreadyClosedLaunch_IsNotReopenedOrRestated()
    {
        (_, Activity span) = ReportAgainstALaunch(
            new InvalidOperationException("something died long after start-up"),
            launch => launch.Dispose());   // the window opened; the launch succeeded and exported its span

        Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Unset),
            "a launch that already reported success must not be rewritten as the failure that came after it");
    }
}
