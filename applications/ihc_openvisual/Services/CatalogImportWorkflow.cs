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
            logger.LogError(ex, "Failed to import catalog file {File}", path);
            await dialogs.ShowMessageAsync("Import failed",
                $"'{Path.GetFileName(path)}' is not a valid product or function-block definition file:\n{ex.Message}");
            return false;
        }
    }

    /// <summary>Imports every <c>.def</c>/<c>.ifb</c> in a folder and its subfolders (US-060), optionally persisting
    /// each (US-061). Returns the number imported; a missing folder returns -1. Stops at the first unreadable file,
    /// naming it, keeping earlier ones (US-062). Fires <see cref="CatalogChanged"/>.</summary>
    public async Task<int> ImportFolderAsync(string dir, bool persist)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(CatalogImportWorkflow)}.{nameof(ImportFolderAsync)}");
        if (!Directory.Exists(dir))
        {
            await dialogs.ShowMessageAsync("Import failed", $"The folder '{dir}' does not exist.");
            return -1;
        }
        int count = 0;
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
                    await dialogs.ShowMessageAsync("Import stopped",
                        $"'{Path.GetFileName(file)}' could not be imported ({count} imported before it):\n{ex.Message}");
                    break;   // stop at the first unreadable file (US-062)
                }
            }
        }
        finally
        {
            CatalogChanged?.Invoke(this, EventArgs.Empty);
        }
        return count;
    }
}
