using System;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-9: the shared tree drag-and-drop dispatcher (A-30…A-34), extracted from
/// <see cref="MainWindowViewModel"/>. It decides the drop legality + route (<see cref="CanDropOn"/>), performs the
/// drop (<see cref="PerformDropAsync"/>) and drives the drop-target highlight (<see cref="HighlightDropTarget"/>).
/// Avalonia-free so it stays headlessly testable; the code-behind's DragOver/Drop handlers read the dragged id from
/// the DataTransfer and call these.
/// <para><b>Single verdict per drop (W3-9):</b> <see cref="CanDropOn"/> resolves the legality ONCE and records the
/// concrete <see cref="DropRoute"/> in the verdict, so <see cref="PerformDropAsync"/> performs it by route without
/// re-asking the SDK (the old dispatcher re-evaluated up to 3×). Legality is asked through
/// <see cref="ProjectWorkflow.CanApply"/> (the command's own Evaluate) and
/// <see cref="ProjectWorkflow.CanReorderNode"/> — both document-backed probes against the per-commit index
/// (crudarch T008: no per-pointer-event session or index rebuild) — do NOT re-encode vendor grammar here; the
/// per-route grammar (container-admissibility, link legality) belongs to the SDK op the drop calls.</para>
/// </summary>
public sealed class TreeDragDropController(
    ProjectWorkflow session,
    Func<ElementId, TreeNodeViewModel?> findNode,
    Func<bool> isProgrammingBlockLocked,
    Func<Ihc.OperationScope, ProjectCommand, string, Task> applyAndReport,
    Action<TreeNodeViewModel, TreeNodeViewModel> useVariableInProgram,
    Action<string> setStatus,
    Func<string, Func<Ihc.OperationScope, Task>, Task<Ihc.OperationOutcome>> runAsync)
{
    private TreeNodeViewModel? _dropTargetNode;

    /// <summary>The status a completed reparent reports. Declared here rather than at each call site because the
    /// drag reparent and Edit ▸ Paste-after-Cut are the SAME outcome to the installer — one node moved — and two
    /// copies of the sentence would let the two routes drift into describing it differently.</summary>
    public const string MovedStatus = "Flyttet.";

    /// <summary>The last resort when an SDK verdict refuses without saying why. Never expected — every
    /// <c>EditVerdict.Refuse</c> carries a reason — but a silent refused drop looks like a broken app, so the
    /// fallback states that something refused rather than nothing at all.</summary>
    private const string UnexplainedRefusal = "Handlingen blev afvist.";

    /// <summary>Whether — and how — the dragged node may drop onto the target: a <see cref="DropVerdict"/> of ok +
    /// effect + the resolved <see cref="DropRoute"/>, or a reason when refused. Only the legality every route shares
    /// is decided here (a node cannot drop onto itself); the per-route grammar is asked of the SDK.</summary>
    public DropVerdict CanDropOn(ElementId dragged, ElementId target)
    {
        if (dragged == target)
            return DropVerdict.Refused("En node kan ikke slippes på sig selv.");
        TreeNodeViewModel? draggedNode = findNode(dragged);
        TreeNodeViewModel? targetNode = findNode(target);
        if (draggedNode is null || targetNode is null)
            return DropVerdict.None;
        // Link: dropping one pin onto another creates a link when the SDK's data-flow rule allows it (US-022/US-023).
        // Ask the LinkPins command's own Evaluate; this precedes reorder so two same-tag pins link (never reorder).
        if (draggedNode.IsPin && targetNode.IsPin)
        {
            // The SDK verdict carries its own Danish sentence, so it is FORWARDED rather than restated here. The
            // copy that used to live at this line said the same thing in the same words — maintained separately,
            // free to drift from the rule it described.
            EditVerdict link = session.CanApply(session.Commands.LinkPins(session.Current!, dragged, target));
            return link.Ok ? DropVerdict.PinLink() : DropVerdict.Refused(link.Reason ?? UnexplainedRefusal);
        }
        // Program build: dropping a variable/pin onto an events, commands OR conditions container arms the method
        // popup for that family (US-028 "one drag gesture, three families" — the target group picks Event/Command/
        // Condition). Gated on the A-27 locked-block rule — no authoring drop into a locked library block.
        if (draggedNode.IsPin && (targetNode.IsEventsContainer || targetNode.IsCommandsContainer || targetNode.IsConditionsContainer))
        {
            return isProgrammingBlockLocked()
                ? DropVerdict.Refused("Denne blok er låst — lås den op for at redigere dens program.")
                : DropVerdict.ProgramBuild();
        }
        // Reorder: dropping onto a same-parent, same-tag sibling moves the node to that position (US-055). The SDK owns
        // the "same-tag sibling" rule; ask the document's index-backed probe (crudarch T008 — no full-tree walk per
        // drag-over pointer event).
        if (session.CanReorderNode(dragged, target))
            return DropVerdict.Reorder();
        // A product can be dragged to re-parent it into another locality (US-054). Ask the MoveNode command's Evaluate
        // whether this exact target is a legal destination (the same legality Cut/Paste uses).
        if (draggedNode.Kind == TreeNodeKind.Product)
        {
            EditVerdict move = session.CanApply(session.Commands.MoveNode(session.Current!, dragged, target));
            return move.Ok ? DropVerdict.Reparent() : DropVerdict.Refused(move.Reason ?? UnexplainedRefusal);
        }
        return DropVerdict.None;
    }

    /// <summary>Performs a drop, routing purely on the <see cref="DropVerdict.Route"/> that <see cref="CanDropOn"/>
    /// already resolved (no re-evaluation): a pin link, a program-build arm, a reorder, or a re-parent. A refused
    /// drop surfaces its reason and mutates nothing.</summary>
    public Task PerformDropAsync(ElementId dragged, ElementId target) =>
        PerformDropAsync(dragged, target, CanDropOn(dragged, target));

    /// <summary>Performs a drop using a verdict the view already obtained while routing the gesture. This overload
    /// lets the UI present route-specific feedback without asking the SDK's legality probes a second time.</summary>
    public Task<Ihc.OperationOutcome> PerformDropAsync(ElementId dragged, ElementId target, DropVerdict verdict) => runAsync(nameof(PerformDropAsync), async scope =>
    {
        if (!verdict.Ok)
        {
            if (verdict.Reason is { } reason)
                setStatus(reason);
            return;
        }
        switch (verdict.Route)
        {
            case DropRoute.ProgramBuild:
                // A variable dropped onto an events/commands container arms the same method popup as Use-in-program.
                if (findNode(dragged) is { } variable && findNode(target) is { } container)
                    useVariableInProgram(variable, container);
                break;
            case DropRoute.PinLink:
                await applyAndReport(scope, session.Commands.LinkPins(session.Current!, dragged, target), "Linket.");
                break;
            case DropRoute.Reorder:
                if (session.Commands.ReorderNodeToSibling(session.Current!, dragged, target) is { } command)
                    await applyAndReport(scope, command, "Omarrangeret.");
                break;
            case DropRoute.Reparent:
                await applyAndReport(scope, session.Commands.MoveNode(session.Current!, dragged, target), MovedStatus);
                break;
        }
        // The row that was dropped ONTO opens, with everything under it, and stays open — measured against IHC
        // Visual (uxparity S-11): after a drag, the target row's whole subtree is expanded, and a second drag onto
        // a different row leaves the first one open too. It shows what the drop landed next to. Only the drag does
        // this; the keyboard supplements (edit.moveUp/moveDown) reorder without touching expansion, there as here.
        if (findNode(target) is { } dropped)
            dropped.ExpandSubtree();
    });

    /// <summary>Highlights (or clears) the current legal drop target so the tree shows where a drop will land (A-30):
    /// sets <see cref="TreeNodeViewModel.IsDropTarget"/> on the addressed node and clears any previous one; pass
    /// <c>null</c> to clear.</summary>
    public void HighlightDropTarget(ElementId? target)
    {
        TreeNodeViewModel? node = target is { } id ? findNode(id) : null;
        if (ReferenceEquals(node, _dropTargetNode))
            return;
        if (_dropTargetNode is not null)
            _dropTargetNode.IsDropTarget = false;
        _dropTargetNode = node;
        if (_dropTargetNode is not null)
            _dropTargetNode.IsDropTarget = true;
    }
}
