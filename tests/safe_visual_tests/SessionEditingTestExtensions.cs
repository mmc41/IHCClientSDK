using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// fablerefac W2-14: test-only edit builders. The GUI drives every mutation through <see cref="ProjectWorkflow.ApplyAsync"/>
/// + a command (see <see cref="MainWindowViewModel"/>); these extensions give the pinning suites the same one-call
/// ergonomics over that <b>same</b> production path — each builds the command via the public <c>Build*</c> factories /
/// command records and maps the outcome to the bool / id the tests assert on. Production <see cref="ProjectWorkflow"/>
/// keeps no per-op mutation wrappers; this keeps the ~293 existing call sites readable while exercising the new path.
/// </summary>
internal static class SessionEditingTestExtensions
{
    private static async Task<bool> Committed(this ProjectWorkflow s, ProjectCommand? command) =>
        command is not null && (await s.ApplyAsync(command)).Status == EditStatus.Committed;

    private static async Task<bool> CommittedOrNoChange(this ProjectWorkflow s, ProjectCommand? command) =>
        command is not null && (await s.ApplyAsync(command)).Status is EditStatus.Committed or EditStatus.NoChange;

    private static async Task<ElementId?> ProducedId(this ProjectWorkflow s, ProjectCommand<ElementId>? command)
    {
        if (command is null)
            return null;
        // Only a Committed outcome carries a real id — EditOutcome<T>.Value on a refused/failed outcome is
        // default(ElementId) (_0x0, not null) because T is an unconstrained value type, so gate on the status.
        EditOutcome<ElementId> outcome = await s.ApplyAsync(command);
        return outcome.Status == EditStatus.Committed ? outcome.Value : null;
    }

    // ---- localities / structure ----
    public static Task<ElementId?> AddLocalityAsync(this ProjectWorkflow s) =>
        s.ProducedId(new AddLocality(ProjectWorkflow.NewLocalityName));

    public static Task<bool> RenameLocalityAsync(this ProjectWorkflow s, ElementId id, string name, string note) =>
        s.Committed(new RenameLocality(id, name, note));

    public static Task<bool> DeleteLocalityAsync(this ProjectWorkflow s, ElementId id) =>
        s.Committed(new DeleteLocality(id));

    public static Task<bool> DeleteNodeAsync(this ProjectWorkflow s, ElementId id)
    {
        DeleteImpact impact = s.Commands.PreviewDelete(s.Current!, id);
        return impact.Deletable ? s.Committed(s.Commands.DeleteNode(s.Current!, id, impact.NeedsConfirm)) : Task.FromResult(false);
    }

    public static Task<bool> MoveNodeAsync(this ProjectWorkflow s, ElementId sourceId, ElementId targetParentId) =>
        s.Committed(s.Commands.MoveNode(s.Current!, sourceId, targetParentId));

    public static Task<ElementId?> CopyNodeAsync(this ProjectWorkflow s, ElementId sourceId, ElementId targetParentId) =>
        s.ProducedId(s.Commands.CopyNode(s.Current!, sourceId, targetParentId));

    public static Task<bool> ReorderNodeAsync(this ProjectWorkflow s, ElementId id, int delta) =>
        s.Committed(s.Commands.ReorderNode(s.Current!, id, delta));

    public static Task<bool> ReorderNodeToSiblingAsync(this ProjectWorkflow s, ElementId dragged, ElementId targetSibling) =>
        s.Committed(s.Commands.ReorderNodeToSibling(s.Current!, dragged, targetSibling));

    // ---- products / function blocks / variables (T004: built through the SDK ProjectCommands gateway) ----
    /// <param name="displayName">Which product, when the identifier names more than one. Eight identifiers do
    /// (D22) — `_0x4304` is both <c>Lampeudtag dimmer</c> and <c>1-10v converter - Lampeudtag dimmer</c> — and
    /// the SDK factory REFUSES an ambiguous identifier rather than guessing (T046). Omit for the ~92 products
    /// whose identifier is unique.</param>
    public static Task<ElementId?> AddProductAsync(
        this ProjectWorkflow s, ElementId localityId, string productIdentifier, string? displayName = null)
    {
        // The at-most-one-modem rule (US-013) is an app-level pre-check, not part of the command's legality.
        if (s.Commands.WouldExceedModemLimit(s.Current!, productIdentifier))
        {
            return Task.FromResult<ElementId?>(null);
        }
        AddProduct? command = displayName is null
            ? s.Commands.AddProduct(s.Current!, localityId, productIdentifier)
            : s.Commands.AddProduct(s.Current!, localityId,
                s.ResolveCatalogProduct(productIdentifier, displayName)
                ?? throw new InvalidOperationException(
                    $"No catalog product '{productIdentifier}' named '{displayName}'."));
        return s.ProducedId(command);
    }

