#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
// NOTE: deliberately NOT `using Ihc.Vis.Session;` — a factory named e.g. `AddLocality` would collide with the
// same-named command type in a `new AddLocality(...)` body. Command types are written `Session.<Name>` instead
// (resolved as the Ihc.Vis.Session sub-namespace from within Ihc.Vis), keeping the factory names 1:1 with the
// vocabulary they mint.

namespace Ihc.Vis
{
    /// <summary>
    /// The single discoverable authoring door (R1/D01): a <b>stateless</b> planner that mints ready-to-apply
    /// <see cref="Session.ProjectCommand"/>s for every domain edit, reached from <see cref="ProjectAppService.Commands"/>.
    /// A GUI or console frontend obtains a command here and hands it to its session/apply path
    /// (<c>ProjectDocumentSession.Apply</c> / the app's <c>ApplyAsync</c>) — it never constructs command types
    /// directly. Factories take the target <see cref="Project"/> per call (mirroring the app's per-call OpenScratch
    /// runner — there is no session lifecycle here) and resolve catalog-bearing commands against the service's own
    /// lazy catalog and its clock. Every factory resolves <b>exactly</b> as direct construction did (D10), so the
    /// bytes a command eventually writes are unchanged. Families land per R1 task: Locality (T003); Product (T004);
    /// Structure (T005); Link/Scene (T006); Program (T007); Metadata (T008). <see cref="Session.CompositeCommand"/>
    /// is excluded (D04).
    /// </summary>
    public sealed class ProjectCommands
    {
        // Held for the catalog-bearing families: factories resolve products/function blocks/templates against the
        // service's embedded catalog, exactly as ProjectWorkflow's Build* methods did via the service today.
        private readonly Lazy<Catalog.CompositeCatalog> _catalog;
        // The service's clock — the empty-function-block "created" stamp reads today from it (was DateTime.Now
        // app-side; TimeProvider.System in production yields the identical date, and tests can pin it).
        private readonly TimeProvider _timeProvider;

        internal ProjectCommands(Lazy<Catalog.CompositeCatalog> catalog, TimeProvider timeProvider)
        {
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        // ---- Locality family (T003; context-free — the Project keeps the vocabulary uniform per D01/D03) ----

        /// <summary>Command to insert a new locality named <paramref name="name"/> (US-008).</summary>
        public Session.AddLocality AddLocality(Project project, string name) =>
            new Session.AddLocality(name);

        /// <summary>Command to rename the locality (or function block) <paramref name="id"/> (US-007/US-019),
        /// setting its name and note.</summary>
        public Session.RenameLocality RenameLocality(Project project, ElementId id, string name, string note) =>
            new Session.RenameLocality(id, name, note);

        /// <summary>Command to delete the locality <paramref name="id"/>, cascading through its contents (US-009).</summary>
        public Session.DeleteLocality DeleteLocality(Project project, ElementId id) =>
            new Session.DeleteLocality(id);

        // ---- Product family (T004) ----

        /// <summary>Command to insert the catalog product with <paramref name="productIdentifier"/> into a locality
        /// (US-010), or null when no such product is in the catalog. The at-most-one-modem rule (US-013) is a separate
        /// pre-check — <see cref="WouldExceedModemLimit"/> — so the caller can surface it before applying.</summary>
        public Session.AddProduct? AddProduct(Project project, ElementId localityId, string productIdentifier) =>
            _catalog.Value.Products.FirstOrDefault(p => p.ProductIdentifier == productIdentifier) is { } definition
                ? new Session.AddProduct(localityId, definition)
                : null;

        /// <summary>Command to insert a preprogrammed library function block by master type into a locality (US-018),
        /// or null when no such block is in the catalog.</summary>
        public Session.AddFunctionBlock? AddFunctionBlock(Project project, ElementId localityId, string masterType) =>
            _catalog.Value.FunctionBlocks.FirstOrDefault(f => f.MasterType == masterType) is { } definition
                ? new Session.AddFunctionBlock(localityId, definition)
                : null;

        /// <summary>Command to insert the catalog "Tom blok" empty function-block template into a locality (US-019),
        /// stamped with today's date (from the service clock) and the caller-supplied default <paramref name="name"/>.</summary>
        public Session.AddEmptyFunctionBlock AddEmptyFunctionBlock(Project project, ElementId localityId, string name) =>
            new Session.AddEmptyFunctionBlock(localityId, _catalog.Value.EmptyFunctionBlockTemplate,
                DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime), name);

