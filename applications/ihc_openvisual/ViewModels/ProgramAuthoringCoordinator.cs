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
using Ihc.Vis.Projects;
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
    IDialogService dialogs,
    Func<string, Func<Task>, Task> runAsync,
    Func<ProjectCommand, string, Task> applyAndReport,
    Action<TreeNodeViewModel> selectNode,
    Action<string> setStatus,
    Func<TreeNodeViewModel?> getSelectedNode,
    Func<ElementId?> getProgrammingBlockId,
    Action menusChanged)
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

    // The variable armed by "Brug i program" to become the operand of the next event/command (US-028). Setting it
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

    // The name a newly created program carries, matching the empty-block template's own program.
    private const string ProgramDefaultName = "Program";

    // The GUI-side presentation verb for each program method (US-028/029), keyed by the SDK method's
    // (PinType, Category, Token) — NOT by a positional index parallel to the ProgramMethodCatalog lists, so a catalog
    // reorder or resize can never mis-label a menu item or throw IndexOutOfRange. A method absent from this map is
    // simply not surfaced (the app owns which methods it presents and how it phrases them; the SDK owns the tokens/
    // names/notes/semantics). The full (PinType, Category, Token) triple is the method's identity — the token alone is
    // reused across categories, AND the same (Category, Token) means different things across pin-type families (e.g.
    // (Command,_0xa) is "sættes til ON" for a Bool pin but "= 0" for a Timer pin), which is why PinType is part of the key.
    private static readonly FrozenDictionary<(ProgramPinType PinType, ProgramMethodCategory Category, string Token), string> MethodVerbs =
        new Dictionary<(ProgramPinType, ProgramMethodCategory, string), string>
        {
            [(ProgramPinType.Bool, ProgramMethodCategory.Event, "_0xa")] = "skifter til ON",
            [(ProgramPinType.Bool, ProgramMethodCategory.Event, "_0x14")] = "skifter til OFF",
            [(ProgramPinType.Bool, ProgramMethodCategory.Event, "_0x1e")] = "skifter til",           // 2-operand (second pin)
            [(ProgramPinType.Bool, ProgramMethodCategory.Event, "_0x28")] = "skifter til IKKE",       // 2-operand
            [(ProgramPinType.Bool, ProgramMethodCategory.Event, "_0x96")] = "skifter tilstand",
            [(ProgramPinType.Bool, ProgramMethodCategory.Event, "_0x9b")] = "tildeles",
            [(ProgramPinType.Bool, ProgramMethodCategory.Command, "_0xa")] = "sættes til ON",
            [(ProgramPinType.Bool, ProgramMethodCategory.Command, "_0x14")] = "sættes til OFF",
            [(ProgramPinType.Bool, ProgramMethodCategory.Command, "_0x1e")] = "sættes til",             // 2-operand
            [(ProgramPinType.Bool, ProgramMethodCategory.Command, "_0x28")] = "sættes til IKKE",         // 2-operand
            [(ProgramPinType.Bool, ProgramMethodCategory.Command, "_0x23")] = "kippes",
            [(ProgramPinType.Bool, ProgramMethodCategory.Condition, "_0xa")] = "er ON",
            [(ProgramPinType.Bool, ProgramMethodCategory.Condition, "_0x14")] = "er OFF",
            [(ProgramPinType.Bool, ProgramMethodCategory.Condition, "_0x1e")] = "er lig med",           // 2-operand
            [(ProgramPinType.Bool, ProgramMethodCategory.Condition, "_0x28")] = "er forskellig fra",     // 2-operand (was unary "is NOT ON")
            // Weekday (PG-1b): the System-weekday assignment reads pin-first in the menu though it stores "System ugedag -> %P".
            [(ProgramPinType.Weekday, ProgramMethodCategory.Event, "_0x5")] = "= system ugedag",
            // Timer (D21/D22, the full nine) — a shared token (_0xa) means something different than the bool verb.
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0xa")] = "= 0",
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0x19")] = "= initial værdi",
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0x1e")] = "= en anden timer",           // 2-operand
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0x5a")] = "øges med",              // 2-operand
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0x64")] = "mindskes med",              // 2-operand
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0xbe")] = "nedtælling fra initial værdi",
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0xc8")] = "optælling aktiveret",
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0xd2")] = "nedtælling aktiveret",
            [(ProgramPinType.Timer, ProgramMethodCategory.Command, "_0xdc")] = "stop tælling",
            // Timer events (D22/progmode3).
            [(ProgramPinType.Timer, ProgramMethodCategory.Event, "_0xa")] = "når 0",
            [(ProgramPinType.Timer, ProgramMethodCategory.Event, "_0x9b")] = "skrives",
            // Timer conditions (D22/progmode3) — the comparisons are two-operand; the count-state predicates reuse
            // the command opcodes but are (code, family)-scoped, so their verbs live under the Condition family here.
            [(ProgramPinType.Timer, ProgramMethodCategory.Condition, "_0xa")] = "er 0",
            [(ProgramPinType.Timer, ProgramMethodCategory.Condition, "_0x32")] = "større end",   // 2-operand
            [(ProgramPinType.Timer, ProgramMethodCategory.Condition, "_0x46")] = "mindst",       // 2-operand
            [(ProgramPinType.Timer, ProgramMethodCategory.Condition, "_0x50")] = "højst",        // 2-operand
            [(ProgramPinType.Timer, ProgramMethodCategory.Condition, "_0xc8")] = "tæller op",
            [(ProgramPinType.Timer, ProgramMethodCategory.Condition, "_0xd2")] = "tæller ned",
            [(ProgramPinType.Timer, ProgramMethodCategory.Condition, "_0xdc")] = "stoppet",
        }.ToFrozenDictionary();

    /// <summary>Pairs each catalog method with its GUI menu label ("<paramref name="varName"/> &lt;verb&gt;") by the
    /// method's (<paramref name="pinType"/>, Category, Token) — a timer's <c>_0xa</c> ("= 0") differs from a bool's
    /// ("sættes til ON") for the same token, so the pin type is part of the key; a type without its own verb falls back to
    /// the bool verb (analog/weekday reuse the bool changes-state/is-assigned verbs). Order-independent and
    /// resize-safe: a method with no verb is dropped rather than mis-labelled or throwing. The pure core each
    /// Event/Command/Condition menu is built from (internal so it is unit-testable against a reordered method list).</summary>
    internal static IEnumerable<(ProgramMethod Method, string Label)> MethodMenuItems(
        IEnumerable<ProgramMethod> methods, string varName, ProgramPinType pinType)
    {
        foreach (ProgramMethod m in methods)
            if (MethodVerbs.TryGetValue((pinType, m.Category, m.Token), out string? verb)
                || MethodVerbs.TryGetValue((ProgramPinType.Bool, m.Category, m.Token), out verb))
                yield return (m, $"{varName} {verb}");
    }

    /// <summary>Arms a variable (a block input/output/setting/internal, US-028) as the operand for the next event or
    /// command; the Events/Commands node then offers that variable's triggers and commands.</summary>
    public void Arm(TreeNodeViewModel? node)
    {
        if (node is { IsPin: true })
        {
            setStatus($"Bruger {node.DisplayName} — vælg 'Tilføj hændelse' eller 'Tilføj kommando' på programmet.");
            PendingProgramVariable = node;
        }
    }

    /// <summary>Arms <paramref name="variable"/> and surfaces the method popup on <paramref name="container"/> — the
    /// drag gesture behind US-028. It selects the drop-target container so the shared menu-builder populates that
    /// container's Add-event/Add-command menu for the armed variable; the user then chooses a method, which builds the
    /// event/command exactly as the two-step "Brug i program" does. A-27's locked-block gate is applied upstream.</summary>
    public void ArmAndSelect(TreeNodeViewModel variable, TreeNodeViewModel container)
    {
        PendingProgramVariable = variable;
        selectNode(container);   // triggers the view-model's OnSelectedNodeChanged → Rebuild(container)
        setStatus($"Using {variable.DisplayName} — choose a method on {container.DisplayName}.");
    }

    /// <summary>Rebuilds the five program menus for the armed variable against <paramref name="value"/> (the selected
    /// program node). Called on selection change and when a variable is armed. Announces the rebuild afterwards: the
    /// view-model's submenu gates read these collections' CONTENTS (an empty submenu is not offered), and arming an
    /// operand refills them without touching the selection, so nothing else would raise them.</summary>
    public void Rebuild(TreeNodeViewModel? value)
    {
        RebuildMenus(value);
        menusChanged();
    }

    private void RebuildMenus(TreeNodeViewModel? value)
    {
        ProgramEventMenu.Clear();
        ProgramCommandMenu.Clear();
        ProgramConditionMenu.Clear();
        ProgramCaseMenu.Clear();
        ProgramArithmeticMenu.Clear();
        if (PendingProgramVariable is not { ElementId: { } varId })
            return;
        // The armed variable is resolved ONCE — every fact the five menus need (name, tag, direction) comes off this
        // one element. FindById is a whole-tree walk and this runs on every selection change while a variable is armed.
        ProjectElement? armed = session.Current?.FindById(varId);
        // The armed variable's NAME, not its tree label: since W8/T027 a variable row reads "Tal = 0", and a menu
        // built from the label would offer "Tal = 0 += …". The name comes from the project, which is where it lives.
        string varName = (armed is not null ? session.Current!.View(armed).Name : null)
                         ?? PendingProgramVariable.DisplayName;
        string armedTag = armed?.Tag ?? string.Empty;
        // PG-1b: the dragged pin's TYPE picks the operator list per container, so a timer/analog/weekday pin no
        // longer inherits the bool operators (a tag outside those families stays on the bool default).
        ProgramPinType pinType = ProgramMethodCatalog.ClassifyPin(armedTag);
        if (value is { IsEventsContainer: true, ElementId: { } eventsId })
        {
            foreach ((ProgramMethod m, string label) in MethodMenuItems(ProgramMethodCatalog.EventsFor(pinType), varName, pinType))
                AddOperator(ProgramEventMenu, m, label, pinType, varId,
                    one => AddProgramEventAsync(eventsId, varId, one.Token, one.NameTemplate, one.Note),
                    (two, opId) => AddTwoOperandEventAsync(eventsId, varId, opId, two.Token, two.NameTemplate, two.Note));
        }
        if (value is { IsCommandsContainer: true, ElementId: { } actionsId })
        {
            // PG-1c: Toggle (and any bool-output-only command) is offered only when the armed variable is a bool
            // OUTPUT pin — resolved from the SDK read model, not the VM flag, matching the case/arithmetic checks below.
            bool boolOutput = armed is { IsOutputPin: true };
            foreach ((ProgramMethod m, string label) in MethodMenuItems(ProgramMethodCatalog.CommandsFor(pinType), varName, pinType))
            {
                if (ProgramMethodCatalog.BoolOutputOnlyCommandTokens.Contains(m.Token) && !boolOutput)
                    continue;
                // A two-operand command (%P = %S / %P <> %S) reuses the arithmetic-shape command (link1 target, link2 operand).
                AddOperator(ProgramCommandMenu, m, label, pinType, varId,
                    one => AddProgramCommandAsync(actionsId, varId, one.Token, one.NameTemplate, one.Note),
                    (two, opId) => AddArithmeticAsync(actionsId, varId, two.Token, opId, two.NameTemplate));
            }
            // A case can be built here when the armed variable is an eligible switch type (US-031).
            if (ProgramMethodCatalog.EligibleCaseVariableTags.Contains(armedTag))
                ProgramCaseMenu.Add(new ProductMenuItemViewModel($"Case ({varName})", "case",
                    new AsyncRelayCommand(() => AddCaseAsync(actionsId, varId))));
            // Arithmetic (US-032, F-108/F-109): a numeric target register offers each operator as a second-operand
            // submenu, but ONLY the authorable cells — the opcode and legality per (op, target-type, operand-type)
            // come from the SDK grid; a dead cell is never offered. A counter target additionally gets the 1-op steps.
            if (ProgramMethodCatalog.NumericVariableTags.Contains(armedTag))
            {
                foreach (ProgramMethod op in ProgramMethodCatalog.Arithmetic)
                {
                    var opNode = new ProductMenuItemViewModel($"{varName} {op.OperatorSymbol}= …");   // category
                    foreach ((string opName, ElementId opId, string operandTag) in NumericOperandsInBlock())
                        if (ProgramMethodCatalog.ArithmeticToken(op.OperatorSymbol!, armedTag, operandTag) is { } token)
                            opNode.Children.Add(new ProductMenuItemViewModel(opName, token,
                                new AsyncRelayCommand(() => AddArithmeticAsync(actionsId, varId, token, opId, op.NameTemplate))));
                    if (opNode.Children.Count > 0)
                        ProgramArithmeticMenu.Add(opNode);
                }
                if (armedTag == "resource_counter")
                    foreach (ProgramMethod step in ProgramMethodCatalog.CounterSteps)
                        ProgramArithmeticMenu.Add(new ProductMenuItemViewModel($"{varName} {step.OperatorSymbol} 1", step.Token,
                            new AsyncRelayCommand(() => AddProgramCommandAsync(actionsId, varId, step.Token, step.NameTemplate, step.Note))));
            }
        }
        if (value is { IsConditionsContainer: true, ElementId: { } conditionsId })
        {
            foreach ((ProgramMethod m, string label) in MethodMenuItems(ProgramMethodCatalog.ConditionsFor(pinType), varName, pinType))
                AddOperator(ProgramConditionMenu, m, label, pinType, varId,
                    one => AddConditionAsync(conditionsId, varId, one.Token, one.NameTemplate, one.Note),
                    (two, opId) => AddTwoOperandConditionAsync(conditionsId, varId, opId, two.Token, two.NameTemplate, two.Note));
        }
    }

    // The one-operand and two-operand authors of a family report the SAME outcome — the installer sees an event or a
    // condition appear either way — so each family's status is declared once instead of at both call sites.
    private const string EventAddedStatus = "Hændelse tilføjet til programmet.";
    private const string ConditionAddedStatus = "Betingelse tilføjet.";

    private Task AddProgramEventAsync(ElementId eventsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddProgramEventAsync), async () =>
        {
            if (session.Commands.AddProgramEvent(session.Current!, eventsId, variableId, method, name, note) is { } command)
                await applyAndReport(command, EventAddedStatus);
        });

    private Task AddProgramCommandAsync(ElementId actionsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddProgramCommandAsync), () =>
            applyAndReport(session.Commands.AddProgramCommand(session.Current!, actionsId, variableId, method, name, note), "Kommando tilføjet til programmet."));

    private Task AddConditionAsync(ElementId conditionsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddConditionAsync), () =>
            applyAndReport(session.Commands.AddCondition(session.Current!, conditionsId, variableId, method, name, note), ConditionAddedStatus));

    private Task AddCaseAsync(ElementId commandsId, ElementId switchVariableId) =>
        runAsync(nameof(AddCaseAsync), () =>
            applyAndReport(session.Commands.AddCase(session.Current!, commandsId, switchVariableId), "Case struktur indsat."));

    private Task AddArithmeticAsync(ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name) =>
        runAsync(nameof(AddArithmeticAsync), () =>
            applyAndReport(session.Commands.AddArithmeticCommand(session.Current!, commandsId, targetId, method, operandId, name), "Aritmetisk kommando tilføjet."));

    // T008: the two-operand event / condition authors — the arithmetic peer for the Events/Conditions families
    // (%P <op> %S with the author-chosen operand %S), through the extended AddProgramEvent/AddCondition (link2).
    private Task AddTwoOperandEventAsync(ElementId eventsId, ElementId variableId, ElementId operandId, string method, string name, string note) =>
        runAsync(nameof(AddTwoOperandEventAsync), async () =>
        {
            if (session.Commands.AddProgramEvent(session.Current!, eventsId, variableId, method, name, note, operandId) is { } command)
                await applyAndReport(command, EventAddedStatus);
        });

    private Task AddTwoOperandConditionAsync(ElementId conditionsId, ElementId variableId, ElementId operandId, string method, string name, string note) =>
        runAsync(nameof(AddTwoOperandConditionAsync), () =>
            applyAndReport(session.Commands.AddCondition(session.Current!, conditionsId, variableId, method, name, note, operandId), ConditionAddedStatus));

    // The numeric variables (decimal/integer/counter) in the programming block — the operand candidates for an
    // arithmetic command line (US-032).
    private IEnumerable<(string Name, ElementId Id, string Tag)> NumericOperandsInBlock()
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
                    yield return (project.NameOr(pin, pin.Tag), pid, pin.Tag);   // tag drives the F-108 opcode grid
        }
    }

    // T008: builds one operator menu item into <paramref name="target"/> — a flat item for a 1-operand method, or a
    // second-pin submenu for a two-operand row (%P <op> %S), reusing the arithmetic submenu shape. The author picks
    // %S from the block's same-type pins; a two-operand method with no candidate operand is dropped, never silently
    // auto-bound. <paramref name="authorOne"/>/<paramref name="authorTwo"/> author the unary / two-operand row.
    private void AddOperator(ObservableCollection<ProductMenuItemViewModel> target, ProgramMethod m, string label,
        ProgramPinType pinType, ElementId varId, Func<ProgramMethod, Task> authorOne, Func<ProgramMethod, ElementId, Task> authorTwo)
    {
        if (m.OperandCount < 2)
        {
            target.Add(new ProductMenuItemViewModel(label, m.Token, new AsyncRelayCommand(() => authorOne(m))));
            return;
        }
        var node = new ProductMenuItemViewModel($"{label} …");
        foreach ((string opName, ElementId opId) in SecondOperandCandidates(pinType, varId))
            node.Children.Add(new ProductMenuItemViewModel(opName, m.Token, new AsyncRelayCommand(() => authorTwo(m, opId))));
        if (node.Children.Count > 0)
            target.Add(node);
    }

    // T008: the block's pins of the same type family as the armed pin (the same ClassifyPin the popup keys on),
    // excluding the armed pin itself — the %S candidates a two-operand row lets the author pick.
    private IEnumerable<(string Name, ElementId Id)> SecondOperandCandidates(ProgramPinType type, ElementId exclude)
    {
        if (session.Current is not { } project || getProgrammingBlockId() is not { } blockId
            || project.FindById(blockId) is not { } block)
            yield break;
        foreach ((string container, string _) in FunctionBlockSections.All)
        {
            if (block.FindChild(container) is not { } section)
                continue;
            foreach (ProjectElement pin in section.ChildrenOrEmpty())
                if (pin.Id is { } pid && pid != exclude && ProgramMethodCatalog.ClassifyPin(pin.Tag) == type)
                    yield return (project.NameOr(pin, pin.Tag), pid);
        }
    }

    // ---- T018: the stray program-authoring handlers consolidated here (US-029/031/033); the view-model keeps the
    // thin [RelayCommand] entry points, delegating their bodies to these. ----

    /// <summary>Adds a Powerup system event to the selected Events group (US-033) — no operand needed.</summary>
    public Task AddPowerEventAsync(TreeNodeViewModel? node) => runAsync("AddPowerEvent", async () =>
    {
        if (node is { IsEventsContainer: true, ElementId: { } eventsId } && session.Current is { } project
            && session.Commands.AddPowerEvent(project, eventsId) is { } command)
            await applyAndReport(command, "Powerup hændelse tilføjet til programmet.");
    });

    /// <summary>Toggles an output's <i>Save current value</i> power-loss persistence (US-033).</summary>
    public Task ToggleSaveValueAsync(TreeNodeViewModel? node) => runAsync("ToggleSaveValue", async () =>
    {
        if (node is { IsOutputPin: true, ElementId: { } outputId } && session.Current is { } project)
            await applyAndReport(session.Commands.SetOutputBackup(project, outputId, !node.IsValueSaved),
                node.IsValueSaved ? "Udgangsværdi gemmes ikke længere ved strømsvigt." : "Udgangsværdi gemmes ved strømsvigt.");
    });

    /// <summary>Adds a new, empty program to a block's Programs group (US-026, uxparity2 W4). A block may hold more
    /// than one program; each arrives with its own events and commands groups, ready to author.</summary>
    public Task AddProgramAsync(TreeNodeViewModel? node) => runAsync("AddProgram", async () =>
    {
        if (node is { Kind: TreeNodeKind.Programs, ElementId: { } id } && session.Current is { } project)
            await applyAndReport(session.Commands.AddProgram(project, id, ProgramDefaultName), "Program indsat.");
    });

    /// <summary>Inserts a conditional sub-program (Conditions + true/false command branches) into a Commands group (US-029).</summary>
    public Task AddSubProgramAsync(TreeNodeViewModel? node) => runAsync("AddSubProgram", async () =>
    {
        if (node is { IsCommandsContainer: true, ElementId: { } id } && session.Current is { } project)
            await applyAndReport(session.Commands.AddSubProgram(project, id), "Under program indsat.");
    });

    /// <summary>Inserts a nested logic group inside a Conditions group for a compound expression (US-029).</summary>
    public Task AddLogicGroupAsync(TreeNodeViewModel? node) => runAsync("AddLogicGroup", async () =>
    {
        if (node is { IsConditionsContainer: true, ElementId: { } id } && session.Current is { } project)
            await applyAndReport(session.Commands.AddLogicGroup(project, id), "Logik gruppe indsat.");
    });

    /// <summary>Combines a Conditions group with OR (<c>&gt;=1</c>) (US-029).</summary>
    public Task SetConditionsOrAsync(TreeNodeViewModel? node) => ToggleConditionsAsync(node, or: true);

    /// <summary>Combines a Conditions group with AND (<c>&amp;</c>, the default) (US-029).</summary>
    public Task SetConditionsAndAsync(TreeNodeViewModel? node) => ToggleConditionsAsync(node, or: false);

    private Task ToggleConditionsAsync(TreeNodeViewModel? node, bool or) => runAsync("ToggleConditionsAsync", async () =>
    {
        if (node is { IsConditionsContainer: true, ElementId: { } id } && session.Current is { } project)
            await applyAndReport(session.Commands.SetConditionsLogic(project, id, or),
                or ? "Betingelser kombineret med OR (>=1)." : "Betingelser kombineret med AND (&).");
    });

    /// <summary>Adds a case value branch to the selected Case node (US-031): prompts for the criterion value, then
    /// inserts a command group tagged with it (filled by the normal Add-command gesture). For an ENUM-keyed case the
    /// criterion must name one of the type's states (T014) — they are surfaced in the prompt and a non-state is
    /// reported rather than silently dropped.</summary>
    public Task NewCaseValueAsync(TreeNodeViewModel? node) => runAsync("NewCaseValue", async () =>
    {
        if (node is not { IsCaseNode: true, ElementId: { } caseId } || session.Current is not { } project)
            return;
        // An enum switch takes one of its type's STATE names as the criterion (the gateway rejects any other value);
        // surface the states so the user enters a real one. A literal switch (counter/integer/…) takes a free value.
        IReadOnlyList<string> states = EnumSwitchStates(project, caseId);
        string title = states.Count > 0 ? $"Ny case værdi ({string.Join(", ", states)})" : "Ny case værdi";
        PropertiesResult? result = await dialogs.EditPropertiesAsync(title, string.Empty, string.Empty);
        if (result is null || string.IsNullOrWhiteSpace(result.Name))
            return;
        string criterion = result.Name.Trim();
        if (session.Commands.AddCaseValue(project, caseId, criterion) is { } command)
            await applyAndReport(command, $"Case værdi '{criterion}' tilføjet.");
        else if (states.Count > 0)
            setStatus($"'{criterion}' er ikke en tilstand i denne enumerator — vælg en af: {string.Join(", ", states)}.");
    });

    // The state names of an enum-keyed case's switch (US-031/T014), or an empty list when the switch is not an enum —
    // read straight off the case's linked switch and its enum type.
    private static IReadOnlyList<string> EnumSwitchStates(Project project, ElementId caseId)
    {
        var states = new List<string>();
        if (project.FindById(caseId) is { } kase
            && ElementId.TryParse(kase.GetAttribute("link"), out ElementId switchId)
            && project.FindById(switchId) is { Tag: "resource_enum" } switchVar
            && ElementId.TryParse(switchVar.GetAttribute("typedef"), out ElementId defId)
            && project.FindById(defId) is { } def)
        {
            foreach (ProjectElement value in def.ChildrenOrEmpty())
            {
                if (value.Tag == "enum_value")
                {
                    states.Add(value.GetAttribute("name") ?? string.Empty);
                }
            }
        }
        return states;
    }
}