    public static Task<ElementId?> AddFunctionBlockAsync(this ProjectWorkflow s, ElementId localityId, string masterType) =>
        s.ProducedId(s.Commands.AddFunctionBlock(s.Current!, localityId, masterType));

    public static Task<ElementId?> AddEmptyFunctionBlockAsync(this ProjectWorkflow s, ElementId localityId) =>
        s.ProducedId(s.Commands.AddEmptyFunctionBlock(s.Current!, localityId, ProjectWorkflow.EmptyBlockName));

    public static Task<bool> UnlockFunctionBlockAsync(this ProjectWorkflow s, ElementId functionBlockId) =>
        s.Committed(s.Commands.UnlockFunctionBlock(s.Current!, functionBlockId, "Test Installer"));

    public static Task<ElementId?> AddVariableAsync(this ProjectWorkflow s, ElementId sectionId, string resourceTag, string name) =>
        s.ProducedId(s.Commands.AddVariable(s.Current!, sectionId, resourceTag, name));

    public static Task<ElementId?> AddEnumVariableAsync(
        this ProjectWorkflow s, ElementId sectionId, string variableName, string typeName, IReadOnlyList<string> states) =>
        s.ProducedId(s.Commands.AddEnumVariable(s.Current!, sectionId, variableName, typeName, states));

    public static Task<bool> UpdateEnumStatesAsync(this ProjectWorkflow s, ElementId enumVariableId, IReadOnlyList<string> states) =>
        s.CommittedOrNoChange(s.Commands.UpdateEnumStates(s.Current!, enumVariableId, states));

    // ---- properties / documentation (T008: built through the SDK ProjectCommands gateway) ----
    public static Task<bool> UpdateProjectInfoAsync(this ProjectWorkflow s, ProjectInfoData data) =>
        s.Committed(s.Commands.UpdateProjectInfo(s.Current!, data));

    /// <summary>
    /// Edits a placed product's dialog fields BY CAPTION, as the installer would — composing the real dialog,
    /// resolving each caption to the field the composer offered, and committing through
    /// <see cref="ProjectCommands.ApplyProductDialog"/>.
    /// <para>Replaces the per-family <c>UpdateProductAsync</c>/<c>UpdateModemAsync</c> pair (T031). Caption rather
    /// than attribute name, because a caption is what the dialog shows and what the vendor oracle records — and
    /// because a caption the dialog does not offer THROWS here, where an attribute name would have written a field
    /// the installer was never shown.</para>
    /// </summary>
    public static Task<bool> EditProductDialogAsync(
        this ProjectWorkflow s, ElementId productId, params (string Caption, string Value)[] edits)
    {
        ProductDialogDescriptor dialog = s.GetProductDialog(productId);
        var byCaption = dialog.Groups.SelectMany(g => g.Fields).ToDictionary(f => f.Caption);
        ImmutableArray<ProductDialogEdit> resolved =
        [
            .. edits.Select(e => byCaption.TryGetValue(e.Caption, out DialogDescriptorField? field)
                ? new ProductDialogEdit(field.Target, field.Attribute, e.Value)
                : throw new InvalidOperationException(
                    $"'{dialog.Title}' offers no field captioned '{e.Caption}'. It offers: "
                    + string.Join(", ", byCaption.Keys)))
        ];
        return s.CommittedOrNoChange(s.Commands.ApplyProductDialog(s.Current!, productId, resolved));
    }

    public static Task<bool> UpdateDimmerSettingsAsync(this ProjectWorkflow s, ElementId productId, AdvancedDimmerResult r) =>
        s.CommittedOrNoChange(s.Commands.UpdateDimmerSettings(s.Current!, productId, r));

    public static Task<bool> UpdatePinAsync(this ProjectWorkflow s, ElementId pinId, PinPropertiesResult r) =>
        s.Committed(s.Commands.UpdatePin(s.Current!, pinId, r));

    // ---- links / scenes (T006: built through the SDK ProjectCommands gateway) ----
    public static Task<bool> LinkPinsAsync(this ProjectWorkflow s, ElementId draggedPinId, ElementId dropTargetPinId) =>
        s.Committed(s.Commands.LinkPins(s.Current!, draggedPinId, dropTargetPinId));

    public static Task<bool> RemoveLinkAsync(this ProjectWorkflow s, ElementId linkRowId) =>
        s.Committed(s.Commands.RemoveLink(s.Current!, linkRowId));

