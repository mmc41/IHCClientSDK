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

        /// <summary>Command to edit the Name and Note of a locality, function block, or ordinary FB resource variable
        /// <paramref name="id"/> (US-007/US-019/US-026/US-027) — one generic name/note command serves all three (T015).</summary>
        public Session.RenameLocality RenameLocality(Project project, ElementId id, string name, string note) =>
            new Session.RenameLocality(id, name, note);

        /// <summary>Command to set an ordinary FB resource variable's typed initial value (US-027, T016); refused
        /// inside a locked block by T003.</summary>
        public Session.SetResourceInitialValue SetResourceInitialValue(Project project, ElementId id, Session.ResourceInitialValue value) =>
            new Session.SetResourceInitialValue(id, value);

        /// <summary>Command to edit a resource variable's Name, Note, and typed initial value in ONE undoable step
        /// (US-027, T016), refused inside a locked block by T003. A <see cref="Session.ResourceValueKind.None"/> value
        /// leaves the initial value untouched.</summary>
        public Session.SetVariableProperties SetVariableProperties(Project project, ElementId id, string name, string note, Session.ResourceInitialValue value, string helpNote = "") =>
            new Session.SetVariableProperties(id, name, note, value, helpNote);

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

        /// <summary>Command to add a variable of an EXISTING project-global enumerator type to a function-block section
        /// (US-030 enum-type picker, PG-4) — authors no new type; null when the section is not a function-block variable
        /// section.</summary>
        public Session.AddEnumVariableOfExistingType? AddEnumVariableOfType(
            Project project, ElementId sectionId, string variableName, string typeName) =>
            project.FindById(sectionId) is { } section
                && project.FindParent(sectionId) is { Id: { } blockId } block && block.Kind == ElementKind.FunctionBlock
                ? new Session.AddEnumVariableOfExistingType(blockId, section.Tag, variableName, typeName)
                : null;

        /// <summary>Command to author a standalone project-global enumerator TYPE (no variable) — a 0-state,
        /// unreferenced type when <paramref name="states"/> is empty (US-030 standalone-type route, PG-7/D02).</summary>
        public Session.AddStandaloneEnumType AddStandaloneEnumType(Project project, string typeName, IReadOnlyList<string> states) =>
            new Session.AddStandaloneEnumType(typeName, states);

        /// <summary>Commands behind the six buttons of IHC Visual's <i>Bibliotek ▸ Rediger Enumerator typer</i>
        /// two-pane editor. Each takes the type by NAME and a value by its 0-based POSITION — what the dialog has —
        /// and each refuses (rather than faults) on a "[read only]" built-in, matching the vendor's greyed buttons.</summary>
        public Session.RenameEnumType RenameEnumType(Project project, string typeName, string newName) =>
            new Session.RenameEnumType(typeName, newName);

        /// <inheritdoc cref="RenameEnumType"/>
        public Session.DeleteEnumType DeleteEnumType(Project project, string typeName) =>
            new Session.DeleteEnumType(typeName);

        /// <inheritdoc cref="RenameEnumType"/>
        public Session.AddEnumValue AddEnumValue(Project project, string typeName, string valueName) =>
            new Session.AddEnumValue(typeName, valueName);

        /// <inheritdoc cref="RenameEnumType"/>
        public Session.RenameEnumValue RenameEnumValue(Project project, string typeName, int valueIndex, string newName) =>
            new Session.RenameEnumValue(typeName, valueIndex, newName);

        /// <inheritdoc cref="RenameEnumType"/>
        public Session.DeleteEnumValue DeleteEnumValue(Project project, string typeName, int valueIndex) =>
            new Session.DeleteEnumValue(typeName, valueIndex);

        /// <summary>Command to apply edited product documentation (US-011), capturing the product's current locality
        /// so the command can re-parent it when the Location changed.</summary>
        public Session.UpdateProduct UpdateProduct(Project project, ElementId productId, Session.ProductPropertiesResult result) =>
            new Session.UpdateProduct(productId, result, project.FindParent(productId)?.Id);

        /// <summary>Command to apply edited data-line pin addressing (US-012).</summary>
        public Session.UpdatePin UpdatePin(Project project, ElementId pinId, Session.PinPropertiesResult result) =>
            new Session.UpdatePin(pinId, result);

        /// <summary>Command to unlock a locked library function block for editing (US-020). Unlocking takes ownership,
        /// so it stamps <paramref name="programmer"/> and the date from the service clock (never
        /// <c>DateTime.Now</c>) — see <see cref="Editing.FunctionBlockRef.Unlock"/>.</summary>
        public Session.UnlockFunctionBlock UnlockFunctionBlock(Project project, ElementId id, string programmer) =>
            new Session.UnlockFunctionBlock(id, programmer,
                DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime));

        /// <summary>Command to transform an in-project function block into a locked library instance (US-021), stamping
        /// the export date from the service clock (never <c>DateTime.Now</c>).</summary>
        public Session.SaveFunctionBlockToLibrary SaveFunctionBlockToLibrary(Project project, ElementId id, string name, string programmer, string? note) =>
            new Session.SaveFunctionBlockToLibrary(id, name, programmer,
                DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime), note);

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
        /// or null at the list ends / for a rootless node. One rule, two entry points (review F02): this
        /// Project-walking factory and the index-backed overload below share <see cref="ResolveReorderTarget"/>.</summary>
        public Session.ReorderNode? ReorderNode(Project project, ElementId id, int delta) =>
            ResolveReorderTarget(project.FindById(id), project.FindParent(id), id, delta) is { } there
                ? new Session.ReorderNode(id, there)
                : null;

        // The index-backed entry point of the delta move (review F02): the same US-055 boundary rule answered from a
        // per-commit ProjectIndex, for the per-selection MENU gate — IProjectDocument.CanReorder forwards here, so
        // Move up/down stops paying two full-tree walks and a sibling-list allocation per arrow-key press.
        internal static Session.ReorderNode? ReorderNode(Session.ProjectIndex index, ElementId id, int delta) =>
            ResolveReorderTarget(index.FindById(id), index.FindParent(id), id, delta) is { } there
                ? new Session.ReorderNode(id, there)
                : null;

        // The one delta-move rule (US-055) both entry points share: the same-tag sibling position `delta` steps from
        // the node's own, or null for a no-op delta, a rootless/absent node, or a step off either end. Counts in one
        // pass rather than materializing the sibling list — this runs per pointer event and per selection change.
        private static int? ResolveReorderTarget(
            ProjectElement? node, ProjectElement? parent, ElementId id, int delta)
        {
            int? target = null;
            if (delta != 0 && node is not null && parent is { Id: { } })
            {
                int here = -1;
                int count = 0;
                foreach (ProjectElement sibling in parent.ChildrenOrEmpty())
                {
                    if (sibling.Tag == node.Tag)
                    {
                        if (sibling.Id == id)
                        {
                            here = count;
                        }
                        count++;
                    }
                }
                int there = here + delta;
                target = here < 0 || there < 0 || there >= count ? null : there;
            }
            return target;
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
        /// <see cref="ReorderNodeToSibling"/>. One rule, two entry points (review F5): this Project-walking query
        /// and the index-backed overload below share <see cref="IsReorderablePair"/>.</summary>
        public bool CanReorderNode(Project project, ElementId dragged, ElementId target) =>
            dragged != target && IsReorderablePair(
                project.FindById(dragged), project.FindById(target),
                project.FindParent(dragged), project.FindParent(target));

        // The index-backed entry point (review F5): the same US-055 rule answered from a per-commit ProjectIndex,
        // for the drag-over pointer path — IProjectDocument.CanReorderNode forwards here, so the probe stops
        // paying full-tree FindById/FindParent walks per pointer event.
        internal static bool CanReorderNode(Session.ProjectIndex index, ElementId dragged, ElementId target) =>
            dragged != target && IsReorderablePair(
                index.FindById(dragged), index.FindById(target),
                index.FindParent(dragged), index.FindParent(target));

        // The one reorder-drop rule (US-055) both entry points share: resolved, same-tag elements under the same
        // id-bearing parent.
        private static bool IsReorderablePair(
            ProjectElement? dragged, ProjectElement? target,
            ProjectElement? draggedParent, ProjectElement? targetParent) =>
            dragged is not null && target is not null && dragged.Tag == target.Tag
            && draggedParent is { Id: { } parentId }
            && targetParent?.Id == parentId;

        // The cheap delete classification shared by CanDelete, PreviewDelete AND Session.DeleteNode.Evaluate (G7: the
        // command must refuse exactly what the gate forbids, so both read this one chokepoint): which DeleteKind a node
        // needs, WITHOUT the (potentially expensive) strict-cascade simulation the confirm flag needs. Resolves the
        // element once and hands it back so PreviewDelete can compute the confirm without a second lookup.
        internal static (DeleteKind Kind, ProjectElement? Element) ClassifyDelete(Project project, ElementId id)
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
        public Session.AddProgramEvent? AddProgramEvent(Project project, ElementId containerId, ElementId variableId, string method, string name, string? note, ElementId? operandId = null) =>
            ProgramOfEventsContainer(project, containerId) is { } programId
                ? new Session.AddProgramEvent(programId, variableId, method, name, note, operandId) : null;

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

        /// <summary>Command to add a new program to a function block's <c>programs</c> container (US-026/W4). A block
        /// may hold several programs; each is created empty, with its own events and commands containers.</summary>
        public Session.AddProgram AddProgram(Project project, ElementId programsId, string name) =>
            new Session.AddProgram(programsId, name);

        /// <summary>Command to add a condition to a conditions group (US-029).</summary>
        public Session.AddCondition AddCondition(Project project, ElementId conditionsId, ElementId variableId, string method, string name, string? note, ElementId? operandId = null) =>
            new Session.AddCondition(conditionsId, variableId, method, name, note, operandId);

        /// <summary>Command to toggle a conditions group's AND/OR combination (US-029).</summary>
        public Session.SetConditionsLogic SetConditionsLogic(Project project, ElementId conditionsId, bool or) =>
            new Session.SetConditionsLogic(conditionsId, or);

        /// <summary>Command to add a nested logic group inside a conditions group (US-029).</summary>
        public Session.AddLogicGroup AddLogicGroup(Project project, ElementId conditionsId) =>
            new Session.AddLogicGroup(conditionsId);

        /// <summary>Command to author one arithmetic command line into a command container (US-032).</summary>
        public Session.AddArithmeticCommand AddArithmeticCommand(
            Project project,
            ElementId commandsId,
            ElementId targetId,
            string method,
            ElementId operandId,
            string name,
            string note) =>
            new Session.AddArithmeticCommand(commandsId, targetId, method, operandId, name, note);

        /// <summary>Command to insert a case structure keyed on a switch variable (US-031).</summary>
        public Session.AddCase AddCase(Project project, ElementId commandsId, ElementId switchVariableId) =>
            new Session.AddCase(commandsId, switchVariableId);

        /// <summary>Command to add a case-value branch to a <c>program_case</c> (US-031), or null for a non-case target
        /// or a missing switch. A literal switch takes the criterion verbatim; an ENUM switch (T014, PG-6) accepts the
        /// criterion only when it names one of the switch's enum-type states, carrying the type name so the command
        /// routes to the enum overload (typedef + the state's inivalue). The switch is resolved here from the case's link.</summary>
        public Session.AddCaseValue? AddCaseValue(Project project, ElementId caseId, string criterion)
        {
            if (project.FindById(caseId) is not { } kase || !kase.IsProgramCase
                || !ElementId.TryParse(project.View(kase).Effective("link"), out ElementId switchId)
                || project.FindById(switchId) is not { } switchVar)
            {
                return null;
            }
            if (switchVar.Kind != ElementKind.EnumResource)
            {
                return new Session.AddCaseValue(caseId, criterion, switchVar.Tag);
            }
            // Enum switch: the criterion is a STATE name of the switch's enum type. Resolve the type and accept only a
            // real state so the branch operand can carry the right typedef + inivalue.
            return ElementId.TryParse(project.View(switchVar).Effective("typedef"), out ElementId defId)
                && project.FindById(defId) is { } def && project.View(def).Name is { } typeName
                && def.ChildrenOrEmpty().Any(v => v.IsEnumValue && project.View(v).Name == criterion)
                    ? new Session.AddCaseValue(caseId, criterion, switchVar.Tag, typeName) : null;
        }

        /// <summary>Command to set an output's "Gem aktuel værdi" power-loss persistence (US-033).</summary>
        public Session.SetOutputBackup SetOutputBackup(Project project, ElementId outputId, bool save) =>
            new Session.SetOutputBackup(outputId, save);

        /// <summary>Command to toggle a "Log …" row's log mark (US-068).</summary>
        public Session.ToggleLogMark ToggleLogMark(Project project, ElementId logRowId) =>
            new Session.ToggleLogMark(logRowId);

        // Resolves the program owning an `events` container (US-028/US-033), or null when the target is not one. The
        // owner must be a program_simple — the only program that carries events — matching AddProgramEvent/AddPowerEvent's
        // own RequireTag(…, "program_simple"), so the factory never mints a command those commands would then refuse (A4).
        private static ElementId? ProgramOfEventsContainer(Project project, ElementId containerId) =>
            project.FindById(containerId)?.IsEventsContainer == true
            && project.FindParent(containerId) is { Id: { } programId } parent && parent.Tag == "program_simple"
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

        /// <summary>Command to edit the enumerator type referenced by a <c>resource_enum</c> variable (US-030), or null
        /// for a non-enum (or locked-block) target. The dialog's full ordered state list is diffed here against the
        /// type's current values by POSITION: a changed label at an existing position becomes a relabel (T013), the tail
        /// beyond the existing count becomes appends — so an edit of nothing falls out as a NoChange. (Reorder / remove
        /// are out of scope, D05, so the list is an in-order prefix of existing values plus appends.)</summary>
        public Session.UpdateEnumStates? UpdateEnumStates(Project project, ElementId enumVariableId, IReadOnlyList<string> states)
        {
            if (project.FindById(enumVariableId) is not { } variable
                || variable.Kind != ElementKind.EnumResource
                // T004: withdraw the enum-state edit when its entry-point variable is inside a locked block (the enum
                // TYPE it would edit is project-global, so the lock is checked on the variable acted upon, not the def).
                || ProjectEditor.IsWithinLockedBlock(project.Root, enumVariableId, inclusive: true)
                || !ElementId.TryParse(project.View(variable).Effective("typedef"), out ElementId defId)
                || project.FindById(defId) is not { } def || project.View(def).Name is not { } defName)
            {
                return null;
            }
            var existingValues = def.ChildrenOrEmpty().Where(c => c.IsEnumValue).ToList();
            var relabels = new List<(ElementId ValueId, string NewName)>();
            int prefix = Math.Min(existingValues.Count, states.Count);
            for (int i = 0; i < prefix; i++)
            {
                if (states[i] != (project.View(existingValues[i]).Name ?? string.Empty))
                {
                    relabels.Add((existingValues[i].Id!.Value, states[i]));
                }
            }
            string[] added = states.Skip(existingValues.Count).ToArray();
            return new Session.UpdateEnumStates(defName, added) { Relabels = relabels };
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
            || tag is "program_simple" or "event" or "event_power" or "action" or "condition"
                or "program_sub" or "program_case" or "case_action";

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
