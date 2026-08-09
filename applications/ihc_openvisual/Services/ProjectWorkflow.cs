using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
/// history, dirty flag and version), plus the file path, and orchestrates the whole project lifecycle
/// (new/open/save/save-as/close/quit). Enforces the single-project constraint and drives the save-prompt
/// through <see cref="IDialogService"/>. Deliberately Avalonia-free so it is testable headlessly.
/// </summary>
public sealed class ProjectWorkflow : IDisposable
{
    private readonly ProjectAppService _service;
    private readonly RecentProjectsStore _recent;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ProjectWorkflow> _logger;

    // The persistent document (crudarch D01): created via the service door on the first load and re-opened per
    // load/create after. Null only before StartAsync. All mutations happen on the UI thread (D04 contract).
    private IProjectDocument? _document;

    // T019: the non-lifecycle concerns split into their own collaborators — reporting and catalog imports/persist.
    // ProjectWorkflow retains the document lifecycle + editing/history.
    private readonly ProjectReportWorkflow _reports;
    private readonly CatalogImportWorkflow _catalog;

    private readonly string _catalogDir;

    public ProjectWorkflow(
        ProjectAppService service,
        RecentProjectsStore recent,
        IDialogService dialogs,
        ILoggerFactory? loggerFactory = null,
        string? catalogDir = null,
        InstallerIdentityStore? installerIdentity = null,
        DataTableStore? dataTables = null)
    {
        _service = service;
        _recent = recent;
        _dialogs = dialogs;
        // Not defaulted to CreateDefault(): an unconfigured session must not read (or write) the real user's
        // settings file, so tests and design-time instances start from an empty in-memory identity.
        InstallerIdentity = installerIdentity ?? new InstallerIdentityStore(
            Path.Combine(catalogDir ?? DefaultCatalogDir(), "installer.json"));
        // Same rule: the data tables are application state, so an unconfigured session gets its own file.
        DataTables = dataTables ?? new DataTableStore(
            Path.Combine(catalogDir ?? DefaultCatalogDir(), "datatables.json"));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ProjectWorkflow>();
        _catalogDir = catalogDir ?? DefaultCatalogDir();
        _reports = new ProjectReportWorkflow(_service, _dialogs, _logger, () => Current);
        _catalog = new CatalogImportWorkflow(_service, _dialogs, _logger, _catalogDir);
        _catalog.LoadPersisted();   // persisted imports load on startup (US-061)
    }

    /// <summary>The app-data folder persisted catalog imports are copied into and loaded from on startup (US-061).</summary>
    private static string DefaultCatalogDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IHC OpenVisual", "catalog");

    public Project? Current => _document?.Current;

    /// <summary>The installer contact details stamped into every new project (US-002).</summary>
    public InstallerIdentityStore InstallerIdentity { get; }

    /// <summary>The installer's reusable documentation texts (US-049) — application state shared across projects,
    /// exactly as IHC Visual keeps them.</summary>
    public DataTableStore DataTables { get; }

    /// <summary>The SDK command-factory gateway (the single authoring door, D01): builds ready-to-apply
    /// <see cref="ProjectCommand"/>s the VM hands to <see cref="ApplyAsync(ProjectCommand,int?)"/>. Exposes the
    /// stateless planner on the underlying <see cref="ProjectAppService"/> — the app never constructs command
    /// types directly.</summary>
    public ProjectCommands Commands => _service.Commands;

    public string? FilePath { get; private set; }

    /// <summary>Whether the document differs from its last written save point — owned by the document (computed
    /// by reference against the saved snapshot, so undoing back to the saved state reads clean).</summary>
    public bool IsDirty => _document?.IsDirty ?? false;

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

    /// <summary>Start-up entry point: open the project named on the command line, otherwise a fresh empty project
    /// (US-002).
    /// <para><paramref name="startupProjectPath"/> is the file the app was launched on — a double-clicked
    /// <c>.vis</c> or an explicit path argument. A path that cannot be opened reports itself through the ordinary
    /// open-failure dialog and leaves the empty starter project, so a bad association never blocks the
    /// launch.</para></summary>
    public async Task StartAsync(string? startupProjectPath = null)
    {
        // The launch file (BP-11a). OpenAsync is the same door File ▸ Open uses, so the load, the normalization,
        // the recent-list entry and the failure dialog are all the established ones; a failure just falls through
        // to the empty project below.
        if (!string.IsNullOrWhiteSpace(startupProjectPath) && await OpenAsync(startupProjectPath))
            return;

        NewInternal();
    }