        /// <summary>Command to add a typed variable to a function-block variable section (US-027), or null when the
        /// section is not a function-block variable section.</summary>
        public Session.AddVariable? AddVariable(Project project, ElementId sectionId, string resourceTag, string name) =>
            project.FindById(sectionId) is { } section
                && project.FindParent(sectionId) is { Id: { } blockId } block && block.Kind == ElementKind.FunctionBlock
                ? new Session.AddVariable(blockId, section.Tag, resourceTag, name)
                : null;

        /// <summary>Command to create a project-global enum type and add a variable of it to a function-block section
        /// (US-030), or null when the section is not a function-block variable section.</summary>
        public Session.AddEnumVariable? AddEnumVariable(
            Project project, ElementId sectionId, string variableName, string typeName, IReadOnlyList<string> states) =>
            project.FindById(sectionId) is { } section
                && project.FindParent(sectionId) is { Id: { } blockId } block && block.Kind == ElementKind.FunctionBlock
                ? new Session.AddEnumVariable(blockId, section.Tag, variableName, typeName, states)
                : null;

        /// <summary>Command to apply edited product documentation (US-011), capturing the product's current locality
        /// so the command can re-parent it when the Location changed.</summary>
        public Session.UpdateProduct UpdateProduct(Project project, ElementId productId, Session.ProductPropertiesResult result) =>
            new Session.UpdateProduct(productId, result, project.FindParent(productId)?.Id);

        /// <summary>Command to apply edited data-line pin addressing (US-012).</summary>
        public Session.UpdatePin UpdatePin(Project project, ElementId pinId, Session.PinPropertiesResult result) =>
            new Session.UpdatePin(pinId, result);

        /// <summary>Command to unlock a locked library function block for editing (US-020).</summary>
        public Session.UnlockFunctionBlock UnlockFunctionBlock(Project project, ElementId id) =>
            new Session.UnlockFunctionBlock(id);

        /// <summary>Whether inserting the product would break the at-most-one-modem rule (US-013, sliver #10 relocated
        /// from the app): the product is a modem and the project already holds one. The confirm <i>wording</i> stays
        /// GUI-side; this owns only the decision.</summary>
        public bool WouldExceedModemLimit(Project project, string productIdentifier) =>
            _catalog.Value.Products.FirstOrDefault(p => p.ProductIdentifier == productIdentifier) is { } definition
            && ProductClassifier.IsModem(definition.Body.Tag) && HasModem(project);

        // Whether the project already contains a modem device root (the at-most-one-modem rule, US-013).
        private static bool HasModem(Project project) =>
            project.Root.DescendantsAndSelf().Any(e => ProductClassifier.IsModem(e.Tag));

        // ---- Structure family (T005): move / copy / delete / reorder + their legality (sliver #9 relocated) ----

        /// <summary>Command to move <paramref name="sourceId"/> under <paramref name="targetParentId"/> (US-054).</summary>
        public Session.MoveNode MoveNode(Project project, ElementId sourceId, ElementId targetParentId) =>
            new Session.MoveNode(sourceId, targetParentId);

        /// <summary>Command to copy <paramref name="sourceId"/> under <paramref name="targetParentId"/> (US-056),
        /// producing the new node's id.</summary>
        public Session.CopyNode CopyNode(Project project, ElementId sourceId, ElementId targetParentId) =>
            new Session.CopyNode(sourceId, targetParentId);

        /// <summary>Command to delete a non-locality node (US-053); <paramref name="cascade"/> also removes the
        /// links / program rows that reference it (the reference-cascade flag <see cref="PreviewDelete"/> computed).</summary>
        public Session.DeleteNode DeleteNode(Project project, ElementId id, bool cascade) =>
            new Session.DeleteNode(id, cascade);

