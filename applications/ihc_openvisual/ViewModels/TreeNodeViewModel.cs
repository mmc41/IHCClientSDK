using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Ihc.Vis.Model;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// A node in one of the two tree panes. Its structural type is carried by a single <see cref="Kind"/>
/// (fablerefac W3-7): the kind flags and the automation <see cref="NodeKind"/> string are COMPUTED from it, so the
/// projector classifies a row once instead of setting a string plus a dozen booleans. State/provenance that is not a
/// kind (linked/locked/output/backup/catalog/log-mark) stays stored.
/// </summary>
public sealed partial class TreeNodeViewModel : ObservableObject
{
    public TreeNodeViewModel(string displayName, string iconAsset, bool isExpanded = false, bool isBold = false,
        ElementId? elementId = null, bool isUnlinked = false, bool isLockedFunctionBlock = false)
    {
        DisplayName = displayName;
        IconAsset = iconAsset;
        IsExpanded = isExpanded;
        IsBold = isBold;
        ElementId = elementId;
        IsUnlinked = isUnlinked;
        IsLockedFunctionBlock = isLockedFunctionBlock;
    }

    /// <summary>The row's structural classification — the single source the projector sets; the kind flags and the
    /// <see cref="NodeKind"/> automation string derive from it (W3-7).</summary>
    public TreeNodeKind Kind { get; init; } = TreeNodeKind.Unknown;

    /// <summary>Whether this row should OPEN when it gains its first child, so the new node is visible (US-006).
    /// Distinct from <see cref="IsExpanded"/>, which is the row's state right now: a locality opens on its first
    /// product but still starts closed when a project is opened, and a product does not open on its first pin.</summary>
    public bool RevealsOnFirstChild { get; init; }

    /// <summary>The parameterised suffix of a <see cref="TreeNodeKind.Pin"/> (its resource tag) or
    /// <see cref="TreeNodeKind.Section"/> (its container tag), so the <see cref="NodeKind"/> string can reproduce the
    /// vendor-parameterised forms <c>pin:&lt;tag&gt;</c> / <c>section:&lt;container&gt;</c>; null for fixed kinds.</summary>
    public string? KindDetail { get; init; }

    /// <summary>Whether to show the yellow "!" unlinked marker — a wireless product not yet linked to the
    /// controller (US-014). Re-rendered in place by the W3-4 reconciler when a product links/unlinks.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial bool IsUnlinked { get; set; }

    /// <summary>Whether this is a locked library function block — the target of <i>Unlock</i> (US-020).
    /// Re-rendered in place by the W3-4 reconciler on unlock.</summary>
    [ObservableProperty]
    public partial bool IsLockedFunctionBlock { get; set; }

    /// <summary>Whether this node is a function block — the target of <i>Save block…</i> (US-021).</summary>
    public bool IsFunctionBlock => Kind is TreeNodeKind.FunctionBlock;

    /// <summary>Whether this node is a locality — the only node that hosts <i>Insert product</i>/<i>Insert function
    /// block</i> (US-068, by pane).</summary>
    public bool IsLocality => Kind is TreeNodeKind.Locality;

    /// <summary>Whether this node is a resource pin — a drag source/target for linking (US-022).</summary>
    public bool IsPin => Kind is TreeNodeKind.Pin;

    /// <summary>Whether this node is a product's <c>scenes</c> container — the target of a scenario link (US-024).</summary>
    public bool IsSceneTarget => Kind is TreeNodeKind.Scenes;

    /// <summary>Whether this node can be a link target — a pin or a scenes container (US-022/US-024).</summary>
    public bool IsLinkTarget => IsPin || IsSceneTarget;

    /// <summary>The container tag when this node is a function-block variable section (<c>inputs</c>/<c>outputs</c>/
    /// <c>settings</c>/<c>internalsettings</c>) — the target of <i>Insert variable</i> (US-027); null otherwise.</summary>
    public string? SectionTag => Kind is TreeNodeKind.Section ? KindDetail : null;

    /// <summary>Whether this node is a function-block variable section with a backing container to insert into
    /// (US-027). A section whose container element is absent has no <see cref="ElementId"/>, so it is not insertable.</summary>
    public bool IsBlockSection => Kind is TreeNodeKind.Section && ElementId is not null;

    /// <summary>Whether this node is a program's <c>events</c> container — the target of <i>Add event</i> (US-028).</summary>
    public bool IsEventsContainer => Kind is TreeNodeKind.Events;

