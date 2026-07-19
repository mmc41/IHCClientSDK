using System;
using System.IO;
using System.Text.Json;

namespace ihc_openvisual.Services;

/// <summary>Origin of a recovery backup: the file the crashed session was editing (null = Untitled) and when
/// the backup was taken.</summary>
public sealed record RecoveryInfo(string? OriginPath, DateTimeOffset SavedAtUtc);

/// <summary>
/// Manages the crash-recovery backup files (US-005): the location of the recovery project copy and its origin
/// marker, and their creation/detection/deletion. Only the file bookkeeping lives here; <see cref="ProjectWorkflow"/>
/// owns the schedule (10-minute timer + every-10th-change) and writes the actual project bytes via the SDK.
/// Avalonia-free and testable.
/// </summary>
public sealed class BackupService
{
    private readonly string _directory;

    public BackupService(string directory) => _directory = directory;

    public static BackupService CreateDefault() => new(DefaultDirectory());

    public static string DefaultDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IHC OpenVisual",
            "recovery");

    /// <summary>The path the recovery project copy is written to.</summary>
    public string RecoveryProjectPath => Path.Combine(_directory, "recovery.vis");

    private string MarkerPath => Path.Combine(_directory, "recovery.json");

    public void EnsureDirectory() => Directory.CreateDirectory(_directory);

    /// <summary>True when a complete recovery backup (project copy + marker) is present from a prior session.</summary>
    public bool HasRecovery() => File.Exists(RecoveryProjectPath) && File.Exists(MarkerPath);

    public RecoveryInfo? ReadMarker()
    {
        try
        {
            return File.Exists(MarkerPath)
                ? JsonSerializer.Deserialize<RecoveryInfo>(File.ReadAllText(MarkerPath))
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void WriteMarker(string? originPath, DateTimeOffset savedAtUtc)
    {
        EnsureDirectory();
        File.WriteAllText(MarkerPath, JsonSerializer.Serialize(new RecoveryInfo(originPath, savedAtUtc)));
    }

    /// <summary>Removes the recovery files. Called on a clean, acknowledged close/quit (US-005 lifecycle rule).</summary>
    public void Delete()
    {
        TryDelete(RecoveryProjectPath);
        TryDelete(MarkerPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception)
        {
            // Best-effort cleanup; a leftover file only means the next start offers a stale recovery.
        }
    }
}
