using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ihc_openvisual.Configuration;
using Ihc.Vis;
using Ihc.Vis.Addressing;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.Services;

/// <summary>
/// The single open-document session for the window: owns the one <see cref="Project"/>, its file path, the
/// dirty flag and the change counter, and orchestrates the whole project lifecycle (new/open/save/save-as/
/// close/quit) on top of the stateless SDK <see cref="ProjectAppService"/>. Enforces the single-project
/// constraint, drives the save-prompt through <see cref="IDialogService"/>, and runs the crash-recovery
/// auto-backup (10-minute timer + every 10th change). Deliberately Avalonia-free so it is testable headlessly.
/// </summary>
public sealed class ProjectWorkflow : IDisposable
{
    private readonly ProjectAppService _service;
    private readonly BackupService _backup;
    private readonly RecentProjectsStore _recent;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ProjectWorkflow> _logger;
    private readonly int _changeBackupThreshold;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    // T019: the non-lifecycle concerns split into their own collaborators — reporting, catalog imports/persist, and
    // the auto-backup writer (timer + write lock). ProjectWorkflow retains the document lifecycle + editing/history.
    private readonly ProjectReportWorkflow _reports;
    private readonly CatalogImportWorkflow _catalog;
    private readonly AutoBackupScheduler _autoBackup;

    private readonly string _catalogDir;

    public ProjectWorkflow(
        ProjectAppService service,
        BackupService backup,
        RecentProjectsStore recent,
        IDialogService dialogs,
        ILoggerFactory? loggerFactory = null,
        TimeSpan? autoBackupInterval = null,
        int changeBackupThreshold = 10,
        string? catalogDir = null,
        TimeProvider? timeProvider = null)
    {
        _service = service;
        _backup = backup;
        _recent = recent;
        _dialogs = dialogs;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ProjectWorkflow>();
        _timeProvider = timeProvider ?? TimeProvider.System;   // D8: the auto-backup clock/timer, fakeable in tests
        _changeBackupThreshold = changeBackupThreshold < 1 ? 10 : changeBackupThreshold;
        _catalogDir = catalogDir ?? DefaultCatalogDir();
        _reports = new ProjectReportWorkflow(_service, _dialogs, _logger, () => Current);
        _catalog = new CatalogImportWorkflow(_service, _dialogs, _logger, _catalogDir);
        // The auto-backup writer captures the snapshot + origin under THIS workflow's gate (document state stays here);
        // the change-threshold trigger stays in ApplyAsync (it owns the change counter).
        _autoBackup = new AutoBackupScheduler(_backup, _service, _timeProvider, _logger,
            autoBackupInterval ?? TimeSpan.FromMinutes(10), CaptureBackupSnapshot);
        _catalog.LoadPersisted();   // persisted imports load on startup (US-061)
    }

    // Captures the current project snapshot + its origin path under the workflow gate, for the auto-backup writer.
    private (Project? Snapshot, string? Origin) CaptureBackupSnapshot()
    {
        lock (_gate)
        {
            return (Current, FilePath);
        }
    }

    /// <summary>The app-data folder persisted catalog imports are copied into and loaded from on startup (US-061).</summary>
    private static string DefaultCatalogDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IHC OpenVisual", "catalog");

    public Project? Current { get; private set; }

    /// <summary>The SDK command-factory gateway (the single authoring door, D01): builds ready-to-apply
    /// <see cref="ProjectCommand"/>s the VM hands to <see cref="ApplyAsync(ProjectCommand,int?)"/>. Exposes the
    /// stateless planner on the underlying <see cref="ProjectAppService"/> — the app never constructs command
    /// types directly.</summary>
    public ProjectCommands Commands => _service.Commands;

    public string? FilePath { get; private set; }

    public bool IsDirty { get; private set; }

    public int ChangeCount { get; private set; }

    private int _version;

    /// <summary>A monotone counter bumped on every state transition (commit / undo / redo / load). A caller that
    /// prepares an edit against a dialog can capture it and pass it back to <see cref="ApplyAsync(ProjectCommand,int?)"/>
    /// as the base version; a mismatch means the project moved on and the stale edit is refused (W2-14).</summary>
    public int Version
    {
        get { lock (_gate) { return _version; } }
    }

