using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
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
    Func<string, Func<Ihc.OperationScope, Task>, Task<Ihc.OperationOutcome>> runAsync,
    Func<Ihc.OperationScope, ProjectCommand, string, Task> applyAndReport,
    Action<TreeNodeViewModel> selectNode,
    Action<string> setStatus,
    Func<TreeNodeViewModel?> getSelectedNode,
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

    // The GUI-side presentation verb for each offered operation (US-028/029), keyed by the identity the SDK's own
    // offer carries: (Family, Kind, Method). NOT by a positional index parallel to any catalog list, so a catalog
    // reorder or resize can never mis-label a menu item or throw IndexOutOfRange. An operation absent from this map is
    // simply not surfaced (the app owns which operations it presents and how it phrases them; the SDK owns which are
    // compatible at all, and their tokens/names/notes). The full triple is the identity — the token alone is reused
    // across kinds, AND the same (Kind, Method) means different things across families (e.g. (Command,_0xa) is
    // "sættes til ON" for a bool pin but "= 0" for a timer), which is why the family is part of the key.
    private static readonly FrozenDictionary<(ProgramPinFamily Family, ProgramOperationKind Kind, string Token), string> MethodVerbs =
        new Dictionary<(ProgramPinFamily, ProgramOperationKind, string), string>
        {
            [(ProgramPinFamily.Bool, ProgramOperationKind.Event, "_0xa")] = "skifter til ON",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Event, "_0x14")] = "skifter til OFF",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Event, "_0x1e")] = "skifter til",           // 2-operand (second pin)
            [(ProgramPinFamily.Bool, ProgramOperationKind.Event, "_0x28")] = "skifter til IKKE",       // 2-operand
            [(ProgramPinFamily.Bool, ProgramOperationKind.Event, "_0x96")] = "skifter tilstand",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Event, "_0x9b")] = "tildeles",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Command, "_0xa")] = "sættes til ON",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Command, "_0x14")] = "sættes til OFF",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Command, "_0x1e")] = "sættes til",             // 2-operand
            [(ProgramPinFamily.Bool, ProgramOperationKind.Command, "_0x28")] = "sættes til IKKE",         // 2-operand
            [(ProgramPinFamily.Bool, ProgramOperationKind.Command, "_0x23")] = "kippes",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Condition, "_0xa")] = "er ON",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Condition, "_0x14")] = "er OFF",
            [(ProgramPinFamily.Bool, ProgramOperationKind.Condition, "_0x1e")] = "er lig med",           // 2-operand
            [(ProgramPinFamily.Bool, ProgramOperationKind.Condition, "_0x28")] = "er forskellig fra",     // 2-operand (was unary "is NOT ON")
            // Enumerator (S2-13): live vendor popup labels are symbolic and its binary operators accept only another
            // variable of the same enum definition. The catalog excludes all Boolean ON/OFF/NOT rows for this family.
            [(ProgramPinFamily.Enum, ProgramOperationKind.Event, "_0x1e")] = "->",
            [(ProgramPinFamily.Enum, ProgramOperationKind.Event, "_0x96")] = "skifter tilstand",
            [(ProgramPinFamily.Enum, ProgramOperationKind.Event, "_0x9b")] = "bliver tilskrevet",
            [(ProgramPinFamily.Enum, ProgramOperationKind.Command, "_0x1e")] = "=",
            [(ProgramPinFamily.Enum, ProgramOperationKind.Condition, "_0x1e")] = "=",
            [(ProgramPinFamily.Enum, ProgramOperationKind.Condition, "_0x28")] = "<>",
            // Weekday (PG-1b): the System-weekday assignment reads pin-first in the menu though it stores "System ugedag -> %P".
            [(ProgramPinFamily.Weekday, ProgramOperationKind.Event, "_0x5")] = "= system ugedag",
            // Timer (D21/D22, the full nine) — a shared token (_0xa) means something different than the bool verb.
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0xa")] = "= 0",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0x19")] = "= initial værdi",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0x1e")] = "= en anden timer",           // 2-operand
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0x5a")] = "øges med",              // 2-operand
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0x64")] = "mindskes med",              // 2-operand
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0xbe")] = "nedtælling fra initial værdi",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0xc8")] = "optælling aktiveret",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0xd2")] = "nedtælling aktiveret",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Command, "_0xdc")] = "stop tælling",
            // Timer events (D22/progmode3).
            [(ProgramPinFamily.Timer, ProgramOperationKind.Event, "_0xa")] = "når 0",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Event, "_0x9b")] = "skrives",
            // Timer conditions (D22/progmode3) — the comparisons are two-operand; the count-state predicates reuse
            // the command opcodes but are (code, family)-scoped, so their verbs live under the Condition family here.
            [(ProgramPinFamily.Timer, ProgramOperationKind.Condition, "_0xa")] = "er 0",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Condition, "_0x32")] = "større end",   // 2-operand
            [(ProgramPinFamily.Timer, ProgramOperationKind.Condition, "_0x46")] = "mindst",       // 2-operand
            [(ProgramPinFamily.Timer, ProgramOperationKind.Condition, "_0x50")] = "højst",        // 2-operand
            [(ProgramPinFamily.Timer, ProgramOperationKind.Condition, "_0xc8")] = "tæller op",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Condition, "_0xd2")] = "tæller ned",
            [(ProgramPinFamily.Timer, ProgramOperationKind.Condition, "_0xdc")] = "stoppet",
        }.ToFrozenDictionary();

    /// <summary>Pairs each OFFERED operation with its GUI menu label ("<paramref name="varName"/> &lt;verb&gt;") by
    /// the offer's own (Family, Kind, Method) identity — a timer's <c>_0xa</c> ("= 0") differs from a bool's
    /// ("sættes til ON") for the same token, so the family is part of the key; a family without its own verb falls
    /// back to the bool verb (analog/weekday reuse the bool changes-state/is-assigned verbs). Order-independent and
    /// resize-safe: an operation with no verb is dropped rather than mis-labelled or throwing. The pure core each
    /// Event/Command/Condition menu is built from (internal so it is unit-testable against a reordered offer list).</summary>
    internal static IEnumerable<(ProgramOperationOffer Offer, string Label)> MethodMenuItems(
        IEnumerable<ProgramOperationOffer> offers, string varName)
    {
        foreach (ProgramOperationOffer offer in offers)
            if (VerbFor(offer) is { } verb)
                yield return (offer, $"{varName} {verb}");
    }

    /// <summary>The presentation verb for one offered operation, or null when the app does not surface it. A family
    /// without its own verb falls back to the bool one, which is what lets analog and weekday pins reuse the bool
    /// changes-state/is-assigned wording.</summary>
    private static string? VerbFor(ProgramOperationOffer offer) =>
        MethodVerbs.TryGetValue((offer.Family, offer.Kind, offer.Method), out string? verb)
        || MethodVerbs.TryGetValue((ProgramPinFamily.Bool, offer.Kind, offer.Method), out verb)
            ? verb
            : null;

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
        // The armed variable's NAME, not its tree label: since W8/T027 a variable row reads "Tal = 0", and a menu
        // built from the label would offer "Tal = 0 += …". The name comes from the project, which is where it lives.
        ProjectElement? armed = session.Current?.FindById(varId);
        string varName = (armed is not null ? session.Current!.View(armed).Name : null)
                         ?? PendingProgramVariable.DisplayName;

        if (value is { IsEventsContainer: true, ElementId: { } eventsId })
        {
            foreach ((ProgramOperationOffer offer, string label) in Offered(eventsId, varId, varName))
                AddOperator(ProgramEventMenu, offer, label,
                    one => AddProgramEventAsync(eventsId, varId, one.Method, one.NameTemplate, one.Note),
                    (two, operand) => AddTwoOperandEventAsync(eventsId, varId, operand.VariableId, operand.Method,
                        two.NameTemplate, operand.Note));
        }
        if (value is { IsCommandsContainer: true, ElementId: { } actionsId })
        {
            foreach ((ProgramOperationOffer offer, string label) in Offered(actionsId, varId, varName))
            {
                switch (offer.Kind)
                {
                    // A command and an arithmetic row are authored identically -- a unary command, or a
                    // second-operand row through the arithmetic-shape command (link1 target, link2 operand). They
                    // differ only in which menu shows them, so only that differs here.
                    case ProgramOperationKind.Command:
                    case ProgramOperationKind.Arithmetic:
                        AddOperator(
                            offer.Kind is ProgramOperationKind.Arithmetic ? ProgramArithmeticMenu : ProgramCommandMenu,
                            offer, label,
                            one => AddProgramCommandAsync(actionsId, varId, one.Method, one.NameTemplate, one.Note),
                            (two, operand) => AddArithmeticAsync(actionsId, varId, operand.Method, operand.VariableId,
                                two.NameTemplate, operand.Note));
                        break;
                    case ProgramOperationKind.Case:
                        ProgramCaseMenu.Add(new ProductMenuItemViewModel(label, "case",
                            new AsyncRelayCommand(() => AddCaseAsync(actionsId, varId))));
                        break;
                    default:
                        break;
                }
            }
        }
        if (value is { IsConditionsContainer: true, ElementId: { } conditionsId })
        {
            foreach ((ProgramOperationOffer offer, string label) in Offered(conditionsId, varId, varName))
                AddOperator(ProgramConditionMenu, offer, label,
                    one => AddConditionAsync(conditionsId, varId, one.Method, one.NameTemplate, one.Note),
                    (two, operand) => AddTwoOperandConditionAsync(conditionsId, varId, operand.VariableId,
                        operand.Method, two.NameTemplate, operand.Note));
        }
    }

    // Every menu is built from the SAME question, asked of the SDK: what may this armed variable be given here.
    // Each kind is labelled where it is handled — an operator by its verb, a case and an arithmetic row from the
    // variable's own name — so nothing has to be smuggled past a lookup that was never meant to label it.
    private IEnumerable<(ProgramOperationOffer Offer, string Label)> Offered(
        ElementId containerId, ElementId variableId, string varName)
    {
        foreach (ProgramOperationOffer offer in session.GetProgramOperations(containerId, variableId))
        {
            switch (offer.Kind)
            {
                case ProgramOperationKind.Case:
                    yield return (offer, $"Case ({varName})");
                    break;
                case ProgramOperationKind.Arithmetic:
                    // The one-operand counter steps read "Tæller + 1"; an operator with operands is a submenu
                    // category, and AddOperator appends the ellipsis every second-operand family carries.
                    yield return (offer, offer.NeedsOperand
                        ? $"{varName} {offer.OperatorSymbol}="
                        : $"{varName} {offer.OperatorSymbol} 1");
                    break;
                default:
                    if (VerbFor(offer) is { } verb)
                        yield return (offer, $"{varName} {verb}");
                    break;
            }
        }
    }

    // The one-operand and two-operand authors of a family report the SAME outcome — the installer sees an event or a
    // condition appear either way — so each family's status is declared once instead of at both call sites.
    private const string EventAddedStatus = "Hændelse tilføjet til programmet.";
    private const string ConditionAddedStatus = "Betingelse tilføjet.";

    private Task<Ihc.OperationOutcome> AddProgramEventAsync(ElementId eventsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddProgramEventAsync), async scope =>
        {
            if (session.Commands.AddProgramEvent(session.Current!, eventsId, variableId, method, name, note) is { } command)
                await applyAndReport(scope, command, EventAddedStatus);
        });

    private Task<Ihc.OperationOutcome> AddProgramCommandAsync(ElementId actionsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddProgramCommandAsync), scope =>
            applyAndReport(scope, session.Commands.AddProgramCommand(session.Current!, actionsId, variableId, method, name, note), "Kommando tilføjet til programmet."));

    private Task<Ihc.OperationOutcome> AddConditionAsync(ElementId conditionsId, ElementId variableId, string method, string name, string note) =>
        runAsync(nameof(AddConditionAsync), scope =>
            applyAndReport(scope, session.Commands.AddCondition(session.Current!, conditionsId, variableId, method, name, note), ConditionAddedStatus));

    private Task<Ihc.OperationOutcome> AddCaseAsync(ElementId commandsId, ElementId switchVariableId) =>
        runAsync(nameof(AddCaseAsync), scope =>
            applyAndReport(scope, session.Commands.AddCase(session.Current!, commandsId, switchVariableId), "Case struktur indsat."));

    private Task<Ihc.OperationOutcome> AddArithmeticAsync(
        ElementId commandsId,
        ElementId targetId,
        string method,
        ElementId operandId,
        string name,
        string note) =>
        runAsync(nameof(AddArithmeticAsync), scope =>
            applyAndReport(scope, session.Commands.AddArithmeticCommand(session.Current!, commandsId, targetId, method, operandId, name, note), "Aritmetisk kommando tilføjet."));

    // T008: the two-operand event / condition authors — the arithmetic peer for the Events/Conditions families
    // (%P <op> %S with the author-chosen operand %S), through the extended AddProgramEvent/AddCondition (link2).
    private Task<Ihc.OperationOutcome> AddTwoOperandEventAsync(ElementId eventsId, ElementId variableId, ElementId operandId, string method, string name, string note) =>
        runAsync(nameof(AddTwoOperandEventAsync), async scope =>
        {
            if (session.Commands.AddProgramEvent(session.Current!, eventsId, variableId, method, name, note, operandId) is { } command)
                await applyAndReport(scope, command, EventAddedStatus);
        });

    private Task<Ihc.OperationOutcome> AddTwoOperandConditionAsync(ElementId conditionsId, ElementId variableId, ElementId operandId, string method, string name, string note) =>
        runAsync(nameof(AddTwoOperandConditionAsync), scope =>
            applyAndReport(scope, session.Commands.AddCondition(session.Current!, conditionsId, variableId, method, name, note, operandId), ConditionAddedStatus));

    // T008: builds one operator menu item into <paramref name="target"/> — a flat item for a 1-operand operation, or
    // a second-operand submenu for a two-operand row (%P <op> %S), reusing the arithmetic submenu shape. The author
    // picks %S from the operands the SDK offered; an operation with none is never offered in the first place, so
    // nothing here has to decide what is eligible. <paramref name="authorOne"/>/<paramref name="authorTwo"/> author
    // the unary / two-operand row.
    private static void AddOperator(ObservableCollection<ProductMenuItemViewModel> target, ProgramOperationOffer offer,
        string label, Func<ProgramOperationOffer, Task> authorOne,
        Func<ProgramOperationOffer, ProgramOperandOffer, Task> authorTwo)
    {
        if (!offer.NeedsOperand)
        {
            target.Add(new ProductMenuItemViewModel(label, offer.Method, new AsyncRelayCommand(() => authorOne(offer))));
            return;
        }
        // The vendor's enumerator popup presents the operator verbatim ("Mode =" / "Mode ->") with submenu chrome;
        // its text does not carry the ellipsis used by OpenVisual's other second-operand families.
        var node = new ProductMenuItemViewModel(offer.Family is ProgramPinFamily.Enum ? label : $"{label} …");
        // No emptiness check: the SDK does not offer a two-operand operation it has no operand for, which is the
        // rule that replaced this shell's own candidate search.
        foreach (ProgramOperandOffer operand in offer.Operands)
            node.Children.Add(new ProductMenuItemViewModel(operand.Name, operand.Method,
                new AsyncRelayCommand(() => authorTwo(offer, operand))));
        target.Add(node);
    }

    // ---- T018: the stray program-authoring handlers consolidated here (US-029/031/033); the view-model keeps the
    // thin [RelayCommand] entry points, delegating their bodies to these. ----

    /// <summary>Adds a Powerup system event to the selected Events group (US-033) — no operand needed.</summary>
    public Task<Ihc.OperationOutcome> AddPowerEventAsync(TreeNodeViewModel? node) => runAsync("AddPowerEvent", async scope =>
    {
        if (node is { IsEventsContainer: true, ElementId: { } eventsId } && session.Current is { } project
            && session.Commands.AddPowerEvent(project, eventsId) is { } command)
            await applyAndReport(scope, command, "Powerup hændelse tilføjet til programmet.");
    });

    /// <summary>Toggles an output's <i>Save current value</i> power-loss persistence (US-033).</summary>
    public Task<Ihc.OperationOutcome> ToggleSaveValueAsync(TreeNodeViewModel? node) => runAsync("ToggleSaveValue", async scope =>
    {
        if (node is { IsOutputPin: true, ElementId: { } outputId } && session.Current is { } project)
            await applyAndReport(scope, session.Commands.SetOutputBackup(project, outputId, !node.IsValueSaved),
                node.IsValueSaved ? "Udgangsværdi gemmes ikke længere ved strømsvigt." : "Udgangsværdi gemmes ved strømsvigt.");
    });

    /// <summary>Adds a new, empty program to a block's Programs group (US-026, uxparity2 W4). A block may hold more
    /// than one program; each arrives with its own events and commands groups, ready to author.</summary>
    public Task<Ihc.OperationOutcome> AddProgramAsync(TreeNodeViewModel? node) => runAsync("AddProgram", async scope =>
    {
        if (node is { Kind: TreeNodeKind.Programs, ElementId: { } id } && session.Current is { } project)
            await applyAndReport(scope, session.Commands.AddProgram(project, id, ProgramDefaultName), "Program indsat.");
    });

    /// <summary>Inserts a conditional sub-program (Conditions + true/false command branches) into a Commands group (US-029).</summary>
    public Task<Ihc.OperationOutcome> AddSubProgramAsync(TreeNodeViewModel? node) => runAsync("AddSubProgram", async scope =>
    {
        if (node is { IsCommandsContainer: true, ElementId: { } id } && session.Current is { } project)
            await applyAndReport(scope, session.Commands.AddSubProgram(project, id), "Under program indsat.");
    });

    /// <summary>Inserts a nested logic group inside a Conditions group for a compound expression (US-029).</summary>
    public Task<Ihc.OperationOutcome> AddLogicGroupAsync(TreeNodeViewModel? node) => runAsync("AddLogicGroup", async scope =>
    {
        if (node is { IsConditionsContainer: true, ElementId: { } id } && session.Current is { } project)
            await applyAndReport(scope, session.Commands.AddLogicGroup(project, id), "Logik gruppe indsat.");
    });

    /// <summary>Combines a Conditions group with OR (<c>&gt;=1</c>) (US-029).</summary>
    public Task<Ihc.OperationOutcome> SetConditionsOrAsync(TreeNodeViewModel? node) => ToggleConditionsAsync(node, or: true);

    /// <summary>Combines a Conditions group with AND (<c>&amp;</c>, the default) (US-029).</summary>
    public Task<Ihc.OperationOutcome> SetConditionsAndAsync(TreeNodeViewModel? node) => ToggleConditionsAsync(node, or: false);

    private Task<Ihc.OperationOutcome> ToggleConditionsAsync(TreeNodeViewModel? node, bool or) => runAsync("ToggleConditionsAsync", async scope =>
    {
        if (node is { IsConditionsContainer: true, ElementId: { } id } && session.Current is { } project)
            await applyAndReport(scope, session.Commands.SetConditionsLogic(project, id, or),
                or ? "Betingelser kombineret med OR (>=1)." : "Betingelser kombineret med AND (&).");
    });

    /// <summary>Adds a case value branch to the selected Case node (US-031): prompts for the criterion value, then
    /// inserts a command group tagged with it (filled by the normal Add-command gesture). For an ENUM-keyed case the
    /// criterion must name one of the type's states (T014) — they are surfaced in the prompt and a non-state is
    /// reported rather than silently dropped.</summary>
    public Task<Ihc.OperationOutcome> NewCaseValueAsync(TreeNodeViewModel? node) => runAsync("NewCaseValue", async scope =>
    {
        if (node is not { IsCaseNode: true, ElementId: { } caseId } || session.Current is not { } project)
            return;
        // An enum switch takes one of its type's STATE names as the criterion (the gateway rejects any other value);
        // surface the states so the user enters a real one. A literal switch (counter/integer/…) takes a free value.
        IReadOnlyList<string> states = EnumSwitchStates(project, caseId);
        string title = states.Count > 0 ? $"Ny case værdi ({string.Join(", ", states)})" : "Ny case værdi";
        PropertiesResult? result = await dialogs.EditPropertiesAsync(title, string.Empty, string.Empty);
        if (result is null)
            return;
        // Blank: the shared required-field decision, reported rather than swallowed (T045, va-ana G2).
        if (session.MissingRequiredField(result.Name) is { } blank)
        {
            setStatus(blank.Message);
            return;
        }
        string criterion = result.Name.Trim();
        // Not a state: the SDK says so, in its own words. This site used to re-derive the sentence AND re-list the
        // states behind it from the very data the factory reads — two answers to one question, and only one of them
        // could be right after a rename.
        if (session.Commands.TryAddCaseValue(project, caseId, criterion,
                out AddCaseValue? command, out Problem? refusal))
        {
            await applyAndReport(scope, command, $"Case værdi '{criterion}' tilføjet.");
        }
        else
        {
            setStatus(refusal.Message);
        }
    });

    // The state names of an enum-keyed case's switch (US-031/T014), or an empty list when the switch is not an enum.
    // The case→switch hop is this method's own; the switch→type→states walk is the SDK's one EnumVariableView, so
    // the list offered in the prompt is resolved exactly the way the gateway resolves the list it accepts.
    private static IReadOnlyList<string> EnumSwitchStates(Project project, ElementId caseId) =>
        SwitchOf(project, caseId) is { } switchVar ? new EnumVariableView(project, switchVar).States : [];

    // The switch variable an enum-keyed case is wired to, or null.
    private static ProjectElement? SwitchOf(Project project, ElementId caseId) =>
        project.FindById(caseId) is { } kase
        && ElementId.TryParse(project.View(kase).Effective("link"), out ElementId switchId)
            ? project.FindById(switchId)
            : null;
}
