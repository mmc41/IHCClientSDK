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
    public IAsyncRelayCommand ModuleMapCommand => Registry.Commands["project.moduleMap"];
    public IAsyncRelayCommand SendProjectCommand => Registry.Commands["controller.send"];
    public IAsyncRelayCommand RetrieveProjectCommand => Registry.Commands["controller.retrieve"];
    public IAsyncRelayCommand FunctionsReportCommand => Registry.Commands["reports.functions"];
    public IAsyncRelayCommand InstallationReportCommand => Registry.Commands["reports.installation"];
    public IAsyncRelayCommand FunctionBlocksReportCommand => Registry.Commands["reports.functionBlocks"];
    public IAsyncRelayCommand ManageEnumTypesCommand => Registry.Commands["library.manageEnumTypes"];
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
    public IAsyncRelayCommand AddProgramCommand => Registry.Commands["program.addProgram"];
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
    [ObservableProperty] private string _statusText = "Tryk F1 for hjælp";

    /// <summary>
    /// Whether the application currently has a controller connection (W9/F10) — surfaced as an indicator at the
    /// right-hand end of the status bar, where the reference application puts its own. This build never contacts a
    /// controller (E10 is an offline slice), so it stays false; the property exists so the indicator reflects BOTH
    /// states rather than being a permanent decoration.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ControllerConnectionIcon))]
    [NotifyPropertyChangedFor(nameof(ControllerConnectionText))]
    private bool _isControllerConnected;

    // The connection is an availability trigger: the two transfer commands are withheld without one (F-4), so a
    // change has to reach the registry through the same single funnel every other trigger uses.
    partial void OnIsControllerConnectedChanged(bool value) => RebuildContext();

    /// <summary>The indicator's glyph. The two states are two GLYPHS, not one glyph in two colours — a colour-only
    /// signal fails `docs/icons_design.md` and is invisible to a colour-blind installer.</summary>
    public string ControllerConnectionIcon => NodeIcons.ControllerConnection(IsControllerConnected);

    /// <summary>The indicator's tooltip and accessible name — the state in words, for the same reason.</summary>
    public string ControllerConnectionText =>
        IsControllerConnected ? "Forbundet til controller" : "Ikke forbundet til controller";
    [ObservableProperty] private string _installationPaneHeader = "Installation";
    [ObservableProperty] private string _functionsPaneHeader = "Funktioner";

    /// <summary>Whether the window is in programming mode (one function block's variables + program), vs the two
    /// locality trees of configuration mode (US-026).</summary>
    [ObservableProperty] private bool _isProgrammingMode;
    private ElementId? _programmingBlockId;

    /// <summary>The one writer of the programming-mode pair: the block id IS the state, and
    /// <see cref="IsProgrammingMode"/> is the derived <c>[ObservableProperty]</c> the XAML and the registry context
    /// bind to. Assigning through here means the two cannot disagree, so every reader tests the id alone.
    /// The id is set FIRST — assigning the flag runs <c>OnIsProgrammingModeChanged</c>, which rebuilds the shell
    /// context, and that read must not observe the previous block.</summary>
    private void SetProgrammingBlock(ElementId? id)
    {
        _programmingBlockId = id;
        IsProgrammingMode = id is not null;
    }
    [ObservableProperty] private bool _isToolbarVisible = true;
    [ObservableProperty] private bool _isStatusBarVisible = true;
    [ObservableProperty] private AppTheme _currentTheme;
    /// <summary>The active workspace text-size step (US-001) — what the <i>Vis ▸ Tekststørrelse</i> radio items check.</summary>
    [ObservableProperty] private TextScale _currentTextScale;

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
    [NotifyPropertyChangedFor(nameof(CanAddCase))]
    [NotifyPropertyChangedFor(nameof(CanAddArithmetic))]
    [NotifyPropertyChangedFor(nameof(CanAddCondition))]
    private TreeNodeViewModel? _selectedNode;

    /// <summary>Whether the block currently being programmed is a locked (library) block. A locked block is
    /// VIEW-ONLY: its program renders, but every authoring command is withdrawn (A-27/F-076) — the installer must
    /// unlock it deliberately first. Unlock is a separate, irreversible action (F-046).</summary>
    public bool IsProgrammingBlockLocked =>
        _programmingBlockId is { } id
        && _session.Current is { } project && project.FindById(id) is { } block
        && project.View(block).Locked;

    // The programming-mode authoring context-menu gates: a container node's own kind, an editable (unlocked)
    // programming block, AND a non-empty menu. On a locked block every one is false, so the vendor's "missing, not
    // greyed" affordance holds. These remain because they gate the DATA-DRIVEN ItemsSource submenus, which are
    // non-rows by the documented ruling — every gate with a registry row (New case value…, among others) is the
    // row's, and only the row's, so a rule edit cannot land in a second dead home (review F09).
    //
    // Each gate names the ONE collection its submenu binds. Gating on the container kind alone offered headers that
    // opened onto nothing: the program menus are populated by the ARMED operand, so before a variable is armed all
    // five are empty. And Commands feeds three different submenus (Add command / Case / Arithmetic) whose menus fill
    // under different conditions, so one shared flag could not have spoken for all three.
    public bool CanInsertVariable =>
        SelectedNode?.IsBlockSection == true && !IsProgrammingBlockLocked && VariablePaletteMenu.Count > 0;
    public bool CanAddEvent =>
        SelectedNode?.IsEventsContainer == true && !IsProgrammingBlockLocked && ProgramEventMenu.Count > 0;
    public bool CanAddCommand =>
        SelectedNode?.IsCommandsContainer == true && !IsProgrammingBlockLocked && ProgramCommandMenu.Count > 0;
    public bool CanAddCase =>
        SelectedNode?.IsCommandsContainer == true && !IsProgrammingBlockLocked && ProgramCaseMenu.Count > 0;
    public bool CanAddArithmetic =>
        SelectedNode?.IsCommandsContainer == true && !IsProgrammingBlockLocked && ProgramArithmeticMenu.Count > 0;
    public bool CanAddCondition =>
        SelectedNode?.IsConditionsContainer == true && !IsProgrammingBlockLocked && ProgramConditionMenu.Count > 0;

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

    /// <summary>Gate for <i>Insert product</i>: offered only on a <b>locality</b> in the Installation pane — the
    /// Functions pane hosts function blocks, and the Localities root hosts localities (US-010/US-068). Governs the
    /// context menu's visibility AND every generated product leaf's availability on the bar (alignment F-8), so a
    /// greyed leaf and a refused invoke can never disagree.</summary>
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
    public BulkObservableCollection<ProductMenuItemViewModel> ProductsMenu { get; } = new();

    /// <summary>The library function-block insertion submenu (US-018), built from the catalog's FB folders.</summary>
    public BulkObservableCollection<ProductMenuItemViewModel> FunctionBlocksMenu { get; } = new();

    /// <summary>The variable types insertable into the currently selected block section (US-027); rebuilt when the
    /// selection changes so it only offers the types that section accepts.</summary>
    public ObservableCollection<ProductMenuItemViewModel> VariablePaletteMenu { get; } = new();

    /// <summary>The SECTION node's context flyout (F-13b): the accepted variable types FLAT and alphabetically,
    /// Enum as a submenu among them, and Egenskaber last — the vendor/US-027 shape. Bound by SectionContextMenu,
    /// which a section row selects in place of the shared node flyout.</summary>
    public ObservableCollection<ProductMenuItemViewModel> SectionFlyoutItems { get; } = new();

    // The variable palette (label, resource tag, section kind) is projected over the SDK variable-type registry by
    // VariablePalette (US-027, ADR-002/D07) — so the types the engine accepts and the types the UI offers cannot
    // drift, and a dropped type is a deliberate, tested suppression rather than a silent omission.

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value)
    {
        RebuildContext();   // T010: selection is a context trigger (first, so the early return below cannot skip it)
        _programAuthoring.Rebuild(value);
        VariablePaletteMenu.Clear();
        RebuildVariableBarMenu(value);   // F-12/F-13a: the bar list is always present; only enablement varies
        if (value is not { IsBlockSection: true, ElementId: { } sectionId, SectionTag: { } sectionTag })
        {
            SectionFlyoutItems.Clear();   // F-13b: only a section carries this flyout
            return;
        }
        // W1/D03: the section→types rule is the ENGINE's, asked once here. The view-model used to re-derive it from
        // the section tag ('I'/'O'/'V'), which offered an Input section only its own signal type — the vendor offers
        // the signal type PLUS all 19 value types (uxparity2 V3). PlacementRules already modelled that correctly, so
        // the fix is to delete the GUI's second copy, not widen it. The door also filters resource_scene out: a scene
        // is not a variable and reaches the project through US-024's own route.
        // F-16: a section's flyout follows the MODE. Reached in the Funktioner pane with no program open, the only
        // thing the vendor offers on it is the way IN — no type list, no Egenskaber. The palette below is what a
        // section answers once the program is open, and inserting a variable is a programming-mode act anyway, so
        // offering it here was also offering an authoring command outside the mode that owns it.
        if (!IsProgrammingMode)
        {
            SectionFlyoutItems.Clear();
            SectionFlyoutItems.Add(
                new ProductMenuItemViewModel("Vis program", "ctx.view.showProgram", EnterProgrammingModeCommand));
            return;
        }
        string sectionLabel = value.DisplayName;
        // A locked block remains navigable, but its section flyout must not mint executable authoring items. The
        // engine still guards direct calls; this projection matches the vendor's view-only flyout (Properties only).
        List<(string Label, string Tag)> accepted = IsProgrammingBlockLocked
            ? []
            : VariablePalette.LabelledTypes(_session.GetInsertableVariableTypes(sectionId)).ToList();
        foreach ((string label, string tag) in accepted)
            VariablePaletteMenu.Add(CreateVariableMenuItem(sectionId, label, tag, sectionLabel));

        // F-13b: the SECTION context flyout lists the accepted types FLAT and ALPHABETICALLY (da-DK), with Enum as
        // a submenu among them and Egenskaber last — the shape the vendor shows and US-027 mandates ("pick the type
        // from the popup"), NOT nested under an "Insert variable" parent. Fresh item instances (a menu item cannot
        // sit in two flyouts) built off the same engine-accepted set.
        // F-20: the section's own SIGNAL type leads the list; the value types follow it. Measured across all four
        // sections of an unlocked block: Input leads with Indgang, Output with Udgang, and the two value sections
        // lead with nothing — which is why this needs no section→type table. Whichever signal type the section
        // ACCEPTS leads, and a section that accepts none simply has no leader, so the engine's accepted set stays
        // the single source of the rule (W1/D03).
        //
        // The value types stay in DANISH collation, which is a REGISTERED deliberate difference from the vendor's
        // æ-as-"ae" ordering (product.md, F-26) — the leading entry is a divergence, the collation is not.
        SectionFlyoutItems.Clear();
        IEnumerable<(string Label, string Tag)> ordered = accepted
            .OrderBy(t => IsSignalVariable(t.Tag) ? 0 : 1)
            .ThenBy(t => t.Label, DisplayOrder.Danish);
        foreach ((string label, string tag) in ordered)
            SectionFlyoutItems.Add(CreateVariableMenuItem(sectionId, label, tag, sectionLabel));
        SectionFlyoutItems.Add(new ProductMenuItemViewModel("Egenskaber…", "ctx.node.properties", PropertiesCommand));
    }

    /// <summary>Whether a variable type is a block's SIGNAL (its wiring in or out) rather than a value it holds —
    /// the types the vendor sets off at the head of a section's flyout (F-20). Asked of the SDK registry rather
    /// than re-derived from tag spellings here: which family a tag belongs to is the engine's answer, and a role
    /// added or a tag renamed there must not leave this flyout silently mis-led.</summary>
    private static bool IsSignalVariable(string tag) =>
        !VariableTypeRegistry.ValueTypeTags.Contains(tag);

    // Builds one variable-insert menu item — a plain leaf, or (for resource_enum) the PG-4 type-picker submenu:
    // "Ny type…" (author a new type via the enumerator dialog), "Ny selvstændig type…" (PG-7/D02, a standalone
    // 0-state type — no variable; a registered difference, product.md/F-21), then the existing enumerator types
    // (pick one → reference its def-id). A fresh instance each call, so the same type can appear in both
    // VariablePaletteMenu and the section flyout without a menu item being parented twice.
    //
    // F-21: the CREATE route leads and the existing types are SORTED, both measured on the vendor 2026-08-11 by
    // creating a probe type after the two built-ins — it appeared first, so the list is sorted rather than
    // appended in creation order, which the two built-ins alone (already in order) could not have shown.
    private ProductMenuItemViewModel CreateVariableMenuItem(ElementId sectionId, string label, string tag, string sectionLabel)
    {
        if (tag != "resource_enum")
            return new ProductMenuItemViewModel(label, tag,
                new AsyncRelayCommand(() => InsertVariableAsync(sectionId, tag, label, sectionLabel)));
        var enumNode = new ProductMenuItemViewModel(label);
        enumNode.Children.Add(new ProductMenuItemViewModel("Ny type…", "enum-new",
            new AsyncRelayCommand(() => InsertEnumAsync(sectionId, sectionLabel))));
        enumNode.Children.Add(new ProductMenuItemViewModel("Ny selvstændig type…", "enum-standalone",
            new AsyncRelayCommand(AddStandaloneEnumTypeAsync)));
        foreach (string typeName in (_session.Current?.GetEnumeratorTypes() ?? System.Array.Empty<string>())
                     .OrderBy(t => t, DisplayOrder.Danish))
            enumNode.Children.Add(new ProductMenuItemViewModel(typeName, "enum-type",
                new AsyncRelayCommand(() => InsertEnumOfExistingTypeAsync(sectionId, typeName, sectionLabel))));
        return enumNode;
    }

    // One shared always-disabled command greys the F-12/F-13a bar items; a fresh instance per item would only
    // re-answer the same CanExecute=false per row.
    private static readonly RelayCommand s_variableInsertUnavailable = new(() => { }, () => false);

    /// <summary>The BAR's Indsæt ▸ Variable list (F-12/F-13a): the vendor's fixed vocabulary — every palette type
    /// except Enum (the vendor bar never carries an Enum item; the section flyout owns the enum type picker) —
    /// always present, with only enablement varying by the selected section (armed vendor dumps, 2026-08-09).
    /// Distinct from <see cref="VariablePaletteMenu"/>, which is the section flyout's accepted-only list.</summary>
    public ObservableCollection<ProductMenuItemViewModel> VariableBarMenu { get; } = new();

    private void RebuildVariableBarMenu(TreeNodeViewModel? value)
    {
        VariableBarMenu.Clear();
        HashSet<string>? accepted = value is { IsBlockSection: true, ElementId: { } sid }
            ? new HashSet<string>(_session.GetInsertableVariableTypes(sid), StringComparer.Ordinal)
            : null;
        ElementId? sectionId = value?.ElementId;
        string sectionLabel = value?.DisplayName ?? string.Empty;
        foreach ((string label, string tag) in VariablePalette.Entries)
        {
            if (tag == "resource_enum")
                continue;   // vendor bar carries no Enum — the flyout's type picker is the enum route (F-13a)
            string itemTag = tag;
            string itemLabel = label;
            // F-25: the vendor's Variable menu shows Ctrl+I on Indgang and Ctrl+U on Udgang (the pin-insert
            // shortcuts, wired here as window KeyBindings program.insertInput/insertOutput). Advertise them.
            string? gesture = tag switch { "resource_input" => "Ctrl+I", "resource_output" => "Ctrl+U", _ => null };
            VariableBarMenu.Add(new ProductMenuItemViewModel(label, tag,
                accepted is not null && accepted.Contains(tag) && sectionId is { } sec
                    ? new AsyncRelayCommand(() => InsertVariableAsync(sec, itemTag, itemLabel, sectionLabel))
                    : s_variableInsertUnavailable) { InputGesture = gesture });
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
                    await ApplyAsync(command, $"{label} blev indsat under {sectionLabel}");
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
    public string UndoMenuHeader => _session.CanUndo ? $"_Fortryd {_session.UndoLabel}" : "_Fortryd";

    /// <summary>The Edit ▸ Redo menu header, naming the action it would re-apply (E14), or just "Redo".</summary>
    public string RedoMenuHeader => _session.CanRedo ? $"_Gentag {_session.RedoLabel}" : "_Gentag";

    /// <summary>Edit ▸ Undo (US-052, Ctrl+Z): reverses the last project-mutating edit; a no-op when there is nothing
    /// to undo. Refreshes both panes via the session's StateChanged.</summary>
    private Task Undo() => RunAsync(nameof(Undo), async () =>
    {
        string? label = _session.UndoLabel;   // capture before the stack pops — names the action (E14)
        StatusText = await _session.UndoAsync()
            ? label is null ? "Fortrød den seneste ændring." : $"Fortrød: {label}"
            : "Intet at fortryde.";
    });

    /// <summary>Edit ▸ Redo (US-052, Ctrl+Y): re-applies the last undone edit; a no-op when the redo history is empty.</summary>
    private Task Redo() => RunAsync(nameof(Redo), async () =>
    {
        string? label = _session.RedoLabel;
        StatusText = await _session.RedoAsync()
            ? label is null ? "Gentog ændringen." : $"Gentog: {label}"
            : "Intet at gentage.";
    });

    // The single outcome→status/dialog rule (W2-14): Committed → success status; NoChange → silent (a no-op edit
    // leaves the status alone); Refused → the refusal reason as status; Failed → an error dialog. Applying a command
    // through the session and mapping its outcome here is how the VM drives every edit, replacing the per-op wrappers.
    /// <summary>The one sentence shown when an edit FAILS (as opposed to being refused): the engine's own message
    /// is an English developer diagnostic, so it goes to the log and the installer gets this.</summary>
    internal const string EditFailedMessage =
        "Redigeringen kunne ikke gennemføres på grund af en intern fejl. Ændringen blev ikke gemt.";

    /// <summary>The title and sentence <see cref="RunAsync"/> answers an unhandled command exception with. Fixed
    /// Danish text rather than the exception's own message, for the reason <see cref="UserFacingRefusal"/> gives:
    /// an engine diagnostic belongs in the log, not on the installer's screen.</summary>
    internal const string UnexpectedErrorTitle = "Uventet fejl";

    internal const string UnexpectedErrorMessage =
        "Handlingen kunne ikke gennemføres på grund af en intern fejl. Detaljerne er skrevet til loggen.";

    /// <summary>The generic that stands in wherever a non-committed outcome has no sentence this app may show —
    /// deliberately not <see cref="EditFailedMessage"/>, which <see cref="ReportOutcomeAsync"/> has already put in
    /// front of the installer by the time a still-open dialog needs this.</summary>
    internal const string EditRejectedMessage = "Handlingen blev afvist.";

    /// <summary>
    /// The sentence an outcome may put in front of the installer, or null when it has none. The ONE place that
    /// decides it, because the decision is easy to get wrong: <c>Refused</c> and <c>Failed</c> BOTH carry a
    /// non-null <c>Reason</c>, so a caller reaching for "the reason, if there is one" silently gets the failure
    /// diagnostic too. Only a refusal qualifies — it is a rule the installer can act on and the SDK writes it in
    /// Danish (FR-2.6 / D13), whereas a failure's reason is the engine's own exception message, an English
    /// developer diagnostic naming element tags, attribute names and <c>_0x</c> ids.
    /// </summary>
    internal static string? UserFacingRefusal(EditOutcome outcome) =>
        outcome.Status == EditStatus.Refused ? outcome.Reason : null;

    private async Task<EditOutcome> ReportOutcomeAsync(EditOutcome outcome, string? successStatus)
    {
        switch (outcome.Status)
        {
            case EditStatus.Committed when successStatus is not null: StatusText = successStatus; break;
            // Refused: the SDK's reason IS the user-facing sentence (Danish since T015) — shown verbatim.
            case EditStatus.Refused when UserFacingRefusal(outcome) is { } refusal: StatusText = refusal; break;
            // Failed: the reason is an ENGINE EXCEPTION message — a developer diagnostic, in English, naming
            // element tags and attribute names. Showing it put untranslated internals in front of the installer,
            // so it is logged and one fixed Danish sentence is shown instead. A refusal is a rule the installer
            // can act on; a failure is a defect they can only report, and the log is where the detail belongs.
            case EditStatus.Failed:
                _logger.LogError("Edit failed: {Label} — {Reason}", outcome.Label, outcome.Reason);
                await _dialogs.ShowMessageAsync("Redigering mislykkedes", EditFailedMessage);
                break;
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
            : "Der findes ingen specifik hjælp til dette element.";
        await _dialogs.ShowMessageAsync($"Hjælp — {name}", help);
    });

    /// <summary>Inserts an input variable into the programming block's Input section (US-045, Ctrl+I).</summary>
    private Task InsertInput() => InsertBlockPinAsync("inputs", "resource_input", "Input");

    /// <summary>Inserts an output variable into the programming block's Output section (US-045, Ctrl+U).</summary>
    private Task InsertOutput() => InsertBlockPinAsync("outputs", "resource_output", "Output");

    private Task InsertBlockPinAsync(string container, string tag, string label) => RunAsync(nameof(InsertBlockPinAsync), async () =>
    {
        if (IsProgrammingBlockLocked || _programmingBlockId is not { } blockId
            || _session.Current?.FindById(blockId)?.FindChild(container) is not { Id: { } sectionId })
        {
            StatusText = IsProgrammingBlockLocked
                ? "Dette er en låst biblioteksblok — lås den op for at redigere dens program."
                : "Gå ind i en bloks programmeringstilstand for at indsætte en indgang eller udgang.";
            return;
        }
        if (_session.Current is { } project && _session.Commands.AddVariable(project, sectionId, tag, label) is { } command)
            await ApplyAsync(command, $"{label} indsat i blokken.");
    });

    /// <summary>Opens the Project information dialog (US-039) prefilled from the project, and applies edits.</summary>
    private Task ProjectInfo() => RunAsync(nameof(ProjectInfo), async () =>
    {
        ProjectInfoData? result = await _dialogs.EditProjectInfoAsync(
            _session.GetProjectInfo(), ProjectInfoSuggestions.From(_session.DataTables));
        if (result is null || _session.Current is not { } project)
            return;
        if (await ApplyAsync(_session.Commands.UpdateProjectInfo(project, result), "Projekt oplysninger opdateret."))
        {
            // What was typed here joins the data tables, so the next project's dialog offers it — this is how the
            // vendor's tables fill up (every one of its Kunder rows was typed into this dialog, not into the
            // data-tables editor).
            _session.DataTables.Commit(ProjectInfoSuggestions.Absorb(_session.DataTables, result));
            // The installer's OWN contact details are an application setting, not per-project data (US-002) — this
            // dialog is where they are entered, so this is where they are remembered, and every later File → New
            // stamps them into the new project. Until now the store had no writer anywhere in the app, so a new
            // project always carried a blank installer_info unless the JSON was hand-edited.
            _session.InstallerIdentity.Update(ToInstallerIdentity(result.Installer));
        }
    });

    // The project's installer contact → the persisted application setting. Blank fields stay blank here;
    // InstallerIdentityStore collapses them back to "not written" when it builds a new project's details.
    private static InstallerIdentity ToInstallerIdentity(ContactInfo contact) => new()
    {
        Name = contact.Name,
        Address = contact.Address,
        City = contact.City,
        ZipCode = contact.Zip,
        Country = contact.Country,
        Phone = contact.Phone,
        MobilePhone = contact.Mobile,
        Email = contact.Email,
    };

    /// <summary>Documentation ▸ Data line modules (US-050): opens the read-only input/output data-line module map.</summary>
    private Task ModuleMap() => RunAsync(nameof(ModuleMap), async () =>
    {
        await _dialogs.ShowModuleMapAsync(_session.GetDatalineModuleMap());
    });

    /// <summary>The message shown when a controller-only operation is invoked in this controller-free build (E10).</summary>
    private const string ControllerRequiredMessage =
        "kræver en tilsluttet controller. Denne version kontakter ikke en controller (ingen controller-sideeffekter).";

    /// <summary>The dialog title and the status line that go with <see cref="ControllerRequiredMessage"/>. Send and
    /// Retrieve differ only in the verb naming the operation, so the surrounding wording is declared once — the two
    /// halves of one E10 answer cannot drift apart.</summary>
    private const string ControllerRequiredTitle = "Controller påkrævet";
    private const string ControllerRequiredStatus = "Controller-overførsel kræver en tilsluttet controller.";

    /// <summary>Controller ▸ Send project (US-042, F5): runs the offline pre-flight — warns about unlinked wireless
    /// products (they can be linked later) — then reports that the actual transfer needs a connected controller (the
    /// controller send/retrieve itself is deferred per E10; this build never contacts a controller).</summary>
    private Task SendProject() => RunAsync(nameof(SendProject), async () =>
    {
        IReadOnlyList<string> unlinked = _session.GetUnlinkedWirelessProducts();
        if (unlinked.Count > 0 &&
            !await _dialogs.ConfirmAsync("Ikke-linkede trådløse produkter",
                $"{unlinked.Count} trådløst produkt/produkter er ikke linket til controlleren ({string.Join(", ", unlinked)}). "
                + "De kan linkes senere. Send alligevel?"))
        {
            StatusText = "Afsendelse annulleret.";
            return;
        }
        await _dialogs.ShowMessageAsync(ControllerRequiredTitle, "Afsendelse af projektet " + ControllerRequiredMessage);
        StatusText = ControllerRequiredStatus;
    });

    /// <summary>Controller ▸ Retrieve project (US-043): reports that retrieving needs a connected controller — the
    /// transfer is deferred per E10 and this build never contacts a controller.</summary>
    private Task RetrieveProject() => RunAsync(nameof(RetrieveProject), async () =>
    {
        await _dialogs.ShowMessageAsync(ControllerRequiredTitle, "Hentning af et projekt " + ControllerRequiredMessage);
        StatusText = ControllerRequiredStatus;
    });

    /// <summary>Documentation ▸ the three report entries (T015, R12/D4/D01): each opens the ONE shared
    /// picker dialog with its report pre-selected in the type dropdown; [Vis] generates via the facade
    /// (SVG icons for HTML) to a temp file and opens it in the OS default application (US-063).</summary>
    private Task OpenReportPicker(ReportKind preselected) => RunAsync(nameof(OpenReportPicker), async () =>
    {
        var viewModel = new ReportPickerViewModel(preselected,
            _session.ViewReportInBrowserAsync, _session.SaveReportAsAsync);
        await _dialogs.ShowReportPickerAsync(viewModel);
    });

    // T018: AddPowerEvent / ToggleSaveValue / AddSubProgram / AddLogicGroup / SetConditionsOr / SetConditionsAnd /
    // NewCaseValue (US-029/031/033) moved into ProgramAuthoringCoordinator; the VM keeps thin [RelayCommand] entry
    // points delegating their bodies there (the XAML bindings and the *Command tests are unchanged).

    /// <summary>Adds a Powerup system event to the selected Events group (US-033).</summary>
    private Task AddPowerEvent(TreeNodeViewModel? node) => _programAuthoring.AddPowerEventAsync(node);

    /// <summary>Toggles an output's <i>Save current value</i> power-loss persistence (US-033).</summary>
    private Task ToggleSaveValue(TreeNodeViewModel? node) => _programAuthoring.ToggleSaveValueAsync(node);

    /// <summary>Adds a new program to a block's Programs group (US-026, W4) — a block may hold several.</summary>
    private Task AddProgram(TreeNodeViewModel? node) => _programAuthoring.AddProgramAsync(node);

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
        CurrentTextScale = theme.TextScale;
        _properties = new PropertiesDialogCoordinator(
            _session, _dialogs, (command, status) => ApplyAsync(command, status), status => StatusText = status);
        _programAuthoring = new ProgramAuthoringCoordinator(
            _session, _dialogs, RunAsync, (command, status) => ApplyAsync(command, status), SelectNode,
            status => StatusText = status, () => SelectedNode, () => _programmingBlockId, NotifyProgramMenuGates);
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
        RebuildVariableBarMenu(null);   // F-12/F-13a: nothing is selected at startup, and the bar list is never empty
    }

    /// <summary>Detaches the session/recent-store event handlers so this view-model does not leak through those
    /// longer-lived sources (review Low). Called on app shutdown; the session itself is disposed separately.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Detaches this view-model's handlers. The design-time subclass may extend it; an override must
    /// call the base implementation or the handlers stay attached.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>, false from a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _session.StateChanged -= _onSessionStateChanged;
        _session.CatalogChanged -= _onSessionCatalogChanged;
        _recent.Changed -= _onRecentChanged;
    }

    // Rebuilds the product/function-block insertion menus from the current catalog (US-059/US-060: after an import
    // the newly available components appear here). Same call as the initial build — nothing to clear first, since
    // ReplaceAll below does the clearing as part of its single notification.
    private void RebuildCatalogMenus() => BuildProductMenu();

    private void BuildProductMenu()
    {
        // Each leaf carries the SAME predicate its body checks, so the bar greys a product exactly when
        // inserting it would be refused (alignment F-8: the reference application greys its catalog leaves
        // and keeps the category containers above them live — which is what binding the gate to the LEAF,
        // rather than to the hosting submenu, reproduces).
        AsyncRelayCommand Insert(CatalogItem product) =>
            new(() => InsertProductAsync(product.Identifier, product.DisplayName), () => CanInsertProduct);

        // The top categories are derived from the catalog data (H2/D08) — so an imported .def (empty CategoryPath)
        // lands in the "Imported/Uncategorized" bucket instead of being dropped by a hardcoded four-category filter.
        ProductsMenu.ReplaceAll(CatalogMenu.BuildProductForest(_session.GetProductCatalogItems(), Insert));
        FunctionBlocksMenu.ReplaceAll(CatalogMenu.BuildFunctionBlocks(
            _session.GetFunctionBlockCatalogItems(),
            fb => new AsyncRelayCommand(() => InsertFunctionBlockAsync(fb.Identifier, fb.DisplayName),
                                        () => CanInsertFunctionBlock)));
    }

    // The gate value each catalog forest was last notified for. Both start false, which is what the gates read on
    // a fresh view-model (nothing selected ⇒ no locality ⇒ neither insert is offered), so the first rebuild raises
    // nothing — and needs to raise nothing: a newly built forest's commands are queried live by the menu items
    // that realize over them, so they have no stale verdict to correct. The same holds after a catalog import.
    private bool _notifiedCanInsertProduct;
    private bool _notifiedCanInsertFunctionBlock;

    /// <summary>
    /// Re-asks the generated catalog leaves whether they can run. Necessary because those commands are built once
    /// per catalog and then OUTLIVE every selection change: a CanExecute predicate that is never re-queried
    /// leaves the whole menu frozen at whatever it evaluated to when the menu was built — greyed forever, or
    /// live forever — which looks exactly like no gate at all. Called from the one place the availability
    /// inputs change, so no caller has to remember it.
    /// <para>Every leaf of a forest checks the SAME shared gate, so a forest whose gate has not moved has nothing
    /// to say — and re-raising it anyway walked ~173 leaves and re-queried every realized menu item on each of the
    /// many things that rebuild the context (selection, pane, mode, clipboard, undo, connect). Driving the sweep
    /// off the gate VALUE keeps the invariant that a greyed leaf and a refused invoke can never disagree.</para>
    /// </summary>
    private void RefreshCatalogLeafAvailability()
    {
        if (CanInsertProduct != _notifiedCanInsertProduct)
        {
            _notifiedCanInsertProduct = CanInsertProduct;
            NotifyLeaves(ProductsMenu);
        }
        if (CanInsertFunctionBlock != _notifiedCanInsertFunctionBlock)
        {
            _notifiedCanInsertFunctionBlock = CanInsertFunctionBlock;
            NotifyLeaves(FunctionBlocksMenu);
        }

        static void NotifyLeaves(IEnumerable<ProductMenuItemViewModel> forest)
        {
            foreach (ProductMenuItemViewModel item in forest)
            {
                (item.Command as IRelayCommand)?.NotifyCanExecuteChanged();
                NotifyLeaves(item.Children);
            }
        }
    }

    /// <summary>Library ▸ Import catalog file (US-059): imports a single <c>.def</c>/<c>.ifb</c> so its component
    /// becomes insertable; persisted by default (US-061) so it survives a restart.</summary>
    private Task ImportCatalogFile() => RunAsync(nameof(ImportCatalogFile), async () =>
    {
        if (await _dialogs.PickCatalogFileAsync() is not { } path)
            return;
        if (await _session.ImportCatalogFileAsync(path, persist: true))
            StatusText = "Importerede 1 komponent (gemt i katalogmappen).";
    });

    /// <summary>Library ▸ Import catalog folder (US-060): imports every <c>.def</c>/<c>.ifb</c> in a folder and its
    /// subfolders, reporting how many components were imported; persisted by default (US-061).</summary>
    private Task ImportCatalogFolder() => RunAsync(nameof(ImportCatalogFolder), async () =>
    {
        if (await _dialogs.PickCatalogFolderAsync() is not { } dir)
            return;
        CatalogImportOutcome outcome = await _session.ImportCatalogFolderAsync(dir, persist: true);
        if (outcome.FolderMissing)
            return;   // already reported by the workflow, and nothing was imported to announce
        string components = $"{outcome.Imported} komponent{(outcome.Imported == 1 ? string.Empty : "er")}";
        // A run that stopped at an unreadable file must not read like a finished one: the same count means two
        // different things, and only one of them is "the folder is imported" (UX review CORE-03).
        StatusText = outcome.Completed
            ? $"Importerede {components} (gemt i katalogmappen)."
            : $"Import stoppet: {components} nåede at blive importeret (gemt i katalogmappen); resten blev ikke læst.";
    });

    /// <summary>Inserts an empty function block under the selected locality (US-019). Invoked from the right-click
    /// <i>Empty function block</i> item and Ctrl+Shift+B.</summary>
    private Task InsertEmptyFunctionBlock() => RunAsync(nameof(InsertEmptyFunctionBlock), async () =>
    {
        if (SelectedNode?.ElementId is not { } localityId || _session.Current is not { } project)
        {
            StatusText = "Vælg først en lokalitet, indsæt derefter den tomme funktionsblok.";
            return;
        }
        string localityName = SelectedNode.DisplayName;
        if (await ApplyAsync(_session.Commands.AddEmptyFunctionBlock(project, localityId, ProjectWorkflow.EmptyBlockName),
                $"{ProjectWorkflow.EmptyBlockName} blev indsat under {localityName}") is not { } blockId)
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
                StatusText = "Vælg først en lokalitet, indsæt derefter funktionsblokken.";
                return;
            }
            string localityName = SelectedNode.DisplayName;
            if (_session.Commands.AddFunctionBlock(project, localityId, masterType) is not { } command)
            {
                await _dialogs.ShowMessageAsync("Indsætning mislykkedes", $"Ingen biblioteks-funktionsblok med master type '{masterType}'.");
                return;
            }
            // The placed block opens, showing the sections and pins it brought — the reference application reveals
            // it all the way down (measured 2026-08-11: the block AND both sections expanded, every other locality
            // left collapsed), and the installer's next action is almost always to wire one of those pins. The same
            // reveal the product path already performed; it was simply never wired here (alignment F-19).
            if (await ApplyAsync(command, $"Funktionsblokken '{blockName}' er indsat under {localityName}") is { } newId)
                RevealSubtree(newId);
        });

    // No parameterless "design-time" constructor: the one that used to be here created two never-deleted temp files
    // plus a whole ProjectAppService on every instantiation, and heavy work in a view-model constructor is exactly
    // what the previewer cannot afford (Avalonia architecture review AP-18/A-13). Design-time construction lives in
    // the side-effect-free DesignTime/DesignMainWindowViewModel subclass that MainWindow.axaml points
    // Design.DataContext at, so the real constructor cannot drift from the real composition root. Pinned by
    // OpenVisualDesignTimeTests.

    /// <summary>Opens the start-up document: <paramref name="startupProjectPath"/> (the <c>.vis</c> the app was
    /// launched on), else the empty project.</summary>
    public Task InitializeAsync(string? startupProjectPath = null) => _session.StartAsync(startupProjectPath);

    /// <summary>Runs the window-close save prompt (US-064); returns false to cancel the quit.
    /// <para>Routed through <see cref="RunAsync"/> — the view-model's one error boundary — because the caller is
    /// <c>Window.Closing</c>, which runs off the window message loop where NO global exception handler can see a
    /// fault (Avalonia logging review AP-06/WS-11). A failure therefore leaves the answer <c>false</c>: the quit is
    /// cancelled, since a save prompt that never completed cannot be read as "the installer chose to discard".</para></summary>
    public async Task<bool> CanCloseAsync()
    {
        bool canClose = false;
        await RunAsync(nameof(CanCloseAsync), async () => canClose = await _session.CanQuitAsync());
        return canClose;
    }

    private Task NewAsync() => RunAsync(nameof(NewAsync), async () =>
    {
        if (await _session.NewAsync())
            StatusText = "Startede et nyt projekt.";
    });

    private Task OpenAsync() => RunAsync(nameof(OpenAsync), async () =>
    {
        if (await _session.OpenWithPickerAsync())
            StatusText = $"Åbnede {_session.DocumentName}.";
    });

    [RelayCommand]
    private Task OpenRecentAsync(string path) => RunAsync(nameof(OpenRecentAsync), async () =>
    {
        if (await _session.OpenAsync(path))
            StatusText = $"Åbnede {_session.DocumentName}.";
    });

    private Task SaveAsync() => RunAsync(nameof(SaveAsync), async () =>
    {
        if (await _session.SaveAsync())
            StatusText = $"Gemte {_session.DocumentName}.";
    });

    private Task SaveAsAsync() => RunAsync(nameof(SaveAsAsync), async () =>
    {
        if (await _session.SaveAsAsync())
            StatusText = $"Gemte {_session.DocumentName}.";
    });

    private Task CloseAsync() => RunAsync(nameof(CloseAsync), async () =>
    {
        if (await _session.CloseAsync())
            StatusText = "Lukkede projektet.";
    });

    private void Exit() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void ToggleToolbar()
    {
        IsToolbarVisible = !IsToolbarVisible;
        StatusText = IsToolbarVisible ? "Værktøjslinie vist." : "Værktøjslinie skjult.";
    }

    private void ToggleStatusBar() => IsStatusBarVisible = !IsStatusBarVisible;

    [RelayCommand]
    private void SetTheme(AppTheme theme)
    {
        _themeService.Apply(theme);
        CurrentTheme = theme;
        StatusText = $"Tema: {theme}.";
    }

    /// <summary>Vis ▸ Tekststørrelse (US-001): scales all workspace text at once. A parameterized item command
    /// like <see cref="SetThemeCommand"/> — presentation-only, with no SDK edit verdict and no per-surface
    /// availability policy — so it is one of the narrow, registry-exempt commands (see CommandRegistry).</summary>
    [RelayCommand]
    private void SetTextScale(TextScale scale)
    {
        _themeService.ApplyTextScale(scale);
        CurrentTextScale = scale;
        StatusText = $"Tekststørrelse: {scale}.";
    }

    /// <summary>Inserts a new locality under <i>Localities</i> (US-008), then selects it in the Installation pane.
    /// Invoked from the right-click <i>Insert locality</i> item on the Localities root.</summary>
    private Task InsertLocality() => RunAsync(nameof(InsertLocality), async () =>
    {
        if (_session.Current is not { } project)
            return;
        // Name the container the way the tree does — from the project, not a hard-coded caption. The two must
        // agree: a message reading "under Localities" beside a root row reading "Lokaliteter" names nothing the
        // installer can see, so the fallback is the projector's own (ProjectTreeProjector.LocalitiesRootName)
        // rather than a second copy of the word.
        string container = project.Child("groups") is { } groups
            ? project.NameOr(groups, ProjectTreeProjector.LocalitiesRootName)
            : ProjectTreeProjector.LocalitiesRootName;
        if (await ApplyAsync(_session.Commands.AddLocality(project, ProjectWorkflow.NewLocalityName),
                $"{ProjectWorkflow.NewLocalityName} blev indsat under {container}") is not { } id)
            return;
        // Refresh already rebuilt the trees (StateChanged); highlight the new locality in the Installation pane
        // (which sets it as the active node).
        SelectedInstallationNode = FindNode(InstallationNodes, id);
    });

    /// <summary>
    /// Bibliotek ▸ Gem Funktionsblok… (US-021, Ctrl+G): saves the selected block, or the active programming
    /// block when one of its child rows is selected, into the library as a reusable <c>.ifb</c>. Also on the
    /// block node's right-click flyout — the vendor offers it in both places.
    /// <para>
    /// ONE dialog, asking a Navn and a Note: that is the reference application's own <i>Gem Funktionsblok...</i>
    /// form (measured 2026-08-04), and it asks for no path because it writes into its component folder. OpenVisual
    /// used to raise a second, OS file picker after the same form — so a saved block landed wherever the installer
    /// browsed to and never appeared under <i>Indsæt ▸ FunktionsBlokke</i>, which is the one thing saving it to the
    /// library is for.
    /// </para>
    /// </summary>
    private Task SaveFunctionBlock(TreeNodeViewModel? node) => RunAsync(nameof(SaveFunctionBlock), async () =>
    {
        ElementId? id = node?.Kind == TreeNodeKind.FunctionBlock ? node.ElementId : _programmingBlockId;
        if (id is not { } blockId || _session.Current?.FindById(blockId) is not { } fb || fb.Kind != ElementKind.FunctionBlock)
            return;
        string currentName = _session.Current!.View(fb).Name ?? "block";
        string currentNote = _session.Current!.View(fb).Note ?? string.Empty;
        PropertiesResult? meta = await _dialogs.EditPropertiesAsync("Gem Funktionsblok...", currentName, currentNote,
            affirmative: "Gem");
        if (meta is null)
            return;   // cancelled
        if (await _session.SaveFunctionBlockToLibraryAsync(blockId, meta.Name, meta.Note) is not null)
            StatusText = $"Gemte funktionsblokken '{meta.Name}' i biblioteket.";
    });

    /// <summary>Unlocks a locked library function block (US-020) so its internals become editable; the tree rebuild
    /// then shows the editable icon. Invoked from the right-click <i>Unlock</i> item.</summary>
    private Task Unlock(TreeNodeViewModel? node) => RunAsync(nameof(Unlock), async () =>
    {
        if (node?.ElementId is not { } id || _session.Current is not { } project)
            return;
        string name = node.DisplayName;
        // Unlocking takes ownership of the block (uxparity S-20), so it is stamped with whoever did it.
        await ApplyAsync(_session.Commands.UnlockFunctionBlock(project, id, Environment.UserName), $"Låste {name} op.");
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
            // The SDK's own sentence, forwarded — not a generic copy authored here. It names WHICH rule refused
            // (a catalog-declared pin, a locked library block, project structure), which the GUI cannot know.
            await _dialogs.ShowMessageAsync("Kan ikke slette", impact.Reason!);
            return;
        }
        if (impact.Kind == DeleteKind.Link)
        {
            // Removing a link deletes its reciprocal pair, not a subtree (US-057).
            await ApplyAsync(_session.Commands.RemoveLink(project, id), "Link fjernet.");
            return;
        }
        string name = node.DisplayName;
        // Prompt for what a node CONTAINS, never for what merely points at it — the vendor's rule, measured:
        // deleting a locality holding function blocks asks (S-09), while deleting a product that other logic links
        // to just goes (S-15), and the resulting file is byte-identical either way. Note NeedsConfirm is still the
        // reference-CASCADE flag below; only the question is dropped.
        if (impact.NeedsConfirm && impact.Kind == DeleteKind.Locality)
        {
            if (!await _dialogs.ConfirmAsync("Slet lokalitet",
                    $"'{name}' indeholder produkter. Sletter du den, fjernes også de produkter og de "
                    + "kommandoer og betingelser, der bruger dem. Slet alligevel?"))
            {
                return;   // declined — nothing is deleted
            }
        }
        if (impact.Kind == DeleteKind.Locality)
            await ApplyAsync(_session.Commands.DeleteLocality(project, id), $"Slettede {name}.");   // the US-009 locality worked example
        else
            // impact.NeedsConfirm is the reference-cascade flag PreviewDelete computed for this node.
            await ApplyAsync(_session.Commands.DeleteNode(project, id, impact.NeedsConfirm), $"Slettede {name}.");   // US-053
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
        StatusText = $"Klippede {node.DisplayName} — indsæt på en lokalitet for at flytte den.";
    }

    /// <summary>Copy the selected node (US-056, Ctrl+C): stashes it so a Paste onto a locality duplicates it.</summary>
    private void Copy(TreeNodeViewModel? node)
    {
        if (node?.ElementId is not { } id)
            return;
        SetClipboard(id, isCut: false);
        StatusText = $"Kopierede {node.DisplayName} — indsæt på en lokalitet for at duplikere den.";
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
            if (await ApplyAsync(_session.Commands.MoveNode(project, sourceId, targetId), TreeDragDropController.MovedStatus))
            {
                SetClipboard(null, isCut: false);   // a cut is consumed by its paste
            }
        }
        else if (await ApplyAsync(_session.Commands.CopyNode(project, sourceId, targetId), "Indsatte en kopi.") is { } pastedId)
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
            await ApplyAsync(command, delta < 0 ? "Flyttet op." : "Flyttet ned.");
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
    /// <see cref="ProductsMenu"/> call this. Routed through <see cref="RunAsync"/> for tracing and error surfacing.</summary>
    private Task InsertProductAsync(string productIdentifier, string productName) =>
        RunAsync(nameof(InsertProductAsync), async () =>
        {
            if (SelectedNode?.ElementId is not { } localityId || _session.Current is not { } project)
            {
                StatusText = "Vælg først en lokalitet, indsæt derefter produktet.";
                return;
            }
            string localityName = SelectedNode.DisplayName;
            // Resolved by identifier AND the leaf's own name: eight catalog identifiers are shared by two or
            // three products (D22), so the identifier alone cannot say whether the installer picked LK FUGA or
            // LK OPUS. It used to take whichever came first, which placed the wrong product under the wrong
            // name (T046).
            if (_session.ResolveCatalogProduct(productIdentifier, productName) is not { } definition)
            {
                await _dialogs.ShowMessageAsync("Indsætning mislykkedes", $"Intet katalogprodukt med identifikator '{productIdentifier}'.");
                return;
            }
            AddProduct command = _session.Commands.AddProduct(project, localityId, definition);
            if (_session.Commands.WouldExceedModemLimit(project, productIdentifier))   // at most one modem per project (US-013)
            {
                await _dialogs.ShowMessageAsync("Kun ét modem",
                    "Et projekt må højst indeholde ét modem. Fjern det eksisterende modem, før du tilføjer et nyt.");
                return;
            }
            // Placing a product ASKS for its documentation as part of placing it, and cancelling places nothing —
            // measured against IHC Visual (uxparity S-12), where the Insert menu raises the product dialog and
            // Annuller leaves both the tree and the id counter untouched. (An earlier note here claimed the vendor
            // does not auto-open on insert; that came from a driver verb which posts the catalog command directly
            // and skips the dialog — see tmp/uxparity/MCPFIXES.md.)
            // Applied WITHOUT announcing it. The insert is committed here so the dialog can be built from the
            // placed element, but the installer can still press Annuller — and until they do not, the project has
            // not gained a product. Announcing at this point put "Produktet 'X' indsat under Y", in the completed
            // past tense, on the status line of an application that was still asking whether to do it (measured
            // live 2026-08-11, alignment F-14). A refusal is still reported: ApplyAsync only withholds the SUCCESS
            // line. The announcement moves below, after the dialog commits.
            if (await ApplyAsync(command) is not { } newId)
                return;
            if (!await _properties.OpenForInsertAsync(newId))
            {
                // Cancelled: roll the insert back — NOT Undo. Rollback restores the snapshot verbatim (the vendor
                // burns no ids on Annuller — S-12) and leaves no redo entry, where Undo deliberately keeps the
                // raised id counter (alignment F-10) and would leave the cancelled insert redoable.
                await _session.RollbackAsync();
                StatusText = $"Indsætning af '{productName}' annulleret.";
                return;
            }
            // Committed: now it is true, so now it is said.
            StatusText = $"Produktet '{productName}' indsat under {localityName}";
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
            await ApplyAsync(_session.Commands.ToggleLogMark(project, id), $"Skiftede logmærket på {node.DisplayName}.");
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
                SetProgrammingBlock(id);
                Refresh();
                NotifyProgrammingAuthoringGates();
                // Lockedness comes from the MODEL (IsProgrammingBlockLocked), never from a tree node: Refresh() has
                // just re-projected FunctionNodes into the programming-mode tree, so a node lookup there found
                // nothing carrying the flag and this message silently never appeared (uxparity2 T007/V4).
                StatusText = IsProgrammingBlockLocked
                    ? "Programmeringstilstand (skrivebeskyttet — blokken er låst). Tryk Esc for at vende tilbage."
                    : "Programmeringstilstand — tryk Esc for at vende tilbage til konfiguration.";
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
            // Configuration-tree blocks are structural items; the active programming root is not. Function-block
            // variables allow Cut only in an unlocked programming view.
            Gate: ctx => ctx.Node is { CanCut: true, Id: not null } node && CutOrDeleteAllowed(ctx, node)
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en lokalitet, et produkt eller en funktionsblok, der skal klippes.")));

        Registry.Register(new CommandSpec("edit.copy", "Ctrl+C",
            Surfaces.MenuBar | Surfaces.ContextMenu | Surfaces.Toolbar,
            Execute: Sync(ctx => Copy(ResolveNode(ctx))),
            // The whole rule lives in the GATE, deliberately: a failed gate hides on the transient flyout and greys
            // on the persistent bar, so encoding a restriction in a SurfacePolicy instead would leave the bar
            // offering what the flyout omits — which is what the earlier product-terminal exception did (F-15/F-17).
            Gate: ctx => ctx.Node is { Id: not null } node && CopyOffered(ctx, node)
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en node, der skal kopieres.")));

        Registry.Register(new CommandSpec("edit.paste", "Ctrl+V",
            Surfaces.MenuBar | Surfaces.ContextMenu | Surfaces.Toolbar,
            Execute: ctx => Paste(ResolveNode(ctx)),
            Gate: ctx => ctx.Clipboard is null
                ? EditVerdict.Refuse("Klip eller kopier først en node.")
                : ctx.Node is { Kind: TreeNodeKind.Locality }
                    ? EditVerdict.Allow
                    : EditVerdict.Refuse("Indsæt på en lokalitet.")));

        Registry.Register(new CommandSpec("edit.delete", "Delete",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => Delete(ResolveNode(ctx)),
            // One classification, not two: DeleteNode.Evaluate allows exactly the set CanDelete accepts (both read
            // ClassifyDelete, G7) and otherwise carries the SDK's specific protected-element reason, so asking
            // CanDelete first only re-ran the same walk on the refusal path. cascade:false keeps deletable
            // containers offered — the gate is about deletability, and the confirm flow decides cascade.
            Gate: ctx => ctx.Node is { } node && CutOrDeleteAllowed(ctx, node)
                && node.Id is { } id && _session.Current is { } project
                ? _session.CanApply(_session.Commands.DeleteNode(project, id, cascade: false))
                : EditVerdict.Refuse("Vælg et element, der skal slettes.")));

        Registry.Register(new CommandSpec("view.showProgram", "F3",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: Sync(ctx => EnterProgrammingMode(ResolveNode(ctx))),
            // Offered on a block AND on its pins — the vendor jumps from a pin to the program using it (S-28).
            Gate: ctx => ctx.Node is { } node
                && (node.Kind == TreeNodeKind.FunctionBlock ? node.Id : OwningFunctionBlockByAncestry(node.Id)) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en funktionsblok for at vise dens program."),
            // The BAR is stricter than the flyout in ONE way only: it needs a block selected DIRECTLY, where the
            // flyout also accepts a pin and resolves the owning block (S-28).
            //
            // It is NOT stricter about locking. That reading (uxparity2 F13) was retired by D15: V1 measured both
            // surfaces on both fixtures and the reference application enables Show program on the bar for a locked
            // block too. The contradicting measurement reproduces on demand by reading the bar UNARMED, which is what
            // it turned out to be. A locked block's program opens read-only from either surface — entry was never the
            // thing being withheld; authoring is, and IsProgrammingBlockLocked withdraws that separately.
            SurfacePolicy: (ctx, surface) =>
                surface == Surface.ContextMenu
                    // Inside PROGRAMMING MODE no flyout offers Vis program — the program is already open. Measured
                    // 2026-08-11 on an unlocked block across every row type the mode exposes (alignment F-22): the
                    // block root, a section and a section pin in the installation pane, and all ten program-row
                    // types in the functions pane. It is the MODE that withdraws it, not the node kind — F-13c said
                    // as much ("reaching a section means you are already in the program") but keyed it to sections,
                    // which both under-applied it here and over-applied it in configuration mode, where the vendor
                    // DOES offer it on a section (F-16). The block and pin routes (S-28) are unaffected: they are
                    // configuration-mode routes.
                    ? (ctx.IsProgrammingMode ? Availability.Hidden : null)
                    : ctx.Node is { Kind: TreeNodeKind.FunctionBlock }
                        ? null
                        : Availability.Disabled("Vælg en funktionsblok i træet.")));
    }

    // crudarch T013: the remaining node-scoped tree commands as rows — gates are the former IsVisible/CanExecute
    // conditions verbatim; the one divergence (Properties: Edit-menu enabled on a link row the flyout omits) is
    // SurfacePolicy data. Bodies stay the existing private methods, resolved via ResolveNode.
    private static bool CutOrDeleteAllowed(ShellContext context, NodeContext node) => node.Kind switch
    {
        // The active programming root is not a structural item; a configuration-tree block is.
        TreeNodeKind.ProgramBlockRoot => false,
        TreeNodeKind.FunctionBlock => !context.IsProgrammingMode,
        // A function-block variable allows Cut only in an unlocked programming view. `CanCut` already excludes the
        // protected pin families for a pin (a catalog-declared pin and a product terminal both fail it), so this
        // needs no second exclusion of its own.
        _ => !(node.IsPin && node.CanCut) || (context.IsProgrammingMode && !context.ProgrammingBlockLocked),
    };

    /// <summary>Whether a PIN is a signal SOURCE — a row whose value the system reads rather than writes.
    /// A product's INPUT terminal (the button feeding the controller) and a function block's OUTPUT pin (the value
    /// the block produces) are sources; a product's output and a block's input are sinks. The two families
    /// therefore run OPPOSITE ways, which is why the rule cannot be stated as a direction alone.</summary>
    private static bool IsSourcePin(NodeContext node) =>
        node.IsPin && node.IsProductTerminal != node.IsOutputPin;

    /// <summary>Whether <i>Kopier</i> is offered on this node, on every surface.
    ///
    /// <para>Measured 2026-08-11 against the reference application on one project holding both pin families in the
    /// same state, each row read on both its flyout and <c>Rediger ▸ Kopier</c> (alignment F-17): a pin carries
    /// Kopier exactly when it is a <see cref="IsSourcePin">source</see>. This replaced "a product terminal is the
    /// copy-only exception, inputs only" (F-15), which was true of products and silent about block pins — where the
    /// direction runs the other way — and which lived in a SurfacePolicy, so it corrected the flyout while leaving
    /// the menu bar offering Copy on a product output.</para>
    ///
    /// <para>A block's own variables stay copyable from a programming view, including a locked block's read-only
    /// one; that is unmeasured against the vendor and so deliberately left as it was.</para></summary>
    private static bool CopyOffered(ShellContext context, NodeContext node) =>
        node.IsPin
            ? IsSourcePin(node) || context.IsProgrammingMode && node.CanCopy
            : node.CanCopy && (node.Kind != TreeNodeKind.FunctionBlock || !context.IsProgrammingMode);

    private void RegisterNodeRows()
    {
        Registry.Register(new CommandSpec("insert.locality", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: _ => InsertLocality(),
            // Alignment F-1 (tmp/align-campaign-2026-08-09.md): NO selection allows it — the vendor's bar item is
            // enabled on a fresh project and inserts at the root, and InsertLocality never reads the selection. A
            // non-root selection stays refused-with-reason: the vendor silently no-ops there (measured
            // Code=NoEffect 2026-08-09), and the explained grey is the registered enhancement over that.
            Gate: ctx => ctx.Node is null or { Kind: TreeNodeKind.LocalitiesRoot }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg Lokaliteter-roden for at indsætte en lokalitet.")));

        Registry.Register(new CommandSpec("insert.emptyFunctionBlock", "Ctrl+Shift+B",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: _ => InsertEmptyFunctionBlock(),
            Gate: ctx => !ctx.InstallationPaneActive && ctx.Node is { Kind: TreeNodeKind.Locality }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en lokalitet i Funktioner-ruden.")));

        // The bar and Ctrl+G follow the active unlocked programming block even when a program child is selected;
        // the context flyout remains block-row-specific (S2-16, measured against the vendor's Programmer row).
        Registry.Register(new CommandSpec("node.saveBlock", "Ctrl+G",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => SaveFunctionBlock(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { Kind: TreeNodeKind.FunctionBlock or TreeNodeKind.ProgramBlockRoot }
                         || ctx.IsProgrammingMode && !ctx.ProgrammingBlockLocked
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en ulåst funktionsblok, der skal gemmes."),
            SurfacePolicy: (ctx, surface) =>
                surface == Surface.ContextMenu
                    && ctx.Node is not { Kind: TreeNodeKind.FunctionBlock or TreeNodeKind.ProgramBlockRoot }
                    ? Availability.Hidden
                    : surface == Surface.MenuBar && ctx.IsProgrammingMode && ctx.ProgrammingBlockLocked
                        ? Availability.Disabled("En låst funktionsblok kan ikke gemmes i biblioteket.")
                        : null));

        // On the BAR as well as the flyout: the vendor carries it as Bibliotek's third item (id 24766, no shortcut).
        // Its gate is confirmed identical — measured 2026-08-04 across three blocks of the vendor's own project,
        // Oplås was enabled on the locked ones and greyed on the unlocked one.
        Registry.Register(new CommandSpec("node.unlock", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => Unlock(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsLockedBlock: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Kun en låst biblioteksblok kan låses op.")));

        Registry.Register(new CommandSpec("node.toggleLogMark", null,
            Surfaces.ContextMenu,
            Execute: ctx => ToggleLogMark(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsLogMarkPin: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en klemme der kan logmærkes.")));

        Registry.Register(new CommandSpec("help.onNode", "F1",
            Surfaces.MenuBar,
            Execute: ctx => Help(ResolveNode(ctx)),
            Gate: _ => EditVerdict.Allow));   // F1 always answers — with or without a selection

        Registry.Register(new CommandSpec("node.useInProgram", null,
            Surfaces.ContextMenu,
            Execute: Sync(ctx => UseInProgram(ResolveNode(ctx))),
            Gate: ctx => ctx.Node is { IsPin: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en variabel eller klemme.")));

        Registry.Register(new CommandSpec("link.startFromHere", null,
            Surfaces.ContextMenu,
            Execute: Sync(ctx => StartLink(ResolveNode(ctx))),
            Gate: ctx => ctx.Node is { IsPin: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en klemme at linke fra.")));

        Registry.Register(new CommandSpec("link.toHere", null,
            Surfaces.ContextMenu,
            Execute: ctx => LinkToHere(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsLinkTarget: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en klemme eller scenarie-beholder at linke til.")));

        Registry.Register(new CommandSpec("link.jumpOpposite", "F4",
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: Sync(ctx => NavigateLinkOpposite(ResolveNode(ctx))),
            Gate: ctx => ctx.Node is { IsLinkRow: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en link-række for at hoppe til dens modsatte halvdel.")));

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
                : EditVerdict.Refuse("Vælg en node med egenskaber."),
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
                : EditVerdict.Refuse("Allerede i konfigurationsvisning.")));

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
                : EditVerdict.Refuse("Vælg en hændelsesgruppe i en ulåst blok.")));

        Registry.Register(new CommandSpec("program.toggleSaveValue", null,
            Surfaces.ContextMenu,
            Execute: ctx => ToggleSaveValue(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsOutputPin: true }
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en udgang.")));

        // W4/F11: creating a PROGRAM, the fourth Insert ▸ Program elements entry. A block may hold several programs
        // (project2-CustomBlock's AutoProof holds two), and the SDK command behind this is AddProgram (T018).
        Registry.Register(new CommandSpec("program.addProgram", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => AddProgram(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { Kind: TreeNodeKind.Programs } && !ctx.ProgrammingBlockLocked
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg Programmer-gruppen i en ulåst blok.")));

        Registry.Register(new CommandSpec("program.addSubProgram", null,
            Surfaces.MenuBar | Surfaces.ContextMenu,
            Execute: ctx => AddSubProgram(ResolveNode(ctx)),
            Gate: ctx => ctx.Node is { IsCommandsContainer: true } && !ctx.ProgrammingBlockLocked
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Vælg en kommandogruppe i en ulåst blok.")));

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
                : EditVerdict.Refuse("Vælg en case i en ulåst blok.")));
    }

    // crudarch T015: the app-level rows. Most gate on ProjectOpen or Allow; Save is ALWAYS enabled (D07 —
    // vendor parity); OpenRecent/SetTheme stay parameterized item commands (non-rows).
    private void RegisterAppRows()
    {
        // T017 (US-052/U-BP-07): Undo/Redo gate on the document's history — greyed when empty. The XAML owns the
        // captions, which here are DYNAMIC and action-named (UndoMenuHeader/RedoMenuHeader).
        RegisterAppRow("edit.undo", "Ctrl+Z", _ => Undo(),
            ctx => ctx.CanUndo ? EditVerdict.Allow : EditVerdict.Refuse("Intet at fortryde."));
        RegisterAppRow("edit.redo", "Ctrl+Y", _ => Redo(),
            ctx => ctx.CanRedo ? EditVerdict.Allow : EditVerdict.Refuse("Intet at gentage."));
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
        RegisterAppRow("project.moduleMap", null, _ => ModuleMap(), ProjectOpenGate);
        // Alignment F-4: both transfer commands need a controller, and are withheld — with a reason — without
        // one, exactly as the reference application greys Hent/Send projekt (measured on a fresh AND on a saved
        // project, so its gate is the connection, not the document). This is the app's own spec too: FR-9.1
        // sends "to a connected controller" and every E10 scenario opens "Given a controller is connected".
        // Offering them while the status bar reads "Ikke forbundet til controller" advertised a transfer the
        // app knew it could not make, and answered with a message box what the indicator already said.
        RegisterAppRow("controller.send", "F5", _ => SendProject(),
            ctx => !ctx.ControllerConnected
                ? EditVerdict.Refuse("Ingen controller er forbundet.")
                : ProjectOpenGate(ctx),
            Surfaces.MenuBar | Surfaces.Toolbar);   // T020: a real toolbar button (persistent surface)
        RegisterAppRow("controller.retrieve", null, _ => RetrieveProject(),
            ctx => ctx.ControllerConnected
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Ingen controller er forbundet."),
            Surfaces.MenuBar | Surfaces.Toolbar);
        // T015 (R12/D01): the three report entries, each opening the shared picker pre-selected.
        RegisterAppRow("reports.functions", null, _ => OpenReportPicker(ReportKind.Functions), ProjectOpenGate);
        RegisterAppRow("reports.installation", null, _ => OpenReportPicker(ReportKind.Installation), ProjectOpenGate);
        RegisterAppRow("reports.functionBlocks", null, _ => OpenReportPicker(ReportKind.FunctionBlocks), ProjectOpenGate);
        // W10/F12: the enumerator-type manager, on the Library menu where the reference application puts it.
        RegisterAppRow("library.manageEnumTypes", null, _ => ManageEnumTypesAsync(), ProjectOpenGate);
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
        ctx.ProjectOpen ? EditVerdict.Allow : EditVerdict.Refuse(EditRefusals.NoProjectOpenRefusal);

    // Ctrl+I/Ctrl+U pin authoring: only inside an UNLOCKED block's programming view (A-27).
    private EditVerdict ProgrammingAuthoringGate(ShellContext ctx) =>
        ctx.IsProgrammingMode && !ctx.ProgrammingBlockLocked
            ? EditVerdict.Allow
            : EditVerdict.Refuse("Åbn først en ulåst bloks program.");

    // Conditions-group authoring (US-029): a conditions/logic group in an unlocked block.
    private EditVerdict ConditionsGate(ShellContext ctx) =>
        ctx.Node is { IsConditionsContainer: true } && !ctx.ProgrammingBlockLocked
            ? EditVerdict.Allow
            : EditVerdict.Refuse("Vælg en betingelsesgruppe i en ulåst blok.");

    // Reordering applies only to unlocked structural tree items, never the function-block projection used as the
    // active programming root; the session supplies directional boundary verdicts. Its index-backed CanReorder probe
    // applies the same boundary rule the ReorderNode factory does plus the command's own verdict,
    // so the keybindings stop firing no-ops, the flyout omits an impossible move, and this gate (re-run on every
    // selection change, twice) costs dictionary lookups instead of tree walks and mints nothing (review F02).
    private EditVerdict MoveGate(ShellContext ctx, int delta) =>
        ctx.Node is { CanReorder: true, Id: { } id } node && !ctx.ProgrammingBlockLocked
            && !(ctx.IsProgrammingMode && node.Kind == TreeNodeKind.FunctionBlock)
            ? _session.CanReorder(id, delta)
                ? EditVerdict.Allow
                : EditVerdict.Refuse(delta < 0 ? "Allerede først blandt sine søskende." : "Allerede sidst blandt sine søskende.")
            : EditVerdict.Refuse("Vælg en lokalitet, et produkt eller en funktionsblok, der skal flyttes.");

    // (uxparity2 T017/T031) There is deliberately NO shared "the bar is stricter on a locked block" helper here any
    // more. That rule — the bar greying a locked block's structural commands while its own flyout offers them — was
    // retired by D15 after both surfaces were measured enabling them, on both fixtures. Cut and Delete now carry no
    // SurfacePolicy at all, so the rule has no home to regrow in.

    // Resolves the context row back to its live tree node for the command bodies; the id-less Localities root
    // falls back to the selection (it IS the selected row whenever its context is active).
    private TreeNodeViewModel? ResolveNode(ShellContext ctx) =>
        ctx.Node?.Id is { } id ? FindInEitherPane(id) : SelectedNode;

    // The locked-block authoring gates depend on which block is being programmed; re-evaluate them when that changes.
    private void NotifyProgrammingAuthoringGates()
    {
        OnPropertyChanged(nameof(IsProgrammingBlockLocked));
        NotifyProgramMenuGates();
        RebuildContext();   // T012/T013: every registry row re-evaluates off the lock/mode state
    }

    // The submenu gates read their menus' CONTENTS, so they must be re-raised whenever those menus are rebuilt.
    // A selection change is covered by the [NotifyPropertyChangedFor] entries on SelectedNode (which fire last,
    // after the variable palette is rebuilt); this is the other trigger — arming an operand refills the five
    // program menus without touching the selection at all. Deliberately does NOT rebuild the availability context:
    // these six are plain XAML IsVisible gates, not registry rows, so no row's verdict depends on them.
    private void NotifyProgramMenuGates()
    {
        OnPropertyChanged(nameof(CanInsertVariable));
        OnPropertyChanged(nameof(CanAddEvent));
        OnPropertyChanged(nameof(CanAddCommand));
        OnPropertyChanged(nameof(CanAddCase));
        OnPropertyChanged(nameof(CanAddArithmetic));
        OnPropertyChanged(nameof(CanAddCondition));
    }

    /// <summary>Leaves programming mode (US-026, Esc), restoring the two locality trees of configuration mode.</summary>
    private void LeaveProgrammingMode()
    {
        if (!IsProgrammingMode)
            return;
        AsOneContextRebuild(() =>   // review F03: mode + refresh + authoring gates = ONE transition, one sweep
        {
            SetProgrammingBlock(null);
            Refresh();
            NotifyProgrammingAuthoringGates();
            StatusText = "Konfigurationstilstand.";
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
        StatusText = $"Hoppede til {SelectedNode?.DisplayName}.";
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
            new EnumDefinitionInput("Ny enumerator", string.Empty, System.Array.Empty<string>(), IsNew: true));
        if (result is null || string.IsNullOrWhiteSpace(result.TypeName))
            return;
        if (_session.Current is { } project
            && _session.Commands.AddEnumVariable(project, sectionId, result.TypeName, result.TypeName, result.States) is { } command)
            await ApplyAsync(command, $"Enumeratoren '{result.TypeName}' blev indsat under {sectionLabel}");
    });

    // PG-7/D02: authors a standalone (0-state, unreferenced) enumerator TYPE — no variable is inserted, decoupled from
    // any section. The enumerator dialog supplies the name (and any states); an empty type is authored when none given.
    private Task AddStandaloneEnumTypeAsync() => RunAsync(nameof(AddStandaloneEnumTypeAsync), async () =>
    {
        EnumDefinitionResult? result = await _dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput("Ny selvstændig enumerator type", string.Empty, System.Array.Empty<string>(), IsNew: true));
        if (result is null || string.IsNullOrWhiteSpace(result.TypeName))
            return;
        if (_session.Current is { } project)
            await ApplyAsync(_session.Commands.AddStandaloneEnumType(project, result.TypeName, result.States),
                $"Enumerator typen '{result.TypeName}' blev oprettet");
    });

    /// <summary>
    /// Library ▸ Rediger Enumerator typer (US-030, W10/F12): the two-pane types-and-values editor, shaped on the
    /// reference application's dialog of the same name (measured 2026-08-04). The dialog owns WHICH button was
    /// pressed; this owns what each one means — so every enumerator edit goes through the same command gateway,
    /// undo history and refusal reporting as the rest of the app.
    /// <para>
    /// It applies LIVE (the vendor's dialog has an OK and no Cancel), so the dialog re-reads
    /// <see cref="ProjectProjections.GetEnumeratorTypeViews"/> after each operation rather than holding a copy.
    /// </para>
    /// </summary>
    private Task ManageEnumTypesAsync() => RunAsync(nameof(ManageEnumTypesAsync), () =>
        _dialogs.ManageEnumTypesAsync(new EnumTypeManagerInput(
            "Enumerator typer og værdier",
            () => _session.Current?.GetEnumeratorTypeViews() ?? System.Array.Empty<EnumTypeView>(),
            ApplyEnumTypeOperationAsync)));

    /// <summary>Turns one enumerator-manager button press into its command and applies it. Returns null when it
    /// committed, otherwise the refusal sentence — the dialog cannot show a reason it is not handed. An outcome
    /// that did not merely refuse hands over the generic instead: its reason is an engine diagnostic that
    /// <see cref="ReportOutcomeAsync"/> has already logged and answered with its own Danish sentence.</summary>
    private async Task<string?> ApplyEnumTypeOperationAsync(EnumTypeManagerOperation operation)
    {
        if (_session.Current is not { } project)
            return EditRefusals.NoProjectOpenRefusal;

        (ProjectCommand command, string status) = operation switch
        {
            EnumTypeManagerOperation.NewType op =>
                ((ProjectCommand)_session.Commands.AddStandaloneEnumType(project, op.Name, System.Array.Empty<string>()),
                    $"Enumerator typen '{op.Name}' blev oprettet."),
            EnumTypeManagerOperation.RenameType op =>
                (_session.Commands.RenameEnumType(project, op.TypeName, op.NewName),
                    $"Enumerator typen blev omdøbt til '{op.NewName}'."),
            EnumTypeManagerOperation.DeleteType op =>
                (_session.Commands.DeleteEnumType(project, op.TypeName),
                    $"Enumerator typen '{op.TypeName}' blev slettet."),
            EnumTypeManagerOperation.NewValue op =>
                (_session.Commands.AddEnumValue(project, op.TypeName, op.Name),
                    $"Værdien '{op.Name}' blev tilføjet '{op.TypeName}'."),
            EnumTypeManagerOperation.RenameValue op =>
                (_session.Commands.RenameEnumValue(project, op.TypeName, op.ValueIndex, op.NewName),
                    $"Værdien blev omdøbt til '{op.NewName}'."),
            EnumTypeManagerOperation.DeleteValue op =>
                (_session.Commands.DeleteEnumValue(project, op.TypeName, op.ValueIndex),
                    $"Værdien blev slettet fra '{op.TypeName}'."),
            _ => throw new System.ArgumentOutOfRangeException(nameof(operation)),
        };

        EditOutcome outcome = await _session.ApplyAsync(command);
        await ReportOutcomeAsync(outcome, status);
        return outcome.Status == EditStatus.Committed ? null : UserFacingRefusal(outcome) ?? EditRejectedMessage;
    }

    // PG-4: inserts a variable of an EXISTING enumerator type — references its def-id, authoring NO new type (the "Ny…"
    // option above authors a new one).
    private Task InsertEnumOfExistingTypeAsync(ElementId sectionId, string typeName, string sectionLabel) =>
        RunAsync(nameof(InsertEnumOfExistingTypeAsync), async () =>
        {
            if (_session.Current is { } project
                && _session.Commands.AddEnumVariableOfType(project, sectionId, typeName, typeName) is { } command)
                await ApplyAsync(command, $"Enumeratoren '{typeName}' blev indsat under {sectionLabel}");
        });


    private Task AboutAsync() => RunAsync(nameof(AboutAsync), () => _dialogs.ShowAboutAsync());

    private Task ShowSettingsAsync() => RunAsync(nameof(ShowSettingsAsync), () => _dialogs.ShowSettingsAsync(BuildSettingsText()));

    private Task TelemetryDiagnosticsAsync() => RunAsync(nameof(TelemetryDiagnosticsAsync), async () =>
    {
        string? host = _config?.TelemetryConfig.Host;
        if (string.IsNullOrWhiteSpace(host))
            await _dialogs.ShowMessageAsync("Telemetridiagnostik", "Der er ikke konfigureret nogen telemetri-vært i ihcsettings.json.");
        else if (!await _dialogs.OpenExternalUrlAsync(host))
            await _dialogs.ShowMessageAsync("Telemetridiagnostik", $"Kunne ikke åbne telemetri-værten:\n{host}");
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
            // Same rule as a Failed edit outcome (D01): the exception message is an English developer diagnostic
            // naming element tags, attribute names and _0x ids, so it goes to the log and the installer gets one
            // fixed Danish sentence. This is the widest instance of that channel — every command routes through
            // here — and it is the one place that cannot name what failed, since it catches for all of them.
            StatusText = UnexpectedErrorMessage;
            await _dialogs.ShowMessageAsync(UnexpectedErrorTitle, UnexpectedErrorMessage);
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
            CanUndo: _session.CanUndo, CanRedo: _session.CanRedo,
            ControllerConnected: IsControllerConnected);
        // The generated catalog leaves are gated on the same selection/pane inputs the registry rows are, but
        // they are ICommands rather than registry rows, so the sweep above does not reach them (alignment F-8).
        // Here, in the ONE funnel every availability trigger already passes through, rather than at each of the
        // five trigger sites — where the next new trigger would silently not include it.
        RefreshCatalogLeafAvailability();
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
            if (_programmingBlockId is { } blockId
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
                SetProgrammingBlock(null);   // the block is gone (or never set) → configuration mode
                InstallationPaneHeader = "Installation";
                FunctionsPaneHeader = "Funktioner";
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
        sb.AppendLine($"Program: {Constants.AppName} {Ihc.Bootstrap.AppTelemetryBootstrap.GetAppVersionStr()}");
        sb.AppendLine($"SDK: {Ihc.VersionInfo.GetSdkVersionStr()}");
        sb.AppendLine();
        if (_config is null)
        {
            sb.AppendLine("Ingen konfiguration indlæst.");
            return sb.ToString();
        }

        sb.AppendLine($"Indstillingsfil: {(_config.SettingsFileFound ? _config.SettingsFilePath : "(ingen — bruger standardværdier)")}");
        sb.AppendLine();
        sb.AppendLine("Controller:");
        sb.AppendLine($"  Slutpunkt: {OrNone(_config.IhcSettings.Endpoint)}");
        sb.AppendLine($"  Bruger: {OrNone(_config.IhcSettings.UserName)}");
        sb.AppendLine();
        sb.AppendLine("Telemetri:");
        sb.AppendLine($"  Log: {OrNone(_config.TelemetryConfig.Logs)}");
        sb.AppendLine($"  Spor: {OrNone(_config.TelemetryConfig.Traces)}");
        sb.AppendLine($"  Selvtjek: {OrNone(_config.TelemetryConfig.SelfCheckEndpoint)}");
        return sb.ToString();

        static string OrNone(string? value) => string.IsNullOrWhiteSpace(value) ? "(ikke angivet)" : value;
    }

}
