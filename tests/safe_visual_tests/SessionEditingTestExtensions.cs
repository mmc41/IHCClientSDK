using System.Collections.Generic;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// fablerefac W2-14: test-only edit builders. The GUI drives every mutation through <see cref="ProjectSession.ApplyAsync"/>
/// + a command (see <see cref="MainWindowViewModel"/>); these extensions give the pinning suites the same one-call
/// ergonomics over that <b>same</b> production path — each builds the command via the public <c>Build*</c> factories /
/// command records and maps the outcome to the bool / id the tests assert on. Production <see cref="ProjectSession"/>
/// keeps no per-op mutation wrappers; this keeps the ~293 existing call sites readable while exercising the new path.
/// </summary>
internal static class SessionEditingTestExtensions
{
    private static async Task<bool> Committed(this ProjectSession s, ProjectCommand? command) =>
        command is not null && (await s.ApplyAsync(command)).Status == EditStatus.Committed;

    private static async Task<bool> CommittedOrNoChange(this ProjectSession s, ProjectCommand? command) =>
        command is not null && (await s.ApplyAsync(command)).Status is EditStatus.Committed or EditStatus.NoChange;

    private static async Task<ElementId?> ProducedId(this ProjectSession s, ProjectCommand<ElementId>? command)
    {
        if (command is null)
            return null;
        // Only a Committed outcome carries a real id — EditOutcome<T>.Value on a refused/failed outcome is
        // default(ElementId) (_0x0, not null) because T is an unconstrained value type, so gate on the status.
        EditOutcome<ElementId> outcome = await s.ApplyAsync(command);
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    // ---- localities / structure ----
    public static Task<ElementId?> AddLocalityAsync(this ProjectSession s) =>
        s.ProducedId(new AddLocality(ProjectSession.NewLocalityName));

    public static Task<bool> RenameLocalityAsync(this ProjectSession s, ElementId id, string name, string note) =>
        s.Committed(new RenameLocality(id, name, note));

    public static Task<bool> DeleteLocalityAsync(this ProjectSession s, ElementId id) =>
        s.Committed(new DeleteLocality(id));

    public static Task<bool> DeleteNodeAsync(this ProjectSession s, ElementId id)
    {
        ProjectSession.DeleteImpact impact = s.PreviewDelete(id);
        return impact.Deletable ? s.Committed(new DeleteNode(id, impact.NeedsConfirm)) : Task.FromResult(false);
    }

    public static Task<bool> MoveNodeAsync(this ProjectSession s, ElementId sourceId, ElementId targetParentId) =>
        s.Committed(new MoveNode(sourceId, targetParentId));

    public static Task<ElementId?> CopyNodeAsync(this ProjectSession s, ElementId sourceId, ElementId targetParentId) =>
        s.ProducedId(new CopyNode(sourceId, targetParentId));

    public static Task<bool> ReorderNodeAsync(this ProjectSession s, ElementId id, int delta) =>
        s.Committed(s.BuildReorderNode(id, delta));

    public static Task<bool> ReorderNodeToSiblingAsync(this ProjectSession s, ElementId dragged, ElementId targetSibling) =>
        s.Committed(s.BuildReorderNodeToSibling(dragged, targetSibling));

    // ---- products / function blocks / variables ----
    public static Task<ElementId?> AddProductAsync(this ProjectSession s, ElementId localityId, string productIdentifier) =>
        // The at-most-one-modem rule (US-013) is an app-level pre-check, not part of the command's legality.
        s.WouldExceedModemLimit(productIdentifier)
            ? Task.FromResult<ElementId?>(null)
            : s.ProducedId(s.BuildAddProduct(localityId, productIdentifier));

    public static Task<ElementId?> AddFunctionBlockAsync(this ProjectSession s, ElementId localityId, string masterType) =>
        s.ProducedId(s.BuildAddFunctionBlock(localityId, masterType));

    public static Task<ElementId?> AddEmptyFunctionBlockAsync(this ProjectSession s, ElementId localityId) =>
        s.ProducedId(s.BuildAddEmptyFunctionBlock(localityId));

    public static Task<bool> UnlockFunctionBlockAsync(this ProjectSession s, ElementId functionBlockId) =>
        s.Committed(new UnlockFunctionBlock(functionBlockId));

    public static Task<ElementId?> AddVariableAsync(this ProjectSession s, ElementId sectionId, string resourceTag, string name) =>
        s.ProducedId(s.BuildAddVariable(sectionId, resourceTag, name));

    public static Task<ElementId?> AddEnumVariableAsync(
        this ProjectSession s, ElementId sectionId, string variableName, string typeName, IReadOnlyList<string> states) =>
        s.ProducedId(s.BuildAddEnumVariable(sectionId, variableName, typeName, states));

    public static Task<bool> UpdateEnumStatesAsync(this ProjectSession s, ElementId enumVariableId, IReadOnlyList<string> states) =>
        s.CommittedOrNoChange(s.BuildUpdateEnumStates(enumVariableId, states));

    // ---- properties / documentation ----
    public static Task<bool> UpdateProjectInfoAsync(this ProjectSession s, ProjectInfoData data) =>
        s.Committed(new UpdateProjectInfo(data));

    public static Task<bool> UpdateProductAsync(this ProjectSession s, ElementId productId, ProductPropertiesResult r) =>
        s.CommittedOrNoChange(s.BuildUpdateProduct(productId, r));

    public static Task<bool> UpdateModemAsync(this ProjectSession s, ElementId modemId, ModemPropertiesResult r) =>
        s.CommittedOrNoChange(s.BuildUpdateModem(modemId, r));

    public static Task<bool> UpdateDimmerSettingsAsync(this ProjectSession s, ElementId productId, AdvancedDimmerResult r) =>
        s.CommittedOrNoChange(new UpdateDimmerSettings(productId, r));

    public static Task<bool> UpdatePinAsync(this ProjectSession s, ElementId pinId, PinPropertiesResult r) =>
        s.Committed(new UpdatePin(pinId, r));

    // ---- links / scenes ----
    public static Task<bool> LinkPinsAsync(this ProjectSession s, ElementId draggedPinId, ElementId dropTargetPinId) =>
        s.Committed(new LinkPins(draggedPinId, dropTargetPinId));

    public static Task<bool> RemoveLinkAsync(this ProjectSession s, ElementId linkRowId) =>
        s.Committed(new RemoveLink(linkRowId));

    public static Task<bool> LinkSceneAsync(
        this ProjectSession s, ElementId sceneOutputId, ElementId scenesId, SceneValueResult r, bool isDimmer) =>
        s.Committed(new LinkScene(sceneOutputId, scenesId, r, isDimmer));

    public static Task<bool> UpdateSceneValueAsync(this ProjectSession s, ElementId memberId, SceneValueResult r) =>
        s.Committed(new UpdateSceneValue(memberId, r));

    public static Task<bool> UpdateSceneContainerAsync(this ProjectSession s, ElementId scenesId, string note) =>
        s.Committed(new UpdateSceneContainer(scenesId, note));

    public static Task<bool> ToggleLogMarkAsync(this ProjectSession s, ElementId logRowId) =>
        s.Committed(new ToggleLogMark(logRowId));

    // ---- data tables (user-defined texts) ----
    public static Task<bool> AddUserTextAsync(this ProjectSession s, string text) =>
        s.CommittedOrNoChange(s.BuildAddUserText(text));

    public static Task<bool> UpdateUserTextAsync(this ProjectSession s, ElementId textId, string text) =>
        s.Committed(new UpdateUserText(textId, text));

    public static Task<bool> DeleteUserTextAsync(this ProjectSession s, ElementId textId) =>
        s.Committed(new DeleteUserText(textId));

    // ---- program-mode authoring ----
    public static Task<bool> AddProgramEventAsync(
        this ProjectSession s, ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        s.Committed(s.BuildAddProgramEvent(containerId, variableId, method, name, note));

    public static Task<bool> AddProgramCommandAsync(
        this ProjectSession s, ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        s.Committed(new AddProgramCommand(containerId, variableId, method, name, note));

    public static Task<bool> AddPowerEventAsync(this ProjectSession s, ElementId eventsContainerId) =>
        s.Committed(s.BuildAddPowerEvent(eventsContainerId));

    public static Task<bool> SetOutputBackupAsync(this ProjectSession s, ElementId outputId, bool save) =>
        s.CommittedOrNoChange(new SetOutputBackup(outputId, save));

    public static Task<bool> AddSubProgramAsync(this ProjectSession s, ElementId commandsId) =>
        s.Committed(new AddSubProgram(commandsId));

    public static Task<bool> AddConditionAsync(
        this ProjectSession s, ElementId conditionsId, ElementId variableId, string method, string name, string? note) =>
        s.Committed(new AddCondition(conditionsId, variableId, method, name, note));

    public static Task<bool> SetConditionsLogicAsync(this ProjectSession s, ElementId conditionsId, bool or) =>
        s.Committed(new SetConditionsLogic(conditionsId, or));

    public static Task<bool> AddLogicGroupAsync(this ProjectSession s, ElementId conditionsId) =>
        s.Committed(new AddLogicGroup(conditionsId));

    public static Task<bool> AddArithmeticCommandAsync(
        this ProjectSession s, ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name) =>
        s.Committed(new AddArithmeticCommand(commandsId, targetId, method, operandId, name));

    public static Task<bool> AddCaseAsync(this ProjectSession s, ElementId commandsId, ElementId switchVariableId) =>
        s.Committed(new AddCase(commandsId, switchVariableId));

    public static Task<bool> AddCaseValueAsync(this ProjectSession s, ElementId caseId, string criterion) =>
        s.Committed(s.BuildAddCaseValue(caseId, criterion));
}