    // The multi-level undo/redo history (US-052): immutable project snapshots. Every project-mutating commit goes
    // through CommitAsync, which pushes the pre-edit snapshot here and clears the redo list; loads (New/Open/Close)
    // reset the history. Unlimited (W4-4): a committed snapshot path-copies only the subtrees it changed (W4-3), so a
    // deep history costs its changed paths, not a full tree per entry — bounded only by process memory.
    // Each entry pairs the pre-edit snapshot to restore with the label of the edit that produced the newer state
    // (the command's Describe, surfaced as EditOutcome.Label — W2-14/E14), so Undo/Redo can name their action.
    private readonly List<(Project Snapshot, string Label)> _undo = new();
    private readonly List<(Project Snapshot, string Label)> _redo = new();

    // The snapshot the document was last known to match: what a Save wrote, or the state a New/Open started from.
    // Dirtiness is "Current is not this snapshot", so undoing back to a saved state clears the flag rather than
    // latching it and prompting to save a project identical to its file. Null when no clean state exists (a
    // recovered project). Projects are immutable and the history stores the very same instances, so reference
    // identity is the comparison — a value comparison would walk the whole tree on every edit.
    private Project? _savePoint;

    /// <summary>Whether there is an edit to undo (US-052).</summary>
    public bool CanUndo
    {
        get { lock (_gate) { return _undo.Count > 0; } }
    }

    /// <summary>Whether there is an undone edit to redo (US-052).</summary>
    public bool CanRedo
    {
        get { lock (_gate) { return _redo.Count > 0; } }
    }

    /// <summary>The label of the edit <see cref="UndoAsync"/> would reverse (e.g. "Insert locality"), or null when
    /// there is nothing to undo — so the status bar and Edit ▸ Undo can name the action (US-052/E14).</summary>
    public string? UndoLabel
    {
        get { lock (_gate) { return _undo.Count > 0 ? _undo[^1].Label : null; } }
    }

    /// <summary>The label of the edit <see cref="RedoAsync"/> would re-apply, or null when nothing to redo (E14).</summary>
    public string? RedoLabel
    {
        get { lock (_gate) { return _redo.Count > 0 ? _redo[^1].Label : null; } }
    }

    /// <summary>The document name shown in the title bar: <c>Untitled</c> before the first save, else the file name.</summary>
    public string DocumentName => FilePath is null ? Constants.UntitledDocument : Path.GetFileName(FilePath);

    /// <summary>Raised whenever the current project, file path or dirty flag changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>The structural change set of the most recent committed edit — the delta the GUI reconciler (W3-4)
    /// applies to the tree in place — or null when the last transition was NOT a single incremental edit (a load,
    /// undo, redo, save or close), which the GUI handles with a full rebuild. Set immediately before
    /// <see cref="StateChanged"/> fires, so the refresh that event triggers reads the change that caused it.</summary>
    public ProjectChangeSet? LastChange { get; private set; }

    /// <summary>Start-up entry point: offer to recover a crash backup if one exists (US-005), otherwise open a
    /// fresh empty project (US-002); then begin the auto-backup timer.
    /// <para>When <paramref name="skipRecovery"/> is set (the <c>--skip-recovery</c> launch flag), the recovery
    /// prompt is bypassed entirely and any stale crash backup is discarded, so an unattended UI-automation
    /// session always opens a deterministic fresh project instead of blocking on a modal dialog.</para></summary>
    public async Task StartAsync(bool skipRecovery = false)
    {
        if (skipRecovery)
        {
            _backup.Delete();
        }
        else if (_backup.HasRecovery())
        {
            RecoveryInfo? info = _backup.ReadMarker();
            string when = info is { } i ? $" from {i.SavedAtUtc.ToLocalTime():g}" : string.Empty;
            bool recover = await _dialogs.ConfirmAsync(
                "Recover project",
                $"IHC OpenVisual did not close normally last time. Recover unsaved work{when}?");
            if (recover)
            {
                try
                {
                    Project recovered = await _service.Load(_backup.RecoveryProjectPath);
                    SetProject(recovered, info?.OriginPath, dirty: true);
                    ResetChangeCount();
                    _autoBackup.Start();
                    return;
                }
                catch (Exception ex)
                {
                    // A corrupt recovery file must not crash the launch — this runs under App's async-void
                    // window.Opened handler, so an unhandled throw is unobserved and fatal. Route it through the
                    // same error path as OpenAsync: report it, discard the unusable backup below, and fall through
                    // to a fresh project (review Low "startup async void").
                    _logger.LogError(ex, "Failed to recover crash backup {Path}", _backup.RecoveryProjectPath);
                    await _dialogs.ShowMessageAsync("Recovery failed",
                        $"The recovered project could not be loaded and was discarded:\n{ex.Message}");
                }
            }
            _backup.Delete();
        }

        NewInternal();
        _autoBackup.Start();
    }

