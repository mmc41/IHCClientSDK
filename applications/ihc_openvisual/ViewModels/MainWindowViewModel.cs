using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Configuration;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Addressing;
using Ihc.Vis.Session;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Programs;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// The shell view-model: the window title, the two locality tree panes, the status-bar hint, the toolbar/
/// status-bar/theme view state, and the <i>File</i>/<i>View</i>/<i>Help</i> commands. A thin coordinator over
/// <see cref="ProjectWorkflow"/> (all project logic) and <see cref="IDialogService"/>/<see cref="IThemeService"/>
/// (all Avalonia); free of Avalonia types so it is testable headlessly.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ProjectWorkflow _session;

    // Stored so Dispose can detach them: an inline lambda cannot be unsubscribed, which would leak this view-model
    // through the longer-lived session / recent-store event sources (review Low).
    private readonly EventHandler _onSessionStateChanged;
    private readonly EventHandler _onSessionCatalogChanged;
    private readonly EventHandler _onRecentChanged;

    // The two-pane tree-sync engine (W3-6 / T031): owns the keyed reconcilers and drives the per-edit in-place
    // reconcile-or-rebuild + programming-mode pane build. Selection capture/restore stays here (view-state).
    private readonly TreePaneCoordinator _treePanes;

    // The per-node-type Properties dialog flows, extracted from this view-model (W3-8). It applies results through
    // this view-model's single outcome→status/dialog rule (ApplyAsync).
    private readonly PropertiesDialogCoordinator _properties;
    private readonly ProgramAuthoringCoordinator _programAuthoring;
    // T017: the pin/scene linking engine (US-022/024/025); the VM keeps thin entry points delegating here.
    private readonly LinkingCoordinator _linking;

    /// <summary>The tree drag-and-drop dispatcher (W3-9): drop legality/route, the drop mutation, and the drop-target
    /// highlight. The code-behind's DragOver/Drop handlers and the headless drag tests drive this.</summary>
    public TreeDragDropController DragDrop { get; }

    /// <summary>The declarative command registry (crudarch T012, proposal §3.3): one row per migrated user-facing
    /// command; materializes the commands from each row's gate and computes the per-surface availability
    /// snapshots the XAML binds to (<c>Registry.Bar[id]</c>/<c>Registry.ContextMenu[id]</c>).</summary>
    public CommandRegistry Registry { get; }

    // crudarch T012: the divergent family's commands are MATERIALIZED by the registry from each row's single
    // gate (D02). The properties below are a NAMING SHIM, nothing more: they exist only so XAML bindings and
    // existing call sites can keep saying CutCommand instead of Registry.Commands["edit.cut"] — the row id is
    // the real name. Adding one is never required by a new row; add it only to keep an existing call site
    // compiling, and delete it when that call site goes (review F08).
    public IAsyncRelayCommand CutCommand => Registry.Commands["edit.cut"];
    public IAsyncRelayCommand CopyCommand => Registry.Commands["edit.copy"];
    public IAsyncRelayCommand PasteCommand => Registry.Commands["edit.paste"];
    public IAsyncRelayCommand DeleteCommand => Registry.Commands["edit.delete"];
    public IAsyncRelayCommand EnterProgrammingModeCommand => Registry.Commands["view.showProgram"];

    // crudarch T013: the remaining node-scoped tree commands as registry rows.
    public IAsyncRelayCommand InsertLocalityCommand => Registry.Commands["insert.locality"];
    public IAsyncRelayCommand InsertEmptyFunctionBlockCommand => Registry.Commands["insert.emptyFunctionBlock"];
    public IAsyncRelayCommand SaveFunctionBlockCommand => Registry.Commands["node.saveBlock"];
    public IAsyncRelayCommand UnlockCommand => Registry.Commands["node.unlock"];
    public IAsyncRelayCommand ToggleLogMarkCommand => Registry.Commands["node.toggleLogMark"];
    public IAsyncRelayCommand HelpCommand => Registry.Commands["help.onNode"];
    public IAsyncRelayCommand UseInProgramCommand => Registry.Commands["node.useInProgram"];
    public IAsyncRelayCommand StartLinkCommand => Registry.Commands["link.startFromHere"];
    public IAsyncRelayCommand LinkToHereCommand => Registry.Commands["link.toHere"];
    public IAsyncRelayCommand NavigateLinkOppositeCommand => Registry.Commands["link.jumpOpposite"];
    public IAsyncRelayCommand MoveUpCommand => Registry.Commands["edit.moveUp"];
    public IAsyncRelayCommand MoveDownCommand => Registry.Commands["edit.moveDown"];
    public IAsyncRelayCommand PropertiesCommand => Registry.Commands["node.properties"];

    // crudarch T017: Undo/Redo materialized from their history-gated rows (US-052).
    public IAsyncRelayCommand UndoCommand => Registry.Commands["edit.undo"];
    public IAsyncRelayCommand RedoCommand => Registry.Commands["edit.redo"];

    // crudarch T015: the app-level commands as registry rows (Save stays always-enabled, D07). OpenRecent and
    // SetTheme remain parameterized ITEM commands (data-driven lists — the established non-row ruling).
    public IAsyncRelayCommand NewCommand => Registry.Commands["file.new"];
    public IAsyncRelayCommand OpenCommand => Registry.Commands["file.open"];
    public IAsyncRelayCommand SaveCommand => Registry.Commands["file.save"];
    public IAsyncRelayCommand SaveAsCommand => Registry.Commands["file.saveAs"];
    public IAsyncRelayCommand CloseCommand => Registry.Commands["file.close"];
    public IAsyncRelayCommand ExitCommand => Registry.Commands["app.exit"];
    public IAsyncRelayCommand ToggleToolbarCommand => Registry.Commands["view.toggleToolbar"];
    public IAsyncRelayCommand ToggleStatusBarCommand => Registry.Commands["view.toggleStatusBar"];
    public IAsyncRelayCommand ProjectInfoCommand => Registry.Commands["project.info"];
    public IAsyncRelayCommand DataTablesCommand => Registry.Commands["project.dataTables"];
    public IAsyncRelayCommand ModuleMapCommand => Registry.Commands["project.moduleMap"];
    public IAsyncRelayCommand SendProjectCommand => Registry.Commands["controller.send"];
    public IAsyncRelayCommand RetrieveProjectCommand => Registry.Commands["controller.retrieve"];
    public IAsyncRelayCommand OpenReportsCommand => Registry.Commands["reports.open"];
    public IAsyncRelayCommand ImportCatalogFileCommand => Registry.Commands["catalog.importFile"];
    public IAsyncRelayCommand ImportCatalogFolderCommand => Registry.Commands["catalog.importFolder"];
    public IAsyncRelayCommand AboutCommand => Registry.Commands["help.about"];
    public IAsyncRelayCommand ShowSettingsCommand => Registry.Commands["app.settings"];
    public IAsyncRelayCommand TelemetryDiagnosticsCommand => Registry.Commands["app.telemetryDiagnostics"];

    // crudarch T014: the programming-mode set as registry rows.
    public IAsyncRelayCommand LeaveProgrammingModeCommand => Registry.Commands["program.leaveMode"];
    public IAsyncRelayCommand InsertInputCommand => Registry.Commands["program.insertInput"];
    public IAsyncRelayCommand InsertOutputCommand => Registry.Commands["program.insertOutput"];
    public IAsyncRelayCommand AddPowerEventCommand => Registry.Commands["program.addPowerEvent"];
    public IAsyncRelayCommand ToggleSaveValueCommand => Registry.Commands["program.toggleSaveValue"];
    public IAsyncRelayCommand AddSubProgramCommand => Registry.Commands["program.addSubProgram"];
    public IAsyncRelayCommand AddLogicGroupCommand => Registry.Commands["program.addLogicGroup"];
    public IAsyncRelayCommand SetConditionsOrCommand => Registry.Commands["program.setConditionsOr"];
    public IAsyncRelayCommand SetConditionsAndCommand => Registry.Commands["program.setConditionsAnd"];
    public IAsyncRelayCommand NewCaseValueCommand => Registry.Commands["program.newCaseValue"];

    private readonly IDialogService _dialogs;
    private readonly RecentProjectsStore _recent;
    private readonly IThemeService _themeService;
    private readonly AppConfiguration? _config;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty] private string _title = $"{Constants.UntitledDocument} - {Constants.AppName}";
    [ObservableProperty] private string _statusText = "For help, press F1";
    [ObservableProperty] private string _installationPaneHeader = "Installation";
    [ObservableProperty] private string _functionsPaneHeader = "Functions";

    /// <summary>Whether the window is in programming mode (one function block's variables + program), vs the two
    /// locality trees of configuration mode (US-026).</summary>
    [ObservableProperty] private bool _isProgrammingMode;
    private ElementId? _programmingBlockId;
    [ObservableProperty] private bool _isToolbarVisible = true;
    [ObservableProperty] private bool _isStatusBarVisible = true;
    [ObservableProperty] private AppTheme _currentTheme;

    /// <summary>The active tree node — whichever pane the installer last selected in. Context-menu commands, F2 and
    /// the insert target all read this. Not bound directly to a tree (each pane binds its own selection below), so a
    /// Functions-pane node (a function block) can be the active node without fighting the Installation tree.</summary>
    // crudarch T012: the divergent family (Cut/Copy/Paste/Delete/Show program) lost its Notify* entries here —
    // their enablement flows through the registry, invalidated by the ONE ContextChanged signal (C-BP-06).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInsertProduct))]
    [NotifyPropertyChangedFor(nameof(CanInsertFunctionBlock))]
    [NotifyPropertyChangedFor(nameof(CanInsertVariable))]
    [NotifyPropertyChangedFor(nameof(CanAddEvent))]
    [NotifyPropertyChangedFor(nameof(CanAddCommand))]
    [NotifyPropertyChangedFor(nameof(CanAddCondition))]
    private TreeNodeViewModel? _selectedNode;

    /// <summary>Whether the block currently being programmed is a locked (library) block. A locked block is
    /// VIEW-ONLY: its program renders, but every authoring command is withdrawn (A-27/F-076) — the installer must
    /// unlock it deliberately first. Unlock is a separate, irreversible action (F-046).</summary>
    public bool IsProgrammingBlockLocked =>
        IsProgrammingMode && _programmingBlockId is { } id
        && _session.Current is { } project && project.FindById(id) is { } block
        && project.View(block).Locked;

    // The programming-mode authoring context-menu gates: a container node's own kind AND an editable (unlocked)
    // programming block. On a locked block every one is false, so the vendor's "missing, not greyed" affordance
    // holds. These four remain because they gate the DATA-DRIVEN ItemsSource submenus, which are non-rows by the
    // documented ruling — every gate with a registry row (New case value…, among others) is the row's, and only
    // the row's, so a rule edit cannot land in a second dead home (review F09).
    public bool CanInsertVariable => SelectedNode?.IsBlockSection == true && !IsProgrammingBlockLocked;
    public bool CanAddEvent => SelectedNode?.IsEventsContainer == true && !IsProgrammingBlockLocked;
    public bool CanAddCommand => SelectedNode?.IsCommandsContainer == true && !IsProgrammingBlockLocked;
    public bool CanAddCondition => SelectedNode?.IsConditionsContainer == true && !IsProgrammingBlockLocked;

    // crudarch T012: the Cut/Copy/Paste/Delete/Show-program gates (context AND the stricter bar variants,
    // uxparity S-27/S-28) moved into the registry rows — see RegisterCoreEditRows. Their per-surface
    // divergences are SurfacePolicy data (D13), evaluated by the ONE CommandRegistry.For evaluator.

    // crudarch T013/T014: CanMoveSelected/CanInsertLocalityHere/CanShowProperties/CanNavigateLinkOpposite/
    // CanLeaveProgrammingMode moved into the registry rows (edit.moveUp/Down, insert.locality, node.properties,
    // link.jumpOpposite, program.leaveMode).

    /// <summary>Whether the active selection lives in the <i>Installation</i> pane (vs the <i>Functions</i> pane). The
    /// shared node context menu uses this to offer product insertion only where products belong.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInsertProduct))]
    [NotifyPropertyChangedFor(nameof(CanInsertFunctionBlock))]
    private bool _isInstallationPaneActive;

    /// <summary>Context-menu gate: <i>Insert product</i> is offered only on a <b>locality</b> in the Installation pane
    /// — the Functions pane hosts function blocks, and the Localities root hosts localities (US-010/US-068).</summary>
    public bool CanInsertProduct => IsInstallationPaneActive && SelectedNode?.IsLocality == true;

    /// <summary>Context-menu gate: <i>Insert function block</i> / <i>Empty function block</i> are offered only on a
    /// <b>locality</b> in the <i>Functions</i> pane — function blocks belong there, products to the Installation pane
    /// (A-5a/F-008/US-068). Mirrors <see cref="CanInsertProduct"/> on the opposite pane.</summary>
    public bool CanInsertFunctionBlock => !IsInstallationPaneActive && SelectedNode?.IsLocality == true;

    /// <summary>The <i>Installation</i> pane's current selection (two-way bound); also set programmatically to
    /// highlight a just-inserted locality (US-008).</summary>
    [ObservableProperty] private TreeNodeViewModel? _selectedInstallationNode;

    /// <summary>The <i>Functions</i> pane's current selection (two-way bound).</summary>
    [ObservableProperty] private TreeNodeViewModel? _selectedFunctionsNode;

    partial void OnSelectedInstallationNodeChanged(TreeNodeViewModel? value)
    {
        if (value is not null)
        {
            IsInstallationPaneActive = true;
            SelectedNode = value;
        }
        else if (IsInstallationPaneActive)
        {
            // The active pane's selection was cleared (delete / undo / project-switch reconciliation): drop the
            // shared selection too so SelectedNode never dangles on a detached node and the mutation gates disable
            // (review C4). The inactive pane clearing its own stale selection must not touch SelectedNode.
            SelectedNode = null;
        }
    }

    partial void OnSelectedFunctionsNodeChanged(TreeNodeViewModel? value)
    {
        if (value is not null)
        {
            IsInstallationPaneActive = false;
            SelectedNode = value;
        }
        else if (!IsInstallationPaneActive)
        {
            SelectedNode = null;   // active-pane clear → drop the shared selection (review C4)
        }
    }

    public ObservableCollection<TreeNodeViewModel> InstallationNodes { get; } = new();
    public ObservableCollection<TreeNodeViewModel> FunctionNodes { get; } = new();
    public ObservableCollection<RecentProjectViewModel> RecentProjects { get; } = new();

    /// <summary>The product insertion menu (US-010, H2/D08): a single tree of top-level category folders DERIVED from
    /// the catalog products' own <c>CategoryPath</c> (Wired / IHC Wireless / Bus / Special, plus an
    /// "Imported/Uncategorized" bucket for empty-category imported <c>.def</c> products), each nesting its
    /// subcategories and product leaves that insert under the selected locality. The top categories are catalog data,
    /// not a hardcoded set, so an imported product can never be dropped from the menu.</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProductsMenu { get; } = new();

    /// <summary>The library function-block insertion submenu (US-018), built from the catalog's FB folders.</summary>
    public ObservableCollection<ProductMenuItemViewModel> FunctionBlocksMenu { get; } = new();

    /// <summary>The variable types insertable into the currently selected block section (US-027); rebuilt when the
    /// selection changes so it only offers the types that section accepts.</summary>
    public ObservableCollection<ProductMenuItemViewModel> VariablePaletteMenu { get; } = new();

    // The variable palette (label, resource tag, section kind) is projected over the SDK variable-type registry by
    // VariablePalette (US-027, ADR-002/D07) — so the types the engine accepts and the types the UI offers cannot
    // drift, and a dropped type is a deliberate, tested suppression rather than a silent omission.

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value)
    {
        RebuildContext();   // T010: selection is a context trigger (first, so the early return below cannot skip it)
        _programAuthoring.Rebuild(value);
        VariablePaletteMenu.Clear();
        if (value is not { IsBlockSection: true, ElementId: { } sectionId, SectionTag: { } sectionTag })
            return;
        char kind = sectionTag switch { "inputs" => 'I', "outputs" => 'O', _ => 'V' };
        string sectionLabel = value.DisplayName;
        foreach ((string label, string tag, char _) in VariablePalette.Entries.Where(t => t.Kind == kind))
        {
            if (tag == "resource_enum")
            {
                // PG-4: an enum insertion offers a TYPE PICKER — the existing enumerator types (pick one → reference
                // its def-id, no new type) plus a "New…" that authors a new type through the enumerator dialog.
                var enumNode = new ProductMenuItemViewModel(label);
                foreach (string typeName in _session.Current?.GetEnumeratorTypes() ?? System.Array.Empty<string>())
                {
                    enumNode.Children.Add(new ProductMenuItemViewModel(typeName, "enum-type",
                        new AsyncRelayCommand(() => InsertEnumOfExistingTypeAsync(sectionId, typeName, sectionLabel))));
                }
                enumNode.Children.Add(new ProductMenuItemViewModel("New…", "enum-new",
                    new AsyncRelayCommand(() => InsertEnumAsync(sectionId, sectionLabel))));
                // PG-7/D02: a DISTINCT route that authors a standalone (0-state, unreferenced) type — NO variable.
                enumNode.Children.Add(new ProductMenuItemViewModel("New standalone type…", "enum-standalone",
                    new AsyncRelayCommand(AddStandaloneEnumTypeAsync)));
                VariablePaletteMenu.Add(enumNode);
            }
            else
            {
                VariablePaletteMenu.Add(new ProductMenuItemViewModel(label, tag,
                    new AsyncRelayCommand(() => InsertVariableAsync(sectionId, tag, label, sectionLabel))));
            }
        }
    }

    private Task InsertVariableAsync(ElementId sectionId, string tag, string label, string sectionLabel) =>
        // An enum insertion first defines its type + states through the enumerator dialog (US-030); all other
        // variable types insert directly (US-027).
        tag == "resource_enum"
            ? InsertEnumAsync(sectionId, sectionLabel)
            : RunAsync(nameof(InsertVariableAsync), async () =>
            {
                if (_session.Current is { } project && _session.Commands.AddVariable(project, sectionId, tag, label) is { } command)
                    await ApplyAsync(command, $"{label} was inserted under {sectionLabel}");
            });

    // T030: the program-authoring menus + engine (US-028/029/031/032) live in ProgramAuthoringCoordinator. The
    // view-model re-exposes the five XAML-bound menu collections and keeps the thin [RelayCommand] entry points,
    // delegating their bodies to the coordinator.

    /// <summary>The events a selected variable can raise, offered on a program's Events node (US-028).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramEventMenu => _programAuthoring.ProgramEventMenu;

    /// <summary>The commands a selected variable can be driven by, offered on a program's Commands node (US-028).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramCommandMenu => _programAuthoring.ProgramCommandMenu;

    /// <summary>The conditions a selected variable can be tested by on a sub-program's Conditions node (US-029).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramConditionMenu => _programAuthoring.ProgramConditionMenu;

    /// <summary>The "Case (&lt;variable&gt;)" option offered on a Commands node for an eligible switch variable (US-031).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramCaseMenu => _programAuthoring.ProgramCaseMenu;

    /// <summary>The arithmetic operations offered on a Commands node when a numeric target register is armed (US-032).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramArithmeticMenu => _programAuthoring.ProgramArithmeticMenu;

    /// <summary>Arms a variable (a block input/output/setting/internal, US-028) as the operand for the next event or
    /// command; the Events/Commands node then offers that variable's triggers and commands.</summary>
    private void UseInProgram(TreeNodeViewModel? node) => _programAuthoring.Arm(node);

    /// <summary>The Edit ▸ Undo menu header, naming the action it would reverse (E14): e.g. "Undo Insert locality",
    /// or just "Undo" when the history is empty. The leading underscore keeps the Alt+U access key.</summary>
    public string UndoMenuHeader => _session.CanUndo ? $"_Undo {_session.UndoLabel}" : "_Undo";

    /// <summary>The Edit ▸ Redo menu header, naming the action it would re-apply (E14), or just "Redo".</summary>
    public string RedoMenuHeader => _session.CanRedo ? $"_Redo {_session.RedoLabel}" : "_Redo";

    /// <summary>Edit ▸ Undo (US-052, Ctrl+Z): reverses the last project-mutating edit; a no-op when there is nothing
    /// to undo. Refreshes both panes via the session's StateChanged.</summary>
    private Task Undo() => RunAsync(nameof(Undo), async () =>
    {
        string? label = _session.UndoLabel;   // capture before the stack pops — names the action (E14)
        StatusText = await _session.UndoAsync()
            ? label is null ? "Undid the last change." : $"Undid: {label}"
            : "Nothing to undo.";
    });

    /// <summary>Edit ▸ Redo (US-052, Ctrl+Y): re-applies the last undone edit; a no-op when the redo history is empty.</summary>
    private Task Redo() => RunAsync(nameof(Redo), async () =>
    {
        string? label = _session.RedoLabel;
        StatusText = await _session.RedoAsync()
            ? label is null ? "Redid the change." : $"Redid: {label}"
            : "Nothing to redo.";
    });

    // The single outcome→status/dialog rule (W2-14): Committed → success status; NoChange → silent (a no-op edit
    // leaves the status alone); Refused → the refusal reason as status; Failed → an error dialog. Applying a command
    // through the session and mapping its outcome here is how the VM drives every edit, replacing the per-op wrappers.
    private async Task<EditOutcome> ReportOutcomeAsync(EditOutcome outcome, string? successStatus)
    {
        switch (outcome.Status)
        {
            case EditStatus.Committed when successStatus is not null: StatusText = successStatus; break;
            case EditStatus.Refused when outcome.Reason is not null: StatusText = outcome.Reason; break;
            case EditStatus.Failed: await _dialogs.ShowMessageAsync("Edit failed", outcome.Reason ?? "The edit failed."); break;
        }
        return outcome;
    }

    /// <summary>Applies a command through the session and maps its outcome (W2-14); returns whether it committed.</summary>
    private async Task<bool> ApplyAsync(ProjectCommand command, string? successStatus = null) =>
        (await ReportOutcomeAsync(await _session.ApplyAsync(command), successStatus)).Status == EditStatus.Committed;

    /// <summary>Applies a value-producing command and maps its outcome; returns the produced id, or null when it did
    /// not commit.</summary>
    private async Task<ElementId?> ApplyAsync(ProjectCommand<ElementId> command, string? successStatus = null)
    {
        EditOutcome<ElementId> outcome = await _session.ApplyAsync(command);
        await ReportOutcomeAsync(outcome, successStatus);
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    /// <summary>Shows help text for the selected element (US-044/US-045, F1) — the element's note, or a generic
    /// message when it has none.</summary>
    private Task Help(TreeNodeViewModel? node) => RunAsync(nameof(Help), async () =>
    {
        string name = node?.DisplayName ?? Constants.AppName;
        string help = node?.ElementId is { } id && _session.Current?.FindById(id) is { } element
            && _session.Current!.View(element).Note is { Length: > 0 } note
            ? note
            : "No specific help is available for this element.";
        await _dialogs.ShowMessageAsync($"Help — {name}", help);
    });

    /// <summary>Inserts an input variable into the programming block's Input section (US-045, Ctrl+I).</summary>
    private Task InsertInput() => InsertBlockPinAsync("inputs", "resource_input", "Input");

    /// <summary>Inserts an output variable into the programming block's Output section (US-045, Ctrl+U).</summary>
    private Task InsertOutput() => InsertBlockPinAsync("outputs", "resource_output", "Output");

    private Task InsertBlockPinAsync(string container, string tag, string label) => RunAsync(nameof(InsertBlockPinAsync), async () =>
    {
        if (!IsProgrammingMode || IsProgrammingBlockLocked || _programmingBlockId is not { } blockId
            || _session.Current?.FindById(blockId)?.FindChild(container) is not { Id: { } sectionId })
        {
            StatusText = IsProgrammingBlockLocked
                ? "This is a locked library block — unlock it to edit its program."
                : "Enter a block's programming mode to insert an input or output.";
            return;
        }
        if (_session.Current is { } project && _session.Commands.AddVariable(project, sectionId, tag, label) is { } command)
            await ApplyAsync(command, $"{label} inserted into the block.");
    });

    /// <summary>Opens the Project information dialog (US-039) prefilled from the project, and applies edits.</summary>
    private Task ProjectInfo() => RunAsync(nameof(ProjectInfo), async () =>
    {
        ProjectInfoData? result = await _dialogs.EditProjectInfoAsync(_session.GetProjectInfo());
        if (result is null || _session.Current is not { } project)
            return;
        await ApplyAsync(_session.Commands.UpdateProjectInfo(project, result), "Project information updated.");
    });

    /// <summary>Documentation ▸ Data tables (US-049): opens the data-tables dialog (read-only system tables +
    /// editable user-defined texts).</summary>
    private Task DataTables() => RunAsync(nameof(DataTables), async () =>
    {
        await _dialogs.ShowDataTablesAsync(new DataTablesViewModel(_session, _dialogs));
    });

    /// <summary>Documentation ▸ Wired module map (US-050): opens the read-only wired input/output module address map.</summary>
    private Task ModuleMap() => RunAsync(nameof(ModuleMap), async () =>
    {
        await _dialogs.ShowModuleMapAsync(_session.GetModuleAddressMap());
    });

    /// <summary>The message shown when a controller-only operation is invoked in this controller-free build (E10).</summary>
    private const string ControllerRequiredMessage =
        "requires a connected controller. This build does not contact a controller (no controller side effects).";

    /// <summary>Controller ▸ Send project (US-042, F5): runs the offline pre-flight — warns about unlinked wireless
    /// products (they can be linked later) — then reports that the actual transfer needs a connected controller (the
    /// controller send/retrieve itself is deferred per E10; this build never contacts a controller).</summary>
    private Task SendProject() => RunAsync(nameof(SendProject), async () =>
    {
        IReadOnlyList<string> unlinked = _session.GetUnlinkedWirelessProducts();
        if (unlinked.Count > 0 &&
            !await _dialogs.ConfirmAsync("Unlinked wireless products",
                $"{unlinked.Count} wireless product(s) are not linked to the controller ({string.Join(", ", unlinked)}). "
                + "They can be linked later. Send anyway?"))
        {
            StatusText = "Send cancelled.";
            return;
        }
        await _dialogs.ShowMessageAsync("Controller required", "Sending the project " + ControllerRequiredMessage);
        StatusText = "Controller transfer requires a connected controller.";
    });

    /// <summary>Controller ▸ Retrieve project (US-043): reports that retrieving needs a connected controller — the
    /// transfer is deferred per E10 and this build never contacts a controller.</summary>
    private Task RetrieveProject() => RunAsync(nameof(RetrieveProject), async () =>
    {
        await _dialogs.ShowMessageAsync("Controller required", "Retrieving a project " + ControllerRequiredMessage);
        StatusText = "Controller transfer requires a connected controller.";
    });

    /// <summary>Documentation ▸ Reports… (US-040 / D14 / T021): open the single Reports view rendering the combined
    /// project-documentation model as ONE navigable HTML document (on-screen or printer variant) — the one command
    /// that replaces the former six direct installation/end-user/function-block screen/print commands.</summary>
    private Task OpenReports() => RunAsync(nameof(OpenReports), async () =>
    {
        if (_session.GenerateProjectDocumentationReport() is not { } report)
            return;   // no project open
        var viewModel = new ReportsViewModel(report, _session.WriteReportHtmlAsync, _dialogs.OpenExternalUrlAsync);
        await _dialogs.ShowReportsAsync(viewModel);
        StatusText = "Reports opened.";
    });

    // T018: AddPowerEvent / ToggleSaveValue / AddSubProgram / AddLogicGroup / SetConditionsOr / SetConditionsAnd /
    // NewCaseValue (US-029/031/033) moved into ProgramAuthoringCoordinator; the VM keeps thin [RelayCommand] entry
    // points delegating their bodies there (the XAML bindings and the *Command tests are unchanged).

    /// <summary>Adds a Powerup system event to the selected Events group (US-033).</summary>
    private Task AddPowerEvent(TreeNodeViewModel? node) => _programAuthoring.AddPowerEventAsync(node);

    /// <summary>Toggles an output's <i>Save current value</i> power-loss persistence (US-033).</summary>
    private Task ToggleSaveValue(TreeNodeViewModel? node) => _programAuthoring.ToggleSaveValueAsync(node);

    /// <summary>Inserts a conditional sub-program into a Commands group (US-029).</summary>
    private Task AddSubProgram(TreeNodeViewModel? node) => _programAuthoring.AddSubProgramAsync(node);

    /// <summary>Inserts a nested logic group inside a Conditions group (US-029).</summary>
    private Task AddLogicGroup(TreeNodeViewModel? node) => _programAuthoring.AddLogicGroupAsync(node);

    /// <summary>Combines a Conditions group with OR (<c>&gt;=1</c>) (US-029).</summary>
    private Task SetConditionsOr(TreeNodeViewModel? node) => _programAuthoring.SetConditionsOrAsync(node);

    /// <summary>Combines a Conditions group with AND (<c>&amp;</c>, the default) (US-029).</summary>
    private Task SetConditionsAnd(TreeNodeViewModel? node) => _programAuthoring.SetConditionsAndAsync(node);

    /// <summary>Adds a case value branch to the selected Case node (US-031).</summary>
    private Task NewCaseValue(TreeNodeViewModel? node) => _programAuthoring.NewCaseValueAsync(node);

    /// <summary>Raised by the <i>Exit</i> command to ask the window to close (the close then runs the save prompt).</summary>
    public event EventHandler? CloseRequested;

    public MainWindowViewModel(
        ProjectWorkflow session,
        IDialogService dialogs,
        RecentProjectsStore recent,
        IThemeService theme,
        AppConfiguration? config = null,
        ILoggerFactory? loggerFactory = null)
    {
        _session = session;
        _dialogs = dialogs;
        _recent = recent;
        _themeService = theme;
        _config = config;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<MainWindowViewModel>();
        CurrentTheme = theme.Current;
        _properties = new PropertiesDialogCoordinator(
            _session, _dialogs, (command, status) => ApplyAsync(command, status), status => StatusText = status);
        _programAuthoring = new ProgramAuthoringCoordinator(
            _session, _dialogs, RunAsync, (command, status) => ApplyAsync(command, status), SelectNode,
            status => StatusText = status, () => SelectedNode, () => _programmingBlockId);
        _treePanes = new TreePaneCoordinator(
            InstallationNodes, FunctionNodes, () => _session.Current, () => _session.LastChange,
            (installHeader, functionsHeader) => { InstallationPaneHeader = installHeader; FunctionsPaneHeader = functionsHeader; });
        DragDrop = new TreeDragDropController(
            _session,
            FindInEitherPane,
            () => IsProgrammingBlockLocked,
            (command, status) => ApplyAsync(command, status),
            _programAuthoring.ArmAndSelect,
            status => StatusText = status,
            RunAsync);
        _linking = new LinkingCoordinator(
            _session, _dialogs, RunAsync, (command, status) => ApplyAsync(command, status), status => StatusText = status,
            () => PendingLinkSource, node => PendingLinkSource = node, RevealAndSelectOpposite);

        Registry = new CommandRegistry(() => Context,
            // The command-parameter bridge: a surface or caller that addresses a specific row (context-menu
            // click, the Delete-key route, existing call sites) passes it as the ICommand parameter; selecting
            // it FIRST makes the context the row's Execute reads BE that node. Null/redundant parameters no-op.
            parameter =>
            {
                if (parameter is TreeNodeViewModel node && !ReferenceEquals(node, SelectedNode))
                    SelectNode(node);
            });
        RegisterCoreEditRows();
        RegisterNodeRows();
        RegisterProgrammingRows();
        RegisterAppRows();
        ContextChanged += (_, _) => Registry.OnContextChanged();

        _onSessionStateChanged = (_, _) => Refresh();
        _onSessionCatalogChanged = (_, _) => RebuildCatalogMenus();
        _onRecentChanged = (_, _) => RefreshRecent();
        _session.StateChanged += _onSessionStateChanged;
        _session.CatalogChanged += _onSessionCatalogChanged;
        _recent.Changed += _onRecentChanged;
        BuildProductMenu();
        RefreshRecent();
        Refresh();
    }

    /// <summary>Detaches the session/recent-store event handlers so this view-model does not leak through those
    /// longer-lived sources (review Low). Called on app shutdown; the session itself is disposed separately.</summary>
    public void Dispose()
    {
        _session.StateChanged -= _onSessionStateChanged;
        _session.CatalogChanged -= _onSessionCatalogChanged;
        _recent.Changed -= _onRecentChanged;
    }

    // Rebuilds the product/function-block insertion menus from the current catalog (US-059/US-060: after an import
    // the newly available components appear here).
    private void RebuildCatalogMenus()
    {
        ProductsMenu.Clear();
        FunctionBlocksMenu.Clear();
        BuildProductMenu();
    }

    private void BuildProductMenu()
    {
        AsyncRelayCommand Insert(CatalogItem product) =>
            new(() => InsertProductAsync(product.Identifier, product.DisplayName));

        // The top categories are derived from the catalog data (H2/D08) — so an imported .def (empty CategoryPath)
        // lands in the "Imported/Uncategorized" bucket instead of being dropped by a hardcoded four-category filter.
        foreach (ProductMenuItemViewModel item in CatalogMenu.BuildProductForest(_session.GetProductCatalogItems(), Insert))
            ProductsMenu.Add(item);

        foreach (ProductMenuItemViewModel item in CatalogMenu.BuildFunctionBlocks(
                     _session.GetFunctionBlockCatalogItems(),
                     fb => new AsyncRelayCommand(() => InsertFunctionBlockAsync(fb.Identifier, fb.DisplayName))))
        {
            FunctionBlocksMenu.Add(item);
        }
    }

    /// <summary>Library ▸ Import catalog file (US-059): imports a single <c>.def</c>/<c>.ifb</c> so its component
    /// becomes insertable; persisted by default (US-061) so it survives a restart.</summary>
    private Task ImportCatalogFile() => RunAsync(nameof(ImportCatalogFile), async () =>
    {
        if (await _dialogs.PickCatalogFileAsync() is not { } path)
            return;
        if (await _session.ImportCatalogFileAsync(path, persist: true))
            StatusText = "Imported 1 component (persisted to the catalog folder).";
    });

    /// <summary>Library ▸ Import catalog folder (US-060): imports every <c>.def</c>/<c>.ifb</c> in a folder and its
    /// subfolders, reporting how many components were imported; persisted by default (US-061).</summary>
    private Task ImportCatalogFolder() => RunAsync(nameof(ImportCatalogFolder), async () =>
    {
        if (await _dialogs.PickCatalogFolderAsync() is not { } dir)
            return;
        int count = await _session.ImportCatalogFolderAsync(dir, persist: true);
        if (count >= 0)
            StatusText = $"Imported {count} component{(count == 1 ? string.Empty : "s")} (persisted to the catalog folder).";
    });

    /// <summary>Inserts an empty function block under the selected locality (US-019). Invoked from the right-click
    /// <i>Empty function block</i> item and Ctrl+Shift+B.</summary>
    private Task InsertEmptyFunctionBlock() => RunAsync(nameof(InsertEmptyFunctionBlock), async () =>
    {
        if (SelectedNode?.ElementId is not { } localityId || _session.Current is not { } project)
        {
            StatusText = "Select a locality first, then insert the empty function block.";
            return;
        }
        string localityName = SelectedNode.DisplayName;
        if (await ApplyAsync(_session.Commands.AddEmptyFunctionBlock(project, localityId, ProjectWorkflow.EmptyBlockName),
                $"{ProjectWorkflow.EmptyBlockName} was inserted under {localityName}") is not { } blockId)
            return;
        // A blank block exists only to be authored, so creating one opens it: both panes re-root at the new block
        // exactly as F3 would (uxparity S-18 — the vendor does this too).
        EnterProgrammingMode(FindNode(FunctionNodes, blockId));
    });

    /// <summary>Inserts a preprogrammed library function block (US-018) under the selected locality — shown in the
    /// Functions pane. Invoked by the leaf commands in <see cref="FunctionBlocksMenu"/>.</summary>
    private Task InsertFunctionBlockAsync(string masterType, string blockName) =>
        RunAsync(nameof(InsertFunctionBlockAsync), async () =>
        {
            if (SelectedNode?.ElementId is not { } localityId || _session.Current is not { } project)
            {
                StatusText = "Select a locality first, then insert the function block.";
                return;
            }
            string localityName = SelectedNode.DisplayName;
            if (_session.Commands.AddFunctionBlock(project, localityId, masterType) is not { } command)
            {
                await _dialogs.ShowMessageAsync("Insert failed", $"No library function block with master type '{masterType}'.");
                return;
            }
            await ApplyAsync(command, $"Function block '{blockName}' has been inserted under {localityName}");
        });

    /// <summary>Parameterless constructor for the XAML designer / template smoke test only.</summary>
    public MainWindowViewModel()
        : this(CreateDesignSession(), new NullDialogService(), new RecentProjectsStore(System.IO.Path.GetTempFileName()), new NullThemeService())
    {
    }

    public Task InitializeAsync(bool skipRecovery = false) => _session.StartAsync(skipRecovery);

    /// <summary>Runs the window-close save prompt (US-064); returns false to cancel the quit.</summary>
    public Task<bool> CanCloseAsync() => _session.CanQuitAsync();

    private Task NewAsync() => RunAsync(nameof(NewAsync), async () =>
    {
        if (await _session.NewAsync())
            StatusText = "Started a new project.";
    });

    private Task OpenAsync() => RunAsync(nameof(OpenAsync), async () =>
    {
        if (await _session.OpenWithPickerAsync())
            StatusText = $"Opened {_session.DocumentName}.";
    });

    [RelayCommand]
    private Task OpenRecentAsync(string path) => RunAsync(nameof(OpenRecentAsync), async () =>
    {
        if (await _session.OpenAsync(path))
            StatusText = $"Opened {_session.DocumentName}.";
    });

    private Task SaveAsync() => RunAsync(nameof(SaveAsync), async () =>
    {
        if (await _session.SaveAsync())
            StatusText = $"Saved {_session.DocumentName}.";
    });

    private Task SaveAsAsync() => RunAsync(nameof(SaveAsAsync), async () =>
    {
        if (await _session.SaveAsAsync())
            StatusText = $"Saved {_session.DocumentName}.";
    });

    private Task CloseAsync() => RunAsync(nameof(CloseAsync), async () =>
    {
        if (await _session.CloseAsync())
            StatusText = "Closed the project.";
    });

    private void Exit() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void ToggleToolbar()
    {
        IsToolbarVisible = !IsToolbarVisible;
        StatusText = IsToolbarVisible ? "Toolbar shown." : "Toolbar hidden.";
    }

    private void ToggleStatusBar() => IsStatusBarVisible = !IsStatusBarVisible;

    [RelayCommand]
    private void SetTheme(AppTheme theme)
    {
        _themeService.Apply(theme);
        CurrentTheme = theme;
        StatusText = $"Theme: {theme}.";
    }

    /// <summary>Inserts a new locality under <i>Localities</i> (US-008), then selects it in the Installation pane.
    /// Invoked from the right-click <i>Insert locality</i> item on the Localities root.</summary>
    private Task InsertLocality() => RunAsync(nameof(InsertLocality), async () =>
    {
        if (_session.Current is not { } project)
            return;
        // Name the container the way the tree does — from the project, not a hard-coded caption. The two must
        // agree: a message reading "under Localities" beside a root row reading "Lokaliteter" names nothing the
        // installer can see.
        string container = project.Child("groups") is { } groups ? project.NameOr(groups, "Localities") : "Localities";
        if (await ApplyAsync(_session.Commands.AddLocality(project, ProjectWorkflow.NewLocalityName),
                $"{ProjectWorkflow.NewLocalityName} was inserted under {container}") is not { } id)
            return;
        // Refresh already rebuilt the trees (StateChanged); highlight the new locality in the Installation pane
        // (which sets it as the active node).
        SelectedInstallationNode = FindNode(InstallationNodes, id);
    });

    /// <summary>Saves a placed function block to a reusable <c>.ifb</c> file (US-021). Invoked from the right-click
    /// <i>Save block…</i> item and Ctrl+G.</summary>
    private Task SaveFunctionBlock(TreeNodeViewModel? node) => RunAsync(nameof(SaveFunctionBlock), async () =>
    {
        if (node?.ElementId is not { } id || _session.Current?.FindById(id) is not { } fb || fb.Kind != ElementKind.FunctionBlock)
            return;
        string currentName = _session.Current!.View(fb).Name ?? "block";
        string currentNote = _session.Current!.View(fb).Note ?? string.Empty;
        PropertiesResult? meta = await _dialogs.EditPropertiesAsync("Save function block", currentName, currentNote,
            affirmative: "Save");   // this dialog goes on to write a file (S-22)
        if (meta is null)
            return;   // cancelled the name/note step
        string? path = await _dialogs.PickSaveFunctionBlockAsync($"{meta.Name}.ifb");
        if (path is null)
            return;   // cancelled the file picker
        if (await _session.SaveFunctionBlockAsync(id, path, meta.Name, meta.Note))
            StatusText = $"Saved function block '{meta.Name}'.";
    });

    /// <summary>Unlocks a locked library function block (US-020) so its internals become editable; the tree rebuild
    /// then shows the editable icon. Invoked from the right-click <i>Unlock</i> item.</summary>
    private Task Unlock(TreeNodeViewModel? node) => RunAsync(nameof(Unlock), async () =>
    {
        if (node?.ElementId is not { } id || _session.Current is not { } project)
            return;
        string name = node.DisplayName;
        // Unlocking takes ownership of the block (uxparity S-20), so it is stamped with whoever did it.
        await ApplyAsync(_session.Commands.UnlockFunctionBlock(project, id, Environment.UserName), $"Unlocked {name}.");
    });

    /// <summary>Deletes the selected node (US-053), dispatching by type: a link row removes its reciprocal pair
    /// (US-057), a locality uses the US-009 cascade, and any other node (product, block, variable, program element)
    /// uses the general confirm-and-cascade delete. Reachable from the right-click item, Edit ▸ Delete, and the
    /// Delete key (US-044) — all three routes run the registry's "edit.delete" command, gated by the row's ONE
    /// SDK-backed gate (the engine's <c>CanDelete</c>), so none can bypass the guard.</summary>
    private Task Delete(TreeNodeViewModel? node) => RunAsync(nameof(Delete), async () =>
    {
        // The localities root is structure, not content: it holds the localities but is not itself a node the
        // installer can remove. It carries no element id (see the projector), so the ElementId guard below already
        // stops it — IsLocalitiesRoot is stated too, to keep that intent explicit on a destructive path.
        if (node is null || node.IsLocalitiesRoot || node.ElementId is not { } id || _session.Current is not { } project)
            return;
        // Preview → dispatch → confirm → apply (W2-13): the SDK decides the delete KIND (sliver #9); the
        // confirmation lives here in the GUI, never below the session.
        DeleteImpact impact = _session.Commands.PreviewDelete(project, id);
        if (!impact.Deletable)
        {
            await _dialogs.ShowMessageAsync("Cannot delete", "This node cannot be deleted.");
            return;
        }
        if (impact.Kind == DeleteKind.Link)
        {
            // Removing a link deletes its reciprocal pair, not a subtree (US-057).
            await ApplyAsync(_session.Commands.RemoveLink(project, id), "Link removed.");
            return;
        }
        string name = node.DisplayName;
        // Prompt for what a node CONTAINS, never for what merely points at it — the vendor's rule, measured:
        // deleting a locality holding function blocks asks (S-09), while deleting a product that other logic links
        // to just goes (S-15), and the resulting file is byte-identical either way. Note NeedsConfirm is still the
        // reference-CASCADE flag below; only the question is dropped.
        if (impact.NeedsConfirm && impact.Kind == DeleteKind.Locality)
        {
            if (!await _dialogs.ConfirmAsync("Delete locality",
                    $"'{name}' contains products. Deleting it also removes those products and the "
                    + "commands and conditions that use them. Delete anyway?"))
            {
                return;   // declined — nothing is deleted
            }
        }
        if (impact.Kind == DeleteKind.Locality)
            await ApplyAsync(_session.Commands.DeleteLocality(project, id), $"Deleted {name}.");   // the US-009 locality worked example
        else
            // impact.NeedsConfirm is the reference-cascade flag PreviewDelete computed for this node.
            await ApplyAsync(_session.Commands.DeleteNode(project, id, impact.NeedsConfirm), $"Deleted {name}.");   // US-053
    });

    // The structural-editing clipboard (US-054/US-056): the id of the cut/copied node and whether it is a cut
    // (paste = move, US-054) or a copy (paste = duplicate, US-056).
    private ElementId? _clipboardId;
    private bool _clipboardIsCut;

    // The ONE clipboard mutation funnel (T010): assigns the value pair and rebuilds the availability context.
    private void SetClipboard(ElementId? id, bool isCut)
    {
        _clipboardId = id;
        _clipboardIsCut = isCut;
        RebuildContext();
    }

    /// <summary>Cut the selected node (US-054, Ctrl+X): stashes it so a Paste onto a locality moves it there.</summary>
    private void Cut(TreeNodeViewModel? node)
    {
        if (node?.ElementId is not { } id)
            return;
        SetClipboard(id, isCut: true);
        StatusText = $"Cut {node.DisplayName} — paste onto a locality to move it.";
    }

    /// <summary>Copy the selected node (US-056, Ctrl+C): stashes it so a Paste onto a locality duplicates it.</summary>
    private void Copy(TreeNodeViewModel? node)
    {
        if (node?.ElementId is not { } id)
            return;
        SetClipboard(id, isCut: false);
        StatusText = $"Copied {node.DisplayName} — paste onto a locality to duplicate it.";
    }

    /// <summary>Paste the clipboard node onto the selected target (US-054 move / US-056 duplicate, Ctrl+V).</summary>
    private Task Paste(TreeNodeViewModel? node) => RunAsync(nameof(Paste), async () =>
    {
        if (_clipboardId is not { } sourceId || node is null || _session.Current is not { } project)
            return;
        // A copied locality pastes onto the locality ROOT — the container localities live in — because a locality
        // does not nest inside another locality. The root row holds no element id of its own (see the projector),
        // so the target is resolved from the project; without this the paste returned silently and a copied
        // locality could be pasted nowhere at all.
        if ((node.IsLocalitiesRoot ? project.Child("groups")?.Id : node.ElementId) is not { } targetId)
            return;
        if (_clipboardIsCut)
        {
            if (await ApplyAsync(_session.Commands.MoveNode(project, sourceId, targetId), "Moved."))
            {
                SetClipboard(null, isCut: false);   // a cut is consumed by its paste
            }
        }
        else if (await ApplyAsync(_session.Commands.CopyNode(project, sourceId, targetId), "Pasted a copy.") is { } pastedId)
        {
            // A copy is not consumed by its paste, so the clipboard stays. Open the arrival all the way down:
            // a pasted subtree lands already populated, so the "reveal on first child" rule never fires for it,
            // and it would otherwise appear as a single closed row giving no sign of what was actually pasted.
            RevealSubtree(pastedId);
        }
    });

    // Opens a just-produced subtree (a paste or insert arrival) all the way down in whichever pane holds it, so the
    // installer sees what landed. A pasted locality appears in both panes, a product/block in one — the pane it is
    // absent from simply finds nothing.
    private void RevealSubtree(ElementId id)
    {
        foreach (var pane in new[] { InstallationNodes, FunctionNodes })
            if (FindNode(pane, id) is { } node)
                node.ExpandSubtree();
    }

    /// <summary>Moves the selected node one position up among its siblings (US-055) — the non-drag reorder route.</summary>
    private Task MoveUp(TreeNodeViewModel? node) => ReorderAsync(node, -1);

    /// <summary>Moves the selected node one position down among its siblings (US-055).</summary>
    private Task MoveDown(TreeNodeViewModel? node) => ReorderAsync(node, +1);

    private Task ReorderAsync(TreeNodeViewModel? node, int delta) => RunAsync(nameof(ReorderAsync), async () =>
    {
        if (node?.ElementId is { } id && _session.Current is { } project
            && _session.Commands.ReorderNode(project, id, delta) is { } command)
            await ApplyAsync(command, delta < 0 ? "Moved up." : "Moved down.");
    });

    /// <summary>Opens the Properties dialog for a tree node to rename a locality (US-007). Invoked from the
    /// right-click <i>Properties</i> item (node passed in) and from F2 (the selected node passed in).</summary>
    private Task Properties(TreeNodeViewModel? node) => RunAsync(nameof(Properties), () => OpenPropertiesAsync(node));

    /// <summary>
    /// Activates a tree node — the double-click route (US-044). IHC Visual opens a per-node-type properties dialog
    /// on double-click and handles the gesture on <em>every</em> node type, which is also what suppresses the
    /// toolkit's expand/collapse default everywhere; the caller must mark the event handled to match.
    /// <para>This is deliberately NOT <see cref="PropertiesCommand"/>. Two cells differ: a <b>pin</b> activates its
    /// <b>parent product's</b> dialog, because the vendor has no per-pin dialog at all — a terminal is configured
    /// from inside the product dialog — whereas F2 on a pin opens OpenVisual's own pin dialog; and the
    /// installation root and a link row open <b>nothing</b>.</para>
    /// </summary>
    [RelayCommand]
    private Task ActivateNode(TreeNodeViewModel? node) => RunAsync(nameof(ActivateNode), () => ActivateNodeAsync(node));

    private Task ActivateNodeAsync(TreeNodeViewModel? node)
    {
        if (node is null || node.IsLocalitiesRoot || node.IsLinkRow || node.ElementId is not { } id
            || _session.Current is not { } project || project.FindById(id) is not { } element)
        {
            return Task.CompletedTask;
        }
        // A pin is configured through its owner, so activation walks up to the product it belongs to. Only a pin
        // redirects: the scenes container is a product child too, but it has its own dialog.
        ElementId target = node.IsPin && IsPinOfProduct(project, element, out ElementId productId) ? productId : id;
        return _properties.OpenAsync(target);
    }

    // True when the element is a resource child of a product (so activating it should open that product's dialog).
    private static bool IsPinOfProduct(Project project, ProjectElement element, out ElementId productId)
    {
        productId = default;
        if (element.Id is not { } id || project.FindParent(id) is not { } parent || parent.Id is not { } parentId
            || !ProductClassifier.IsProduct(parent.Tag))
        {
            return false;
        }
        productId = parentId;
        return true;
    }

    private static TreeNodeViewModel? FindNode(IEnumerable<TreeNodeViewModel> nodes, ElementId id)
    {
        foreach (TreeNodeViewModel node in nodes)
        {
            if (node.ElementId == id)
                return node;
            if (FindNode(node.Children, id) is { } found)
                return found;
        }
        return null;
    }

    // The live tree row for an id, from whichever pane holds it — Installation first (review F16). Most ids live in
    // exactly one pane; a locality appears in both, and for these callers (resolving a command's target, answering
    // the drag controller) either row addresses the same element, so first-match is the answer. NOT for callers that
    // must touch EVERY pane holding the id — see RevealSubtree, which expands both.
    private TreeNodeViewModel? FindInEitherPane(ElementId id) =>
        FindNode(InstallationNodes, id) ?? FindNode(FunctionNodes, id);

    /// <summary>Inserts a catalog product (US-010) under the currently selected locality; the leaf menu commands in
    /// <see cref="WiredProductsMenu"/> call this. Routed through <see cref="RunAsync"/> for tracing and error surfacing.</summary>
    private Task InsertProductAsync(string productIdentifier, string productName) =>
        RunAsync(nameof(InsertProductAsync), async () =>
        {
            if (SelectedNode?.ElementId is not { } localityId || _session.Current is not { } project)
            {
                StatusText = "Select a locality first, then insert the product.";
                return;
            }
            string localityName = SelectedNode.DisplayName;
            if (_session.Commands.AddProduct(project, localityId, productIdentifier) is not { } command)
            {
                await _dialogs.ShowMessageAsync("Insert failed", $"No catalog product with identifier '{productIdentifier}'.");
                return;
            }
            if (_session.Commands.WouldExceedModemLimit(project, productIdentifier))   // at most one modem per project (US-013)
            {
                await _dialogs.ShowMessageAsync("Only one modem",
                    "A project may contain at most one modem. Remove the existing modem before adding another.");
                return;
            }
            // Placing a product ASKS for its documentation as part of placing it, and cancelling places nothing —
            // measured against IHC Visual (uxparity S-12), where the Insert menu raises the product dialog and
            // Annuller leaves both the tree and the id counter untouched. (An earlier note here claimed the vendor
            // does not auto-open on insert; that came from a driver verb which posts the catalog command directly
            // and skips the dialog — see tmp/uxparity/MCPFIXES.md.)
            if (await ApplyAsync(command, $"Product '{productName}' inserted under {localityName}") is not { } newId)
                return;
            if (!await _properties.OpenForInsertAsync(newId))
            {
                // Cancelled: undo the insert. Undo restores the whole project snapshot, so the id counter goes back
                // too — the vendor burns no ids on a cancelled insert either.
                await _session.UndoAsync();
                StatusText = $"Insert of '{productName}' cancelled.";
                return;
            }
            // The placed product opens, showing the terminals it brought — the same reveal a drop does (S-11), and
            // what IHC Visual shows after an insert.
            RevealSubtree(newId);
        });

    /// <summary>Makes <paramref name="node"/> the active node — the insert/command target. Used by tests and by
    /// programmatic selection; the live trees feed the active node through their own two-way selection bindings.</summary>
    public void SelectNode(TreeNodeViewModel node) => SelectedNode = node;

    /// <summary>Toggles a "Log …" row's log mark (US-068, the vendor's &amp;Logmærke): the SDK flips its Logning state
    /// between Off and the first logging mode, and the tree re-renders the row's new state.</summary>
    private Task ToggleLogMark(TreeNodeViewModel? node) => RunAsync(nameof(ToggleLogMark), async () =>
    {
        if (node is { IsLogMarkPin: true, ElementId: { } id } && _session.Current is { } project)
            await ApplyAsync(_session.Commands.ToggleLogMark(project, id), $"Toggled the log mark on {node.DisplayName}.");
    });

    /// <summary>Enters programming mode for the selected function block (US-026, F3): the panes switch to the block's
    /// variable sections (left) and its program subtree (right), both headed with the block's name.</summary>
    private void EnterProgrammingMode(TreeNodeViewModel? node)
    {
        // A PIN opens the program of the block that owns it (uxparity S-28): the vendor offers Vis program on a
        // pin as well as on the block, so you can go straight to the logic that uses the pin.
        if (OwningFunctionBlockOf(node) is { } id)
        {
            AsOneContextRebuild(() =>   // review F03: mode + refresh + authoring gates = ONE transition, one sweep
            {
                _programmingBlockId = id;
                IsProgrammingMode = true;
                Refresh();
                NotifyProgrammingAuthoringGates();
                StatusText = FindNode(FunctionNodes, id)?.IsLockedFunctionBlock == true
                    ? "Programming mode (read-only — the block is locked). Press Esc to return."
                    : "Programming mode — press Esc to return to configuration.";
            });
        }
    }

    // The function block a node belongs to: the block itself, or the block owning the pin/section (S-28). Null
    // when the node is outside any block, which is what makes Show program a no-op on a locality.
    private ElementId? OwningFunctionBlockOf(TreeNodeViewModel? node) =>
        node is { IsFunctionBlock: true, ElementId: { } blockId } ? blockId : OwningFunctionBlockByAncestry(node?.ElementId);

    // The id-based half of the rule, shared with the "view.showProgram" registry gate (which reads the context's
    // node id, not the live node): walks the ancestry for the enclosing function block.
    private ElementId? OwningFunctionBlockByAncestry(ElementId? nodeId)
    {
        ElementId? owner = null;
        if (nodeId is { } start && _session.Current is { } project)
        {
            for (ProjectElement? e = project.FindParent(start); e is not null; e = e.Id is { } id ? project.FindParent(id) : null)
            {
                if (e.Kind == ElementKind.FunctionBlock)
                {
                    owner = e.Id;
                    break;
                }
            }
        }
        return owner;
    }

    // ---- crudarch T012 (proposal §3.3): the divergent family as registry rows. Gates normalise the SDK idioms
    // (gateway queries → verdicts with reasons); the measured US-044/US-068 per-surface divergences (D13,
    // uxparity S-28) are SurfacePolicy DATA — reproduced, never reconciled. ----

    private void RegisterCoreEditRows()
    {
        Registry.Register(new CommandSpec("edit.cut", "Ctrl+X",
            Surfaces.MenuBar | Surfaces.ContextMenu | Surfaces.Toolbar,
            Execute: Sync(ctx => Cut(ResolveNode(ctx))),
            Gate: ctx => ctx.Node is { CanCut: true, Id: not null }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a locality, product or function block to cut."),
            SurfacePolicy: LockedBlockGreysOutsideContextMenu("A locked block cannot be cut from the menu bar.")));

        Registry.Register(new CommandSpec("edit.copy", "Ctrl+C",
            Surfaces.MenuBar | Surfaces.ContextMenu | Surfaces.Toolbar,
            Execute: Sync(ctx => Copy(ResolveNode(ctx))),
            // Bar semantics (D13): ANY pin copies from Rediger — measured on `Tryk (venstre)`.
            Gate: ctx => ctx.Node is { Id: not null } node && (node.CanCopy || node.IsPin)
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a node to copy."),
            // The flyout is NARROWER: Kopier on a product terminal, none on an FB pin (uxparity S-28).
            SurfacePolicy: (ctx, surface) =>
                surface == Surface.ContextMenu && ctx.Node is { } node && !(node.CanCopy || node.IsProductTerminal)
                    ? Availability.Hidden
                    : null));

        Registry.Register(new CommandSpec("edit.paste", "Ctrl+V",
            Surfaces.MenuBar | Surfaces.ContextMenu | Surfaces.Toolbar,
            Execute: ctx => Paste(ResolveNode(ctx)),
            Gate: ctx => ctx.Clipboard is null
                ? EditVerdict.Refuse("Cut or copy a node first.")
                : ctx.Node is { Kind: TreeNodeKind.Locality }
                    ? EditVerdict.Allow
                    : EditVerdict.Refuse("Paste onto a locality.")));

        Registry.Register(new CommandSpec("edit.delete", "Delete",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => Delete(ResolveNode(ctx)),
            // The SDK deletion verdict drives every route — context menu, Edit ▸ Delete AND the Delete key — so a
            // catalog pin or a locked block's interior can never slip through. Asking the COMMAND (the shape MoveGate
            // uses) rather than the boolean CanDelete keeps the reason the engine already computed — "…is a
            // catalog-declared pin of its product", "…inside a locked function block" — so US-044's grey explains
            // itself precisely instead of restating a generic literal the SDK also owns (review F05).
            Gate: ctx => ctx.Node?.Id is { } id && _session.Current is { } project
                ? _session.CanApply(_session.Commands.DeleteNode(project, id, cascade: false))
                : EditVerdict.Refuse("Select an element to delete."),
            SurfacePolicy: LockedBlockGreysOutsideContextMenu("A locked block cannot be deleted from the menu bar.")));

        Registry.Register(new CommandSpec("view.showProgram", "F3",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: Sync(ctx => EnterProgrammingMode(ResolveNode(ctx))),
            // Offered on a block AND on its pins — the vendor jumps from a pin to the program using it (S-28).
            Gate: ctx => ctx.Node is { } node
                && (node.Kind == TreeNodeKind.FunctionBlock ? node.Id : OwningFunctionBlockByAncestry(node.Id)) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a function block to show its program."),
            // The BAR is stricter than the flyout twice over (S-28): only a direct, UNLOCKED block enables it.
            SurfacePolicy: (ctx, surface) =>
                surface == Surface.ContextMenu || ctx.Node is { Kind: TreeNodeKind.FunctionBlock, IsLockedBlock: false }
                    ? null
                    : ctx.Node is { IsLockedBlock: true }
                        ? Availability.Disabled("A locked block's program is opened from the block's own menu.")
                        : Availability.Disabled("Select a function block in the tree.")));
    }

    // crudarch T013: the remaining node-scoped tree commands as rows — gates are the former IsVisible/CanExecute
    // conditions verbatim; the one divergence (Properties: Edit-menu enabled on a link row the flyout omits) is
    // SurfacePolicy data. Bodies stay the existing private methods, resolved via ResolveNode.
    private void RegisterNodeRows()
    {
        Registry.Register(new CommandSpec("insert.locality", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: _ => InsertLocality(),
            Gate: ctx => ctx.Node is { Kind: TreeNodeKind.LocalitiesRoot }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select the Localities root to insert a locality.")));

        Registry.Register(new CommandSpec("insert.emptyFunctionBlock", "Ctrl+Shift+B",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: _ => InsertEmptyFunctionBlock(),
            Gate: ctx => !ctx.InstallationPaneActive && ctx.Node is { Kind: TreeNodeKind.Locality }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a locality in the Functions pane.")));

        Registry.Register(new CommandSpec("node.saveBlock", "Ctrl+G",
            Surfaces.ContextMenu,
            Execute: ctx => SaveFunctionBlock(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { Kind: TreeNodeKind.FunctionBlock }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a function block to save.")));

        Registry.Register(new CommandSpec("node.unlock", null,
            Surfaces.ContextMenu,
            Execute: ctx => Unlock(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsLockedBlock: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Only a locked library block can be unlocked.")));

        Registry.Register(new CommandSpec("node.toggleLogMark", null,
            Surfaces.ContextMenu,
            Execute: ctx => ToggleLogMark(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsLogMarkPin: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a log-markable pin.")));

        Registry.Register(new CommandSpec("help.onNode", "F1",
            Surfaces.MenuBar,
            Execute: ctx => Help(ResolveNode(ctx)),
            Gate: _ => EditVerdict.Allow));   // F1 always answers — with or without a selection

        Registry.Register(new CommandSpec("node.useInProgram", null,
            Surfaces.ContextMenu,
            Execute: Sync(ctx => UseInProgram(ResolveNode(ctx))),
            Gate: ctx => ctx.Node is { IsPin: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a variable or pin.")));

        Registry.Register(new CommandSpec("link.startFromHere", null,
            Surfaces.ContextMenu,
            Execute: Sync(ctx => StartLink(ResolveNode(ctx))),
            Gate: ctx => ctx.Node is { IsPin: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a pin to link from.")));

        Registry.Register(new CommandSpec("link.toHere", null,
            Surfaces.ContextMenu,
            Execute: ctx => LinkToHere(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsLinkTarget: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a pin or scenes container to link to.")));

        Registry.Register(new CommandSpec("link.jumpOpposite", "F4",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: Sync(ctx => NavigateLinkOpposite(ResolveNode(ctx))),
            Gate: ctx => ctx.Node is { IsLinkRow: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a link row to jump to its opposite half.")));

        Registry.Register(new CommandSpec("edit.moveUp", "Ctrl+Shift+Up",
            Surfaces.ContextMenu,
            Execute: ctx => MoveUp(ResolveNode(ctx)),
            Gate: ctx => MoveGate(ctx, -1)));

        Registry.Register(new CommandSpec("edit.moveDown", "Ctrl+Shift+Down",
            Surfaces.ContextMenu,
            Execute: ctx => MoveDown(ResolveNode(ctx)),
            Gate: ctx => MoveGate(ctx, +1)));

        Registry.Register(new CommandSpec("node.properties", "F2",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => Properties(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { Id: not null, Kind: not TreeNodeKind.LocalitiesRoot }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a node with properties."),
            // The flyout is narrower than Rediger: no Egenskaber on a link row (measured) — the bar keeps it.
            SurfacePolicy: (ctx, surface) => surface == Surface.ContextMenu && ctx.Node is { IsLinkRow: true }
                ? Availability.Hidden
                : null));
    }

    // crudarch T014: the programming-mode set as rows. Authoring gates = container kind + the A-27 locked-block
    // withdrawal (flyout omits via the evaluator's transient-surface default; bar greys with the reason); the
    // mode commands gate on ShellContext.IsProgrammingMode.
    private void RegisterProgrammingRows()
    {
        Registry.Register(new CommandSpec("program.leaveMode", "Escape",
            Surfaces.MenuBar,
            Execute: Sync(_ => LeaveProgrammingMode()),
            Gate: ctx => ctx.IsProgrammingMode
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Already in configuration view.")));

        Registry.Register(new CommandSpec("program.insertInput", "Ctrl+I",
            Surfaces.None,   // keybinding-only (Ctrl+I) — no menu surface
            Execute: _ => InsertInput(),
            Gate: ProgrammingAuthoringGate));

        Registry.Register(new CommandSpec("program.insertOutput", "Ctrl+U",
            Surfaces.None,   // keybinding-only (Ctrl+U)
            Execute: _ => InsertOutput(),
            Gate: ProgrammingAuthoringGate));

        Registry.Register(new CommandSpec("program.addPowerEvent", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => AddPowerEvent(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsEventsContainer: true } && !ctx.ProgrammingBlockLocked
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select an events group in an unlocked block.")));

        Registry.Register(new CommandSpec("program.toggleSaveValue", null,
            Surfaces.ContextMenu,
            Execute: ctx => ToggleSaveValue(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsOutputPin: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select an output.")));

        Registry.Register(new CommandSpec("program.addSubProgram", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => AddSubProgram(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsCommandsContainer: true } && !ctx.ProgrammingBlockLocked
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a command group in an unlocked block.")));

        Registry.Register(new CommandSpec("program.addLogicGroup", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => AddLogicGroup(ResolveNode(ctx)),
            Gate: ConditionsGate));

        Registry.Register(new CommandSpec("program.setConditionsOr", null,
            Surfaces.ContextMenu,
            Execute: ctx => SetConditionsOr(ResolveNode(ctx)),
            Gate: ConditionsGate));

        Registry.Register(new CommandSpec("program.setConditionsAnd", null,
            Surfaces.ContextMenu,
            Execute: ctx => SetConditionsAnd(ResolveNode(ctx)),
            Gate: ConditionsGate));

        Registry.Register(new CommandSpec("program.newCaseValue", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => NewCaseValue(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsCaseNode: true } && !ctx.ProgrammingBlockLocked
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Select a case in an unlocked block.")));
    }

    // crudarch T015: the app-level rows. Most gate on ProjectOpen or Allow; Save is ALWAYS enabled (D07 —
    // vendor parity); OpenRecent/SetTheme stay parameterized item commands (non-rows).
    private void RegisterAppRows()
    {
        // T017 (US-052/U-BP-07): Undo/Redo gate on the document's history — greyed when empty. The XAML owns the
        // captions, which here are DYNAMIC and action-named (UndoMenuHeader/RedoMenuHeader).
        RegisterAppRow("edit.undo", "Ctrl+Z", _ => Undo(),
            ctx => ctx.CanUndo ? EditVerdict.Allow : EditVerdict.Refuse("Nothing to undo."));
        RegisterAppRow("edit.redo", "Ctrl+Y", _ => Redo(),
            ctx => ctx.CanRedo ? EditVerdict.Allow : EditVerdict.Refuse("Nothing to redo."));
        RegisterAppRow("file.new", "Ctrl+N", _ => NewAsync(), AllowGate,
            Surfaces.MenuBar | Surfaces.Toolbar);
        RegisterAppRow("file.open", "Ctrl+O", _ => OpenAsync(), AllowGate,
            Surfaces.MenuBar | Surfaces.Toolbar);
        RegisterAppRow("file.save", "Ctrl+S", _ => SaveAsync(), AllowGate,   // D07: always enabled
            Surfaces.MenuBar | Surfaces.Toolbar);
        RegisterAppRow("file.saveAs", null, _ => SaveAsAsync(), ProjectOpenGate);
        RegisterAppRow("file.close", null, _ => CloseAsync(), ProjectOpenGate);
        RegisterAppRow("app.exit", null, Sync(_ => Exit()), AllowGate);
        RegisterAppRow("view.toggleToolbar", null, Sync(_ => ToggleToolbar()), AllowGate);
        RegisterAppRow("view.toggleStatusBar", null, Sync(_ => ToggleStatusBar()), AllowGate);
        RegisterAppRow("project.info", null, _ => ProjectInfo(), ProjectOpenGate);
        RegisterAppRow("project.dataTables", null, _ => DataTables(), ProjectOpenGate);
        RegisterAppRow("project.moduleMap", null, _ => ModuleMap(), ProjectOpenGate);
        RegisterAppRow("controller.send", "F5", _ => SendProject(), ProjectOpenGate,
            Surfaces.MenuBar | Surfaces.Toolbar);   // T020: a real toolbar button (persistent surface)
        RegisterAppRow("controller.retrieve", null, _ => RetrieveProject(), AllowGate,
            Surfaces.MenuBar | Surfaces.Toolbar);
        RegisterAppRow("reports.open", null, _ => OpenReports(), ProjectOpenGate);
        RegisterAppRow("catalog.importFile", null, _ => ImportCatalogFile(), AllowGate);
        RegisterAppRow("catalog.importFolder", null, _ => ImportCatalogFolder(), AllowGate);
        RegisterAppRow("help.about", null, _ => AboutAsync(), AllowGate,
            Surfaces.MenuBar | Surfaces.Toolbar);
        RegisterAppRow("app.settings", null, _ => ShowSettingsAsync(), AllowGate);
        RegisterAppRow("app.telemetryDiagnostics", null, _ => TelemetryDiagnosticsAsync(), AllowGate);
    }

    // App-level rows default to bar-only placement; the two controller commands also ride the toolbar (T020).
    private void RegisterAppRow(string id, string? gesture, Func<ShellContext, Task> execute,
        Func<ShellContext, EditVerdict> gate, Surfaces placement = Surfaces.MenuBar) =>
        Registry.Register(new CommandSpec(id, gesture, placement, execute, gate));

    // A row's Execute is Func<ShellContext, Task>, but many command bodies are plain void — this is the ONE home
    // for the sync→async ceremony they need, instead of a `{ …; return Task.CompletedTask; }` block per row
    // (review F13). What each row then shows is its actual body, not the adapter around it.
    private static Func<ShellContext, Task> Sync(Action<ShellContext> body) =>
        ctx => { body(ctx); return Task.CompletedTask; };

    private static EditVerdict AllowGate(ShellContext ctx) => EditVerdict.Allow;

    private static EditVerdict ProjectOpenGate(ShellContext ctx) =>
        ctx.ProjectOpen ? EditVerdict.Allow : EditVerdict.Refuse("No project is open.");

    // Ctrl+I/Ctrl+U pin authoring: only inside an UNLOCKED block's programming view (A-27).
    private EditVerdict ProgrammingAuthoringGate(ShellContext ctx) =>
        ctx.IsProgrammingMode && !ctx.ProgrammingBlockLocked
            ? EditVerdict.Allow
            : EditVerdict.Refuse("Open an unlocked block's program first.");

    // Conditions-group authoring (US-029): a conditions/logic group in an unlocked block.
    private EditVerdict ConditionsGate(ShellContext ctx) =>
        ctx.Node is { IsConditionsContainer: true } && !ctx.ProgrammingBlockLocked
            ? EditVerdict.Allow
            : EditVerdict.Refuse("Select a conditions group in an unlocked block.");

    // Move up/down (US-055/US-068 D07, crudarch T018/G6): a reorderable structural node, an unlocked
    // programming block (F-087), AND actual reorderability in the asked direction — the document's index-backed
    // CanReorder probe applies the same boundary rule the ReorderNode factory does plus the command's own verdict,
    // so the keybindings stop firing no-ops, the flyout omits an impossible move, and this gate (re-run on every
    // selection change, twice) costs dictionary lookups instead of tree walks and mints nothing (review F02).
    private EditVerdict MoveGate(ShellContext ctx, int delta) =>
        ctx.Node is { CanReorder: true, Id: { } id } && !ctx.ProgrammingBlockLocked
            ? _session.CanReorder(id, delta)
                ? EditVerdict.Allow
                : EditVerdict.Refuse(delta < 0 ? "Already first among its siblings." : "Already last among its siblings.")
            : EditVerdict.Refuse("Select a locality, product or function block to move.");

    // The shared S-28 bar rule for Cut/Delete: the menu bar greys a locked (library) block's structural
    // commands while its own context menu still offers them — and they really run there (D13).
    private static Func<ShellContext, Surface, Availability?> LockedBlockGreysOutsideContextMenu(string reason) =>
        (ctx, surface) => surface != Surface.ContextMenu && ctx.Node is { IsLockedBlock: true }
            ? Availability.Disabled(reason)
            : null;

    // Resolves the context row back to its live tree node for the command bodies; the id-less Localities root
    // falls back to the selection (it IS the selected row whenever its context is active).
    private TreeNodeViewModel? ResolveNode(ShellContext ctx) =>
        ctx.Node?.Id is { } id ? FindInEitherPane(id) : SelectedNode;

    // The locked-block authoring gates depend on which block is being programmed; re-evaluate them when that changes.
    private void NotifyProgrammingAuthoringGates()
    {
        OnPropertyChanged(nameof(IsProgrammingBlockLocked));
        OnPropertyChanged(nameof(CanInsertVariable));
        OnPropertyChanged(nameof(CanAddEvent));
        OnPropertyChanged(nameof(CanAddCommand));
        OnPropertyChanged(nameof(CanAddCondition));
        RebuildContext();   // T012/T013: every registry row re-evaluates off the lock/mode state
    }

    /// <summary>Leaves programming mode (US-026, Esc), restoring the two locality trees of configuration mode.</summary>
    private void LeaveProgrammingMode()
    {
        if (!IsProgrammingMode)
            return;
        AsOneContextRebuild(() =>   // review F03: mode + refresh + authoring gates = ONE transition, one sweep
        {
            IsProgrammingMode = false;
            _programmingBlockId = null;
            Refresh();
            NotifyProgrammingAuthoringGates();
            StatusText = "Configuration mode.";
        });
    }

    /// <summary>Links two pins (US-022/US-023) — a thin entry point delegating to <see cref="LinkingCoordinator"/>
    /// (the drag path and the LinkPins characterization test drive this).</summary>
    public Task LinkPins(TreeNodeViewModel? source, TreeNodeViewModel? target) => _linking.LinkPinsAsync(source, target);

    /// <summary>The pin from which a link is being drawn — armed by <i>Link from here</i>, consumed by
    /// <i>Link to here</i> (US-022). The two-step gesture is the reliable, testable substitute for pin drag-and-drop.</summary>
    [ObservableProperty] private TreeNodeViewModel? _pendingLinkSource;

    /// <summary>Arms a link from the given pin (US-022) — delegates to <see cref="LinkingCoordinator"/>.</summary>
    private void StartLink(TreeNodeViewModel? node) => _linking.StartLink(node);

    /// <summary>Completes a link onto the given pin or scenes container (US-022/US-024) — delegates to
    /// <see cref="LinkingCoordinator"/>.</summary>
    private Task LinkToHere(TreeNodeViewModel? node) => _linking.LinkToHereAsync(node);

    /// <summary>Home: selects the FIRST row of the pane (uxparity S-29 — the vendor lands on the tree root).</summary>
    [RelayCommand]
    private void SelectFirstRow(bool functionsPane)
    {
        IReadOnlyList<TreeNodeViewModel> pane = functionsPane ? FunctionNodes : InstallationNodes;
        if (pane.Count > 0)
            SelectRowInPane(pane[0], functionsPane);
    }

    /// <summary>End: selects the LAST row currently VISIBLE in the pane — the deepest last descendant reachable
    /// through expanded nodes only, which is what a tree walk with the caret would reach (uxparity S-29).</summary>
    [RelayCommand]
    private void SelectLastVisibleRow(bool functionsPane)
    {
        IReadOnlyList<TreeNodeViewModel> pane = functionsPane ? FunctionNodes : InstallationNodes;
        if (pane.Count == 0)
            return;
        TreeNodeViewModel last = pane[^1];
        while (last.IsExpanded && last.Children.Count > 0)
            last = last.Children[^1];
        SelectRowInPane(last, functionsPane);
    }

    // Selects a row THE WAY THE CONTROL SEES IT. SelectNode only sets the SelectedNode aggregate, which the
    // trees do NOT bind to — their SelectedItem binds to SelectedInstallationNode/SelectedFunctionsNode, and
    // the flow between them is one-way (pane property → aggregate). Setting the aggregate alone therefore
    // moves nothing on screen while looking correct to a view-model test (uxparity S-29).
    private void SelectRowInPane(TreeNodeViewModel node, bool functionsPane)
    {
        if (functionsPane)
            SelectedFunctionsNode = node;
        else
            SelectedInstallationNode = node;
    }

    /// <summary>Jumps from a link row to the opposite end (US-025, F4) — delegates the link logic to
    /// <see cref="LinkingCoordinator"/>, which calls back <see cref="RevealAndSelectOpposite"/> for the tree reveal.</summary>
    private void NavigateLinkOpposite(TreeNodeViewModel? node) => _linking.NavigateLinkOpposite(node);

    // Reveals + selects the opposite pin in whichever pane holds it — the tree-navigation view-state the linking
    // coordinator calls back into after computing the opposite end (A-6/F-012).
    private void RevealAndSelectOpposite(ElementId oppositeId)
    {
        bool inFunctionsPane = false;
        if (FindNode(InstallationNodes, oppositeId) is { } installationNode)
        {
            ExpandAncestors(InstallationNodes, oppositeId);   // realize the target so the selection sticks (A-6)
            SelectedInstallationNode = installationNode;
        }
        else if (FindNode(FunctionNodes, oppositeId) is { } functionsNode)
        {
            ExpandAncestors(FunctionNodes, oppositeId);
            SelectedFunctionsNode = functionsNode;
            inFunctionsPane = true;
        }
        else
        {
            return;
        }
        StatusText = $"Jumped to {SelectedNode?.DisplayName}.";
        // Keyboard focus follows the caret across the panes (uxparity S-25) — otherwise the jump moves the selection
        // somewhere the arrow keys and F4 cannot reach without first pressing F6.
        JumpedToPane?.Invoke(this, inFunctionsPane);
    }

    /// <summary>Raised after an F4 jump so the view can move keyboard focus into the pane that now holds the caret
    /// (uxparity S-25). The argument is true when that is the Functions pane.</summary>
    public event EventHandler<bool>? JumpedToPane;

    // Expands every ancestor on the path to the node with the given id (not the node itself), so the target is
    // realized and scrolled into view when it is selected — the F4 jump's missing half (A-6/F-012).
    private static bool ExpandAncestors(IEnumerable<TreeNodeViewModel> nodes, ElementId id)
    {
        foreach (TreeNodeViewModel node in nodes)
        {
            if (node.ElementId == id)
                return true;
            if (ExpandAncestors(node.Children, id))
            {
                node.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    // CompleteSceneLinkAsync (the scenario-link value flow, US-024) moved to LinkingCoordinator (T017).

    // The Properties route (right-click / F2) dispatches by element type: a modem opens the modem dialog (US-013),
    // any other product the documentation dialog (US-011), an I/O pin the addressing dialog (US-012), a locality the
    // rename dialog (US-007).
    private Task OpenPropertiesAsync(TreeNodeViewModel? node) =>
        node?.ElementId is { } id ? _properties.OpenAsync(id) : Task.CompletedTask;

    private Task InsertEnumAsync(ElementId sectionId, string sectionLabel) => RunAsync(nameof(InsertEnumAsync), async () =>
    {
        EnumDefinitionResult? result = await _dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput("New enumerator", string.Empty, System.Array.Empty<string>(), IsNew: true));
        if (result is null || string.IsNullOrWhiteSpace(result.TypeName))
            return;
        if (_session.Current is { } project
            && _session.Commands.AddEnumVariable(project, sectionId, result.TypeName, result.TypeName, result.States) is { } command)
            await ApplyAsync(command, $"Enumerator '{result.TypeName}' was inserted under {sectionLabel}");
    });

    // PG-7/D02: authors a standalone (0-state, unreferenced) enumerator TYPE — no variable is inserted, decoupled from
    // any section. The enumerator dialog supplies the name (and any states); an empty type is authored when none given.
    private Task AddStandaloneEnumTypeAsync() => RunAsync(nameof(AddStandaloneEnumTypeAsync), async () =>
    {
        EnumDefinitionResult? result = await _dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput("New standalone enumerator type", string.Empty, System.Array.Empty<string>(), IsNew: true));
        if (result is null || string.IsNullOrWhiteSpace(result.TypeName))
            return;
        if (_session.Current is { } project)
            await ApplyAsync(_session.Commands.AddStandaloneEnumType(project, result.TypeName, result.States),
                $"Enumerator type '{result.TypeName}' was created");
    });

    // PG-4: inserts a variable of an EXISTING enumerator type — references its def-id, authoring NO new type (the "New…"
    // option above authors a new one).
    private Task InsertEnumOfExistingTypeAsync(ElementId sectionId, string typeName, string sectionLabel) =>
        RunAsync(nameof(InsertEnumOfExistingTypeAsync), async () =>
        {
            if (_session.Current is { } project
                && _session.Commands.AddEnumVariableOfType(project, sectionId, typeName, typeName) is { } command)
                await ApplyAsync(command, $"Enumerator '{typeName}' was inserted under {sectionLabel}");
        });


    private Task AboutAsync() => RunAsync(nameof(AboutAsync), () => _dialogs.ShowAboutAsync());

    private Task ShowSettingsAsync() => RunAsync(nameof(ShowSettingsAsync), () => _dialogs.ShowSettingsAsync(BuildSettingsText()));

    private Task TelemetryDiagnosticsAsync() => RunAsync(nameof(TelemetryDiagnosticsAsync), async () =>
    {
        string? host = _config?.TelemetryConfig.Host;
        if (string.IsNullOrWhiteSpace(host))
            await _dialogs.ShowMessageAsync("Telemetry diagnostics", "No telemetry host is configured in ihcsettings.json.");
        else
            await _dialogs.OpenExternalUrlAsync(host);
    });

    private async Task RunAsync(string operation, Func<Task> action)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(MainWindowViewModel)}.{operation}", ActivityKind.Internal);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Ihc.ActivityExtensions.SetError(activity, ex);
            _logger.LogError(ex, "Command {Operation} failed", operation);
            StatusText = $"Error: {ex.Message}";
            await _dialogs.ShowMessageAsync("Unexpected error", ex.Message);
        }
    }

    /// <summary>The current availability context (crudarch T010, §3.2) — ONE immutable snapshot every surface
    /// gate reads (ids and value flags only; see <see cref="ShellContext"/>). Rebuilt only by
    /// <see cref="RebuildContext"/>; consumers react to <see cref="ContextChanged"/>.</summary>
    public ShellContext Context { get; private set; } = ShellContext.Empty;

    /// <summary>Raised after every <see cref="Context"/> rebuild — the ONE announcement the command registry
    /// (T011) subscribes to.</summary>
    public event EventHandler? ContextChanged;

    // crudarch T010 (§3.2): the ONE context rebuild — every trigger funnels here: selection
    // (OnSelectedNodeChanged), pane (OnIsInstallationPaneActiveChanged), mode (OnIsProgrammingModeChanged),
    // clipboard (SetClipboard), and every document transition (Refresh, driven by the session's StateChanged).
    // Projects VALUES only — ids and flags copied at rebuild time, never the live node or a Project reference.
    // Set while a composite transition runs, so the triggers it re-enters mark nothing (review F03).
    private bool _contextRebuildSuspended;

    // Runs a transition that fires several context triggers from the inside — the mode assignment, the selection
    // restore, the authoring-gate notify — as ONE rebuild: the inner triggers are suspended and the single sweep
    // runs at the end. Nested scopes fold into the outermost one, so Refresh composes inside a mode switch.
    private void AsOneContextRebuild(Action transition)
    {
        bool outer = _contextRebuildSuspended;
        _contextRebuildSuspended = true;
        try
        {
            transition();
        }
        finally
        {
            _contextRebuildSuspended = outer;
            RebuildContext();   // T010: every transition rebuilds the context (selection/mode/dirty/undo/lock state)
        }
    }

    private void RebuildContext()
    {
        if (_contextRebuildSuspended)
        {
            return;
        }
        TreeNodeViewModel? node = SelectedNode;
        Context = new ShellContext(
            ProjectOpen: _session.Current is not null,
            IsProgrammingMode: IsProgrammingMode,
            ProgrammingBlockLocked: IsProgrammingBlockLocked,
            InstallationPaneActive: IsInstallationPaneActive,
            Node: node is null
                ? null
                : new NodeContext(
                    node.ElementId, node.Kind,
                    node.IsPin, node.IsProductTerminal, node.IsLinkRow, node.IsLinkTarget, node.IsLogMarkPin,
                    node.IsOutputPin, node.IsEventsContainer, node.IsCommandsContainer, node.IsConditionsContainer, node.IsCaseNode,
                    node.IsLockedFunctionBlock,
                    node.CanCut, node.CanCopy, node.CanReorder),
            Clipboard: _clipboardId is { } clipboardSource ? new ClipboardContext(clipboardSource, _clipboardIsCut) : null,
            CanUndo: _session.CanUndo, CanRedo: _session.CanRedo);
        ContextChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsInstallationPaneActiveChanged(bool value) => RebuildContext();   // T010: pane trigger

    partial void OnIsProgrammingModeChanged(bool value) => RebuildContext();   // T010: mode trigger

    // The identity of the view last built into the panes. An in-place rebuild (every edit fires StateChanged →
    // Refresh) keeps the same key, so the panes' expand/collapse state is carried across (US-070); a deliberate
    // MODE switch (config ⇄ a block's programming view) changes the key, so that view opens fresh at its defaults.
    // review F03: a refresh re-enters the context triggers from inside — RestoreSelection fires the selection
    // trigger and the config path assigns IsProgrammingMode — so it used to sweep the whole registry 2–3 times
    // back to back per edit/undo/load. It is one document transition, so it is ONE rebuild.
    private void Refresh() => AsOneContextRebuild(() =>
        {
            // D07 (U-BP-06): the dirty bullet marks unsaved changes in the title; Save itself stays always-enabled.
            Title = $"{_session.DocumentName}{(_session.IsDirty ? "•" : string.Empty)} - {Constants.AppName}";
            OnPropertyChanged(nameof(UndoMenuHeader));   // the history may have grown/shrunk — refresh the Edit-menu labels (E14)
            OnPropertyChanged(nameof(RedoMenuHeader));
            if (IsProgrammingMode && _programmingBlockId is { } blockId
                && _session.Current?.FindById(blockId) is { } block && block.Kind == ElementKind.FunctionBlock)
            {
                // BuildProgrammingTrees clears and rebuilds both panes (fresh node instances), so — exactly like the
                // config-mode fallback below — capture the selection by id and restore it after, else a program edit
                // (every edit fires StateChanged → Refresh) drops the selected container to an orphan (review C5).
                RebuildPreservingSelection(() =>
                    _treePanes.BuildProgrammingTrees(block, preserveExpansion: _treePanes.SameViewAsLastBuild("prog:" + blockId.ToToken())));
            }
            else
            {
                IsProgrammingMode = false;   // the block is gone (or never set) → configuration mode
                _programmingBlockId = null;
                InstallationPaneHeader = "Installation";
                FunctionsPaneHeader = "Functions";
                bool sameView = _treePanes.SameViewAsLastBuild("config");
                // Reconcile in place when this is an incremental transition on the SAME view whose panes still hold the
                // reconcilers' roots — edits AND undo/redo, whose outcomes carry their exact delta (crudarch G3/T007);
                // otherwise (load/save/close/mode switch/first build — LastChange null — or panes out of sync) rebuild
                // through the reconciler, which re-seeds it (W3-6 keeps the fallback permanent, US-070).
                if (!(sameView && _treePanes.TryReconcileConfig()))
                {
                    // The full-rebuild fallback tears down the node instances, so the reconcile path's by-identity survival
                    // of the installer's place is lost here — capture selection (which Avalonia's focus + scroll-into-view
                    // follow) by id before the rebuild and restore it after, so a load (or any reconcile fallback) lands
                    // the user back where they were (E14 place restore). Expansion is carried inside the coordinator's fallback.
                    RebuildPreservingSelection(() => _treePanes.RebuildConfig(preserve: sameView));
                }
            }
        });

    // Captures the per-pane selection by id, runs a full <paramref name="rebuild"/> that replaces the pane nodes with
    // fresh instances, then re-selects those ids — the shared guard both Refresh rebuild branches use so a rebuild
    // (every edit fires StateChanged → Refresh) never drops the selected node to an orphan (review C5 / E14 place restore).
    private void RebuildPreservingSelection(Action rebuild)
    {
        ElementId? selInstallation = SelectedInstallationNode?.ElementId;
        ElementId? selFunctions = SelectedFunctionsNode?.ElementId;
        bool installationActive = IsInstallationPaneActive;
        rebuild();
        RestoreSelection(selInstallation, selFunctions, installationActive);
    }

    // Re-selects, in each pane, the node standing for the id selected before a fallback rebuild (a rebuilt tree holds
    // fresh instances, so the old selection would dangle). The active pane is restored LAST so IsInstallationPaneActive
    // and SelectedNode — which the pane-selection change handlers set — settle on it; a selected node the edit removed
    // simply isn't found and its selection clears.
    private void RestoreSelection(ElementId? installationId, ElementId? functionsId, bool installationActive)
    {
        TreeNodeViewModel? installation = installationId is { } iid ? FindNode(InstallationNodes, iid) : null;
        TreeNodeViewModel? functions = functionsId is { } fid ? FindNode(FunctionNodes, fid) : null;
        if (installationActive)
        {
            SelectedFunctionsNode = functions;
            SelectedInstallationNode = installation;
        }
        else
        {
            SelectedInstallationNode = installation;
            SelectedFunctionsNode = functions;
        }
        IsInstallationPaneActive = installationActive;
    }

    private void RefreshRecent()
    {
        RecentProjects.Clear();
        foreach (string path in _recent.Items)
            RecentProjects.Add(new RecentProjectViewModel(path, OpenRecentCommand));
    }

    private string BuildSettingsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Application: {Constants.AppName} {Ihc.Bootstrap.AppTelemetryBootstrap.GetAppVersionStr()}");
        sb.AppendLine($"SDK: {Ihc.VersionInfo.GetSdkVersionStr()}");
        sb.AppendLine();
        if (_config is null)
        {
            sb.AppendLine("No configuration loaded.");
            return sb.ToString();
        }

        sb.AppendLine($"Settings file: {(_config.SettingsFileFound ? _config.SettingsFilePath : "(none — using defaults)")}");
        sb.AppendLine();
        sb.AppendLine("Controller:");
        sb.AppendLine($"  Endpoint: {OrNone(_config.IhcSettings.Endpoint)}");
        sb.AppendLine($"  User: {OrNone(_config.IhcSettings.UserName)}");
        sb.AppendLine();
        sb.AppendLine("Telemetry:");
        sb.AppendLine($"  Logs: {OrNone(_config.TelemetryConfig.Logs)}");
        sb.AppendLine($"  Traces: {OrNone(_config.TelemetryConfig.Traces)}");
        sb.AppendLine($"  Self-check: {OrNone(_config.TelemetryConfig.SelfCheckEndpoint)}");
        return sb.ToString();

        static string OrNone(string? value) => string.IsNullOrWhiteSpace(value) ? "(not set)" : value;
    }

    private static ProjectWorkflow CreateDesignSession()
    {
        var service = new Ihc.Vis.ProjectAppService(new Ihc.IhcSettings());
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ihc_openvisual_design");
        return new ProjectWorkflow(service, new BackupService(tempDir), new RecentProjectsStore(System.IO.Path.GetTempFileName()), new NullDialogService());
    }
}
