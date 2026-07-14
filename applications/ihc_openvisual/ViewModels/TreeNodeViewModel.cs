using System.Collections.ObjectModel;
using Ihc.Vis.Model;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// A node in one of the two tree panes. For epic E1 the trees are the locality skeleton (a <c>Localities</c>
/// root over the project's rooms); products, function blocks and pins are added by later epics, so children
/// are exposed generically here.
/// </summary>
public sealed class TreeNodeViewModel
{
    public TreeNodeViewModel(string displayName, string iconAsset, bool isExpanded = false, bool isBold = false,
        ElementId? elementId = null, bool isLocalitiesRoot = false, bool isUnlinked = false,
        bool isLockedFunctionBlock = false)
    {
        DisplayName = displayName;
        IconAsset = iconAsset;
        IsExpanded = isExpanded;
        IsBold = isBold;
        ElementId = elementId;
        IsLocalitiesRoot = isLocalitiesRoot;
        IsUnlinked = isUnlinked;
        IsLockedFunctionBlock = isLockedFunctionBlock;
    }

    /// <summary>Whether to show the yellow "!" unlinked marker — a wireless product not yet linked to the
    /// controller (US-014).</summary>
    public bool IsUnlinked { get; }

    /// <summary>Whether this is a locked library function block — the target of <i>Unlock</i> (US-020).</summary>
    public bool IsLockedFunctionBlock { get; }

    /// <summary>Whether this node is a function block — the target of <i>Save block…</i> (US-021).</summary>
    public bool IsFunctionBlock { get; init; }

    /// <summary>Whether this node is a resource pin — a drag source/target for linking (US-022).</summary>
    public bool IsPin { get; init; }

    /// <summary>Whether this node is a product's <c>scenes</c> container — the target of a scenario link (US-024).</summary>
    public bool IsSceneTarget { get; init; }

    /// <summary>Whether this node can be a link target — a pin or a scenes container (US-022/US-024).</summary>
    public bool IsLinkTarget => IsPin || IsSceneTarget;

    /// <summary>The container tag when this node is a function-block variable section (<c>inputs</c>/<c>outputs</c>/
    /// <c>settings</c>/<c>internalsettings</c>) — the target of <i>Insert variable</i> (US-027); null otherwise.</summary>
    public string? SectionTag { get; init; }

    /// <summary>Whether this node is a function-block variable section (US-027).</summary>
    public bool IsBlockSection => SectionTag is not null;

    /// <summary>Whether this node is a program's <c>events</c> container — the target of <i>Add event</i> (US-028).</summary>
    public bool IsEventsContainer { get; init; }

    /// <summary>Whether this node is a program's <c>actions</c> ("Commands") container — the target of
    /// <i>Add command</i> and <i>Sub-program</i> (US-028/US-029).</summary>
    public bool IsCommandsContainer { get; init; }

    /// <summary>Whether this node is a <c>conditions</c> group — the target of <i>Add condition</i>,
    /// <i>Logic group</i> and the AND/OR toggle (US-029).</summary>
    public bool IsConditionsContainer { get; init; }

    /// <summary>Whether this conditions group is OR-combined (<c>&gt;=1</c>) rather than the default AND (US-029).</summary>
    public bool IsOrGroup { get; init; }

    /// <summary>Whether this node is a <c>program_case</c> switch — the target of <i>New case value…</i> (US-031).</summary>
    public bool IsCaseNode { get; init; }

    /// <summary>Whether this node is an output pin (a function-block or physical output) — the target of the
    /// <i>Save current value</i> power-loss persistence toggle (US-033).</summary>
    public bool IsOutputPin { get; init; }

    /// <summary>Whether this output's value is persisted across a power loss (<c>backup="yes"</c>, US-033) — the
    /// checked state of <i>Save current value</i>.</summary>
    public bool IsValueSaved { get; init; }

    /// <summary>Whether this node is a link row (a "link from"/"link to"/scene-link child) — the F4/Delete target
    /// for link navigation and removal (US-025/US-057).</summary>
    public bool IsLinkRow { get; init; }

    /// <summary>Whether this is the synthetic <c>Localities</c> root — the target of <i>Insert locality</i> (US-008).</summary>
    public bool IsLocalitiesRoot { get; }

    /// <summary>Context-menu gate: <i>Insert locality</i> is offered on the Localities root.</summary>
    public bool CanInsertLocality => IsLocalitiesRoot;

    /// <summary>Context-menu gate: <i>Properties</i> is offered on nodes that address a real element (a locality).</summary>
    public bool CanEditProperties => ElementId is not null;

    /// <summary>Context-menu gate: <i>Delete</i> is offered on nodes that address a real element (a locality).</summary>
    public bool CanDelete => ElementId is not null;

    /// <summary>Hover tooltip (US-047/US-048): the node's documentation note and, for a resource-mapped node (input,
    /// output, function block), its IHC resource id — each on its own line(s). Null when the node has neither, so no
    /// tooltip is shown (e.g. the Localities root or an empty locality).</summary>
    public string? Tooltip { get; init; }

    public string DisplayName { get; }

    /// <summary>The stable id of the project element this node stands for (a locality's <c>group</c> id, later a
    /// product/FB id); null for the synthetic <c>Localities</c> root, which addresses no element.</summary>
    public ElementId? ElementId { get; }

    /// <summary>The <c>/Assets/*.svg</c> glyph rendered beside the label (per the icon-mapping doc).</summary>
    public string IconAsset { get; }

    /// <summary>Whether the node is expanded by default (the <c>Localities</c> root is; rooms are collapsed).</summary>
    public bool IsExpanded { get; }

    /// <summary>Whether the label renders bold — locality nodes do (US-006).</summary>
    public bool IsBold { get; }

    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();
}
