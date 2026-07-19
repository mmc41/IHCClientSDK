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
/// <see cref="ProjectWorkflow.CanApply"/> (the command's own Evaluate) — do NOT re-encode vendor grammar here; the
/// per-route grammar (container-admissibility, link legality) belongs to the SDK op the drop calls.</para>
/// </summary>
public sealed class TreeDragDropController(
    ProjectWorkflow session,
    Func<ElementId, TreeNodeViewModel?> findNode,
    Func<bool> isProgrammingBlockLocked,
    Func<ProjectCommand, string, Task> applyAndReport,
    Action<TreeNodeViewModel, TreeNodeViewModel> useVariableInProgram,
    Action<string> setStatus,
    Func<string, Func<Task>, Task> runAsync)
{
    private TreeNodeViewModel? _dropTargetNode;

    /// <summary>Whether — and how — the dragged node may drop onto the target: a <see cref="DropVerdict"/> of ok +
    /// effect + the resolved <see cref="DropRoute"/>, or a reason when refused. Only the legality every route shares
    /// is decided here (a node cannot drop onto itself); the per-route grammar is asked of the SDK.</summary>
    public DropVerdict CanDropOn(ElementId dragged, ElementId target)
    {
        if (dragged == target)
            return DropVerdict.Refused("Cannot drop a node onto itself.");
        TreeNodeViewModel? draggedNode = findNode(dragged);
        TreeNodeViewModel? targetNode = findNode(target);
        if (draggedNode is null || targetNode is null)
            return DropVerdict.None;
        // Link: dropping one pin onto another creates a link when the SDK's data-flow rule allows it (US-022/US-023).
        // Ask the LinkPins command's own Evaluate; this precedes reorder so two same-tag pins link (never reorder).
        if (draggedNode.IsPin && targetNode.IsPin)
        {
            return session.CanApply(new LinkPins(dragged, target)).Ok
                ? DropVerdict.PinLink()
                : DropVerdict.Refused("Those two pins can't be linked in that direction.");
        }
        // Program build: dropping a variable/pin onto an events or commands container arms the method popup (US-028).
        // Gated on the A-27 locked-block rule — no authoring drop into a locked library block.
        if (draggedNode.IsPin && (targetNode.IsEventsContainer || targetNode.IsCommandsContainer))
        {
            return isProgrammingBlockLocked()
                ? DropVerdict.Refused("This block is locked — unlock it to edit its program.")
                : DropVerdict.ProgramBuild();
        }
        // Reorder: dropping onto a same-parent, same-tag sibling moves the node to that position (US-055). The SDK owns
        // the "same-tag sibling" rule; ask it.
        if (session.CanReorderNode(dragged, target))
            return DropVerdict.Reorder();
        // A product can be dragged to re-parent it into another locality (US-054). Ask the MoveNode command's Evaluate
        // whether this exact target is a legal destination (the same legality Cut/Paste uses).
        if (draggedNode.Kind == TreeNodeKind.Product)
        {
            return session.CanApply(new MoveNode(dragged, target)).Ok
                ? DropVerdict.Reparent()
                : DropVerdict.Refused("That location can't hold this item.");
        }
        return DropVerdict.None;
    }

    /// <summary>Performs a drop, routing purely on the <see cref="DropVerdict.Route"/> that <see cref="CanDropOn"/>
    /// already resolved (no re-evaluation): a pin link, a program-build arm, a reorder, or a re-parent. A refused
    /// drop surfaces its reason and mutates nothing.</summary>
    public Task PerformDropAsync(ElementId dragged, ElementId target) => runAsync(nameof(PerformDropAsync), async () =>
    {
        DropVerdict verdict = CanDropOn(dragged, target);   // the single evaluation
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
                await applyAndReport(new LinkPins(dragged, target), "Linked.");
                break;
            case DropRoute.Reorder:
                if (session.BuildReorderNodeToSibling(dragged, target) is { } command)
                    await applyAndReport(command, "Reordered.");
                break;
            case DropRoute.Reparent:
                await applyAndReport(new MoveNode(dragged, target), "Moved.");
                break;
        }
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
