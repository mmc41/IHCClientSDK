using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Services;

/// <summary>
/// The one load/save body behind every JSON application preference — the recent-projects list, the installer
/// identity and the documentation data tables. Each store keeps its own shape and its own path and delegates the
/// parts that were identical: the filtered catch, the directory creation, and one warning naming the file and the
/// operation.
/// <para>
/// A preference is best-effort by design: a corrupt file must not stop the app starting, and a write that cannot
/// land must not fail the edit that triggered it. What changed is that failing is no longer SILENT: each store
/// used to swallow the exception in a catch block of its own, so a store whose file was unwritable stayed broken
/// forever with nothing to grep for. The exception is carried into the record, so the log keeps the detail the
/// user is deliberately not shown.
/// </para>
/// </summary>
internal static class JsonPreferenceStore
{
    /// <summary>Reads and deserializes the preference file, or returns <c>default</c> when it is absent,
    /// unreadable or corrupt. A missing file is the normal first-run case and is not logged.</summary>
    /// <remarks>
    /// <para>The <c>File.Exists</c> guard STAYS, and not for the missing-file case the open would report anyway:
    /// it answers "is there a FILE here", so a directory occupying the store's path reads as no preference rather
    /// than as an unreadable one. Dropping it turned that case into a logged load failure — a store that never
    /// had a file complaining that it could not read one.</para>
    /// <para>Deserializing from the STREAM, though: transcoding the whole file to a <see cref="string"/> only for
    /// the parser to re-encode it to UTF-8 bytes is a round trip with no reader.</para>
    /// </remarks>
    public static T? TryLoad<T>(string filePath, ILogger logger)
    {
        if (!File.Exists(filePath))
            return default;
        try
        {
            using FileStream file = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<T>(file);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Warn(logger, ex, "load", filePath);
            return default;
        }
    }

    /// <summary>Creates the containing directory if needed and writes the preference file, reporting rather than
    /// throwing when it cannot.</summary>
    /// <remarks>
    /// INDENTED by default, and that is the whole reason the option is not a parameter: a preference file is read
    /// and occasionally hand-edited by people. Passed per store it was a setting two of the three remembered,
    /// which is how one preference file came to be written unreadably while its neighbours were not.
    /// </remarks>
    public static void TrySave<T>(string filePath, T value, ILogger logger)
    {
        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            using FileStream file = File.Create(filePath);
            JsonSerializer.Serialize(file, value, Indented);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warn(logger, ex, "save", filePath);
        }
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static void Warn(ILogger logger, Exception ex, string operation, string filePath) =>
        logger.LogWarning(ex, "Preference {Operation} failed for {PreferenceFile}", operation, filePath);
}
