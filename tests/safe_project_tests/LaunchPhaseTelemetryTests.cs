using System;
using System.Diagnostics;
using System.Linq;
using ihc_openvisual;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The launch phases that run BEFORE the telemetry pipeline exists — the .NET host, the multicore-JIT
/// accelerator, configuration, and the construction of the pipeline itself — together outweigh the span that
/// used to be the only record of a launch. They cannot be recorded while they happen: an <c>ActivitySource</c>
/// with no listener hands back null, so until the pipeline is up there is nothing to open a span on.
///
/// <para>What is pinned HERE is what <see cref="Program"/> decides: which phases exist, that their boundaries
/// partition one stretch of wall clock without a gap, and that one unusable pair costs only its own phase.
/// That a backdated span carries the duration it was handed, and stays a root even under an ambient activity,
/// are properties of the core's own door — pinned on it in <c>OperationTelemetryTests</c>, because this
/// fixture can only restate them one call further away and would move whenever that door did.</para>
/// </summary>
[TestFixture]
public class LaunchPhaseTelemetryTests
{
    private const string HostPhase = "Launch.Host";
    private const string JitPhase = "Launch.Jit";
    private const string ConfigPhase = "Launch.Config";
    private const string TelemetryPhase = "Launch.Telemetry";

    private static readonly string[] AllPhases = [HostPhase, JitPhase, ConfigPhase, TelemetryPhase];

    private static readonly TimeSpan JitDuration = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan ConfigDuration = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// This process's start, read once for the fixture. The member under test reads the same clock for itself,
    /// so every expectation here has to be anchored to it rather than counted back from now.
    /// </summary>
    private static readonly DateTime ProcessStart = ReadProcessStart();

    private static DateTime ReadProcessStart()
    {
        using Process self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime();
    }

    /// <summary>What one recorded launch left behind, and the times it was told to record.</summary>
    /// <param name="Spans">The captured phases.</param>
    /// <param name="Log">What the member reported about phases it could not record.</param>
    /// <param name="Entered">The <c>Main</c>-entered stamp handed to the member.</param>
    /// <param name="Accelerated">The multicore-JIT-started stamp handed to the member.</param>
    /// <param name="Configured">The configuration-built stamp handed to the member.</param>
    /// <param name="Before">Read immediately before the call, bounding the stamp the member takes itself.</param>
    /// <param name="After">Read immediately after it, bounding the same stamp from above.</param>
    private sealed record Recorded(TelemetryCapture Spans, CapturingLogger Log, DateTime Entered,
                                   DateTime Accelerated, DateTime Configured, DateTime Before, DateTime After);

    /// <summary>
    /// Records a launch whose phases took known amounts of time, and hands back what was exported alongside
    /// the timestamps it was given.
    /// </summary>
    /// <remarks>
    /// Anchoring at <see cref="ProcessStart"/> is not fussiness: the member reads <c>Process.StartTime</c> for
    /// itself, so a <c>UtcNow - 30s</c> would put the launch's first phase before the process existed whenever
    /// the test host happens to be younger than that. The pair is then reversed, which the core rejects — so
    /// the fixture would be asserting against a phase its own arithmetic had suppressed.
    /// </remarks>
    /// <param name="entered">Where <c>Main</c> is to be told it was entered. Defaults to just after this
    /// process started, which is the only ordering a real launch can produce.</param>
    private static Recorded RecordALaunch(DateTime? entered = null)
    {
        TelemetryCapture spans = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanNames: AllPhases);
        CapturingLogger log = new();

        DateTime enteredAt = entered ?? ProcessStart + TimeSpan.FromMilliseconds(1);
        DateTime accelerated = enteredAt + JitDuration;
        DateTime configured = accelerated + ConfigDuration;

        DateTime before = DateTime.UtcNow;
        Program.RecordPreTelemetryPhases(enteredAt, accelerated, configured, log);
        DateTime after = DateTime.UtcNow;

