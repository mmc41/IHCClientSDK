using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis;
using Ihc.Vis.Editing;
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
    // Read element attributes through the SDK read surface (project.View), not raw GetAttribute (W1-6). The
    // projected element always belongs to `project`, which supplies the schema context.
    private ElementView View(ProjectElement element) => project.View(element);

    // The element's effective name, or the fallback when it is empty.
    private string NameOr(ProjectElement element, string fallback) =>
        View(element).Name is { Length: > 0 } name ? name : fallback;

    /// <summary>The function block's Programs subtree (US-028/029): block → Programs → Program → Events/Commands.</summary>
    public TreeNodeViewModel BuildBlockProgramsNode(ProjectElement block, string name)
    {
        bool locked = View(block).Locked;
        var blockNode = new TreeNodeViewModel(name, locked ? "/Assets/fb-lk.svg" : "/Assets/fb-editable.svg",
            isExpanded: true, elementId: block.Id) { Kind = TreeNodeKind.ProgramBlockRoot };
        ProjectElement? programs = block.FindChild("programs");
        var programsNode = new TreeNodeViewModel("Programs", NodeIcons.For("programs", null),
            isExpanded: true, elementId: programs?.Id) { Kind = TreeNodeKind.Programs };
        if (programs is not null)
        {
            foreach (ProjectElement program in programs.ChildrenOrEmpty().Where(p => p.IsProgram))
            {
                var programNode = new TreeNodeViewModel(NameOr(program, "Program"),
                    NodeIcons.For("program_simple", null), isExpanded: true, elementId: program.Id)
                    { Kind = TreeNodeKind.Program };
                if (program.FindChild("events") is { } events)
                {
                    var eventsNode = new TreeNodeViewModel("Events", NodeIcons.For("events", null),
                        isExpanded: true, elementId: events.Id) { Kind = TreeNodeKind.Events };
                    foreach (ProjectElement ev in events.ChildrenOrEmpty().Where(e => e.IsProgramEvent))
                        eventsNode.Children.Add(new TreeNodeViewModel(EventCommandLabel(ev),
                            NodeIcons.For(ev.Tag, null), elementId: ev.Id) { Kind = TreeNodeKind.Event });
                    programNode.Children.Add(eventsNode);
                }
                if (program.FindChild("actions") is { } actions)
                {
                    var commandsNode = new TreeNodeViewModel("Commands", NodeIcons.For("actions", null),
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
                    NodeIcons.For("action", null), elementId: child.Id) { Kind = TreeNodeKind.Command });
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
        string stored = View(sub).Name ?? string.Empty;
        string label = stored.Length == 0 || stored == "Under program" ? "Sub-program" : stored;
        var node = new TreeNodeViewModel(label, NodeIcons.For("program_sub", null),
            isExpanded: true, elementId: sub.Id) { Kind = TreeNodeKind.SubProgram };
        if (sub.FindChild("conditions") is { } conditions)
            node.Children.Add(BuildConditionsNode(conditions));
        foreach (ProjectElement branch in sub.ChildrenOrEmpty().Where(a => a.IsActionsContainer))
        {
            bool isTrue = (View(branch).Effective("type") ?? "") == "_0x1";
            var branchNode = new TreeNodeViewModel(
                isTrue ? "Commands when conditions true" : "Commands when conditions false",
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
        bool or = View(conditions).Effective("type") == "or";
        string label = $"{(nested ? "Logic group" : "Conditions")} ({(or ? ">=1" : "&")})";
        var node = new TreeNodeViewModel(label, NodeIcons.For(or ? "conditions-or" : "conditions", null),
            isExpanded: true, elementId: conditions.Id)
            { IsOrGroup = or, Kind = nested ? TreeNodeKind.LogicGroup : TreeNodeKind.Conditions };
        foreach (ProjectElement child in conditions.ChildrenOrEmpty())
        {
            if (child.IsCondition)
                node.Children.Add(new TreeNodeViewModel(EventCommandLabel(child),
                    NodeIcons.For("condition", null), elementId: child.Id) { Kind = TreeNodeKind.Condition });
            else if (child.IsConditionsGroup)
                node.Children.Add(BuildConditionsNode(child, nested: true));
        }
        return node;
    }

    // Renders a case switch (US-031): "Case (<switch variable>)" over its value branches and the default Else branch.
    // Every branch is a command container, so commands can be added to it with the normal gesture.
    private TreeNodeViewModel BuildCaseNode(ProjectElement kase)
    {
        string switchName = ResolveOperandName(View(kase).Effective("link"));
        var node = new TreeNodeViewModel($"Case ({switchName})", NodeIcons.For("program_case", null),
            isExpanded: true, elementId: kase.Id) { Kind = TreeNodeKind.Case };
        foreach (ProjectElement child in kase.ChildrenOrEmpty())
        {
            if (child.IsCaseValue)
            {
                // "caseValue", not "commands": this row's LABEL is user data and it is ALSO an
                // IsCommandsContainer, so neither the label nor the flag can tell it from a real
                // Commands container — it needs a kind of its own or the two merge in the census.
                var valueNode = new TreeNodeViewModel(NameOr(child, "value"),
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
        string name = NameOr(leaf, leaf.Tag);
        return name.Replace("%P", ResolveOperandName(View(leaf).Effective("link1")))
                   .Replace("%S", ResolveOperandName(View(leaf).Effective("link2")));
    }

    private string ResolveOperandName(string? token) =>
        ElementId.TryParse(token, out ElementId id) && project.FindById(id) is { } operand
            ? project.View(operand).Name ?? string.Empty
            : string.Empty;

    private const string LocalityIcon = "/Assets/locality.svg";

    // Both panes share the Localities skeleton; the Installation pane nests each locality's products (with their
    // pins), the Functions pane its function blocks (US-006/US-010).
    public TreeNodeViewModel BuildLocalitiesRoot(bool functions)
    {
        var root = new TreeNodeViewModel("Localities", LocalityIcon, isExpanded: true)
            { Kind = TreeNodeKind.LocalitiesRoot };
        foreach (ProjectElement group in project.Groups)
        {
            string name = NameOr(group, "(unnamed)");
            var components = new List<ProjectElement>();
            foreach (ProjectElement child in group.ChildrenOrEmpty())
            {
                if ((child.Kind == ElementKind.FunctionBlock) == functions)
                    components.Add(child);
            }
            // A locality that holds components opens by default so they are visible (US-006 container reveal).
            var locality = new TreeNodeViewModel(name, LocalityIcon, isExpanded: components.Count > 0,
                isBold: true, elementId: group.Id) { Tooltip = BuildTooltip(group), Kind = TreeNodeKind.Locality };
            foreach (ProjectElement child in components)
                locality.Children.Add(BuildComponentNode(child));
            root.Children.Add(locality);
        }
        return root;
    }

    // A product's tree label carries its placement descriptor: "name (position) " — trailing space included — and
    // the bare name when position is absent (F-003). The trailing space is the vendor's and is reproduced so a
    // label-mode diff against IHC Visual stays exact.
    private static string ProductLabel(string name, string? position) =>
        string.IsNullOrEmpty(position) ? name : $"{name} ({position}) ";

    // A product / function block node. A product flattens its resource (pin) children (structural containers are
    // omitted); a function block shows its four variable sections (US-018/US-019).
    private TreeNodeViewModel BuildComponentNode(ProjectElement component)
    {
        string name = NameOr(component, component.Tag);
        if (component.Kind == ElementKind.FunctionBlock)
            return BuildFunctionBlockNode(component, name, programmingMode: false);

        bool unlinked = View(component).IsUnlinkedWireless;
        var node = new TreeNodeViewModel(ProductLabel(name, View(component).Position),
            NodeIcons.For(component.Tag, View(component).Icon),
            elementId: component.Id, isUnlinked: unlinked)
            { Tooltip = BuildTooltip(component), Kind = TreeNodeKind.Product };
        foreach (ProjectElement resource in component.ChildrenOrEmpty())
        {
            if (resource.IsScenesContainer)
                node.Children.Add(BuildScenesNode(resource));   // a product's scenario output (scene link target, US-024)
            else if (!ProductRows.IsStructuralChild(resource.Tag)
                     && !ProductRows.IsHiddenFromTree(resource.Tag, View(resource).Effective("setting")))
                node.Children.Add(BuildPinNode(resource, catalogDeclared: true));   // catalog-declared pins (A-24, F-001/F-002)
        }
        return node;
    }

    // A product's scenes container — a scenario-link target — showing its scene member rows (US-024).
    private TreeNodeViewModel BuildScenesNode(ProjectElement scenes)
    {
        var node = new TreeNodeViewModel(NameOr(scenes, "Scenarier"), "/Assets/scenario.svg",
            elementId: scenes.Id) { Kind = TreeNodeKind.Scenes };
        foreach (ProjectElement member in scenes.ChildrenOrEmpty())
        {
            if (member.IsSceneMember)
                node.Children.Add(BuildSceneMemberNode(member));
        }
        return node;
    }

    private static (string Value, string RampTime) SceneMemberValue(ProjectElement member)
    {
        if (!SceneValue.TryParse(member, out SceneValue sv))
            return (string.Empty, string.Empty);
        return sv.Kind switch
        {
            SceneValueKind.Relay => (sv.On ? "ON" : "OFF", string.Empty),
            SceneValueKind.Dimmer => ($"{sv.LevelPercent}%", $"{sv.RampTime.TotalSeconds:0.#}s"),
            SceneValueKind.Shutter => (sv.ShutterUp ? "up" : "down", string.Empty),
            _ => (string.Empty, string.Empty),
        };
    }

    private TreeNodeViewModel BuildSceneMemberNode(ProjectElement member)
    {
        // A shutter member renders the BARE opposite path + direction as the product's shutter pin name (F-051/A-19);
        // relay/dimmer keep "= <value>". Value/ramp belong to the scene-container dialog, not this row.
        string label;
        if (member.IsSceneShutter)
        {
            label = ShutterDirectionPinName(member) is { Length: > 0 } dir
                ? $"{LinkOppositePath(member)} / {dir}"
                : LinkOppositePath(member);
        }
        else
        {
            (string value, string ramp) = SceneMemberValue(member);
            string text = ramp.Length > 0 ? $"{value} / {ramp}" : value;
            label = $"{LinkOppositePath(member)} = {text}";
        }
        return new TreeNodeViewModel(label, "/Assets/link-from.svg",
            elementId: member.Id) { Kind = TreeNodeKind.SceneMember };
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
        bool locked = View(fb).Locked;
        string icon = locked ? "/Assets/fb-lk.svg" : "/Assets/fb-editable.svg";
        var node = new TreeNodeViewModel(name, icon, elementId: fb.Id, isLockedFunctionBlock: locked)
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
            var section = new TreeNodeViewModel(label, NodeIcons.For(container, null), elementId: holder?.Id)
            {
                KindDetail = container,
                Kind = TreeNodeKind.Section,
            };
            if (holder is not null)
            {
                foreach (ProjectElement pin in holder.ChildrenOrEmpty())
                    section.Children.Add(BuildPinNode(pin, inFunctionBlockSettings: container == "settings"));
            }
            node.Children.Add(section);
        }
        return node;
    }

    // The hover tooltip (US-047/US-048): the documentation note plus, for a resource-mapped node, its IHC resource id.
    private string? BuildTooltip(ProjectElement element)
    {
        var parts = new List<string>();
        if (View(element).Note is { Length: > 0 } note)
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

    // A function block's Indstillinger rows carry a literal time value (A-21/F-062), scoped to the time-carrying kinds.
    private string? SettingsTimeLiteral(ProjectElement resource)
    {
        if (!resource.IsTimeSetting)
            return null;
        int Part(string attr) => int.TryParse(View(resource).Effective(attr), out int v) ? v : 0;
        return $"{Part("hour"):00}:{Part("minute"):00}:{Part("second"):00}";
    }

    private TreeNodeViewModel BuildPinNode(ProjectElement resource, bool inFunctionBlockSettings = false,
        bool catalogDeclared = false)
    {
        string name = NameOr(resource, resource.Tag);
        string? value = StateValue(resource)
                     ?? (inFunctionBlockSettings ? SettingsTimeLiteral(resource) : null)
                     ?? View(resource).Value;
        bool isOutput = resource.IsOutputPin;
        bool saved = isOutput && View(resource).Backup;
        // The label carries the pin's name and, for a state row, its value; the save flag surfaces via IsValueSaved (F-019).
        string label = string.IsNullOrEmpty(value) ? name : $"{name} = {value}";
        var node = new TreeNodeViewModel(label, NodeIcons.For(resource.Tag, View(resource).Icon),
            elementId: resource.Id)
            {
                IsOutputPin = isOutput, IsValueSaved = saved, Tooltip = BuildTooltip(resource),
                IsCatalogPin = catalogDeclared,
                IsLogMarkPin = ProjectEditor.IsLogRow(resource, project),
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
        return new TreeNodeViewModel(LinkOppositePath(linkRow), icon, elementId: linkRow.Id)
            { Kind = linkRow.IsSceneLink ? TreeNodeKind.SceneLink : isSourceEnd ? TreeNodeKind.LinkFrom : TreeNodeKind.LinkTo };
    }

    private string LinkOppositePath(ProjectElement linkRow) =>
        LinkOppositeParts(linkRow) is { Count: > 0 } parts ? string.Join(" / ", parts) : "(unresolved)";

    // The opposite end's path parts, outermost first: [locality, product-or-block, pin]. Empty when unresolvable.
    private IReadOnlyList<string> LinkOppositeParts(ProjectElement linkRow)
    {
        if (!ElementId.TryParse(View(linkRow).Effective("link"), out ElementId partnerId)
            || project.FindParent(partnerId) is not { } oppositePin)
        {
            return Array.Empty<string>();
        }
        var parts = new List<string>();
        ProjectElement? current = oppositePin;
        bool leaf = true;
        while (current is not null)
        {
            bool significant = leaf || current.IsLocalityGroup || current.Kind is ElementKind.FunctionBlock || ProductClassifier.IsProduct(current.Tag);
            if (significant && View(current).Name is { Length: > 0 } partName)
                parts.Insert(0, ProductClassifier.IsProduct(current.Tag)
                    ? ProductLabel(partName, View(current).Position)
                    : partName);
            current = current.Id is { } cid ? project.FindParent(cid) : null;
            leaf = false;
        }
        return parts;
    }
}