        /// <summary>Command to reorder a node <paramref name="delta"/> positions among its same-tag siblings (US-055),
        /// or null at the list ends / for a rootless node.</summary>
        public Session.ReorderNode? ReorderNode(Project project, ElementId id, int delta)
        {
            if (delta == 0 || project.FindParent(id) is not { Id: { } } parent || project.FindById(id) is not { } node)
            {
                return null;
            }
            var siblings = parent.ChildrenOrEmpty().Where(c => c.Tag == node.Tag).ToList();
            int here = siblings.FindIndex(c => c.Id == id);
            int there = here + delta;
            return here < 0 || there < 0 || there >= siblings.Count ? null : new Session.ReorderNode(id, there);
        }

        /// <summary>Command to reorder <paramref name="dragged"/> to <paramref name="targetSibling"/>'s position among
        /// their shared same-tag siblings (US-055, the drag drop), or null when they are not a reorderable pair.</summary>
        public Session.ReorderNode? ReorderNodeToSibling(Project project, ElementId dragged, ElementId targetSibling)
        {
            if (project.FindParent(dragged) is not { Id: { } parentId } parent
                || project.FindById(dragged) is not { } node
                || project.FindParent(targetSibling)?.Id != parentId)
            {
                return null;
            }
            int targetIndex = parent.ChildrenOrEmpty().Where(c => c.Tag == node.Tag).ToList().FindIndex(c => c.Id == targetSibling);
            return targetIndex < 0 ? null : new Session.ReorderNode(dragged, targetIndex);
        }

        /// <summary>Whether <paramref name="dragged"/> and <paramref name="target"/> are distinct <b>same-parent,
        /// same-tag siblings</b> — a reorder drop (US-055), the drag-over hint peer of
        /// <see cref="ReorderNodeToSibling"/>.</summary>
        public bool CanReorderNode(Project project, ElementId dragged, ElementId target)
        {
            if (dragged == target
                || project.FindById(dragged) is not { } a
                || project.FindById(target) is not { } b)
            {
                return false;
            }
            return a.Tag == b.Tag
                && project.FindParent(dragged) is { Id: { } parentId }
                && project.FindParent(target)?.Id == parentId;
        }

        // The cheap delete classification shared by CanDelete and PreviewDelete: which DeleteKind a node needs, WITHOUT
        // the (potentially expensive) strict-cascade simulation the confirm flag needs. Resolves the element once and
        // hands it back so PreviewDelete can compute the confirm without a second lookup.
        private static (DeleteKind Kind, ProjectElement? Element) ClassifyDelete(Project project, ElementId id)
        {
            ProjectElement? element = project.FindById(id);
            DeleteKind kind;
            if (element is null || ProjectEditor.DeletionRefusalReason(project.Root, id) is not null)
            {
                kind = DeleteKind.NotDeletable;   // missing, or a catalog pin / locked-block node (review3 H1)
            }
            else if (element.IsLinkHalf || element.IsSceneMember)
            {
                kind = DeleteKind.Link;   // link row → remove reciprocal (US-057)
            }
            else if (element.IsLocalityGroup)
            {
                kind = DeleteKind.Locality;   // US-009 cascade
            }
            else
            {
                kind = IsDeletableNode(element.Tag) ? DeleteKind.General : DeleteKind.NotDeletable;
            }
            return (kind, element);
        }

        /// <summary>Whether <paramref name="id"/> can be deleted at all (US-053) — the SDK verdict the GUI's Delete gate
        /// (context menu / Edit ▸ Delete / Delete key) reads. Unlike <see cref="PreviewDelete"/> this skips the strict
        /// cascade simulation, so it stays cheap to re-evaluate on every selection, menu-open and post-edit refresh.</summary>
        public bool CanDelete(Project project, ElementId id) => ClassifyDelete(project, id).Kind != DeleteKind.NotDeletable;

