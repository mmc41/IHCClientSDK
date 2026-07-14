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
using Ihc.Vis.Editing;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Reporting;
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

    // The multi-level undo/redo history (US-052): immutable project snapshots. Every project-mutating commit goes
    // through CommitAsync, which pushes the pre-edit snapshot here and clears the redo list; loads (New/Open/Close)
    // reset the history. Capped so a long session can't grow the snapshot list without bound.
    private const int MaxHistoryDepth = 100;
    private readonly List<Project> _undo = new();
    private readonly List<Project> _redo = new();

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

    /// <summary>The document name shown in the title bar: <c>Untitled</c> before the first save, else the file name.</summary>
    public string DocumentName => FilePath is null ? Constants.UntitledDocument : Path.GetFileName(FilePath);

    /// <summary>Raised whenever the current project, file path or dirty flag changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Start-up entry point: offer to recover a crash backup if one exists (US-005), otherwise open a
    /// fresh empty project (US-002); then begin the auto-backup timer.</summary>
    public async Task StartAsync()
    {
        if (_backup.HasRecovery())
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
    /// Creates a reciprocal follow-link between two pins (US-022/US-023): the <paramref name="dropTargetPinId"/>
    /// (the pin dragged <i>onto</i>) receives the <c>link_from_resource</c> half and the
    /// <paramref name="draggedPinId"/> the <c>link_to_resource</c> half, each naming the other's full path. Commits,
    /// marks dirty. Returns false (with a diagnostic) when a pin is missing or the link would be invalid.
    /// </summary>
    /// <summary>Builds the render-ready installation report model for the open project (US-040), or null if none.</summary>
    public InstallationReport? GenerateInstallationReport() =>
        Current is { } project ? _service.GenerateInstallationReport(project) : null;

    /// <summary>Builds the render-ready end-user report model for the open project (US-040), or null if none.</summary>
    public EndUserReport? GenerateEndUserReport() =>
        Current is { } project ? _service.GenerateEndUserReport(project) : null;

    /// <summary>The function-block variable sections, in document order, with their display labels. Shared by the
    /// FB report model and the Functions-pane tree/operand projection so both stay in lockstep.</summary>
    public static readonly (string Container, string Label)[] FbVariableSections =
    {
        ("inputs", "Input"), ("outputs", "Output"), ("settings", "Settings"), ("internalsettings", "Internal variables"),
    };

    /// <summary>
    /// Builds a minimal function-block documentation report model (US-041): every function block in Installation/
    /// Functions-pane document order, each with its variable sections. A minimal listing pending the SDK model's deep
    /// per-field internal layout (unspecified). Read-only; no controller.
    /// </summary>
    public FbReport BuildFunctionBlockReport()
    {
        var blocks = ImmutableArray.CreateBuilder<FbReportBlock>();
        if (Current is { } project)
        {
            foreach (ProjectElement group in project.Groups)
            {
                foreach (ProjectElement fb in group.ChildrenOrEmpty().Where(c => c.Tag == "functionblock"))
                {
                    var sections = ImmutableArray.CreateBuilder<FbReportSection>();
                    foreach ((string container, string label) in FbVariableSections)
                    {
                        var vars = fb.FindChild(container)?.ChildrenOrEmpty()
                            .Select(v => v.GetAttribute("name") ?? v.Tag).ToImmutableArray() ?? ImmutableArray<string>.Empty;
                        sections.Add(new FbReportSection(label, vars));
                    }
                    blocks.Add(new FbReportBlock(fb.GetAttribute("name") ?? "block", sections.ToImmutable()));
                }
            }
        }
        return new FbReport("Functionsblok dokumentation", blocks.ToImmutable());
    }

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

    /// <summary>Reads the current project/customer/installer information (US-039) to prefill the dialog.</summary>
    public ProjectInfoData GetProjectInfo()
    {
        if (Current is null)
            return ProjectInfoData.Empty;
        ProjectElement? pi = Current.Child("project_info");
        return new ProjectInfoData(
            Attr(pi, "description"), Attr(pi, "number"), Attr(pi, "programmer"),
            ReadContact(Current.Child("customer_info")), ReadContact(Current.Child("installer_info")));
    }

    private static string Attr(ProjectElement? e, string name) => e?.GetAttribute(name) ?? string.Empty;

    private static ContactInfo ReadContact(ProjectElement? c) => new(
        Attr(c, "name"), Attr(c, "address"), Attr(c, "city"), Attr(c, "zipcode"),
        Attr(c, "country"), Attr(c, "phone"), Attr(c, "mobilephone"), Attr(c, "email"));

    /// <summary>
    /// Applies edited project information (US-039): writes the <c>project_info</c> metadata and the
    /// <c>customer_info</c>/<c>installer_info</c> contact attributes by id (blank clears to the DTD default). This
    /// identifies the installation in the generated reports. Commits, marks dirty. Returns false on failure.
    /// </summary>
    public Task<bool> UpdateProjectInfoAsync(ProjectInfoData data) =>
        RunEditAsync(nameof(UpdateProjectInfoAsync), "Project information failed", (project, editor) =>
        {
            editor.SetMetadata("project_info",
                ("description", data.Description), ("number", data.Number), ("programmer", data.Programmer));
            WriteContact(editor, "customer_info", data.Customer);
            WriteContact(editor, "installer_info", data.Installer);
            return true;
        });

    /// <summary>The dedicated user enum definition that holds the data-tables "user-defined texts" (US-049).</summary>
    public const string UserTextsTableName = "User-defined texts";

    /// <summary>
    /// Reads the project's data tables (US-049): the read-only system tables (the built-in <c>typeid</c>-bearing enum
    /// definitions) and the editable user-defined texts (the values of the <see cref="UserTextsTableName"/> enum).
    /// </summary>
    public DataTablesModel GetDataTables()
    {
        var system = ImmutableArray.CreateBuilder<DataTableView>();
        var texts = ImmutableArray.CreateBuilder<UserText>();
        if (Current?.Child("enum_definitions") is { } container)
        {
            foreach (ProjectElement def in container.ChildrenOrEmpty().Where(c => c.Tag == "enum_definition"))
            {
                var values = def.ChildrenOrEmpty().Where(v => v.Tag == "enum_value").ToList();
                if (def.GetAttribute("name") == UserTextsTableName)
                {
                    foreach (ProjectElement v in values)
                        if (v.Id is { } id)
                            texts.Add(new UserText(id.ToToken(), v.GetAttribute("name") ?? string.Empty));
                }
                else if ((def.GetAttribute("typeid") ?? ElementId.NullToken) != ElementId.NullToken)
                {
                    system.Add(new DataTableView(def.GetAttribute("name") ?? string.Empty,
                        values.Select(v => v.GetAttribute("name") ?? string.Empty).ToImmutableArray()));
                }
            }
        }
        return new DataTablesModel(system.ToImmutable(), texts.ToImmutable());
    }

    /// <summary>
    /// Names the wireless products in the project not yet linked to the controller (US-042 pre-flight): the offline
    /// half of the "warn about unlinked wireless products before sending" check. Read-only; no controller contact.
    /// </summary>
    public IReadOnlyList<string> GetUnlinkedWirelessProducts()
    {
        var names = new List<string>();
        if (Current is { } project)
        {
            foreach (ProjectElement group in project.Groups)
            {
                foreach (ProjectElement product in group.ChildrenOrEmpty())
                {
                    if (ProductKinds.IsUnlinkedWireless(product.Tag, product.GetAttribute("serialnumber")))
                        names.Add(product.GetAttribute("name") ?? product.Tag);
                }
            }
        }
        return names;
    }

    /// <summary>
    /// Builds the read-only Wired module address map (US-050): every addressed <c>dataline_input</c>/<c>dataline_output</c>
    /// terminal across all products, decoded to its <c>line.terminal</c> address and paired with the occupying product
    /// terminal, split into input and output modules and sorted by address. Unaddressed terminals are omitted; wireless
    /// products carry no module addressing so contribute nothing. Read-only; mutates nothing; no controller.
    /// </summary>
    public ModuleAddressMap GetModuleAddressMap()
    {
        var inputs = new List<(int Line, int Terminal, ModuleAddressEntry Entry)>();
        var outputs = new List<(int Line, int Terminal, ModuleAddressEntry Entry)>();
        if (Current is { } project)
        {
            foreach (ProjectElement group in project.Groups)
            {
                foreach (ProjectElement product in group.ChildrenOrEmpty())
                {
                    string productName = product.GetAttribute("name") ?? product.Tag;
                    foreach (ProjectElement pin in product.ChildrenOrEmpty())
                    {
                        bool isOutput = pin.Tag == "dataline_output";
                        if (pin.Tag != "dataline_input" && !isOutput)
                            continue;
                        if (!DatalineAddressing.TryDecode(pin.GetAttribute("address_dataline"),
                                DatalineAddressing.TerminalsPerLine(isOutput), out int line, out int terminal))
                            continue;
                        var entry = new ModuleAddressEntry($"{line}.{terminal}", productName, pin.GetAttribute("name") ?? pin.Tag);
                        (isOutput ? outputs : inputs).Add((line, terminal, entry));
                    }
                }
            }
        }
        return new ModuleAddressMap(SortByAddress(inputs), SortByAddress(outputs));
    }

    private static ImmutableArray<ModuleAddressEntry> SortByAddress(List<(int Line, int Terminal, ModuleAddressEntry Entry)> rows) =>
        rows.OrderBy(r => r.Line).ThenBy(r => r.Terminal).Select(r => r.Entry).ToImmutableArray();

    /// <summary>Appends a user-defined text (US-049), creating the user-texts table on first use. Returns false on failure.</summary>
    public Task<bool> AddUserTextAsync(string text) =>
        RunEditAsync(nameof(AddUserTextAsync), "Add text failed", (project, editor) =>
        {
            bool exists = project.Child("enum_definitions")?.ChildrenOrEmpty()
                .Any(c => c.Tag == "enum_definition" && c.GetAttribute("name") == UserTextsTableName) == true;
            EnumDefinitionRef def = exists ? editor.EnumDefinition(UserTextsTableName) : editor.AddEnumDefinition(UserTextsTableName);
            editor.AddEnumValues(def, text);
            return true;
        });

    /// <summary>Renames a user-defined text by id (US-049 Edit). Returns false on failure.</summary>
    public Task<bool> UpdateUserTextAsync(ElementId textId, string text) =>
        RunEditAsync(nameof(UpdateUserTextAsync), "Edit text failed", (project, editor) =>
        {
            if (!editor.TryResolve(textId, out ElementRef? handle))
                return false;
            handle.SetAttribute("name", text);
            return true;
        });

    /// <summary>Deletes a user-defined text by id (US-049 Delete). Returns false on failure.</summary>
    public Task<bool> DeleteUserTextAsync(ElementId textId) =>
        RunEditAsync(nameof(DeleteUserTextAsync), "Delete text failed", (project, editor) =>
        {
            editor.DeleteById(textId, DeleteReferencePolicy.CascadeReferences);
            return true;
        });

    private static void WriteContact(ProjectEditor editor, string tag, ContactInfo c) =>
        editor.SetMetadata(tag, ("name", c.Name), ("address", c.Address), ("city", c.City),
            ("zipcode", c.Zip), ("country", c.Country), ("phone", c.Phone), ("mobilephone", c.Mobile), ("email", c.Email));

    // The function block that owns a variable pin (block → section → pin), or null if the pin is not an FB variable
    // (e.g. a product pin). Used to detect and constrain function-block-to-function-block links (US-033b).
    private ProjectElement? OwningFunctionBlock(ElementId pinId)
    {
        if (Current?.FindParent(pinId) is not { Id: { } sectionId })
            return null;
        return Current.FindParent(sectionId) is { Tag: "functionblock" } block ? block : null;
    }

    public Task<bool> LinkPinsAsync(ElementId draggedPinId, ElementId dropTargetPinId) =>
        RunEditAsync(nameof(LinkPinsAsync), "Link failed", async (project, editor) =>
        {
            if (draggedPinId == dropTargetPinId)
                return false;
            // A direct function-block-to-function-block variable link (US-033b) only joins compatible endpoints: a
            // flag/output source of one block to a flag/input target of another block. (Product↔block links, where at
            // most one endpoint is an FB variable, keep their existing behaviour.)
            if (OwningFunctionBlock(draggedPinId) is { } sourceBlock && OwningFunctionBlock(dropTargetPinId) is { } targetBlock)
            {
                string sourceTag = project.FindById(draggedPinId)?.Tag ?? string.Empty;
                string targetTag = project.FindById(dropTargetPinId)?.Tag ?? string.Empty;
                if (sourceBlock.Id == targetBlock.Id
                    || sourceTag is not ("resource_flag" or "resource_output")
                    || targetTag is not ("resource_flag" or "resource_input"))
                {
                    await _dialogs.ShowMessageAsync("Incompatible link",
                        "Link a flag or output of one block to a flag or input of another block.");
                    return false;
                }
            }
            editor.Link(dropTargetPinId, draggedPinId);   // drop target = link_from (destination), dragged = link_to (source)
            return true;
        });

    /// <summary>
    /// Edits an existing scenario link's stored value (US-058): rewrites the scene member's value attributes by id —
    /// <c>dimming_value</c>/<c>ramptime_ms</c> for a dimmer member, <c>relay_value</c> for a relay/socket member.
    /// Commits, marks dirty. Returns false on failure.
    /// </summary>
    public Task<bool> UpdateSceneValueAsync(ElementId memberId, SceneValueResult r, bool isDimmer) =>
        RunEditAsync(nameof(UpdateSceneValueAsync), "Scene value update failed", (project, editor) =>
        {
            if (!editor.TryResolve(memberId, out ElementRef? handle))
                return false;
            if (isDimmer)
            {
                handle.SetAttribute("dimming_value", r.LevelPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                handle.SetAttribute("ramptime_ms", (((r.RampMinutes * 60) + r.RampSeconds) * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                handle.SetAttribute("relay_value", r.On ? "on" : "off");
            }
            return true;
        });

    /// <summary>
    /// Removes a link by one of its rows (US-057): deletes the selected link half (follow-link "link to"/"link from"
    /// row, or a scene member / scene_link) and cascades its reciprocal partner, so both halves of exactly that link
    /// go together while every other link on the two pins is left intact. Commits, marks dirty. Returns false on
    /// failure.
    /// </summary>
    public Task<bool> RemoveLinkAsync(ElementId linkRowId) =>
        RunEditAsync(nameof(RemoveLinkAsync), "Remove link failed", (project, editor) =>
        {
            editor.DeleteById(linkRowId);   // cascades the reciprocal half that points back into the deleted row
            return true;
        });

    /// <summary>
    /// Creates a scenario link (US-024): wires the function-block scene output pin to the product's scenes container
    /// with the given value — <see cref="SceneValue.Dimmer"/> (light level %, ramp) for a dimmer or
    /// <see cref="SceneValue.Relay"/> (ON/OFF) otherwise. Commits, marks dirty. Returns false on failure.
    /// </summary>
    public Task<bool> LinkSceneAsync(ElementId sceneOutputId, ElementId scenesId, SceneValueResult r, bool isDimmer) =>
        RunEditAsync(nameof(LinkSceneAsync), "Scene link failed", (project, editor) =>
        {
            SceneValue value = isDimmer
                ? SceneValue.Dimmer(r.LevelPercent, TimeSpan.FromSeconds((r.RampMinutes * 60) + r.RampSeconds))
                : SceneValue.Relay(r.On);
            editor.LinkScene(sceneOutputId, scenesId, value);
            return true;
        });

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
    public Task<ElementId?> AddVariableAsync(ElementId sectionId, string resourceTag, string name)
    {
        ElementId? addedId = null;
        return RunEditAsync<ElementId?>(nameof(AddVariableAsync), "Add variable failed", (project, editor) =>
        {
            Activity.Current?.SetTag("variable.type", resourceTag);
            ProjectElement? section = project.FindById(sectionId);
            ProjectElement? block = project.FindParent(sectionId);
            if (section is null || block?.Tag != "functionblock" || block.Id is not { } blockId)
                return false;
            FunctionBlockRef fb = editor.FunctionBlock(blockId);
            ResourceRef added = section.Tag switch
            {
                "inputs" => fb.AddInput(resourceTag, name),
                "outputs" => fb.AddOutput(resourceTag, name),
                "settings" => fb.AddSetting(resourceTag, name),
                "internalsettings" => fb.AddInternalVariable(resourceTag, name),
                _ => throw new InvalidOperationException($"<{section.Tag}> is not a function-block variable section."),
            };
            addedId = added.Id;
            return true;
        }, _ => addedId, onFail: null);
    }

    /// <summary>
    /// Creates a project-global enumerator type and inserts a variable of it (US-030): authors an
    /// <c>enum_definition</c> named <paramref name="typeName"/> with the ordered <paramref name="states"/>, then adds a
    /// <c>resource_enum</c> variable named <paramref name="variableName"/> to the block section
    /// <paramref name="sectionId"/> wired to that type (<c>typedef</c> + <c>inivalue</c> of the first state). The type
    /// is global — other blocks can reference it. Returns the new variable's id, or null on failure. No controller.
    /// </summary>
    public Task<ElementId?> AddEnumVariableAsync(ElementId sectionId, string variableName, string typeName, IReadOnlyList<string> states)
    {
        ElementId? addedId = null;
        return RunEditAsync<ElementId?>(nameof(AddEnumVariableAsync), "Add enumerator failed", (project, editor) =>
        {
            Activity.Current?.SetTag("enum.type", typeName);
            ProjectElement? section = project.FindById(sectionId);
            ProjectElement? block = project.FindParent(sectionId);
            if (section is null || block?.Tag != "functionblock" || block.Id is not { } blockId)
                return false;
            EnumDefinitionRef def = editor.AddEnumDefinition(typeName, states.ToArray());
            FunctionBlockRef fb = editor.FunctionBlock(blockId);
            void Configure(ElementRef r)
            {
                r.SetAttribute("typedef", def.Typedef);
                if (states.Count > 0)
                    r.SetAttribute("inivalue", def.InitialValue(states[0]));
            }
            ResourceRef added = section.Tag switch
            {
                "settings" => fb.AddSetting("resource_enum", variableName, Configure),
                "internalsettings" => fb.AddInternalVariable("resource_enum", variableName, Configure),
                _ => throw new InvalidOperationException($"<{section.Tag}> does not accept an enum variable."),
            };
            addedId = added.Id;
            return true;
        }, _ => addedId, onFail: null);
    }

    /// <summary>
    /// Appends any newly-listed states to an existing enumerator type (US-030) — the type referenced by the
    /// <c>resource_enum</c> variable <paramref name="enumVariableId"/>. Only states not already present are added
    /// (the SDK's <c>AddEnumValues</c> is append-only; built-in read-only types are refused). Returns true when at
    /// least the call succeeded (a no-op append still returns true), false on failure or a non-enum target.
    /// </summary>
    public async Task<bool> UpdateEnumStatesAsync(ElementId enumVariableId, IReadOnlyList<string> states)
    {
        if (Current is null)
            return false;
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{nameof(UpdateEnumStatesAsync)}");
        try
        {
            ProjectElement? variable = Current.FindById(enumVariableId);
            if (variable?.Tag != "resource_enum"
                || !ElementId.TryParse(variable.GetAttribute("typedef"), out ElementId defId)
                || Current.FindById(defId) is not { } def || def.GetAttribute("name") is not { } defName)
                return false;
            var existing = def.ChildrenOrEmpty().Where(c => c.Tag == "enum_value")
                .Select(c => c.GetAttribute("name")).ToHashSet();
            string[] added = states.Where(s => !existing.Contains(s)).ToArray();
            if (added.Length == 0)
                return true;   // nothing new to append
            ProjectEditor editor = Current.Edit();
            editor.AddEnumValues(editor.EnumDefinition(defName), added);
            Project updated = editor.ToProject();
            await CommitAsync(updated);
            return true;
        }
        catch (Exception ex)
        {
            Ihc.ActivityExtensions.SetError(activity, ex);
            _logger.LogError(ex, "Failed to update enumerator states for {Id}", enumVariableId.ToToken());
            await _dialogs.ShowMessageAsync("Edit enumerator failed", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Authors a program <c>event</c> (US-028): appends an event to the program owning <paramref name="containerId"/>
    /// (the selected <c>events</c> node), triggered by the resource <paramref name="variableId"/> per the vendor
    /// <paramref name="method"/> token. The stored <paramref name="name"/> keeps the vendor <c>%P</c> template so it
    /// stays live if the variable is renamed. Returns false (with a diagnostic) when the target is not a program's
    /// events container. Read-add over the project; no controller contact.
    /// </summary>
    public Task<bool> AddProgramEventAsync(ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        AuthorProgramChildAsync(containerId, variableId, method, name, note, isEvent: true);

    /// <summary>
    /// Authors a program <c>action</c> command (US-028): appends a top-level command to the program owning
    /// <paramref name="containerId"/> (the selected <c>actions</c>/"Commands" node), driving the resource
    /// <paramref name="variableId"/> per the vendor <paramref name="method"/> token. Events fire commands
    /// top-to-bottom. Returns false when the target is not a program's actions container.
    /// </summary>
    public Task<bool> AddProgramCommandAsync(ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        AuthorProgramChildAsync(containerId, variableId, method, name, note, isEvent: false);

    /// <summary>
    /// Adds a <c>Powerup</c> system event (US-033) to the program owning <paramref name="eventsContainerId"/> — the
    /// program then runs on controller power-up (also on project transfer and software restart), useful for
    /// re-establishing timer values. Takes no operand. Returns false for a non-events target. No controller.
    /// </summary>
    public Task<bool> AddPowerEventAsync(ElementId eventsContainerId) =>
        RunEditAsync(nameof(AddPowerEventAsync), "Add Powerup event failed", (project, editor) =>
        {
            if (project.FindById(eventsContainerId)?.Tag != "events"
                || project.FindParent(eventsContainerId) is not { Tag: "program_simple", Id: { } programId })
                return false;
            editor.Program(programId).AddPowerEvent("Powerup",
                "Runs the program on controller power-up (also on project transfer and software restart).");
            return true;
        });

    /// <summary>
    /// Sets an output's "Save current value" power-loss persistence (US-033): writes <c>backup="yes"|"no"</c> on the
    /// function-block or physical output <paramref name="outputId"/> so its value is restored after a power loss
    /// instead of reset. Returns false when the target is not an output. No controller.
    /// </summary>
    public Task<bool> SetOutputBackupAsync(ElementId outputId, bool save) =>
        RunEditAsync(nameof(SetOutputBackupAsync), "Save current value failed", (project, editor) =>
        {
            if (project.FindById(outputId)?.Tag is not ("resource_output" or "dataline_output" or "airlink_relay"))
                return false;
            if (!editor.TryResolve(outputId, out ElementRef? handle))
                return false;
            handle.SetAttribute("backup", save ? "yes" : "no");
            return true;
        });

    private Task<bool> AuthorProgramChildAsync(
        ElementId containerId, ElementId variableId, string method, string name, string? note, bool isEvent) =>
        RunEditAsync(
            isEvent ? nameof(AddProgramEventAsync) : nameof(AddProgramCommandAsync),
            isEvent ? "Add event failed" : "Add command failed",
            (project, editor) =>
            {
                Activity.Current?.SetTag("program.method", method);
                ProjectElement? container = project.FindById(containerId);
                ResourceRef variable = editor.Resource(variableId);
                if (isEvent)
                {
                    // Events live only on the program root's events container (parent = program_simple).
                    if (container?.Tag != "events" || project.FindParent(containerId) is not { Tag: "program_simple", Id: { } programId })
                        return false;
                    editor.Program(programId).AddEvent(name, variable, method, note: note);
                }
                else
                {
                    // Commands go into any command container — the root "Commands", a sub-program's true/false branch,
                    // or a case value's <case_action> (US-028/US-029/US-031).
                    if (container?.Tag is not ("actions" or "case_action"))
                        return false;
                    editor.Branch(containerId).AddAction(name, variable, method, note: note);
                }
                return true;
            });

    /// <summary>
    /// Inserts a conditional sub-program (US-029) into an <c>actions</c> container <paramref name="commandsId"/> (a
    /// program's Commands group or a branch) — a <c>program_sub</c> with a Conditions group and the true/false command
    /// branches. Returns false when the target is not an actions container. No controller contact.
    /// </summary>
    public Task<bool> AddSubProgramAsync(ElementId commandsId) =>
        MutateProgramAsync(nameof(AddSubProgramAsync), commandsId, "actions", "sub-program",
            editor => editor.Branch(commandsId).AddSubProgram());

    /// <summary>
    /// Adds a <c>condition</c> (US-029) to a Conditions group <paramref name="conditionsId"/>, testing the resource
    /// <paramref name="variableId"/> per the vendor <paramref name="method"/> token (the popup's NOT variant is just a
    /// different token). The stored <paramref name="name"/> keeps the <c>%P</c>/<c>%S</c> template. Returns false when
    /// the target is not a conditions group.
    /// </summary>
    public Task<bool> AddConditionAsync(ElementId conditionsId, ElementId variableId, string method, string name, string? note) =>
        MutateProgramAsync(nameof(AddConditionAsync), conditionsId, "conditions", "condition",
            editor => editor.ConditionsGroup(conditionsId).AddCondition(name, editor.Resource(variableId), method, note: note));

    /// <summary>
    /// Toggles a Conditions group's logical combination (US-029): <paramref name="or"/> true → OR (<c>&gt;=1</c>),
    /// false → AND (<c>&amp;</c>, the default). Returns false when the target is not a conditions group.
    /// </summary>
    public Task<bool> SetConditionsLogicAsync(ElementId conditionsId, bool or) =>
        MutateProgramAsync(nameof(SetConditionsLogicAsync), conditionsId, "conditions", "logic",
            editor => { var g = editor.ConditionsGroup(conditionsId); if (or) g.Or(); else g.And(); });

    /// <summary>
    /// Adds a nested logic group (US-029) — a nested <c>conditions</c> group inside <paramref name="conditionsId"/> —
    /// for compound expressions. Returns false when the target is not a conditions group.
    /// </summary>
    public Task<bool> AddLogicGroupAsync(ElementId conditionsId) =>
        MutateProgramAsync(nameof(AddLogicGroupAsync), conditionsId, "conditions", "logic-group",
            editor => editor.ConditionsGroup(conditionsId).AddConditionGroup());

    /// <summary>
    /// Authors a single arithmetic command line (US-032): one operation on the target register
    /// <paramref name="targetId"/> against <paramref name="operandId"/> per <paramref name="method"/> (add
    /// <c>_0x5a</c> / subtract <c>_0x64</c>), appended to the command container <paramref name="commandsId"/>. One
    /// operation per line by construction — larger formulas are a sequence of these. The stored <paramref name="name"/>
    /// keeps the vendor <c>%P = %P ± %S</c> template. Returns false on a non-command target. No controller.
    /// </summary>
    public Task<bool> AddArithmeticCommandAsync(ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name) =>
        RunEditAsync(nameof(AddArithmeticCommandAsync), "Add arithmetic failed", (project, editor) =>
        {
            Activity.Current?.SetTag("program.method", method);
            if (project.FindById(commandsId)?.Tag is not ("actions" or "case_action"))
                return false;
            editor.Branch(commandsId).AddAction(name, editor.Resource(targetId), method, editor.Resource(operandId));
            return true;
        });

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
    public Task<bool> AddCaseAsync(ElementId commandsId, ElementId switchVariableId) =>
        RunEditAsync(nameof(AddCaseAsync), "Add case failed", (project, editor) =>
        {
            ProjectElement? container = project.FindById(commandsId);
            ProjectElement? switchVar = project.FindById(switchVariableId);
            if (container?.Tag is not ("actions" or "case_action") || switchVar is null
                || !EligibleCaseVariableTags.Contains(switchVar.Tag))
                return false;
            editor.Branch(commandsId).AddCase("Case", editor.Resource(switchVariableId));
            return true;
        });

    /// <summary>
    /// Adds a case value branch (US-031) to a <c>program_case</c> <paramref name="caseId"/> for the literal
    /// <paramref name="criterion"/> — a bare typed operand matching the switch variable's type (e.g. a counter's
    /// <c>&lt;resource_counter inivalue="100"&gt;</c>). Returns false for a non-case target, a missing switch, or an
    /// enum switch (enum case values need the type's states — deferred). No controller.
    /// </summary>
    public Task<bool> AddCaseValueAsync(ElementId caseId, string criterion) =>
        RunEditAsync(nameof(AddCaseValueAsync), "Add case value failed", (project, editor) =>
        {
            ProjectElement? kase = project.FindById(caseId);
            if (kase?.Tag != "program_case" || !ElementId.TryParse(kase.GetAttribute("link"), out ElementId switchId)
                || project.FindById(switchId) is not { } switchVar || switchVar.Tag == "resource_enum")
                return false;
            editor.Case(caseId).Case(criterion, switchVar.Tag, op => op.SetAttribute("inivalue", criterion));
            return true;
        });

    private Task<bool> MutateProgramAsync(string op, ElementId targetId, string requiredTag, string kind, Action<ProjectEditor> mutate) =>
        RunEditAsync(op, $"Add {kind} failed", (project, editor) =>
        {
            if (project.FindById(targetId)?.Tag != requiredTag)
                return false;
            mutate(editor);
            return true;
        });

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
    public Task<bool> UnlockFunctionBlockAsync(ElementId functionBlockId) =>
        RunEditAsync(nameof(UnlockFunctionBlockAsync), "Unlock failed", (project, editor) =>
        {
            if (!editor.TryResolve(functionBlockId, out ElementRef? handle))
            {
                _logger.LogWarning("Cannot unlock {Id}: it no longer exists", functionBlockId.ToToken());
                return false;
            }
            handle.SetAttribute("locked", "no");
            return true;
        });

    /// <summary>
    /// Inserts an empty "from scratch" function block into a locality (US-019): scaffolds the catalog's
    /// <c>Tom blok</c> template (the four variable sections + one empty program) named <see cref="EmptyBlockName"/>,
    /// commits, marks dirty. Returns the new block's id, or null when there is no open project or the edit fails.
    /// </summary>
    public Task<ElementId?> AddEmptyFunctionBlockAsync(ElementId localityId) =>
        RunEditAsync<ElementId?>(nameof(AddEmptyFunctionBlockAsync), "Insert failed",
            (project, editor) =>
            {
                FunctionBlockDefinition template = _service.GetEmptyFunctionBlockTemplate();
                editor.Group(localityId).AddEmptyFunctionBlock(template, DateOnly.FromDateTime(DateTime.Now), EmptyBlockName);
                return true;
            },
            updated => updated.FindById(localityId)?.ChildrenOrEmpty().LastOrDefault(c => c.Tag == "functionblock")?.Id,
            onFail: null);

    /// <summary>
    /// Inserts a preprogrammed library function block into a locality (US-018): deep-copies the block identified by
    /// <paramref name="masterType"/> under the <c>group</c> <paramref name="localityId"/> (fresh ids; its variable
    /// sections and program materialized by the SDK), commits, marks dirty. Returns the new block's id, or null when
    /// there is no open project, the id is not a known library block, or the edit fails.
    /// </summary>
    public Task<ElementId?> AddFunctionBlockAsync(ElementId localityId, string masterType) =>
        RunEditAsync<ElementId?>(nameof(AddFunctionBlockAsync), "Insert failed",
            (project, editor) =>
            {
                Activity.Current?.SetTag("functionblock.masterType", masterType);
                FunctionBlockDefinition definition = _service.GetAvailableFunctionBlocks()
                    .FirstOrDefault(f => f.MasterType == masterType)
                    ?? throw new InvalidOperationException($"No library function block with master type '{masterType}'.");
                editor.Group(localityId).AddFunctionBlock(definition);
                return true;
            },
            updated => updated.FindById(localityId)?.ChildrenOrEmpty().LastOrDefault(c => c.Tag == "functionblock")?.Id,
            onFail: null);

    /// <summary>
    /// Inserts a catalog product into a locality (US-010): deep-copies the product identified by
    /// <paramref name="productIdentifier"/> under the <c>group</c> <paramref name="localityId"/> (fresh ids, pins and
    /// scenes materialized by the SDK), commits, marks dirty and records the change. Returns the new product's id, or
    /// null when there is no open project, the id is not a known catalog product, or the edit fails.
    /// </summary>
    public Task<ElementId?> AddProductAsync(ElementId localityId, string productIdentifier) =>
        RunEditAsync<ElementId?>(nameof(AddProductAsync), "Insert failed",
            async (project, editor) =>
            {
                Activity.Current?.SetTag("product.identifier", productIdentifier);
                ProductDefinition definition = _service.GetAvailableProducts()
                    .FirstOrDefault(p => p.ProductIdentifier == productIdentifier)
                    ?? throw new InvalidOperationException($"No catalog product with identifier '{productIdentifier}'.");
                // At most one modem per project, regardless of type (US-013).
                if (ProductKinds.IsModem(definition.Body.Tag) && HasModem(project))
                {
                    Activity.Current?.SetTag("modem.blocked", true);
                    await _dialogs.ShowMessageAsync("Only one modem",
                        "A project may contain at most one modem. Remove the existing modem before adding another.");
                    return false;
                }
                editor.Group(localityId).AddProduct(definition);
                return true;
            },
            // The product is appended as the locality's last child.
            updated => updated.FindById(localityId)?.ChildrenOrEmpty().LastOrDefault()?.Id,
            onFail: null);

    /// <summary>
    /// Inserts a new locality (US-008): appends a room named <see cref="NewLocalityName"/> under the project's
    /// localities, commits, marks the project dirty and records the change. Returns the new locality's id (last in
    /// <see cref="Project.Groups"/>) so the caller can select it, or null when there is no open project or the edit
    /// fails.
    /// </summary>
    public Task<ElementId?> AddLocalityAsync() =>
        RunEditAsync<ElementId?>(nameof(AddLocalityAsync), "Insert failed",
            (project, editor) =>
            {
                editor.AddGroup(NewLocalityName);
                return true;
            },
            // The new room is appended last, so it is the final entry in Groups.
            updated => updated.Groups.Count > 0 ? updated.Groups[^1].Id : null,
            onFail: null);

    /// <summary>
    /// Deletes a locality (US-009). An empty room is removed silently; a room that still holds products or function
    /// blocks is removed only after the installer confirms, and the delete cascades to the commands and conditions
    /// that referenced the removed products (<see cref="DeleteReferencePolicy.CascadeReferences"/>). Returns false
    /// (nothing mutated) when the id is absent, the installer declines the confirmation, or the edit fails.
    /// </summary>
    public Task<bool> DeleteLocalityAsync(ElementId id) =>
        RunEditAsync(nameof(DeleteLocalityAsync), "Delete failed", async (project, editor) =>
        {
            ProjectElement? group = project.FindById(id);
            if (group is null)
                return false;
            if (!group.Children.IsDefaultOrEmpty)
            {
                string name = group.GetAttribute("name") ?? "this locality";
                Activity.Current?.SetTag("locality.hasContents", true);
                bool confirmed = await _dialogs.ConfirmAsync("Delete locality",
                    $"'{name}' contains products. Deleting it also removes those products and the commands and " +
                    "conditions that use them. Delete anyway?");
                if (!confirmed)
                    return false;   // declined — nothing is deleted
            }
            editor.DeleteById(id, DeleteReferencePolicy.CascadeReferences);
            return true;
        });

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

    /// <summary>
    /// Deletes any project node and its subtree (US-053), generalising the US-009 locality delete to products,
    /// function blocks, variables/pins and program elements. An unreferenced leaf is removed silently; a node that
    /// other logic references (a link, or a command/condition/event) is **confirmed first**, then removed together
    /// with the reciprocal link halves and referencing program rows as **one** undoable step. Declining deletes
    /// nothing. If the engine cannot safely cascade a binding it **refuses and explains what to rewire**. Returns
    /// false on refusal/decline/failure. No controller.
    /// </summary>
    public Task<bool> DeleteNodeAsync(ElementId id) =>
        RunEditAsync(nameof(DeleteNodeAsync), "Delete failed", async (project, editor) =>
        {
            if (project.FindById(id) is not { } element || !IsDeletableNode(element.Tag))
            {
                await _dialogs.ShowMessageAsync("Cannot delete", "This node cannot be deleted.");
                return false;
            }
            bool referenced = HasLinkHalves(element) || WouldThrowStrict(id);
            if (referenced && !await _dialogs.ConfirmAsync("Delete",
                    $"'{element.GetAttribute("name") ?? element.Tag}' is referenced by other logic (links and/or "
                    + "commands). Delete it together with those references?"))
            {
                return false;   // declined — nothing is deleted
            }
            editor.DeleteById(id, referenced ? DeleteReferencePolicy.CascadeReferences : DeleteReferencePolicy.Strict);
            return true;
        }, refusalTitle: "Cannot delete");   // the engine refuses a delete it cannot safely cascade

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
    public Task<bool> ReorderNodeAsync(ElementId id, int delta) =>
        RunEditAsync(nameof(ReorderNodeAsync), "Reorder failed", (project, editor) =>
        {
            if (delta == 0 || project.FindParent(id) is not { Id: { } parentId } parent || project.FindById(id) is not { } node)
                return false;
            var siblings = parent.ChildrenOrEmpty().Where(c => c.Tag == node.Tag).ToList();
            int here = siblings.FindIndex(c => c.Id == id);
            int there = here + delta;
            if (here < 0 || there < 0 || there >= siblings.Count)
                return false;   // already at the end in that direction
            // Translate the same-tag position to the absolute child index of the sibling we swap with.
            int absoluteIndex = parent.ChildrenOrEmpty().ToList().FindIndex(c => c.Id == siblings[there].Id);
            editor.MoveSubtree(id, parentId, absoluteIndex);
            return true;
        });

    /// <summary>
    /// Moves a node to another container (US-054): re-parents the subtree under <paramref name="targetParentId"/> with
    /// its **identity preserved** — the IHC resource ids do not change, so its documentation, addressing and every link
    /// it participates in survive. Refuses an illegal container, a self/descendant target, or a no-op move into the
    /// current parent. Undoable (single snapshot). Returns false on refusal/failure. No controller.
    /// </summary>
    public Task<bool> MoveNodeAsync(ElementId sourceId, ElementId targetParentId) =>
        RunEditAsync(nameof(MoveNodeAsync), "Move failed", async (project, editor) =>
        {
            if (project.FindById(sourceId) is not { } source || project.FindById(targetParentId) is not { } target)
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
            editor.MoveSubtree(sourceId, targetParentId);
            return true;
        }, refusalTitle: "Cannot move");   // self/descendant target

    /// <summary>
    /// Copies a node and pastes it as an **independent duplicate** under <paramref name="targetParentId"/> (US-056):
    /// the SDK <see cref="ProjectEditor.CopySubtree"/> deep-copies the subtree with **fresh IHC resource ids** and
    /// **drops any link half whose other end lies outside the copy** (links wholly inside the copy are duplicated and
    /// stay connected); the original is left unchanged. Refuses an illegal container. Undoable. Returns the new node's
    /// id, or null on refusal/failure. No controller.
    /// </summary>
    public Task<ElementId?> CopyNodeAsync(ElementId sourceId, ElementId targetParentId)
    {
        ElementId newId = default;
        return RunEditAsync<ElementId?>(nameof(CopyNodeAsync), "Paste failed",
            async (project, editor) =>
            {
                if (project.FindById(sourceId) is not { } source || project.FindById(targetParentId) is not { } target)
                    return false;
                if (!CanContain(source.Tag, target.Tag))
                {
                    await _dialogs.ShowMessageAsync("Cannot paste", "That container cannot hold this node.");
                    return false;
                }
                newId = editor.CopySubtree(sourceId, targetParentId);
                return true;
            },
            _ => newId, onFail: null, refusalTitle: "Cannot paste");
    }

    /// <summary>
    /// Applies edited advanced wireless-dimmer settings (US-015): writes the <c>value</c> of the dimmer's
    /// <c>dimmer_setting_*</c> children — fade-rate up/down (soft on/off), dimming rate (manual ramp), minimum/maximum
    /// value, and the load-mode token. Commits, marks dirty. Returns false on failure.
    /// </summary>
    public Task<bool> UpdateDimmerSettingsAsync(ElementId productId, AdvancedDimmerResult r) =>
        RunEditAsync(nameof(UpdateDimmerSettingsAsync), "Update failed", (project, editor) =>
        {
            ProjectElement? product = project.FindById(productId);
            if (product is null)
                return false;
            void SetSetting(string tag, string value)
            {
                if (product.DescendantsAndSelf().FirstOrDefault(e => e.Tag == tag) is { Id: { } sid }
                    && editor.TryResolve(sid, out ElementRef? h))
                {
                    h.SetAttribute("value", value);
                }
            }
            string Dec(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            SetSetting("dimmer_setting_fade_rate_up", Dec(r.SoftOnMs));
            SetSetting("dimmer_setting_fade_rate_down", Dec(r.SoftOffMs));
            SetSetting("dimmer_setting_dimming_rate", Dec(r.ManualRampS));
            SetSetting("dimmer_setting_minimum_value", Dec(r.MinimumPercent));
            SetSetting("dimmer_setting_maximum_value", Dec(r.MaximumPercent));
            SetSetting("dimmer_setting_load_mode", r.LoadMode);
            return true;
        });

    /// <summary>Whether the project already contains a modem device root (the at-most-one-modem rule, US-013).</summary>
    public static bool HasModem(Project project) =>
        project.Root.DescendantsAndSelf().Any(e => ProductKinds.IsModem(e.Tag));

    /// <summary>
    /// Applies edited modem documentation (US-013): writes the modem's name/note/identification and the four RS485
    /// cabling wire colours by id, the SIM <c>sms_modem_pincode</c> value, and telephone numbers 1..N onto the
    /// matching <c>sms_modem_phonenumber</c> slots; re-parents to the chosen Location when changed. Commits, marks
    /// dirty. Returns false on failure.
    /// </summary>
    public Task<bool> UpdateModemAsync(ElementId modemId, ModemPropertiesResult r) =>
        RunEditAsync(nameof(UpdateModemAsync), "Update failed", (project, editor) =>
        {
            ProjectElement? modem = project.FindById(modemId);
            if (modem is null)
                return false;
            if (!editor.TryResolve(modemId, out ElementRef? handle))
                return false;
            handle.SetAttribute("name", r.Name);
            handle.SetAttribute("note", r.Note);
            handle.SetAttribute("documentation_tag", r.IdentificationCode);
            handle.SetAttribute("cablecolour_0V", r.Cable0V);
            handle.SetAttribute("cablecolour_24V", r.Cable24V);
            handle.SetAttribute("cablecolour_RS485Minus", r.CableRS485Minus);
            handle.SetAttribute("cablecolour_RS485Plus", r.CableRS485Plus);

            if (modem.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "sms_modem_pincode") is { Id: { } pinId }
                && editor.TryResolve(pinId, out ElementRef? pinHandle))
            {
                pinHandle.SetAttribute("value", string.IsNullOrEmpty(r.PinCode) ? "0" : r.PinCode);
            }
            for (int i = 0; i < r.PhoneNumbers.Count; i++)
            {
                string slot = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (modem.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "sms_modem_phonenumber"
                        && e.GetAttribute("address") == slot) is { Id: { } pnId }
                    && editor.TryResolve(pnId, out ElementRef? pnHandle))
                {
                    pnHandle.SetAttribute("phonenumber", r.PhoneNumbers[i]);
                }
            }
            if (ElementId.TryParse(r.LocalityId, out ElementId targetLocality)
                && project.FindParent(modemId)?.Id is { } currentParent && currentParent != targetLocality)
            {
                editor.MoveSubtree(modemId, targetLocality);
            }
            return true;
        });

    /// <summary>
    /// Applies edited terminal addressing to a product input/output pin (US-012): encodes (data line, terminal) into
    /// <c>address_dataline</c> and writes <c>cable_colour</c>, <c>note</c>, and — for an output — the <c>inivalue</c>
    /// (on = normally-closed, off = normally-open). Commits, marks dirty, records the change. Returns false on failure.
    /// </summary>
    public Task<bool> UpdatePinAsync(ElementId pinId, PinPropertiesResult r) =>
        RunEditAsync(nameof(UpdatePinAsync), "Addressing failed", (project, editor) =>
        {
            ProjectElement? pin = project.FindById(pinId);
            if (pin is null)
                return false;
            bool isOutput = pin.Tag == "dataline_output";
            if (!editor.TryResolve(pinId, out ElementRef? handle))
                return false;
            handle.SetAttribute("address_dataline",
                DatalineAddressing.Encode(r.DataLine, r.Terminal, DatalineAddressing.TerminalsPerLine(isOutput)));
            handle.SetAttribute("cable_colour", r.CableColour);
            handle.SetAttribute("note", r.Note);
            if (isOutput)
                handle.SetAttribute("inivalue", r.InitialValueOn ? "on" : "off");
            return true;
        });

    /// <summary>
    /// Applies edited product documentation (US-011): writes the product's <c>name</c>/<c>note</c>/<c>cabletype</c>/
    /// <c>cablenumber</c>/<c>documentation_tag</c>/<c>power_group</c> by id (a blank value clears the attribute), and
    /// re-parents the product to the chosen <c>Location</c> locality when it changed (ids preserved via
    /// <see cref="ProjectEditor.MoveSubtree"/>). Commits, marks dirty, records the change. Returns false on failure.
    /// </summary>
    public Task<bool> UpdateProductAsync(ElementId productId, ProductPropertiesResult r) =>
        RunEditAsync(nameof(UpdateProductAsync), "Update failed", (project, editor) =>
        {
            if (!editor.TryResolve(productId, out ElementRef? handle))
            {
                _logger.LogWarning("Cannot update product {Id}: it no longer exists", productId.ToToken());
                return false;
            }
            handle.SetAttribute("name", r.Name);
            handle.SetAttribute("note", r.Note);
            handle.SetAttribute("documentation_tag", r.IdentificationCode);
            handle.SetAttribute("power_group", r.LightGroup);
            // Wireless (airlink) products declare no cabling attributes — only wired products carry them.
            if (!ProductKinds.IsWireless(handle.Tag))
            {
                handle.SetAttribute("cabletype", r.CableType);
                handle.SetAttribute("cablenumber", r.CableNumber);
            }
            if (ElementId.TryParse(r.LocalityId, out ElementId targetLocality)
                && project.FindParent(productId)?.Id is { } currentParent && currentParent != targetLocality)
            {
                editor.MoveSubtree(productId, targetLocality);   // Location changed → re-parent (ids preserved)
            }
            return true;
        });

    /// <summary>
    /// Renames a locality (US-007): sets the <c>group</c>'s <c>name</c> and <c>note</c> by id, commits the edited
    /// project, and records the change — which also marks the project dirty and drives the every-Nth-change crash
    /// backup (US-005). Returns false (with a diagnostic) when the id no longer resolves or the edit fails.
    /// </summary>
    public Task<bool> RenameLocalityAsync(ElementId id, string name, string note) =>
        RunEditAsync(nameof(RenameLocalityAsync), "Rename failed", (project, editor) =>
        {
            if (!editor.TryResolve(id, out ElementRef? handle))
            {
                _logger.LogWarning("Cannot rename locality {Id}: it no longer exists", id.ToToken());
                return false;
            }
            handle.SetAttribute("name", name);
            handle.SetAttribute("note", note);
            return true;
        });

    /// <summary>Records one committed edit (the hook editors use in E2+): marks the project dirty and triggers a
    /// crash backup on every Nth change. Fire-and-forget for UI callers; tests await <see cref="MarkChangedAsync"/>.</summary>
    public void MarkChanged() => _ = MarkChangedAsync();

    // The single commit path for every project-mutating operation (US-052): snapshots the pre-edit project for undo,
    // invalidates the redo history, swaps in the new project, then marks changed (dirty + backup + StateChanged).
    private async Task CommitAsync(Project updated)
    {
        lock (_gate)
        {
            if (Current is not null)
            {
                _undo.Add(Current);
                if (_undo.Count > MaxHistoryDepth)
                    _undo.RemoveAt(0);
            }
            _redo.Clear();
            Current = updated;
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
        lock (_gate)
        {
            if (_undo.Count == 0 || Current is null)
                return false;
            _redo.Add(Current);
            Current = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
        }
        await MarkChangedAsync();
        return true;
    }

    /// <summary>Re-applies the last undone edit (US-052): the mirror of <see cref="UndoAsync"/>. No-op (false) when the
    /// redo history is empty.</summary>
    public async Task<bool> RedoAsync()
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectSession)}.{nameof(RedoAsync)}");
        lock (_gate)
        {
            if (_redo.Count == 0 || Current is null)
                return false;
            _undo.Add(Current);
            Current = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
        }
        await MarkChangedAsync();
        return true;
    }

    internal async Task MarkChangedAsync()
    {
        bool backup;
        lock (_gate)
        {
            IsDirty = true;
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
            _undo.Clear();   // a load starts a fresh, empty edit history (US-052)
            _redo.Clear();
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
