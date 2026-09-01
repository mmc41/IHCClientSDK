using System.IO;
using ihc_openvisual.Configuration;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The editor must start with no usable settings at all. Configuration is read before the logger factory exists,
/// so anything thrown here escapes to <c>Main</c>'s last-resort catch with nothing wired to write it — and from a
/// windowed executable with no console, the process ends with no window, no message and no log record. A file
/// editor that will not open because a settings file is malformed is the worst shape that failure can take.
/// <para>
/// The class already promised this in a comment. It delivered it only for the <c>ihcclient</c> section: the JSON
/// parse itself, and the telemetry binding, sat outside the guard.
/// </para>
/// </summary>
[TestFixture]
public class AppConfigurationFallbackTests
{
    /// <summary>A scratch directory holding one settings file with the given content.</summary>
    private static ScratchDir DirectoryWith(string settingsFileContent)
    {
        ScratchDir dir = new("ihc_ov_cfg_");
        File.WriteAllText(dir.File("ihcsettings.json"), settingsFileContent);
        return dir;
    }

    /// <summary>Reproduce-first: a file the JSON parser rejects threw out of the configuration builder, before
    /// anything existed to report it.</summary>
    [Test]
    public void AMalformedSettingsFile_YieldsDefaults_RatherThanThrowing()
    {
        using ScratchDir dir = DirectoryWith("{ this is not json");

        AppConfiguration? config = null;
        Assert.DoesNotThrow(() => config = new AppConfiguration(dir.Path),
            "a malformed settings file must not stop the editor from starting");
        Assert.Multiple(() =>
        {
            Assert.That(config!.SettingsFileFound, Is.True, "the file IS there — it is its content that is unusable");
            Assert.That(config.TelemetryConfig.Logs, Is.Null.Or.Empty, "telemetry falls back to off");
            Assert.That(config.IhcSettings.Endpoint, Is.Null.Or.Empty, "and the controller settings fall back to blank");
        });
    }

    /// <summary>A file that parses but whose telemetry section is not the shape the binder expects. This one was
    /// ALREADY tolerated -- measured, not assumed: the binder returns null for a scalar where it wanted an object,
    /// so nothing was ever thrown here. It is pinned because the guard now covers the statement that reads it, and
    /// a future binder that starts throwing would otherwise take the editor down with it.</summary>
    [Test]
    public void AWrongShapedTelemetrySection_YieldsDefaults_RatherThanThrowing()
    {
        using ScratchDir dir = DirectoryWith("""{ "telemetry": "off" }""");

        AppConfiguration? config = null;
        Assert.DoesNotThrow(() => config = new AppConfiguration(dir.Path),
            "a settings file whose telemetry section is not an object must not stop the editor from starting");
        Assert.That(config!.TelemetryConfig.Logs, Is.Null.Or.Empty);
    }

    /// <summary>The control: a usable file is still read. Without this the fix could pass by discarding every
    /// settings file, malformed or not.</summary>
    [Test]
    public void AUsableSettingsFile_IsStillRead()
    {
        using ScratchDir dir = DirectoryWith("""{ "telemetry": { "Logs": "http://collector.local/v1/logs" } }""");

        var config = new AppConfiguration(dir.Path);

        Assert.Multiple(() =>
        {
            Assert.That(config.SettingsFileFound, Is.True);
            Assert.That(config.TelemetryConfig.Logs, Is.EqualTo("http://collector.local/v1/logs"),
                "the guard must not swallow a file that is perfectly good");
        });
    }

    /// <summary>
    /// Degrading is right; degrading SILENTLY is not. An app running with telemetry off and blank controller
    /// settings because its configuration could not be read looks exactly like one nobody configured, and until
    /// now nothing anywhere recorded which of the two had happened.
    /// </summary>
    /// <remarks>
    /// The class justified its silence with "there is nothing to report to yet … Program's last-resort line is
    /// the channel for it". Both halves are false: this guard is inside the constructor, so <c>Main</c>'s catch
    /// never sees it, and <see cref="ihc_openvisual.Services.TaskSupervisor"/> was since built to BUFFER faults
    /// raised before the composition root attaches — which is precisely this moment.
    /// </remarks>
    [Test]
    public void AMalformedSettingsFile_IsRecordedAsAnInternalFault()
    {
        using ScratchDir dir = DirectoryWith("{ this is not json");
        using CapturedFaults captured = new();

        AppConfiguration config = new(dir.Path);

        Assert.Multiple(() =>
        {
            Assert.That(config.TelemetryConfig.Logs, Is.Null.Or.Empty, "precondition: it still degraded");
            Assert.That(captured.Rows, Is.Not.Empty, "and said so somewhere a person can reach");
            Assert.That(captured.Rows[0].Origin, Is.EqualTo(Ihc.Vis.Problems.InternalErrorOrigin.Host));
        });
    }

    /// <summary>A file that reads perfectly well reports nothing — the record is for failures, not for start-up.</summary>
    [Test]
    public void AUsableSettingsFile_RecordsNoFault()
    {
        using ScratchDir dir = DirectoryWith("""{ "telemetry": { "Logs": "http://collector.local/v1/logs" } }""");
        using CapturedFaults captured = new();

        _ = new AppConfiguration(dir.Path);

        Assert.That(captured.Rows, Is.Empty);
    }

    /// <summary>No file at all is the ordinary case for this editor, not a failure.</summary>
    [Test]
    public void NoSettingsFile_YieldsDefaults()
    {
        using ScratchDir dir = new("ihc_ov_cfg_");

        var config = new AppConfiguration(dir.Path);

        Assert.Multiple(() =>
        {
            Assert.That(config.SettingsFileFound, Is.False);
            Assert.That(config.TelemetryConfig.Logs, Is.Null.Or.Empty);
        });
    }
}
