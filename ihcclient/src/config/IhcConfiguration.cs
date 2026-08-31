using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Ihc
{
    /// <summary>
    /// Loads <c>ihcsettings.json</c> from the directory the running application was launched from — the one
    /// bootstrap every IHC console host needs before it can read anything else out of configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It exists because every console entry point built this by hand, and the copies had already drifted:
    /// some null-guarded <see cref="Assembly.GetEntryAssembly"/> and some did not. That guard is
    /// not decoration — <c>GetEntryAssembly()</c> returns null whenever the process was not started from a
    /// managed <c>Main</c> (a native host, or an assembly loaded for reflection), and
    /// <see cref="Assembly.Location"/> is empty for a single-file publish, which is a
    /// <see cref="NullReferenceException"/> before the first line of the program's own work.
    /// </para>
    /// <para>
    /// Returning the whole <see cref="IConfigurationRoot"/> rather than a settings object is the point: a
    /// console reads its own sections out of the same root — telemetry, encryption, and each utility's own —
    /// and building the root twice is how two halves of one process come to disagree about their settings.
    /// <see cref="IhcSettings.GetFromFile"/> is this call plus <see cref="IhcSettings.GetFromConfiguration"/>.
    /// </para>
    /// </remarks>
    public static class IhcConfiguration
    {
        /// <summary>The settings file every IHC host reads.</summary>
        public const string SettingsFileName = "ihcsettings.json";

        /// <summary>
        /// Builds the configuration root from <see cref="SettingsFileName"/> beside the entry assembly,
        /// falling back to <see cref="AppContext.BaseDirectory"/> when there is no entry assembly location
        /// to take a directory from.
        /// </summary>
        /// <returns>The configuration root; the file is required and its absence throws.</returns>
        public static IConfigurationRoot FromAppDirectory()
        {
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(SettingsFileName)
                .Build();
        }
    }
}