    /// <summary>Whether this node is a program's <c>actions</c> ("Commands") container — the target of
    /// <i>Add command</i> and <i>Sub-program</i> (US-028/US-029).</summary>
    public bool IsCommandsContainer =>
        Kind is TreeNodeKind.Commands or TreeNodeKind.CommandsWhenTrue or TreeNodeKind.CommandsWhenFalse
            or TreeNodeKind.CaseValue or TreeNodeKind.CaseElse;

    /// <summary>Whether this node is a <c>conditions</c> group — the target of <i>Add condition</i>,
    /// <i>Logic group</i> and the AND/OR toggle (US-029).</summary>
    public bool IsConditionsContainer => Kind is TreeNodeKind.Conditions or TreeNodeKind.LogicGroup;

    /// <summary>Whether this conditions group is OR-combined (<c>&gt;=1</c>) rather than the default AND (US-029).
    /// Intentional test-only seam (D02): the projector SETS it (the AND/OR shows in the icon + label suffix); it is
    /// currently READ only by the projection tests, kept so a future label/icon binding can consume it without churn.</summary>
    public bool IsOrGroup { get; init; }

    /// <summary>Whether this node is a <c>program_case</c> switch — the target of <i>New case value…</i> (US-031).</summary>
    public bool IsCaseNode => Kind is TreeNodeKind.Case;

    /// <summary>Whether this node is an output pin (a function-block or physical output) — the target of the
    /// <i>Save current value</i> power-loss persistence toggle (US-033). A pin sub-type carried by the resource tag,
    /// not a kind of its own.</summary>
    public bool IsOutputPin { get; init; }

    /// <summary>Whether this output's value is persisted across a power loss (<c>backup="yes"</c>, US-033) — the
    /// checked state of <i>Save current value</i>.</summary>
    public bool IsValueSaved { get; init; }

    /// <summary>Whether this node is a link row (a "link from"/"link to"/scene-link child or a scene member) — the
    /// F4/Delete target for link navigation and removal (US-025/US-057).</summary>
    public bool IsLinkRow =>
        Kind is TreeNodeKind.LinkFrom or TreeNodeKind.LinkTo or TreeNodeKind.SceneLink or TreeNodeKind.SceneMember;

    /// <summary>Whether this is the synthetic <c>Localities</c> root — the target of <i>Insert locality</i> (US-008).</summary>
    public bool IsLocalitiesRoot => Kind is TreeNodeKind.LocalitiesRoot;

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

    /// <summary>Context-menu gate: <i>Cut</i> is offered on the structural components — a locality, a product or a
    /// function block (A-5b/F-009). Not on the Localities root, link rows, pins, sections, a scene container or the
    /// program-tree nodes.</summary>
    public bool CanCut => Kind is TreeNodeKind.Locality or TreeNodeKind.Product or TreeNodeKind.FunctionBlock;

    /// <summary>Context-menu gate: <i>Copy</i> is offered on every <see cref="CanCut">cuttable</see> node PLUS a
    /// scene container (US-068: a scene container's menu is <i>Copy</i>/Properties, no Cut).</summary>
    public bool CanCopy => CanCut || Kind is TreeNodeKind.Scenes;

    /// <summary>Context-menu gate: <i>Move up</i>/<i>Move down</i> stay on the reorderable structural nodes — the same
    /// locality/product/function-block set as <see cref="CanCut"/> (US-068, D07). Absent from link rows, pins,
    /// sections, scene containers and program-tree nodes.</summary>
    public bool CanReorder => CanCut;

    /// <summary>Context-menu gate: <i>Move up</i>/<i>Move down</i> and <i>Properties</i> are offered on any addressable
    /// node EXCEPT a link row — the link row's only items are <i>Jump to opposite</i> and <i>Delete</i> (A-5b).</summary>
    public bool CanEditNonLink => ElementId is not null && !IsLinkRow;

    /// <summary>Hover tooltip (US-047/US-048): the node's documentation note and, for a resource-mapped node (input,
    /// output, function block), its IHC resource id — each on its own line(s). Null when the node has neither, so no
    /// tooltip is shown (e.g. the Localities root or an empty locality).</summary>
    [ObservableProperty]
    public partial string? Tooltip { get; set; }

