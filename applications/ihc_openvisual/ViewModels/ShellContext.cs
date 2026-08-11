using Ihc.Vis.Model;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// The explicit availability context (crudarch T010, proposal §3.2): ONE immutable snapshot of everything
/// command availability depends on, rebuilt only by the view-model's single <c>RebuildContext()</c> (whose
/// triggers are selection, pane-active, mode, clipboard and every document transition) and announced by ONE
/// <c>ContextChanged</c> event. Holds ids and VALUE flags only — never a <c>Project</c>/<c>ProjectElement</c>
/// or a live <see cref="TreeNodeViewModel"/> (review F4: a live reference would let a context in hand drift
/// while the tree mutates). The command registry (T011) evaluates every row against this one record.
/// </summary>
/// <remarks>Every member is one a gate, <c>SurfacePolicy</c> or handler actually reads — the record is the
/// checkable statement of what availability depends on, so a value nothing consults does not belong here (it
/// would be hauled on every rebuild while quietly weakening that claim). Dirty state and the undo/redo LABELS
/// are read straight off the workflow by the title and the Edit-menu headers, never through a gate, so they
/// are not snapshot; <c>CanUndo</c>/<c>CanRedo</c> stay because the undo/redo rows gate on them (review F10).</remarks>
[CommandContextValue]
public sealed record ShellContext(
    bool ProjectOpen,
    bool IsProgrammingMode, bool ProgrammingBlockLocked,
    bool InstallationPaneActive,
    NodeContext? Node,
    ClipboardContext? Clipboard,
    bool CanUndo, bool CanRedo,
    // Whether a controller is reachable. An availability trigger like any other (alignment F-4): the two
    // transfer commands gate on it, so it is snapshot here rather than read live off the view-model, and it
    // changes only through the one RebuildContext.
    bool ControllerConnected = false)
{
    /// <summary>The closed-shell context (no project, nothing selected) — the pre-initialization value.</summary>
    public static ShellContext Empty { get; } = new(
        ProjectOpen: false,
        IsProgrammingMode: false, ProgrammingBlockLocked: false,
        InstallationPaneActive: false,
        Node: null, Clipboard: null,
        CanUndo: false, CanRedo: false,
        ControllerConnected: false);
}

/// <summary>A VALUE snapshot of the active tree row, projected from <see cref="TreeNodeViewModel"/> at rebuild
/// time — the intrinsic flags the registry's gates read, never the live node. <see cref="Id"/> is null for the
/// id-less virtual rows (e.g. the Localities root), whose <see cref="Kind"/> still drives the insert gates.</summary>
/// <remarks>Same rule as <see cref="ShellContext"/>: only flags a gate reads. Which pane is active is the
/// shell's <see cref="ShellContext.InstallationPaneActive"/>, not a per-node copy of it, and "can edit
/// properties" is <see cref="Id"/> being non-null — which the properties gate reads directly (review F10).</remarks>
[CommandContextValue]
public sealed record NodeContext(
    ElementId? Id, TreeNodeKind Kind,
    bool IsPin, bool IsProductTerminal, bool IsLinkRow, bool IsLinkTarget, bool IsLogMarkPin,
    bool IsOutputPin, bool IsEventsContainer, bool IsCommandsContainer, bool IsConditionsContainer, bool IsCaseNode,
    bool IsLockedBlock,
    bool CanCut, bool CanCopy, bool CanReorder);

/// <summary>The structural-editing clipboard as a value (US-054/US-056): enough to MINT the paste —
/// <see cref="SourceId"/> + <see cref="IsCut"/> decide MoveNode vs CopyNode, then CanApply probes the target
/// (review F4: <c>ClipboardHasNode</c> alone could not).</summary>
[CommandContextValue]
public sealed record ClipboardContext(ElementId SourceId, bool IsCut);
