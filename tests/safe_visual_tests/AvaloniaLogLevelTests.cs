using System.Collections.Generic;
using Avalonia;
using Avalonia.Logging;
using Ihc.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// <c>LogLevel:Avalonia</c> governs what Avalonia's own logs cost — and it has to govern the destination that
/// actually costs something. The forwarding sink is the one that reaches <see cref="ILogger"/>, and so the console
/// and the telemetry backend; the trace logger beside it writes to <see cref="System.Diagnostics.Trace"/>, which
/// nothing in a normal run reads.
/// </summary>
/// <remarks>
/// The regression these pin is a whole run's worth of noise, not a wrong number. The sink's own default is
/// <see cref="LogEventLevel.Verbose"/> — deliberately no floor, so an ILogger filter can be the single gate — and
/// it logs under its own category rather than an <c>Avalonia.*</c> one, so the <c>LogLevel:Avalonia</c> entry
/// cannot reach it as a filter either. Handed no level it therefore forwarded EVERY layout pass Avalonia reports,
/// at Information, into the log pipeline: hundreds of lines per run on the console and the same volume shipped to
/// OpenTelemetry, while the configured level silently applied only to the trace logger.
/// <para><see cref="CapturingLoggerFactory"/> is enabled at every level by construction, which is what leaves the
/// sink's own floor as the only gate these assert on — an ILogger-side filter would hide the defect again.</para>
/// </remarks>
public class AvaloniaLogLevelTests
{
    /// <summary>Avalonia's <c>Layout</c> area reports each layout pass at this level; it is the volume the
    /// configured floor exists to keep out.</summary>
    private const LogEventLevel LayoutPass = LogEventLevel.Information;

    private static IConfiguration LoggingConfig(string? avaloniaLevel) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            avaloniaLevel is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["LogLevel:Avalonia"] = avaloniaLevel })
            .Build();

    /// <summary>Installs the logging Program installs, and hands back the sink it left behind. The previous sink
    /// is restored by the caller: <c>Logger.Sink</c> is process-global and this suite hosts a real app.</summary>
    private static ILogSink? InstallSink(string? avaloniaLevel, CapturingLoggerFactory logs)
    {
        _ = ihc_openvisual.Program.WithAvaloniaLogging(
            AppBuilder.Configure<ihc_openvisual.App>(), LoggingConfig(avaloniaLevel), logs);
        return Logger.Sink;
    }

    [TestCase("Warning", false, TestName = "AvaloniaLogging_AtWarning_DropsLayoutPasses")]
    [TestCase(null, false, TestName = "AvaloniaLogging_Unconfigured_DropsLayoutPasses")]
    [TestCase("Trace", true, TestName = "AvaloniaLogging_AtTrace_ForwardsLayoutPasses")]
    public void AvaloniaLogging_ForwardsLayoutPasses_OnlyWhenTheLevelAsksFor(string? configured, bool expected)
    {
        ILogSink? previous = Logger.Sink;
        using CapturingLoggerFactory logs = new();
        try
        {
            ILogSink? sink = InstallSink(configured, logs);

            Assert.That(sink, Is.Not.Null, "the forwarding sink was not installed");
            Assert.That(sink!.IsEnabled(LayoutPass, "Layout"), Is.EqualTo(expected),
                $"LogLevel:Avalonia={configured ?? "(unset)"} must decide whether layout passes reach ILogger");
        }
        finally
        {
            Logger.Sink = previous;
        }
    }

    /// <summary>The floor is a floor, not a mute: a warning from Avalonia still reaches the pipeline at the
    /// quietest configured level. Without this, "drops layout passes" would also be satisfied by a sink that
    /// forwarded nothing at all.</summary>
    [Test]
    public void AvaloniaLogging_AtWarning_StillForwardsWarnings()
    {
        ILogSink? previous = Logger.Sink;
        using CapturingLoggerFactory logs = new();
        try
        {
            ILogSink? sink = InstallSink("Warning", logs);

            Assert.That(sink!.IsEnabled(LogEventLevel.Warning, "Layout"), Is.True);

            sink.Log(LogEventLevel.Warning, "Layout", this, "a real Avalonia warning");
            Assert.That(logs.Messages, Has.Some.Contains("a real Avalonia warning"),
                "a forwarded warning must reach the ILogger pipeline, and so OpenTelemetry");
        }
        finally
        {
            Logger.Sink = previous;
        }
    }
}
