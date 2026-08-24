using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Vis;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Services;

/// <summary>
/// T019 (M6): the catalog-import collaborator extracted from <see cref="ProjectWorkflow"/> — runtime import of a
/// single <c>.def</c>/<c>.ifb</c> file or a whole folder (US-059/060), persistence into the app-data catalog
/// folder and the deterministic startup reload of those persisted files (US-061/M4). Delegates the actual parse to
/// the stateless <see cref="ProjectAppService"/>; raises <see cref="CatalogChanged"/> which
/// <see cref="ProjectWorkflow"/> forwards so the insertion menus rebuild. Holds no document state.
/// </summary>
internal sealed class CatalogImportWorkflow(
    ProjectAppService service, IDialogService dialogs, ILogger logger, string catalogDir)
{
    /// <summary>Raised after a catalog import changes the available products/function blocks (US-059/US-060).</summary>
    public event EventHandler? CatalogChanged;

    // The ONE title over an import that never started: an unreadable single file (US-059) and a folder that is not
    // there (US-060) are the same answer to the installer, so the wording is declared once. The mid-folder abort
    // (US-062) is deliberately titled differently — earlier files WERE imported, which is a different outcome.
    private const string ImportFailedTitle = "Import mislykkedes";

    /// <summary>The title over a FOLDER import that stopped part-way: the ones before it were kept, so the box
    /// says stopped rather than failed (US-062).</summary>
    private const string ImportStoppedTitle = "Import stoppet";

    /// <summary>
    /// US-062's "naming it", now that the box's SENTENCE is the SDK's cause (D01): the offending file names the
    /// TITLE, so the installer reads which file in the caption and why in the body, rather than which file twice
    /// and why not at all.
    /// </summary>
    private static string Naming(string title, string path) => $"{title}: {Path.GetFileName(path)}";

    private static IEnumerable<string> EnumerateCatalogFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".def", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".ifb", StringComparison.OrdinalIgnoreCase))
            // Deterministic import order for EVERY caller (M4): last-import-wins resolution and the resulting menu
            // order must not depend on the raw, filesystem-dependent directory enumeration order (unsorted on many
            // filesystems; case-insensitive on NTFS). The startup reload and the interactive folder import share this.
            .OrderBy(f => f, StringComparer.Ordinal);

    /// <summary>Loads persisted imports on startup (US-061), best-effort: a single unreadable persisted file is
    /// skipped (logged); it does not stop the load or crash startup.</summary>
    public void LoadPersisted()
    {
        try
        {
            if (!Directory.Exists(catalogDir))
                return;
            foreach (string file in EnumerateCatalogFiles(catalogDir))
            {
                try
                {
                    service.ImportCatalogFile(file);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped unreadable persisted catalog file {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load persisted catalog from {Dir}", catalogDir);
        }
    }

    private void PersistFile(string path)
    {
        Directory.CreateDirectory(catalogDir);
        File.Copy(path, Path.Combine(catalogDir, Path.GetFileName(path)), overwrite: true);
    }

    /// <summary>Imports a single product (<c>.def</c>)/function-block (<c>.ifb</c>) file (US-059); when
    /// <paramref name="persist"/> is set it is also copied into the catalog folder (US-061). On success
    /// <see cref="CatalogChanged"/> fires; on failure the error names the file (US-062). Returns true on success.</summary>
    public async Task<bool> ImportFileAsync(string path, bool persist)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(CatalogImportWorkflow)}.{nameof(ImportFileAsync)}");
        try
        {
            service.ImportCatalogFile(path);
            if (persist)
                PersistFile(path);
            CatalogChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            ActivityExtensions.SetError(activity, ex);
            // One-child chain: the shell's framing as the operation over the SDK's coded cause, so exactly one
            // sentence reaches the user and it says WHY the file was rejected (D01, case 2). The file itself is
            // US-062's obligation and is carried by the title; the English detail goes to the log.
            logger.LogError(ex, "Failed to import catalog file {File}", path);
            await RaisedProblemDisplay.ShowAsync(dialogs, Naming(ImportFailedTitle, path),
                HostProblems.CatalogFileRejected(Path.GetFileName(path), ex), ex);
            return false;
        }
    }

    /// <summary>Imports every <c>.def</c>/<c>.ifb</c> in a folder and its subfolders (US-060), optionally persisting
    /// each (US-061). Stops at the first unreadable file, naming it, keeping earlier ones (US-062). Fires
    /// <see cref="CatalogChanged"/>.</summary>
    public async Task<CatalogImportOutcome> ImportFolderAsync(string dir, bool persist)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(CatalogImportWorkflow)}.{nameof(ImportFolderAsync)}");
        if (!Directory.Exists(dir))
        {
            // The shell's own condition — it checked the folder before asking the SDK for anything — so a host code.
            await dialogs.ShowProblemAsync(ImportFailedTitle, HostProblems.CatalogFolderMissing(dir));
            return CatalogImportOutcome.NotFound;
        }
        int count = 0;
        bool stopped = false;
        try
        {
            foreach (string file in EnumerateCatalogFiles(dir))   // already Ordinal-ordered by EnumerateCatalogFiles (M4)
            {
                try
                {
                    service.ImportCatalogFile(file);
                    if (persist)
                        PersistFile(file);
                    count++;
                }
                catch (Exception ex)
                {
                    ActivityExtensions.SetError(activity, ex);
                    logger.LogError(ex, "Folder import stopped at {File}", file);
                    // The same one-child chain as the single-file site, with the batch count as a declared argument.
                    await RaisedProblemDisplay.ShowAsync(dialogs, Naming(ImportStoppedTitle, file),
                        HostProblems.CatalogImportStopped(Path.GetFileName(file), count, ex), ex);
                    stopped = true;
                    break;   // stop at the first unreadable file (US-062)
                }
            }
        }
        finally
        {
            CatalogChanged?.Invoke(this, EventArgs.Empty);
        }
        return new CatalogImportOutcome(count, stopped);
    }
}

/// <summary>
/// How a folder import ENDED, not merely how much of it succeeded (UX review CORE-03). A folder that stops at an
/// unreadable file keeps the components imported before it, so the count alone is indistinguishable from a complete
/// import of that many files — and the shell reported both as "Importerede N komponenter", telling the installer and
/// any automation client that a half-finished import had finished.
/// </summary>
/// <param name="Imported">How many components were imported (0 or more; 0 when the folder was missing).</param>
/// <param name="Stopped">Whether the run stopped early on an unreadable file, so more files were left unread.</param>
/// <param name="FolderMissing">Whether the folder did not exist, so nothing was even attempted.</param>
public readonly record struct CatalogImportOutcome(int Imported, bool Stopped, bool FolderMissing = false)
{
    /// <summary>The outcome of an import whose folder was not there.</summary>
    public static CatalogImportOutcome NotFound { get; } = new(0, Stopped: false, FolderMissing: true);

    /// <summary>Whether every file in the folder was read — the only outcome that may be announced as complete.</summary>
    public bool Completed => !Stopped && !FolderMissing;
}
