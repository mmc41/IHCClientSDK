using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ihc;
using ihc_openvisual.Configuration;
using Ihc.Vis;
using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;
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

    private readonly ProjectFindingsWorkflow _findings;
    private readonly CatalogImportWorkflow _catalog;

    private readonly string _catalogDir;

    /// <param name="service">The SDK facade every operation below routes through.</param>
    /// <param name="recent">The recent-projects store the open/save paths update.</param>
    /// <param name="dialogs">Where a refusal or a confirmation is shown.</param>
    /// <param name="loggerFactory">The host's logging pipeline, or none.</param>
    /// <param name="catalogDir">Where persisted catalog imports live.</param>
    /// <param name="installerIdentity">The installer identity store, or a private one.</param>
    /// <param name="dataTables">The data-table store, or a private one.</param>
    /// <param name="post">
    /// The marshal back to the owning thread, supplied by the composition root because that is the only layer
    /// allowed to name a UI framework. Everything below that touches a background result goes through THIS one —
    /// see <see cref="Post"/>. A caller that omits it gets inline invocation, which is right in a single-threaded
    /// test and wrong anywhere else.
    /// </param>
    /// <param name="timeProvider">The clock every debounce and delay in the shell runs on — see <see cref="Time"/>.</param>
    public ProjectWorkflow(
        ProjectAppService service,
        RecentProjectsStore recent,
        IDialogService dialogs,
        ILoggerFactory? loggerFactory = null,
        string? catalogDir = null,
        InstallerIdentityStore? installerIdentity = null,
        DataTableStore? dataTables = null,
        Action<Action>? post = null,
        TimeProvider? timeProvider = null)
    {
        _service = service;
        _recent = recent;
        _dialogs = dialogs;
        Post = post ?? (action => action());
        Time = timeProvider ?? TimeProvider.System;
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
        // The document NAME is what the SDK cannot supply: a Project carries no path, so the source a
        // findings file records has to come from the session that opened it.
        _findings = new ProjectFindingsWorkflow(_service, _dialogs, _logger, () => Current, () => DocumentName);
        _catalog = new CatalogImportWorkflow(_service, _dialogs, _logger, _catalogDir);
        _catalog.LoadPersisted();   // persisted imports load on startup (US-061)
        // LAST, because it reads this workflow: everything it touches (Current, Version, LastChange, Post, Time,
        // StateChanged) is assigned above. Eager rather than lazy so no reader can create it late and miss the
        // document changes that already happened.
        Validation = new ValidationMonitor(this, ValidateStructured, loggerFactory);
    }

    /// <summary>
    /// The marshal back to the thread that owns the document. The composition root supplies it; a collaborator
    /// that binds a background result reads it here rather than taking its own, so the whole app has ONE answer
    /// to "how do I get back to the owning thread".
    /// </summary>
    public Action<Action> Post { get; }

    /// <summary>The clock the shell's debounces and delays run on. A fake one makes every one of them testable.</summary>
    public TimeProvider Time { get; }

    /// <summary>
    /// The continuous whole-project validation over the open document: what it found, and whether that BLOCKS.
    /// <para>
    /// It lives here rather than in the Problemer panel because the blocking answer is a fact about the document
    /// that more than one thing asks — the transfer gate reads it whether or not the panel is open, or built.
    /// </para>
    /// </summary>
    public ValidationMonitor Validation { get; }

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

    /// <summary>The SDK's blank-field decision and its sentence (<see cref="ProjectAppService.MissingRequiredField"/>),
    /// reached the same way the command gateway is: through the service this workflow already holds. The shell
    /// composes nothing — it asks, and reports the answer on whichever surface raised the question.</summary>
    /// <param name="value">The submitted value, untrimmed: the SDK's policy decides what blank means.</param>
    public Problem? MissingRequiredField(string? value) => _service.MissingRequiredField(value);

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
        using OperationScope scope = _telemetry.Start(nameof(StartAsync));
        scope.Activity?.SetTag(AppTelemetryRegistry.Attributes.ProjectSource,
            string.IsNullOrWhiteSpace(startupProjectPath) ? SourceEmpty : SourceStartupArgument);

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
        using OperationScope scope = _telemetry.Start(nameof(NewAsync));
        scope.Activity?.SetTag(AppTelemetryRegistry.Attributes.ProjectSource, SourceEmpty);
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        NewInternal();
        return true;
    }

    /// <summary>File → Open (US-004): prompt to save, then load the chosen file as the single active project.</summary>
    public async Task<bool> OpenAsync(string path)
    {
        using OperationScope scope = _telemetry.Start(nameof(OpenAsync), metrics:
            LoadMetrics);
        scope.AddSharedTag(AppTelemetryRegistry.Attributes.ProjectSource, SourceFile);
        scope.Activity?.SetTag(AppTelemetryRegistry.Attributes.ProjectPath, path);

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
            scope.Activity?.SetTag(AppTelemetryRegistry.Attributes.ProjectFileSize, FileSizeOrNull(path));
            return true;
        }
        catch (Exception ex)
        {
            // BEFORE the dialog and the swallow. The dialog awaits a human, so recording after it would fold
            // arbitrary think-time into the operation - and the `return false` below discards the exception
            // entirely, which is how an open failure used to leave no trace at all.
            scope.SetOutcome(Ihc.OperationOutcome.Failed(ex));
            _logger.LogError(ex, "Failed to open project {Path}", path);
            await RaisedProblemDisplay.ShowAsync(
                _dialogs, OpenFailedTitle, HostProblems.ProjectOpenFailed(path, ex), ex);
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
        using OperationScope scope = _telemetry.Start(nameof(SaveAsync));
        if (Current is null)
            return false;
        return FilePath is null ? await SaveAsAsync() : await SaveToAsync(FilePath);
    }

    /// <summary>File → Save As (US-003): pick a file name and write the project there.</summary>
    public async Task<bool> SaveAsAsync()
    {
        using OperationScope scope = _telemetry.Start(nameof(SaveAsAsync));
        if (Current is null)
            return false;
        string suggested = FilePath is not null ? Path.GetFileName(FilePath) : "Untitled.vis";
        string? path = await _dialogs.PickSaveProjectAsync(_recent.LastDirectory, suggested);
        return path is not null && await SaveToAsync(path);
    }

    /// <summary>File → Close: prompt to save, then return to a fresh empty project.</summary>
    public async Task<bool> CloseAsync()
    {
        using OperationScope scope = _telemetry.Start(nameof(CloseAsync));
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
    public Task ViewReportInBrowserAsync(ReportKind kind, ReportMode mode, ReportFormat format) =>
        _reports.ViewInBrowserAsync(kind, mode, format);

    /// <summary>[Gem som…] for the picked report (T016/R12): file dialog then facade generation to the chosen
    /// file in the picked format — delegates to the ProjectReportWorkflow collaborator.</summary>
    public Task SaveReportAsAsync(ReportKind kind, ReportMode mode, ReportFormat format) =>
        _reports.SaveAsAsync(kind, mode, format);

    /// <summary>[Eksportér…] for the Problemer panel's list (US-085): file dialog then facade export of the
    /// findings the panel handed over — delegates to the ProjectFindingsWorkflow collaborator.</summary>
    public Task ExportFindingsAsync(FindingsExportRequest request) => _findings.ExportAsync(request);

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

    /// <summary>
    /// The whole-project validation run, in its STRUCTURED shape — one finding per problem, with the locations and
    /// category a panel filters, sorts and navigates by.
    /// </summary>
    /// <remarks>
    /// It takes the snapshot as a parameter rather than reading <see cref="Current"/>, and that is the point of the
    /// signature: the caller is a BACKGROUND recompute, which must capture snapshot and version together on the UI
    /// thread before the work starts (ADR-001, host contract step 1). A door reading <see cref="Current"/> itself
    /// would take that read off the owning thread, where the pair is no longer atomic.
    /// </remarks>
    public EquatableArray<ValidationFinding> ValidateStructured(Project project) =>
        _service.ValidateStructured(project);

    /// <summary>The catalog products as slim insert-menu items (<see cref="CatalogItem"/>) — what the insert menu binds to.</summary>
    public IReadOnlyList<CatalogItem> GetProductCatalogItems() => _service.GetProductCatalogItems();

    /// <summary>The catalog function blocks as slim insert-menu items (<see cref="CatalogItem"/>).</summary>
    public IReadOnlyList<CatalogItem> GetFunctionBlockCatalogItems() => _service.GetFunctionBlockCatalogItems();

    /// <summary>
    /// The catalog product an insert-menu leaf stands for, resolved by identifier AND display name — the name is
    /// not decoration, since catalog identifiers are not unique (D22). The rule is the SDK's
    /// (<c>ProjectAppService.ResolveProduct</c>); the menu leaf knows both halves, so it can say which.
    /// </summary>
    public ProductDefinition? ResolveCatalogProduct(string productIdentifier, string displayName) =>
        _service.ResolveProduct(productIdentifier, displayName);

    /// <summary>
    /// The same lookup, with the SDK's CODED PROBLEM when it finds nothing (T043) — what an insert path needs, since
    /// it has to tell the installer why nothing was placed and the reason is the SDK's to word.
    /// </summary>
    public bool TryResolveCatalogProduct(string productIdentifier, string displayName,
        [NotNullWhen(true)] out ProductDefinition? product, [NotNullWhen(false)] out Problem? refusal) =>
        _service.TryResolveProduct(productIdentifier, displayName, out product, out refusal);

    /// <summary>The composed properties dialog for a placed product — its groups, fields, current values, rules and
    /// write targets, all decided by the SDK. Empty when no project is open or the element composes no dialog.</summary>
    public ProductDialogDescriptor GetProductDialog(ElementId productId) =>
        Current is { } project ? _service.GetProductDialog(project, productId) : new ProductDialogDescriptor("", []);

    /// <summary>
    /// The same, composed against a GIVEN project rather than the open one.
    /// <para>Not a convenience over the overload above: a route planner reasons about the snapshot a validation
    /// run saw, which is not necessarily the document as it now stands, so "the open project" is the wrong
    /// question for it to be asking.</para>
    /// </summary>
    public ProductDialogDescriptor GetProductDialog(Project project, ElementId productId) =>
        _service.GetProductDialog(project, productId);

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
        using OperationScope scope = _telemetry.Start(nameof(SaveFunctionBlockAsync));
        string? normalizedNote = string.IsNullOrEmpty(note) ? null : note;
        try
        {
            await _service.ExportFunctionBlock(project, functionBlockId, filePath, name, Environment.UserName,
                note: normalizedNote);
        }
        catch (Exception ex)
        {
            scope.SetOutcome(Ihc.OperationOutcome.Failed(ex));
            _logger.LogError(ex, "Failed to save function block {Id} to {Path}", functionBlockId.ToToken(), filePath);
            // The engine's English message is DETAIL, never the sentence: it goes to the log above, and the
            // installer reads the SDK's own coded CAUSE (D01) — why the export failed, not merely which file it
            // was writing, which the shell's framing already carries as the chain's operation.
            await RaisedProblemDisplay.ShowAsync(
                _dialogs, SaveFailedTitle, HostProblems.BlockExportFailed(name, filePath, ex), ex);
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

    // The ONE "no document is open" refusal for every route through this workflow (review F14) — and it is the
    // SDK's own sentence, FORWARDED rather than a second wording. The session answers this question when it holds
    // no project; this workflow answers it when it holds no document at all, where there is no session to ask. Two
    // separately-authored sentences meant the installer saw "Intet projekt er åbent." or "Der er ikke åbnet et
    // projekt." depending on which layer noticed first. One condition, one sentence (D13).
    private const string NoDocumentReason = EditRefusals.NoProjectOpenRefusal;

    // Likewise the ONE title over every failed write this workflow reports — saving the project and saving a
    // function block to the library both surface it, and one title for one kind of failure is what the installer
    // learns to recognise.
    /// <summary>The title over a project that could not be opened. Named beside its save counterpart so the two
    /// framings of a file operation stay together.</summary>
    private const string OpenFailedTitle = "Åbning mislykkedes";

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
    public Task<bool> UndoAsync() => HistoryStep(nameof(UndoAsync), d => d.Undo());

    /// <summary>Discards the last committed edit as if it never happened — the cancel arm of an
    /// apply → dialog → cancel gesture (a cancelled product insert). Unlike <see cref="UndoAsync"/> the document
    /// restores the snapshot verbatim (a cancelled gesture burns no ids — vendor-measured, uxparity S-12) and
    /// leaves nothing on the redo stack (a gesture that never completed is not redoable). No-op (false) when
    /// there is nothing to roll back.</summary>
    public Task<bool> RollbackAsync() => HistoryStep(nameof(RollbackAsync), d => d.Rollback());

    /// <summary>Re-applies the last undone edit (US-052): the mirror of <see cref="UndoAsync"/>. No-op (false) when the
    /// redo history is empty.</summary>
    public Task<bool> RedoAsync() => HistoryStep(nameof(RedoAsync), d => d.Redo());

    /// <summary>
    /// The shared body of the three history gestures. They differ only in which document call they make, and
    /// the classification and the change-raise below are the part that must not diverge between them.
    /// </summary>
    private Task<bool> HistoryStep(string operation, Func<IProjectDocument, EditOutcome> step) =>
        Task.FromResult(_telemetry.Run(operation, scope =>
        {
            EditOutcome? outcome = _document is null ? null : step(_document);
            if (outcome is not { Status: EditStatus.Committed })
            {
                scope.SetOutcome(ClassifyEdit(outcome));
                return false;
            }
            RaiseChanged(outcome.Changes);
            return true;
        }));

    /// <summary>Where the project in play came from: the vocabulary of the project-source dimension.</summary>
    private const string SourceFile = "file";
    private const string SourceEmpty = "empty";
    private const string SourceStartupArgument = "startup-argument";

    /// <summary>
    /// The file size, or null when it cannot be read. Null rather than 0 or -1: a missing size is not a size,
    /// and a zero would be indistinguishable from an empty file.
    /// </summary>
    private static long? FileSizeOrNull(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception)
        {
            // Reporting a size is never worth failing a save over.
            return null;
        }
    }

    /// <summary>
    /// The workflow's entry point into the instrumentation core. Owner is the type, so spans keep reading
    /// <c>ProjectWorkflow.&lt;operation&gt;</c>.
    /// </summary>
    private readonly OperationTelemetry _telemetry =
        new(AppTelemetryRegistry.Surface, nameof(ProjectWorkflow));

    /// <summary>The binding is IMMUTABLE and its instruments are static, so it is built once rather than per operation.</summary>
    private static readonly Ihc.MetricBinding LoadMetrics =
        Ihc.MetricBinding.For(AppTelemetryRegistry.ProjectLoadDuration);

    /// <summary>The binding is IMMUTABLE and its instruments are static, so it is built once rather than per operation.</summary>
    private static readonly Ihc.MetricBinding SaveMetrics =
        Ihc.MetricBinding.For(AppTelemetryRegistry.ProjectSaveDuration);

    /// <summary>
    /// Turns an edit outcome into the operation's outcome. The three non-committed cases are NOT the same
    /// thing and collapsing them into the returned <c>false</c> is what made an undo that FAILED
    /// indistinguishable from one that had nothing to undo.
    /// </summary>
    private static Ihc.OperationOutcome ClassifyEdit(EditOutcome? outcome) => outcome?.Status switch
    {
        // No document, or nothing on the history: the operation did what it could, which was nothing.
        null or EditStatus.NoChange or EditStatus.Committed => Ihc.OperationOutcome.Ok,
        EditStatus.Refused => Ihc.OperationOutcome.Refused(outcome.Code.Value),
        EditStatus.Failed => Ihc.OperationOutcome.FailedWith(outcome.Code.Value),
        _ => Ihc.OperationOutcome.Ok,
    };

    private async Task<bool> SaveToAsync(string path)
    {
        using OperationScope scope = _telemetry.Start(nameof(SaveToAsync), metrics:
            SaveMetrics);
        scope.AddSharedTag(AppTelemetryRegistry.Attributes.ProjectSource, SourceFile);
        scope.Activity?.SetTag(AppTelemetryRegistry.Attributes.ProjectPath, path);

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
            scope.Activity?.SetTag(AppTelemetryRegistry.Attributes.ProjectFileSize, FileSizeOrNull(path));
            return true;
        }
        catch (Exception ex)
        {
            // BEFORE the dialog and the swallow, for the same reason as the open path above.
            scope.SetOutcome(Ihc.OperationOutcome.Failed(ex));
            _logger.LogError(ex, "Failed to save project {Path}", path);
            await RaisedProblemDisplay.ShowAsync(
                _dialogs, SaveFailedTitle, HostProblems.ProjectSaveFailed(path, ex), ex);
            return false;
        }
    }

    private async Task<bool> ConfirmSaveIfDirtyAsync()
    {
        // Its OWN span, and a child of whatever lifecycle operation is running. The prompt awaits a HUMAN, so
        // left inline its think-time would land in the parent duration and every load/save percentile would
        // measure how fast the user reads rather than how fast the app works.
        using OperationScope scope = _telemetry.Start(nameof(ConfirmSaveIfDirtyAsync));
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

    // Validation is driven by StateChanged like any other reader — the monitor subscribes in its own constructor,
    // so there is nothing to call here and no ordering between the two to get wrong.

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;   // idempotent
        _disposed = true;
        _reports.Dispose();   // drops this process's report viewing directory
        // Detaches its StateChanged handler and cancels any run still in flight, so no pool thread publishes a
        // result about a document this workflow has already closed.
        Validation.Dispose();
        // Close the document too — this workflow is the only type permitted to (arch-enforced), so nobody else can,
        // and a disposed workflow holding an open document's snapshot + full undo history is state nothing can reach.
        _document?.Close();
    }
}
