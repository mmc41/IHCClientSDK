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
using Ihc.Vis.Editing;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Reporting;
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
public sealed class ProjectSession : IDisposable
{
    private readonly ProjectAppService _service;
    private readonly BackupService _backup;
    private readonly RecentProjectsStore _recent;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ProjectSession> _logger;
    private readonly TimeSpan _autoBackupInterval;
    private readonly int _changeBackupThreshold;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _backupLock = new(1, 1);
    private Timer? _timer;

    private readonly string _catalogDir;

    public ProjectSession(
        ProjectAppService service,
        BackupService backup,
        RecentProjectsStore recent,
        IDialogService dialogs,
        ILoggerFactory? loggerFactory = null,
        TimeSpan? autoBackupInterval = null,
        int changeBackupThreshold = 10,
        string? catalogDir = null)
    {
        _service = service;
        _backup = backup;
        _recent = recent;
        _dialogs = dialogs;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ProjectSession>();
        _autoBackupInterval = autoBackupInterval ?? TimeSpan.FromMinutes(10);
        _changeBackupThreshold = changeBackupThreshold < 1 ? 10 : changeBackupThreshold;
        _catalogDir = catalogDir ?? DefaultCatalogDir();
        LoadPersistedCatalog();   // persisted imports load on startup (US-061)
    }

    /// <summary>The app-data folder persisted catalog imports are copied into and loaded from on startup (US-061).</summary>
    private static string DefaultCatalogDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IHC OpenVisual", "catalog");