    /// <summary>
    /// What KIND of thing this row is, independent of what it is called — computed from <see cref="Kind"/>. Surfaced
    /// to automation as the row's <c>AutomationProperties.AutomationId</c>; never rendered, never announced as content.
    /// <para>It exists because in programming mode a label cannot identify a node — the labels ARE user data.
    /// "Kip Udgang" is a command, "Kip ved kort tryk -&gt; ON" an event, "Input Timer &gt;= 00:00:01,000" a
    /// condition; all three are just what someone named their wiring. The comparison census has to partition
    /// rows by type, and neither the icon (<c>program_sub</c>/<c>program_case</c> share a glyph) nor the parent
    /// label (a case branch's label is user data) can do it.</para>
    /// <para><see cref="UnknownKind"/> reads as "nobody classified this row", never as a kind in its own right.</para>
    /// </summary>
    public string NodeKind => Kind switch
    {
        TreeNodeKind.LocalitiesRoot => "localitiesRoot",
        TreeNodeKind.Locality => "locality",
        TreeNodeKind.Product => "product",
        TreeNodeKind.Scenes => "scenes",
        TreeNodeKind.SceneMember => "sceneMember",
        TreeNodeKind.FunctionBlock => "functionBlock",
        TreeNodeKind.ProgramBlockRoot => "functionBlock",
        TreeNodeKind.Section => $"section:{KindDetail}",
        TreeNodeKind.Pin => $"pin:{KindDetail}",
        TreeNodeKind.Programs => "programs",
        TreeNodeKind.Program => "program",
        TreeNodeKind.Events => "events",
        TreeNodeKind.Event => "event",
        TreeNodeKind.Commands => "commands",
        TreeNodeKind.Command => "command",
        TreeNodeKind.CommandsWhenTrue => "commandsWhenTrue",
        TreeNodeKind.CommandsWhenFalse => "commandsWhenFalse",
        TreeNodeKind.SubProgram => "subProgram",
        TreeNodeKind.Conditions => "conditions",
        TreeNodeKind.LogicGroup => "logicGroup",
        TreeNodeKind.Condition => "condition",
        TreeNodeKind.Case => "case",
        TreeNodeKind.CaseValue => "caseValue",
        TreeNodeKind.CaseElse => "caseElse",
        TreeNodeKind.LinkFrom => "linkFrom",
        TreeNodeKind.LinkTo => "linkTo",
        TreeNodeKind.SceneLink => "sceneLink",
        TreeNodeKind.Unknown => UnknownKind,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unmapped TreeNodeKind"),
    };

    /// <summary>The <see cref="NodeKind"/> of a row no construction site has classified.</summary>
    public const string UnknownKind = "unknown";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial string DisplayName { get; set; }

    /// <summary>The name a screen reader announces for this row. It folds the visible label together with the
    /// unlinked state (which is otherwise conveyed only by the "!" glyph and tooltip), so assistive technology
    /// hears the status too (accessibility — Avalonia <c>AutomationProperties.Name</c>).</summary>
    public string AccessibleName => IsUnlinked ? $"{DisplayName}, not linked to the controller" : DisplayName;

    /// <summary>The stable id of the project element this node stands for (a locality's <c>group</c> id, later a
    /// product/FB id); null for the synthetic <c>Localities</c> root, which addresses no element.</summary>
    public ElementId? ElementId { get; }

    /// <summary>The OTHER elements whose live name/value the projector rendered into this row's label — a link/scene
    /// row's opposite-end ancestors, a program row's %P/%S operands, a case's switch operand. Emitted by the projector
    /// (T022) so the reconciler reads the cross-reference dependency edges from the projection itself instead of
    /// re-deriving them and risking drift from what was actually rendered. Empty for a row whose label is composed
    /// only of its own attributes.</summary>
    public IReadOnlyList<ElementId> CrossReferences { get; set; } = Array.Empty<ElementId>();

    /// <summary>The <c>/Assets/*.svg</c> glyph rendered beside the label (per the icon-mapping doc).
    /// Re-rendered in place by the W3-4 reconciler (e.g. a locked FB becomes editable).</summary>
    [ObservableProperty]
    public partial string IconAsset { get; set; }

    /// <summary>Whether the node is expanded (the <c>Localities</c> root is by default; rooms are collapsed).
    /// Settable and observable so a jump (F4/A-6) can expand the opposite pin's ancestor chain to bring it into view;
    /// the tree binds this one-way to the container's IsExpanded.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Whether the label renders bold — locality nodes do (US-006).</summary>
    [ObservableProperty]
    public partial bool IsBold { get; set; }

    /// <summary>Whether this row is the current drag-over drop target — the item template paints its background so the
    /// user sees where a drop will land (A-30). Observable so the highlight follows the pointer as a drag moves across
    /// rows; set by <see cref="TreeDragDropController.HighlightDropTarget"/>, never bound to persisted state.</summary>
    [ObservableProperty]
    public partial bool IsDropTarget { get; set; }

    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();
}
