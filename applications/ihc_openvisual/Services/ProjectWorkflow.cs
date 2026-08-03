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
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.Services;

/// <summary>
/// The single open-document workflow for the window: holds the one <see cref="IProjectDocument"/> (obtained
/// from <see cref="ProjectAppService.OpenDocument"/>, crudarch D01 — the document owns the snapshot, undo/redo
/// history, dirty flag and version), plus the file path and change counter, and orchestrates the whole project
/// lifecycle (new/open/save/save-as/close/quit). Enforces the single-project constraint, drives the save-prompt
/// through <see cref="IDialogService"/>, and runs the crash-recovery auto-backup (10-minute timer + every 10th
/// change). Deliberately Avalonia-free so it is testable headlessly.
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

    // The persistent document (crudarch D01): created via the service door on the first load and re-opened per
    // load/create/recover after. Null only before StartAsync. All mutations happen on the UI thread (D04 contract);
    // the backup worker only READS document.Current, which the lock-serialized session makes legal.
    private IProjectDocument? _document;

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
        TimeProvider? timeProvider = null,
        InstallerIdentityStore? installerIdentity = null)
    {
        _service = service;
        _backup = backup;
        _recent = recent;
        _dialogs = dialogs;
        // Not defaulted to CreateDefault(): an unconfigured session must not read (or write) the real user's
        // settings file, so tests and design-time instances start from an empty in-memory identity.
        InstallerIdentity = installerIdentity ?? new InstallerIdentityStore(
            Path.Combine(catalogDir ?? DefaultCatalogDir(), "installer.json"));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ProjectWorkflow>();
        _timeProvider = timeProvider ?? TimeProvider.System;   // D8: the auto-backup clock/timer, fakeable in tests
        _changeBackupThreshold = changeBackupThreshold < 1 ? 10 : changeBackupThreshold;
        _catalogDir = catalogDir ?? DefaultCatalogDir();
        _reports = new ProjectReportWorkflow(_service, _dialogs, _logger, () => Current);
        _catalog = new CatalogImportWorkflow(_service, _dialogs, _logger, _catalogDir);
        // The auto-backup writer captures the snapshot + origin through the delegate; the change-threshold trigger
        // stays in ApplyAsync's tail (it owns the change counter).
        _autoBackup = new AutoBackupScheduler(_backup, _service, _timeProvider, _logger,
            autoBackupInterval ?? TimeSpan.FromMinutes(10), CaptureBackupSnapshot);
        _catalog.LoadPersisted();   // persisted imports load on startup (US-061)
    }

    // Captures the current project snapshot + origin path for the auto-backup writer. Runs on the backup worker
    // thread: document.Current is a legal off-thread READ (the session is lock-serialized, crudarch D04), and
    // FilePath is only written on the UI thread.
    private (Project? Snapshot, string? Origin) CaptureBackupSnapshot() => (Current, FilePath);

    /// <summary>The app-data folder persisted catalog imports are copied into and loaded from on startup (US-061).</summary>
    private static string DefaultCatalogDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IHC OpenVisual", "catalog");

    public Project? Current => _document?.Current;

    /// <summary>The installer contact details stamped into every new project (US-002).</summary>
    public InstallerIdentityStore InstallerIdentity { get; }

    /// <summary>The SDK command-factory gateway (the single authoring door, D01): builds ready-to-apply
    /// <see cref="ProjectCommand"/>s the VM hands to <see cref="ApplyAsync(ProjectCommand,int?)"/>. Exposes the
    /// stateless planner on the underlying <see cref="ProjectAppService"/> — the app never constructs command
    /// types directly.</summary>
    public ProjectCommands Commands => _service.Commands;

    public string? FilePath { get; private set; }

    /// <summary>Whether the document differs from its last written save point — owned by the document (computed
    /// by reference against the saved snapshot, so undoing back to the saved state reads clean).</summary>
    public bool IsDirty => _document?.IsDirty ?? false;

    public int ChangeCount { get; private set; }

    /// <summary>A monotone counter bumped on every state transition (commit / undo / redo / load) — the document's
    /// version. A caller that prepares an edit against a dialog can capture it and pass it back to
    /// <see cref="ApplyAsync(ProjectCommand,int?)"/> as the base version; a mismatch means the project moved on and
    /// the stale edit is refused (W2-14).</summary>
    public int Version => _document?.Version ?? 0;

    // The multi-level undo/redo history (US-052) lives on the document (crudarch D01): labelled snapshot entries,
    // unlimited by default (W4-4 — a committed snapshot path-copies only the subtrees it changed, W4-3). These
    // members surface the document's history state for the Edit menu / status bar (E14).

    /// <summary>Whether there is an edit to undo (US-052).</summary>
    public bool CanUndo => _document?.CanUndo ?? false;

    /// <summary>Whether there is an undone edit to redo (US-052).</summary>
    public bool CanRedo => _document?.CanRedo ?? false;

    /// <summary>The label of the edit <see cref="UndoAsync"/> would reverse (e.g. "Insert locality"), or null when
    /// there is nothing to undo — so the status bar and Edit ▸ Undo can name the action (US-052/E14).</summary>
    public string? UndoLabel => _document?.UndoLabel;

    /// <summary>The label of the edit <see cref="RedoAsync"/> would re-apply, or null when nothing to redo (E14).</summary>
    public string? RedoLabel => _document?.RedoLabel;

    /// <summary>The document name shown in the title bar: <c>Untitled</c> before the first save, else the file name.</summary>
    public string DocumentName => FilePath is null ? Constants.UntitledDocument : Path.GetFileName(FilePath);

    /// <summary>Raised whenever the current project, file path or dirty flag changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>The structural change set of the most recent committed transition — the delta the GUI reconciler
    /// (W3-4) applies to the tree in place — INCLUDING undo/redo (crudarch G3: their outcomes carry the exact
    /// delta with Origin "undo"/"redo"); null when the last transition was a load, save or close, which the GUI
    /// handles with a full rebuild. Set immediately before <see cref="StateChanged"/> fires, so the refresh that
    /// event triggers reads the change that caused it.</summary>
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
            // Opening is not a passive read: the catalog enums are re-hoisted with fresh ids, so a file opened and
            // saved back unchanged legitimately differs from the file that was opened. Done before SetProject so it
            // lands outside the undo history and leaves the document clean — it is part of opening, not an edit.
            Project loaded = _service.NormalizeOnOpen(await _service.Load(path));
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

    /// <summary>The placeholder name a freshly inserted locality carries until the installer renames it (US-008).
    /// It is written into the file as <c>&lt;group name="…"&gt;</c> — project data, not UI text — so it is the
    /// format's own placeholder rather than an English one.</summary>
    public const string NewLocalityName = "Lokalitet";

    /// <summary>Generates the picked report in the picked format (facade; SVG icons for HTML) to a temp file
    /// and opens it in the default browser (T015/R12) — delegates to the ProjectReportWorkflow collaborator.</summary>
    public Task ViewReportInBrowserAsync(ReportKind kind, ReportMode mode, string mimeType) =>
        _reports.ViewInBrowserAsync(kind, mode, mimeType);

    /// <summary>[Gem som…] for the picked report (T016/R12): file dialog then facade generation to the chosen
    /// file in the picked format — delegates to the ProjectReportWorkflow collaborator.</summary>
    public Task SaveReportAsAsync(ReportKind kind, ReportMode mode, string mimeType) =>
        _reports.SaveAsAsync(kind, mode, mimeType);

    /// <summary>Reads the current project/customer/installer information (US-039) to prefill the dialog. Delegates
    /// to the SDK projection (<c>Ihc.Vis.ProjectProjections</c>) over <see cref="Current"/>.</summary>
    public ProjectInfoData GetProjectInfo() => Current?.GetProjectInfo() ?? ProjectInfoData.Empty;

    /// <summary>
    /// Reads the project's data tables (US-049): the read-only system tables (the built-in <c>typeid</c>-bearing enum
    /// definitions) and the editable user-defined texts (the values of the <see cref="ProjectProjections.UserTextsTableName"/> enum).
    /// Delegates to the SDK projection over <see cref="Current"/>.
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
    /// sorted by address. Delegates to the SDK projection over <see cref="Current"/>.
    /// </summary>
    public ModuleAddressMap GetModuleAddressMap() =>
        Current?.GetModuleAddressMap() ?? ModuleAddressMap.Empty;

    /// <summary>
    /// The variable types the engine accepts directly under <paramref name="containerId"/>, as SDK tags (US-027,
    /// uxparity2 W1/D03). The variable palette labels these; it never decides membership itself, so the section→types
    /// rule has exactly one home — <c>PlacementRules</c> — and cannot drift between engine and UI. Empty for a
    /// container that holds no variables, and with no project open.
    /// </summary>
    public IReadOnlyList<string> GetInsertableVariableTypes(ElementId containerId) =>
        Current is { } project ? _service.GetInsertableVariableTypes(project, containerId) : [];

    /// <summary>The catalog products as slim insert-menu items (<see cref="CatalogItem"/>) — what the insert menu binds to.</summary>
    public IReadOnlyList<CatalogItem> GetProductCatalogItems() => _service.GetProductCatalogItems();

    /// <summary>The catalog function blocks as slim insert-menu items (<see cref="CatalogItem"/>).</summary>
    public IReadOnlyList<CatalogItem> GetFunctionBlockCatalogItems() => _service.GetFunctionBlockCatalogItems();

    /// <summary>The default name a freshly inserted empty function block carries until renamed (US-019). Written into
    /// the file as the block's <c>name</c> — project data like <see cref="NewLocalityName"/>, so it is the format's own
    /// placeholder rather than an English one.</summary>
    public const string EmptyBlockName = "Tom blok";


    /// <summary>
    /// Saves a placed function block to the library (US-021, PG-3a) in the D05 two-step order: exports it to a
    /// keyless <c>.ifb</c> file FIRST (<see cref="ProjectAppService.ExportFunctionBlock(Project, ElementId, string, string, string, DateOnly?, string?)"/>,
    /// atomic write, clock-defaulted date, app-supplied author — a failed export returns false with a diagnostic
    /// and the document is untouched), THEN commits the in-project transform through the document
    /// (<see cref="ProjectCommands.SaveFunctionBlockToLibrary"/> via <see cref="ApplyAsync(ProjectCommand,int?)"/>):
    /// rename + <c>master_*</c> + badge + <c>locked="yes"</c>, one undoable step — one undo restores the prior
    /// unlocked block. The facade's combined helper remains for non-interactive callers; the workflow no longer
    /// adopts externally mutated snapshots (the port has no such API by design).
    /// <para>
    /// <b>Why the D05 order lives here as well as in the facade's combined overloads</b> (review F18 — considered
    /// and declined): a document-aware facade overload could only commit via <c>document.Apply</c>, while the
    /// interactive door must go through <see cref="ApplyAsync(ProjectCommand,int?)"/>, which also runs
    /// <c>NotifyChangedAsync</c> — the change set that drives the tree reconcile plus the dirty/backup
    /// bookkeeping. Moving the two statements into the SDK would therefore force this method to hand-roll the
    /// notify half instead, trading two shared statements for more coupling. What is duplicated is the ORDER,
    /// and each door pins it with its own armed regression test: <c>SaveToLibraryTests</c> (a failing .ifb sink
    /// leaves the project unlocked) for the stateless door, and <c>ProjectWorkflowTests</c> (a bad path leaves
    /// the snapshot, dirty flag, version and history untouched) for this one — so the two cannot drift silently.
    /// </para>
    /// </summary>
    public async Task<bool> SaveFunctionBlockAsync(ElementId functionBlockId, string filePath, string name, string note)
    {
        if (Current is not { } project)
            return false;
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(SaveFunctionBlockAsync)}");
        string? normalizedNote = string.IsNullOrEmpty(note) ? null : note;
        try
        {
            await _service.ExportFunctionBlock(project, functionBlockId, filePath, name, Environment.UserName,
                note: normalizedNote);
        }
        catch (Exception ex)
        {
            Ihc.ActivityExtensions.SetError(activity, ex);
            _logger.LogError(ex, "Failed to save function block {Id} to {Path}", functionBlockId.ToToken(), filePath);
            await _dialogs.ShowMessageAsync("Save failed", ex.Message);
            return false;
        }
        EditOutcome outcome = await ApplyAsync(
            Commands.SaveFunctionBlockToLibrary(project, functionBlockId, name, Environment.UserName, normalizedNote));
        return outcome.Status == EditStatus.Committed;
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


    // The single edit path (US-052): every project-mutating operation routes through the persistent
    // IProjectDocument (ProjectAppService.OpenDocument, crudarch D01) — the document owns snapshots, undo/redo,
    // labels, dirty-by-reference and the version guard; this workflow adds file lifecycle (path, prompts, recent
    // list), the change counter and the crash backup.
    //
    // DESIGN NOTE (supersedes the 2026-07-20 rejection of a persistent GUI session): that rejection was taken
    // under the old D12 thread-affinity contract, where a persistent session would have forced every
    // session-touching test onto [AvaloniaTest] and the backup timer's worker-thread snapshot read would throw.
    // crudarch D04 switched ProjectDocumentSession to LOCK-SERIALIZATION (a private monitor; Changed/StateChanged
    // raised outside the lock on the mutating thread), which dissolves both objections: headless tests drive the
    // document from any thread, and AutoBackupScheduler legally reads document.Current off-thread. The contract
    // this GUI upholds in exchange (D04 a–c): ALL document mutations happen on the UI thread (workers only read),
    // and ConfigureAwait(false) stays confined to paths that never mutate the document or handle its events
    // (today only the backup writer's read-only path).
    /// <summary>
    /// Applies a command to the open document and commits it on success (W2-14): the single edit entry the VM
    /// drives. Runs the command through the persistent <see cref="IProjectDocument"/> (evaluate → execute →
    /// commit + undo history, labelled by the command), then publishes the change (LastChange + StateChanged +
    /// backup counter). Returns the raw <see cref="EditOutcome"/>; the caller maps it to status text / dialogs
    /// (the single outcome→status/dialog rule). When <paramref name="baseVersion"/> is supplied and no longer
    /// matches <see cref="Version"/>, the document refuses the edit as stale (a dialog prepared against an older
    /// project).
    /// </summary>
    public async Task<EditOutcome> ApplyAsync(ProjectCommand command, int? baseVersion = null)
    {
        EditOutcome outcome = _document is { } document
            ? document.Apply(command, baseVersion)
            : NoDocument(command);
        if (outcome.Status == EditStatus.Committed)
            await NotifyChangedAsync(outcome.Changes);
        return outcome;
    }

    /// <summary>The value-producing overload of <see cref="ApplyAsync(ProjectCommand,int?)"/> (e.g. a new element's id).</summary>
    public async Task<EditOutcome<T>> ApplyAsync<T>(ProjectCommand<T> command, int? baseVersion = null)
    {
        EditOutcome<T> outcome = _document is { } document
            ? document.Apply(command, baseVersion)
            : NoDocument(command);
        if (outcome.Status == EditStatus.Committed)
            await NotifyChangedAsync(outcome.Changes);
        return outcome;
    }

    /// <summary>The command's legality verdict against the open document (cheap — reuses the per-commit index, no
    /// edit), for drag-over probes and menu gates. Refused when no project is open.</summary>
    public EditVerdict CanApply(ProjectCommand command) =>
        _document?.CanApply(command) ?? EditVerdict.Refuse(NoDocumentReason);

    /// <summary>Whether the pair is a reorderable same-tag sibling pair (US-055) — the drag-over reorder probe,
    /// answered by the document against its per-commit index so the pointer path pays no full-tree walk
    /// (crudarch T008, review F5; one rule shared with the gateway's <c>Commands.CanReorderNode</c>). False when
    /// no project is open.</summary>
    public bool CanReorderNode(ElementId dragged, ElementId target) =>
        _document?.CanReorderNode(dragged, target) ?? false;

    /// <summary>Whether the node can move <paramref name="delta"/> positions among its same-tag siblings (US-055) —
    /// the index-backed MENU-gate peer of <see cref="CanReorderNode"/> that the Move up/down gates use, so a
    /// selection change costs dictionary lookups instead of two full-tree walks per direction and no command is
    /// minted until Execute (review F02). False when no project is open.</summary>
    public bool CanReorder(ElementId id, int delta) => _document?.CanReorder(id, delta) ?? false;

    /// <summary>The typed preview of a command applied now, without committing (M8/D05): the delta it would commit
    /// when it <see cref="PreviewStatus.WouldChange"/>, else a refuse / no-change / engine-fault status. Drives the
    /// Preview→confirm→Apply flow (W2-13). Currently exercised only by tests — the GUI's delete-confirm flow reads
    /// <see cref="ProjectCommands.PreviewDelete"/> instead.</summary>
    public PreviewOutcome Preview(ProjectCommand command) =>
        _document?.Preview(command) ?? PreviewOutcome.Refused(NoDocumentReason);

    // The ONE "no document is open" refusal for every route through this workflow (review F14): both Apply
    // overloads and both probes answer with the same wording, built the same way, so re-wording it is one edit
    // rather than a hunt across the file. The stale-version guard that used to sit alongside it now lives in the
    // document, where the version does.
    private const string NoDocumentReason = "No project is open.";

    private static EditOutcome NoDocument(ProjectCommand command) =>
        new(EditStatus.Refused, command.GetType().Name, NoDocumentReason, null);

    private static EditOutcome<T> NoDocument<T>(ProjectCommand<T> command) =>
        new(EditStatus.Refused, command.GetType().Name, NoDocumentReason, null, default);

    /// <summary>
    /// Undoes the last project-mutating edit (US-052): the document restores the previous snapshot and reports the
    /// exact delta, which flows to the reconciler via <see cref="LastChange"/> (crudarch G3). A cascading edit
    /// (e.g. a non-empty locality delete) reverses as one step because it was committed as a single snapshot.
    /// A no-op (returns false) when there is nothing to undo.
    /// </summary>
    public async Task<bool> UndoAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(UndoAsync)}");
        if (_document?.Undo() is not { Status: EditStatus.Committed } outcome)
            return false;
        await NotifyChangedAsync(outcome.Changes);
        return true;
    }

    /// <summary>Re-applies the last undone edit (US-052): the mirror of <see cref="UndoAsync"/>. No-op (false) when the
    /// redo history is empty.</summary>
    public async Task<bool> RedoAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(RedoAsync)}");
        if (_document?.Redo() is not { Status: EditStatus.Committed } outcome)
            return false;
        await NotifyChangedAsync(outcome.Changes);
        return true;
    }

    /// <summary>
    /// The shared tail of every committed document mutation: publish the change set (LastChange + StateChanged —
    /// undo/redo included, so the reconciler works in place, crudarch G3), advance the change counter, and take a
    /// crash backup every Nth change. Dirty and version live on the document.
    /// </summary>
    private async Task NotifyChangedAsync(ProjectChangeSet? change)
    {
        ChangeCount++;
        RaiseChanged(change);
        if (ChangeCount % _changeBackupThreshold == 0)
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
            // SaveDocument, not Save: the editor's save keeps the file it replaces as a .BAK side-file.
            await _service.SaveDocument(snapshot, path);
            // The exact written snapshot becomes the save point — the document computes dirty by reference, so an
            // edit that slipped in during the write stays dirty (the race fix).
            _document!.MarkSaved(snapshot);
            FilePath = path;
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
        // English is the product language for UI text only: the default room names land in the .vis as data, so a
        // new project carries the file format's own locality names (US-002) and is interchangeable with the vendor's.
        // The installer identity comes from application settings, the programmer from the signed-in user.
        Project project = _service.CreateNew(InstallerIdentity.NewProjectDetails(), language: LocalityLanguage.Vendor);
        SetProject(project, null, dirty: false);
    }

    private void SetProject(Project project, string? path, bool dirty)
    {
        // One persistent document for the workflow's lifetime (crudarch D01): created via the service door on the
        // first load, re-opened per load/create/recover after. Each Open resets history and version (US-052). A
        // project loaded clean can be returned to (save point = the opened snapshot); a recovered one (dirty) has
        // no clean state to return to (startClean: false) — which the FACTORY carries too, so the first load opens
        // once instead of building the index twice and briefly reporting a recovered project clean (review F04).
        if (_document is { } document)
            document.Open(project, startClean: !dirty);
        else
            _document = _service.OpenDocument(project, startClean: !dirty);
        FilePath = path;
        RaiseChanged();
    }

    private void ResetChangeCount() => ChangeCount = 0;

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
        // Close the document too — this workflow is the only type permitted to (arch-enforced), so nobody else can,
        // and a disposed workflow holding an open document's snapshot + full undo history is state nothing can reach.
        _document?.Close();
    }
}
