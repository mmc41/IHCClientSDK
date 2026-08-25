using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>Recursive tree-node lookup shared across the visual suite.</summary>
internal static class TreeNodes
{
    /// <summary>The first node in <paramref name="roots"/> (depth-first) whose element id equals
    /// <paramref name="id"/>, or null.</summary>
    public static TreeNodeViewModel? FindById(IEnumerable<TreeNodeViewModel> roots, ElementId id) =>
        FindFirst(roots, n => n.ElementId == id);

    /// <summary>
    /// A row's VARIABLE NAME — its label with any rendered value suffix removed. A typed variable row reads
    /// <c>"Tal = 0"</c> (uxparity2 W8/T027: the value is rendered per type, in every section), so a test looking for
    /// the variable itself must not match on the whole label.
    /// </summary>
    public static string NameOf(TreeNodeViewModel node) =>
        node.DisplayName.Split(" = ", 2, StringSplitOptions.None)[0];

    /// <summary>The first PIN in <paramref name="roots"/> whose variable name is <paramref name="name"/>, ignoring
    /// any rendered value suffix.</summary>
    public static TreeNodeViewModel? FindPin(IEnumerable<TreeNodeViewModel> roots, string name) =>
        FindFirst(roots, n => n.IsPin && NameOf(n) == name);

    /// <summary>The first node in <paramref name="roots"/> (depth-first) matching <paramref name="match"/>, or null.</summary>
    public static TreeNodeViewModel? FindFirst(IEnumerable<TreeNodeViewModel> roots, Func<TreeNodeViewModel, bool> match)
    {
        foreach (TreeNodeViewModel node in roots)
        {
            if (match(node))
                return node;
            if (FindFirst(node.Children, match) is { } found)
                return found;
        }
        return null;
    }
}

/// <summary>
/// Headless drag-and-drop test helpers for Wave 9 (A-30…A-34). A window test names only the two <b>nodes</b> to drag
/// between — never the <c>DataTransfer</c>, the hit-test point, the <c>DragEnter</c>-before-<c>DragOver</c> sequencing,
/// or the dispatcher pump. Each helper builds its payload with the <b>production</b>
/// <see cref="TreeDragData.BuildDragData"/>, so the test exercises the real source-payload contract the drop handler
/// reads back — not a shortcut.
/// <para><b>Use these only for the FEW window tests that verify the end-to-end wiring.</b> Keep the bulk of the
/// legality/mutation coverage at the controller layer — call <c>vm.DragDrop.CanDropOn(draggedId, targetId)</c> /
/// <c>await vm.DragDrop.PerformDropAsync(draggedId, targetId)</c> directly through a <see cref="ShellHarness"/> with no window
/// (faster, and where §0.3 says most coverage belongs).</para>
/// <para>Reorder (A-32) drops <i>between</i> siblings at a position, not onto a node — add a position-aware variant
/// when it lands; do not force it through <see cref="DragOnto"/>.</para>
/// </summary>
internal static class DragDropTestSupport
{
    // Move|Link covers every Wave-9 gesture (re-parent, reorder, pin link, program build); the app decides the actual
    // effect from the target.
    private const DragDropEffects Allowed = DragDropEffects.Move | DragDropEffects.Link;

    /// <summary>The window point at the centre of a node's OWN label. A TreeViewItem's bounds enclose its whole
    /// expanded subtree, so its centre lands on a descendant row; the label is the node's own hit point.
    /// <para>
    /// The row is brought INTO VIEW first, and that step is load-bearing rather than tidy. A row scrolled out of
    /// the tree's viewport still translates to a perfectly well-formed window point — one that lies outside the
    /// tree, over whatever else the shell has laid out there. The gesture then targets that other control, and
    /// the test fails as a DROP-LEGALITY failure ("expected locality B, was locality A") when what actually
    /// happened is that the tree got shorter. Scrolling first keeps these tests about drag legality instead of
    /// about how much vertical space the shell happens to give the trees.
    /// </para></summary>
    public static Point RowPoint(this Window window, TreeNodeViewModel node)
    {
        TreeViewItem item = window.GetVisualDescendants().OfType<TreeViewItem>().First(i => ReferenceEquals(i.DataContext, node));
        item.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        TextBlock label = item.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == node.DisplayName);
        return label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
    }

    /// <summary>Drags <paramref name="dragged"/> onto <paramref name="target"/> — the full DragEnter → DragOver → Drop
    /// gesture — then pumps the dispatcher so the drop's async mutation completes. Assert the resulting domain state
    /// (the SDK move/link, the rebuilt tree, the status text) afterwards.</summary>
    public static void DragOnto(this Window window, TreeNodeViewModel dragged, TreeNodeViewModel target)
    {
        DataTransfer data = DragPayload(dragged);
        Point point = window.RowPoint(target);
        window.DragDrop(point, RawDragEventType.DragEnter, data, Allowed);
        window.DragDrop(point, RawDragEventType.DragOver, data, Allowed);
        window.DragDrop(point, RawDragEventType.Drop, data, Allowed);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Drags <paramref name="dragged"/> over <paramref name="target"/> (DragEnter → DragOver, no drop) and
    /// returns the drop effect the app computed — Move/Link over a legal target, None over an illegal one. The effect
    /// is captured from the routed event (via a temporary handled-events-too handler on the window, which is last on
    /// the bubble path), so it does not depend on any app observation field.</summary>
    public static DragDropEffects DragOverEffect(this Window window, TreeNodeViewModel dragged, TreeNodeViewModel target)
    {
        DataTransfer data = DragPayload(dragged);
        Point point = window.RowPoint(target);
        DragDropEffects captured = DragDropEffects.None;
        void Capture(object? sender, DragEventArgs e) => captured = e.DragEffects;
        window.AddHandler(DragDrop.DragEnterEvent, Capture, RoutingStrategies.Bubble, handledEventsToo: true);
        window.AddHandler(DragDrop.DragOverEvent, Capture, RoutingStrategies.Bubble, handledEventsToo: true);
        try
        {
            window.DragDrop(point, RawDragEventType.DragEnter, data, Allowed);
            window.DragDrop(point, RawDragEventType.DragOver, data, Allowed);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.RemoveHandler(DragDrop.DragEnterEvent, Capture);
            window.RemoveHandler(DragDrop.DragOverEvent, Capture);
        }
        return captured;
    }

    private static DataTransfer DragPayload(TreeNodeViewModel node) =>
        TreeDragData.BuildDragData(node)
            ?? throw new ArgumentException($"node '{node.DisplayName}' is not draggable (it addresses no element)", nameof(node));
}
