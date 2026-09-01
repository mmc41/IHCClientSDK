using System;
using System.IO;
using System.Reflection;
using Ihc;
using Microsoft.Extensions.Configuration;

namespace ihc_openvisual.Configuration;

/// <summary>
/// Application configuration loaded once at start-up. Unlike the utility apps, IHC OpenVisual is a
/// file editor that must start even with <b>no</b> <c>ihcsettings.json</c> present and with no telemetry
/// configured (US-063): a missing file, missing telemetry section or missing controller settings all fall
/// back to safe defaults (local logging only, blank <see cref="Ihc.IhcSettings"/>). Treated as immutable
/// after construction.
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>The <c>Logging</c> section (possibly empty) fed to the logger factory.</summary>
    public IConfigurationSection LoggingConfig { get; }

    /// <summary>Telemetry endpoints/headers; all-empty when unconfigured (telemetry then stays off).</summary>
    public TelemetryConfiguration TelemetryConfig { get; }

    /// <summary>IHC controller settings; blank when no file/section is present (file-only operation).</summary>
    public IhcSettings IhcSettings { get; }

    /// <summary>True when an <c>ihcsettings.json</c> was found and loaded next to the executable.</summary>
    public bool SettingsFileFound { get; }

    /// <summary>The absolute path probed for <c>ihcsettings.json</c> (whether or not it exists).</summary>
    public string SettingsFilePath { get; }

    public AppConfiguration()
        : this(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory)
    {
    }

    /// <summary>
    /// Loads from an explicit directory instead of the executable's own. Exists so a test can exercise the
    /// REAL parse of a written <c>ihcsettings.json</c> - asserting against a hand-built
    /// <see cref="TelemetryConfiguration"/> would prove the type has a property, not that the key is read.
    /// </summary>
    internal AppConfiguration(string basePath)
    {
        SettingsFilePath = Path.Combine(basePath, "ihcsettings.json");
        SettingsFileFound = File.Exists(SettingsFilePath);
        (LoggingConfig, TelemetryConfig, IhcSettings) = Read(basePath, SettingsFileFound);
    }

    /// <summary>
    /// The WHOLE read, guarded as one — the JSON parse included. A file the parser rejects throws
    /// <see cref="InvalidDataException"/> out of the configuration builder, which happens before the logger
    /// factory exists: the process would end with nothing written anywhere and, from a windowed executable with
    /// no console, no visible sign at all. A file editor that will not open because a settings file is malformed
    /// is the worst shape that failure can take.
    /// <para>
    /// A method rather than inline, so the fallback is reachable by a test without starting a process.
    /// </para>
    /// </summary>
    private static (IConfigurationSection Logging, TelemetryConfiguration Telemetry, IhcSettings Settings)
        Read(string basePath, bool settingsFileFound)
    {
        try
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("ihcsettings.json", optional: true)
                .Build();

            return (
                config.GetSection("Logging"),
                config.GetSection(TelemetryConfiguration.Key).Get<TelemetryConfiguration>()
                    ?? new TelemetryConfiguration(),
                Settled(ControllerSettings(config, settingsFileFound)));
        }
        catch (Exception ex)
        {
            // Every fallback the class documents, taken together: local logging only, telemetry off, blank
            // controller settings. Degrading is right; degrading SILENTLY is not — an editor running unconfigured
            // because its settings could not be read is indistinguishable from one nobody configured.
            //
            // Reported through the supervisor's port, which BUFFERS until the composition root attaches: this
            // runs inside the constructor, so Main's last-resort line never sees it, and the buffer exists for
            // exactly this moment. Not a dialog — the editor still starts, which is the whole point.
            Services.TaskSupervisor.Report(ex, $"{nameof(AppConfiguration)}.{nameof(Read)}");
            return (Defaults.GetSection("Logging"), new TelemetryConfiguration(), Settled(new IhcSettings()));
        }
    }

    /// <summary>
    /// The controller section keeps a guard OF ITS OWN, inside the whole-read guard above. The two fall back
    /// independently on purpose: the sections are independent, so a malformed <c>ihcclient</c> section must not
    /// also discard a telemetry section that read perfectly well. Folding them into one guard silently did
    /// exactly that.
    /// </summary>
    private static IhcSettings ControllerSettings(IConfigurationRoot config, bool settingsFileFound)
    {
        try
        {
            return settingsFileFound ? IhcSettings.GetFromConfiguration(config) : new IhcSettings();
        }
        catch (Exception)
        {
            // NOT reported as a fault, unlike the outer guard, and the difference is measured rather than
            // stylistic: this editor is file-only, so a settings file carrying just a telemetry section is an
            // ORDINARY configuration — and GetFromConfiguration throws on the absent ihcclient section every
            // time. Reporting here would put an internal-error row in front of every installer who configured
            // telemetry and no controller. A malformed section arrives as the same exception from the same call,
            // so the two cannot be told apart at this layer; the common one wins.
            return new IhcSettings();
        }
    }

    // Continuing on the captured (UI) context can deadlock the GUI; force it off as the utilities do.
    private static IhcSettings Settled(IhcSettings settings) =>
        settings with { AsyncContinueOnCapturedContext = false };

    // An empty root, so the fallback still hands out a real (empty) section rather than null.
    private static readonly IConfigurationRoot Defaults = new ConfigurationBuilder().Build();
}
