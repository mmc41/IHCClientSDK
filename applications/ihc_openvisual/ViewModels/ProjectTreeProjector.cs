using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-1: the Avalonia-free projection of a <see cref="Project"/> into the VM's
/// <see cref="TreeNodeViewModel"/> forest. Extracted from <c>MainWindowViewModel</c> so the reconciler (W3-4) and
/// the full-rebuild fallback share one row-rendering path. Constructed per render over the open <paramref name="project"/>;
/// pure (project in, nodes out). Row-rendering logic is moved verbatim from the view-model.
/// </summary>
public sealed class ProjectTreeProjector(Project project)
{
    // Reads go through the SDK read surface (project.View, Ihc.Vis) and the shared project.NameOr projection
    // (ProjectReadExtensions), not raw GetAttribute (W1-6). The projected element always belongs to `project`.

    /// <summary>The function block's Programs subtree (US-028/029): block → Programs → Program → Events/Commands.</summary>
    public TreeNodeViewModel BuildBlockProgramsNode(ProjectElement block, string name)
    {
        bool locked = project.View(block).Locked;
        var blockNode = new TreeNodeViewModel(name, NodeIcons.FunctionBlock(locked),
            isExpanded: true, elementId: block.Id) { Kind = TreeNodeKind.ProgramBlockRoot };
        ProjectElement? programs = block.FindChild("programs");
        // Container captions are the containers' STORED names (S-33): `programs`, `events` and `actions` all carry
        // one in the file, so an invented English word would drift from what the project actually says.
        var programsNode = new TreeNodeViewModel(
            programs is null ? "Programmer" : project.NameOr(programs, "Programmer"),
            NodeIcons.For("programs", null),
            isExpanded: true, elementId: programs?.Id) { Kind = TreeNodeKind.Programs };
        if (programs is not null)
        {
            foreach (ProjectElement program in programs.ChildrenOrEmpty().Where(p => p.IsProgram))
            {
                var programNode = new TreeNodeViewModel(project.NameOr(program, "Program"),
                    NodeIcons.For("program_simple", null), isExpanded: true, elementId: program.Id)
                    { Kind = TreeNodeKind.Program };
                if (program.FindChild("events") is { } events)
                {
                    var eventsNode = new TreeNodeViewModel(project.NameOr(events, "Hændelser"),
                        NodeIcons.For("events", null),
                        isExpanded: true, elementId: events.Id) { Kind = TreeNodeKind.Events };
                    foreach (ProjectElement ev in events.ChildrenOrEmpty().Where(e => e.IsProgramEvent))
                        eventsNode.Children.Add(new TreeNodeViewModel(EventCommandLabel(ev),
                            NodeIcons.For(ev.Tag, null), elementId: ev.Id)
                            { Kind = TreeNodeKind.Event, CrossReferences = CrossReferencesOf(ev) });
                    programNode.Children.Add(eventsNode);
                }
                if (program.FindChild("actions") is { } actions)
                {
                    var commandsNode = new TreeNodeViewModel(project.NameOr(actions, "Kommandoer"),
                        NodeIcons.For("actions", null),
                        isExpanded: true, elementId: actions.Id) { Kind = TreeNodeKind.Commands };
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
            if (child.IsProgramCommand)
                commandsNode.Children.Add(new TreeNodeViewModel(EventCommandLabel(child),
                    NodeIcons.For("action", null), elementId: child.Id)
                    { Kind = TreeNodeKind.Command, CrossReferences = CrossReferencesOf(child) });
            else if (child.IsSubProgram)
                commandsNode.Children.Add(BuildSubProgramNode(child));
            else if (child.IsProgramCase)
                commandsNode.Children.Add(BuildCaseNode(child));
        }
    }

    // Renders a conditional sub-program (US-029): its Conditions group and true/false command branches.
    private TreeNodeViewModel BuildSubProgramNode(ProjectElement sub)
    {
        // NodeKind "subProgram", NOT the icon: NodeIcons maps program_sub and program_case to the SAME
        // glyph, so an icon-derived kind would merge these with case switches.
        // The label is the user's stored name (A-26/F-075); a never-renamed sub-program carries the vendor
        // default "Under program", shown here as the English default token "Sub-program" (R-1 — the default is
        // chrome, but a user name stays verbatim). "Under program" is FbGrammar.SubProgramName (internal).
        string stored = project.View(sub).Name ?? string.Empty;
        // W6/F8: the app's own label for an unnamed sub-program is Danish, like the rest of its chrome — and it is
        // the same word the file itself stores, so the app no longer restates the project's language in English.
        string label = stored.Length == 0 ? "Under program" : stored;
        var node = new TreeNodeViewModel(label, NodeIcons.For("program_sub", null),
            isExpanded: true, elementId: sub.Id) { Kind = TreeNodeKind.SubProgram };
        if (sub.FindChild("conditions") is { } conditions)
            node.Children.Add(BuildConditionsNode(conditions));
        foreach (ProjectElement branch in sub.ChildrenOrEmpty().Where(a => a.IsActionsContainer))
        {
            bool isTrue = (project.View(branch).Effective("type") ?? "") == "_0x1";
            var branchNode = new TreeNodeViewModel(
                isTrue ? "Kommandoer ved betingelser sande" : "Kommandoer ved betingelser falske",
                NodeIcons.For("actions", null), isExpanded: true, elementId: branch.Id)
                { Kind = isTrue ? TreeNodeKind.CommandsWhenTrue : TreeNodeKind.CommandsWhenFalse };
            RenderActionsInto(branchNode, branch);
            node.Children.Add(branchNode);
        }
        return node;
    }

    // Renders a conditions group (US-029): its condition rows and nested logic groups; the AND/OR combination shows in
    // the icon (& vs >=1) and a label suffix.
    private TreeNodeViewModel BuildConditionsNode(ProjectElement conditions, bool nested = false)
    {
        bool or = project.View(conditions).Effective("type") == "or";
        // "Betingelser" is measured (uxparity2 V6); "Logisk gruppe" for a NESTED group is the consistent Danish term
        // — the nested case was not in the recorded dump, so it is translated for consistency, not from measurement.
        string label = $"{(nested ? "Logisk gruppe" : "Betingelser")} ({(or ? ">=1" : "&")})";
        var node = new TreeNodeViewModel(label, NodeIcons.For(or ? "conditions-or" : "conditions", null),
            isExpanded: true, elementId: conditions.Id)
            { IsOrGroup = or, Kind = nested ? TreeNodeKind.LogicGroup : TreeNodeKind.Conditions };
        foreach (ProjectElement child in conditions.ChildrenOrEmpty())
        {
            if (child.IsCondition)
                node.Children.Add(new TreeNodeViewModel(EventCommandLabel(child),
                    NodeIcons.For("condition", null), elementId: child.Id)
                    { Kind = TreeNodeKind.Condition, CrossReferences = CrossReferencesOf(child) });
            else if (child.IsConditionsGroup)
                node.Children.Add(BuildConditionsNode(child, nested: true));
        }
        return node;
    }

    // Renders a case switch (US-031): "Case (<switch variable>)" over its value branches and the default Else branch.
    // Every branch is a command container, so commands can be added to it with the normal gesture.
    private TreeNodeViewModel BuildCaseNode(ProjectElement kase)
    {
        string switchName = ResolveOperandName(project.View(kase).Effective("link"));
        var node = new TreeNodeViewModel($"Case ({switchName})", NodeIcons.For("program_case", null),
            isExpanded: true, elementId: kase.Id) { Kind = TreeNodeKind.Case, CrossReferences = CrossReferencesOf(kase) };
        foreach (ProjectElement child in kase.ChildrenOrEmpty())
        {
            if (child.IsCaseValue)
            {
                // "caseValue", not "commands": this row's LABEL is user data and it is ALSO an
                // IsCommandsContainer, so neither the label nor the flag can tell it from a real
                // Commands container — it needs a kind of its own or the two merge in the census.
                var valueNode = new TreeNodeViewModel(project.NameOr(child, "værdi"),
                    NodeIcons.For("case_action", null), isExpanded: true, elementId: child.Id)
                    { Kind = TreeNodeKind.CaseValue };
                RenderActionsInto(valueNode, child);   // the embedded criterion operand is skipped (not a command)
                node.Children.Add(valueNode);
            }
            else if (child.IsActionsContainer)
            {
                var elseNode = new TreeNodeViewModel("Else", NodeIcons.For("actions", null),
                    isExpanded: true, elementId: child.Id) { Kind = TreeNodeKind.CaseElse };
                RenderActionsInto(elseNode, child);
                node.Children.Add(elseNode);
            }
        }
        return node;
    }

    // Renders a program event/action row (US-028): the stored %P/%S template resolved to its operands' live names.
    private string EventCommandLabel(ProjectElement leaf)
    {
        string name = project.NameOr(leaf, leaf.Tag);
        return name.Replace("%P", ResolveOperandName(project.View(leaf).Effective("link1")))
                   .Replace("%S", ResolveOperandName(project.View(leaf).Effective("link2")));
    }

    private string ResolveOperandName(string? token) =>
        ElementId.TryParse(token, out ElementId id) && project.FindById(id) is { } operand
            ? project.View(operand).Name ?? string.Empty
            : string.Empty;

    // The cross-reference dependency edges for a row the projector just built — the OTHER elements whose live
    // name/value it rendered into the row's label — stamped inline (CrossReferences = …) by each builder that
    // produces one of these kinds, so the edges can never drift from what was actually rendered and no whole-tree
    // re-resolve is needed (T022). The reconciler reads them off the projection instead of re-deriving them. A
    // link/scene row renders the opposite end's ancestor names (the shared partner walk); a program event/command/
    // condition renders its %P/%S operands; a case its switch operand. Every other row's label is composed only of
    // its own attributes, so it keeps the default empty list.
    private IReadOnlyList<ElementId> CrossReferencesOf(ProjectElement element)
    {
        if (element.IsLinkHalf || element.IsSceneMember)
        {
            var ids = new List<ElementId>();
            foreach (ProjectElement ancestor in TreeLabelFormatter.LinkPartnerChain(project, element))
                if (ancestor.Id is { } id)
                    ids.Add(id);
            return ids;
        }
        if (element.IsProgramEvent || element.IsProgramCommand || element.IsCondition)
            return OperandRefs(element, "link1", "link2");
        if (element.IsProgramCase)
            return OperandRefs(element, "link");
        return Array.Empty<ElementId>();
    }

    private List<ElementId> OperandRefs(ProjectElement element, params string[] attrs)
    {
        var ids = new List<ElementId>();
        foreach (string attr in attrs)
            if (ElementId.TryParse(project.View(element).Effective(attr), out ElementId id) && project.FindById(id) is not null)
                ids.Add(id);
        return ids;
    }

    /// <summary>The caption the localities root falls back to when a file leaves the <c>groups</c> container
    /// unnamed. Declared here, where the row is built, because the status messages that name the container an edit
    /// landed in must read the same word the row shows — they resolve the stored name first and fall back to this
    /// one, so a message saying "under X" beside a root reading "Y" is not expressible.</summary>
    public const string LocalitiesRootName = "Lokaliteter";

    // Both panes share the Localities skeleton; the Installation pane nests each locality's products (with their
    // pins), the Functions pane its function blocks (US-006/US-010).
    public TreeNodeViewModel BuildLocalitiesRoot(bool functions)
    {
        // The root row shows the container's own stored name — it is project data (the vendor's "Lokaliteter"),
        // not a caption the app owns; LocalitiesRootName only stands in when a file leaves it unnamed.
        // The root row deliberately carries NO element id, even though the <groups> container is a real element:
        // an id would make it a target for every id-addressed command (delete, properties, help), and what the
        // root should answer to is a separate question per command. Kind is what identifies it; the one command
        // that needs the container — paste, whose target parent this is — resolves it from the project by kind.
        string rootName = project.Child("groups") is { } container
            ? project.NameOr(container, LocalitiesRootName)
            : LocalitiesRootName;
        var root = new TreeNodeViewModel(rootName, NodeIcons.Locality, isExpanded: true)
            { Kind = TreeNodeKind.LocalitiesRoot };
        foreach (ProjectElement group in project.Groups)
        {
            string name = project.NameOr(group, "(uden navn)");
            var components = new List<ProjectElement>();
            foreach (ProjectElement child in group.ChildrenOrEmpty())
            {
                if ((child.Kind == ElementKind.FunctionBlock) == functions)
                    components.Add(child);
            }
            // A locality starts closed however much it holds (US-006: expanding is the user's move). Opening the
            // populated ones by default would bury the overview of the installation the root is there to give.
            // It does open when it gains its FIRST child, so a just-inserted product is visible - that is a
            // transition, not an initial state, hence the separate flag.
            var locality = new TreeNodeViewModel(name, NodeIcons.Locality, isExpanded: false,
                isBold: true, elementId: group.Id)
                { Tooltip = BuildTooltip(group), Kind = TreeNodeKind.Locality, RevealsOnFirstChild = true };
            foreach (ProjectElement child in components)
                locality.Children.Add(BuildComponentNode(child));
            root.Children.Add(locality);
        }
        return root;
    }

    // A product / function block node. A product flattens its resource (pin) children (structural containers are
    // omitted); a function block shows its four variable sections (US-018/US-019).
    private TreeNodeViewModel BuildComponentNode(ProjectElement component)
    {
        string name = project.NameOr(component, component.Tag);
        if (component.Kind == ElementKind.FunctionBlock)
            return BuildFunctionBlockNode(component, name, programmingMode: false);

        bool unlinked = project.View(component).IsUnlinkedWireless;
        var node = new TreeNodeViewModel(TreeLabelFormatter.ProductLabel(name, project.View(component).Position),
            NodeIcons.For(component.Tag, project.View(component).Icon),
            elementId: component.Id, isUnlinked: unlinked)
            { Tooltip = BuildTooltip(component), Kind = TreeNodeKind.Product };
        foreach (ProjectElement resource in component.ChildrenOrEmpty())
        {
            if (resource.IsScenesContainer)
                node.Children.Add(BuildScenesNode(resource));   // a product's scenario output (scene link target, US-024)
            else if (!ProductRows.IsStructuralChild(resource.Tag)
                     && !ProductRows.IsHiddenFromTree(resource.Tag, project.View(resource).Effective("setting")))
                node.Children.Add(BuildPinNode(resource, catalogDeclared: true));   // catalog-declared pins (A-24, F-001/F-002)
        }
        return node;
    }

    // A product's scenes container — a scenario-link target — showing its scene member rows (US-024).
    private TreeNodeViewModel BuildScenesNode(ProjectElement scenes)
    {
        var node = new TreeNodeViewModel(project.NameOr(scenes, "Scenarier"), "/Assets/scenario.svg",
            elementId: scenes.Id) { Kind = TreeNodeKind.Scenes };
        foreach (ProjectElement member in scenes.ChildrenOrEmpty())
        {
            if (member.IsSceneMember)
                node.Children.Add(BuildSceneMemberNode(member));
        }
        return node;
    }

    private TreeNodeViewModel BuildSceneMemberNode(ProjectElement member)
    {
        // A shutter member renders the BARE opposite path + direction as the product's shutter pin name (F-051/A-19);
        // relay/dimmer keep "= <value>". Value/ramp belong to the scene-container dialog, not this row.
        string label;
        if (member.IsSceneShutter)
        {
            label = ShutterDirectionPinName(member) is { Length: > 0 } dir
                ? $"{TreeLabelFormatter.LinkOppositePath(project, member)} / {dir}"
                : TreeLabelFormatter.LinkOppositePath(project, member);
        }
        else
        {
            (string value, string ramp) = TreeLabelFormatter.SceneMemberValue(member);
            string text = ramp.Length > 0 ? $"{value} / {ramp}" : value;
            label = $"{TreeLabelFormatter.LinkOppositePath(project, member)} = {text}";
        }
        return new TreeNodeViewModel(label, "/Assets/link-from.svg",
            elementId: member.Id) { Kind = TreeNodeKind.SceneMember, CrossReferences = CrossReferencesOf(member) };
    }

    private string? ShutterDirectionPinName(ProjectElement member)
    {
        if (member.Id is not { } memberId)
            return null;
        bool up = project.View(member).Effective("shutter_position") == "up";
        string pinTag = up ? "airlink_shutter_up" : "airlink_shutter_down";
        ProjectElement? product = project.FindParent(memberId) is { Id: { } scenesId }
            ? project.FindParent(scenesId)
            : null;
        ProjectElement? shutterPin = product?.ChildrenOrEmpty().FirstOrDefault(c => c.Tag == pinTag);
        return shutterPin is not null ? project.View(shutterPin).Name : null;
    }

    // A function block node. Configuration mode shows Input/Output/Settings (hiding empty ones); programming mode
    // adds Internal variables and keeps every section (US-018/US-026, A-17/A-18).
    public TreeNodeViewModel BuildFunctionBlockNode(ProjectElement fb, string name, bool programmingMode)
    {
        bool locked = project.View(fb).Locked;
        // W7/F6: in PROGRAMMING mode this node is the pane's root, and the installer is here to work on the block's
        // data — so it opens with the root and every section expanded, matching the reference application. In
        // CONFIGURATION mode it is one row among many and stays collapsed, or loading a project would explode every
        // block open at once.
        var node = new TreeNodeViewModel(name, NodeIcons.FunctionBlock(locked), isExpanded: programmingMode,
            elementId: fb.Id, isLockedFunctionBlock: locked)
        {
            Tooltip = BuildTooltip(fb),
            Kind = TreeNodeKind.FunctionBlock,
        };
        foreach ((string container, string label) in FunctionBlockSections.All)
        {
            if (!programmingMode && container == "internalsettings")
                continue;   // Internal variables is programming-mode-only (A-17)
            ProjectElement? holder = fb.FindChild(container);
            if (!programmingMode && (holder is null || !holder.ChildrenOrEmpty().Any()))
                continue;   // configuration mode hides an empty/childless container (A-18)
            // The caption is the container's own stored name when it has one (the standard blocks name their
            // settings section "Indstillinger"); the table's label is the fallback for a container that leaves it
            // unset. Same rule as the locality root: a name in the file is data, not a caption the app owns.
            string sectionLabel = holder is not null ? project.NameOr(holder, label) : label;
            var section = new TreeNodeViewModel(sectionLabel, NodeIcons.For(container, null),
                isExpanded: programmingMode, elementId: holder?.Id)
            {
                KindDetail = container,
                Kind = TreeNodeKind.Section,
            };
            if (holder is not null)
            {
                foreach (ProjectElement pin in holder.ChildrenOrEmpty())
                    section.Children.Add(BuildPinNode(pin, inFunctionBlockVariableSection: true));
            }
            node.Children.Add(section);
        }
        return node;
    }

    // The hover tooltip (US-047/US-048): the documentation note plus, for a resource-mapped node, its IHC resource id.
    private string? BuildTooltip(ProjectElement element)
    {
        var parts = new List<string>();
        if (project.View(element).Note is { Length: > 0 } note)
            parts.Add(note.Replace("\r\n", "\n"));
        if (element.HasResourceId && element.Id is { } id)
            parts.Add($"Resource ID: {id.Value}");
        return parts.Count > 0 ? string.Join("\n\n", parts) : null;
    }

    // A state row renders its INITIAL value into the label (F-004) — only resource_enum's inivalue IDREF to an
    // enum_value name; scoped deliberately (inivalue is a literal elsewhere).
    private string? StateValue(ProjectElement resource) =>
        resource.Kind == ElementKind.EnumResource
        && ElementId.TryParse(project.View(resource).Effective("inivalue"), out ElementId valueId)
        && project.FindById(valueId) is { } operand
        && project.View(operand).Name is { Length: > 0 } state
            ? state
            : null;

    // (uxparity2 T027/T031) The settings-only time literal that used to live here is GONE. It rendered a time value
    // for the `settings` section alone, so the same variable read differently depending on which section it sat in.
    // VariableValueFormat now renders every type identically in all four sections, which is what was measured.

    // A scene row shows its note after the name — "Scenarie Tænd (Fremkalder scen...)" — because a scene's note is
    // what says which fixtures it drives, and the row is otherwise indistinguishable from its siblings. A note
    // longer than the budget is cut and elided. Scoped to scenes: input/output pins carry notes too (they hold the
    // catalog's installer guidance) and render bare.
    private const int SceneNoteBudget = 15;

    private string? SceneNoteSuffix(ProjectElement resource)
    {
        if (resource.Tag != "resource_scene" || project.View(resource).Note is not { Length: > 0 } note)
            return null;
        return note.Length > SceneNoteBudget ? note[..SceneNoteBudget] + "..." : note;
    }

    private TreeNodeViewModel BuildPinNode(ProjectElement resource, bool inFunctionBlockVariableSection = false,
        bool catalogDeclared = false)
    {
        // One read view for the whole row — name, value, icon and the backup flag all come off it.
        ElementView view = project.View(resource);
        string name = project.NameOr(resource, resource.Tag);
        // W8/F7: a function-block VARIABLE renders its value per TYPE, in every one of the block's four sections.
        // It used to be section-dependent — a time literal appeared only under `settings` — so the same variable read
        // differently depending on where it sat, while the reference application renders it identically everywhere
        // (uxparity2 V6/T011, all 21 types measured). The measurement covered a BLOCK's sections only, so a product's
        // own terminal/setting rows keep the rendering they had.
        string? value = (inFunctionBlockVariableSection
                            ? VariableValueFormat.For(resource.Tag, view.Effective, StateValue(resource))
                            : StateValue(resource))
                     ?? view.Value;
        bool isOutput = resource.IsOutputPin;
        bool saved = isOutput && view.Backup;
        // The label carries the pin's name and, for a state row, its value; the save flag surfaces via IsValueSaved (F-019).
        string label = string.IsNullOrEmpty(value) ? name : $"{name} = {value}";
        if (SceneNoteSuffix(resource) is { } sceneNote)
            label += $" ({sceneNote})";
        var node = new TreeNodeViewModel(label, NodeIcons.For(resource.Tag, view.Icon),
            elementId: resource.Id)
            {
                IsOutputPin = isOutput, IsValueSaved = saved, Tooltip = BuildTooltip(resource),
                IsCatalogPin = catalogDeclared,
                IsProductTerminal = resource.Kind == Ihc.Vis.ElementKind.DatalinePin,
                IsLogMarkPin = resource.IsLogRow(project),
                KindDetail = resource.Tag, Kind = TreeNodeKind.Pin,
            };
        // A linked pin reveals its follow-link / scene-link rows (US-022/025).
        foreach (ProjectElement child in resource.ChildrenOrEmpty())
        {
            if (child.IsLinkHalf)
                node.Children.Add(BuildLinkNode(child));
        }
        return node;
    }

    // A "link from"/"link to" row under a pin, labelled with the bare full path of the opposite end; direction is
    // carried by the icon (and NodeKind), never the label text (F-020).
    private TreeNodeViewModel BuildLinkNode(ProjectElement linkRow)
    {
        bool isSourceEnd = linkRow.IsLinkFromEnd;
        string icon = isSourceEnd ? "/Assets/link-from.svg" : "/Assets/link-to.svg";
        return new TreeNodeViewModel(TreeLabelFormatter.LinkOppositePath(project, linkRow), icon, elementId: linkRow.Id)
            { Kind = linkRow.IsSceneLink ? TreeNodeKind.SceneLink : isSourceEnd ? TreeNodeKind.LinkFrom : TreeNodeKind.LinkTo,
              CrossReferences = CrossReferencesOf(linkRow) };
    }
}
