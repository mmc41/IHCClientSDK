using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Configuration;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// What the Danish settings readout tells an installer about telemetry.
///
/// The readout is the only place a user can see which endpoints this installation exports to, so an
/// endpoint that is configured but not listed reads as "telemetry is off" when it is not. These tests
/// drive the REAL configuration parse from a written ihcsettings.json rather than fabricating a
/// TelemetryConfiguration, so they prove the key is actually read as well as displayed.
/// </summary>
[TestFixture]
public class SettingsReadoutTests
{
    private static AppConfiguration ConfigWith(string telemetrySection, string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ihcsettings.json"),
            $$"""{ "telemetry": {{{telemetrySection}}} }""");
        return new AppConfiguration(dir);
    }

    private static async Task<string> ReadoutAsync(ShellHarness harness, AppConfiguration config)
    {
        MainWindowViewModel vm = harness.CreateViewModel(config: config);
        await vm.ShowSettingsCommand.ExecuteAsync(null);
        return harness.Dialogs.LastSettingsText!;
    }

    [Test]
    public async Task Readout_ListsTheConfiguredMetricsEndpointBesideLogsAndTraces()
    {
        using ShellHarness harness = ShellHarness.Create();
        AppConfiguration config = ConfigWith("""
              "Logs": "http://collector.local/v1/logs",
              "Traces": "http://collector.local/v1/traces",
              "Metrics": "http://collector.local/v1/metrics"
            """, harness.TempPath("cfg"));

        string readout = await ReadoutAsync(harness, config);

        Assert.Multiple(() =>
        {
            Assert.That(config.TelemetryConfig.Metrics, Is.EqualTo("http://collector.local/v1/metrics"),
                "the key must be READ from the settings file, not merely exist on the type");
            Assert.That(readout, Does.Contain("Metrikker: http://collector.local/v1/metrics"));
            // The neighbours must survive: this line is added beside them, not in place of one.
            Assert.That(readout, Does.Contain("Log: http://collector.local/v1/logs"));
            Assert.That(readout, Does.Contain("Spor: http://collector.local/v1/traces"));
        });
    }

    /// <summary>
    /// Empty means disabled, exactly as for Traces and Logs - and the readout says so in Danish rather
    /// than showing a blank, which would be indistinguishable from a rendering fault.
    /// </summary>
    [Test]
    public async Task Readout_WithNoMetricsEndpoint_SaysNotConfigured()
    {
        using ShellHarness harness = ShellHarness.Create();
        AppConfiguration config = ConfigWith("""
              "Traces": "http://collector.local/v1/traces"
            """, harness.TempPath("cfg"));

        string readout = await ReadoutAsync(harness, config);

        Assert.Multiple(() =>
        {
            Assert.That(config.TelemetryConfig.Metrics, Is.Empty);
            Assert.That(readout, Does.Contain("Metrikker: (ikke angivet)"));
        });
    }
}