    /// <summary>File → New (US-002): prompt to save the open project, then open the standard empty project.</summary>
    public async Task<bool> NewAsync()
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        NewInternal();
        _backup.Delete();
        ResetChangeCount();
        return true;
    }

    /// <summary>File → Open (US-004): prompt to save, then load the chosen file as the single active project.</summary>
    public async Task<bool> OpenAsync(string path)
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        try
        {
            Project loaded = await _service.Load(path);
            SetProject(loaded, path, dirty: false);
            _recent.Add(path);
            _backup.Delete();
            ResetChangeCount();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open project {Path}", path);
            await _dialogs.ShowMessageAsync("Open failed", $"Could not open '{path}':\n{ex.Message}");
            return false;
        }
    }

    public async Task<bool> OpenWithPickerAsync()
    {
        string? path = await _dialogs.PickOpenProjectAsync(_recent.LastDirectory);
        return path is not null && await OpenAsync(path);
    }

    /// <summary>File → Save (US-003): re-save to the existing file, or fall through to Save As when unnamed.</summary>
    public async Task<bool> SaveAsync()
    {
        if (Current is null)
            return false;
        return FilePath is null ? await SaveAsAsync() : await SaveToAsync(FilePath);
    }

    /// <summary>File → Save As (US-003): pick a file name and write the project there.</summary>
    public async Task<bool> SaveAsAsync()
    {
        if (Current is null)
            return false;
        string suggested = FilePath is not null ? Path.GetFileName(FilePath) : "Untitled.vis";
        string? path = await _dialogs.PickSaveProjectAsync(_recent.LastDirectory, suggested);
        return path is not null && await SaveToAsync(path);
    }

    /// <summary>File → Close (US-005): prompt to save, discard the crash backup, and return to a fresh empty project.</summary>
    public async Task<bool> CloseAsync()
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        _backup.Delete();
        NewInternal();
        ResetChangeCount();
        return true;
    }

    /// <summary>Quit gate (US-064): prompt to save; on a clean, acknowledged exit discard the crash backup.
    /// Returns false to cancel the quit.</summary>
    public async Task<bool> CanQuitAsync()
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        _backup.Delete();
        return true;
    }

    /// <summary>The default name a freshly inserted locality carries until the installer renames it (US-008).</summary>
    public const string NewLocalityName = "Locality";

    // Reports (US-040/041) delegate to the ProjectReportWorkflow collaborator (T019).
    /// <summary>The render-ready installation report model for the open project (US-040), or null if none.</summary>
    public InstallationReport? GenerateInstallationReport() => _reports.Installation();

    /// <summary>The render-ready end-user report model for the open project (US-040), or null if none.</summary>
    public EndUserReport? GenerateEndUserReport() => _reports.EndUser();

    /// <summary>The render-ready function-block documentation report model for the open project (US-041), or null.</summary>
    public FunctionBlockReport? GenerateFunctionBlockReport() => _reports.FunctionBlock();

    /// <summary>Writes a rendered report HTML page to a temp file (US-040) and returns its path for the browser to
    /// open; null on failure — delegates to the ProjectReportWorkflow collaborator (T019).</summary>
    public Task<string?> WriteReportHtmlAsync(string fileStem, string html) => _reports.WriteHtmlAsync(fileStem, html);

    /// <summary>Reads the current project/customer/installer information (US-039) to prefill the dialog. Delegates
    /// to the SDK projection (<c>Ihc.Vis.ProjectProjections</c>). Permanent: the GUI runs commands through a per-call
    /// scratch session, not one persistent <c>ProjectDocumentSession</c> (D12 thread-affinity superseded W2's
    /// persistent-session goal), so there is no long-lived session query to delegate to.</summary>
    public ProjectInfoData GetProjectInfo() => Current?.GetProjectInfo() ?? ProjectInfoData.Empty;

    /// <summary>
    /// Reads the project's data tables (US-049): the read-only system tables (the built-in <c>typeid</c>-bearing enum
    /// definitions) and the editable user-defined texts (the values of the <see cref="ProjectProjections.UserTextsTableName"/> enum).
    /// Delegates to the SDK projection. Permanent (see <see cref="GetProjectInfo"/>): no persistent session to query.
    /// </summary>
    public DataTablesModel GetDataTables() => Current?.GetDataTables() ?? DataTablesModel.Empty;

    /// <summary>
    /// Names the wireless products in the project not yet linked to the controller (US-042 pre-flight): the offline
    /// half of the "warn about unlinked wireless products before sending" check. Delegates to the SDK projection.
    /// </summary>
    public IReadOnlyList<string> GetUnlinkedWirelessProducts() =>
        Current?.GetUnlinkedWirelessProducts() ?? new List<string>();

    /// <summary>
    /// Builds the read-only Wired module address map (US-050): every addressed <c>dataline_input</c>/<c>dataline_output</c>
    /// terminal across all products, decoded to its <c>line.terminal</c> address and split into input/output modules,
    /// sorted by address. Delegates to the SDK projection. Permanent (see <see cref="GetProjectInfo"/>): no
    /// persistent session to query.
    /// </summary>
    public ModuleAddressMap GetModuleAddressMap() =>
        Current?.GetModuleAddressMap() ?? ModuleAddressMap.Empty;

    /// <summary>The catalog products available for insertion (from the SDK-embedded catalog; no controller needed).</summary>
    public IReadOnlyList<ProductDefinition> GetAvailableProducts() => _service.GetAvailableProducts();

    /// <summary>The catalog library function blocks available for insertion (SDK-embedded catalog; no controller).</summary>
    public IReadOnlyList<FunctionBlockDefinition> GetAvailableFunctionBlocks() => _service.GetAvailableFunctionBlocks();

    /// <summary>The catalog products as slim insert-menu items (<see cref="CatalogItem"/>) — what the insert menu binds to.</summary>
    public IReadOnlyList<CatalogItem> GetProductCatalogItems() => _service.GetProductCatalogItems();

    /// <summary>The catalog function blocks as slim insert-menu items (<see cref="CatalogItem"/>).</summary>
    public IReadOnlyList<CatalogItem> GetFunctionBlockCatalogItems() => _service.GetFunctionBlockCatalogItems();

    /// <summary>The default name a freshly inserted empty function block carries until renamed (US-019).</summary>
    public const string EmptyBlockName = "Empty block";


    /// <summary>
    /// Saves a placed function block to a reusable <c>.ifb</c> catalog file (US-021): lifts the block (by id) to a
    /// keyless user-block definition via <see cref="Ihc.Vis.Editing.FunctionBlockRef.ExportDefinition"/> and writes it with
    /// <see cref="Ihc.Vis.Catalog.CatalogFileWriter"/>. Read-only over the project (nothing is mutated, so no dirty
    /// flag). Returns false (with a diagnostic) when the id is not a function block or the write fails.
    /// </summary>
    public async Task<bool> SaveFunctionBlockAsync(ElementId functionBlockId, string filePath, string name, string note)
    {
        if (Current is not { } project)
            return false;
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(SaveFunctionBlockAsync)}");
        try
        {
            // The Gem… composition now lives behind the one door (ProjectAppService.ExportFunctionBlock, R3):
            // explicit author (the OS user is an app-side concern), clock-defaulted date, atomic write. The app
            // supplies only the author.
            await _service.ExportFunctionBlock(project, functionBlockId, filePath, name, Environment.UserName,
                note: string.IsNullOrEmpty(note) ? null : note);
            return true;
        }
        catch (Exception ex)
        {
            Ihc.ActivityExtensions.SetError(activity, ex);
            _logger.LogError(ex, "Failed to save function block {Id} to {Path}", functionBlockId.ToToken(), filePath);
            await _dialogs.ShowMessageAsync("Save failed", ex.Message);
            return false;
        }
    }

    // ---- Command factories (W2-14): resolve parent context into a ready-to-apply command (a query, no mutation).
    // The catalog-bearing Product family (T004) relocated to the SDK gateway (ProjectAppService.Commands); the
    // families still below migrate in later R1 tasks. A null return = "could not be built". ----

    // Catalog imports/persist (US-059/060/061/062) delegate to the CatalogImportWorkflow collaborator (T019);
    // the startup LoadPersisted() ran in the ctor.

    /// <summary>Raised after a catalog import changes the available products/function blocks (US-059/US-060), so the
    /// insertion menus rebuild — forwarded from the catalog collaborator.</summary>
    public event EventHandler? CatalogChanged
    {
        add => _catalog.CatalogChanged += value;
        remove => _catalog.CatalogChanged -= value;
    }

    /// <summary>Imports a single product/function-block file (US-059/061) — delegates to the catalog collaborator.</summary>
    public Task<bool> ImportCatalogFileAsync(string path, bool persist) => _catalog.ImportFileAsync(path, persist);

    /// <summary>Imports every definition in a folder (US-060/062) — delegates to the catalog collaborator.</summary>
    public Task<int> ImportCatalogFolderAsync(string dir, bool persist) => _catalog.ImportFolderAsync(dir, persist);


    // The single commit path for every project-mutating operation (US-052): snapshots the pre-edit project for undo,
    // invalidates the redo history, swaps in the new project, then marks changed (dirty + backup + StateChanged).
    // fablerefac W2-5 (migrate): route a command through a document session, then persist the result via the
    // existing commit path so ProjectWorkflow's Current/undo/dirty stay the source of truth. A fresh session per call
    // is created on the calling thread, sidestepping the session's thread-affinity guard; it is used once as a
    // stateless command runner.
    //
    // DESIGN NOTE (do not "finish W2" by swapping this for one persistent ProjectDocumentSession): W2-14 originally
    // envisioned a single persistent session the VM drives directly. That was SUPERSEDED by the later D12 decision to
    // make ProjectDocumentSession thread-affine (owner thread captured at construction). A persistent session would
    // be constructible only on the exact thread it is later operated from, forcing every session-touching test onto
    // [AvaloniaTest] and coupling core correctness to Avalonia's async-continuation marshalling (under-documented and
    // evolving across major versions). Deep research (2026-07-20) confirmed that approach is *feasible* in Avalonia
    // 12 but strictly worse on simplicity/testability/stability/portability than this OpenScratch runner, which
    // depends on nothing Avalonia-specific. Keep the per-call scratch session; keep undo/redo/version here.
    /// <summary>
    /// Applies a command to the open project and commits it on success (W2-14): the single edit entry the VM drives.
    /// Runs the command through a stateless document session over <see cref="Current"/>, then — only on
    /// <see cref="EditStatus.Committed"/> — swaps in the result, snapshots for undo (labelled by the command) and
    /// marks changed. Returns the raw <see cref="EditOutcome"/>; the caller maps it to status text / dialogs (the
    /// single outcome→status/dialog rule). When <paramref name="baseVersion"/> is supplied and no longer matches
    /// <see cref="Version"/>, the edit is refused as stale (a dialog prepared against an older project).
    /// </summary>
    public async Task<EditOutcome> ApplyAsync(ProjectCommand command, int? baseVersion = null)
    {
        if (StaleOrClosed(command, baseVersion) is { } refusal)
            return refusal;
        ProjectDocumentSession document = OpenScratch(Current!);
        EditOutcome outcome = document.Apply(command);
        if (outcome.Status == EditStatus.Committed)
            await CommitAsync(document.Current!, outcome.Label, outcome.Changes);
        return outcome;
    }

    /// <summary>The value-producing overload of <see cref="ApplyAsync(ProjectCommand,int?)"/> (e.g. a new element's id).</summary>
    public async Task<EditOutcome<T>> ApplyAsync<T>(ProjectCommand<T> command, int? baseVersion = null)
    {
        if (StaleOrClosed(command, baseVersion) is { } refusal)
            return new EditOutcome<T>(refusal.Status, refusal.Label, refusal.Reason, null, default);
        ProjectDocumentSession document = OpenScratch(Current!);
        EditOutcome<T> outcome = document.Apply(command);
        if (outcome.Status == EditStatus.Committed)
            await CommitAsync(document.Current!, outcome.Label, outcome.Changes);
        return outcome;
    }

    // The shared no-project / stale-base-version guard: returns a Refused outcome to short-circuit, or null to proceed.
    private EditOutcome? StaleOrClosed(ProjectCommand command, int? baseVersion)
    {
        if (Current is null)
            return new EditOutcome(EditStatus.Refused, command.GetType().Name, "No project is open.", null);
        if (baseVersion is { } expected && expected != Version)
            return new EditOutcome(EditStatus.Refused, command.GetType().Name,
                "The project changed since this edit was prepared.", null);
        return null;
    }

    // Opens a throwaway document session over a snapshot of the given project — the stateless runner behind every
    // command probe/apply/preview against Current (nothing persists back except through CommitAsync).
    private static ProjectDocumentSession OpenScratch(Project project)
    {
        var document = new ProjectDocumentSession();
        document.Open(project, startClean: true);
        return document;
    }

    /// <summary>The command's legality verdict against the open project (cheap — no edit), for drag-over probes and
    /// menu gates. Refused when no project is open.</summary>
    public EditVerdict CanApply(ProjectCommand command)
    {
        if (Current is not { } current)
            return EditVerdict.Refuse("No project is open.");
        return OpenScratch(current).CanApply(command);
    }

    /// <summary>The typed preview of a command applied now, without committing (M8/D05): the delta it would commit
    /// when it <see cref="PreviewStatus.WouldChange"/>, else a refuse / no-change / engine-fault status. Drives the
    /// Preview→confirm→Apply flow (W2-13). Currently exercised only by tests — the GUI's delete-confirm flow reads
    /// <see cref="ProjectCommands.PreviewDelete"/> instead.</summary>
    public PreviewOutcome Preview(ProjectCommand command) =>
        Current is { } current
            ? OpenScratch(current).Preview(command)
            : PreviewOutcome.Refused("No project is open.");

    private async Task CommitAsync(Project updated, string label = "Edit", ProjectChangeSet? changes = null)
    {
        lock (_gate)
        {
            if (Current is not null)
            {
                _undo.Add((Current, label));   // unlimited (W4-4): no trim — entries are cheap path-copies (W4-3)
            }
            _redo.Clear();
            Current = updated;
            _version++;
        }
        // Carry the edit's change set to the GUI so it reconciles the tree in place (W3-6); a null change set (undo/
        // redo/load/save) drives the full-rebuild fallback instead.
        await NotifyChangedAsync(dirty: true, changes);
    }

    /// <summary>
    /// Undoes the last project-mutating edit (US-052): restores the previous snapshot, pushes the current one onto the
    /// redo history, and refreshes both panes. A cascading edit (e.g. a non-empty locality delete) reverses as one step
    /// because it was committed as a single snapshot. A no-op (returns false) when there is nothing to undo.
    /// </summary>
    public async Task<bool> UndoAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(UndoAsync)}");
        bool dirty;
        lock (_gate)
        {
            if (_undo.Count == 0 || Current is null)
                return false;
            (Project snapshot, string label) = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add((Current, label));   // redo re-applies the same edit, so it carries the same label
            Current = snapshot;
            _version++;
            dirty = !ReferenceEquals(Current, _savePoint);
        }
        await NotifyChangedAsync(dirty);
        return true;
    }

    /// <summary>Re-applies the last undone edit (US-052): the mirror of <see cref="UndoAsync"/>. No-op (false) when the
    /// redo history is empty.</summary>
    public async Task<bool> RedoAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(RedoAsync)}");
        bool dirty;
        lock (_gate)
        {
            if (_redo.Count == 0 || Current is null)
                return false;
            (Project snapshot, string label) = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add((Current, label));
            Current = snapshot;
            _version++;
            dirty = !ReferenceEquals(Current, _savePoint);
        }
        await NotifyChangedAsync(dirty);
        return true;
    }

    /// <summary>
    /// The shared tail of every change: set the flag, advance the change counter, notify, and take a crash backup
    /// every Nth change. Undo/redo pass a <paramref name="dirty"/> derived from the save point rather than a
    /// constant, because navigating the history can land back on the saved snapshot.
    /// </summary>
    private async Task NotifyChangedAsync(bool dirty, ProjectChangeSet? change = null)
    {
        bool backup;
        lock (_gate)
        {
            IsDirty = dirty;
            ChangeCount++;
            backup = ChangeCount % _changeBackupThreshold == 0;
        }
        RaiseChanged(change);
        if (backup)
            await _autoBackup.WriteAsync();
    }

    /// <summary>Writes the current project to the recovery location — the change-counter (here) and timer paths
    /// delegate to the AutoBackupScheduler collaborator (T019); exposed internally so tests can drive it.</summary>
    internal Task AutoBackupAsync() => _autoBackup.WriteAsync();

    private async Task<bool> SaveToAsync(string path)
    {
        try
        {
            // Capture the snapshot that is being written BEFORE the await (the race fix): an edit landing during
            // the file I/O must not be marked clean — the save point is exactly the snapshot the file holds.
            Project snapshot = Current!;
            await _service.Save(snapshot, path);
            lock (_gate)
            {
                FilePath = path;
                IsDirty = !ReferenceEquals(Current, snapshot);   // still dirty if an edit slipped in during the write
                _savePoint = snapshot;
            }
            _recent.Add(path);
            // The work is now safely persisted, so the crash backup is stale and the change counter starts
            // over — matching the New/Open/Close transitions.
            _backup.Delete();
            ResetChangeCount();
            RaiseChanged();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project {Path}", path);
            await _dialogs.ShowMessageAsync("Save failed", $"Could not save '{path}':\n{ex.Message}");
            return false;
        }
    }

    private async Task<bool> ConfirmSaveIfDirtyAsync()
    {
        if (!IsDirty)
            return true;
        SaveChangesResult result = await _dialogs.ConfirmSaveChangesAsync(DocumentName);
        return result switch
        {
            SaveChangesResult.Save => await SaveAsync(),
            SaveChangesResult.Discard => true,
            _ => false
        };
    }

    private void NewInternal()
    {
        // English is OpenVisual's product language, so authored projects start from English room names (US-002).
        // The seeding lives in the SDK now (CreateNew's LocalityLanguage option), not an app-side project.Edit().
        Project project = _service.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty),
            language: LocalityLanguage.English);
        SetProject(project, null, dirty: false);
    }

    private void SetProject(Project project, string? path, bool dirty)
    {
        lock (_gate)
        {
            Current = project;
            FilePath = path;
            IsDirty = dirty;
            // A project loaded clean can be returned to; a recovered one (dirty) has no clean state to return to.
            _savePoint = dirty ? null : project;
            _undo.Clear();   // a load starts a fresh, empty edit history (US-052)
            _redo.Clear();
            _version++;
        }
        RaiseChanged();
    }

    private void ResetChangeCount()
    {
        lock (_gate)
        {
            ChangeCount = 0;
        }
    }

    // StartTimer / the auto-backup timer + write lock moved to the AutoBackupScheduler collaborator (T019).

    // Publishes the current state to the GUI. `change` is the incremental edit's change set (reconcile in place) or
    // null (full-rebuild fallback); it is set on LastChange before the event so the triggered refresh reads it.
    private void RaiseChanged(ProjectChangeSet? change = null)
    {
        LastChange = change;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;   // idempotent
        _disposed = true;
        _autoBackup.Dispose();   // stops the timer and waits for any in-flight backup (T019)
    }
}
