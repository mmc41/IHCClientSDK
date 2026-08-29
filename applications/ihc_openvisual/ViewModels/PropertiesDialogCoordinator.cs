using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-8: the per-node-type <i>Properties</i> dialog flows, extracted from
/// <see cref="MainWindowViewModel"/> (C# 12 primary ctor). <see cref="OpenAsync"/> is the node dispatch; each flow
/// reads the element through a typed SDK read view (<see cref="PinView"/>/<see cref="ProductView"/>/
/// <see cref="DimmerView"/> or <see cref="ElementView"/>), opens its dialog through
/// <see cref="IDialogService"/>, and applies the result as a command via <paramref name="applyAndReport"/> — the
/// view-model's single outcome→status/dialog rule (<paramref name="setStatus"/> serves the one flow, pin
/// addressing, that reports a bespoke message). No raw schema attribute reads remain in this layer.
/// </summary>
internal sealed class PropertiesDialogCoordinator(
    ProjectWorkflow session,
    IDialogService dialogs,
    Func<ProjectCommand, string, Task> applyAndReport,
    Action<string> setStatus)
{
    /// <summary>Opens the properties dialog appropriate to the element's type (the node dispatch, US-044). A modem, a
    /// product, a data-line pin, a scenes container, a scene value, an enum variable, and a locality/function block
    /// each route to their own flow.</summary>
    public Task OpenAsync(ElementId id) => OpenAsync(id, null);

    /// <summary>
    /// THE node dispatch — one ladder, whether the installer arrived by gesture or by a route's dialog leg.
    /// <para>The two used to be written out separately, which made every new element kind two edits in the right
    /// relative order and let the row's promise and the click's destination disagree over an element only one of
    /// them classified. A hop adds a focus and a product arrival to the same ladder; it does not re-walk it.</para>
    /// </summary>
    /// <param name="hop">The route leg being carried out, or null for the plain gesture.</param>
    private async Task OpenAsync(ElementId id, DialogHop? hop)
    {
        if (session.Current is not { } project || project.FindById(id) is not { } element)
            return;
        // Where a route wants the caret, and null for the plain gesture — every flow below already defaults to
        // opening without landing on a particular control.
        string? focus = hop?.Attribute;
        // EVERY product family — wired, wireless, modem, LED dimmer, S0 — opens the same dialog on its own
        // composed descriptor (T030). There is no per-family branch left to get wrong.
        if (ProductClassifier.IsProduct(element.Tag))
            await OpenComposedDialogAsync(id, hop);
        else if (element.Kind == ElementKind.DatalinePin)
            await OpenPinAsync(id, element, focus);
        else if (element.IsScenesContainer)
            // The product's Scenarier dialog (US-024). No focus: its one editable field is the note, and the
            // dialog already opens on it.
            await OpenSceneContainerAsync(id, element);
        else if (element.IsSceneMember && !element.IsSceneShutter)
            await OpenSceneValueAsync(id, element, focus);   // edit a scenario link's value (US-058)
        else if (element.Kind is ElementKind.Resource or ElementKind.EnumResource)
            // An ordinary FB resource variable edits Name/Note plus its typed initial value (US-026/US-027,
            // T015/T016), and F-50: an enum VARIABLE opens that same dialog rather than the type editor, with an
            // Initial værdi combo of its TYPE's states. Opening the type editor from the row (which is what this
            // used to do) both hid the variable's own fields and put a project-global edit behind the ordinary
            // gesture on one variable.
            await OpenVariableAsync(id, element, focus);
        else if (element.Tag == "conditions")
            // A Betingelser group edits Name/Note AND the operator its conditions combine with (F-48).
            await OpenConditionsAsync(id, element, focus);
        else if (element.IsLocalityGroup || element.Kind is ElementKind.FunctionBlock)
            // A function block renames through the same Name/Note dialog as a locality (US-007/US-019).
            await OpenNameNoteAsync(id, project.View(element).Name ?? string.Empty, focus);
    }

    /// <summary>
    /// Edits a <c>Betingelser</c> group: its Name, its Note, and the logical operator its conditions combine with
    /// (alignment F-48). The reference application's <c>Rediger Betingelser egenskaber</c> holds exactly those
    /// three, the operator as a captioned <i>Logisk betingelse</i> combo of AND/OR (measured 2026-08-11).
    /// <para>OpenVisual reached the operator from the flyout already; it had no dialog at all here, so
    /// <i>Egenskaber…</i> on this row did nothing and the group's Name and Note were unreachable.</para>
    /// </summary>
    private async Task OpenConditionsAsync(
        ElementId id, ProjectElement element, string? focusAttribute = null)
    {
        if (session.Current is not { } project)
            return;
        ElementView view = project.View(element);
        // The tree names every conditions container "Betingelser" whatever the element carries, and the original
        // titles the dialog from the name it shows — so an unnamed group still reads "Rediger Betingelser
        // egenskaber" rather than "Rediger  egenskaber".
        string name = view.Name is { Length: > 0 } stored ? stored : ConditionsDisplayName;
        bool or = view.Effective("type") == "or";

        PropertiesResult? result = await dialogs.EditPropertiesAsync(
            $"Rediger {name} egenskaber", name, view.Note ?? string.Empty, conditionsOr: or,
            focus: ElementFieldFor(focusAttribute));
        if (result is null)
            return;   // cancelled

        await applyAndReport(session.Commands.RenameLocality(project, id, result.Name, result.Note),
            $"'{result.Name}' blev opdateret.");
        // A separate command because it is a separate attribute, and applied only when it actually CHANGED, so an
        // untouched dialog leaves no second entry in the undo history — the same rule the variable dialog's
        // power-loss flag follows.
        if (result.ConditionsOr is { } chosen && chosen != or && session.Current is { } updated)
            await applyAndReport(session.Commands.SetConditionsLogic(updated, id, chosen),
                chosen ? "Betingelser kombineret med OR (>=1)." : "Betingelser kombineret med AND (&).");
    }

    // The label the tree gives every conditions container, and the original's own stored name for a fresh one.
    private const string ConditionsDisplayName = "Betingelser";

    /// <summary>The original's fixed caption for a function block's properties dialog — the node TYPE, not the
    /// block's name (measured 2026-08-11 on a locked library block and an empty one alike, alignment F-49).</summary>
    private const string FunctionBlockDialogTitle = "Funktionsblok egenskaber";

    /// <summary>
    /// Opens the dialog a JUST-PLACED product raises, and reports whether the installer committed it. Placing a
    /// product asks for its documentation as part of placing it and cancelling places nothing (US-011, uxparity
    /// S-12) — so the insert path needs both the right dialog for the type (a modem raises the modem dialog, not
    /// the generic product one — measured: IHC Visual opens "SMS Modem Egenskaber") and the yes/no answer.
    /// <para><b>The answer is whether the installer ACCEPTED the dialog, never whether the edit changed
    /// anything.</b> Pressing OK without touching a field is an ordinary act and produces
    /// <c>EditStatus.NoChange</c>; the caller rolls the insert back when this returns false, so reporting a
    /// commit status here would delete a product the installer had just placed and accepted. Only Cancel — a
    /// null dialog result — is false. Pinned by
    /// <c>MainWindowViewModelTests.InsertThenOkWithoutEditing_KeepsTheProduct</c>.</para>
    /// </summary>
    public async Task<bool> OpenForInsertAsync(ElementId id)
    {
        if (session.Current is not { } project || project.FindById(id) is null)
            return false;
        return await OpenComposedDialogAsync(id);
    }

    /// <summary>Edits a locality's or function block's Name and Note through the shared dialog and generic rename
    /// command (US-007/US-019) — refused inside a locked block by T003.</summary>
    public async Task OpenNameNoteAsync(ElementId id, string currentName, string? focusAttribute = null)
    {
        if (session.Current is not { } project)
            return;
        ProjectElement? element = project.FindById(id);
        string currentNote = element is not null ? project.View(element).Note ?? string.Empty : string.Empty;
        // Title format follows the vendor's own dialog: "Rediger <navn> egenskaber" (measured on a locality
        // 2026-08-09, alignment F-16) — not "Rediger egenskaber for <navn>".
        // F-24: a FUNCTION BLOCK's dialog captions the editable pair, as the vendor's does — on a library block, an
        // unlocked one and an empty one alike. A locality's does not: the vendor groups that dialog differently
        // (one captioned box per field), so a caption here would invent grouping it does not have.
        bool isBlock = element is { Kind: ElementKind.FunctionBlock };
        string? userGroup = isBlock ? "Bruger egenskaber" : null;
        // F-49: the title pattern is per NODE TYPE, not one rule. A locality is named ("Rediger Stue
        // egenskaber"); a function block is not — the original captions its dialog "Funktionsblok egenskaber",
        // by the type, whatever the block is called (measured 2026-08-11). Applying the locality's pattern here
        // was a generalization from the one node type F-16 happened to measure.
        string title = isBlock ? FunctionBlockDialogTitle : $"Rediger {currentName} egenskaber";
        PropertiesResult? result = await dialogs.EditPropertiesAsync(
            title, currentName, currentNote, OriginOf(project, element),
            userGroupCaption: userGroup, focus: ElementFieldFor(focusAttribute));
        if (result is null)
            return;   // cancelled — the locality keeps its original name and note
        await applyAndReport(session.Commands.RenameLocality(project, id, result.Name, result.Note),
            $"Omdøbt til {result.Name}.");
    }

    /// <summary>
    /// The read-only provenance to show under the editable fields: where a LIBRARY function block came from
    /// (uxparity S-19). Null for a locality and for a block authored from scratch — neither has a master.
    /// </summary>
    private static LibraryOrigin? OriginOf(Project project, ProjectElement? element)
    {
        LibraryOrigin? origin = null;
        if (element is { Kind: ElementKind.FunctionBlock } block
            && new FunctionBlockView(project, block) is { IsLibraryBlock: true } view)
            origin = new LibraryOrigin(
                view.MasterName ?? string.Empty,
                view.MasterType ?? string.Empty,
                view.MasterVersion ?? string.Empty,
                view.MasterDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
                view.MasterProgrammer ?? string.Empty);
        return origin;
    }

    /// <summary>Edits an ordinary FB resource variable's Name, Note, and typed initial value (US-026/US-027, T015/T016)
    /// through the variable dialog, applying all three as one undoable step (refused inside a locked block by T003).
    /// The value control shown depends on the variable's type; a type with no editable initial value edits Name/Note
    /// only.</summary>
    public async Task OpenVariableAsync(
        ElementId id, ProjectElement variable, string? focusAttribute = null)
    {
        if (session.Current is not { } project)
            return;
        ElementView view = project.View(variable);
        // W5: a variable carries TWO documentation fields — the function documentation and the installer help text
        // (note-2) — so both are pre-filled from the project and both are applied.
        // F-27: the power-loss flag is a property of the VARIABLE and belongs in its dialog, where the vendor puts
        // it — not only on the context flyout, which is where OpenVisual had it alone.
        bool backupBefore = view.Backup;
        // An enum's states and current one come from its TYPE; null for every other variable.
        (string Name, List<string> States)? enumInfo =
            variable.Kind == ElementKind.EnumResource ? ReadEnumInfo(id) : null;
        string? currentEnumState = enumInfo is null ? null : project.EnumStateName(variable);
        // An enum's editable value is WHICH STATE it starts in — a Choice over its type's states, carrying the
        // state's LABEL because the stored value is an IDREF this layer resolves on both sides.
        ResourceInitialValue current = enumInfo is { } ei
            ? ResourceInitialValue.OfChoice(currentEnumState ?? ei.States.FirstOrDefault() ?? string.Empty)
            : ReadInitialValue(variable);
        VariablePropertiesResult? result = await dialogs.EditVariablePropertiesAsync(new VariablePropertiesInput(
            $"Rediger {view.Name} egenskaber", view.Name ?? string.Empty, view.Note ?? string.Empty,
            current, view.HelpNote ?? string.Empty, backupBefore,
            // F-42: only the types that DECLARE a millisecond get the field. resource_time has none, and the
            // writer refuses to write one for it — so showing the box offered an edit that was then discarded.
            ShowMilliseconds: variable.Tag is not "resource_time",
            DecimalPlaces: DecimalPlacesFor(variable.Tag),
            // F-50: an enum offers its own TYPE's states; every other type leaves this null and the dialog uses
            // what the format declares.
            ChoiceOptions: enumInfo?.States,
            // Where a ROUTE asked the caret to land. Null for the ordinary Egenskaber, which keeps opening on
            // the name.
            Focus: VariableFieldFor(focusAttribute)));
        if (result is null)
            return;   // cancelled
        // An enum's initial state is NOT written by the generic value writer: its inivalue is an IDREF to one of
        // its type's enum_value elements, and the writer would store the state's name and break the reference.
        // The name/note/help edit goes through as usual with the value suppressed, and the state follows below as
        // its own command (F-50).
        ResourceInitialValue applied = enumInfo is null ? result.Value : ResourceInitialValue.None;
        await applyAndReport(
            session.Commands.SetVariableProperties(project, id, result.Name, result.Note, applied, result.HelpNote),
            $"'{result.Name}' blev opdateret.");
        // Applied only when the state actually CHANGED, so an untouched dialog leaves one undo entry, not two.
        if (enumInfo is { } info && result.Value.Kind == ResourceValueKind.Choice
            && info.States.IndexOf(result.Value.Token) is >= 0 and int chosen
            && chosen != info.States.IndexOf(currentEnumState ?? string.Empty)
            && session.Current is { } withEnum)
        {
            await applyAndReport(session.Commands.SetEnumInitialState(withEnum, id, info.Name, chosen),
                $"Starttilstanden blev sat til {result.Value.Token}.");
        }
        // "Rediger" beside the state list: the variable's own edits are applied above, and THEN the shared type
        // editor opens — the original's second dialog, reached by a deliberate button rather than by the row's
        // ordinary gesture (F-50).
        if (result.EditEnumType && enumInfo is not null)
            await OpenEnumAsync(id);
        // A separate command because it is a separate attribute; applied only when it actually changed, so an
        // untouched dialog leaves no second entry in the undo history.
        if (result.SaveOnPowerLoss != backupBefore && session.Current is { } updated)
            await applyAndReport(session.Commands.SetOutputBackup(updated, id, result.SaveOnPowerLoss),
                result.SaveOnPowerLoss
                    ? "Værdien gemmes ved strømsvigt."
                    : "Værdien gemmes ikke længere ved strømsvigt.");
    }

    // Maps a resource variable's tag + current attributes to its typed initial value (US-027, T016) — the inverse of
    // SetResourceInitialValue's serialization, used to pre-fill the dialog. An unrecognised type has no editable value.
    private static ResourceInitialValue ReadInitialValue(ProjectElement variable)
    {
        switch (variable.Tag)
        {
            case "resource_flag":
            case "resource_input":
            case "resource_output":
            // F-41: a Helligdag is on/off like a flag — the reference application's Helligdag dialog offers a
            // combo holding exactly OFF/ON (read live), and the DTD declares `inivalue (on | off)` on
            // resource_holiday just as on resource_flag. The engine's bool writer is tag-agnostic, so this type
            // was simply not listed and fell through to "no editable initial value".
            case "resource_holiday":
                return ResourceInitialValue.OfBool(variable.GetAttribute("inivalue") == "on");
            // F-41: a weekday's initial value is one of seven enumerated tokens. An ABSENT inivalue is the DTD
            // default (monday), not "no value" — the format omits an attribute sitting at its default, so a
            // reader that treated missing as empty would show the wrong day.
            case "resource_weekday":
                return ResourceInitialValue.OfChoice(
                    variable.GetAttribute("inivalue") is { Length: > 0 } day ? day : "monday");
            // F-41: a date's editable value is its DAY and MONTH. The type also stores a year, which the
            // original's dialog does not offer (its picker reads "01 January") and which the writer therefore
            // leaves untouched. Both default to 1, matching the catalog template's year="2000" month="1" day="1".
            case "resource_date":
                return ResourceInitialValue.OfDate(Int(variable, "day", 1), Int(variable, "month", 1));
            case "resource_counter":
            case "resource_integer":
            // F-41: the INTEGER-valued types — the ones whose DTD default is a bare "0". Confirmed at byte level:
            // the reference application saved inivalue="17" for a Tal and "42" for a Lys. The UNIT is never in the
            // dialog: the field holds the bare number and the unit belongs to the tree row (42 Lux, 42%).
            case "resource_light":
            case "resource_light_level":
                return ResourceInitialValue.OfNumber(
                    long.TryParse(variable.GetAttribute("inivalue"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n : 0);
            // F-41/F-44: the DECIMAL family — exactly the types whose DTD default is "0.00", which is NOT the set
            // the dialog's appearance suggests. W and Wh show a whole number and round what is typed (42,7 gave a
            // row reading 43W), yet the bytes the reference application saved were inivalue="43.00" and "7.00",
            // never "43" — so they serialise through the decimal writer, not the integer one. How many decimals
            // the FIELD shows is a separate, per-type matter and travels as DecimalPlaces.
            case "kW":
            case "kWh":
            case "W":
            case "Wh":
            case "resource_floating_point":
            case "resource_humidity_level":
            case "resource_temperature":
                return ResourceInitialValue.OfDecimal(
                    double.TryParse(variable.GetAttribute("inivalue"), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0);
            case "resource_timer":
            case "resource_time":
            // F-41: a Timertid takes the same editor. The reference application shows 00:00:00,000 for both
            // resource_timer and resource_timertime (and 00.00.00 for resource_time), and the DTD gives the first
            // two hour/minute/second/millisecond #REQUIRED — the writer drops the millisecond only for the type
            // that declares none.
            case "resource_timertime":
                return ResourceInitialValue.OfTime(Int(variable, "hour"), Int(variable, "minute"), Int(variable, "second"), Int(variable, "millisecond"));
            default:
                return ResourceInitialValue.None;
        }
    }

    // How many decimals the VALUE FIELD shows, per type (F-41) — measured field by field in the reference
    // application: kW/kWh read 0,000, Kommatal 0,00, Fugtighed/Temperatur 0,0, and W/Wh a plain 0. It also rounds
    // what the user types, which is how a W turns 42,7 into 43.
    //
    // Deliberately its own table rather than a read of VariableValueFormat's row precisions. The two agree today,
    // but they are separate surfaces measured separately, and this campaign has already found them differing in
    // kind: the unit appears only in the row, never in the field.
    private static int DecimalPlacesFor(string tag) => tag switch
    {
        "kW" or "kWh" => 3,
        "resource_floating_point" => 2,
        "resource_humidity_level" or "resource_temperature" => 1,
        "W" or "Wh" => 0,
        _ => 2,
    };

    private static int Int(ProjectElement variable, string attribute, int fallback = 0) =>
        int.TryParse(variable.GetAttribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;

    // The product's scene container (US-024): its fixed name, its note, and a row per membership naming the
    // scenario, the function block driving it and that block's locality — the same triple the membership's link row
    // shows as a path, split into columns.
    public async Task OpenSceneContainerAsync(ElementId scenesId, ProjectElement scenes)
    {
        var rows = new List<SceneContainerRow>();
        foreach (ProjectElement member in scenes.Children)
        {
            if (!member.IsSceneMember)
                continue;
            IReadOnlyList<string> parts = TreeLabelFormatter.LinkOppositeParts(session.Current!, member);
            (string value, string ramp) = TreeLabelFormatter.SceneMemberValue(member);
            rows.Add(new SceneContainerRow(
                SceneName: parts.Count > 2 ? parts[2] : string.Empty,
                FunctionBlock: parts.Count > 1 ? parts[1] : string.Empty,
                Locality: parts.Count > 0 ? parts[0] : string.Empty,
                Value: value, RampTime: ramp));
        }
        ElementView scenesView = session.Current!.View(scenes);
        string name = scenesView.Name ?? "Scenarier";
        SceneContainerResult? result = await dialogs.EditSceneContainerAsync(
            new SceneContainerInput(name, scenesView.Note ?? string.Empty, rows));
        if (result is null)
            return;
        await applyAndReport(session.Commands.UpdateSceneContainer(session.Current!, scenesId, result.Note),
            $"'{name}' blev opdateret.");
    }

    public async Task OpenSceneValueAsync(
        ElementId memberId, ProjectElement member, string? focusAttribute = null)
    {
        if (!SceneValue.TryParse(member, out SceneValue sv))
            return;
        bool isDimmer = sv.Kind == SceneValueKind.Dimmer;
        int ms = (int)sv.RampTime.TotalMilliseconds;
        // F-49: the original titles this dialog by the MEMBER's type — "Relæ scenarie egenskaber" for a relay
        // member, "Lysdæmper scenarie egenskaber" for a dimmer one (both measured live 2026-08-11 by wiring a
        // block's scene pin to a Lampeudtag and to a Lampeudtag dimmer). One fixed caption, as this had, cannot
        // tell the installer which of the two dialogs is open.
        var input = new SceneValueInput(SceneValueTitles.For(isDimmer), isDimmer, sv.On, sv.LevelPercent,
            ms / 60000, ms / 1000 % 60, SceneValue.LevelConstraint, SceneValue.RampPartConstraint,
            Focus: SceneFieldFor(focusAttribute));

        SceneValueResult? result = await dialogs.EditSceneValueAsync(input);
        if (result is null)
            return;
        await applyAndReport(session.Commands.UpdateSceneValue(session.Current!, memberId, result),
            "Scenarieværdien blev opdateret.");
    }

    public async Task OpenEnumAsync(ElementId enumVariableId)
    {
        if (ReadEnumInfo(enumVariableId) is not { } info)
            return;
        EnumDefinitionResult? result = await dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput($"Rediger {info.Name}", info.Name, info.States, IsNew: false));
        if (result is null)
            return;
        if (session.Commands.UpdateEnumStates(session.Current!, enumVariableId, result.States) is { } command)
            await applyAndReport(command, $"Enumeratoren '{info.Name}' blev opdateret.");
    }

    // Reads an enum variable's type name and ordered state names for the Edit dialog (US-030); null if not an enum.
    private (string Name, List<string> States)? ReadEnumInfo(ElementId enumVariableId) =>
        session.Current is { } project && project.FindById(enumVariableId) is { } variable
        && new EnumVariableView(project, variable) is { TypeName: { } typeName } view
            ? (typeName, view.States.ToList())
            : null;

    /// <summary>
    /// Carries out a route's DIALOG leg — the plan the panel already worked out, never re-derived here.
    /// <para>Re-deriving it would give the row's promise and the click's destination two authors, which is the
    /// one thing the planner exists to prevent. This turns an answer into an arrival and decides nothing.</para>
    /// </summary>
    /// <param name="hop">Whose dialog opens, which element carries the value, and which field to land on.</param>
    public Task ExecuteAsync(DialogHop hop) => OpenAsync(hop.Owner, hop);

    /// <summary>
    /// Where the product dialog should open for this hop — §5.4's four combinations, read off the pair the plan
    /// already carries rather than re-decided.
    /// </summary>
    /// <remarks>
    /// The field id is looked up in the COMPOSED descriptor. A hop naming an attribute the dialog does not offer
    /// therefore focuses nothing, which is the same honest answer the planner gives when it degrades such a
    /// route to dialog-level — the two cannot disagree, because both ask the descriptor.
    /// </remarks>
    private ProductDialogShowOptions ArrivalFor(
        Project project, ProductDialogDescriptor descriptor, DialogHop hop)
    {
        // A FIELD OF THIS DIALOG, wherever its value lives. Asked first, and asked about the pair rather than
        // about whether the site is the owner: a dimmer's settings are composed as ordinary fields bound to
        // descendant elements, so a hop naming one has Site != Owner and is still a plain focus. Deciding by
        // Site == Owner sent every one of them down the terminal branch below, which selected a row that does
        // not exist and focused nothing.
        if (hop.Attribute is { } attribute
            && descriptor.AllFields
                .FirstOrDefault(f => f.Target == hop.Site && f.Attribute == attribute) is { } offered)
        {
            return new ProductDialogShowOptions(FocusAutomationId: offered.AutomationId);
        }

        if (hop.Site == hop.Owner)
        {
            // The owner's own dialog, and it offers no field for this attribute — so the honest arrival is the
            // plain open, which is the same answer the planner gave when it degraded the route.
            return ProductDialogShowOptions.None;
        }

        // A sub-item: select its row, and — when the hop names a field — step into it as the installer would,
        // landing on that field. With no attribute the row is selected and nothing is stepped into, which is how
        // a finding whose fix is not a field still lands on the right sub-item.
        //
        // WHICH grid depends on what the sub-item IS. A setting and a terminal are both rows of the same dialog
        // and both reached by stepping into them, but they are different lists with different selections, so a
        // single "sub-item" answer would have pointed a constant's route at the terminal grid.
        if (project.FindById(hop.Site) is { } site
            && ProductRows.IsSetting(site.GetAttribute(ProductRows.SettingAttribute)))
        {
            return new ProductDialogShowOptions(
                SelectSettingId: hop.Site.ToToken(),
                InitialAction: hop.Attribute is null
                    ? null
                    : new ProductDialogWidgetAction(DialogWidgetKind.SettingsGrid, hop.Site));
        }

        return new ProductDialogShowOptions(
            SelectTerminalPin: hop.Site.ToToken(),
            InitialAction: hop.Attribute is null
                ? null
                : new ProductDialogWidgetAction(DialogWidgetKind.TerminalGrids, hop.Site));
    }

    /// <summary>
    /// Opens the ONE generic product dialog on a composed descriptor and applies whatever the installer changed.
    /// <para>Nothing family-specific happens here. The composer decided the title, the groups, every field's
    /// caption, current value, validation rule and write target; this reads none of them and touches no attribute
    /// — which is the whole point of the metadata engine and the reason the modem no longer needs a flow of its
    /// own (T029). Returns <c>false</c> only on cancel: an untouched OK is an ordinary commit with nothing in it,
    /// and the insert path rolls back on <c>false</c>.</para>
    /// </summary>
    /// <param name="hop">The route leg that opened it, or null for the plain gesture: it decides where the
    /// dialog arrives and which field a step into a sub-item lands on.</param>
    private async Task<bool> OpenComposedDialogAsync(ElementId productId, DialogHop? hop = null)
    {
        if (session.Current is not { } project)
            return false;
        ProductDialogDescriptor descriptor = session.GetProductDialog(productId);
        if (descriptor.Groups.IsEmpty)
            return false;   // nothing composed for this element — no dialog to open

        // Composed ONCE and read twice: the arrival is a question about this descriptor's fields, so asking the
        // composer again for a second copy of it would be the same answer at the price of a second compose.
        ProductDialogShowOptions? arrival = hop is { } at ? ArrivalFor(project, descriptor, at) : null;

        // The terminal rows are element DATA, not dialog metadata: the descriptor says whether the grids
        // apply, and this supplies what goes in them. Read once here rather than inside the dialog, which
        // has no project to read.
        ProductView? productView =
            project.FindById(productId) is { } el ? new ProductView(project, el) : null;
        IReadOnlyList<ProductTerminal> terminals =
            productView is { } pv ? BuildTerminals(pv) : [];
        // Same rule as the terminals: the descriptor says whether the Indstillinger grid appears, and
        // these are the rows that go in it (T070).
        //
        // The VALUE is rendered by the app's existing per-type formatter, not printed raw. The vendor
        // shows a calibration offset as "0,0 °C" -- Danish decimal comma, one place, unit -- and
        // VariableValueFormat already produces exactly that for resource_temperature (F-41/F-44). The
        // raw inivalue is "0.00", so printing it directly showed the stored form rather than the
        // displayed one, which is a display-interpretation concern and belongs on this side of the
        // boundary (ADR-002).
        IReadOnlyList<ProductSetting> settings = productView is { } sv ? BuildSettings(project, sv) : [];

        // THE VISIT'S PENDING STATE. A sub-dialog's OK lands here, not in the document: the whole visit is
        // one transaction, so nothing is written until the product dialog's own OK — and Annuller discards
        // the terminal addressing along with everything else, because the installer cancelled the act they
        // were performing, not half of it.
        Dictionary<ElementId, PinPropertiesResult> pendingTerminals = [];

        // The constants edited in Rediger konstant, under the same rule (T040). Held as the installer's TEXT:
        // the command that writes them owns the format.
        Dictionary<ElementId, string> pendingSettings = [];

        // The visit's sub-dialogs open OVER this window, which stays on screen — so the step handler is
        // where a composite is entered, not a result the dialog closes itself to report.
        // The route's nested focus applies to the step the ROUTE opened, not to whatever the installer does
        // next: consumed on first use, so a second Konfigurer in the same visit opens plainly.
        string? routeFocus = hop?.Attribute;
        ProductDialogEdits? result = await dialogs.EditProductDialogAsync(
            descriptor, terminals, settings, arrival,
            onStep: action =>
            {
                string? focus = routeFocus;
                routeFocus = null;
                return StepIntoAsync(productId, action, pendingTerminals, pendingSettings, focus);
            });
        if (result is null)
            return false;   // cancelled — the product keeps its documentation AND its addressing

        // The status line names the product as the installer will see it in the tree a moment later — so the
        // name AFTER the edit when they renamed it, not the descriptor's title (which is the product TYPE).
        string nameNow = project.FindById(productId) is { } element
            ? project.View(element).Name ?? string.Empty
            : string.Empty;
        string name = result.Edits
            .Where(edit => edit.Target == productId && edit.Attribute == "name")
            .Select(edit => edit.Value)
            .LastOrDefault() ?? nameNow;

        // ONE command for the whole visit: the product's fields, every terminal addressed inside it, and every
        // constant edited in it. One undo entry, so Fortryd afterwards takes back the act the installer
        // actually performed.
        await applyAndReport(
            session.Commands.ApplyProductDialogVisit(
                project, productId, result.Edits,
                [.. pendingTerminals.Select(e => new ProductDialogTerminalEdit(e.Key, e.Value))],
                [.. SettingEdits(project, pendingSettings)]),
            $"{name} blev opdateret.");

        return true;
    }

    /// <summary>
    /// A composite the installer stepped into WHILE the product dialog is open. It runs over that dialog and
    /// returns to it — the window is never closed, so nothing it holds is lost on the way in or out.
    /// </summary>
    private async Task<ProductDialogRefresh?> StepIntoAsync(
        ElementId productId, ProductDialogWidgetAction action,
        Dictionary<ElementId, PinPropertiesResult> pendingTerminals,
        Dictionary<ElementId, string> pendingSettings, string? focusAttribute = null)
    {
        switch (action)
        {
            case { Kind: DialogWidgetKind.TerminalGrids, Target: { } pinId }
                when session.Current?.FindById(pinId) is { Kind: ElementKind.DatalinePin } pin:
                // Into the VISIT, not into the document. The last answer for a terminal wins, which is what
                // re-opening the same row and changing your mind means.
                await OpenPinAsync(pinId, pin, focusAttribute,
                    collect: values => pendingTerminals[pinId] = values);
                return Project(productId, pendingTerminals, pendingSettings);
            case { Kind: DialogWidgetKind.SettingsGrid, Target: { } settingId }
                when Displayed(settingId, pendingSettings) is { } shown:
                // Rediger konstant, into the same pending state and by the same rule (T040). What comes back is
                // the installer's TEXT, kept as text: the command that writes it owns the format, and a
                // conversion here would be a second reading of it.
                if (await dialogs.EditConstantAsync(
                        new ConstantEditorInput(settingId, shown.Name, shown.Value)) is { } accepted)
                {
                    pendingSettings[settingId] = accepted;
                }
                return Project(productId, pendingTerminals, pendingSettings);
        }
        return null;
    }

    /// <summary>
    /// The settings row as it stands in the visit — the pending value where the installer has already edited it,
    /// the document's otherwise. Null when the element is not a setting of this product any more.
    /// </summary>
    private ProductSetting? Displayed(ElementId settingId, IReadOnlyDictionary<ElementId, string> pending)
    {
        if (session.Current is not { } project || project.FindById(settingId) is not { } element)
            return null;
        ElementView view = project.View(element);
        return Overlay(
            new ProductSetting(
                view.Name ?? string.Empty,
                view.Note ?? string.Empty,
                VariableValueFormat.For(element.Tag, view.Effective) ?? string.Empty,
                settingId),
            pending);
    }

    /// <summary>
    /// The constants the visit edited, as typed values the command can write.
    /// <para>The installer's text is turned back into a value HERE, on the presentation side: the grid rendered
    /// the value through this application's per-type format table, so reading it back is the same table's job
    /// (ADR-002). The SDK is handed a <see cref="ResourceInitialValue"/> of the setting's own kind, never a
    /// string it would have to interpret.</para>
    /// <para>Text that names no number is DROPPED rather than written as zero — the row keeps the value it had,
    /// which is what the installer sees when they come back out of the editor.</para>
    /// </summary>
    private IEnumerable<ProductDialogSettingEdit> SettingEdits(
        Project project, IReadOnlyDictionary<ElementId, string> pending)
    {
        foreach ((ElementId id, string text) in pending)
        {
            if (project.FindById(id) is { } element
                && Retyped(ReadInitialValue(element), text) is { } value)
            {
                yield return new ProductDialogSettingEdit(id, value);
            }
        }
    }

    /// <summary>
    /// The installer's text as a value of the SAME kind the setting already holds — its kind is the element's,
    /// never guessed from how the text looks.
    /// </summary>
    /// <remarks>
    /// The leading number is taken and the rest ignored, because the box is pre-filled with the row's rendered
    /// value and that carries a unit ("0,0 °C") the installer may well leave in place. Danish first, since the
    /// text came from a Danish-formatted row; invariant after, so a typed period is not a syntax error.
    /// </remarks>
    private static ResourceInitialValue? Retyped(ResourceInitialValue current, string text) =>
        LeadingNumber(text) is not { } number
            ? null
            : current.Kind switch
            {
                ResourceValueKind.Decimal => ResourceInitialValue.OfDecimal(number),
                ResourceValueKind.Number => ResourceInitialValue.OfNumber((long)Math.Round(number)),
                _ => null,   // a setting of a kind no editor offers is not written by one
            };

    private static double? LeadingNumber(string text)
    {
        string trimmed = new([.. text.Trim().TakeWhile(c =>
            char.IsAsciiDigit(c) || c is '-' or '+' or ',' or '.')]);
        return double.TryParse(trimmed, NumberStyles.Float, DanishCulture, out double danish) ? danish
            : double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double invariant)
                ? invariant
                : null;
    }

    private static readonly CultureInfo DanishCulture = CultureInfo.GetCultureInfo("da-DK");

    /// <summary>The visit's pending value laid over a row, or the row unchanged where there is none.</summary>
    private static ProductSetting? Overlay(
        ProductSetting? row, IReadOnlyDictionary<ElementId, string> pending) =>
        row is not null && pending.TryGetValue(row.Id, out string? edited)
            ? row with { Value = edited }
            : row;

    /// <summary>
    /// What the product dialog should now show: the document, with the visit's pending terminal values laid over
    /// it.
    /// <para>Neither source alone is right mid-visit. The document has not seen the terminals the installer
    /// addressed inside the visit, and the pending map holds only those — so the answer is the overlay.</para>
    /// </summary>
    private ProductDialogRefresh? Project(
        ElementId productId, IReadOnlyDictionary<ElementId, PinPropertiesResult> pendingTerminals,
        IReadOnlyDictionary<ElementId, string> pendingSettings)
    {
        if (session.Current is not { } project || project.FindById(productId) is not { } element)
            return null;
        var view = new ProductView(project, element);
        return new ProductDialogRefresh(
            BuildTerminals(view, pendingTerminals), BuildSettings(project, view, pendingSettings));
    }

    /// <summary>
    /// Whether the <i>Rediger konstant</i> editor edits this attribute (T047).
    /// </summary>
    /// <remarks>
    /// A bool rather than a key, because that editor has exactly ONE field: there is nothing for a caller to
    /// choose between, and a key vocabulary of one member would be ceremony around a yes/no. The value lives in
    /// <c>inivalue</c> — the attribute the vendor's own editor writes — and any other attribute of a setting
    /// element is not something this editor offers.
    /// </remarks>
    internal static bool ConstantFieldFor(string? attribute) => attribute == "inivalue";

    /// <summary>
    /// The SDK's attribute name as a scene dialog's field key (T045) — one map over both scene windows, because
    /// a route names a field and which window opens follows from the element the finding is about.
    /// </summary>
    /// <remarks>
    /// A shutter member's <c>shutter_position</c> is deliberately absent: no dialog edits one, so a route that
    /// named it would promise a field that does not exist anywhere.
    /// </remarks>
    internal static SceneDialogField? SceneFieldFor(string? attribute) => attribute switch
    {
        "note" => SceneDialogField.Note,
        "relay_value" => SceneDialogField.State,
        "dimming_value" => SceneDialogField.Level,
        "ramptime_ms" => SceneDialogField.RampTime,
        _ => null,
    };

    /// <summary>
    /// The SDK's attribute name as the element dialog's own field key — the third of the three vocabularies
    /// this coordinator translates, beside <see cref="PinFieldFor"/> and <see cref="VariableFieldFor"/>.
    /// </summary>
    /// <remarks>
    /// The <c>master_*</c> provenance attributes are ABSENT by design, so they answer null and their findings
    /// degrade to dialog-level. The dialog does show them — greyed, below the editable pair — and focusing a
    /// disabled box would land the caret where nothing can be typed while the row had promised a field.
    /// </remarks>
    internal static ElementDialogField? ElementFieldFor(string? attribute) => attribute switch
    {
        "name" => ElementDialogField.Name,
        "note" => ElementDialogField.Note,
        // A Betingelser group's operator. Named `type` in the file, which is why the map exists at all.
        "type" => ElementDialogField.Logic,
        _ => null,
    };

    /// <summary>
    /// The SDK's attribute name as the variable editor's own field key — the peer of
    /// <see cref="PinFieldFor"/>, and here for the same reason: this is the one place the two vocabularies meet.
    /// </summary>
    /// <remarks>
    /// The value-carrying attribute spellings all answer <see cref="VariableDialogField.InitialValue"/>, because
    /// they are one field of the dialog: a time variable stores its value in <c>hour</c>/<c>minute</c>/
    /// <c>second</c>/<c>millisecond</c> and a date in <c>day</c>/<c>month</c>, and a finding about any of them is
    /// a finding about the value the installer edits in one group. An attribute this dialog does not render
    /// answers null rather than guessing at a nearby field.
    /// </remarks>
    internal static VariableDialogField? VariableFieldFor(string? attribute) => attribute switch
    {
        "name" => VariableDialogField.Name,
        "note" => VariableDialogField.Note,
        "note-2" => VariableDialogField.HelpNote,
        "inivalue" => VariableDialogField.InitialValue,
        "hour" or "minute" or "second" or "millisecond" => VariableDialogField.InitialValue,
        "day" or "month" => VariableDialogField.InitialValue,
        "backup" => VariableDialogField.Backup,
        _ => null,
    };

    /// <summary>
    /// The SDK's attribute name, as the terminal editor's own field key — the ONE place the two vocabularies
    /// meet.
    /// </summary>
    /// <remarks>
    /// It lives on the coordinator because the coordinator is already where typed SDK reads are bound into
    /// dialog inputs. The window below it knows only keys, and the SDK above it knows only attributes, so
    /// neither acquires a name belonging to the other — and an attribute this dialog does not render answers
    /// null rather than guessing at a nearby field.
    /// </remarks>
    internal static PinDialogField? PinFieldFor(string? attribute) => attribute switch
    {
        "address_dataline" => PinDialogField.Address,
        "cable_colour" => PinDialogField.CableColour,
        "note" => PinDialogField.Note,
        "inivalue" => PinDialogField.InitialValue,
        "backup" => PinDialogField.Backup,
        _ => null,
    };

    /// <summary>
    /// The terminal editor.
    /// </summary>
    /// <param name="pinId">The terminal being edited.</param>
    /// <param name="pin">Its element, for the pre-fill reads.</param>
    /// <param name="focusAttribute">The SDK attribute a route wants the caret on, or null.</param>
    /// <param name="collect">
    /// Where a commit goes. Null — the TREE's own gesture on a pin — writes straight to the document, because a
    /// dialog opened standalone IS its own transaction. Non-null — opened from inside a product-dialog visit —
    /// collects into that visit instead, so the visit stays one commit and one undo entry.
    /// <para>Same window, two commit semantics. It is stated here because the difference is invisible from
    /// inside the dialog, and a reader meeting only one of the two routes would take it for the rule.</para>
    /// </param>
    private async Task OpenPinAsync(
        ElementId pinId, ProjectElement pin, string? focusAttribute = null,
        Action<PinPropertiesResult>? collect = null)
    {
        var view = new PinView(session.Current!, pin);
        bool isOutput = view.IsOutput;
        (int dataLine, int terminal) = view.Address is { } addr ? (addr.DataLine, addr.Terminal) : (1, 0);
        var input = new PinPropertiesInput(
            $"{(isOutput ? "Udgang" : "Indgang")} '{view.Name}'",
            isOutput, dataLine, terminal,
            view.CableColour ?? string.Empty,
            view.Note ?? string.Empty,
            view.InitialValueOn,
            InUseTerminals(isOutput, pinId),
            view.Name ?? string.Empty,
            view.Backup,
            PinFieldFor(focusAttribute));

        // Apply commits and leaves the dialog open, so several terminals can be addressed in one visit
        // (the vendor's Anvend); OK commits the same way and closes.
        async Task Commit(PinPropertiesResult r)
        {
            if (collect is not null)
            {
                // Into the visit. Nothing is validated here: the one command the visit commits validates every
                // terminal it carries, so an address refused there refuses the whole visit rather than being
                // caught twice with two different answers.
                collect(r);
                setStatus($"{view.Name} blev adresseret til datalinie {r.DataLine}, klemme {r.Terminal}.");
                return;
            }
            // A bespoke failure message (invalid address) rather than the generic mapping, so read the outcome
            // directly.
            EditOutcome outcome = await session.ApplyAsync(session.Commands.UpdatePin(session.Current!, pinId, r));
            setStatus(outcome.Status == EditStatus.Committed
                ? $"{view.Name} blev adresseret til datalinie {r.DataLine}, klemme {r.Terminal}."
                : $"Datalinie {r.DataLine}, klemme {r.Terminal} er ikke en gyldig adresse.");
        }

        PinPropertiesResult? result = await dialogs.EditPinPropertiesAsync(input, Commit);
        if (result is null)
            return;   // cancelled — the pin keeps its addressing
        await Commit(result);
    }

    // The addresses already used by other pins of the same direction (US-012 in-use indication). Handed over as
    // DatalineAddress values, not as formatted keys: the dialog matches them by the record's own equality, so
    // neither side has to agree with the other about how a line and a terminal are spelled into one string.
    private IReadOnlyList<DatalineAddress> InUseTerminals(bool isOutput, ElementId except)
    {
        var used = new List<DatalineAddress>();
        if (session.Current is not { } project)
            return used;
        string tag = isOutput ? "dataline_output" : "dataline_input";
        foreach (ProjectElement element in project.Root.DescendantsAndSelf())
        {
            if (element.Tag == tag && element.Id is { } eid && eid != except
                && new PinView(project, element).Address is { } a)
            {
                used.Add(a);
            }
        }
        return used;
    }

    // The product's input/output terminals for the addressing grids (US-012): each terminal's name, its
    // vendor-formatted "Datalinie N.PP" address (blank when unassigned), cable colour and note. The typed PinView
    // owns the reads + address decode — the coordinator only formats the row.
    private static IReadOnlyList<ProductTerminal> BuildTerminals(
        ProductView product, IReadOnlyDictionary<ElementId, PinPropertiesResult>? pending = null)
    {
        var terminals = new List<ProductTerminal>();
        foreach (PinView t in product.Terminals)
        {
            // A terminal the installer addressed INSIDE this visit renders from what they entered, not from the
            // document — which has not been told yet, and must not be until the visit commits.
            if (t.Id is { } id && pending is not null && pending.TryGetValue(id, out PinPropertiesResult? edited) && edited is not null)
            {
                DatalineAddress.TryEncode(edited.DataLine, edited.Terminal, t.IsOutput, out string token);
                string pendingLabel = DatalineAddress.ToVendorLabel(token, t.IsOutput);
                terminals.Add(new ProductTerminal(
                    t.Name ?? string.Empty,
                    pendingLabel == "?" ? string.Empty : $"Datalinie {pendingLabel}",
                    edited.CableColour,
                    edited.Note,
                    t.IsOutput,
                    id.ToToken()));
                continue;
            }
            string label = DatalineAddress.ToVendorLabel(t.AddressToken, t.IsOutput);
            terminals.Add(new ProductTerminal(
                t.Name ?? string.Empty,
                label == "?" ? string.Empty : $"Datalinie {label}",
                t.CableColour ?? string.Empty,
                t.Note ?? string.Empty,
                t.IsOutput,
                t.Id?.ToToken() ?? string.Empty));
        }
        return terminals;
    }

    /// <summary>The <i>Indstillinger</i> rows, rendered by the app's per-type value formatter.</summary>
    // A setting with no id is SKIPPED rather than shown unidentified: the row is what an editor is opened on, and
    // one that cannot say which element it stands for is a row whose click could only fail.
    //
    // The visit's pending values are laid over the document for the same reason the terminals' are: a constant
    // edited inside the visit has not reached the document yet, and a grid that re-read the document would show
    // the installer the value they had just replaced.
    /// <remarks>
    /// <c>internal</c> so the dialog's own tests project their rows through THIS, rather than each hand-copying
    /// the projection. A copy is what a grid test then asserts against instead of the app's own rows — and the
    /// copies had already lost the id filter, so they projected settings the dialog never shows.
    /// </remarks>
    internal static IReadOnlyList<ProductSetting> BuildSettings(
        Project project, ProductView product, IReadOnlyDictionary<ElementId, string>? pending = null) =>
        [.. product.SettingElements.Where(e => e.Id is not null).Select(project.View).Select(view => Overlay(
            new ProductSetting(
                view.Name ?? string.Empty,
                view.Note ?? string.Empty,
                VariableValueFormat.For(view.Element.Tag, view.Effective) ?? string.Empty,
                view.Element.Id!.Value),
            pending ?? System.Collections.Immutable.ImmutableDictionary<ElementId, string>.Empty)!)];

}
