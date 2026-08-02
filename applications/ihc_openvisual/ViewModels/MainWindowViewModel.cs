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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInsertProduct))]
    [NotifyPropertyChangedFor(nameof(CanInsertFunctionBlock))]
    [NotifyPropertyChangedFor(nameof(CanPaste))]
    [NotifyPropertyChangedFor(nameof(CanInsertVariable))]
    [NotifyPropertyChangedFor(nameof(CanAddEvent))]
    [NotifyPropertyChangedFor(nameof(CanAddCommand))]
    [NotifyPropertyChangedFor(nameof(CanAddCaseValue))]
    [NotifyPropertyChangedFor(nameof(CanAddCondition))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelected))]
    [NotifyPropertyChangedFor(nameof(CanMoveSelected))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]   // the Delete-key route gates on DeleteCommand.CanExecute (T003)
    [NotifyPropertyChangedFor(nameof(CanCutSelected))]
    [NotifyPropertyChangedFor(nameof(CanCutFromMenuBar))]
    [NotifyPropertyChangedFor(nameof(CanCopySelected))]
    [NotifyPropertyChangedFor(nameof(CanCopyFromContextMenu))]
    [NotifyPropertyChangedFor(nameof(CanDeleteFromMenuBar))]
    [NotifyPropertyChangedFor(nameof(CanInsertLocalityHere))]
    [NotifyPropertyChangedFor(nameof(CanShowProperties))]
    [NotifyPropertyChangedFor(nameof(CanShowProgram))]
    [NotifyPropertyChangedFor(nameof(CanShowProgramFromMenuBar))]
    [NotifyPropertyChangedFor(nameof(CanNavigateLinkOpposite))]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(PasteCommand))]
    [NotifyCanExecuteChangedFor(nameof(PropertiesCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnterProgrammingModeCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateLinkOppositeCommand))]
    [NotifyCanExecuteChangedFor(nameof(InsertEmptyFunctionBlockCommand))]
    [NotifyCanExecuteChangedFor(nameof(InsertLocalityCommand))]
    private TreeNodeViewModel? _selectedNode;

    /// <summary>Whether the block currently being programmed is a locked (library) block. A locked block is
    /// VIEW-ONLY: its program renders, but every authoring command is withdrawn (A-27/F-076) — the installer must
    /// unlock it deliberately first. Unlock is a separate, irreversible action (F-046).</summary>
    public bool IsProgrammingBlockLocked =>
        IsProgrammingMode && _programmingBlockId is { } id
        && _session.Current is { } project && project.FindById(id) is { } block
        && project.View(block).Locked;

    // The programming-mode authoring context-menu gates: a container node's own kind AND an editable (unlocked)
    // programming block. On a locked block every one is false, so the vendor's "missing, not greyed" affordance holds.
    public bool CanInsertVariable => SelectedNode?.IsBlockSection == true && !IsProgrammingBlockLocked;
    public bool CanAddEvent => SelectedNode?.IsEventsContainer == true && !IsProgrammingBlockLocked;
    public bool CanAddCommand => SelectedNode?.IsCommandsContainer == true && !IsProgrammingBlockLocked;
    public bool CanAddCaseValue => SelectedNode?.IsCaseNode == true && !IsProgrammingBlockLocked;
    public bool CanAddCondition => SelectedNode?.IsConditionsContainer == true && !IsProgrammingBlockLocked;

    /// <summary>Context-menu gate for <i>Delete</i>: a thin projection of the SDK deletion verdict
    /// (<see cref="CanDeleteNode"/> → the engine's <c>CanDelete</c>, D09/T003), so the same rule that refuses a
    /// catalog pin or a node inside a locked (library) block drives the context menu, Edit ▸ Delete AND the Delete
    /// key — no route can bypass the guard. This subsumes the former locked-block special-case: a node inside a
    /// locked block is not deletable per the SDK, so the vendor's view-only affordance (F-087) still holds.</summary>
    public bool CanDeleteSelected => CanDeleteNode(SelectedNode);

    /// <summary>Context-menu gate for <i>Move up/down</i>: a reorderable structural node (locality/product/function
    /// block, US-068/D07) AND an unlocked programming block — Move has no SDK verdict of its own, so it keeps the
    /// measured locked-block rule (F-087).</summary>
    public bool CanMoveSelected => SelectedNode?.CanReorder == true && !IsProgrammingBlockLocked;

    /// <summary>Context-menu gate: <i>Paste</i> is offered on a locality only when the clipboard holds a cut/copied
    /// node (A-5b/F-010) — the vendor shows it conditionally (6 items empty, 7 full).</summary>
    public bool CanPaste => _clipboardId is not null && SelectedNode?.NodeKind == "locality";

    // Menu-enablement gates measured against IHC Visual (uxparity S-27): the vendor GREYS a command that cannot
    // apply to the current selection, which is how the installer sees what is possible. OpenVisual enforced the
    // same rules when a command ran, but left every menu item enabled, so the menu promised more than it did.
    /// <summary>Cut needs a structural node that may be MOVED — and the vendor greys it on a locked (library)
    /// block, whose contents are not the installer's to take (uxparity S-28).</summary>
    public bool CanCutSelected =>
        SelectedNode?.ElementId is not null && SelectedNode?.CanCut == true;

    /// <summary>Copy reaches further than Cut: the vendor offers it on a product terminal, which cannot be cut
    /// (measured on `Tryk (venstre)`: Kopier enabled, Klip greyed). Only a PRODUCT terminal — an FB pin's
    /// context menu has no Kopier — so the discriminator is the pin's own resource tag.</summary>
    public bool CanCopySelected =>
        SelectedNode?.ElementId is not null && (SelectedNode?.CanCopy == true || SelectedNode?.IsPin == true);

    /// <summary>The CONTEXT menu is narrower than the bar here: the vendor offers Kopier on a product terminal's
    /// flyout but NOT on a function-block pin's, while Rediger ▸ Kopier is enabled for both (uxparity S-28).</summary>
    public bool CanCopyFromContextMenu =>
        SelectedNode?.ElementId is not null
        && (SelectedNode?.CanCopy == true || SelectedNode?.NodeKind?.StartsWith("pin:dataline") == true);

    /// <summary>Cut in the MENU BAR is stricter than on the context menu, like Delete and Show program: the
    /// vendor greys Klip in Rediger for a locked block while its context menu still offers it (S-28).</summary>
    public bool CanCutFromMenuBar => CanCutSelected && SelectedNode?.IsLockedFunctionBlock != true;

    /// <summary>Delete in the MENU BAR is stricter than on the context menu: the vendor greys it for a locked
    /// (library) block in Rediger while its own context menu still offers Slet there (uxparity S-28, verified by
    /// screenshot). Both surfaces are reproduced as measured rather than reconciled.</summary>
    public bool CanDeleteFromMenuBar => CanDeleteSelected && SelectedNode?.IsLockedFunctionBlock != true;

    /// <summary>Show program in the MENU BAR is stricter for the same reason as Delete: the vendor greys Vis
    /// program in Vis for a locked (library) block while offering it on the block's own context menu
    /// (uxparity S-28). A locked block's program is view-only, not unreachable.</summary>
    public bool CanShowProgramFromMenuBar =>
        SelectedNode?.IsFunctionBlock == true && SelectedNode?.IsLockedFunctionBlock != true;

    /// <summary>Insert ▸ Locality is offered only on the localities root — a locality cannot hold a locality
    /// (S-07), and the vendor greys the item everywhere else.</summary>
    public bool CanInsertLocalityHere => SelectedNode?.CanInsertLocality == true;

    /// <summary>Properties needs a node that HAS properties: the localities root does not.</summary>
    public bool CanShowProperties => SelectedNode?.ElementId is not null && SelectedNode?.IsLocalitiesRoot != true;

    /// <summary>Leaving programming mode is only possible while in it.</summary>
    public bool CanLeaveProgrammingMode => IsProgrammingMode;

    /// <summary>Show program needs a function block.</summary>
    public bool CanShowProgram => OwningFunctionBlockOf(SelectedNode) is not null;

    /// <summary>Jump-to-opposite needs a link row.</summary>
    public bool CanNavigateLinkOpposite => SelectedNode?.IsLinkRow == true;

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
    [RelayCommand]
    private void UseInProgram(TreeNodeViewModel? node) => _programAuthoring.Arm(node);

    /// <summary>The Edit ▸ Undo menu header, naming the action it would reverse (E14): e.g. "Undo Insert locality",
    /// or just "Undo" when the history is empty. The leading underscore keeps the Alt+U access key.</summary>
    public string UndoMenuHeader => _session.CanUndo ? $"_Undo {_session.UndoLabel}" : "_Undo";

    /// <summary>The Edit ▸ Redo menu header, naming the action it would re-apply (E14), or just "Redo".</summary>
    public string RedoMenuHeader => _session.CanRedo ? $"_Redo {_session.RedoLabel}" : "_Redo";

    /// <summary>Edit ▸ Undo (US-052, Ctrl+Z): reverses the last project-mutating edit; a no-op when there is nothing
    /// to undo. Refreshes both panes via the session's StateChanged.</summary>
    [RelayCommand]
    private Task Undo() => RunAsync(nameof(Undo), async () =>
    {
        string? label = _session.UndoLabel;   // capture before the stack pops — names the action (E14)
        StatusText = await _session.UndoAsync()
            ? label is null ? "Undid the last change." : $"Undid: {label}"
            : "Nothing to undo.";
    });

    /// <summary>Edit ▸ Redo (US-052, Ctrl+Y): re-applies the last undone edit; a no-op when the redo history is empty.</summary>
    [RelayCommand]
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
    [RelayCommand]
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
    [RelayCommand]
    private Task InsertInput() => InsertBlockPinAsync("inputs", "resource_input", "Input");

    /// <summary>Inserts an output variable into the programming block's Output section (US-045, Ctrl+U).</summary>
    [RelayCommand]
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
    [RelayCommand]
    private Task ProjectInfo() => RunAsync(nameof(ProjectInfo), async () =>
    {
        ProjectInfoData? result = await _dialogs.EditProjectInfoAsync(_session.GetProjectInfo());
        if (result is null || _session.Current is not { } project)
            return;
        await ApplyAsync(_session.Commands.UpdateProjectInfo(project, result), "Project information updated.");
    });

    /// <summary>Documentation ▸ Data tables (US-049): opens the data-tables dialog (read-only system tables +
    /// editable user-defined texts).</summary>
    [RelayCommand]
    private Task DataTables() => RunAsync(nameof(DataTables), async () =>
    {
        await _dialogs.ShowDataTablesAsync(new DataTablesViewModel(_session, _dialogs));
    });

    /// <summary>Documentation ▸ Wired module map (US-050): opens the read-only wired input/output module address map.</summary>
    [RelayCommand]
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
    [RelayCommand]
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
    [RelayCommand]
    private Task RetrieveProject() => RunAsync(nameof(RetrieveProject), async () =>
    {
        await _dialogs.ShowMessageAsync("Controller required", "Retrieving a project " + ControllerRequiredMessage);
        StatusText = "Controller transfer requires a connected controller.";
    });

    /// <summary>Documentation ▸ Reports… (US-040 / D14 / T021): open the single Reports view rendering the combined
    /// project-documentation model as ONE navigable HTML document (on-screen or printer variant) — the one command
    /// that replaces the former six direct installation/end-user/function-block screen/print commands.</summary>
    [RelayCommand]
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
    [RelayCommand]
    private Task AddPowerEvent(TreeNodeViewModel? node) => _programAuthoring.AddPowerEventAsync(node);

    /// <summary>Toggles an output's <i>Save current value</i> power-loss persistence (US-033).</summary>
    [RelayCommand]
    private Task ToggleSaveValue(TreeNodeViewModel? node) => _programAuthoring.ToggleSaveValueAsync(node);

    /// <summary>Inserts a conditional sub-program into a Commands group (US-029).</summary>
    [RelayCommand]
    private Task AddSubProgram(TreeNodeViewModel? node) => _programAuthoring.AddSubProgramAsync(node);

    /// <summary>Inserts a nested logic group inside a Conditions group (US-029).</summary>
    [RelayCommand]
    private Task AddLogicGroup(TreeNodeViewModel? node) => _programAuthoring.AddLogicGroupAsync(node);

    /// <summary>Combines a Conditions group with OR (<c>&gt;=1</c>) (US-029).</summary>
    [RelayCommand]
    private Task SetConditionsOr(TreeNodeViewModel? node) => _programAuthoring.SetConditionsOrAsync(node);

    /// <summary>Combines a Conditions group with AND (<c>&amp;</c>, the default) (US-029).</summary>
    [RelayCommand]
    private Task SetConditionsAnd(TreeNodeViewModel? node) => _programAuthoring.SetConditionsAndAsync(node);

    /// <summary>Adds a case value branch to the selected Case node (US-031).</summary>
    [RelayCommand]
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
            id => FindNode(InstallationNodes, id) ?? FindNode(FunctionNodes, id),
            () => IsProgrammingBlockLocked,
            (command, status) => ApplyAsync(command, status),
            _programAuthoring.ArmAndSelect,
            status => StatusText = status,
            RunAsync);
        _linking = new LinkingCoordinator(
            _session, _dialogs, RunAsync, (command, status) => ApplyAsync(command, status), status => StatusText = status,
            () => PendingLinkSource, node => PendingLinkSource = node, RevealAndSelectOpposite);

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
    [RelayCommand]
    private Task ImportCatalogFile() => RunAsync(nameof(ImportCatalogFile), async () =>
    {
        if (await _dialogs.PickCatalogFileAsync() is not { } path)
            return;
        if (await _session.ImportCatalogFileAsync(path, persist: true))
            StatusText = "Imported 1 component (persisted to the catalog folder).";
    });

    /// <summary>Library ▸ Import catalog folder (US-060): imports every <c>.def</c>/<c>.ifb</c> in a folder and its
    /// subfolders, reporting how many components were imported; persisted by default (US-061).</summary>
    [RelayCommand]
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
    [RelayCommand(CanExecute = nameof(CanInsertFunctionBlock))]
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

    [RelayCommand]
    private Task NewAsync() => RunAsync(nameof(NewAsync), async () =>
    {
        if (await _session.NewAsync())
            StatusText = "Started a new project.";
    });

    [RelayCommand]
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

    [RelayCommand]
    private Task SaveAsync() => RunAsync(nameof(SaveAsync), async () =>
    {
        if (await _session.SaveAsync())
            StatusText = $"Saved {_session.DocumentName}.";
    });

    [RelayCommand]
    private Task SaveAsAsync() => RunAsync(nameof(SaveAsAsync), async () =>
    {
        if (await _session.SaveAsAsync())
            StatusText = $"Saved {_session.DocumentName}.";
    });

    [RelayCommand]
    private Task CloseAsync() => RunAsync(nameof(CloseAsync), async () =>
    {
        if (await _session.CloseAsync())
            StatusText = "Closed the project.";
    });

    [RelayCommand]
    private void Exit() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ToggleToolbar()
    {
        IsToolbarVisible = !IsToolbarVisible;
        StatusText = IsToolbarVisible ? "Toolbar shown." : "Toolbar hidden.";
    }

    [RelayCommand]
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
    [RelayCommand(CanExecute = nameof(CanInsertLocalityHere))]
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
    [RelayCommand]
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
    [RelayCommand]
    private Task Unlock(TreeNodeViewModel? node) => RunAsync(nameof(Unlock), async () =>
    {
        if (node?.ElementId is not { } id || _session.Current is not { } project)
            return;
        string name = node.DisplayName;
        // Unlocking takes ownership of the block (uxparity S-20), so it is stamped with whoever did it.
        await ApplyAsync(_session.Commands.UnlockFunctionBlock(project, id, Environment.UserName), $"Unlocked {name}.");
    });

    /// <summary>The single SDK-backed delete gate (review3 H1 / T003, D09): a node is deletable exactly when the
    /// engine's <see cref="DeleteImpact.Deletable"/> verdict allows it — a catalog pin or a node inside a locked
    /// function block is refused. All three delete routes project THIS decision: the context menu / Edit ▸ Delete via
    /// <see cref="CanDeleteSelected"/>, and the Delete key via this command's <c>CanExecute</c> — so none can bypass
    /// the guard (the former Delete-key path gated on a raw per-node deletable flag, which ignored the lock).</summary>
    private bool CanDeleteNode(TreeNodeViewModel? node) =>
        node?.ElementId is { } id && _session.Current is { } project
        && _session.Commands.CanDelete(project, id);

    /// <summary>Deletes the selected node (US-053), dispatching by type: a link row removes its reciprocal pair
    /// (US-057), a locality uses the US-009 cascade, and any other node (product, block, variable, program element)
    /// uses the general confirm-and-cascade delete. Reachable from the right-click item, Edit ▸ Delete, and the
    /// Delete key (US-044) — all three routes call this command, gated by <see cref="CanDeleteNode"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteNode))]
    private Task Delete(TreeNodeViewModel? node) => RunAsync(nameof(Delete), async () =>
    {
        // The locality root is structure, not content: it holds the localities but is not itself a node the
        // installer can remove. It used to be protected only by having no element id at all — now that it
        // carries one (so a locality can be pasted onto it), the rule has to be stated.
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

    /// <summary>Cut the selected node (US-054, Ctrl+X): stashes it so a Paste onto a locality moves it there.</summary>
    [RelayCommand(CanExecute = nameof(CanCutSelected))]
    private void Cut(TreeNodeViewModel? node)
    {
        if (node?.ElementId is not { } id)
            return;
        _clipboardId = id;
        _clipboardIsCut = true;
        OnPropertyChanged(nameof(CanPaste));
        StatusText = $"Cut {node.DisplayName} — paste onto a locality to move it.";
    }

    /// <summary>Copy the selected node (US-056, Ctrl+C): stashes it so a Paste onto a locality duplicates it.</summary>
    [RelayCommand(CanExecute = nameof(CanCopySelected))]
    private void Copy(TreeNodeViewModel? node)
    {
        if (node?.ElementId is not { } id)
            return;
        _clipboardId = id;
        _clipboardIsCut = false;
        OnPropertyChanged(nameof(CanPaste));
        StatusText = $"Copied {node.DisplayName} — paste onto a locality to duplicate it.";
    }

    /// <summary>Paste the clipboard node onto the selected target (US-054 move / US-056 duplicate, Ctrl+V).</summary>
    [RelayCommand(CanExecute = nameof(CanPaste))]
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
                _clipboardId = null;   // a cut is consumed by its paste
                OnPropertyChanged(nameof(CanPaste));
            }
        }
        else if (await ApplyAsync(_session.Commands.CopyNode(project, sourceId, targetId), "Pasted a copy.") is { } pastedId)
        {
            // A copy is not consumed by its paste, so the clipboard stays. Open the arrival all the way down:
            // a pasted subtree lands already populated, so the "reveal on first child" rule never fires for it,
            // and it would otherwise appear as a single closed row giving no sign of what was actually pasted.
            foreach (var pane in new[] { InstallationNodes, FunctionNodes })
            {
                if (FindNode(pane, pastedId) is { } node)
                    ExpandSubtree(node);
            }
        }
    });

    // Opens a node and everything beneath it. Rows with no children are left alone: IsExpanded on a leaf would
    // render an open twisty over nothing.
    private static void ExpandSubtree(TreeNodeViewModel node)
    {
        if (node.Children.Count == 0)
            return;
        node.IsExpanded = true;
        foreach (TreeNodeViewModel child in node.Children)
            ExpandSubtree(child);
    }

    /// <summary>Moves the selected node one position up among its siblings (US-055) — the non-drag reorder route.</summary>
    [RelayCommand]
    private Task MoveUp(TreeNodeViewModel? node) => ReorderAsync(node, -1);

    /// <summary>Moves the selected node one position down among its siblings (US-055).</summary>
    [RelayCommand]
    private Task MoveDown(TreeNodeViewModel? node) => ReorderAsync(node, +1);

    private Task ReorderAsync(TreeNodeViewModel? node, int delta) => RunAsync(nameof(ReorderAsync), async () =>
    {
        if (node?.ElementId is { } id && _session.Current is { } project
            && _session.Commands.ReorderNode(project, id, delta) is { } command)
            await ApplyAsync(command, delta < 0 ? "Moved up." : "Moved down.");
    });

    /// <summary>Opens the Properties dialog for a tree node to rename a locality (US-007). Invoked from the
    /// right-click <i>Properties</i> item (node passed in) and from F2 (the selected node passed in).</summary>
    [RelayCommand(CanExecute = nameof(CanShowProperties))]
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
            foreach (var pane in new[] { InstallationNodes, FunctionNodes })
            {
                if (FindNode(pane, newId) is { } placed)
                    ExpandSubtree(placed);
            }
        });

    /// <summary>Makes <paramref name="node"/> the active node — the insert/command target. Used by tests and by
    /// programmatic selection; the live trees feed the active node through their own two-way selection bindings.</summary>
    public void SelectNode(TreeNodeViewModel node) => SelectedNode = node;

    /// <summary>Toggles a "Log …" row's log mark (US-068, the vendor's &amp;Logmærke): the SDK flips its Logning state
    /// between Off and the first logging mode, and the tree re-renders the row's new state.</summary>
    [RelayCommand]
    private Task ToggleLogMark(TreeNodeViewModel? node) => RunAsync(nameof(ToggleLogMark), async () =>
    {
        if (node is { IsLogMarkPin: true, ElementId: { } id } && _session.Current is { } project)
            await ApplyAsync(_session.Commands.ToggleLogMark(project, id), $"Toggled the log mark on {node.DisplayName}.");
    });

    /// <summary>Enters programming mode for the selected function block (US-026, F3): the panes switch to the block's
    /// variable sections (left) and its program subtree (right), both headed with the block's name.</summary>
    [RelayCommand(CanExecute = nameof(CanShowProgram))]
    private void EnterProgrammingMode(TreeNodeViewModel? node)
    {
        // A PIN opens the program of the block that owns it (uxparity S-28): the vendor offers Vis program on a
        // pin as well as on the block, so you can go straight to the logic that uses the pin.
        if (OwningFunctionBlockOf(node) is { } id)
        {
            _programmingBlockId = id;
            IsProgrammingMode = true;
            Refresh();
            NotifyProgrammingAuthoringGates();
            StatusText = FindNode(FunctionNodes, id)?.IsLockedFunctionBlock == true
                ? "Programming mode (read-only — the block is locked). Press Esc to return."
                : "Programming mode — press Esc to return to configuration.";
        }
    }

    // The function block a node belongs to: the block itself, or the block owning the pin/section (S-28). Null
    // when the node is outside any block, which is what makes Show program a no-op on a locality.
    private ElementId? OwningFunctionBlockOf(TreeNodeViewModel? node)
    {
        ElementId? owner = null;
        if (node is { IsFunctionBlock: true, ElementId: { } blockId })
        {
            owner = blockId;
        }
        else if (node?.ElementId is { } nodeId && _session.Current is { } project)
        {
            for (ProjectElement? e = project.FindParent(nodeId); e is not null; e = e.Id is { } id ? project.FindParent(id) : null)
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

    // The locked-block authoring gates depend on which block is being programmed; re-evaluate them when that changes.
    private void NotifyProgrammingAuthoringGates()
    {
        OnPropertyChanged(nameof(IsProgrammingBlockLocked));
        OnPropertyChanged(nameof(CanInsertVariable));
        OnPropertyChanged(nameof(CanAddEvent));
        OnPropertyChanged(nameof(CanAddCommand));
        OnPropertyChanged(nameof(CanAddCaseValue));
        OnPropertyChanged(nameof(CanAddCondition));
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(CanMoveSelected));
    }

    /// <summary>Leaves programming mode (US-026, Esc), restoring the two locality trees of configuration mode.</summary>
    [RelayCommand(CanExecute = nameof(CanLeaveProgrammingMode))]
    private void LeaveProgrammingMode()
    {
        if (!IsProgrammingMode)
            return;
        IsProgrammingMode = false;
        _programmingBlockId = null;
        Refresh();
        NotifyProgrammingAuthoringGates();
        StatusText = "Configuration mode.";
    }

    /// <summary>Links two pins (US-022/US-023) — a thin entry point delegating to <see cref="LinkingCoordinator"/>
    /// (the drag path and the LinkPins characterization test drive this).</summary>
    public Task LinkPins(TreeNodeViewModel? source, TreeNodeViewModel? target) => _linking.LinkPinsAsync(source, target);

    /// <summary>The pin from which a link is being drawn — armed by <i>Link from here</i>, consumed by
    /// <i>Link to here</i> (US-022). The two-step gesture is the reliable, testable substitute for pin drag-and-drop.</summary>
    [ObservableProperty] private TreeNodeViewModel? _pendingLinkSource;

    /// <summary>Arms a link from the given pin (US-022) — delegates to <see cref="LinkingCoordinator"/>.</summary>
    [RelayCommand]
    private void StartLink(TreeNodeViewModel? node) => _linking.StartLink(node);

    /// <summary>Completes a link onto the given pin or scenes container (US-022/US-024) — delegates to
    /// <see cref="LinkingCoordinator"/>.</summary>
    [RelayCommand]
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
    [RelayCommand(CanExecute = nameof(CanNavigateLinkOpposite))]
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


    [RelayCommand]
    private Task AboutAsync() => RunAsync(nameof(AboutAsync), () => _dialogs.ShowAboutAsync());

    [RelayCommand]
    private Task ShowSettingsAsync() => RunAsync(nameof(ShowSettingsAsync), () => _dialogs.ShowSettingsAsync(BuildSettingsText()));

    [RelayCommand]
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

    // The identity of the view last built into the panes. An in-place rebuild (every edit fires StateChanged →
    // Refresh) keeps the same key, so the panes' expand/collapse state is carried across (US-070); a deliberate
    // MODE switch (config ⇄ a block's programming view) changes the key, so that view opens fresh at its defaults.
    private void Refresh()
    {
        Title = $"{_session.DocumentName} - {Constants.AppName}";
        OnPropertyChanged(nameof(UndoMenuHeader));   // the history may have grown/shrunk — refresh the Edit-menu labels (E14)
        OnPropertyChanged(nameof(RedoMenuHeader));
        DeleteCommand.NotifyCanExecuteChanged();   // an edit (e.g. Unlock) can flip the selected node's delete verdict (T003)
        OnPropertyChanged(nameof(CanDeleteSelected));
        if (IsProgrammingMode && _programmingBlockId is { } blockId
            && _session.Current?.FindById(blockId) is { } block && block.Kind == ElementKind.FunctionBlock)
        {
            // BuildProgrammingTrees clears and rebuilds both panes (fresh node instances), so — exactly like the
            // config-mode fallback below — capture the selection by id and restore it after, else a program edit
            // (every edit fires StateChanged → Refresh) drops the selected container to an orphan (review C5).
            RebuildPreservingSelection(() =>
                _treePanes.BuildProgrammingTrees(block, preserveExpansion: _treePanes.SameViewAsLastBuild("prog:" + blockId.ToToken())));
            return;
        }
        IsProgrammingMode = false;   // the block is gone (or never set) → configuration mode
        _programmingBlockId = null;
        InstallationPaneHeader = "Installation";
        FunctionsPaneHeader = "Functions";
        bool sameView = _treePanes.SameViewAsLastBuild("config");
        // Reconcile in place when this is an incremental edit on the SAME view whose panes still hold the
        // reconcilers' roots; otherwise (load/undo/redo/mode switch/first build) rebuild through the reconciler,
        // which re-seeds it — with expansion carried across as before (W3-6 keeps the fallback permanent).
        if (!(sameView && _treePanes.TryReconcileConfig()))
        {
            // The full-rebuild fallback tears down the node instances, so the reconcile path's by-identity survival
            // of the installer's place is lost here — capture selection (which Avalonia's focus + scroll-into-view
            // follow) by id before the rebuild and restore it after, so undo/redo/load land the user back where they
            // were (E14 place restore). Expansion is carried inside the coordinator's fallback.
            RebuildPreservingSelection(() => _treePanes.RebuildConfig(preserve: sameView));
        }
    }

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