    public Project? Current { get; private set; }

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
    // reset the history. Capped so a long session can't grow the snapshot list without bound.
    private const int MaxHistoryDepth = 1000;   // W2-14: the session's bounded-history cap
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
                Project recovered = await _service.Load(_backup.RecoveryProjectPath);
                SetProject(recovered, info?.OriginPath, dirty: true);
                ResetChangeCount();
                StartTimer();
                return;
            }
            _backup.Delete();
        }

        NewInternal();
        StartTimer();
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

    /// <summary>
    /// Creates a reciprocal follow-link between two pins (US-022/US-023): <paramref name="draggedPinId"/> is the
    /// SOURCE and receives the <c>link_from_resource</c> half, <paramref name="dropTargetPinId"/> is the SINK and
    /// receives the <c>link_to_resource</c> half, each naming the other's full path — the orientation IHC Visual
    /// writes in every authored file. Commits, marks dirty. Returns false (with a diagnostic) when a pin is
    /// missing or the pair is one the vendor refuses (see <c>ProjectEditor.CanLink</c>).
    /// </summary>
    /// <summary>Builds the render-ready installation report model for the open project (US-040), or null if none.</summary>
    public InstallationReport? GenerateInstallationReport() =>
        Current is { } project ? _service.GenerateInstallationReport(project) : null;

    /// <summary>Builds the render-ready end-user report model for the open project (US-040), or null if none.</summary>
    public EndUserReport? GenerateEndUserReport() =>
        Current is { } project ? _service.GenerateEndUserReport(project) : null;

    /// <summary>Builds the render-ready function-block documentation report model for the open project (US-041), or
    /// null when no project is open.</summary>
    public FunctionBlockReport? GenerateFunctionBlockReport() =>
        Current is { } project ? _service.GenerateFunctionBlockReport(project) : null;

    /// <summary>
    /// Writes a rendered report HTML page to a temp file (US-040) and returns its path for the browser to open;
    /// null on failure. The file is a self-contained static page — no controller contact.
    /// </summary>
    public async Task<string?> WriteReportHtmlAsync(string fileStem, string html)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{nameof(WriteReportHtmlAsync)}");
        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "ihc-openvisual-reports");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileStem + ".html");
            await File.WriteAllTextAsync(path, html, System.Text.Encoding.UTF8);
            return path;
        }
        catch (Exception ex)
        {
            Ihc.ActivityExtensions.SetError(activity, ex);
            _logger.LogError(ex, "Failed to write report HTML {Stem}", fileStem);
            await _dialogs.ShowMessageAsync("Report failed", ex.Message);
            return null;
        }
    }

    /// <summary>Reads the current project/customer/installer information (US-039) to prefill the dialog. Delegates
    /// to the SDK projection (<c>Ihc.Vis.ProjectProjections</c>); removed once the VM calls the session query (W2-12).</summary>
    public ProjectInfoData GetProjectInfo() => Current?.GetProjectInfo() ?? ProjectInfoData.Empty;

    /// <summary>
    /// Applies edited project information (US-039): writes the <c>project_info</c> metadata and the
    /// <c>customer_info</c>/<c>installer_info</c> contact attributes by id (blank clears to the DTD default). This
    /// identifies the installation in the generated reports. Commits, marks dirty. Returns false on failure.
    /// </summary>
    public async Task<bool> UpdateProjectInfoAsync(ProjectInfoData data) =>
        (await RouteAsync(new UpdateProjectInfo(data), "Project information failed"))
            .Status is EditStatus.Committed or EditStatus.NoChange;

    /// <summary>The dedicated user enum definition that holds the data-tables "user-defined texts" (US-049).</summary>
    public const string UserTextsTableName = ProjectProjections.UserTextsTableName;

    /// <summary>
    /// Reads the project's data tables (US-049): the read-only system tables (the built-in <c>typeid</c>-bearing enum
    /// definitions) and the editable user-defined texts (the values of the <see cref="UserTextsTableName"/> enum).
    /// Delegates to the SDK projection; removed once the VM calls the session query (W2-12).
    /// </summary>
    public DataTablesModel GetDataTables() => Current?.GetDataTables() ?? new DataTablesModel([], []);

    /// <summary>
    /// Names the wireless products in the project not yet linked to the controller (US-042 pre-flight): the offline
    /// half of the "warn about unlinked wireless products before sending" check. Delegates to the SDK projection.
    /// </summary>
    public IReadOnlyList<string> GetUnlinkedWirelessProducts() =>
        Current?.GetUnlinkedWirelessProducts() ?? new List<string>();

    /// <summary>
    /// Builds the read-only Wired module address map (US-050): every addressed <c>dataline_input</c>/<c>dataline_output</c>
    /// terminal across all products, decoded to its <c>line.terminal</c> address and split into input/output modules,
    /// sorted by address. Delegates to the SDK projection; removed once the VM calls the session query (W2-12).
    /// </summary>
    public ModuleAddressMap GetModuleAddressMap() =>
        Current?.GetModuleAddressMap() ?? new ModuleAddressMap([], []);

    /// <summary>Appends a user-defined text (US-049), creating the user-texts table on first use. Returns false on failure.</summary>
    /// <summary>Builds the command to append a user-defined text (US-049), reporting whether the user-texts table
    /// already exists so the command creates it on first use.</summary>
    public ProjectCommand BuildAddUserText(string text) =>
        new AddUserText(text, Current is { } project && project.Child("enum_definitions")?.ChildrenOrEmpty()
            .Any(c => c.Tag == "enum_definition" && project.View(c).Name == UserTextsTableName) == true);

    public async Task<bool> AddUserTextAsync(string text) =>
        (await RouteAsync(BuildAddUserText(text), "Add text failed")).Status is EditStatus.Committed or EditStatus.NoChange;

    /// <summary>Renames a user-defined text by id (US-049 Edit). Returns false on failure.</summary>
    public async Task<bool> UpdateUserTextAsync(ElementId textId, string text) =>
        (await RouteAsync(new UpdateUserText(textId, text), "Edit text failed"))
            .Status is EditStatus.Committed or EditStatus.NoChange;

    /// <summary>Deletes a user-defined text by id (US-049 Delete). Returns false on failure.</summary>
    public async Task<bool> DeleteUserTextAsync(ElementId textId) =>
        (await RouteAsync(new DeleteUserText(textId), "Delete text failed")).Status == EditStatus.Committed;


    /// <summary>Whether dragging <paramref name="draggedPin"/> onto <paramref name="dropTargetPin"/> would create a
    /// link — the drag-over hint peer of <see cref="LinkPinsAsync"/>. Applies the SDK's data-flow rule and orientation
    /// (<see cref="ProjectEditor.CanLink"/> / <see cref="Ihc.Vis.Schema.LinkRoles"/>): the dragged pin is the source, the
    /// target the sink; a self-link (a block output onto its own input) is allowed, a crossed or same-family pair
    /// refused. Non-mutating; no controller.</summary>
    public bool CanLinkPins(ElementId draggedPin, ElementId dropTargetPin)
    {
        if (Current is not { } current)
            return false;
        var document = new ProjectDocumentSession();
        document.Open(current, startClean: true);
        return document.CanApply(new LinkPins(draggedPin, dropTargetPin)).Ok;
    }

    // The function block that owns a variable pin (block → section → pin), or null if the pin is not an FB variable
    // (e.g. a product pin). Used to detect and constrain function-block-to-function-block links (US-033b).
    public async Task<bool> LinkPinsAsync(ElementId draggedPinId, ElementId dropTargetPinId)
    {
        // The dragged pin is the SOURCE and the drop target the SINK. A same-pin drop is a silent no-op; an
        // incompatible pair is refused by LinkPins.Evaluate (the vendor data-flow rule), and we surface that here.
        if (draggedPinId == dropTargetPinId)
            return false;
        EditOutcome outcome = await RouteAsync(new LinkPins(draggedPinId, dropTargetPinId), "Link failed");
        if (outcome.Status == EditStatus.Refused)
        {
            await _dialogs.ShowMessageAsync("Incompatible link",
                "Link a signal source to a signal target: a product input or a function-block output can drive "
                + "a function-block input or a product output. Two product pins must be joined through a "
                + "function block.");
            return false;
        }
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>
    /// Edits an existing scenario link's stored value (US-058): rebuilds the scene member's value from the dialog
    /// result and rewrites it in place via <see cref="ProjectEditor.SetSceneValue"/> (member kind derived from the
    /// row's tag, so id/name/link/note are preserved). Commits, marks dirty. Returns false when the target is not a
    /// relay/dimmer scene member or the edit fails.
    /// </summary>
    public async Task<bool> UpdateSceneValueAsync(ElementId memberId, SceneValueResult r)
    {
        EditOutcome outcome = await RouteAsync(new UpdateSceneValue(memberId, r), "Scene value update failed");
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>
    /// Removes a link by one of its rows (US-057): deletes the selected link half (follow-link "link to"/"link from"
    /// row, or a scene member / scene_link) and cascades its reciprocal partner, so both halves of exactly that link
    /// go together while every other link on the two pins is left intact. Commits, marks dirty. Returns false on
    /// failure.
    /// </summary>
    public async Task<bool> RemoveLinkAsync(ElementId linkRowId)
    {
        EditOutcome outcome = await RouteAsync(new RemoveLink(linkRowId), "Remove link failed");
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>
    /// Creates a scenario link (US-024): wires the function-block scene output pin to the product's scenes container
    /// with the given value — <see cref="SceneValue.Dimmer"/> (light level %, ramp) for a dimmer or
    /// <see cref="SceneValue.Relay"/> (ON/OFF) otherwise. Commits, marks dirty. Returns false on failure.
    /// </summary>
    public async Task<bool> LinkSceneAsync(ElementId sceneOutputId, ElementId scenesId, SceneValueResult r, bool isDimmer)
    {
        EditOutcome outcome = await RouteAsync(new LinkScene(sceneOutputId, scenesId, r, isDimmer), "Scene link failed");
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>The catalog products available for insertion (from the SDK-embedded catalog; no controller needed).</summary>
    public IReadOnlyList<ProductDefinition> GetAvailableProducts() => _service.GetAvailableProducts();

    /// <summary>The catalog library function blocks available for insertion (SDK-embedded catalog; no controller).</summary>
    public IReadOnlyList<FunctionBlockDefinition> GetAvailableFunctionBlocks() => _service.GetAvailableFunctionBlocks();

    /// <summary>The default name a freshly inserted empty function block carries until renamed (US-019).</summary>
    public const string EmptyBlockName = "Empty block";

    /// <summary>
    /// Adds a typed variable (<paramref name="resourceTag"/>, e.g. <c>resource_flag</c>) named <paramref name="name"/>
    /// to a function-block variable section (US-027). The section's tag routes it to the SDK's section adder, which
    /// enforces the section↔type matrix (a pin type into <c>settings</c> is refused). Commits, marks dirty. Returns
    /// the new variable's id, or null when the target is not a block section or the type is not allowed there.
    /// </summary>
    public async Task<ElementId?> AddVariableAsync(ElementId sectionId, string resourceTag, string name)
    {
        Activity.Current?.SetTag("variable.type", resourceTag);
        if (BuildAddVariable(sectionId, resourceTag, name) is not { } command)
            return null;
        EditOutcome<ElementId> outcome = await RouteAsync(command, "Add variable failed");
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    /// <summary>
    /// Creates a project-global enumerator type and inserts a variable of it (US-030): authors an
    /// <c>enum_definition</c> named <paramref name="typeName"/> with the ordered <paramref name="states"/>, then adds a
    /// <c>resource_enum</c> variable named <paramref name="variableName"/> to the block section
    /// <paramref name="sectionId"/> wired to that type (<c>typedef</c> + <c>inivalue</c> of the first state). The type
    /// is global — other blocks can reference it. Returns the new variable's id, or null on failure. No controller.
    /// </summary>
    public async Task<ElementId?> AddEnumVariableAsync(ElementId sectionId, string variableName, string typeName, IReadOnlyList<string> states)
    {
        Activity.Current?.SetTag("enum.type", typeName);
        if (BuildAddEnumVariable(sectionId, variableName, typeName, states) is not { } command)
            return null;
        EditOutcome<ElementId> outcome = await RouteAsync(command, "Add enumerator failed");
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    /// <summary>
    /// Appends any newly-listed states to an existing enumerator type (US-030) — the type referenced by the
    /// <c>resource_enum</c> variable <paramref name="enumVariableId"/>. Only states not already present are added
    /// (the SDK's <c>AddEnumValues</c> is append-only; built-in read-only types are refused). Returns true when at
    /// least the call succeeded (a no-op append still returns true), false on failure or a non-enum target.
    /// </summary>
    /// <summary>Builds the command to append the not-yet-present states to the enumerator type referenced by a
    /// <c>resource_enum</c> variable (US-030), or null for a non-enum target. The caller computes the delta, so an
    /// append of nothing new falls out as a NoChange (the old hand-rolled CommitAsync bypass died in W2-10).</summary>
    public ProjectCommand? BuildUpdateEnumStates(ElementId enumVariableId, IReadOnlyList<string> states)
    {
        if (Current?.FindById(enumVariableId) is not { Tag: "resource_enum" } variable
            || !ElementId.TryParse(variable.GetAttribute("typedef"), out ElementId defId)
            || Current.FindById(defId) is not { } def || def.GetAttribute("name") is not { } defName)
            return null;
        var existing = def.ChildrenOrEmpty().Where(c => c.Tag == "enum_value")
            .Select(c => c.GetAttribute("name")).ToHashSet();
        string[] added = states.Where(s => !existing.Contains(s)).ToArray();
        return new UpdateEnumStates(defName, added);
    }

    public async Task<bool> UpdateEnumStatesAsync(ElementId enumVariableId, IReadOnlyList<string> states) =>
        BuildUpdateEnumStates(enumVariableId, states) is { } command
        && (await RouteAsync(command, "Edit enumerator failed")).Status is EditStatus.Committed or EditStatus.NoChange;

    /// <summary>
    /// Authors a program <c>event</c> (US-028): appends an event to the program owning <paramref name="containerId"/>
    /// (the selected <c>events</c> node), triggered by the resource <paramref name="variableId"/> per the vendor
    /// <paramref name="method"/> token. The stored <paramref name="name"/> keeps the vendor <c>%P</c> template so it
    /// stays live if the variable is renamed. Returns false (with a diagnostic) when the target is not a program's
    /// events container. Read-add over the project; no controller contact.
    /// </summary>
    // Resolves the program owning an `events` container (US-028/US-033), or null when the target is not one.
    private ElementId? ProgramOfEventsContainer(ElementId containerId) =>
        Current?.FindById(containerId)?.Tag == "events"
            && Current.FindParent(containerId) is { Tag: "program_simple", Id: { } programId }
            ? programId : null;

    /// <summary>Builds the command to add a resource-triggered program event to an `events` container (US-028), or
    /// null when the target is not a program's events container.</summary>
    public ProjectCommand? BuildAddProgramEvent(ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        ProgramOfEventsContainer(containerId) is { } programId
            ? new AddProgramEvent(programId, variableId, method, name, note) : null;

    /// <summary>Builds the command to add a Powerup system event to an `events` container (US-033), or null for a
    /// non-events target.</summary>
    public ProjectCommand? BuildAddPowerEvent(ElementId eventsContainerId) =>
        ProgramOfEventsContainer(eventsContainerId) is { } programId ? new AddPowerEvent(programId) : null;

    /// <summary>Builds the command to add a case-value branch to a `program_case` (US-031), or null for a non-case
    /// target, a missing switch, or an enum switch (whose case values need the type's states).</summary>
    public ProjectCommand? BuildAddCaseValue(ElementId caseId, string criterion) =>
        Current?.FindById(caseId) is { Tag: "program_case" } kase
            && ElementId.TryParse(Current.View(kase).Effective("link"), out ElementId switchId)
            && Current.FindById(switchId) is { } switchVar && switchVar.Kind != ElementKind.EnumResource
            ? new AddCaseValue(caseId, criterion, switchVar.Tag) : null;

    public async Task<bool> AddProgramEventAsync(ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        BuildAddProgramEvent(containerId, variableId, method, name, note) is { } command
        && (await RouteAsync(command, "Add event failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Authors a program <c>action</c> command (US-028): appends a top-level command to the program owning
    /// <paramref name="containerId"/> (the selected <c>actions</c>/"Commands" node), driving the resource
    /// <paramref name="variableId"/> per the vendor <paramref name="method"/> token. Events fire commands
    /// top-to-bottom. Returns false when the target is not a program's actions container.
    /// </summary>
    public async Task<bool> AddProgramCommandAsync(ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        (await RouteAsync(new AddProgramCommand(containerId, variableId, method, name, note), "Add command failed"))
            .Status == EditStatus.Committed;

    /// <summary>
    /// Adds a <c>Powerup</c> system event (US-033) to the program owning <paramref name="eventsContainerId"/> — the
    /// program then runs on controller power-up (also on project transfer and software restart), useful for
    /// re-establishing timer values. Takes no operand. Returns false for a non-events target. No controller.
    /// </summary>
    public async Task<bool> AddPowerEventAsync(ElementId eventsContainerId) =>
        BuildAddPowerEvent(eventsContainerId) is { } command
        && (await RouteAsync(command, "Add Powerup event failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Sets an output's "Save current value" power-loss persistence (US-033): writes <c>backup="yes"|"no"</c> on the
    /// function-block or physical output <paramref name="outputId"/> so its value is restored after a power loss
    /// instead of reset. Returns false when the target is not an output. No controller.
    /// </summary>
    public async Task<bool> SetOutputBackupAsync(ElementId outputId, bool save) =>
        // An idempotent setter succeeds when the backup is now as requested — Committed, or NoChange when it was
        // already that value (the fresh relay defaults backup="yes"). Refused/Failed alone are false.
        (await RouteAsync(new SetOutputBackup(outputId, save), "Save current value failed"))
            .Status is EditStatus.Committed or EditStatus.NoChange;

    /// <summary>
    /// Inserts a conditional sub-program (US-029) into an <c>actions</c> container <paramref name="commandsId"/> (a
    /// program's Commands group or a branch) — a <c>program_sub</c> with a Conditions group and the true/false command
    /// branches. Returns false when the target is not an actions container. No controller contact.
    /// </summary>
    public async Task<bool> AddSubProgramAsync(ElementId commandsId) =>
        (await RouteAsync(new AddSubProgram(commandsId), "Add sub-program failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Adds a <c>condition</c> (US-029) to a Conditions group <paramref name="conditionsId"/>, testing the resource
    /// <paramref name="variableId"/> per the vendor <paramref name="method"/> token (the popup's NOT variant is just a
    /// different token). The stored <paramref name="name"/> keeps the <c>%P</c>/<c>%S</c> template. Returns false when
    /// the target is not a conditions group.
    /// </summary>
    public async Task<bool> AddConditionAsync(ElementId conditionsId, ElementId variableId, string method, string name, string? note) =>
        (await RouteAsync(new AddCondition(conditionsId, variableId, method, name, note), "Add condition failed"))
            .Status == EditStatus.Committed;

    /// <summary>
    /// Toggles a Conditions group's logical combination (US-029): <paramref name="or"/> true → OR (<c>&gt;=1</c>),
    /// false → AND (<c>&amp;</c>, the default). Returns false when the target is not a conditions group.
    /// </summary>
    public async Task<bool> SetConditionsLogicAsync(ElementId conditionsId, bool or) =>
        (await RouteAsync(new SetConditionsLogic(conditionsId, or), "Set logic failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Adds a nested logic group (US-029) — a nested <c>conditions</c> group inside <paramref name="conditionsId"/> —
    /// for compound expressions. Returns false when the target is not a conditions group.
    /// </summary>
    public async Task<bool> AddLogicGroupAsync(ElementId conditionsId) =>
        (await RouteAsync(new AddLogicGroup(conditionsId), "Add logic group failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Authors a single arithmetic command line (US-032): one operation on the target register
    /// <paramref name="targetId"/> against <paramref name="operandId"/> per <paramref name="method"/> (add
    /// <c>_0x5a</c> / subtract <c>_0x64</c>), appended to the command container <paramref name="commandsId"/>. One
    /// operation per line by construction — larger formulas are a sequence of these. The stored <paramref name="name"/>
    /// keeps the vendor <c>%P = %P ± %S</c> template. Returns false on a non-command target. No controller.
    /// </summary>
    public async Task<bool> AddArithmeticCommandAsync(ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name) =>
        (await RouteAsync(new AddArithmeticCommand(commandsId, targetId, method, operandId, name), "Add arithmetic failed"))
            .Status == EditStatus.Committed;

    /// <summary>The variable types a case may switch on (US-031): counter, enumerator, weekday, integer, or date.</summary>
    public static readonly HashSet<string> EligibleCaseVariableTags = new()
    {
        "resource_counter", "resource_enum", "resource_weekday", "resource_integer", "resource_date",
    };

    /// <summary>
    /// Inserts a case structure (US-031) into a command container <paramref name="commandsId"/>, keyed on the eligible
    /// switch variable <paramref name="switchVariableId"/> (counter/enum/weekday/integer/date) — a <c>program_case</c>
    /// eagerly allocating its default (Else) branch. Returns false for a non-eligible variable or non-command target.
    /// </summary>
    public async Task<bool> AddCaseAsync(ElementId commandsId, ElementId switchVariableId) =>
        (await RouteAsync(new AddCase(commandsId, switchVariableId), "Add case failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Adds a case value branch (US-031) to a <c>program_case</c> <paramref name="caseId"/> for the literal
    /// <paramref name="criterion"/> — a bare typed operand matching the switch variable's type (e.g. a counter's
    /// <c>&lt;resource_counter inivalue="100"&gt;</c>). Returns false for a non-case target, a missing switch, or an
    /// enum switch (enum case values need the type's states — deferred). No controller.
    /// </summary>
    public async Task<bool> AddCaseValueAsync(ElementId caseId, string criterion) =>
        BuildAddCaseValue(caseId, criterion) is { } command
        && (await RouteAsync(command, "Add case value failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Saves a placed function block to a reusable <c>.ifb</c> catalog file (US-021): lifts the block (by id) to a
    /// keyless user-block definition via <see cref="FunctionBlockRef.ExportDefinition"/> and writes it with
    /// <see cref="Ihc.Vis.Catalog.CatalogFileWriter"/>. Read-only over the project (nothing is mutated, so no dirty
    /// flag). Returns false (with a diagnostic) when the id is not a function block or the write fails.
    /// </summary>
    public async Task<bool> SaveFunctionBlockAsync(ElementId functionBlockId, string filePath, string name, string note)
    {
        if (Current is null)
            return false;
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{nameof(SaveFunctionBlockAsync)}");
        try
        {
            ProjectEditor editor = Current.Edit();
            FunctionBlockDefinition definition = editor.FunctionBlock(functionBlockId).ExportDefinition(
                name, Environment.UserName, DateOnly.FromDateTime(DateTime.Now),
                string.IsNullOrEmpty(note) ? null : note);
            await using FileStream stream = File.Create(filePath);
            Ihc.Vis.Catalog.CatalogFileWriter.Write(definition, stream);
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

    /// <summary>
    /// Unlocks a library function block for editing (US-020): clears its <c>locked</c> flag (to <c>no</c>) by id, so
    /// it becomes editable like a custom block. Commits, marks dirty. Returns false when the id no longer resolves or
    /// the edit fails.
    /// </summary>
    public async Task<bool> UnlockFunctionBlockAsync(ElementId functionBlockId)
    {
        EditOutcome outcome = await RouteAsync(new UnlockFunctionBlock(functionBlockId), "Unlock failed");
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>
    /// Inserts an empty "from scratch" function block into a locality (US-019): scaffolds the catalog's
    /// <c>Tom blok</c> template (the four variable sections + one empty program) named <see cref="EmptyBlockName"/>,
    /// commits, marks dirty. Returns the new block's id, or null when there is no open project or the edit fails.
    /// </summary>
    // ---- Command factories (W2-14): resolve catalog / parent context into a ready-to-apply command (a query, no
    // mutation). The VM and tests apply them via ApplyAsync; the per-op wrappers below now consume them too, so the
    // resolution lives in one place and survives the wrappers' deletion. A null return = "could not be built". ----

    /// <summary>Builds the command to insert the catalog "Tom blok" empty function-block template into a locality
    /// (US-019), stamped with today's date.</summary>
    public ProjectCommand<ElementId> BuildAddEmptyFunctionBlock(ElementId localityId) =>
        new AddEmptyFunctionBlock(localityId, _service.GetEmptyFunctionBlockTemplate(),
            DateOnly.FromDateTime(DateTime.Now), EmptyBlockName);

    /// <summary>Builds the command to insert a preprogrammed library function block by master type (US-018), or null
    /// when no such block is in the catalog.</summary>
    public ProjectCommand<ElementId>? BuildAddFunctionBlock(ElementId localityId, string masterType) =>
        _service.GetAvailableFunctionBlocks().FirstOrDefault(f => f.MasterType == masterType) is { } definition
            ? new AddFunctionBlock(localityId, definition)
            : null;

    /// <summary>Builds the command to add a typed variable to a function-block variable section (US-027), or null
    /// when the section is not a function-block variable section.</summary>
    public ProjectCommand<ElementId>? BuildAddVariable(ElementId sectionId, string resourceTag, string name) =>
        Current?.FindById(sectionId) is { } section
            && Current.FindParent(sectionId) is { Tag: "functionblock", Id: { } blockId }
            ? new AddVariable(blockId, section.Tag, resourceTag, name)
            : null;

    /// <summary>Builds the command to create a project-global enum type and add a variable of it to a function-block
    /// section (US-030), or null when the section is not a function-block variable section.</summary>
    public ProjectCommand<ElementId>? BuildAddEnumVariable(
        ElementId sectionId, string variableName, string typeName, IReadOnlyList<string> states) =>
        Current?.FindById(sectionId) is { } section
            && Current.FindParent(sectionId) is { Tag: "functionblock", Id: { } blockId }
            ? new AddEnumVariable(blockId, section.Tag, variableName, typeName, states)
            : null;

    public async Task<ElementId?> AddEmptyFunctionBlockAsync(ElementId localityId)
    {
        EditOutcome<ElementId> outcome = await RouteAsync(BuildAddEmptyFunctionBlock(localityId), "Insert failed");
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    /// <summary>
    /// Inserts a preprogrammed library function block into a locality (US-018): deep-copies the block identified by
    /// <paramref name="masterType"/> under the <c>group</c> <paramref name="localityId"/> (fresh ids; its variable
    /// sections and program materialized by the SDK), commits, marks dirty. Returns the new block's id, or null when
    /// there is no open project, the id is not a known library block, or the edit fails.
    /// </summary>
    public async Task<ElementId?> AddFunctionBlockAsync(ElementId localityId, string masterType)
    {
        Activity.Current?.SetTag("functionblock.masterType", masterType);
        if (BuildAddFunctionBlock(localityId, masterType) is not { } command)
        {
            await _dialogs.ShowMessageAsync("Insert failed", $"No library function block with master type '{masterType}'.");
            return null;
        }
        EditOutcome<ElementId> outcome = await RouteAsync(command, "Insert failed");
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    /// <summary>
    /// Inserts a catalog product into a locality (US-010): deep-copies the product identified by
    /// <paramref name="productIdentifier"/> under the <c>group</c> <paramref name="localityId"/> (fresh ids, pins and
    /// scenes materialized by the SDK), commits, marks dirty and records the change. Returns the new product's id, or
    /// null when there is no open project, the id is not a known catalog product, or the edit fails.
    /// </summary>
    /// <summary>Builds the command to insert a catalog product by identifier into a locality (US-010), or null when
    /// no such product is in the catalog. The at-most-one-modem rule (US-013) is a separate pre-check —
    /// <see cref="WouldExceedModemLimit"/> — so the caller can surface it before applying.</summary>
    public ProjectCommand<ElementId>? BuildAddProduct(ElementId localityId, string productIdentifier) =>
        _service.GetAvailableProducts().FirstOrDefault(p => p.ProductIdentifier == productIdentifier) is { } definition
            ? new AddProduct(localityId, definition)
            : null;

    /// <summary>Whether inserting the product would break the at-most-one-modem rule (US-013): the product is a modem
    /// and the project already holds one.</summary>
    public bool WouldExceedModemLimit(string productIdentifier) =>
        Current is { } project
        && _service.GetAvailableProducts().FirstOrDefault(p => p.ProductIdentifier == productIdentifier) is { } definition
        && ProductClassifier.IsModem(definition.Body.Tag) && HasModem(project);

    public async Task<ElementId?> AddProductAsync(ElementId localityId, string productIdentifier)
    {
        Activity.Current?.SetTag("product.identifier", productIdentifier);
        if (BuildAddProduct(localityId, productIdentifier) is not { } command)
        {
            await _dialogs.ShowMessageAsync("Insert failed", $"No catalog product with identifier '{productIdentifier}'.");
            return null;
        }
        if (WouldExceedModemLimit(productIdentifier))   // at most one modem per project, regardless of type (US-013)
        {
            Activity.Current?.SetTag("modem.blocked", true);
            await _dialogs.ShowMessageAsync("Only one modem",
                "A project may contain at most one modem. Remove the existing modem before adding another.");
            return null;
        }
        EditOutcome<ElementId> outcome = await RouteAsync(command, "Insert failed");
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    /// <summary>
    /// Inserts a new locality (US-008): appends a room named <see cref="NewLocalityName"/> under the project's
    /// localities, commits, marks the project dirty and records the change. Returns the new locality's id (last in
    /// <see cref="Project.Groups"/>) so the caller can select it, or null when there is no open project or the edit
    /// fails.
    /// </summary>
    public async Task<ElementId?> AddLocalityAsync()
    {
        EditOutcome<ElementId> outcome = await RouteAsync(new AddLocality(NewLocalityName), "Insert failed");
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    /// <summary>
    /// Deletes a locality (US-009): removes it and cascades to the products it holds and the commands/conditions
    /// that referenced them (<see cref="DeleteReferencePolicy.CascadeReferences"/>). Dialog-free — the GUI runs the
    /// non-empty-locality confirmation above the session (via <see cref="PreviewDelete"/>, W2-13) so no dialog lives
    /// inside the edit path. Returns false when the id is absent or the edit fails.
    /// </summary>
    public async Task<bool> DeleteLocalityAsync(ElementId id) =>
        (await RouteAsync(new DeleteLocality(id), "Delete failed")).Status == EditStatus.Committed;

    /// <summary>Raised after a catalog import changes the available products/function blocks (US-059/US-060), so the
    /// insertion menus can be rebuilt.</summary>
    public event EventHandler? CatalogChanged;

    private static IEnumerable<string> EnumerateCatalogFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".def", System.StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".ifb", System.StringComparison.OrdinalIgnoreCase));

    // Loads persisted imports on startup (US-061), best-effort: a single unreadable persisted file is skipped (logged),
    // it does not stop the load or crash startup (the folder-stop rule is for interactive folder imports, US-062).
    private void LoadPersistedCatalog()
    {
        try
        {
            if (!Directory.Exists(_catalogDir))
                return;
            foreach (string file in EnumerateCatalogFiles(_catalogDir))
            {
                try
                {
                    _service.ImportCatalogFile(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipped unreadable persisted catalog file {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load persisted catalog from {Dir}", _catalogDir);
        }
    }

    private void PersistCatalogFile(string path)
    {
        Directory.CreateDirectory(_catalogDir);
        File.Copy(path, Path.Combine(_catalogDir, Path.GetFileName(path)), overwrite: true);
    }

    /// <summary>
    /// Imports a single product (<c>.def</c>) or function-block (<c>.ifb</c>) definition file (US-059); when
    /// <paramref name="persist"/> is set it is also copied into the app-data catalog folder so it loads on later
    /// startups (US-061). On success the component appears in <c>GetAvailableProducts</c>/<c>GetAvailableFunctionBlocks</c>
    /// and <see cref="CatalogChanged"/> fires. On failure the available set is unchanged and the error **names the
    /// file** (US-062). Returns true on success. No controller.
    /// </summary>
    public async Task<bool> ImportCatalogFileAsync(string path, bool persist)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{nameof(ImportCatalogFileAsync)}");
        try
        {
            _service.ImportCatalogFile(path);
            if (persist)
                PersistCatalogFile(path);
            CatalogChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Ihc.ActivityExtensions.SetError(activity, ex);
            _logger.LogError(ex, "Failed to import catalog file {File}", path);
            await _dialogs.ShowMessageAsync("Import failed",
                $"'{Path.GetFileName(path)}' is not a valid product or function-block definition file:\n{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Imports every <c>.def</c>/<c>.ifb</c> definition in a folder and its subfolders (US-060), optionally persisting
    /// each (US-061). Returns the number of components imported; a **missing folder** returns -1 (reported, not silently
    /// ignored). It **stops at the first unreadable file**, naming it, keeping the files imported before it (US-062).
    /// Fires <see cref="CatalogChanged"/>. No controller.
    /// </summary>
    public async Task<int> ImportCatalogFolderAsync(string dir, bool persist)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{nameof(ImportCatalogFolderAsync)}");
        if (!Directory.Exists(dir))
        {
            await _dialogs.ShowMessageAsync("Import failed", $"The folder '{dir}' does not exist.");
            return -1;
        }
        int count = 0;
        try
        {
            foreach (string file in EnumerateCatalogFiles(dir).OrderBy(f => f, System.StringComparer.Ordinal))
            {
                try
                {
                    _service.ImportCatalogFile(file);
                    if (persist)
                        PersistCatalogFile(file);
                    count++;
                }
                catch (Exception ex)
                {
                    Ihc.ActivityExtensions.SetError(activity, ex);
                    _logger.LogError(ex, "Folder import stopped at {File}", file);
                    await _dialogs.ShowMessageAsync("Import stopped",
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

    // The node types US-053 can delete: products, function blocks, variables/pins, and program elements. Structural
    // containers (sections, event/command/conditions groups, programs) and metadata are not user-deletable.
    private static bool IsDeletableNode(string tag) =>
        tag.StartsWith("product_", System.StringComparison.Ordinal) || tag == "functionblock"
        || tag.StartsWith("resource_", System.StringComparison.Ordinal)
        || tag.StartsWith("dataline_", System.StringComparison.Ordinal)
        || tag.StartsWith("airlink_", System.StringComparison.Ordinal)
        || tag is "event" or "event_power" or "action" or "condition" or "program_sub" or "program_case" or "case_action";

    private static bool HasLinkHalves(ProjectElement element) =>
        element.DescendantsAndSelf().Any(d => d.Tag is "link_to_resource" or "link_from_resource" or "scene_link");

    private bool WouldThrowStrict(ElementId id)
    {
        try
        {
            Current!.Edit().DeleteById(id, DeleteReferencePolicy.Strict);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;   // a program row still references the subtree — deletion needs the cascade
        }
    }

    /// <summary>The non-mutating impact of deleting a node (US-009/US-053), for the GUI's confirm-before-delete
    /// flow (W2-13): whether the node can be deleted at all, and whether deleting it needs confirmation because it
    /// cascades — a locality that still holds contents, or any other node other logic references (links and/or
    /// program rows). Presentation composes the confirmation wording; this decides only whether one is needed.</summary>
    public readonly record struct DeleteImpact(bool Deletable, bool NeedsConfirm);

    /// <summary>
    /// The non-mutating impact of deleting <paramref name="id"/> (W2-13): drives the GUI's confirm-before-delete
    /// without a dialog below the session. A locality needs confirmation when it still holds contents (US-009);
    /// any other deletable node needs it when other logic references it (link halves, or a program row the strict
    /// delete would trip over). A missing or non-deletable node reports <see cref="DeleteImpact.Deletable"/> false.
    /// </summary>
    public DeleteImpact PreviewDelete(ElementId id)
    {
        if (Current?.FindById(id) is not { } element)
            return new DeleteImpact(false, false);
        if (element.Tag == "group")
            return new DeleteImpact(true, !element.Children.IsDefaultOrEmpty);   // US-009 locality cascade
        if (!IsDeletableNode(element.Tag))
            return new DeleteImpact(false, false);
        return new DeleteImpact(true, HasLinkHalves(element) || WouldThrowStrict(id));
    }

    /// <summary>
    /// Deletes any project node and its subtree (US-053), generalising the US-009 locality delete to products,
    /// function blocks, variables/pins and program elements. A referenced node is removed together with the
    /// reciprocal link halves and referencing program rows as **one** undoable step; if the engine cannot safely
    /// cascade a binding it refuses. Dialog-free — the GUI runs the reference-cascade confirmation above the
    /// session (via <see cref="PreviewDelete"/>, W2-13). Returns false when the node is missing/not deletable or
    /// the edit fails/refuses. No controller.
    /// </summary>
    public async Task<bool> DeleteNodeAsync(ElementId id)
    {
        if (Current is not { } project || project.FindById(id) is not { } element || !IsDeletableNode(element.Tag))
            return false;
        bool referenced = HasLinkHalves(element) || WouldThrowStrict(id);
        EditOutcome outcome = await RouteAsync(new DeleteNode(id, referenced), "Cannot delete");
        return outcome.Status == EditStatus.Committed;
    }

    // Whether a source node may be moved/pasted into a target container (US-054/US-056): a product or function block
    // belongs under a locality (group). (Variable/section moves are a later extension.)
    private static bool CanContain(string sourceTag, string targetTag) =>
        (sourceTag.StartsWith("product_", System.StringComparison.Ordinal) || sourceTag == "functionblock")
        && targetTag == "group";

    /// <summary>
    /// Reorders a node among its siblings (US-055): moves it one position up (<paramref name="delta"/> = -1) or down
    /// (+1) within its own container via the id-preserving <see cref="ProjectEditor.MoveSubtree"/> — only the position
    /// changes, so ids and links are untouched and the new order drives report order (US-040). A no-op (false) at the
    /// list ends. Reorders only same-tag siblings (so a locality moves past localities, a product past products).
    /// Undoable. No controller.
    /// </summary>
    /// <summary>Builds the command to reorder a node <paramref name="delta"/> positions among its same-tag siblings
    /// (US-055), or null at the list ends / for a rootless node.</summary>
    public ProjectCommand? BuildReorderNode(ElementId id, int delta)
    {
        if (delta == 0 || Current is not { } project
            || project.FindParent(id) is not { Id: { } } parent || project.FindById(id) is not { } node)
            return null;
        var siblings = parent.ChildrenOrEmpty().Where(c => c.Tag == node.Tag).ToList();
        int here = siblings.FindIndex(c => c.Id == id);
        int there = here + delta;
        return here < 0 || there < 0 || there >= siblings.Count ? null : new ReorderNode(id, there);
    }

    public async Task<bool> ReorderNodeAsync(ElementId id, int delta) =>
        BuildReorderNode(id, delta) is { } command && (await RouteAsync(command, "Reorder failed")).Status == EditStatus.Committed;

    /// <summary>Whether <paramref name="dragged"/> and <paramref name="target"/> are distinct <b>same-parent, same-tag
    /// siblings</b> — a reorder drop (US-055), the drag-over hint peer of <see cref="ReorderNodeToSiblingAsync"/>.
    /// Non-mutating; no controller.</summary>
    public bool CanReorderNode(ElementId dragged, ElementId target)
    {
        if (dragged == target
            || Current is not { } project
            || project.FindById(dragged) is not { } a
            || project.FindById(target) is not { } b)
            return false;
        return a.Tag == b.Tag
            && project.FindParent(dragged) is { Id: { } parentId }
            && project.FindParent(target)?.Id == parentId;
    }

    /// <summary>Reorders <paramref name="dragged"/> to <paramref name="targetSibling"/>'s position among their shared
    /// same-tag siblings — the drag-reorder drop (US-055). Id-preserving (the SDK <see cref="ProjectEditor.ReorderSubtree"/>),
    /// undoable. Returns false when the two are not a reorderable pair (not same-parent, same-tag siblings). No controller.</summary>
    /// <summary>Builds the command to reorder <paramref name="dragged"/> to <paramref name="targetSibling"/>'s
    /// position among their shared same-tag siblings (US-055), or null when they are not a reorderable pair.</summary>
    public ProjectCommand? BuildReorderNodeToSibling(ElementId dragged, ElementId targetSibling)
    {
        if (Current is not { } project || project.FindParent(dragged) is not { Id: { } parentId } parent
            || project.FindById(dragged) is not { } node
            || project.FindParent(targetSibling)?.Id != parentId)
            return null;
        int targetIndex = parent.ChildrenOrEmpty().Where(c => c.Tag == node.Tag).ToList().FindIndex(c => c.Id == targetSibling);
        return targetIndex < 0 ? null : new ReorderNode(dragged, targetIndex);
    }

    public async Task<bool> ReorderNodeToSiblingAsync(ElementId dragged, ElementId targetSibling) =>
        BuildReorderNodeToSibling(dragged, targetSibling) is { } command
        && (await RouteAsync(command, "Reorder failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Moves a node to another container (US-054): re-parents the subtree under <paramref name="targetParentId"/> with
    /// its **identity preserved** — the IHC resource ids do not change, so its documentation, addressing and every link
    /// it participates in survive. Refuses an illegal container, a self/descendant target, or a no-op move into the
    /// current parent. Undoable (single snapshot). Returns false on refusal/failure. No controller.
    /// </summary>
    public async Task<bool> MoveNodeAsync(ElementId sourceId, ElementId targetParentId)
    {
        if (Current is not { } project
            || project.FindById(sourceId) is not { } source || project.FindById(targetParentId) is not { } target)
            return false;
        if (project.FindParent(sourceId)?.Id == targetParentId)
        {
            await _dialogs.ShowMessageAsync("Move", "The node is already in that container.");
            return false;
        }
        if (!CanContain(source.Tag, target.Tag))
        {
            await _dialogs.ShowMessageAsync("Cannot move", "That container cannot hold this node.");
            return false;
        }
        EditOutcome outcome = await RouteAsync(new MoveNode(sourceId, targetParentId), "Cannot move");
        if (outcome.Status == EditStatus.Refused)   // self/descendant target (the engine move-contract)
            await _dialogs.ShowMessageAsync("Cannot move", "That container cannot hold this node.");
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>Whether <see cref="MoveNodeAsync"/> would move <paramref name="sourceId"/> under
    /// <paramref name="targetParentId"/> — its non-mutating peer, read for the drag-over hint (A-31). Applies the SAME
    /// legality as the move (and as paste): both ids resolve, the target admits the node's kind, the node is not already
    /// there, and the target is not the node itself or one of its descendants (the SDK's move-contract guard,
    /// <see cref="ProjectEditor.CanMoveSubtree"/>). No dialogs, no mutation, no controller.</summary>
    public bool CanMoveNode(ElementId sourceId, ElementId targetParentId)
    {
        if (Current is not { } current)
            return false;
        var document = new ProjectDocumentSession();
        document.Open(current, startClean: true);
        return document.CanApply(new MoveNode(sourceId, targetParentId)).Ok;
    }

    /// <summary>
    /// Copies a node and pastes it as an **independent duplicate** under <paramref name="targetParentId"/> (US-056):
    /// the SDK <see cref="ProjectEditor.CopySubtree"/> deep-copies the subtree with **fresh IHC resource ids** and
    /// **drops any link half whose other end lies outside the copy** (links wholly inside the copy are duplicated and
    /// stay connected); the original is left unchanged. Refuses an illegal container. Undoable. Returns the new node's
    /// id, or null on refusal/failure. No controller.
    /// </summary>
    public async Task<ElementId?> CopyNodeAsync(ElementId sourceId, ElementId targetParentId)
    {
        if (Current is not { } project
            || project.FindById(sourceId) is not { } source || project.FindById(targetParentId) is not { } target)
            return null;
        if (!CanContain(source.Tag, target.Tag))
        {
            await _dialogs.ShowMessageAsync("Cannot paste", "That container cannot hold this node.");
            return null;
        }
        EditOutcome<ElementId> outcome = await RouteAsync(new CopyNode(sourceId, targetParentId), "Cannot paste");
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    /// <summary>
    /// Applies edited advanced wireless-dimmer settings (US-015): writes the <c>value</c> of the dimmer's
    /// <c>dimmer_setting_*</c> children — fade-rate up/down (soft on/off), dimming rate (manual ramp), minimum/maximum
    /// value, and the load-mode token. Commits, marks dirty. Returns false on failure.
    /// </summary>
    public async Task<bool> UpdateDimmerSettingsAsync(ElementId productId, AdvancedDimmerResult r) =>
        (await RouteAsync(new UpdateDimmerSettings(productId, r), "Update failed")).Status is EditStatus.Committed or EditStatus.NoChange;

    /// <summary>Whether the project already contains a modem device root (the at-most-one-modem rule, US-013).</summary>
    public static bool HasModem(Project project) =>
        project.Root.DescendantsAndSelf().Any(e => ProductClassifier.IsModem(e.Tag));

    /// <summary>
    /// Applies edited modem documentation (US-013): writes the modem's name/note/identification and the four RS485
    /// cabling wire colours by id, the SIM <c>sms_modem_pincode</c> value, and telephone numbers 1..N onto the
    /// matching <c>sms_modem_phonenumber</c> slots; re-parents to the chosen Location when changed. Commits, marks
    /// dirty. Returns false on failure.
    /// </summary>
    /// <summary>Builds the command to apply edited modem documentation (US-013), capturing the modem's current
    /// locality so the command can re-parent it when the Location changed.</summary>
    public ProjectCommand BuildUpdateModem(ElementId modemId, ModemPropertiesResult r) =>
        new UpdateModem(modemId, r, Current?.FindParent(modemId)?.Id);

    public async Task<bool> UpdateModemAsync(ElementId modemId, ModemPropertiesResult r) =>
        (await RouteAsync(BuildUpdateModem(modemId, r), "Update failed")).Status is EditStatus.Committed or EditStatus.NoChange;

    /// <summary>Applies the edited note to a product's scene container (US-024) — the only editable field of the
    /// Scenarier dialog; the container's name is fixed by the product's catalog definition.</summary>
    public async Task<bool> UpdateSceneContainerAsync(ElementId scenesId, string note)
    {
        EditOutcome outcome = await RouteAsync(new UpdateSceneContainer(scenesId, note), "Update failed");
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>
    /// Applies edited terminal addressing to a product input/output pin (US-012): encodes (data line, terminal) into
    /// <c>address_dataline</c> and writes <c>cable_colour</c>, <c>note</c>, and — for an output — the <c>inivalue</c>
    /// (on = normally-closed, off = normally-open). Commits, marks dirty, records the change. Returns false on failure.
    /// </summary>
    /// <summary>Toggles a "Log …" row's log mark (US-068, the vendor's &amp;Logmærke): flips its Logning state between
    /// Off and the first logging mode. Commits, marks dirty. Returns false when the target is not a Logning row.</summary>
    public async Task<bool> ToggleLogMarkAsync(ElementId logRowId) =>
        (await RouteAsync(new ToggleLogMark(logRowId), "Log mark failed")).Status == EditStatus.Committed;

    public async Task<bool> UpdatePinAsync(ElementId pinId, PinPropertiesResult r)
    {
        EditOutcome outcome = await RouteAsync(new UpdatePin(pinId, r), "Addressing failed");
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>
    /// Applies edited product documentation (US-011): writes the product's <c>name</c>/<c>note</c>/<c>cabletype</c>/
    /// <c>cablenumber</c>/<c>documentation_tag</c>/<c>power_group</c> by id (a blank value clears the attribute), and
    /// re-parents the product to the chosen <c>Location</c> locality when it changed (ids preserved via
    /// <see cref="ProjectEditor.MoveSubtree"/>). Commits, marks dirty, records the change. Returns false on failure.
    /// </summary>
    /// <summary>Builds the command to apply edited product documentation (US-011), capturing the product's current
    /// locality so the command can re-parent it when the Location changed.</summary>
    public ProjectCommand BuildUpdateProduct(ElementId productId, ProductPropertiesResult r) =>
        new UpdateProduct(productId, r, Current?.FindParent(productId)?.Id);

    public async Task<bool> UpdateProductAsync(ElementId productId, ProductPropertiesResult r) =>
        (await RouteAsync(BuildUpdateProduct(productId, r), "Update failed")).Status == EditStatus.Committed;

    /// <summary>
    /// Renames a locality (US-007): sets the <c>group</c>'s <c>name</c> and <c>note</c> by id, commits the edited
    /// project, and records the change — which also marks the project dirty and drives the every-Nth-change crash
    /// backup (US-005). Returns false (with a diagnostic) when the id no longer resolves or the edit fails.
    /// </summary>
    public async Task<bool> RenameLocalityAsync(ElementId id, string name, string note)
    {
        EditOutcome outcome = await RouteAsync(new RenameLocality(id, name, note), "Rename failed");
        return outcome.Status == EditStatus.Committed;
    }

    /// <summary>Records one committed edit (the hook editors use in E2+): marks the project dirty and triggers a
    /// crash backup on every Nth change. Fire-and-forget for UI callers; tests await <see cref="MarkChangedAsync"/>.</summary>
    public void MarkChanged() => _ = MarkChangedAsync();

    // The single commit path for every project-mutating operation (US-052): snapshots the pre-edit project for undo,
    // invalidates the redo history, swaps in the new project, then marks changed (dirty + backup + StateChanged).
    // fablerefac W2-5 (migrate): route a command through a document session, then persist the result via the
    // existing commit path so ProjectSession's Current/undo/dirty stay the source of truth. W2-14 contracts this to
    // one persistent session the VM drives directly. A fresh session per call is created on the calling thread,
    // sidestepping the session's thread-affinity guard; it is used once as a stateless command runner.
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
        var document = new ProjectDocumentSession();
        document.Open(Current!, startClean: true);
        EditOutcome outcome = document.Apply(command);
        if (outcome.Status == EditStatus.Committed)
            await CommitAsync(document.Current!, outcome.Label);
        return outcome;
    }

    /// <summary>The value-producing overload of <see cref="ApplyAsync(ProjectCommand,int?)"/> (e.g. a new element's id).</summary>
    public async Task<EditOutcome<T>> ApplyAsync<T>(ProjectCommand<T> command, int? baseVersion = null)
    {
        if (StaleOrClosed(command, baseVersion) is { } refusal)
            return new EditOutcome<T>(refusal.Status, refusal.Label, refusal.Reason, null, default);
        var document = new ProjectDocumentSession();
        document.Open(Current!, startClean: true);
        EditOutcome<T> outcome = document.Apply(command);
        if (outcome.Status == EditStatus.Committed)
            await CommitAsync(document.Current!, outcome.Label);
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

    /// <summary>The command's legality verdict against the open project (cheap — no edit), for drag-over probes and
    /// menu gates. Refused when no project is open.</summary>
    public EditVerdict CanApply(ProjectCommand command)
    {
        if (Current is not { } current)
            return EditVerdict.Refuse("No project is open.");
        var document = new ProjectDocumentSession();
        document.Open(current, startClean: true);
        return document.CanApply(command);
    }

    /// <summary>The structural change set the command would produce if applied now, without committing — or null when
    /// it would refuse, fail or make no change. Drives the Preview→confirm→Apply flow (W2-13).</summary>
    public ProjectChangeSet? Preview(ProjectCommand command)
    {
        if (Current is not { } current)
            return null;
        var document = new ProjectDocumentSession();
        document.Open(current, startClean: true);
        return document.Preview(command);
    }

    // The failure-dialog half of the legacy per-op wrappers: the raw ApplyAsync no longer dialogs, so the wrappers
    // report a Failed outcome the old way. Removed with the wrappers when the VM owns the outcome→dialog mapping.
    private async Task<EditOutcome> RouteAsync(ProjectCommand command, string failureTitle)
    {
        EditOutcome outcome = await ApplyAsync(command);
        await ReportFailureAsync(outcome, failureTitle);
        return outcome;
    }

    private async Task<EditOutcome<T>> RouteAsync<T>(ProjectCommand<T> command, string failureTitle)
    {
        EditOutcome<T> outcome = await ApplyAsync(command);
        await ReportFailureAsync(outcome, failureTitle);
        return outcome;
    }

    private async Task ReportFailureAsync(EditOutcome outcome, string failureTitle)
    {
        if (outcome.Status == EditStatus.Failed)
        {
            _logger.LogError("Edit failed: {Reason}", outcome.Reason);
            await _dialogs.ShowMessageAsync(failureTitle, outcome.Reason ?? "The edit failed.");
        }
    }

    private async Task CommitAsync(Project updated, string label = "Edit")
    {
        lock (_gate)
        {
            if (Current is not null)
            {
                _undo.Add((Current, label));
                if (_undo.Count > MaxHistoryDepth)
                    _undo.RemoveAt(0);
            }
            _redo.Clear();
            Current = updated;
            _version++;
        }
        await MarkChangedAsync();
    }

    /// <summary>
    /// The single edit envelope for every project-mutating operation: the null-project guard, the telemetry
    /// activity, and the try/catch that reports a failure as <c>SetError</c> + a logged error + an error dialog. The
    /// <paramref name="mutate"/> callback applies the edit to a fresh <see cref="ProjectEditor"/> over the current
    /// <see cref="Project"/> and returns <c>true</c> to commit or <c>false</c> to abort silently (a failed guard);
    /// on commit the edited project is swapped in via <see cref="CommitAsync"/> and <paramref name="result"/>
    /// computes the return value from it. Returns <paramref name="onFail"/> when there is no open project, a guard
    /// aborts, or the edit throws. A thrown <see cref="InvalidOperationException"/> (an engine refusal) uses
    /// <paramref name="refusalTitle"/> for the dialog when one is given, otherwise <paramref name="failureTitle"/>.
    /// </summary>
    private async Task<T> RunEditAsync<T>(
        string op, string failureTitle,
        Func<Project, ProjectEditor, Task<bool>> mutate,
        Func<Project, T> result, T onFail,
        string? refusalTitle = null)
    {
        if (Current is null)
            return onFail;
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{op}");
        try
        {
            ProjectEditor editor = Current.Edit();
            if (!await mutate(Current, editor))
                return onFail;   // a guard aborted: nothing committed, nothing dirty
            Project updated = editor.ToProject();
            await CommitAsync(updated);
            return result(updated);
        }
        catch (Exception ex)
        {
            Ihc.ActivityExtensions.SetError(activity, ex);
            _logger.LogError(ex, "Edit {Op} failed", op);
            await _dialogs.ShowMessageAsync(
                ex is InvalidOperationException && refusalTitle is not null ? refusalTitle : failureTitle, ex.Message);
            return onFail;
        }
    }

    /// <summary>Synchronous-callback overload of <see cref="RunEditAsync{T}(string,string,Func{Project,ProjectEditor,Task{bool}},Func{Project,T},T,string?)"/>.</summary>
    private Task<T> RunEditAsync<T>(
        string op, string failureTitle,
        Func<Project, ProjectEditor, bool> mutate,
        Func<Project, T> result, T onFail,
        string? refusalTitle = null)
        => RunEditAsync(op, failureTitle, (p, e) => Task.FromResult(mutate(p, e)), result, onFail, refusalTitle);

    /// <summary>Bool-returning convenience: commits and returns <c>true</c>, or <c>false</c> on abort/failure.</summary>
    private Task<bool> RunEditAsync(
        string op, string failureTitle,
        Func<Project, ProjectEditor, Task<bool>> mutate,
        string? refusalTitle = null)
        => RunEditAsync(op, failureTitle, mutate, static _ => true, false, refusalTitle);

    /// <summary>Bool-returning convenience over a synchronous callback.</summary>
    private Task<bool> RunEditAsync(
        string op, string failureTitle,
        Func<Project, ProjectEditor, bool> mutate,
        string? refusalTitle = null)
        => RunEditAsync(op, failureTitle, mutate, static _ => true, false, refusalTitle);

    /// <summary>
    /// Undoes the last project-mutating edit (US-052): restores the previous snapshot, pushes the current one onto the
    /// redo history, and refreshes both panes. A cascading edit (e.g. a non-empty locality delete) reverses as one step
    /// because it was committed as a single snapshot. A no-op (returns false) when there is nothing to undo.
    /// </summary>
    public async Task<bool> UndoAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{nameof(UndoAsync)}");
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
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{nameof(RedoAsync)}");
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

    /// <summary>Records an edit: the document now differs from the file, so it is dirty by definition.</summary>
    internal Task MarkChangedAsync() => NotifyChangedAsync(dirty: true);

    /// <summary>
    /// The shared tail of every change: set the flag, advance the change counter, notify, and take a crash backup
    /// every Nth change. Undo/redo pass a <paramref name="dirty"/> derived from the save point rather than a
    /// constant, because navigating the history can land back on the saved snapshot.
    /// </summary>
    private async Task NotifyChangedAsync(bool dirty)
    {
        bool backup;
        lock (_gate)
        {
            IsDirty = dirty;
            ChangeCount++;
            backup = ChangeCount % _changeBackupThreshold == 0;
        }
        RaiseChanged();
        if (backup)
            await AutoBackupAsync();
    }

    /// <summary>Writes the current project to the recovery location. Invoked by the timer and the change counter;
    /// exposed internally so tests can drive it deterministically without waiting on the timer.</summary>
    internal async Task AutoBackupAsync()
    {
        // Serialize the timer path and the change-threshold path so two backups never write the recovery
        // file concurrently (the atomic File.Replace/File.Move would otherwise race and throw).
        await _backupLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Project? snapshot;
            string? origin;
            lock (_gate)
            {
                snapshot = Current;
                origin = FilePath;
            }
            if (snapshot is null)
                return;
            _backup.EnsureDirectory();
            await _service.Save(snapshot, _backup.RecoveryProjectPath);
            _backup.WriteMarker(origin, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-backup failed");
        }
        finally
        {
            _backupLock.Release();
        }
    }

    private async Task<bool> SaveToAsync(string path)
    {
        try
        {
            await _service.Save(Current!, path);
            lock (_gate)
            {
                FilePath = path;
                IsDirty = false;
                _savePoint = Current;   // this snapshot is now what the file holds
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
        Project project = _service.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
        project = DefaultLocalities.ApplyEnglish(project, _logger);
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

    private void StartTimer()
    {
        _timer ??= new Timer(_ => _ = AutoBackupAsync(), null, _autoBackupInterval, _autoBackupInterval);
    }

    private void RaiseChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _timer?.Dispose();
        _backupLock.Dispose();
    }
}