        return new Recorded(spans, log, enteredAt, accelerated, configured, before, after);
    }

    [Test]
    public void AllFourPhasesAreRecorded()
    {
        Recorded launch = RecordALaunch();
        using (launch.Spans)
        {
            Assert.That(launch.Spans.Spans.Select(s => s.OperationName), Is.EquivalentTo(AllPhases),
                "the four phases between process start and a usable pipeline, each named by the core's own "
                + "owner-prefix policy rather than by a literal");
        }
    }

    /// <summary>
    /// The four are a partition of one stretch of wall clock, so a reader can add them up and subtract the
    /// total from the launch span. A gap or an overlap would make that arithmetic quietly wrong — and because
    /// every boundary is asserted against the times the member was handed, so is each phase's duration.
    /// </summary>
    /// <remarks>
    /// The last boundary is the one time the member takes ITSELF, so it is pinned by bracketing the call
    /// rather than against a handed stamp. Without that the final phase could be recorded as ending where it
    /// began — a legal, zero-length span — and every other assertion here would still pass.
    /// </remarks>
    [Test]
    public void ThePhasesAreContiguous_FromProcessStartToAPipelineThatExists()
    {
        Recorded launch = RecordALaunch();
        using (launch.Spans)
        {
            Activity host = launch.Spans.Span(HostPhase);
            Activity jit = launch.Spans.Span(JitPhase);
            Activity config = launch.Spans.Span(ConfigPhase);
            Activity telemetry = launch.Spans.Span(TelemetryPhase);

            Assert.Multiple(() =>
            {
                Assert.That(host.StartTimeUtc, Is.EqualTo(ProcessStart).Within(Tolerance),
                    "the only record that the phase before Main happened is the OS's own process-start clock");
                Assert.That(host.StartTimeUtc + host.Duration, Is.EqualTo(launch.Entered).Within(Tolerance),
                    "no gap between the host phase and the accelerator");
                Assert.That(jit.StartTimeUtc, Is.EqualTo(launch.Entered).Within(Tolerance),
                    "the accelerator starts where Main was entered — each phase gets the times it was handed");
                Assert.That(jit.StartTimeUtc + jit.Duration, Is.EqualTo(launch.Accelerated).Within(Tolerance),
                    "no gap between the accelerator and configuration");
                Assert.That(config.StartTimeUtc, Is.EqualTo(launch.Accelerated).Within(Tolerance),
                    "configuration starts where the accelerator finished, and NOT where Main was entered — "
                    + "folding the profile directory's first touch into it is the misattribution this split "
                    + "exists to remove");
                Assert.That(config.StartTimeUtc + config.Duration, Is.EqualTo(launch.Configured).Within(Tolerance),
                    "no gap between configuration and the pipeline build");
                Assert.That(telemetry.StartTimeUtc, Is.EqualTo(launch.Configured).Within(Tolerance),
                    "the pipeline build starts where configuration finished");
                Assert.That(telemetry.StartTimeUtc + telemetry.Duration,
                    Is.InRange(launch.Before, launch.After),
                    "and ends when the member was told the pipeline was ready, which is the one boundary it "
                    + "reads for itself rather than being handed");
            });
        }
    }

    /// <summary>
    /// A phase the core refuses costs only itself. Only <c>Launch.Host</c> can be refused in production — it
    /// is the one phase whose two ends come from different clocks, and <c>DateTime</c>'s round trip through
    /// local time resolves the hour repeated at a daylight-saving fall back to standard time, which can date
    /// the process start an hour after <c>Main</c> was entered. Recorded under one <c>try</c>, that once-a-year
    /// refusal took the whole partition with it, including the phases that are stamped from a single clock and
    /// cannot be wrong.
    /// </summary>
    [Test]
    public void ARefusedPhaseCostsOnlyItself_AndIsReported()
    {
        // The same shape without waiting for October: Main entered before the process it runs in started.
        Recorded launch = RecordALaunch(entered: ProcessStart - TimeSpan.FromMilliseconds(1));
        using (launch.Spans)
        {
            Assert.Multiple(() =>
            {
                Assert.That(launch.Spans.Spans.Select(s => s.OperationName),
                    Is.EquivalentTo(new[] { JitPhase, ConfigPhase, TelemetryPhase }),
                    "the three phases stamped from one clock survive a host phase the core would not render");
                Assert.That(launch.Log.Messages, Has.Exactly(1).Contains("Host"),
                    "and the one that did not land says so, naming itself — a phase missing from the trace "
                    + "with nothing in the log is indistinguishable from a phase that never ran");
            });
        }
    }
}
