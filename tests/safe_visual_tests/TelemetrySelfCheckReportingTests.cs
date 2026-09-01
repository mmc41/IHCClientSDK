using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Vis.Problems;
using ihc_openvisual.Services;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The one finding that cannot rely on telemetry to be seen.
///
/// <para><b>Why it needs a surface of its own.</b> When the pipeline is down, every span, metric and log line
/// this run produces is dropped — so the mechanism that would normally report the problem is the mechanism that
/// failed. The SDK's own reporting path writes to <c>Trace.WriteLine</c> and <c>Console.Error</c>, which is the
/// right fallback for a host with no logging pipeline and useless in a <c>WinExe</c> that has neither a console
/// nor anyone reading trace output. This application takes the structured door and routes the RESULT itself.</para>
///
/// <para><b>Unreachable is produced for real, not stubbed.</b> The probe drives the real
/// <see cref="HttpClient"/> at a loopback address it cannot connect to, so the status comes from the SDK's own
/// <c>catch</c> over a genuine socket failure rather than from a test double that could agree with a contract
/// nobody checked.</para>
/// </summary>
[TestFixture]
public class TelemetrySelfCheckReportingTests
{
    /// <summary>
    /// A loopback address the socket layer cannot connect to, so <see cref="TelemetrySelfCheck.ProbeAsync"/>
    /// reaches its <see cref="TelemetrySelfCheckStatus.Unreachable"/> arm through a real failure.
    /// </summary>
    /// <remarks>
    /// PORT ZERO, and measured rather than chosen for looks. The obvious address is a closed port such as :1,
    /// which the OS refuses immediately — but a refusal is not the same as a fast return: Winsock retransmits
    /// before it hands the refusal back, and one such probe costs about two seconds on Windows whatever port or
    /// address family it names (:1, a free high port and [::1]:1 all measured the same, and a <c>localhost</c>
    /// host doubles it by trying both families). Port zero is not connectable at all, so the attempt fails
    /// in about a millisecond with the same exception type from the same handler — the code path under test is
    /// unchanged, only the waiting is gone.
    /// </remarks>
    private const string UnconnectableEndpoint = "http://127.0.0.1:0/";

    /// <summary>A configuration whose endpoint cannot be connected to; see <see cref="UnconnectableEndpoint"/>.</summary>
    private static TelemetryConfiguration Unreachable() => new()
    {
        SelfCheckEndpoint = UnconnectableEndpoint,
        SelfCheckExpectedStatus = "^2..$",
    };

    private static async Task<(List<InternalError> Rows, CapturingLoggerFactory Logs)> RunAsync(
        TelemetryConfiguration telemetry)
    {
        // The capture detaches with this method, so the rows are handed out as a list of their own.
        using SupervisedFaults faults = SupervisedFaults.Capture();
        CapturingLoggerFactory logs = new();
        await ihc_openvisual.Program.ReportSelfCheckAsync(
            logs.CreateLogger("Ihc.OpenVisual.TelemetrySelfCheck"), telemetry);
        return ([.. faults.Rows], logs);
    }

    /// <summary>The gate's assertion: an <c>Unreachable</c> result produces a row.</summary>
    [Test]
    public async Task AnUnreachableEndpointProducesARow()
    {
        var (rows, logs) = await RunAsync(Unreachable());

        InternalError row = rows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(row.Code.Value, Is.EqualTo("app.openvisual.telemetry-pipeline-down"));
            Assert.That(row.Message, Is.EqualTo(HostProblems.TelemetryPipelineDown("x").Message),
                "the Danish sentence comes from the catalogue and says the CONSEQUENCE");
            Assert.That(row.Origin, Is.EqualTo(InternalErrorOrigin.Host));
            Assert.That(row.Detail, Does.Contain(nameof(TelemetrySelfCheckStatus.Unreachable)),
                "the status the SDK actually returned, not a guess");
            Assert.That(logs.Messages, Has.Some.Contains("self-check"),
                "and the log line still happens — the row is an addition, not a replacement");
        });
    }

    /// <summary>
    /// The self-check's own English message travels as the DIAGNOSTIC, never as the sentence. It names an
    /// endpoint and an HTTP status: operator-facing detail, not something to put on a Danish screen.
    /// </summary>
    [Test]
    public async Task TheSelfChecksOwnMessageTravelsAsDiagnosticNotAsSentence()
    {
        var (rows, _) = await RunAsync(Unreachable());

        InternalError row = rows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(row.Diagnostic, Does.Contain("127.0.0.1"), "the endpoint is in the diagnostic");
            Assert.That(row.Message, Does.Not.Contain("127.0.0.1"),
                "and never in the Danish sentence the reader is shown");
        });
    }

    /// <summary>
    /// A self-check nobody configured is not a fault, and a working pipeline is not news. Both report nothing —
    /// otherwise every ordinary start-up would open with an internal row.
    /// </summary>
    [Test]
    public async Task ADisabledSelfCheckReportsNothing()
    {
        var (rows, logs) = await RunAsync(new TelemetryConfiguration());

        Assert.Multiple(() =>
        {
            Assert.That(rows, Is.Empty);
            Assert.That(logs.Messages, Is.Not.Empty, "it is still recorded, at information level");
        });
    }

    /// <summary>
    /// An endpoint set without its expected-status regex is a CONFIG error, and it is a problem too: the check
    /// cannot run, so nobody knows whether the pipeline works.
    /// </summary>
    [Test]
    public async Task AMisconfiguredSelfCheckAlsoProducesARow()
    {
        var (rows, _) = await RunAsync(new TelemetryConfiguration
        {
            SelfCheckEndpoint = UnconnectableEndpoint,
        });

        Assert.That(rows.Single().Detail,
            Does.Contain(nameof(TelemetrySelfCheckStatus.ConfigError)));
    }
}
