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

        IConfigurationRoot config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("ihcsettings.json", optional: true)
            .Build();

        LoggingConfig = config.GetSection("Logging");
        TelemetryConfig = config.GetSection(TelemetryConfiguration.Key).Get<TelemetryConfiguration>()
                          ?? new TelemetryConfiguration();

        IhcSettings settings;
        try
        {
            settings = SettingsFileFound ? IhcSettings.GetFromConfiguration(config) : new IhcSettings();
        }
        catch (Exception)
        {
            // A malformed or partial ihcclient section must not prevent the editor from starting.
            settings = new IhcSettings();
        }

        // Continuing on the captured (UI) context can deadlock the GUI; force it off as the utilities do.
        IhcSettings = settings with { AsyncContinueOnCapturedContext = false };
    }
}
