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
using Ihc.Vis.Editing;
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
public partial class MainWindowViewModel : ViewModelBase
{
    private const string LocalityIcon = "/Assets/locality.svg";

    private readonly ProjectWorkflow _session;

    // The keyed reconcilers for the two configuration-mode panes (W3-6): a committed edit updates the forest in
    // place from the session's change set, preserving node identity (Avalonia keeps containers, selection and
    // expansion) instead of clearing and rebuilding. A non-incremental transition (load/undo/redo/save/mode switch,
    // or a reconcile that falls back) rebuilds through the same reconciler, which re-seeds it.
    private readonly ProjectTreeReconciler _installationReconciler =
        new(p => new ProjectTreeProjector(p).BuildLocalitiesRoot(functions: false));
    private readonly ProjectTreeReconciler _functionsReconciler =
        new(p => new ProjectTreeProjector(p).BuildLocalitiesRoot(functions: true));

    // The per-node-type Properties dialog flows, extracted from this view-model (W3-8). It applies results through
    // this view-model's single outcome→status/dialog rule (ApplyAsync).
    private readonly PropertiesDialogCoordinator _properties;

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

    /// <summary>Context-menu gates for the mutation commands <i>Delete</i> and <i>Move up/down</i>. Each reads the
    /// node's own capability AND that the programming block is not locked: a locked (library) block is fully
    /// view-only — the vendor offers <i>Egenskaber</i> on every node but NEVER Delete or Move (F-087, measured
    /// 2026-07-18). A-27 withdrew only the Add/Insert commands; these complete the view-only affordance. Properties
    /// stays on <see cref="TreeNodeViewModel.CanEditNonLink"/> — only the two mutations are withdrawn on a locked block.</summary>
    public bool CanDeleteSelected => SelectedNode?.CanDelete == true && !IsProgrammingBlockLocked;
    public bool CanMoveSelected => SelectedNode?.CanEditNonLink == true && !IsProgrammingBlockLocked;

    /// <summary>Context-menu gate: <i>Paste</i> is offered on a locality only when the clipboard holds a cut/copied
    /// node (A-5b/F-010) — the vendor shows it conditionally (6 items empty, 7 full).</summary>
    public bool CanPaste => _clipboardId is not null && SelectedNode?.NodeKind == "locality";

    /// <summary>Whether the active selection lives in the <i>Installation</i> pane (vs the <i>Functions</i> pane). The
    /// shared node context menu uses this to offer product insertion only where products belong.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInsertProduct))]
    [NotifyPropertyChangedFor(nameof(CanInsertFunctionBlock))]
    private bool _isInstallationPaneActive;

    /// <summary>Context-menu gate: <i>Insert product</i> is offered only on an addressable node in the Installation
    /// pane — the Functions pane hosts function blocks, and the Localities root hosts localities (US-010).</summary>
    public bool CanInsertProduct => IsInstallationPaneActive && SelectedNode?.CanEditProperties == true;

