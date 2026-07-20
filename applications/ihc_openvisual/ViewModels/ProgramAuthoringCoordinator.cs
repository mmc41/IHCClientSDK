using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Programs;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// T030 (S1): the program-authoring engine extracted from <see cref="MainWindowViewModel"/> — the dynamic
/// Events/Commands/Conditions/Case/Arithmetic menus a program node offers for the armed variable (US-028/029/031/
/// 032), and the command application behind each menu item. Mirrors <see cref="PropertiesDialogCoordinator"/>'s
/// delegate-ctor shape: it owns no Avalonia types and reaches the view-model only through the passed delegates
/// (apply/run/select/status) and getters (selection, programming block, name), so it is headlessly testable.
/// The view-model re-exposes the five menu collections (they are XAML-bound) and keeps the thin
/// <c>[RelayCommand]</c> handlers, delegating their bodies here.
/// </summary>
internal sealed class ProgramAuthoringCoordinator(
    ProjectWorkflow session,
    Func<string, Func<Task>, Task> runAsync,
    Func<ProjectCommand, string, Task> applyAndReport,
    Action<TreeNodeViewModel> selectNode,
    Action<string> setStatus,
    Func<TreeNodeViewModel?> getSelectedNode,
    Func<ElementId?> getProgrammingBlockId,
    Func<ProjectElement, string, string> nameOr)
{
    /// <summary>The events a selected variable can raise, offered on a program's Events node (US-028).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramEventMenu { get; } = new();

    /// <summary>The commands a selected variable can be driven by, offered on a program's Commands node (US-028).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramCommandMenu { get; } = new();

    /// <summary>The conditions a selected variable can be tested by on a sub-program's Conditions node (US-029).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramConditionMenu { get; } = new();

    /// <summary>The "Case (&lt;variable&gt;)" option offered on a Commands node for an eligible switch variable (US-031).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramCaseMenu { get; } = new();

    /// <summary>The arithmetic operations offered on a Commands node when a numeric target register is armed (US-032).</summary>
    public ObservableCollection<ProductMenuItemViewModel> ProgramArithmeticMenu { get; } = new();

    // The variable armed by "Use in program" to become the operand of the next event/command (US-028). Setting it
    // rebuilds the menus against the current selection, mirroring the old [ObservableProperty]/OnChanged pair.
    private TreeNodeViewModel? _pendingProgramVariable;

    /// <summary>The variable armed as the operand for the next event/command (US-028); the testable substitute for
    /// dragging a variable onto Events/Commands. Setting it rebuilds the program menus against the current selection.</summary>
    public TreeNodeViewModel? PendingProgramVariable
    {
        get => _pendingProgramVariable;
        private set
        {
            _pendingProgramVariable = value;
            Rebuild(getSelectedNode());
        }
    }

    // The GUI-side presentation verb for each program method (US-028/029), keyed by the SDK method's
    // (Category, Token) — NOT by a positional index parallel to the ProgramMethodCatalog lists, so a catalog reorder
    // or resize can never mis-label a menu item or throw IndexOutOfRange. A method absent from this map is simply not
    // surfaced (the app owns which methods it presents and how it phrases them; the SDK owns the tokens/names/notes/
    // semantics). The (Category, Token) pair is the method's identity — the token alone is reused across categories.
    private static readonly FrozenDictionary<(ProgramMethodCategory Category, string Token), string> MethodVerbs =
        new Dictionary<(ProgramMethodCategory, string), string>
        {
            [(ProgramMethodCategory.Event, "_0xa")] = "changes to ON",
            [(ProgramMethodCategory.Event, "_0x96")] = "changes state",
            [(ProgramMethodCategory.Event, "_0x9b")] = "is assigned",
            [(ProgramMethodCategory.Command, "_0xa")] = "set to ON",
            [(ProgramMethodCategory.Command, "_0x14")] = "set to OFF",
            [(ProgramMethodCategory.Command, "_0x23")] = "toggled",
            [(ProgramMethodCategory.Condition, "_0xa")] = "is ON",
            [(ProgramMethodCategory.Condition, "_0x14")] = "is OFF",
            [(ProgramMethodCategory.Condition, "_0x28")] = "is NOT ON",
        }.ToFrozenDictionary();

    /// <summary>Pairs each catalog method with its GUI menu label ("<paramref name="varName"/> &lt;verb&gt;") by the
    /// method's (Category, Token) — order-independent and resize-safe: a method with no <see cref="MethodVerbs"/> verb
    /// is dropped rather than mis-labelled or throwing. The pure core each Event/Command/Condition menu is built from
    /// (internal so it is unit-testable against a reordered/resized method list).</summary>
    internal static IEnumerable<(ProgramMethod Method, string Label)> MethodMenuItems(
        IEnumerable<ProgramMethod> methods, string varName)
    {
        foreach (ProgramMethod m in methods)
            if (MethodVerbs.TryGetValue((m.Category, m.Token), out string? verb))
                yield return (m, $"{varName} {verb}");
    }

    /// <summary>Arms a variable (a block input/output/setting/internal, US-028) as the operand for the next event or
    /// command; the Events/Commands node then offers that variable's triggers and commands.</summary>
    public void Arm(TreeNodeViewModel? node)
    {
        if (node is { IsPin: true })
        {
            setStatus($"Using {node.DisplayName} — pick 'Add event' or 'Add command' on the program.");
            PendingProgramVariable = node;
        }
    }

    /// <summary>Arms <paramref name="variable"/> and surfaces the method popup on <paramref name="container"/> — the
    /// drag gesture behind US-028. It selects the drop-target container so the shared menu-builder populates that
    /// container's Add-event/Add-command menu for the armed variable; the user then chooses a method, which builds the
    /// event/command exactly as the two-step "Use in program" does. A-27's locked-block gate is applied upstream.</summary>
    public void ArmAndSelect(TreeNodeViewModel variable, TreeNodeViewModel container)
    {
        PendingProgramVariable = variable;
        selectNode(container);   // triggers the view-model's OnSelectedNodeChanged → Rebuild(container)
        setStatus($"Using {variable.DisplayName} — choose a method on {container.DisplayName}.");
    }

    /// <summary>Rebuilds the five program menus for the armed variable against <paramref name="value"/> (the selected
    /// program node). Called on selection change and when a variable is armed.</summary>
    public void Rebuild(TreeNodeViewModel? value)
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
            foreach ((ProgramMethod m, string label) in MethodMenuItems(ProgramMethodCatalog.Events, varName))
                ProgramEventMenu.Add(new ProductMenuItemViewModel(label, m.Token,
                    new AsyncRelayCommand(() => AddProgramEventAsync(eventsId, varId, m.Token, m.NameTemplate, m.Note))));
        }
        if (value is { IsCommandsContainer: true, ElementId: { } actionsId })
        {
            foreach ((ProgramMethod m, string label) in MethodMenuItems(ProgramMethodCatalog.Commands, varName))
                ProgramCommandMenu.Add(new ProductMenuItemViewModel(label, m.Token,
                    new AsyncRelayCommand(() => AddProgramCommandAsync(actionsId, varId, m.Token, m.NameTemplate, m.Note))));
            // A case can be built here when the armed variable is an eligible switch type (US-031).
            if (session.Current?.FindById(varId)?.Tag is { } varTag && ProgramMethodCatalog.EligibleCaseVariableTags.Contains(varTag))
                ProgramCaseMenu.Add(new ProductMenuItemViewModel($"Case ({varName})", "case",
                    new AsyncRelayCommand(() => AddCaseAsync(actionsId, varId))));
            // Arithmetic can be built here when the armed variable is a numeric target register (US-032).
            if (session.Current?.FindById(varId)?.Tag is { } t && ProgramMethodCatalog.NumericVariableTags.Contains(t))
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
            foreach ((ProgramMethod m, string label) in MethodMenuItems(ProgramMethodCatalog.Conditions, varName))
                ProgramConditionMenu.Add(new ProductMenuItemViewModel(label, m.Token,
                    new AsyncRelayCommand(() => AddConditionAsync(conditionsId, varId, m.Token, m.NameTemplate, m.Note))));
        }
    }

    private Task AddProgramEventAsync(ElementId eventsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddProgramEventAsync), async () =>
        {
            if (session.Commands.AddProgramEvent(session.Current!, eventsId, variableId, method, name, note) is { } command)
                await applyAndReport(command, "Event added to the program.");
        });

    private Task AddProgramCommandAsync(ElementId actionsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddProgramCommandAsync), () =>
            applyAndReport(session.Commands.AddProgramCommand(session.Current!, actionsId, variableId, method, name, note), "Command added to the program."));

    private Task AddConditionAsync(ElementId conditionsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddConditionAsync), () =>
            applyAndReport(session.Commands.AddCondition(session.Current!, conditionsId, variableId, method, name, note), "Condition added."));

    private Task AddCaseAsync(ElementId commandsId, ElementId switchVariableId) =>
        runAsync(nameof(AddCaseAsync), () =>
            applyAndReport(session.Commands.AddCase(session.Current!, commandsId, switchVariableId), "Case structure inserted."));

    private Task AddArithmeticAsync(ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name) =>
        runAsync(nameof(AddArithmeticAsync), () =>
            applyAndReport(session.Commands.AddArithmeticCommand(session.Current!, commandsId, targetId, method, operandId, name), "Arithmetic command added."));

    // The numeric variables (decimal/integer/counter) in the programming block — the operand candidates for an
    // arithmetic command line (US-032).
    private IEnumerable<(string Name, ElementId Id)> NumericOperandsInBlock()
    {
        if (session.Current is not { } project || getProgrammingBlockId() is not { } blockId
            || project.FindById(blockId) is not { } block)
            yield break;
        foreach ((string container, string _) in FunctionBlockSections.All)
        {
            if (block.FindChild(container) is not { } section)
                continue;
            foreach (ProjectElement pin in section.ChildrenOrEmpty())
                if (ProgramMethodCatalog.NumericVariableTags.Contains(pin.Tag) && pin.Id is { } pid)
                    yield return (nameOr(pin, pin.Tag), pid);
        }
    }
}