    public static Task<bool> LinkSceneAsync(
        this ProjectWorkflow s, ElementId sceneOutputId, ElementId scenesId, SceneValueResult r, bool isDimmer) =>
        // The isDimmer variant is the gateway's decision (sliver #11); this helper keeps its parameter for the tests
        // that assert a specific value, so build the command directly with the requested variant.
        s.Committed(new LinkScene(sceneOutputId, scenesId, r, isDimmer));

    public static Task<bool> UpdateSceneValueAsync(this ProjectWorkflow s, ElementId memberId, SceneValueResult r) =>
        s.Committed(s.Commands.UpdateSceneValue(s.Current!, memberId, r));

    public static Task<bool> UpdateSceneContainerAsync(this ProjectWorkflow s, ElementId scenesId, string note) =>
        s.Committed(s.Commands.UpdateSceneContainer(s.Current!, scenesId, note));

    public static Task<bool> ToggleLogMarkAsync(this ProjectWorkflow s, ElementId logRowId) =>
        s.Committed(new ToggleLogMark(logRowId));

    // ---- data tables (user-defined texts) (T008: built through the SDK ProjectCommands gateway) ----
    public static Task<bool> AddUserTextAsync(this ProjectWorkflow s, string text) =>
        s.CommittedOrNoChange(s.Commands.AddUserText(s.Current!, text));

    public static Task<bool> UpdateUserTextAsync(this ProjectWorkflow s, ElementId textId, string text) =>
        s.Committed(s.Commands.UpdateUserText(s.Current!, textId, text));

    public static Task<bool> DeleteUserTextAsync(this ProjectWorkflow s, ElementId textId) =>
        s.Committed(s.Commands.DeleteUserText(s.Current!, textId));

    // ---- program-mode authoring (T007: built through the SDK ProjectCommands gateway) ----
    public static Task<bool> AddProgramEventAsync(
        this ProjectWorkflow s, ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        s.Committed(s.Commands.AddProgramEvent(s.Current!, containerId, variableId, method, name, note));

    public static Task<bool> AddProgramCommandAsync(
        this ProjectWorkflow s, ElementId containerId, ElementId variableId, string method, string name, string? note) =>
        s.Committed(s.Commands.AddProgramCommand(s.Current!, containerId, variableId, method, name, note));

    public static Task<bool> AddPowerEventAsync(this ProjectWorkflow s, ElementId eventsContainerId) =>
        s.Committed(s.Commands.AddPowerEvent(s.Current!, eventsContainerId));

    public static Task<bool> SetOutputBackupAsync(this ProjectWorkflow s, ElementId outputId, bool save) =>
        s.CommittedOrNoChange(s.Commands.SetOutputBackup(s.Current!, outputId, save));

    public static Task<bool> AddSubProgramAsync(this ProjectWorkflow s, ElementId commandsId) =>
        s.Committed(s.Commands.AddSubProgram(s.Current!, commandsId));

    public static Task<bool> AddConditionAsync(
        this ProjectWorkflow s, ElementId conditionsId, ElementId variableId, string method, string name, string? note) =>
        s.Committed(s.Commands.AddCondition(s.Current!, conditionsId, variableId, method, name, note));

    public static Task<bool> SetConditionsLogicAsync(this ProjectWorkflow s, ElementId conditionsId, bool or) =>
        s.Committed(s.Commands.SetConditionsLogic(s.Current!, conditionsId, or));

    public static Task<bool> AddLogicGroupAsync(this ProjectWorkflow s, ElementId conditionsId) =>
        s.Committed(s.Commands.AddLogicGroup(s.Current!, conditionsId));

    public static Task<bool> AddArithmeticCommandAsync(
        this ProjectWorkflow s, ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name, string note) =>
        s.Committed(s.Commands.AddArithmeticCommand(s.Current!, commandsId, targetId, method, operandId, name, note));

    public static Task<bool> AddStandaloneEnumTypeAsync(
        this ProjectWorkflow s, string typeName, System.Collections.Generic.IReadOnlyList<string> states) =>
        s.Committed(s.Commands.AddStandaloneEnumType(s.Current!, typeName, states));

    public static Task<bool> AddCaseAsync(this ProjectWorkflow s, ElementId commandsId, ElementId switchVariableId) =>
        s.Committed(s.Commands.AddCase(s.Current!, commandsId, switchVariableId));

    public static Task<bool> AddCaseValueAsync(this ProjectWorkflow s, ElementId caseId, string criterion) =>
        s.Committed(s.Commands.AddCaseValue(s.Current!, caseId, criterion));
}
