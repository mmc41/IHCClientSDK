using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Ihc.Vis.Model;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// A node in one of the two tree panes. For epic E1 the trees are the locality skeleton (a <c>Localities</c>
/// root over the project's rooms); products, function blocks and pins are added by later epics, so children
/// are exposed generically here.
/// </summary>
public sealed class TreeNodeViewModel : ObservableObject
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

    /// <summary>Whether this pin is declared by the product's catalog type — a product's pins exist because the
    /// catalog type declares them, so they are not the installer's to remove (A-24/F-067, US-068).</summary>
    public bool IsCatalogPin { get; init; }

    /// <summary>Whether this is a "Log …" row (a Logning <c>resource_enum</c>) that offers the vendor's log-mark
    /// toggle (A-22/&amp;Logmærke, US-068).</summary>
    public bool IsLogMarkPin { get; init; }

    /// <summary>Context-menu gate: <i>Delete</i> is offered on nodes that address a real element — except a
    /// catalog-declared pin, which the type owns (A-24). This is the GUI gate; the SDK engine guard is deferred.</summary>
    public bool CanDelete => ElementId is not null && !IsCatalogPin;

    /// <summary>Context-menu gate: <i>Cut</i>/<i>Copy</i> are offered on the structural components — a locality, a
    /// product or a function block (A-5b/F-009). Not on the Localities root, link rows, pins, sections or the
    /// program-tree nodes.</summary>
    public bool CanCutCopy => NodeKind is "locality" or "product" or "functionBlock";

    /// <summary>Context-menu gate: <i>Move up</i>/<i>Move down</i> and <i>Properties</i> are offered on any addressable
    /// node EXCEPT a link row — the link row's only items are <i>Jump to opposite</i> and <i>Delete</i> (A-5b).</summary>
    public bool CanEditNonLink => ElementId is not null && !IsLinkRow;

    /// <summary>Hover tooltip (US-047/US-048): the node's documentation note and, for a resource-mapped node (input,
    /// output, function block), its IHC resource id — each on its own line(s). Null when the node has neither, so no
    /// tooltip is shown (e.g. the Localities root or an empty locality).</summary>
    public string? Tooltip { get; init; }

    /// <summary>
    /// What KIND of thing this row is, independent of what it is called. Surfaced to automation as the row's
    /// <c>AutomationProperties.AutomationId</c>; never rendered, never announced as content.
    /// <para>It exists because in programming mode a label cannot identify a node — the labels ARE user data.
    /// "Kip Udgang" is a command, "Kip ved kort tryk -&gt; ON" an event, "Input Timer &gt;= 00:00:01,000" a
    /// condition; all three are just what someone named their wiring. The comparison census has to partition
    /// rows by type, and no existing property can do it: the container flags below cover only containers, and
    /// the two obvious shortcuts are both traps — the ICON is not a 1:1 kind map (<c>NodeIcons</c> maps
    /// <c>program_sub</c> and <c>program_case</c> to the same glyph), and PARENT-LABEL inference breaks on a
    /// case branch, whose label is user data and which is itself an <see cref="IsCommandsContainer"/>.</para>
    /// <para>Defaults to <see cref="UnknownKind"/> rather than null or empty: an absent value must read as
    /// "nobody classified this row", never as a kind in its own right.</para>
    /// </summary>
    public string NodeKind { get; init; } = UnknownKind;

    /// <summary>The <see cref="NodeKind"/> of a row no construction site has classified.</summary>
    public const string UnknownKind = "unknown";

    public string DisplayName { get; }

    /// <summary>The name a screen reader announces for this row. It folds the visible label together with the
    /// unlinked state (which is otherwise conveyed only by the "!" glyph and tooltip), so assistive technology
    /// hears the status too (accessibility — Avalonia <c>AutomationProperties.Name</c>).</summary>
    public string AccessibleName => IsUnlinked ? $"{DisplayName}, not linked to the controller" : DisplayName;

    /// <summary>The stable id of the project element this node stands for (a locality's <c>group</c> id, later a
    /// product/FB id); null for the synthetic <c>Localities</c> root, which addresses no element.</summary>
    public ElementId? ElementId { get; }

    /// <summary>The <c>/Assets/*.svg</c> glyph rendered beside the label (per the icon-mapping doc).</summary>
    public string IconAsset { get; }

    /// <summary>Whether the node is expanded by default (the <c>Localities</c> root is; rooms are collapsed).</summary>
    private bool _isExpanded;

    /// <summary>Whether the node is expanded. Settable and observable so a jump (F4/A-6) can expand the opposite
    /// pin's ancestor chain to bring it into view; the tree binds this one-way to the container's IsExpanded.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Whether the label renders bold — locality nodes do (US-006).</summary>
    public bool IsBold { get; }

    private bool _isDropTarget;

    /// <summary>Whether this row is the current drag-over drop target — the item template paints its background so the
    /// user sees where a drop will land (A-30). Observable so the highlight follows the pointer as a drag moves across
    /// rows; set by <see cref="MainWindowViewModel.HighlightDropTarget"/>, never bound to persisted state.</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }

    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();
}