        /// <summary>
        /// The non-mutating impact and dispatch of deleting <paramref name="id"/> (US-009/US-053, sliver #9 relocated
        /// from the app): which delete <see cref="DeleteKind"/> applies, whether it can be deleted at all, and whether
        /// deleting it needs confirmation because it cascades. A link row deletes its reciprocal (US-057, no confirm);
        /// a locality needs confirmation when it still holds contents (US-009); any other deletable node needs it when
        /// other logic references it (link halves, or a program row a strict delete would trip over). The GUI composes
        /// the confirmation WORDING from the returned kind (D05); this decides only the kind and whether one is needed.
        /// The strict-cascade probe here is why the interactive gate reads the cheaper <see cref="CanDelete"/> instead.
        /// </summary>
        public DeleteImpact PreviewDelete(Project project, ElementId id)
        {
            (DeleteKind kind, ProjectElement? element) = ClassifyDelete(project, id);
            bool needsConfirm = kind switch
            {
                DeleteKind.Locality => !element!.Children.IsDefaultOrEmpty,   // US-009: confirm only when it still holds contents
                DeleteKind.General => HasLinkHalves(element!) || WouldThrowStrict(project, id),   // link halves / strict cascade
                _ => false,   // NotDeletable / Link never confirm
            };
            return new DeleteImpact(kind != DeleteKind.NotDeletable, needsConfirm, kind);
        }

        // ---- Link/Scene family (T006): pin links, link removal, scenario links + values (sliver #11 relocated) ----

        /// <summary>Command to create a follow-link from a source pin to a target pin (US-022/US-023).</summary>
        public Session.LinkPins LinkPins(Project project, ElementId source, ElementId target) =>
            new Session.LinkPins(source, target);

        /// <summary>Command to remove a link by one of its rows, cascading the reciprocal half (US-057).</summary>
        public Session.RemoveLink RemoveLink(Project project, ElementId linkRowId) =>
            new Session.RemoveLink(linkRowId);

        /// <summary>The scene value variant (sliver #11, relocated from the app): a scenes container bound to a
        /// wireless-dimming output takes dimmer values (light level %); otherwise relay/socket ON/OFF. Inferred from
        /// the bound output family (the element's <c>IsWirelessDimming</c> read predicate). The GUI uses this to
        /// shape the scene-value dialog; <see cref="LinkScene"/> uses it to stamp the command.</summary>
        public bool IsSceneWirelessDimming(Project project, ElementId scenesId) =>
            project.FindById(scenesId) is { } scenes
            && ElementId.TryParse(project.View(scenes).Effective("scene_resource"), out ElementId boundId)
            && project.FindById(boundId)?.IsWirelessDimming == true;

        /// <summary>Command to create a scenario link from a function-block scene output pin to a product's scenes
        /// container with the given value (US-024). The relay/dimmer variant is inferred from the bound output family
        /// (sliver #11) via <see cref="IsSceneWirelessDimming"/>.</summary>
        public Session.LinkScene LinkScene(Project project, ElementId sceneOutputId, ElementId scenesId, Session.SceneValueResult result) =>
            new Session.LinkScene(sceneOutputId, scenesId, result, IsSceneWirelessDimming(project, scenesId));

        /// <summary>Command to edit an existing scenario link's stored value in place (US-058).</summary>
        public Session.UpdateSceneValue UpdateSceneValue(Project project, ElementId memberId, Session.SceneValueResult result) =>
            new Session.UpdateSceneValue(memberId, result);

        /// <summary>Command to edit a scenes container's note (US-024).</summary>
        public Session.UpdateSceneContainer UpdateSceneContainer(Project project, ElementId scenesId, string note) =>
            new Session.UpdateSceneContainer(scenesId, note);

        // ---- Program family (T007): events / commands / conditions / cases / arithmetic + their resolvers ----

        /// <summary>Command to add a resource-triggered program event to an <c>events</c> container (US-028), or null
        /// when the target is not a program's events container (the owning program is resolved here).</summary>
        public Session.AddProgramEvent? AddProgramEvent(Project project, ElementId containerId, ElementId variableId, string method, string name, string? note) =>
            ProgramOfEventsContainer(project, containerId) is { } programId
                ? new Session.AddProgramEvent(programId, variableId, method, name, note) : null;

        /// <summary>Command to add a Powerup system event to an <c>events</c> container (US-033), or null for a
        /// non-events target.</summary>
        public Session.AddPowerEvent? AddPowerEvent(Project project, ElementId eventsContainerId) =>
            ProgramOfEventsContainer(project, eventsContainerId) is { } programId ? new Session.AddPowerEvent(programId) : null;

