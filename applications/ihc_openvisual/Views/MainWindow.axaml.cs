using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;

namespace ihc_openvisual.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private bool _forceClose;

    // ── Wave 9 / A-P0 spike: drag-and-drop state (Installation tree, product → locality move POC). ──
    private TreeNodeViewModel? _dragCandidate;
    private PointerPressedEventArgs? _dragTrigger;
    private Point _dragStart;

    /// <summary>Source-side probe surface, read only by A-P0's <c>DragDropPocTests.Poc3</c>: whether the drag-source
    /// path armed and initiated under simulated pointer input (a regression guard for the <c>handledEventsToo</c>
    /// wiring below — without it a TreeViewItem eats the press and the drag never starts), and any error from
    /// <c>DoDragDropAsync</c>. The DragOver *effect* is not mirrored here — tests capture it from the routed event
    /// (see <c>DragDropTestSupport.DragOverEffect</c>). A-30 may retire these when it grows the POC.</summary>
    public bool DragInitiatedForTest { get; private set; }
    public string? DragSourceError { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        DataContextChanged += (_, _) => HookViewModel();
        // Right-click selects the node under the pointer before its context menu opens, so the menu's
        // commands (Delete/Properties/Insert product) act on the right-clicked locality (US-008/009/010).
        InstallationTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
        FunctionsTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);

        // Wave 9 / A-P0 spike — drag SOURCE + drop TARGET on the Installation tree (product → locality move). These
        // are separate Bubble handlers beside the Tunnel right-click router above, which stays untouched. The drop
        // side reads the dragged id from the DataTransfer (never a captured source field) so window.DragDrop can
        // exercise it headlessly (§0.3); legality + mutation live in the view-model (CanDropOn / PerformDropAsync).
        // handledEventsToo: a TreeViewItem marks PointerPressed handled (for selection) before it bubbles to the
        // TreeView, so a plain bubble handler here would never see the press that arms a drag — the source must opt
        // into handled events. (The Tunnel right-click router above sidesteps this by running before the item.)
        InstallationTree.AddHandler(PointerPressedEvent, OnTreeSourcePointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        InstallationTree.AddHandler(PointerMovedEvent, OnTreeSourcePointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        InstallationTree.AddHandler(PointerReleasedEvent, OnTreeSourcePointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        DragDrop.SetAllowDrop(InstallationTree, true);
        // Handle DragEnter AND DragOver with one handler: the drag device raises DragEnter (not DragOver) the first
        // time a drag reaches a new target, so the hover highlight must be computed on enter as well as on move.
        DragDrop.AddDragEnterHandler(InstallationTree, OnTreeDragOver);
        DragDrop.AddDragOverHandler(InstallationTree, OnTreeDragOver);
        DragDrop.AddDropHandler(InstallationTree, OnTreeDrop);
    }

    // ── Wave 9 / A-P0 spike: drag SOURCE. A left-press on a product arms a drag; a move past a small threshold
    // starts it via DragDrop.DoDragDropAsync with the node's id in the DataTransfer (via the testable BuildDragData
    // helper). Headless has no OS drag loop, so the started drag reaches no target — A-P0 test 3 records that, and it
    // is why the source side is covered via BuildDragData rather than the DoDragDrop call (§0.3).
    private void OnTreeSourcePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _dragCandidate = null;
        _dragTrigger = null;
        if (e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed
            && (e.Source as Control)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is TreeNodeViewModel { NodeKind: "product" } node)
        {
            _dragCandidate = node;
            _dragTrigger = e;
            _dragStart = e.GetPosition(sender as Visual);
        }
    }

    private async void OnTreeSourcePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragCandidate is not { } node || _dragTrigger is not { } trigger)
            return;
        Point pos = e.GetPosition(sender as Visual);
        if (Math.Abs(pos.X - _dragStart.X) < 4 && Math.Abs(pos.Y - _dragStart.Y) < 4)
            return;   // below the click/drag threshold — leave the candidate armed
        _dragCandidate = null;
        _dragTrigger = null;
        if (TreeDragData.BuildDragData(node) is not { } data)
            return;
        DragInitiatedForTest = true;   // the source path was entered (Poc3 guards this — proves the handledEventsToo wiring)
        try { await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Move); }
        catch (Exception ex) { DragSourceError = ex.GetType().Name; }
    }

    private void OnTreeSourcePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragCandidate = null;
        _dragTrigger = null;
    }

    // ── Wave 9 / A-P0 spike: drop TARGET. Reads the dragged id from the DataTransfer and the target node from the
    // control under the pointer, then defers legality/mutation to the view-model.
    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        if (_viewModel is null || TreeDragData.TryGetElementId(e.DataTransfer) is not { } draggedId)
            return;
        if ((e.Source as Control)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is not TreeNodeViewModel { ElementId: { } targetId })
            return;
        e.DragEffects = _viewModel.CanDropOn(draggedId, targetId) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnTreeDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel is null || TreeDragData.TryGetElementId(e.DataTransfer) is not { } draggedId)
            return;
        if ((e.Source as Control)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is not TreeNodeViewModel { ElementId: { } targetId })
            return;
        e.Handled = true;
        await _viewModel.PerformDropAsync(draggedId, targetId);
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(sender as Control).Properties.IsRightButtonPressed)
            return;
        // Select the node under the pointer in its own pane; the pane's two-way binding makes it the active node,
        // so the context menu commands act on it — works for a Functions-pane function block too.
        if (sender is TreeView tree
            && (e.Source as Control)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is TreeNodeViewModel node)
        {
            tree.SelectedItem = node;
        }
    }

    // Double-click activates the node under the pointer (US-044) — bound on the item template's root in XAML.
    // Marking it handled for EVERY node type (including the ones that open nothing) is what stops the expansion
    // toggle: IHC Visual handles the gesture everywhere and so never toggles on a double-click (F-006/F-007).
    // Suppressing it from PointerPressed does NOT work — Avalonia synthesises the DoubleTapped from the pointer
    // stream regardless of whether the pointer event was handled.
    private void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: TreeNodeViewModel node } source)
            return;
        if (source.FindAncestorOfType<TreeView>() is { } tree)
            tree.SelectedItem = node;
        _viewModel?.ActivateNodeCommand.Execute(node);
        e.Handled = true;
    }

    private void HookViewModel()
    {
        if (_viewModel is not null)
            _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
            _viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    // Moves keyboard focus into a tree pane by focusing a real row — the selected row's container when realized,
    // otherwise the first row. Focusing the bare TreeView does not move the caret (A-28).
    private static void FocusPane(TreeView tree)
    {
        var rows = tree.GetVisualDescendants().OfType<TreeViewItem>();
        Control? target = rows.FirstOrDefault(i => ReferenceEquals(i.DataContext, tree.SelectedItem))
            ?? tree.GetVisualDescendants().OfType<TreeViewItem>().FirstOrDefault();
        (target ?? (Control)tree).Focus();
    }

    // Tree keyboard shortcuts (US-044/US-045): F6 switches panes; Shift+F10 opens the context menu; F2 Properties;
    // F4 jumps to a link's opposite end; Delete removes a selected link row. (Arrow keys use the TreeView's native
    // expand/collapse — Right=expand, Left=collapse, per the platform convention the R-note asks us to follow.)
    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        TreeView? tree = sender as TreeView;
        if (e.Key == Key.F6)
        {
            // Focus() on a bare TreeView does not take — keyboard focus must land on a focusable item (A-28). Move
            // it to the sibling pane's selected row (or its first row) so the caret genuinely crosses panes.
            FocusPane(ReferenceEquals(tree, InstallationTree) ? FunctionsTree : InstallationTree);
            e.Handled = true;
            return;
        }
        if (tree is not { SelectedItem: TreeNodeViewModel node })
            return;
        if (e.Key == Key.F10 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            tree.ContextMenu?.Open(tree);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            _viewModel?.PropertiesCommand.Execute(node);
            e.Handled = true;
        }
        else if (e.Key == Key.F4 && node.IsLinkRow)
        {
            _viewModel?.NavigateLinkOppositeCommand.Execute(node);
            // Reveal the target in the opposite pane (A-6): the command expanded its ancestor chain and selected it;
            // now scroll it into view and move keyboard focus to that pane.
            TreeView target = _viewModel?.IsInstallationPaneActive == true ? InstallationTree : FunctionsTree;
            if (target.SelectedItem is { } selected)
            {
                target.ScrollIntoView(selected);
                target.Focus();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && node.CanDelete)
        {
            _viewModel?.DeleteCommand.Execute(node);   // delete any deletable node — link row, product, block, variable… (US-053/US-057)
            e.Handled = true;
        }
    }

    // Closing is synchronous, but the save prompt is async: cancel the first close, run the prompt, and only
    // close for real once the session confirms it is safe to quit (US-064).
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose || _viewModel is null)
            return;

        e.Cancel = true;
        bool canClose = await _viewModel.CanCloseAsync();
        if (canClose)
        {
            _forceClose = true;
            Close();
        }
    }
}
