using System.Linq;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

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
            isExpanded: true, elementId: block.Id) { NodeKind = "functionBlock" };
        ProjectElement? programs = block.FindChild("programs");
        var programsNode = new TreeNodeViewModel("Programs", NodeIcons.For("programs", null),
            isExpanded: true, elementId: programs?.Id) { NodeKind = "programs" };
        if (programs is not null)
        {
            foreach (ProjectElement program in programs.ChildrenOrEmpty().Where(p => p.Tag is "program_simple" or "program_sub"))
            {
                var programNode = new TreeNodeViewModel(NameOr(program, "Program"),
                    NodeIcons.For("program_simple", null), isExpanded: true, elementId: program.Id)
                    { NodeKind = "program" };
                if (program.FindChild("events") is { } events)
                {
                    var eventsNode = new TreeNodeViewModel("Events", NodeIcons.For("events", null),
                        isExpanded: true, elementId: events.Id) { IsEventsContainer = true, NodeKind = "events" };
                    foreach (ProjectElement ev in events.ChildrenOrEmpty().Where(e => e.Tag is "event" or "event_power"))
                        eventsNode.Children.Add(new TreeNodeViewModel(EventCommandLabel(ev),
                            NodeIcons.For(ev.Tag, null), elementId: ev.Id) { NodeKind = "event" });
                    programNode.Children.Add(eventsNode);
                }
                if (program.FindChild("actions") is { } actions)
                {
                    var commandsNode = new TreeNodeViewModel("Commands", NodeIcons.For("actions", null),
                        isExpanded: true, elementId: actions.Id) { IsCommandsContainer = true, NodeKind = "commands" };
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
            switch (child.Tag)
            {
                case "action":
                    commandsNode.Children.Add(new TreeNodeViewModel(EventCommandLabel(child),
                        NodeIcons.For("action", null), elementId: child.Id) { NodeKind = "command" });
                    break;
                case "program_sub":
                    commandsNode.Children.Add(BuildSubProgramNode(child));
                    break;
                case "program_case":
                    commandsNode.Children.Add(BuildCaseNode(child));
                    break;
            }
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
            isExpanded: true, elementId: sub.Id) { NodeKind = "subProgram" };
        if (sub.FindChild("conditions") is { } conditions)
            node.Children.Add(BuildConditionsNode(conditions));
        foreach (ProjectElement branch in sub.ChildrenOrEmpty().Where(a => a.Tag == "actions"))
        {
            bool isTrue = (branch.GetAttribute("type") ?? "") == "_0x1";
            var branchNode = new TreeNodeViewModel(
                isTrue ? "Commands when conditions true" : "Commands when conditions false",
                NodeIcons.For("actions", null), isExpanded: true, elementId: branch.Id)
                { IsCommandsContainer = true, NodeKind = isTrue ? "commandsWhenTrue" : "commandsWhenFalse" };
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
            { IsConditionsContainer = true, IsOrGroup = or, NodeKind = nested ? "logicGroup" : "conditions" };
        foreach (ProjectElement child in conditions.ChildrenOrEmpty())
        {
            if (child.Tag == "condition")
                node.Children.Add(new TreeNodeViewModel(EventCommandLabel(child),
                    NodeIcons.For("condition", null), elementId: child.Id) { NodeKind = "condition" });
            else if (child.Tag == "conditions")
                node.Children.Add(BuildConditionsNode(child, nested: true));
        }
        return node;
    }

    // Renders a case switch (US-031): "Case (<switch variable>)" over its value branches and the default Else branch.
    // Every branch is a command container, so commands can be added to it with the normal gesture.
    private TreeNodeViewModel BuildCaseNode(ProjectElement kase)
    {
        string switchName = ResolveOperandName(kase.GetAttribute("link"));
        var node = new TreeNodeViewModel($"Case ({switchName})", NodeIcons.For("program_case", null),
            isExpanded: true, elementId: kase.Id) { IsCaseNode = true, NodeKind = "case" };
        foreach (ProjectElement child in kase.ChildrenOrEmpty())
        {
            if (child.Tag == "case_action")
            {
                // "caseValue", not "commands": this row's LABEL is user data and it is ALSO an
                // IsCommandsContainer, so neither the label nor the flag can tell it from a real
                // Commands container — it needs a kind of its own or the two merge in the census.
                var valueNode = new TreeNodeViewModel(NameOr(child, "value"),
                    NodeIcons.For("case_action", null), isExpanded: true, elementId: child.Id)
                    { IsCommandsContainer = true, NodeKind = "caseValue" };
                RenderActionsInto(valueNode, child);   // the embedded criterion operand is skipped (not a command)
                node.Children.Add(valueNode);
            }
            else if (child.Tag == "actions")
            {
                var elseNode = new TreeNodeViewModel("Else", NodeIcons.For("actions", null),
                    isExpanded: true, elementId: child.Id) { IsCommandsContainer = true, NodeKind = "caseElse" };
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
}