        /// <summary>Command to author a program action command into a command container (US-028).</summary>
        public Session.AddProgramCommand AddProgramCommand(Project project, ElementId containerId, ElementId variableId, string method, string name, string? note) =>
            new Session.AddProgramCommand(containerId, variableId, method, name, note);

        /// <summary>Command to insert a conditional sub-program into a command container (US-029).</summary>
        public Session.AddSubProgram AddSubProgram(Project project, ElementId commandsId) =>
            new Session.AddSubProgram(commandsId);

        /// <summary>Command to add a condition to a conditions group (US-029).</summary>
        public Session.AddCondition AddCondition(Project project, ElementId conditionsId, ElementId variableId, string method, string name, string? note) =>
            new Session.AddCondition(conditionsId, variableId, method, name, note);

        /// <summary>Command to toggle a conditions group's AND/OR combination (US-029).</summary>
        public Session.SetConditionsLogic SetConditionsLogic(Project project, ElementId conditionsId, bool or) =>
            new Session.SetConditionsLogic(conditionsId, or);

        /// <summary>Command to add a nested logic group inside a conditions group (US-029).</summary>
        public Session.AddLogicGroup AddLogicGroup(Project project, ElementId conditionsId) =>
            new Session.AddLogicGroup(conditionsId);

        /// <summary>Command to author one arithmetic command line into a command container (US-032).</summary>
        public Session.AddArithmeticCommand AddArithmeticCommand(Project project, ElementId commandsId, ElementId targetId, string method, ElementId operandId, string name) =>
            new Session.AddArithmeticCommand(commandsId, targetId, method, operandId, name);

        /// <summary>Command to insert a case structure keyed on a switch variable (US-031).</summary>
        public Session.AddCase AddCase(Project project, ElementId commandsId, ElementId switchVariableId) =>
            new Session.AddCase(commandsId, switchVariableId);

        /// <summary>Command to add a case-value branch to a <c>program_case</c> for a literal criterion (US-031), or
        /// null for a non-case target, a missing switch, or an enum switch (whose case values need the type's states).
        /// The switch tag is resolved here from the case's linked switch.</summary>
        public Session.AddCaseValue? AddCaseValue(Project project, ElementId caseId, string criterion) =>
            project.FindById(caseId) is { } kase && kase.IsProgramCase
            && ElementId.TryParse(project.View(kase).Effective("link"), out ElementId switchId)
            && project.FindById(switchId) is { } switchVar && switchVar.Kind != ElementKind.EnumResource
                ? new Session.AddCaseValue(caseId, criterion, switchVar.Tag) : null;

        /// <summary>Command to set an output's "Save current value" power-loss persistence (US-033).</summary>
        public Session.SetOutputBackup SetOutputBackup(Project project, ElementId outputId, bool save) =>
            new Session.SetOutputBackup(outputId, save);

        /// <summary>Command to toggle a "Log …" row's log mark (US-068).</summary>
        public Session.ToggleLogMark ToggleLogMark(Project project, ElementId logRowId) =>
            new Session.ToggleLogMark(logRowId);

        // Resolves the program owning an `events` container (US-028/US-033), or null when the target is not one.
        private static ElementId? ProgramOfEventsContainer(Project project, ElementId containerId) =>
            project.FindById(containerId)?.IsEventsContainer == true
            && project.FindParent(containerId) is { Id: { } programId } parent && parent.IsProgram
                ? programId : null;

        // ---- Metadata family (T008): project info, user texts, enum states, dimmer/modem documentation ----

        /// <summary>Command to apply edited project/customer/installer information (US-039).</summary>
        public Session.UpdateProjectInfo UpdateProjectInfo(Project project, ProjectInfoData data) =>
            new Session.UpdateProjectInfo(data);

        /// <summary>Command to append a user-defined text (US-049), reporting whether the user-texts table already
        /// exists so the command creates it on first use.</summary>
        public Session.AddUserText AddUserText(Project project, string text) =>
            new Session.AddUserText(text, project.Child("enum_definitions")?.ChildrenOrEmpty()
                .Any(c => c.IsEnumDefinition && project.View(c).Name == ProjectProjections.UserTextsTableName) == true);

