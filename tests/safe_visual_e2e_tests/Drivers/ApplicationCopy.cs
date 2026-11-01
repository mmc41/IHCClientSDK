using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ihc;
using Ihc.Tests;
using Microsoft.Extensions.Configuration;

namespace safe_visual_e2e_tests;

/// <summary>
/// The copy of OpenVisual the real driver launches (O8). From its own build output the application would read the
/// settings file its project stages there, which is the developer's: a real controller's endpoint and login, with
/// only the switch and the launch gate between a launch and that controller, and the gate looks only once the
/// window shows. The copy runs with a file this suite writes instead, naming an endpoint under the reserved
/// <c>.invalid</c> domain and no login, so a launch whose switch failed fails in transport rather than reaching a
/// controller.
/// </summary>
/// <remarks>
/// <para>The developer's file is still read, for its telemetry and logging sections alone, so a desktop run reports
/// to the backend it always did. Those values go into the copy and nowhere else, and nothing here quotes them.</para>
/// <para>One copy, kept between runs and refreshed by the first launch of each, because the build output is large and
/// a build changes little of it. A process still running from the copy would hold the files a refresh replaces, so
/// every caller kills any survivor before first reading <see cref="Executable"/>.</para>
/// </remarks>
internal static class ApplicationCopy
{
    /// <summary>
    /// The endpoint the copy's settings name: a controller host rather than a stub one, under a domain that never
    /// resolves, so only the switch can make the launched application a stubbed one.
    /// </summary>
    internal const string Endpoint = "http://no-stub-switch.invalid";

    /// <summary>The sections of the developer's file the copy keeps: the two that decide what a run reports.</summary>
    private static readonly string[] KeptSections = [TelemetryConfiguration.Key, "Logging"];

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static readonly Lazy<string> Prepared = new(Prepare);

    /// <summary>
    /// The executable to launch: the copy, refreshed on first use, with the settings beside it checked again on every
    /// read, because the process a launch starts reads them at that moment and not when the copy was made. A refresh
    /// that failed is kept for the rest of the run, so no later launch can start from a half-refreshed copy.
    /// </summary>
    internal static string Executable
    {
        get
        {
            string executable = Prepared.Value;
            return SettingsRefusal(Path.GetDirectoryName(executable)!) is { } refusal
                ? throw new InvalidOperationException(refusal)
                : executable;
        }
    }

    /// <summary>
    /// The settings file the copy runs with: a controller section naming <see cref="Endpoint"/> and no login, and the
    /// kept sections of <paramref name="developerSettings"/> when there is a developer's file at all.
    /// </summary>
    internal static string SettingsFor(string? developerSettings)
    {
        var settings = new JsonObject
        {
            [IhcSettings.ConfigurationSectionName] = new JsonObject { ["endpoint"] = Endpoint },
        };
        if (developerSettings is not null && ParseDeveloperSettings(developerSettings) is JsonObject developer)
        {
            foreach (string section in KeptSections)
            {
                if (developer.TryGetPropertyValue(section, out JsonNode? kept) && kept is not null)
                {
                    settings[section] = kept.DeepClone();
                }
            }
        }
        return settings.ToJsonString(Indented);
    }

    /// <summary>
    /// Why the application must not be launched from <paramref name="folder"/>, or null when it may: the settings
    /// beside the executable, read the way the application reads them, must name an endpoint under <c>.invalid</c>
    /// and no login. A refusal never quotes a value, because the file may be the developer's.
    /// </summary>
    internal static string? SettingsRefusal(string folder)
    {
        string file = $"the {IhcConfiguration.SettingsFileName} beside the executable in {folder}";
        if (!File.Exists(Path.Combine(folder, IhcConfiguration.SettingsFileName)))
        {
            return $"there is no {IhcConfiguration.SettingsFileName} beside the executable in {folder}, so the copy was not made";
        }

        IhcSettings settings;
        try
        {
            settings = IhcSettings.GetFromConfiguration(new ConfigurationBuilder()
                .SetBasePath(folder)
                .AddJsonFile(IhcConfiguration.SettingsFileName)
                .Build());
        }
        catch (Exception unreadable) when (unreadable is InvalidOperationException or InvalidDataException
                                               or FormatException or CryptographicException)
        {
            // The type alone: a binding or decryption message can quote the value it failed on.
            return $"{file} names no controller settings the application can read ({unreadable.GetType().Name})";
        }

        if (!IsUnderInvalid(settings.Endpoint))
        {
            return $"{file} names an endpoint outside the reserved .invalid domain";
        }
        if (!string.IsNullOrEmpty(settings.UserName) || !string.IsNullOrEmpty(settings.Password))
        {
            return $"{file} carries a controller login";
        }
        return null;
    }

    /// <summary>Refreshes the copy from the build output and writes its settings.</summary>
    private static string Prepare()
    {
        string built = Metadata("OpenVisualExecutable");
        if (!File.Exists(built))
        {
            throw new InvalidOperationException($"no application at {built} — build applications/ihc_openvisual first");
        }
        var source = new DirectoryInfo(Path.GetDirectoryName(built)!);
        // Inside this suite's own output, so the copy is as configuration-specific as the build it came from.
        var copy = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "openvisual-copy"));
        CleanCopy.Mirror(source, copy);

        string developerFile = Path.Combine(source.FullName, IhcConfiguration.SettingsFileName);
        File.WriteAllText(Path.Combine(copy.FullName, IhcConfiguration.SettingsFileName),
            SettingsFor(File.Exists(developerFile) ? File.ReadAllText(developerFile) : null));
        return Path.Combine(copy.FullName, Path.GetFileName(built));
    }

    /// <summary>
    /// A path baked in at compile time (D04), so it is deterministic and names the configuration these tests were
    /// built in: a Release test run cannot silently drive a Debug application.
    /// </summary>
    private static string Metadata(string key) =>
        typeof(ApplicationCopy).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value
        ?? throw new InvalidOperationException($"the test assembly carries no {key} metadata; see safe_visual_e2e_tests.csproj");

    /// <summary>
    /// The developer's file, read as leniently as the configuration reader the application uses: comments and
    /// trailing commas are allowed there, and a key matches whatever its case.
    /// </summary>
    private static JsonNode? ParseDeveloperSettings(string developerSettings)
    {
        try
        {
            return JsonNode.Parse(developerSettings, new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException malformed)
        {
            // The position alone, never the text the parser stopped at.
            throw new InvalidOperationException(
                $"the developer's {IhcConfiguration.SettingsFileName} is not valid JSON at line {malformed.LineNumber + 1}, "
                + "so its telemetry and logging sections cannot be kept");
        }
    }

    private static bool IsUnderInvalid(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
        && (uri.Host.Equals("invalid", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase));
}