    /// <summary>File → New (US-002): prompt to save the open project, then open the standard empty project.</summary>
    public async Task<bool> NewAsync()
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        NewInternal();
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
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open project {Path}", path);
            await _dialogs.ShowMessageAsync("Åbning mislykkedes", $"Kunne ikke åbne '{path}':\n{ex.Message}");
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

    /// <summary>File → Close: prompt to save, then return to a fresh empty project.</summary>
    public async Task<bool> CloseAsync()
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        NewInternal();
        return true;
    }

    /// <summary>Quit gate (US-064): prompt to save. Returns false to cancel the quit.</summary>
    public Task<bool> CanQuitAsync() => ConfirmSaveIfDirtyAsync();

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
    /// Names the wireless products in the project not yet linked to the controller (US-042 pre-flight): the offline
    /// half of the "warn about unlinked wireless products before sending" check. Delegates to the SDK projection.
    /// </summary>
    public IReadOnlyList<string> GetUnlinkedWirelessProducts() =>
        Current?.GetUnlinkedWirelessProducts() ?? new List<string>();

    /// <summary>
    /// Builds the read-only data-line module map (US-050): every input and output data line, each carrying the
    /// module documented on it — type, locality and description — or blank when the line carries none. Delegates
    /// to the SDK projection over <see cref="Current"/>.
    /// </summary>
    public DatalineModuleMap GetDatalineModuleMap() =>
        Current?.GetDatalineModuleMap() ?? DatalineModuleMap.Empty;

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
            await _dialogs.ShowMessageAsync(SaveFailedTitle, ex.Message);
            return false;
        }
        EditOutcome outcome = await ApplyAsync(
            Commands.SaveFunctionBlockToLibrary(project, functionBlockId, name, Environment.UserName, normalizedNote));
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>
    /// Saves a placed block INTO THE LIBRARY (US-021, uxparity <i>Bibliotek ▸ Gem Funktionsblok...</i>). The
    /// reference application asks a name and a note and nothing else — measured 2026-08-04, its dialog has no file
    /// picker — because it writes into its own component folder. This does the same: <c>&lt;name&gt;.ifb</c> into the
    /// app-data catalog folder, then registers it so the block appears under <i>Indsæt ▸ FunktionsBlokke</i>
    /// immediately, which is the whole point of "saving to the library" and is exactly what a picked path elsewhere
    /// on disk would NOT do.
    /// </summary>
    /// <returns>The file written, or null when the export or the library commit failed.</returns>
    public async Task<string?> SaveFunctionBlockToLibraryAsync(ElementId functionBlockId, string name, string note)
    {
        Directory.CreateDirectory(_catalogDir);
        string path = Path.Combine(_catalogDir, LibraryFileName(name));
        if (!await SaveFunctionBlockAsync(functionBlockId, path, name, note))
            return null;
        // persist:false — the file is ALREADY in the catalog folder, so PersistFile would copy it onto itself.
        // This call is for the registration half: it parses the block into the live catalog and fires
        // CatalogChanged, which is what rebuilds the insertion menus.
        await _catalog.ImportFileAsync(path, persist: false);
        return path;
    }

    /// <summary>The block's name as a file name. A block name is free text and may hold characters no file system
    /// accepts ("Kort / Langt tryk" is a real one in the vendor's own sample project), so every invalid character
    /// becomes '_' rather than throwing at the installer.</summary>
    internal static string LibraryFileName(string name)
    {
        var sanitized = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            sanitized.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
        }
        string stem = sanitized.ToString().Trim();
        return (stem.Length == 0 ? "block" : stem) + ".ifb";
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
    public Task<CatalogImportOutcome> ImportCatalogFolderAsync(string dir, bool persist) =>
        _catalog.ImportFolderAsync(dir, persist);


    // The single edit path (US-052): every project-mutating operation routes through the persistent
    // IProjectDocument (ProjectAppService.OpenDocument, crudarch D01) — the document owns snapshots, undo/redo,
    // labels, dirty-by-reference and the version guard; this workflow adds file lifecycle (path, prompts, recent
    // list).
    //
    // DESIGN NOTE (supersedes the 2026-07-20 rejection of a persistent GUI session): that rejection was taken
    // under the old D12 thread-affinity contract, where a persistent session would have forced every
    // session-touching test onto [AvaloniaTest]. crudarch D04 switched ProjectDocumentSession to
    // LOCK-SERIALIZATION (a private monitor; Changed/StateChanged raised outside the lock on the mutating thread),
    // which dissolves that objection: headless tests drive the document from any thread. The contract this GUI
    // upholds in exchange (D04 a–c): ALL document mutations happen on the UI thread, and no continuation is
    // allowed to leave it — hence the assembly-wide ConfigureAwait ban.
    /// <summary>
    /// Applies a command to the open document and commits it on success (W2-14): the single edit entry the VM
    /// drives. Runs the command through the persistent <see cref="IProjectDocument"/> (evaluate → execute →
    /// commit + undo history, labelled by the command), then publishes the change (LastChange + StateChanged).
    /// Returns the raw <see cref="EditOutcome"/>; the caller maps it to status text / dialogs
    /// (the single outcome→status/dialog rule). When <paramref name="baseVersion"/> is supplied and no longer
    /// matches <see cref="Version"/>, the document refuses the edit as stale (a dialog prepared against an older
    /// project).
    /// </summary>
    public Task<EditOutcome> ApplyAsync(ProjectCommand command, int? baseVersion = null) =>
        Task.FromResult(Publish(_document is { } document ? document.Apply(command, baseVersion) : NoDocument(command)));

    /// <summary>The value-producing overload of <see cref="ApplyAsync(ProjectCommand,int?)"/> (e.g. a new element's id).</summary>
    public Task<EditOutcome<T>> ApplyAsync<T>(ProjectCommand<T> command, int? baseVersion = null) =>
        Task.FromResult(Publish(_document is { } document ? document.Apply(command, baseVersion) : NoDocument(command)));

    // The ONE publish-on-commit rule both Apply overloads obey, written once: an outcome the document COMMITTED is
    // the only one that changed the project, so it is the only one whose delta reaches the reconciler. Generic over
    // the outcome so the value-producing overload hands its own EditOutcome<T> straight back to its caller.
    private TOutcome Publish<TOutcome>(TOutcome outcome) where TOutcome : EditOutcome
    {
        if (outcome.Status == EditStatus.Committed)
            RaiseChanged(outcome.Changes);
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
    private const string NoDocumentReason = "Intet projekt er åbent.";

    // Likewise the ONE title over every failed write this workflow reports — saving the project and saving a
    // function block to the library both surface it, and one title for one kind of failure is what the installer
    // learns to recognise.
    private const string SaveFailedTitle = "Lagring mislykkedes";

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
    public Task<bool> UndoAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(UndoAsync)}");
        if (_document?.Undo() is not { Status: EditStatus.Committed } outcome)
            return Task.FromResult(false);
        RaiseChanged(outcome.Changes);
        return Task.FromResult(true);
    }

    /// <summary>Discards the last committed edit as if it never happened — the cancel arm of an
    /// apply → dialog → cancel gesture (a cancelled product insert). Unlike <see cref="UndoAsync"/> the document
    /// restores the snapshot verbatim (a cancelled gesture burns no ids — vendor-measured, uxparity S-12) and
    /// leaves nothing on the redo stack (a gesture that never completed is not redoable). No-op (false) when
    /// there is nothing to roll back.</summary>
    public Task<bool> RollbackAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(RollbackAsync)}");
        if (_document?.Rollback() is not { Status: EditStatus.Committed } outcome)
            return Task.FromResult(false);
        RaiseChanged(outcome.Changes);
        return Task.FromResult(true);
    }

    /// <summary>Re-applies the last undone edit (US-052): the mirror of <see cref="UndoAsync"/>. No-op (false) when the
    /// redo history is empty.</summary>
    public Task<bool> RedoAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectWorkflow)}.{nameof(RedoAsync)}");
        if (_document?.Redo() is not { Status: EditStatus.Committed } outcome)
            return Task.FromResult(false);
        RaiseChanged(outcome.Changes);
        return Task.FromResult(true);
    }

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
            RaiseChanged();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project {Path}", path);
            await _dialogs.ShowMessageAsync(SaveFailedTitle, $"Kunne ikke gemme '{path}':\n{ex.Message}");
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
        // first load, re-opened per load/create after. Each Open resets history and version (US-052). A project
        // loaded clean can be returned to (save point = the opened snapshot); an already-dirty one has no clean
        // state to return to (startClean: false) — which the FACTORY carries too, so the first load opens once
        // instead of building the index twice (review F04).
        if (_document is { } document)
            document.Open(project, startClean: !dirty);
        else
            _document = _service.OpenDocument(project, startClean: !dirty);
        FilePath = path;
        RaiseChanged();
    }

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
        _reports.Dispose();   // drops this process's report viewing directory
        // Close the document too — this workflow is the only type permitted to (arch-enforced), so nobody else can,
        // and a disposed workflow holding an open document's snapshot + full undo history is state nothing can reach.
        _document?.Close();
    }
}