        /// <summary>Command to rename a user-defined text by id (US-049 Edit).</summary>
        public Session.UpdateUserText UpdateUserText(Project project, ElementId textId, string text) =>
            new Session.UpdateUserText(textId, text);

        /// <summary>Command to delete a user-defined text by id (US-049 Delete).</summary>
        public Session.DeleteUserText DeleteUserText(Project project, ElementId textId) =>
            new Session.DeleteUserText(textId);

        /// <summary>Command to append the not-yet-present states to the enumerator type referenced by a
        /// <c>resource_enum</c> variable (US-030), or null for a non-enum target. The caller-provided states are
        /// diffed against the type's existing values here, so an append of nothing new falls out as a NoChange.</summary>
        public Session.UpdateEnumStates? UpdateEnumStates(Project project, ElementId enumVariableId, IReadOnlyList<string> states)
        {
            if (project.FindById(enumVariableId) is not { } variable
                || variable.Kind != ElementKind.EnumResource
                || !ElementId.TryParse(project.View(variable).Effective("typedef"), out ElementId defId)
                || project.FindById(defId) is not { } def || project.View(def).Name is not { } defName)
            {
                return null;
            }
            var existing = def.ChildrenOrEmpty().Where(c => c.IsEnumValue).Select(c => project.View(c).Name).ToHashSet();
            string[] added = states.Where(s => !existing.Contains(s)).ToArray();
            return new Session.UpdateEnumStates(defName, added);
        }

        /// <summary>Command to apply edited advanced wireless-dimmer settings (US-015).</summary>
        public Session.UpdateDimmerSettings UpdateDimmerSettings(Project project, ElementId productId, Session.AdvancedDimmerResult result) =>
            new Session.UpdateDimmerSettings(productId, result);

        /// <summary>Command to apply edited modem documentation (US-013), capturing the modem's current locality so the
        /// command can re-parent it when the Location changed.</summary>
        public Session.UpdateModem UpdateModem(Project project, ElementId modemId, Session.ModemPropertiesResult result) =>
            new Session.UpdateModem(modemId, result, project.FindParent(modemId)?.Id);

        // The node types US-053 can delete: products, function blocks, variables/pins, and program elements. Structural
        // containers (sections, event/command/conditions groups, programs) and metadata are not user-deletable.
        private static bool IsDeletableNode(string tag) =>
            tag.StartsWith("product_", StringComparison.Ordinal) || tag == "functionblock"
            || tag.StartsWith("resource_", StringComparison.Ordinal)
            || tag.StartsWith("dataline_", StringComparison.Ordinal)
            || tag.StartsWith("airlink_", StringComparison.Ordinal)
            || tag is "event" or "event_power" or "action" or "condition" or "program_sub" or "program_case" or "case_action";

        private static bool HasLinkHalves(ProjectElement element) =>
            element.DescendantsAndSelf().Any(d => d.IsLinkHalf);

        private static bool WouldThrowStrict(Project project, ElementId id)
        {
            try
            {
                project.Edit().DeleteById(id, DeleteReferencePolicy.Strict);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;   // a program row still references the subtree — deletion needs the cascade
            }
        }
    }

    /// <summary>Which delete a node needs (sliver #9, relocated from the app): a link row removes its reciprocal
    /// (US-057), a locality cascades its contents (US-009), any other deletable node deletes with an optional
    /// reference cascade; a non-deletable node reports <see cref="NotDeletable"/>.</summary>
    public enum DeleteKind { NotDeletable, Link, Locality, General }

    /// <summary>The non-mutating impact of deleting a node (US-009/US-053), for the GUI's confirm-before-delete flow:
    /// whether it can be deleted, whether that needs confirmation (it cascades), and which delete <see cref="Kind"/>
    /// dispatches. The confirmation WORDING stays app-side (D05); this decides only the kind and whether a confirm is
    /// needed. For a <see cref="DeleteKind.General"/> node <see cref="NeedsConfirm"/> doubles as the reference-cascade
    /// flag the <c>DeleteNode</c> command takes.</summary>
    public readonly record struct DeleteImpact(bool Deletable, bool NeedsConfirm, DeleteKind Kind);
}