    /// <summary>Context-menu gate: <i>Insert function block</i> / <i>Empty function block</i> are offered only on an
    /// addressable node in the <i>Functions</i> pane — function blocks belong there, products to the Installation pane
    /// (A-5a/F-008). Mirrors <see cref="CanInsertProduct"/> on the opposite pane.</summary>
    public bool CanInsertFunctionBlock => !IsInstallationPaneActive && SelectedNode?.CanEditProperties == true;

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
    }

    partial void OnSelectedFunctionsNodeChanged(TreeNodeViewModel? value)
    {
        if (value is not null)
        {
            IsInstallationPaneActive = false;
            SelectedNode = value;
        }
    }

    public ObservableCollection<TreeNodeViewModel> InstallationNodes { get; } = new();
    public ObservableCollection<TreeNodeViewModel> FunctionNodes { get; } = new();
    public ObservableCollection<RecentProjectViewModel> RecentProjects { get; } = new();

    /// <summary>The wired-products insertion submenu (US-010), built once from the catalog; leaves insert under the
    /// selected locality.</summary>
    public ObservableCollection<ProductMenuItemViewModel> WiredProductsMenu { get; } = new();

    /// <summary>The special-products insertion submenu (US-013): Controller Link, S0 Device, signal-strength tester,
    /// and the Modified-wireless/Windows/Discontinued subcategories (A-11).</summary>
    public ObservableCollection<ProductMenuItemViewModel> SpecialProductsMenu { get; } = new();

    /// <summary>The Bus-products insertion submenu (A-11): the SMS Modem and the IHC LED Dimmer, both subject to the
    /// one-modem rule where applicable.</summary>
    public ObservableCollection<ProductMenuItemViewModel> BusProductsMenu { get; } = new();

    /// <summary>The IHC Wireless products insertion submenu (US-014), built from the catalog.</summary>
    public ObservableCollection<ProductMenuItemViewModel> WirelessProductsMenu { get; } = new();

    /// <summary>The library function-block insertion submenu (US-018), built from the catalog's FB folders.</summary>
    public ObservableCollection<ProductMenuItemViewModel> FunctionBlocksMenu { get; } = new();

    /// <summary>The variable types insertable into the currently selected block section (US-027); rebuilt when the
    /// selection changes so it only offers the types that section accepts.</summary>
    public ObservableCollection<ProductMenuItemViewModel> VariablePaletteMenu { get; } = new();

    // The variable palette: label, resource tag, and which section kind accepts it ('I'nput / 'O'utput / 'V'alue).
    private static readonly (string Label, string Tag, char Kind)[] VariableTypes =
    {
        ("Input", "resource_input", 'I'),
        ("Output", "resource_output", 'O'),
        ("Flag", "resource_flag", 'V'),
        ("Counter", "resource_counter", 'V'),
        ("Integer", "resource_integer", 'V'),
        ("Decimal", "resource_floating_point", 'V'),
        ("Timer", "resource_timer", 'V'),
        ("Timer value", "resource_timertime", 'V'),
        ("Weekday", "resource_weekday", 'V'),
        ("Date", "resource_date", 'V'),
        ("Time of day", "resource_time", 'V'),
        ("Temperature", "resource_temperature", 'V'),
        ("Light", "resource_light", 'V'),
        ("Holiday", "resource_holiday", 'V'),
        ("Enum", "resource_enum", 'V'),
    };

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value)
    {
        RebuildProgramMenus(value);
        VariablePaletteMenu.Clear();
        if (value is not { IsBlockSection: true, ElementId: { } sectionId, SectionTag: { } sectionTag })
            return;
        char kind = sectionTag switch { "inputs" => 'I', "outputs" => 'O', _ => 'V' };
        string sectionLabel = value.DisplayName;
        foreach ((string label, string tag, char _) in VariableTypes.Where(t => t.Kind == kind))
        {
            VariablePaletteMenu.Add(new ProductMenuItemViewModel(label, tag,
                new AsyncRelayCommand(() => InsertVariableAsync(sectionId, tag, label, sectionLabel))));
        }
    }

    private Task InsertVariableAsync(ElementId sectionId, string tag, string label, string sectionLabel) =>
        // An enum insertion first defines its type + states through the enumerator dialog (US-030); all other
        // variable types insert directly (US-027).
        tag == "resource_enum"
            ? InsertEnumAsync(sectionId, sectionLabel)
            : RunAsync(nameof(InsertVariableAsync), async () =>
            {
                if (_session.BuildAddVariable(sectionId, tag, label) is { } command)
                    await ApplyAsync(command, $"{label} was inserted under {sectionLabel}");
            });

    /// <summary>The events a selected variable can raise, offered on a program's Events node (US-028); rebuilt when
    /// selection or the armed program variable changes.</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramEventMenu { get; } = new();

    /// <summary>The commands a selected variable can be driven by, offered on a program's Commands node (US-028).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramCommandMenu { get; } = new();

    // The GUI-side presentation verbs for each program method (US-028/029), positionally aligned with the SDK
    // ProgramMethodCatalog per-category lists (the SDK owns the tokens/names/notes/semantics; the app owns only the
    // menu verb and which methods to surface). Same order as Events/Commands/Conditions below.
    private static readonly string[] EventVerbs = { "changes to ON", "changes state", "is assigned" };
    private static readonly string[] CommandVerbs = { "set to ON", "set to OFF", "toggled" };
    private static readonly string[] ConditionVerbs = { "is ON", "is OFF", "is NOT ON" };

    /// <summary>The variable armed by <i>Use in program</i> to become the operand of the next event/command (US-028) —
    /// the testable substitute for dragging a variable onto Events/Commands.</summary>
    [ObservableProperty] private TreeNodeViewModel? _pendingProgramVariable;

    partial void OnPendingProgramVariableChanged(TreeNodeViewModel? value) => RebuildProgramMenus(SelectedNode);

    /// <summary>Arms a variable (a block input/output/setting/internal, US-028) as the operand for the next event or
    /// command; the Events/Commands node then offers that variable's triggers and commands.</summary>
    [RelayCommand]
    private void UseInProgram(TreeNodeViewModel? node)
    {
        if (node is { IsPin: true })
        {
            PendingProgramVariable = node;
            StatusText = $"Using {node.DisplayName} — pick 'Add event' or 'Add command' on the program.";
        }
    }

    /// <summary>Arms <paramref name="variable"/> and surfaces the method popup on <paramref name="container"/> — the
    /// drag gesture behind US-028. It selects the drop-target container so the two-step's shared menu-builder
    /// (<see cref="RebuildProgramMenus"/>) populates that container's Add-event/Add-command menu for the armed variable;
    /// the user then chooses a method, which builds the event/command exactly as the two-step <i>Use in program</i>
    /// does. A-27's locked-block gate is applied upstream in <see cref="CanDropOn"/>.</summary>
    private void UseVariableInProgram(TreeNodeViewModel variable, TreeNodeViewModel container)
    {
        PendingProgramVariable = variable;
        SelectNode(container);
        StatusText = $"Using {variable.DisplayName} — choose a method on {container.DisplayName}.";
    }

    private void RebuildProgramMenus(TreeNodeViewModel? value)
    {
        ProgramEventMenu.Clear();
        ProgramCommandMenu.Clear();
        ProgramConditionMenu.Clear();
        ProgramCaseMenu.Clear();
        ProgramArithmeticMenu.Clear();
        if (PendingProgramVariable is not { ElementId: { } varId, DisplayName: { } varName })
            return;
        if (value is { IsEventsContainer: true, ElementId: { } eventsId })
        {
            for (int i = 0; i < ProgramMethodCatalog.Events.Length; i++)
            {
                ProgramMethod m = ProgramMethodCatalog.Events[i];
                ProgramEventMenu.Add(new ProductMenuItemViewModel($"{varName} {EventVerbs[i]}", m.Token,
                    new AsyncRelayCommand(() => AddProgramEventAsync(eventsId, varId, m.Token, m.NameTemplate, m.Note))));
            }
        }
        if (value is { IsCommandsContainer: true, ElementId: { } actionsId })
        {
            for (int i = 0; i < ProgramMethodCatalog.Commands.Length; i++)
            {
                ProgramMethod m = ProgramMethodCatalog.Commands[i];
                ProgramCommandMenu.Add(new ProductMenuItemViewModel($"{varName} {CommandVerbs[i]}", m.Token,
                    new AsyncRelayCommand(() => AddProgramCommandAsync(actionsId, varId, m.Token, m.NameTemplate, m.Note))));
            }
            // A case can be built here when the armed variable is an eligible switch type (US-031).
            if (_session.Current?.FindById(varId)?.Tag is { } varTag && ProjectWorkflow.EligibleCaseVariableTags.Contains(varTag))
                ProgramCaseMenu.Add(new ProductMenuItemViewModel($"Case ({varName})", "case",
                    new AsyncRelayCommand(() => AddCaseAsync(actionsId, varId))));
            // Arithmetic can be built here when the armed variable is a numeric target register (US-032).
            if (_session.Current?.FindById(varId)?.Tag is { } t && NumericTags.Contains(t))
            {
                foreach (ProgramMethod op in ProgramMethodCatalog.Arithmetic)
                {
                    var opNode = new ProductMenuItemViewModel($"{varName} {op.OperatorSymbol}= …");   // category
                    foreach ((string opName, ElementId opId) in NumericOperandsInBlock())
                        opNode.Children.Add(new ProductMenuItemViewModel(opName, "arith",
                            new AsyncRelayCommand(() => AddArithmeticAsync(actionsId, varId, op.Token, opId, op.NameTemplate))));
                    if (opNode.Children.Count > 0)
                        ProgramArithmeticMenu.Add(opNode);
                }
            }
        }
        if (value is { IsConditionsContainer: true, ElementId: { } conditionsId })
        {
            for (int i = 0; i < ProgramMethodCatalog.Conditions.Length; i++)
            {
                ProgramMethod m = ProgramMethodCatalog.Conditions[i];
                ProgramConditionMenu.Add(new ProductMenuItemViewModel($"{varName} {ConditionVerbs[i]}", m.Token,
                    new AsyncRelayCommand(() => AddConditionAsync(conditionsId, varId, m.Token, m.NameTemplate, m.Note))));
            }
        }
    }

    private Task AddProgramEventAsync(ElementId eventsId, ElementId variableId, string method, string name, string note) =>
        RunAsync(nameof(AddProgramEventAsync), async () =>
        {
            if (_session.BuildAddProgramEvent(eventsId, variableId, method, name, note) is { } command)
                await ApplyAsync(command, "Event added to the program.");
        });

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
        string help = node?.ElementId is { } id && _session.Current?.FindById(id)?.GetAttribute("note") is { Length: > 0 } note
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
        if (_session.BuildAddVariable(sectionId, tag, label) is { } command)
            await ApplyAsync(command, $"{label} inserted into the block.");
    });

    /// <summary>Opens the Project information dialog (US-039) prefilled from the project, and applies edits.</summary>
    [RelayCommand]
    private Task ProjectInfo() => RunAsync(nameof(ProjectInfo), async () =>
    {
        ProjectInfoData? result = await _dialogs.EditProjectInfoAsync(_session.GetProjectInfo());
        if (result is null)
            return;
        await ApplyAsync(new UpdateProjectInfo(result), "Project information updated.");
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

    /// <summary>Documentation ▸ Reports (US-040): render the installation/end-user report to HTML and open it in
    /// the standard browser, in a screen or a printer-friendly variant.</summary>
    [RelayCommand]
    private Task InstallationReportScreen() => ShowReportAsync(installation: true, print: false);

    [RelayCommand]
    private Task InstallationReportPrint() => ShowReportAsync(installation: true, print: true);

    [RelayCommand]
    private Task EndUserReportScreen() => ShowReportAsync(installation: false, print: false);

    [RelayCommand]
    private Task EndUserReportPrint() => ShowReportAsync(installation: false, print: true);

    [RelayCommand]
    private Task FunctionBlockReportScreen() => ShowFunctionBlockReportAsync(print: false);

    [RelayCommand]
    private Task FunctionBlockReportPrint() => ShowFunctionBlockReportAsync(print: true);

    private Task ShowFunctionBlockReportAsync(bool print) => RunAsync(nameof(ShowFunctionBlockReportAsync), async () =>
    {
        if (_session.GenerateFunctionBlockReport() is not { } model)
            return;   // no project open
        string html = ReportHtmlRenderer.RenderFunctionBlocks(model, print);
        if (await _session.WriteReportHtmlAsync(print ? "functionblocks-print" : "functionblocks", html) is not { } path)
            return;
        await _dialogs.OpenExternalUrlAsync(new System.Uri(path).AbsoluteUri);
        StatusText = "Function-block report opened in your browser.";
    });

    private Task ShowReportAsync(bool installation, bool print) => RunAsync(nameof(ShowReportAsync), async () =>
    {
        string html;
        string stem;
        if (installation)
        {
            if (_session.GenerateInstallationReport() is not { } model)
                return;
            html = ReportHtmlRenderer.RenderInstallation(model, print);
            stem = print ? "installation-print" : "installation";
        }
        else
        {
            if (_session.GenerateEndUserReport() is not { } model)
                return;
            html = ReportHtmlRenderer.RenderEndUser(model, print);
            stem = print ? "enduser-print" : "enduser";
        }
        if (await _session.WriteReportHtmlAsync(stem, html) is not { } path)
            return;
        await _dialogs.OpenExternalUrlAsync(new System.Uri(path).AbsoluteUri);
        StatusText = $"{(installation ? "Installation" : "End-user")} report opened in your browser.";
    });

    /// <summary>Adds a Powerup system event to the selected Events group (US-033) — no operand needed.</summary>
    [RelayCommand]
    private Task AddPowerEvent(TreeNodeViewModel? node) => RunAsync(nameof(AddPowerEvent), async () =>
    {
        if (node is { IsEventsContainer: true, ElementId: { } eventsId }
            && _session.BuildAddPowerEvent(eventsId) is { } command)
            await ApplyAsync(command, "Powerup event added to the program.");
    });

    /// <summary>Toggles an output's <i>Save current value</i> power-loss persistence (US-033).</summary>
    [RelayCommand]
    private Task ToggleSaveValue(TreeNodeViewModel? node) => RunAsync(nameof(ToggleSaveValue), async () =>
    {
        if (node is { IsOutputPin: true, ElementId: { } outputId })
            await ApplyAsync(new SetOutputBackup(outputId, !node.IsValueSaved),
                node.IsValueSaved ? "Output value no longer saved on power loss." : "Output value saved on power loss.");
    });

    private Task AddProgramCommandAsync(ElementId actionsId, ElementId variableId, string method, string name, string note) =>
        RunAsync(nameof(AddProgramCommandAsync), async () =>
        {
            await ApplyAsync(new AddProgramCommand(actionsId, variableId, method, name, note), "Command added to the program.");
        });

    /// <summary>The conditions a selected variable can be tested by, offered on a sub-program's Conditions node
    /// (US-029); includes the NOT variant. Rebuilt with the other program menus.</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramConditionMenu { get; } = new();

    /// <summary>The "Case (&lt;variable&gt;)" option offered on a Commands node when an eligible switch variable is
    /// armed (US-031). Rebuilt with the other program menus.</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramCaseMenu { get; } = new();

    /// <summary>The arithmetic operations offered on a Commands node when a numeric target register is armed (US-032):
    /// a per-operator submenu of the block's numeric operands. One operation per command line.</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramArithmeticMenu { get; } = new();

    // Numeric variable types that can be an arithmetic target register or operand (US-032).
    private static readonly string[] NumericTags = { "resource_floating_point", "resource_integer", "resource_counter" };

    /// <summary>Inserts a conditional sub-program (Conditions + true/false command branches) into a Commands
    /// group (US-029).</summary>
    [RelayCommand]
    private Task AddSubProgram(TreeNodeViewModel? node) => RunAsync(nameof(AddSubProgram), async () =>
    {
        if (node is { IsCommandsContainer: true, ElementId: { } id })
            await ApplyAsync(new AddSubProgram(id), "Sub-program inserted.");
    });

    /// <summary>Inserts a nested logic group inside a Conditions group for a compound expression (US-029).</summary>
    [RelayCommand]
    private Task AddLogicGroup(TreeNodeViewModel? node) => RunAsync(nameof(AddLogicGroup), async () =>
    {
        if (node is { IsConditionsContainer: true, ElementId: { } id })
            await ApplyAsync(new AddLogicGroup(id), "Logic group inserted.");
    });

    /// <summary>Combines a Conditions group with OR (<c>&gt;=1</c>) (US-029).</summary>
    [RelayCommand]
    private Task SetConditionsOr(TreeNodeViewModel? node) => ToggleConditionsAsync(node, or: true);

    /// <summary>Combines a Conditions group with AND (<c>&amp;</c>, the default) (US-029).</summary>
    [RelayCommand]
    private Task SetConditionsAnd(TreeNodeViewModel? node) => ToggleConditionsAsync(node, or: false);

    private Task ToggleConditionsAsync(TreeNodeViewModel? node, bool or) => RunAsync(nameof(ToggleConditionsAsync), async () =>
    {
        if (node is { IsConditionsContainer: true, ElementId: { } id })
            await ApplyAsync(new SetConditionsLogic(id, or),
                or ? "Conditions combined with OR (>=1)." : "Conditions combined with AND (&).");
    });

    private Task AddConditionAsync(ElementId conditionsId, ElementId variableId, string method, string name, string note) =>
        RunAsync(nameof(AddConditionAsync), async () =>
        {
            await ApplyAsync(new AddCondition(conditionsId, variableId, method, name, note), "Condition added.");
        });

    private Task AddCaseAsync(ElementId commandsId, ElementId switchVariableId) =>
        RunAsync(nameof(AddCaseAsync), async () =>
        {
            await ApplyAsync(new AddCase(commandsId, switchVariableId), "Case structure inserted.");
        });

    // The numeric variables (decimal/integer/counter) in the programming block — the operand candidates for an
    // arithmetic command line (US-032).
    private IEnumerable<(string Name, ElementId Id)> NumericOperandsInBlock()
    {
        if (_session.Current is not { } project || _programmingBlockId is not { } blockId
            || project.FindById(blockId) is not { } block)
            yield break;
        foreach ((string container, string _) in FunctionBlockSections.All)
        {
            if (block.FindChild(container) is not { } section)
                continue;
            foreach (ProjectElement pin in section.ChildrenOrEmpty())
                if (NumericTags.Contains(pin.Tag) && pin.Id is { } pid)
                    yield return (NameOr(pin, pin.Tag), pid);
        }
    }

    private Task AddArithmeticAsync(ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name) =>
        RunAsync(nameof(AddArithmeticAsync), async () =>
        {
            await ApplyAsync(new AddArithmeticCommand(commandsId, targetId, method, operandId, name), "Arithmetic command added.");
        });

    /// <summary>Adds a case value branch to the selected Case node (US-031): prompts for the criterion value, then
    /// inserts a command group tagged with it (filled by the normal Add-command gesture).</summary>
    [RelayCommand]
    private Task NewCaseValue(TreeNodeViewModel? node) => RunAsync(nameof(NewCaseValue), async () =>
    {
        if (node is not { IsCaseNode: true, ElementId: { } caseId })
            return;
        PropertiesResult? result = await _dialogs.EditPropertiesAsync("New case value", string.Empty, string.Empty);
        if (result is null || string.IsNullOrWhiteSpace(result.Name))
            return;
        if (_session.BuildAddCaseValue(caseId, result.Name.Trim()) is { } command)
            await ApplyAsync(command, $"Case value '{result.Name.Trim()}' added.");
    });

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

        _session.StateChanged += (_, _) => Refresh();
        _session.CatalogChanged += (_, _) => RebuildCatalogMenus();
        _recent.Changed += (_, _) => RefreshRecent();
        BuildProductMenu();
        RefreshRecent();
        Refresh();
    }

    // Rebuilds the product/function-block insertion menus from the current catalog (US-059/US-060: after an import
    // the newly available components appear here).
    private void RebuildCatalogMenus()
    {
        WiredProductsMenu.Clear();
        WirelessProductsMenu.Clear();
        SpecialProductsMenu.Clear();
        BusProductsMenu.Clear();
        FunctionBlocksMenu.Clear();
        BuildProductMenu();
    }

    private void BuildProductMenu()
    {
        var products = _session.GetAvailableProducts();
        AsyncRelayCommand Insert(Ihc.Vis.Products.ProductDefinition def) =>
            new(() => InsertProductAsync(def.ProductIdentifier, def.DisplayName));

        foreach (ProductMenuItemViewModel item in CatalogMenu.BuildWiredProducts(products, Insert))
            WiredProductsMenu.Add(item);

        foreach (ProductMenuItemViewModel item in CatalogMenu.Build(products, "LK IHC Wireless produkter", Insert))
            WirelessProductsMenu.Add(item);

        // Bus and Special are built from their catalog top category (A-11) — the SDK already partitions all 100
        // products into four (Datalinie/Wireless/Bus/Specielle). The SMS Modem lives under Bus, not Special.
        foreach (ProductMenuItemViewModel item in CatalogMenu.Build(products, "Bus Produkter", Insert))
            BusProductsMenu.Add(item);

        foreach (ProductMenuItemViewModel item in CatalogMenu.Build(products, "Specielle produkter", Insert))
            SpecialProductsMenu.Add(item);

        foreach (ProductMenuItemViewModel item in CatalogMenu.BuildFunctionBlocks(
                     _session.GetAvailableFunctionBlocks(),
                     def => new AsyncRelayCommand(() => InsertFunctionBlockAsync(def.MasterType, def.DisplayName))))
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
    [RelayCommand]
    private Task InsertEmptyFunctionBlock() => RunAsync(nameof(InsertEmptyFunctionBlock), async () =>
    {
        if (SelectedNode?.ElementId is not { } localityId)
        {
            StatusText = "Select a locality first, then insert the empty function block.";
            return;
        }
        string localityName = SelectedNode.DisplayName;
        await ApplyAsync(_session.BuildAddEmptyFunctionBlock(localityId),
            $"{ProjectWorkflow.EmptyBlockName} was inserted under {localityName}");
    });

    /// <summary>Inserts a preprogrammed library function block (US-018) under the selected locality — shown in the
    /// Functions pane. Invoked by the leaf commands in <see cref="FunctionBlocksMenu"/>.</summary>
    private Task InsertFunctionBlockAsync(string masterType, string blockName) =>
        RunAsync(nameof(InsertFunctionBlockAsync), async () =>
        {
            if (SelectedNode?.ElementId is not { } localityId)
            {
                StatusText = "Select a locality first, then insert the function block.";
                return;
            }
            string localityName = SelectedNode.DisplayName;
            if (_session.BuildAddFunctionBlock(localityId, masterType) is not { } command)
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
    [RelayCommand]
    private Task InsertLocality() => RunAsync(nameof(InsertLocality), async () =>
    {
        if (await ApplyAsync(new AddLocality(ProjectWorkflow.NewLocalityName),
                $"{ProjectWorkflow.NewLocalityName} was inserted under Localities") is not { } id)
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
        string currentName = fb.GetAttribute("name") ?? "block";
        string currentNote = fb.GetAttribute("note") ?? string.Empty;
        PropertiesResult? meta = await _dialogs.EditPropertiesAsync("Save function block", currentName, currentNote);
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
        if (node?.ElementId is not { } id)
            return;
        string name = node.DisplayName;
        await ApplyAsync(new UnlockFunctionBlock(id), $"Unlocked {name}.");
    });

    /// <summary>Deletes the selected node (US-053), dispatching by type: a link row removes its reciprocal pair
    /// (US-057), a locality uses the US-009 cascade, and any other node (product, block, variable, program element)
    /// uses the general confirm-and-cascade delete. Reachable from the right-click item, Edit ▸ Delete, and the
    /// Delete key (US-044) — all three routes call this command.</summary>
    [RelayCommand]
    private Task Delete(TreeNodeViewModel? node) => RunAsync(nameof(Delete), async () =>
    {
        if (node?.ElementId is not { } id)
            return;
        if (node.IsLinkRow)
        {
            // Removing a link deletes its reciprocal pair, not a subtree (US-057).
            await ApplyAsync(new RemoveLink(id), "Link removed.");
            return;
        }
        string name = node.DisplayName;
        // Preview → confirm → apply (W2-13): the confirmation lives here in the GUI, never below the session.
        ProjectWorkflow.DeleteImpact impact = _session.PreviewDelete(id);
        if (!impact.Deletable)
        {
            await _dialogs.ShowMessageAsync("Cannot delete", "This node cannot be deleted.");
            return;
        }
        bool isLocality = _session.Current?.FindById(id)?.Tag == "group";
        if (impact.NeedsConfirm)
        {
            (string title, string message) = isLocality
                ? ("Delete locality", $"'{name}' contains products. Deleting it also removes those products and the "
                    + "commands and conditions that use them. Delete anyway?")
                : ("Delete", $"'{name}' is referenced by other logic (links and/or commands). Delete it together "
                    + "with those references?");
            if (!await _dialogs.ConfirmAsync(title, message))
                return;   // declined — nothing is deleted
        }
        if (isLocality)
            await ApplyAsync(new DeleteLocality(id), $"Deleted {name}.");   // the US-009 locality worked example
        else
            // impact.NeedsConfirm is the reference-cascade flag PreviewDelete computed for this node.
            await ApplyAsync(new DeleteNode(id, impact.NeedsConfirm), $"Deleted {name}.");   // US-053
    });

    // The structural-editing clipboard (US-054/US-056): the id of the cut/copied node and whether it is a cut
    // (paste = move, US-054) or a copy (paste = duplicate, US-056).
    private ElementId? _clipboardId;
    private bool _clipboardIsCut;

    /// <summary>Cut the selected node (US-054, Ctrl+X): stashes it so a Paste onto a locality moves it there.</summary>
    [RelayCommand]
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
    [RelayCommand]
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
    [RelayCommand]
    private Task Paste(TreeNodeViewModel? node) => RunAsync(nameof(Paste), async () =>
    {
        if (_clipboardId is not { } sourceId || node?.ElementId is not { } targetId)
            return;
        if (_clipboardIsCut)
        {
            if (await ApplyAsync(new MoveNode(sourceId, targetId), "Moved."))
            {
                _clipboardId = null;   // a cut is consumed by its paste
                OnPropertyChanged(nameof(CanPaste));
            }
        }
        else
        {
            await ApplyAsync(new CopyNode(sourceId, targetId), "Pasted a copy.");   // a copy is not consumed
        }
    });

    /// <summary>Moves the selected node one position up among its siblings (US-055) — the non-drag reorder route.</summary>
    [RelayCommand]
    private Task MoveUp(TreeNodeViewModel? node) => ReorderAsync(node, -1);

    /// <summary>Moves the selected node one position down among its siblings (US-055).</summary>
    [RelayCommand]
    private Task MoveDown(TreeNodeViewModel? node) => ReorderAsync(node, +1);

    private Task ReorderAsync(TreeNodeViewModel? node, int delta) => RunAsync(nameof(ReorderAsync), async () =>
    {
        if (node?.ElementId is { } id && _session.BuildReorderNode(id, delta) is { } command)
            await ApplyAsync(command, delta < 0 ? "Moved up." : "Moved down.");
    });

    // ── Wave 9 / A-30 — the shared drag-and-drop dispatcher (§0.3). The legality (CanDropOn), the mutation
    // (PerformDropAsync) and the drop-target highlight (HighlightDropTarget) all live here in the view-model, so they
    // are testable headlessly with no pointer/drag simulation; the code-behind's DragOver/Drop handlers read the
    // dragged id from the DataTransfer and call these. A-30 ships the product→locality move route (the A-P0 slice);
    // A-31…A-34 add the reorder / pin-link / program-build routes onto this same dispatcher and source their per-route
    // grammar from the SDK — do NOT re-encode vendor grammar here.

    private TreeNodeViewModel? _dropTargetNode;

    /// <summary>Whether — and how — the dragged node may drop onto the target: a <see cref="DropVerdict"/> of ok +
    /// effect (Move/Link/None) + a reason when refused. Only the legality every route shares is decided here (a node
    /// cannot drop onto itself); the per-route grammar (container-admissibility, link legality) belongs to the SDK op
    /// the drop calls, so this is the drag-over hint, not the authoritative guard. Avalonia-free so it stays headlessly
    /// testable.</summary>
    public DropVerdict CanDropOn(ElementId dragged, ElementId target)
    {
        if (dragged == target)
            return DropVerdict.Refused("Cannot drop a node onto itself.");
        TreeNodeViewModel? draggedNode = FindNode(InstallationNodes, dragged) ?? FindNode(FunctionNodes, dragged);
        TreeNodeViewModel? targetNode = FindNode(InstallationNodes, target) ?? FindNode(FunctionNodes, target);
        if (draggedNode is null || targetNode is null)
            return DropVerdict.None;
        // Link: dropping one pin onto another creates a link when the SDK's data-flow rule allows it (US-022/US-023).
        // The 15-cell legality + orientation live in the SDK (LinkRoles/CanLink — A-16/A-16amd/F-066); ask, don't
        // re-encode. This precedes reorder so two same-tag pins link (never silently reorder).
        if (draggedNode.IsPin && targetNode.IsPin)
        {
            return _session.CanLinkPins(dragged, target)
                ? DropVerdict.Linking()
                : DropVerdict.Refused("Those two pins can't be linked in that direction.");
        }
        // Program build: dropping a variable/pin onto an events or commands container arms the method popup (US-028).
        // The effect is Link (an authoring connection); PerformDropAsync routes it to the shared Use-in-program menu,
        // not LinkPinsAsync. Gated on the A-27 locked-block rule — no authoring drop into a locked library block.
        if (draggedNode.IsPin && (targetNode.IsEventsContainer || targetNode.IsCommandsContainer))
        {
            return IsProgrammingBlockLocked
                ? DropVerdict.Refused("This block is locked — unlock it to edit its program.")
                : DropVerdict.Linking();
        }
        // Reorder: dropping onto a same-parent, same-tag sibling moves the node to that position (US-055). The SDK owns
        // the "same-tag sibling" rule; the view-model only asks.
        if (_session.CanReorderNode(dragged, target))
            return DropVerdict.Moving();
        // A product can be dragged to re-parent it into another locality (US-054). Ask the SDK whether this exact
        // target is a legal destination — it owns the self/descendant + container-admissibility rules (the same
        // legality Cut/Paste uses); do not re-encode them here. A-33/A-34 add the pin-link / program-build routes.
        if (draggedNode.NodeKind == "product")
        {
            return _session.CanMoveNode(dragged, target)
                ? DropVerdict.Moving()
                : DropVerdict.Refused("That location can't hold this item.");
        }
        return DropVerdict.None;
    }

    /// <summary>Performs a drop, routing by the verdict from <see cref="CanDropOn"/>. A-30 ships the product→locality
    /// move (the same id-preserving re-parent as Cut/Paste, US-054); a refused drop surfaces its reason and mutates
    /// nothing. A-31…A-34 add their routes here.</summary>
    public Task PerformDropAsync(ElementId dragged, ElementId target) => RunAsync(nameof(PerformDropAsync), async () =>
    {
        DropVerdict verdict = CanDropOn(dragged, target);
        if (!verdict.Ok)
        {
            if (verdict.Reason is { } reason)
                StatusText = reason;
            return;
        }
        // Program build (US-028): a variable dropped onto an events/commands container arms the same method popup as
        // Use-in-program — the user then picks the method. Handled before the pin-link route because it shares the Link
        // effect but routes to the program menu, not LinkPinsAsync.
        TreeNodeViewModel? draggedNode = FindNode(InstallationNodes, dragged) ?? FindNode(FunctionNodes, dragged);
        TreeNodeViewModel? targetNode = FindNode(InstallationNodes, target) ?? FindNode(FunctionNodes, target);
        if (draggedNode is { IsPin: true } && targetNode is { } container && (container.IsEventsContainer || container.IsCommandsContainer))
        {
            UseVariableInProgram(draggedNode, container);
            return;
        }
        // Route by the verdict's effect: a pin-link (US-022/US-023), a reorder among same-tag siblings (US-055), or a
        // re-parent (US-054). The effect already encodes which family; within Move, CanReorderNode splits the two.
        if (verdict.Effect == DropEffect.Link)
        {
            await ApplyAsync(new LinkPins(dragged, target), "Linked.");
        }
        else if (_session.CanReorderNode(dragged, target))
        {
            if (_session.BuildReorderNodeToSibling(dragged, target) is { } command)
                await ApplyAsync(command, "Reordered.");
        }
        else
        {
            await ApplyAsync(new MoveNode(dragged, target), "Moved.");
        }
    });

    /// <summary>Highlights (or clears) the current legal drop target so the tree shows where a drop will land (A-30):
    /// sets <see cref="TreeNodeViewModel.IsDropTarget"/> on the node addressed by <paramref name="target"/> and clears
    /// any previous one; pass <c>null</c> to clear. Avalonia-free — the item template binds a row background to
    /// IsDropTarget.</summary>
    public void HighlightDropTarget(ElementId? target)
    {
        TreeNodeViewModel? node = target is { } id ? FindNode(InstallationNodes, id) ?? FindNode(FunctionNodes, id) : null;
        if (ReferenceEquals(node, _dropTargetNode))
            return;
        if (_dropTargetNode is not null)
            _dropTargetNode.IsDropTarget = false;
        _dropTargetNode = node;
        if (_dropTargetNode is not null)
            _dropTargetNode.IsDropTarget = true;
    }

    /// <summary>Opens the Properties dialog for a tree node to rename a locality (US-007). Invoked from the
    /// right-click <i>Properties</i> item (node passed in) and from F2 (the selected node passed in).</summary>
    [RelayCommand]
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
            if (SelectedNode?.ElementId is not { } localityId)
            {
                StatusText = "Select a locality first, then insert the product.";
                return;
            }
            string localityName = SelectedNode.DisplayName;
            if (_session.BuildAddProduct(localityId, productIdentifier) is not { } command)
            {
                await _dialogs.ShowMessageAsync("Insert failed", $"No catalog product with identifier '{productIdentifier}'.");
                return;
            }
            if (_session.WouldExceedModemLimit(productIdentifier))   // at most one modem per project (US-013)
            {
                await _dialogs.ShowMessageAsync("Only one modem",
                    "A project may contain at most one modem. Remove the existing modem before adding another.");
                return;
            }
            // The product lands under the caret and NO dialog opens — the vendor does not auto-open on insert
            // (A-14/F-027, US-011/US-013). The installer opens Properties (F2 / double-click) on demand.
            await ApplyAsync(command, $"Product '{productName}' inserted under {localityName}");
        });

    /// <summary>Makes <paramref name="node"/> the active node — the insert/command target. Used by tests and by
    /// programmatic selection; the live trees feed the active node through their own two-way selection bindings.</summary>
    public void SelectNode(TreeNodeViewModel node) => SelectedNode = node;

    /// <summary>Toggles a "Log …" row's log mark (US-068, the vendor's &amp;Logmærke): the SDK flips its Logning state
    /// between Off and the first logging mode, and the tree re-renders the row's new state.</summary>
    [RelayCommand]
    private Task ToggleLogMark(TreeNodeViewModel? node) => RunAsync(nameof(ToggleLogMark), async () =>
    {
        if (node is { IsLogMarkPin: true, ElementId: { } id })
            await ApplyAsync(new ToggleLogMark(id), $"Toggled the log mark on {node.DisplayName}.");
    });

    /// <summary>Enters programming mode for the selected function block (US-026, F3): the panes switch to the block's
    /// variable sections (left) and its program subtree (right), both headed with the block's name.</summary>
    [RelayCommand]
    private void EnterProgrammingMode(TreeNodeViewModel? node)
    {
        if (node is { IsFunctionBlock: true, ElementId: { } id })
        {
            _programmingBlockId = id;
            IsProgrammingMode = true;
            Refresh();
            NotifyProgrammingAuthoringGates();
            StatusText = node.IsLockedFunctionBlock
                ? "Programming mode (read-only — the block is locked). Press Esc to return."
                : "Programming mode — press Esc to return to configuration.";
        }
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
    [RelayCommand]
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

    /// <summary>Links two pins (US-022/US-023): the <paramref name="source"/> pin is linked onto the
    /// <paramref name="target"/> pin (the target — the pin the link is dropped/completed onto — gets the
    /// "link from" half). Both must be pins; a reciprocal link is created and confirmed.</summary>
    public Task LinkPins(TreeNodeViewModel? source, TreeNodeViewModel? target) =>
        RunAsync(nameof(LinkPins), async () =>
        {
            if (source?.ElementId is not { } fromId || target?.ElementId is not { } toId
                || !source.IsPin || !target.IsPin)
                return;
            await ApplyAsync(new LinkPins(fromId, toId), $"Linked {source.DisplayName} to {target.DisplayName}.");
        });

    /// <summary>The pin from which a link is being drawn — armed by <i>Link from here</i>, consumed by
    /// <i>Link to here</i> (US-022). The two-step gesture is the reliable, testable substitute for pin drag-and-drop.</summary>
    [ObservableProperty] private TreeNodeViewModel? _pendingLinkSource;

    /// <summary>Arms a link from the given pin (US-022) — the next <i>Link to here</i> completes it.</summary>
    [RelayCommand]
    private void StartLink(TreeNodeViewModel? node)
    {
        if (node is { IsPin: true })
        {
            PendingLinkSource = node;
            StatusText = $"Linking from {node.DisplayName} — choose 'Link to here' on the other pin.";
        }
    }

    /// <summary>Completes a link onto the given pin or scenes container (US-022/US-024), pairing it with the armed
    /// <see cref="PendingLinkSource"/>. A scene output onto a scenes container makes a scenario link (opens the value
    /// dialog); otherwise a follow-link between two pins.</summary>
    [RelayCommand]
    private Task LinkToHere(TreeNodeViewModel? node) => RunAsync(nameof(LinkToHere), async () =>
    {
        if (node is not { } || (!node.IsPin && !node.IsSceneTarget))
            return;
        if (PendingLinkSource is not { } source || ReferenceEquals(source, node))
        {
            StatusText = "Choose 'Link from here' on the source pin first.";
            return;
        }
        PendingLinkSource = null;

        if (node.IsSceneTarget && source.ElementId is { } srcId && node.ElementId is { } scenesId
            && _session.Current?.FindById(srcId)?.Tag == "resource_scene")
        {
            await CompleteSceneLinkAsync(srcId, scenesId);
            return;
        }
        await LinkPins(source, node);
    });

    /// <summary>Navigates from a link row to the pin at the opposite end of the link (US-025, F4) — selecting it in
    /// whichever pane holds it.</summary>
    [RelayCommand]
    private void NavigateLinkOpposite(TreeNodeViewModel? node)
    {
        if (node is not { IsLinkRow: true } || node.ElementId is not { } linkId || _session.Current is not { } project
            || project.FindById(linkId) is not { } linkRow
            || !ElementId.TryParse(linkRow.GetAttribute("link"), out ElementId partnerId)
            || project.FindParent(partnerId) is not { Id: { } oppositeId })
        {
            return;
        }
        if (FindNode(InstallationNodes, oppositeId) is { } installationNode)
        {
            ExpandAncestors(InstallationNodes, oppositeId);   // realize the target so the selection sticks (A-6)
            SelectedInstallationNode = installationNode;
        }
        else if (FindNode(FunctionNodes, oppositeId) is { } functionsNode)
        {
            ExpandAncestors(FunctionNodes, oppositeId);
            SelectedFunctionsNode = functionsNode;
        }
        else
        {
            return;
        }
        StatusText = $"Jumped to {SelectedNode?.DisplayName}.";
    }

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

    private async Task CompleteSceneLinkAsync(ElementId sceneOutputId, ElementId scenesId)
    {
        if (_session.Current is not { } project || project.FindById(scenesId) is not { } scenes)
            return;
        // The scene value variant follows the bound output family: airlink_dimming → dimmer, else relay/socket.
        bool isDimmer = ElementId.TryParse(scenes.GetAttribute("scene_resource"), out ElementId boundId)
            && project.FindById(boundId)?.Tag == "airlink_dimming";
        var input = new SceneValueInput("Scene value", isDimmer, On: true, LevelPercent: isDimmer ? 100 : 0, RampMinutes: 0, RampSeconds: 0);

        SceneValueResult? result = await _dialogs.EditSceneValueAsync(input);
        if (result is null)
            return;
        await ApplyAsync(new LinkScene(sceneOutputId, scenesId, result, isDimmer), "Scene link created.");
    }

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
        if (_session.BuildAddEnumVariable(sectionId, result.TypeName, result.TypeName, result.States) is { } command)
            await ApplyAsync(command, $"Enumerator '{result.TypeName}' was inserted under {sectionLabel}");
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
    private string? _lastBuiltViewKey;

    private void Refresh()
    {
        Title = $"{_session.DocumentName} - {Constants.AppName}";
        OnPropertyChanged(nameof(UndoMenuHeader));   // the history may have grown/shrunk — refresh the Edit-menu labels (E14)
        OnPropertyChanged(nameof(RedoMenuHeader));
        if (IsProgrammingMode && _programmingBlockId is { } blockId
            && _session.Current?.FindById(blockId) is { Tag: "functionblock" } block)
        {
            BuildProgrammingTrees(block, preserveExpansion: SameViewAsLastBuild("prog:" + blockId.ToToken()));
            return;
        }
        IsProgrammingMode = false;   // the block is gone (or never set) → configuration mode
        _programmingBlockId = null;
        InstallationPaneHeader = "Installation";
        FunctionsPaneHeader = "Functions";
        bool sameView = SameViewAsLastBuild("config");
        // Reconcile in place when this is an incremental edit on the SAME view whose panes still hold the
        // reconcilers' roots; otherwise (load/undo/redo/mode switch/first build) rebuild through the reconciler,
        // which re-seeds it — with expansion carried across as before (W3-6 keeps the fallback permanent).
        if (sameView && _session.Current is { } current && _session.LastChange is { } changes
            && PaneHoldsRoot(InstallationNodes, _installationReconciler)
            && PaneHoldsRoot(FunctionNodes, _functionsReconciler))
        {
            ReconcilePane(InstallationNodes, _installationReconciler, current, changes);
            ReconcilePane(FunctionNodes, _functionsReconciler, current, changes);
        }
        else
        {
            // The full-rebuild fallback tears down the node instances, so the reconcile path's by-identity survival
            // of the installer's place is lost here — capture selection (which Avalonia's focus + scroll-into-view
            // follow) by id before the rebuild and restore it after, so undo/redo/load land the user back where they
            // were (E14 place restore). Expansion is carried inside RebuildPaneFallback.
            ElementId? selInstallation = SelectedInstallationNode?.ElementId;
            ElementId? selFunctions = SelectedFunctionsNode?.ElementId;
            bool installationActive = IsInstallationPaneActive;
            RebuildPaneFallback(InstallationNodes, _installationReconciler, preserve: sameView);
            RebuildPaneFallback(FunctionNodes, _functionsReconciler, preserve: sameView);
            RestoreSelection(selInstallation, selFunctions, installationActive);
        }
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

    // Whether the pane currently holds exactly the reconciler's root instance — the precondition for an in-place
    // reconcile (a fallback rebuild or a mode switch leaves them out of sync until the next re-seed).
    private static bool PaneHoldsRoot(ObservableCollection<TreeNodeViewModel> pane, ProjectTreeReconciler reconciler) =>
        reconciler.Root is { } root && pane.Count == 1 && ReferenceEquals(pane[0], root);

    // In-place reconcile: the root instance is preserved, so selection/expansion survive by identity. If the
    // reconciler had to fall back internally (a new root), re-point the pane at it.
    private static void ReconcilePane(ObservableCollection<TreeNodeViewModel> pane, ProjectTreeReconciler reconciler,
        Project current, ProjectChangeSet changes)
    {
        TreeNodeViewModel root = reconciler.Reconcile(current, changes);
        if (pane.Count != 1 || !ReferenceEquals(pane[0], root))
        {
            pane.Clear();
            pane.Add(root);
        }
    }

    // Full-rebuild fallback (US-070): rebuild the pane through the reconciler (which re-seeds it with the new root)
    // and carry each surviving node's expand/collapse state across, unless this is a deliberate mode switch
    // (preserve=false), where the fresh defaults ARE the wanted state.
    private void RebuildPaneFallback(ObservableCollection<TreeNodeViewModel> pane, ProjectTreeReconciler reconciler,
        bool preserve)
    {
        Dictionary<ElementId, bool>? expansion = preserve ? SnapshotExpansion(pane) : null;
        TreeNodeViewModel root = _session.Current is { } project
            ? reconciler.Rebuild(project)
            : new TreeNodeViewModel("Localities", LocalityIcon, isExpanded: true) { Kind = TreeNodeKind.LocalitiesRoot };
        pane.Clear();
        pane.Add(root);
        if (expansion is not null)
            RestoreExpansion(pane, expansion);
    }

    // Records the view about to be built and reports whether it is the SAME as the last build — i.e. whether this
    // is an in-place refresh whose expansion should be carried across, rather than a mode switch that opens fresh.
    private bool SameViewAsLastBuild(string key)
    {
        bool same = _lastBuiltViewKey == key;
        _lastBuiltViewKey = key;
        return same;
    }

    // Carries each surviving node's expand/collapse state across a full pane rebuild (US-070): every edit clears and
    // repopulates the pane, so without this the fresh nodes snap back to their build-time defaults and the whole tree
    // collapses on every change. Snapshot is taken BEFORE <paramref name="populate"/> clears the pane, and restored
    // after; skipped (preserve=false) on a mode switch, where the fresh defaults ARE the wanted state.
    private static void RebuildPreservingExpansion(ObservableCollection<TreeNodeViewModel> target, bool preserve, Action populate)
    {
        Dictionary<ElementId, bool>? previous = preserve ? SnapshotExpansion(target) : null;
        populate();
        if (previous is not null)
            RestoreExpansion(target, previous);
    }

    private static Dictionary<ElementId, bool> SnapshotExpansion(IEnumerable<TreeNodeViewModel> nodes)
    {
        var map = new Dictionary<ElementId, bool>();
        CollectExpansion(nodes, map);
        return map;
    }

    // Records the expand/collapse state of every node that CURRENTLY HAS CHILDREN, keyed by element id. The
    // "has children" gate is what lets a node revealing its FIRST child (an empty locality gaining a product,
    // US-006) keep its open-by-default state rather than inherit a stale collapsed one, while a node that was
    // already a parent carries the installer's expansion across the rebuild (US-070).
    private static void CollectExpansion(IEnumerable<TreeNodeViewModel> nodes, Dictionary<ElementId, bool> into)
    {
        foreach (TreeNodeViewModel node in nodes)
        {
            if (node.ElementId is { } id && node.Children.Count > 0)
                into[id] = node.IsExpanded;
            CollectExpansion(node.Children, into);
        }
    }

    private static void RestoreExpansion(IEnumerable<TreeNodeViewModel> nodes, IReadOnlyDictionary<ElementId, bool> previous)
    {
        foreach (TreeNodeViewModel node in nodes)
        {
            if (node.ElementId is { } id && previous.TryGetValue(id, out bool wasExpanded))
                node.IsExpanded = wasExpanded;
            RestoreExpansion(node.Children, previous);
        }
    }

    // Programming mode (US-026): the left pane shows the block's variable sections, the right pane its program
    // subtree (Programs > Program > { Events, Commands }); both headers carry the block's name.
    private void BuildProgrammingTrees(ProjectElement block, bool preserveExpansion)
    {
        string name = NameOr(block, "block");
        InstallationPaneHeader = name;
        FunctionsPaneHeader = name;

        RebuildPreservingExpansion(InstallationNodes, preserveExpansion, () =>
        {
            InstallationNodes.Clear();
            // block → Input/Output/Settings/Internal variables (row projection extracted to ProjectTreeProjector, W3-1)
            InstallationNodes.Add(new ProjectTreeProjector(_session.Current!).BuildFunctionBlockNode(block, name, programmingMode: true));
        });
        RebuildPreservingExpansion(FunctionNodes, preserveExpansion, () =>
        {
            FunctionNodes.Clear();
            // block → Programs → Program → Events/Commands (row projection extracted to ProjectTreeProjector, W3-1)
            FunctionNodes.Add(new ProjectTreeProjector(_session.Current!).BuildBlockProgramsNode(block, name));
        });
    }

    // fablerefac W1-6: read element attributes through the SDK read surface (project.View) instead of raw
    // GetAttribute. The projected element always belongs to the open project, so the schema context is _session.Current.
    private ElementView View(ProjectElement element) => _session.Current!.View(element);

    // The element's effective name, or the fallback when it is empty — preserving the old
    // `GetAttribute("name") ?? fallback` (a canonicalized project omits an empty name, so it reads back as "").
    private string NameOr(ProjectElement element, string fallback) =>
        View(element).Name is { Length: > 0 } name ? name : fallback;

    private void RefreshRecent()
    {
        RecentProjects.Clear();
        foreach (string path in _recent.Items)
            RecentProjects.Add(new RecentProjectViewModel(path, OpenRecentCommand));
    }

    private string BuildSettingsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Application: {Constants.AppName} {VersionInfo.GetAppVersionStr()}");
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
