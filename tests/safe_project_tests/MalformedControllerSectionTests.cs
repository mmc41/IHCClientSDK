using System.IO;
using System.Linq;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The controller section falls back independently of the rest of the configuration, and silently — on the
/// stated ground that a file-only editor's settings file carrying only a telemetry section is an ORDINARY
/// configuration, and that a malformed section "arrives as the same exception from the same call", so the two
/// could not be told apart.
///
/// <para>They can. Whether the section is THERE is a different question from whether reading it worked, and the
/// configuration root answers the first one directly. Measured with faults injected across the whole
/// settings/encryption chain — five sites, from the section read down to the base64 decode of an encrypted
/// password — every one of them produced an editor that started blank with no record of any kind. An installer
/// whose encrypted password is one character short gets exactly what an installer with no controller gets.</para>
/// </summary>
[TestFixture]
public class MalformedControllerSectionTests
{
    private const string Prefix = "ihc_ov_cfg_";

    /// <summary>The environment variable the secret reads, named by the SDK type that reads it rather than
    /// copied — cleared and restored so this fixture is unaffected by whatever else in the process set it.</summary>
    private const string Passphrase = Ihc.SimpleSecret.EncryptPassphaseEnvName;

    private static string WriteSettings(ScratchDir dir, string json)
    {
        File.WriteAllText(Path.Combine(dir.Path, "ihcsettings.json"), json);
        return dir.Path;
    }

    [Test]
    public void AMalformedControllerSection_IsReported()
    {
        using CapturedFaults faults = new();
        using ScratchDir dir = new(Prefix);
        // A present-but-unusable ihcclient section: encryption is switched on and the passphrase the secret
        // needs is not in the environment — the shape a real machine takes when the variable is not deployed.
        string basePath = WriteSettings(dir, """
            {
              "ihcclient": { "Endpoint": "http://192.168.1.3", "UserName": "u", "Password": "x" },
              "encryption": { "IsEncrypted": true },
              "telemetry": { "Host": "http://127.0.0.1:5080" }
            }
            """);

        // The variable is process-wide, so everything between clearing and restoring it belongs in the try:
        // a throw in between would hand the rest of the run an environment this fixture emptied.
        ihc_openvisual.Configuration.AppConfiguration config;
        string? passphrase = System.Environment.GetEnvironmentVariable(Passphrase);
        try
        {
            System.Environment.SetEnvironmentVariable(Passphrase, null);
            config = new ihc_openvisual.Configuration.AppConfiguration(basePath);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(Passphrase, passphrase);
        }

        Assert.Multiple(() =>
        {
            Assert.That(config.TelemetryConfig.Host, Is.EqualTo("http://127.0.0.1:5080"),
                "the telemetry section still reads — the two sections fall back independently");
            Assert.That(faults.Rows, Is.Not.Empty,
                "an editor running unconfigured because its controller section could not be read is not the "
                + "same as one nobody configured, and the difference has to be recorded somewhere");
        });
    }

    [Test]
    public void NoControllerSectionAtAll_IsOrdinaryAndSaysNothing()
    {
        using CapturedFaults faults = new();
        using ScratchDir dir = new(Prefix);
        string basePath = WriteSettings(dir, """
            { "telemetry": { "Host": "http://127.0.0.1:5080" } }
            """);

        ihc_openvisual.Configuration.AppConfiguration config = new(basePath);

        Assert.Multiple(() =>
        {
            Assert.That(config.TelemetryConfig.Host, Is.EqualTo("http://127.0.0.1:5080"));
            Assert.That(faults.Rows, Is.Empty,
                "a file-only editor configured for telemetry and no controller is the ORDINARY case, and must "
                + "not put an internal-error row in front of the installer who chose it");
        });
    }

    /// <summary>
    /// The one shape the presence test cannot see: <c>"ihcclient": {}</c> is IN the file, and
    /// <c>IConfigurationSection.Exists()</c> is false for it, because a section with no value and no children is
    /// indistinguishable from an absent one to the configuration root.
    ///
    /// <para>Silence is the right answer there, and this pins WHY rather than merely asserting it: an empty
    /// section binds to null, so the read fails at the same "could not read IHC client settings" as an absent one
    /// and never reaches the encryption chain — there is nothing in it to decrypt. An empty section carries no
    /// controller, which is exactly what the installer who wrote it configured.</para>
    /// </summary>
    [Test]
    public void AnEmptyControllerSection_IsOrdinaryAndSaysNothing()
    {
        using CapturedFaults faults = new();
        using ScratchDir dir = new(Prefix);
        string basePath = WriteSettings(dir, """
            {
              "ihcclient": {},
              "encryption": { "IsEncrypted": true },
              "telemetry": { "Host": "http://127.0.0.1:5080" }
            }
            """);

        ihc_openvisual.Configuration.AppConfiguration config = new(basePath);

        Assert.Multiple(() =>
        {
            Assert.That(config.IhcSettings.IsValid(), Is.False,
                "non-vacuity: the fallback settings really were taken, so this is the silent path");
            Assert.That(config.TelemetryConfig.Host, Is.EqualTo("http://127.0.0.1:5080"));
            Assert.That(faults.Rows, Is.Empty,
                "an empty section is a configuration, not a failure to read one");
        });
    }

    [Test]
    public void NoSettingsFileAtAll_IsOrdinaryAndSaysNothing()
    {
        using CapturedFaults faults = new();
        using ScratchDir dir = new(Prefix);

        _ = new ihc_openvisual.Configuration.AppConfiguration(dir.Path);

        Assert.That(faults.Rows, Is.Empty);
    }
}
