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
using Ihc.Vis.Addressing;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Programs;
using Ihc.Vis.Projects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// The shell view-model: the window title, the two locality tree panes, the status-bar hint, the toolbar/
/// status-bar/theme view state, and the <i>File</i>/<i>View</i>/<i>Help</i> commands. A thin coordinator over
/// <see cref="ProjectSession"/> (all project logic) and <see cref="IDialogService"/>/<see cref="IThemeService"/>
/// (all Avalonia); free of Avalonia types so it is testable headlessly.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private const string LocalityIcon = "/Assets/locality.svg";

    private readonly ProjectSession _session;
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
    private TreeNodeViewModel? _selectedNode;

    /// <summary>Whether the active selection lives in the <i>Installation</i> pane (vs the <i>Functions</i> pane). The
    /// shared node context menu uses this to offer product insertion only where products belong.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInsertProduct))]
    private bool _isInstallationPaneActive;

    /// <summary>Context-menu gate: <i>Insert product</i> is offered only on an addressable node in the Installation
    /// pane — the Functions pane hosts function blocks, and the Localities root hosts localities (US-010).</summary>
    public bool CanInsertProduct => IsInstallationPaneActive && SelectedNode?.CanEditProperties == true;

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

    /// <summary>The special-products insertion submenu (US-013) — the modem(s); subject to the one-modem rule.</summary>
    public ObservableCollection<ProductMenuItemViewModel> SpecialProductsMenu { get; } = new();

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
                if (await _session.AddVariableAsync(sectionId, tag, label) is not null)
                    StatusText = $"{label} was inserted under {sectionLabel}";
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
            if (_session.Current?.FindById(varId)?.Tag is { } varTag && ProjectSession.EligibleCaseVariableTags.Contains(varTag))
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
            if (await _session.AddProgramEventAsync(eventsId, variableId, method, name, note))
                StatusText = "Event added to the program.";
        });

    /// <summary>Edit ▸ Undo (US-052, Ctrl+Z): reverses the last project-mutating edit; a no-op when there is nothing
    /// to undo. Refreshes both panes via the session's StateChanged.</summary>
    [RelayCommand]
    private Task Undo() => RunAsync(nameof(Undo), async () =>
    {
        StatusText = await _session.UndoAsync() ? "Undid the last change." : "Nothing to undo.";
    });

    /// <summary>Edit ▸ Redo (US-052, Ctrl+Y): re-applies the last undone edit; a no-op when the redo history is empty.</summary>
    [RelayCommand]
    private Task Redo() => RunAsync(nameof(Redo), async () =>
    {
        StatusText = await _session.RedoAsync() ? "Redid the change." : "Nothing to redo.";
    });

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
        if (!IsProgrammingMode || _programmingBlockId is not { } blockId
            || _session.Current?.FindById(blockId)?.FindChild(container) is not { Id: { } sectionId })
        {
            StatusText = "Enter a block's programming mode to insert an input or output.";
            return;
        }
        if (await _session.AddVariableAsync(sectionId, tag, label) is not null)
            StatusText = $"{label} inserted into the block.";
    });

    /// <summary>Opens the Project information dialog (US-039) prefilled from the project, and applies edits.</summary>
    [RelayCommand]
    private Task ProjectInfo() => RunAsync(nameof(ProjectInfo), async () =>
    {
        ProjectInfoData? result = await _dialogs.EditProjectInfoAsync(_session.GetProjectInfo());
        if (result is null)
            return;
        if (await _session.UpdateProjectInfoAsync(result))
            StatusText = "Project information updated.";
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
        if (node is { IsEventsContainer: true, ElementId: { } eventsId } && await _session.AddPowerEventAsync(eventsId))
            StatusText = "Powerup event added to the program.";
    });

    /// <summary>Toggles an output's <i>Save current value</i> power-loss persistence (US-033).</summary>
    [RelayCommand]
    private Task ToggleSaveValue(TreeNodeViewModel? node) => RunAsync(nameof(ToggleSaveValue), async () =>
    {
        if (node is { IsOutputPin: true, ElementId: { } outputId } && await _session.SetOutputBackupAsync(outputId, !node.IsValueSaved))
            StatusText = node.IsValueSaved ? "Output value no longer saved on power loss." : "Output value saved on power loss.";
    });

    private Task AddProgramCommandAsync(ElementId actionsId, ElementId variableId, string method, string name, string note) =>
        RunAsync(nameof(AddProgramCommandAsync), async () =>
        {
            if (await _session.AddProgramCommandAsync(actionsId, variableId, method, name, note))
                StatusText = "Command added to the program.";
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
        if (node is { IsCommandsContainer: true, ElementId: { } id } && await _session.AddSubProgramAsync(id))
            StatusText = "Sub-program inserted.";
    });

    /// <summary>Inserts a nested logic group inside a Conditions group for a compound expression (US-029).</summary>
    [RelayCommand]
    private Task AddLogicGroup(TreeNodeViewModel? node) => RunAsync(nameof(AddLogicGroup), async () =>
    {
        if (node is { IsConditionsContainer: true, ElementId: { } id } && await _session.AddLogicGroupAsync(id))
            StatusText = "Logic group inserted.";
    });

    /// <summary>Combines a Conditions group with OR (<c>&gt;=1</c>) (US-029).</summary>
    [RelayCommand]
    private Task SetConditionsOr(TreeNodeViewModel? node) => ToggleConditionsAsync(node, or: true);

    /// <summary>Combines a Conditions group with AND (<c>&amp;</c>, the default) (US-029).</summary>
    [RelayCommand]
    private Task SetConditionsAnd(TreeNodeViewModel? node) => ToggleConditionsAsync(node, or: false);

    private Task ToggleConditionsAsync(TreeNodeViewModel? node, bool or) => RunAsync(nameof(ToggleConditionsAsync), async () =>
    {
        if (node is { IsConditionsContainer: true, ElementId: { } id } && await _session.SetConditionsLogicAsync(id, or))
            StatusText = or ? "Conditions combined with OR (>=1)." : "Conditions combined with AND (&).";
    });

    private Task AddConditionAsync(ElementId conditionsId, ElementId variableId, string method, string name, string note) =>
        RunAsync(nameof(AddConditionAsync), async () =>
        {
            if (await _session.AddConditionAsync(conditionsId, variableId, method, name, note))
                StatusText = "Condition added.";
        });

    private Task AddCaseAsync(ElementId commandsId, ElementId switchVariableId) =>
        RunAsync(nameof(AddCaseAsync), async () =>
        {
            if (await _session.AddCaseAsync(commandsId, switchVariableId))
                StatusText = "Case structure inserted.";
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
                    yield return (pin.GetAttribute("name") ?? pin.Tag, pid);
        }
    }

    private Task AddArithmeticAsync(ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name) =>
        RunAsync(nameof(AddArithmeticAsync), async () =>
        {
            if (await _session.AddArithmeticCommandAsync(commandsId, targetId, method, operandId, name))
                StatusText = "Arithmetic command added.";
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
        if (await _session.AddCaseValueAsync(caseId, result.Name.Trim()))
            StatusText = $"Case value '{result.Name.Trim()}' added.";
    });

    /// <summary>Raised by the <i>Exit</i> command to ask the window to close (the close then runs the save prompt).</summary>
    public event EventHandler? CloseRequested;

    public MainWindowViewModel(
        ProjectSession session,
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

        var modems = products.Where(p => ProductClassifier.IsModem(p.Body.Tag));
        foreach (ProductMenuItemViewModel item in CatalogMenu.BuildLeaves(modems, Insert))
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
        if (await _session.AddEmptyFunctionBlockAsync(localityId) is not null)
            StatusText = $"{ProjectSession.EmptyBlockName} was inserted under {localityName}";
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
            if (await _session.AddFunctionBlockAsync(localityId, masterType) is not null)
                StatusText = $"Function block '{blockName}' has been inserted under {localityName}";
        });

    /// <summary>Parameterless constructor for the XAML designer / template smoke test only.</summary>
    public MainWindowViewModel()
        : this(CreateDesignSession(), new NullDialogService(), new RecentProjectsStore(System.IO.Path.GetTempFileName()), new NullThemeService())
    {
    }

    public Task InitializeAsync() => _session.StartAsync();

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
        ElementId? newId = await _session.AddLocalityAsync();
        if (newId is not { } id)
            return;
        StatusText = $"{ProjectSession.NewLocalityName} was inserted under Localities";
        // Refresh already rebuilt the trees (StateChanged); highlight the new locality in the Installation pane
        // (which sets it as the active node).
        SelectedInstallationNode = FindNode(InstallationNodes, id);
    });

    /// <summary>Saves a placed function block to a reusable <c>.ifb</c> file (US-021). Invoked from the right-click
    /// <i>Save block…</i> item and Ctrl+G.</summary>
    [RelayCommand]
    private Task SaveFunctionBlock(TreeNodeViewModel? node) => RunAsync(nameof(SaveFunctionBlock), async () =>
    {
        if (node?.ElementId is not { } id || _session.Current?.FindById(id) is not { } fb || fb.Tag != "functionblock")
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
        if (await _session.UnlockFunctionBlockAsync(id))
            StatusText = $"Unlocked {name}.";
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
            if (await _session.RemoveLinkAsync(id))
                StatusText = "Link removed.";
            return;
        }
        string name = node.DisplayName;
        bool deleted = _session.Current?.FindById(id)?.Tag == "group"
            ? await _session.DeleteLocalityAsync(id)     // the US-009 locality worked example
            : await _session.DeleteNodeAsync(id);        // any other project node (US-053)
        if (deleted)
            StatusText = $"Deleted {name}.";
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
            if (await _session.MoveNodeAsync(sourceId, targetId))
            {
                StatusText = "Moved.";
                _clipboardId = null;   // a cut is consumed by its paste
            }
        }
        else if (await _session.CopyNodeAsync(sourceId, targetId) is not null)
        {
            StatusText = "Pasted a copy.";   // a copy is not consumed — it can be pasted again
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
        if (node?.ElementId is { } id && await _session.ReorderNodeAsync(id, delta))
            StatusText = delta < 0 ? "Moved up." : "Moved down.";
    });

    /// <summary>Opens the Properties dialog for a tree node to rename a locality (US-007). Invoked from the
    /// right-click <i>Properties</i> item (node passed in) and from F2 (the selected node passed in).</summary>
    [RelayCommand]
    private Task Properties(TreeNodeViewModel? node) => RunAsync(nameof(Properties), () => OpenPropertiesAsync(node));

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
            ElementId? productId = await _session.AddProductAsync(localityId, productIdentifier);
            if (productId is not { } pid)
                return;
            StatusText = $"Product '{productName}' inserted under {localityName}";
            await OpenPropertiesForIdAsync(pid);   // the properties dialog opens automatically on insert (US-011/US-013)
        });

    /// <summary>Makes <paramref name="node"/> the active node — the insert/command target. Used by tests and by
    /// programmatic selection; the live trees feed the active node through their own two-way selection bindings.</summary>
    public void SelectNode(TreeNodeViewModel node) => SelectedNode = node;

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
            StatusText = "Programming mode — press Esc to return to configuration.";
        }
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
            if (await _session.LinkPinsAsync(fromId, toId))
                StatusText = $"Linked {source.DisplayName} to {target.DisplayName}.";
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
            SelectedInstallationNode = installationNode;
        else if (FindNode(FunctionNodes, oppositeId) is { } functionsNode)
            SelectedFunctionsNode = functionsNode;
        else
            return;
        StatusText = $"Jumped to {SelectedNode?.DisplayName}.";
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
        if (await _session.LinkSceneAsync(sceneOutputId, scenesId, result, isDimmer))
            StatusText = "Scene link created.";
    }

    // The Properties route (right-click / F2) dispatches by element type: a modem opens the modem dialog (US-013),
    // any other product the documentation dialog (US-011), an I/O pin the addressing dialog (US-012), a locality the
    // rename dialog (US-007).
    private Task OpenPropertiesAsync(TreeNodeViewModel? node) =>
        node?.ElementId is { } id ? OpenPropertiesForIdAsync(id) : Task.CompletedTask;

    private async Task OpenPropertiesForIdAsync(ElementId id)
    {
        if (_session.Current is not { } project || project.FindById(id) is not { } element)
            return;
        if (ProductClassifier.IsModem(element.Tag))
            await OpenModemPropertiesAsync(id);
        else if (ProductClassifier.IsProduct(element.Tag))
            await OpenProductPropertiesAsync(id);
        else if (element.Tag is "dataline_input" or "dataline_output")
            await OpenPinPropertiesAsync(id, element);
        else if (element.Tag is "scene_relay" or "scene_dimmer")
            await OpenSceneValuePropertiesAsync(id, element);   // edit a scenario link's value (US-058)
        else if (element.Tag == "resource_enum")
            await OpenEnumPropertiesAsync(id);   // edit the enum type's states (US-030)
        else if (element.Tag is "group" or "functionblock")
            // A function block renames through the same Name/Note dialog as a locality (US-007/US-019).
            await OpenLocalityPropertiesAsync(id, element.GetAttribute("name") ?? string.Empty);
    }

    private async Task OpenSceneValuePropertiesAsync(ElementId memberId, ProjectElement member)
    {
        if (!SceneValue.TryParse(member, out SceneValue sv))
            return;
        bool isDimmer = sv.Kind == SceneValueKind.Dimmer;
        int ms = (int)sv.RampTime.TotalMilliseconds;
        var input = new SceneValueInput("Scene value", isDimmer, sv.On, sv.LevelPercent, ms / 60000, ms / 1000 % 60);

        SceneValueResult? result = await _dialogs.EditSceneValueAsync(input);
        if (result is null)
            return;
        if (await _session.UpdateSceneValueAsync(memberId, result))
            StatusText = "Scene value updated.";
    }

    // Reads an enum variable's type name and ordered state names for the Edit dialog (US-030); null if not an enum.
    private (string Name, List<string> States)? ReadEnumInfo(ElementId enumVariableId)
    {
        if (_session.Current is not { } project || project.FindById(enumVariableId) is not { Tag: "resource_enum" } variable
            || !ElementId.TryParse(variable.GetAttribute("typedef"), out ElementId defId)
            || project.FindById(defId) is not { } def)
        {
            return null;
        }
        var states = def.ChildrenOrEmpty().Where(c => c.Tag == "enum_value")
            .Select(c => c.GetAttribute("name") ?? string.Empty).ToList();
        return (def.GetAttribute("name") ?? string.Empty, states);
    }

    private async Task OpenEnumPropertiesAsync(ElementId enumVariableId)
    {
        if (ReadEnumInfo(enumVariableId) is not { } info)
            return;
        EnumDefinitionResult? result = await _dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput($"Edit {info.Name}", info.Name, info.States, IsNew: false));
        if (result is null)
            return;
        if (await _session.UpdateEnumStatesAsync(enumVariableId, result.States))
            StatusText = $"Enumerator '{info.Name}' updated.";
    }

    private Task InsertEnumAsync(ElementId sectionId, string sectionLabel) => RunAsync(nameof(InsertEnumAsync), async () =>
    {
        EnumDefinitionResult? result = await _dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput("New enumerator", string.Empty, System.Array.Empty<string>(), IsNew: true));
        if (result is null || string.IsNullOrWhiteSpace(result.TypeName))
            return;
        if (await _session.AddEnumVariableAsync(sectionId, result.TypeName, result.TypeName, result.States) is not null)
            StatusText = $"Enumerator '{result.TypeName}' was inserted under {sectionLabel}";
    });

    private async Task OpenModemPropertiesAsync(ElementId modemId)
    {
        if (_session.Current is not { } project || project.FindById(modemId) is not { } modem)
            return;
        var localities = new List<LocalityChoice>();
        foreach (ProjectElement group in project.Groups)
        {
            if (group.Id is { } gid)
                localities.Add(new LocalityChoice(gid.ToToken(), group.GetAttribute("name") ?? string.Empty));
        }
        string currentLocalityId = project.FindParent(modemId)?.Id?.ToToken() ?? string.Empty;
        var phones = new List<string>();
        for (int slot = 1; slot <= 4; slot++)
        {
            string s = slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ProjectElement? pn = modem.DescendantsAndSelf()
                .FirstOrDefault(e => e.Tag == "sms_modem_phonenumber" && e.GetAttribute("address") == s);
            phones.Add(pn?.GetAttribute("phonenumber") ?? string.Empty);
        }
        string pin = modem.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "sms_modem_pincode")?.GetAttribute("value") ?? string.Empty;
        if (pin == "0")
            pin = string.Empty;   // the DTD default reads as blank in the dialog

        var input = new ModemPropertiesInput(
            "SMS modem properties",
            modem.GetAttribute("name") ?? string.Empty,
            modem.GetAttribute("note") ?? string.Empty,
            modem.GetAttribute("documentation_tag") ?? string.Empty,
            modem.GetAttribute("cablecolour_0V") ?? string.Empty,
            modem.GetAttribute("cablecolour_24V") ?? string.Empty,
            modem.GetAttribute("cablecolour_RS485Minus") ?? string.Empty,
            modem.GetAttribute("cablecolour_RS485Plus") ?? string.Empty,
            pin, phones, localities, currentLocalityId);

        ModemPropertiesResult? result = await _dialogs.EditModemPropertiesAsync(input);
        if (result is null)
            return;
        if (await _session.UpdateModemAsync(modemId, result))
            StatusText = $"Updated {result.Name}.";
    }

    private async Task OpenPinPropertiesAsync(ElementId pinId, ProjectElement pin)
    {
        bool isOutput = pin.Tag == "dataline_output";
        int dataLine = 1, terminal = 0;
        if (DatalineAddress.TryParse(pin.GetAttribute("address_dataline"), isOutput, out DatalineAddress addr))
            (dataLine, terminal) = (addr.DataLine, addr.Terminal);
        var input = new PinPropertiesInput(
            $"{(isOutput ? "Output" : "Input")} '{pin.GetAttribute("name")}'",
            isOutput, dataLine, terminal,
            pin.GetAttribute("cable_colour") ?? string.Empty,
            pin.GetAttribute("note") ?? string.Empty,
            (pin.GetAttribute("inivalue") ?? "off") == "on",
            InUseTerminals(isOutput, pinId));

        PinPropertiesResult? result = await _dialogs.EditPinPropertiesAsync(input);
        if (result is null)
            return;   // cancelled — the pin keeps its addressing
        StatusText = await _session.UpdatePinAsync(pinId, result)
            ? $"Addressed {pin.GetAttribute("name")} to data line {result.DataLine}, terminal {result.Terminal}."
            : $"Data line {result.DataLine}, terminal {result.Terminal} is not a valid address.";
    }

    // The line.terminal addresses already used by other pins of the same direction (US-012 in-use indication).
    private IReadOnlyList<string> InUseTerminals(bool isOutput, ElementId except)
    {
        var used = new List<string>();
        if (_session.Current is not { } project)
            return used;
        string tag = isOutput ? "dataline_output" : "dataline_input";
        foreach (ProjectElement element in project.Root.DescendantsAndSelf())
        {
            if (element.Tag == tag && element.Id is { } eid && eid != except
                && DatalineAddress.TryParse(element.GetAttribute("address_dataline"), isOutput, out DatalineAddress a))
            {
                used.Add($"{a.DataLine}.{a.Terminal}");
            }
        }
        return used;
    }

    private async Task OpenLocalityPropertiesAsync(ElementId id, string currentName)
    {
        string currentNote = _session.Current?.FindById(id)?.GetAttribute("note") ?? string.Empty;
        PropertiesResult? result = await _dialogs.EditPropertiesAsync($"Edit {currentName} properties", currentName, currentNote);
        if (result is null)
            return;   // cancelled — the locality keeps its original name and note
        if (await _session.RenameLocalityAsync(id, result.Name, result.Note))
            StatusText = $"Renamed to {result.Name}.";
    }

    private async Task OpenProductPropertiesAsync(ElementId productId)
    {
        if (_session.Current is not { } project || project.FindById(productId) is not { } product)
            return;
        var localities = new List<LocalityChoice>();
        foreach (ProjectElement group in project.Groups)
        {
            if (group.Id is { } gid)
                localities.Add(new LocalityChoice(gid.ToToken(), group.GetAttribute("name") ?? string.Empty));
        }
        string currentLocalityId = project.FindParent(productId)?.Id?.ToToken() ?? string.Empty;
        var input = new ProductPropertiesInput(
            "Product properties",
            product.GetAttribute("name") ?? string.Empty,
            product.GetAttribute("note") ?? string.Empty,
            product.GetAttribute("cabletype") ?? string.Empty,
            product.GetAttribute("cablenumber") ?? string.Empty,
            product.GetAttribute("documentation_tag") ?? string.Empty,
            product.GetAttribute("power_group") ?? string.Empty,
            localities, currentLocalityId, ProductClassifier.IsWireless(product.Tag), IsWirelessDimmer(product));

        ProductPropertiesResult? result = await _dialogs.EditProductPropertiesAsync(input);
        if (result is null)
            return;   // cancelled — the product keeps its documentation
        if (await _session.UpdateProductAsync(productId, result))
            StatusText = $"Updated {result.Name}.";
        if (result.OpenAdvanced)
            await OpenAdvancedDimmerAsync(productId);   // Properties ▸ Advanced (US-015)
    }

    private static bool IsWirelessDimmer(ProjectElement product) =>
        ProductClassifier.IsWireless(product.Tag) && product.DescendantsAndSelf().Any(e => e.Tag == "dimmer_settings");

    private async Task OpenAdvancedDimmerAsync(ElementId productId)
    {
        if (_session.Current is not { } project || project.FindById(productId) is not { } product)
            return;
        int Read(string tag, int fallback)
        {
            ProjectElement? el = product.DescendantsAndSelf().FirstOrDefault(e => e.Tag == tag);
            return el is not null && int.TryParse(el.GetAttribute("value"), out int v) && v > 0 ? v : fallback;
        }
        string loadMode = product.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "dimmer_setting_load_mode")
            ?.GetAttribute("value") ?? "auto";
        var input = new AdvancedDimmerInput(
            Read("dimmer_setting_fade_rate_up", 700),
            Read("dimmer_setting_fade_rate_down", 700),
            Read("dimmer_setting_dimming_rate", 2),
            Read("dimmer_setting_minimum_value", 0),
            Read("dimmer_setting_maximum_value", 100),
            loadMode);

        AdvancedDimmerResult? result = await _dialogs.EditAdvancedDimmerAsync(input);
        if (result is null)
            return;
        if (await _session.UpdateDimmerSettingsAsync(productId, result))
            StatusText = "Updated dimmer settings.";
    }

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

    private void Refresh()
    {
        Title = $"{_session.DocumentName} - {Constants.AppName}";
        if (IsProgrammingMode && _programmingBlockId is { } blockId
            && _session.Current?.FindById(blockId) is { Tag: "functionblock" } block)
        {
            BuildProgrammingTrees(block);
            return;
        }
        IsProgrammingMode = false;   // the block is gone (or never set) → configuration mode
        _programmingBlockId = null;
        InstallationPaneHeader = "Installation";
        FunctionsPaneHeader = "Functions";
        BuildTree(InstallationNodes, functions: false);
        BuildTree(FunctionNodes, functions: true);
    }

    // Programming mode (US-026): the left pane shows the block's variable sections, the right pane its program
    // subtree (Programs > Program > { Events, Commands }); both headers carry the block's name.
    private void BuildProgrammingTrees(ProjectElement block)
    {
        string name = block.GetAttribute("name") ?? "block";
        InstallationPaneHeader = name;
        FunctionsPaneHeader = name;

        InstallationNodes.Clear();
        InstallationNodes.Add(BuildFunctionBlockNode(block, name));   // block → Input/Output/Settings/Internal variables

        FunctionNodes.Clear();
        FunctionNodes.Add(BuildBlockProgramsNode(block, name));       // block → Programs → Program → Events/Commands
    }

    private TreeNodeViewModel BuildBlockProgramsNode(ProjectElement block, string name)
    {
        bool locked = (block.GetAttribute("locked") ?? "no") == "yes";
        var blockNode = new TreeNodeViewModel(name, locked ? "/Assets/fb-lk.svg" : "/Assets/fb-editable.svg",
            isExpanded: true, elementId: block.Id);
        ProjectElement? programs = block.FindChild("programs");
        var programsNode = new TreeNodeViewModel("Programs", NodeIcons.For("programs", null),
            isExpanded: true, elementId: programs?.Id);
        if (programs is not null)
        {
            foreach (ProjectElement program in programs.ChildrenOrEmpty().Where(p => p.Tag is "program_simple" or "program_sub"))
            {
                var programNode = new TreeNodeViewModel(program.GetAttribute("name") ?? "Program",
                    NodeIcons.For("program_simple", null), isExpanded: true, elementId: program.Id);
                if (program.FindChild("events") is { } events)
                {
                    var eventsNode = new TreeNodeViewModel("Events", NodeIcons.For("events", null),
                        isExpanded: true, elementId: events.Id) { IsEventsContainer = true };
                    foreach (ProjectElement ev in events.ChildrenOrEmpty().Where(e => e.Tag is "event" or "event_power"))
                        eventsNode.Children.Add(new TreeNodeViewModel(EventCommandLabel(ev),
                            NodeIcons.For(ev.Tag, null), elementId: ev.Id));
                    programNode.Children.Add(eventsNode);
                }
                if (program.FindChild("actions") is { } actions)
                {
                    var commandsNode = new TreeNodeViewModel("Commands", NodeIcons.For("actions", null),
                        isExpanded: true, elementId: actions.Id) { IsCommandsContainer = true };
                    RenderActionsInto(commandsNode, actions);
                    programNode.Children.Add(commandsNode);
                }
                programsNode.Children.Add(programNode);
            }
        }
        blockNode.Children.Add(programsNode);
        return blockNode;
    }

    // Renders an actions container's children (US-028/US-029): command leaves, conditional sub-programs, and case
    // switches (case bodies deferred to US-031).
    private void RenderActionsInto(TreeNodeViewModel commandsNode, ProjectElement actions)
    {
        foreach (ProjectElement child in actions.ChildrenOrEmpty())
        {
            switch (child.Tag)
            {
                case "action":
                    commandsNode.Children.Add(new TreeNodeViewModel(EventCommandLabel(child),
                        NodeIcons.For("action", null), elementId: child.Id));
                    break;
                case "program_sub":
                    commandsNode.Children.Add(BuildSubProgramNode(child));
                    break;
                case "program_case":
                    commandsNode.Children.Add(BuildCaseNode(child));
                    break;
            }
        }
    }

    // Renders a conditional sub-program (US-029): its Conditions group and true/false command branches.
    private TreeNodeViewModel BuildSubProgramNode(ProjectElement sub)
    {
        var node = new TreeNodeViewModel("Sub-program", NodeIcons.For("program_sub", null),
            isExpanded: true, elementId: sub.Id);
        if (sub.FindChild("conditions") is { } conditions)
            node.Children.Add(BuildConditionsNode(conditions));
        foreach (ProjectElement branch in sub.ChildrenOrEmpty().Where(a => a.Tag == "actions"))
        {
            bool isTrue = (branch.GetAttribute("type") ?? "") == "_0x1";
            var branchNode = new TreeNodeViewModel(
                isTrue ? "Commands when conditions true" : "Commands when conditions false",
                NodeIcons.For("actions", null), isExpanded: true, elementId: branch.Id) { IsCommandsContainer = true };
            RenderActionsInto(branchNode, branch);
            node.Children.Add(branchNode);
        }
        return node;
    }

    // Renders a conditions group (US-029): its condition rows and nested logic groups; the AND/OR combination shows in
    // the icon (& vs >=1) and a label suffix.
    private TreeNodeViewModel BuildConditionsNode(ProjectElement conditions, bool nested = false)
    {
        bool or = (conditions.GetAttribute("type") ?? "and") == "or";
        string label = $"{(nested ? "Logic group" : "Conditions")} ({(or ? ">=1" : "&")})";
        var node = new TreeNodeViewModel(label, NodeIcons.For(or ? "conditions-or" : "conditions", null),
            isExpanded: true, elementId: conditions.Id) { IsConditionsContainer = true, IsOrGroup = or };
        foreach (ProjectElement child in conditions.ChildrenOrEmpty())
        {
            if (child.Tag == "condition")
                node.Children.Add(new TreeNodeViewModel(EventCommandLabel(child),
                    NodeIcons.For("condition", null), elementId: child.Id));
            else if (child.Tag == "conditions")
                node.Children.Add(BuildConditionsNode(child, nested: true));
        }
        return node;
    }

    // Renders a case switch (US-031): "Case (<switch variable>)" over its value branches and the default Else branch.
    // Every branch is a command container, so commands can be added to it with the normal gesture.
    private TreeNodeViewModel BuildCaseNode(ProjectElement kase)
    {
        string switchName = ResolveOperandName(kase.GetAttribute("link"));
        var node = new TreeNodeViewModel($"Case ({switchName})", NodeIcons.For("program_case", null),
            isExpanded: true, elementId: kase.Id) { IsCaseNode = true };
        foreach (ProjectElement child in kase.ChildrenOrEmpty())
        {
            if (child.Tag == "case_action")
            {
                var valueNode = new TreeNodeViewModel(child.GetAttribute("name") ?? "value",
                    NodeIcons.For("case_action", null), isExpanded: true, elementId: child.Id) { IsCommandsContainer = true };
                RenderActionsInto(valueNode, child);   // the embedded criterion operand is skipped (not a command)
                node.Children.Add(valueNode);
            }
            else if (child.Tag == "actions")
            {
                var elseNode = new TreeNodeViewModel("Else", NodeIcons.For("actions", null),
                    isExpanded: true, elementId: child.Id) { IsCommandsContainer = true };
                RenderActionsInto(elseNode, child);
                node.Children.Add(elseNode);
            }
        }
        return node;
    }

    // Renders a program event/action row (US-028): the stored %P/%S template resolved to its operands' live names.
    private string EventCommandLabel(ProjectElement leaf)
    {
        string name = leaf.GetAttribute("name") ?? leaf.Tag;
        return name.Replace("%P", ResolveOperandName(leaf.GetAttribute("link1")))
                   .Replace("%S", ResolveOperandName(leaf.GetAttribute("link2")));
    }

    private string ResolveOperandName(string? token) =>
        _session.Current is { } project && ElementId.TryParse(token, out ElementId id)
            && project.FindById(id) is { } operand
                ? operand.GetAttribute("name") ?? string.Empty
                : string.Empty;

    // Both panes share the Localities skeleton; the Installation pane nests each locality's products (with their
    // pins), the Functions pane its function blocks (US-006/US-010).
    private void BuildTree(ObservableCollection<TreeNodeViewModel> target, bool functions)
    {
        target.Clear();
        var root = new TreeNodeViewModel("Localities", LocalityIcon, isExpanded: true, isLocalitiesRoot: true);
        if (_session.Current is { } project)
        {
            foreach (ProjectElement group in project.Groups)
            {
                string name = group.GetAttribute("name") ?? "(unnamed)";
                var components = new List<ProjectElement>();
                foreach (ProjectElement child in group.ChildrenOrEmpty())
                {
                    if ((child.Tag == "functionblock") == functions)
                        components.Add(child);
                }
                // A locality that holds components opens by default so they are visible (US-006 container reveal).
                var locality = new TreeNodeViewModel(name, LocalityIcon, isExpanded: components.Count > 0,
                    isBold: true, elementId: group.Id) { Tooltip = BuildTooltip(group) };
                foreach (ProjectElement child in components)
                    locality.Children.Add(BuildComponentNode(child));
                root.Children.Add(locality);
            }
        }
        target.Add(root);
    }

    // A product / function block node. A product flattens its resource (pin) children (structural containers are
    // omitted); a function block shows its four variable sections (Input/Output/Settings/Internal variables), each
    // holding its typed pins (US-018/US-019).
    private TreeNodeViewModel BuildComponentNode(ProjectElement component)
    {
        string name = component.GetAttribute("name") ?? component.Tag;
        if (component.Tag == "functionblock")
            return BuildFunctionBlockNode(component, name);

        bool unlinked = ProductClassifier.IsUnlinkedWireless(component.Tag, component.GetAttribute("serialnumber"));
        var node = new TreeNodeViewModel(name, NodeIcons.For(component.Tag, component.GetAttribute("icon")),
            elementId: component.Id, isUnlinked: unlinked) { Tooltip = BuildTooltip(component) };
        foreach (ProjectElement resource in component.ChildrenOrEmpty())
        {
            if (resource.Tag == "scenes")
                node.Children.Add(BuildScenesNode(resource));   // a product's scenario output (scene link target, US-024)
            else if (!IsStructuralChild(resource.Tag))
                node.Children.Add(BuildPinNode(resource));
        }
        return node;
    }

    // A product's scenes container — a scenario-link target — showing its scene member rows (US-024).
    private TreeNodeViewModel BuildScenesNode(ProjectElement scenes)
    {
        var node = new TreeNodeViewModel(scenes.GetAttribute("name") ?? "Scenarier", "/Assets/scenario.svg",
            elementId: scenes.Id) { IsSceneTarget = true };
        foreach (ProjectElement member in scenes.ChildrenOrEmpty())
        {
            if (member.Tag is "scene_relay" or "scene_dimmer" or "scene_shutter")
                node.Children.Add(BuildSceneMemberNode(member));
        }
        return node;
    }

    private TreeNodeViewModel BuildSceneMemberNode(ProjectElement member)
    {
        string value = SceneValue.TryParse(member, out SceneValue sv)
            ? sv.Kind switch
            {
                SceneValueKind.Relay => sv.On ? "ON" : "OFF",
                SceneValueKind.Dimmer => $"{sv.LevelPercent}% / {sv.RampTime.TotalSeconds:0.#}s",
                SceneValueKind.Shutter => sv.ShutterUp ? "up" : "down",
                _ => string.Empty,
            }
            : string.Empty;
        return new TreeNodeViewModel($"← {LinkOppositePath(member)} = {value}", "/Assets/link-from.svg",
            elementId: member.Id) { IsLinkRow = true };
    }


    private TreeNodeViewModel BuildFunctionBlockNode(ProjectElement fb, string name)
    {
        // A locked library block shows the library icon; an unlocked/empty block the editable icon (US-018/US-020).
        bool locked = (fb.GetAttribute("locked") ?? "no") == "yes";
        string icon = locked ? "/Assets/fb-lk.svg" : "/Assets/fb-editable.svg";
        var node = new TreeNodeViewModel(name, icon, elementId: fb.Id, isLockedFunctionBlock: locked)
        {
            IsFunctionBlock = true,
            Tooltip = BuildTooltip(fb),
        };
        foreach ((string container, string label) in FunctionBlockSections.All)
        {
            ProjectElement? holder = fb.FindChild(container);
            var section = new TreeNodeViewModel(label, NodeIcons.For(container, null), elementId: holder?.Id)
            {
                SectionTag = holder is not null ? container : null,
            };
            if (holder is not null)
            {
                foreach (ProjectElement pin in holder.ChildrenOrEmpty())
                    section.Children.Add(BuildPinNode(pin));
            }
            node.Children.Add(section);
        }
        return node;
    }

    // The node categories that map to an IHC resource id shown in the tooltip (US-048): inputs, outputs, blocks.
    private static readonly string[] ResourceIdTags =
        { "resource_input", "resource_output", "dataline_input", "dataline_output", "functionblock" };

    // The hover tooltip (US-047/US-048): the element's documentation note (line breaks preserved) plus, for a
    // resource-mapped node, its IHC resource id. Null when the element has neither, so no tooltip appears.
    private static string? BuildTooltip(ProjectElement element)
    {
        var parts = new List<string>();
        if (element.GetAttribute("note") is { Length: > 0 } note)
            parts.Add(note.Replace("\r\n", "\n"));
        if (ResourceIdTags.Contains(element.Tag) && element.Id is { } id)
            parts.Add($"Resource ID: {id.Value}");
        return parts.Count > 0 ? string.Join("\n\n", parts) : null;
    }

    private TreeNodeViewModel BuildPinNode(ProjectElement resource)
    {
        string name = resource.GetAttribute("name") ?? resource.Tag;
        string? value = resource.GetAttribute("value");
        bool isOutput = resource.Tag is "resource_output" or "dataline_output" or "airlink_relay";
        bool saved = isOutput && (resource.GetAttribute("backup") ?? "no") == "yes";
        string label = string.IsNullOrEmpty(value) ? name : $"{name} = {value}";   // fixed sub-resource default (US-010)
        if (saved)
            label += " (saved)";   // persisted across power loss (US-033)
        var node = new TreeNodeViewModel(label, NodeIcons.For(resource.Tag, resource.GetAttribute("icon")),
            elementId: resource.Id) { IsPin = true, IsOutputPin = isOutput, IsValueSaved = saved, Tooltip = BuildTooltip(resource) };
        // A linked pin reveals its follow-link / scene-link rows, each naming the opposite end's full path (US-022/025).
        foreach (ProjectElement child in resource.ChildrenOrEmpty())
        {
            if (child.Tag is "link_from_resource" or "link_to_resource" or "scene_link")
                node.Children.Add(BuildLinkNode(child));
        }
        return node;
    }

    // A "link from" (←) or "link to" (→) row under a pin, labelled with the full path of the opposite end. A
    // scene_link is the FB scene output's outgoing (→) reference to the product's scene member.
    private TreeNodeViewModel BuildLinkNode(ProjectElement linkRow)
    {
        bool incoming = linkRow.Tag == "link_from_resource";
        string arrow = incoming ? "← " : "→ ";
        string icon = incoming ? "/Assets/link-from.svg" : "/Assets/link-to.svg";
        return new TreeNodeViewModel(arrow + LinkOppositePath(linkRow), icon, elementId: linkRow.Id) { IsLinkRow = true };
    }

    // The full path (locality / product-or-block / pin) of the pin at the opposite end of a link row.
    private string LinkOppositePath(ProjectElement linkRow)
    {
        if (_session.Current is not { } project
            || !ElementId.TryParse(linkRow.GetAttribute("link"), out ElementId partnerId)
            || project.FindParent(partnerId) is not { } oppositePin)
        {
            return "(unresolved)";
        }
        var parts = new List<string>();
        ProjectElement? current = oppositePin;
        bool leaf = true;
        while (current is not null)
        {
            bool significant = leaf || current.Tag is "group" or "functionblock" || ProductClassifier.IsProduct(current.Tag);
            if (significant && current.GetAttribute("name") is { } partName && partName.Length > 0)
                parts.Insert(0, partName);
            current = current.Id is { } cid ? project.FindParent(cid) : null;
            leaf = false;
        }
        return string.Join(" / ", parts);
    }

    // Mirrors ProductDefinition.IsStructuralChild: a product's non-pin containers (scenes / embedded enum / any
    // settings container) are not resources and stay out of the pin preview.
    private static bool IsStructuralChild(string tag) =>
        tag is "scenes" or "enum_definition" or "settings"
        || tag.EndsWith("_settings", StringComparison.Ordinal);

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

    private static ProjectSession CreateDesignSession()
    {
        var service = new Ihc.Vis.ProjectAppService(new Ihc.IhcSettings());
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ihc_openvisual_design");
        return new ProjectSession(service, new BackupService(tempDir), new RecentProjectsStore(System.IO.Path.GetTempFileName()), new NullDialogService());
    }
}
