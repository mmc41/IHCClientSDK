using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private bool _forceClose;

    // The app's logger for the handler guard below. Read from the process-wide factory rather than injected: a
    // Window is constructed by the XAML loader and by tests, so there is no constructor to inject into. Null when
    // no factory was built (headless tests, the designer), which HandlerGuard tolerates.
    private static ILogger? Logger =>
        Program.LoggerFactory?.CreateLogger("Ihc.OpenVisual.Views.MainWindow");

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
        // D06 (owner-flipped 2026-08-02) + T021 branch B (T016 verdict: disabled controls show no tooltip):
        // registry gestures follow the MENU BAR's availability. The REFUSAL is enforced by what the KeyBindings
        // bind to (Registry.GestureCommands — a KeyBinding never invokes a disabled command), NOT here: Avalonia
        // services a TopLevel's KeyBindings before any instance KeyDown handler, tunnel included, so e.Handled
        // cannot stop one. This handler only explains, in the status bar, a gesture that did not fire.
        AddHandler(KeyDownEvent, OnWindowKeyDownGateGestures, RoutingStrategies.Tunnel);
        // Right-click selects the node under the pointer before its context menu opens, so the menu's
        // commands (Delete/Properties/Insert product) act on the right-clicked locality (US-008/009/010).
        InstallationTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
        FunctionsTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);

        // Wave 9 / A-30 — drag SOURCE + drop TARGET on BOTH trees (product→locality move today; A-31…A-34 add the
        // reorder / pin-link / program-build routes). Wired beside the Tunnel right-click router above, which stays
        // untouched. The drop side reads the dragged id from the DataTransfer (never a captured source field) so
        // window.DragDrop can exercise it headlessly (§0.3); legality + mutation + highlight live in the view-model
        // (CanDropOn / PerformDropAsync / HighlightDropTarget).
        WireTreeDragDrop(InstallationTree);
        WireTreeDragDrop(FunctionsTree);

        // Home/End are handled at the WINDOW on the TUNNEL, scoped to whichever tree holds focus. A handler on the
        // TreeView never fired for them (uxparity S-29) — the keys are consumed below it — while Delete/F2/F4,
        // which the item ignores, reach the XAML KeyDown= handler normally. Tunnel ALONE: the window sits on both
        // the tunnel and the bubble route, so registering for both ran this handler (and posted the selection)
        // twice per keypress. handledEventsToo stays, because the tunnel reaches the window before anything below
        // it can mark the key handled.
        AddHandler(KeyDownEvent, OnTreeNavigationKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    // Attaches the drag SOURCE and drop TARGET handlers to a tree (A-30 — both panes share this wiring).
    // handledEventsToo: a TreeViewItem marks PointerPressed handled (for selection) before it bubbles to the TreeView,
    // so a plain bubble handler would never see the press that arms a drag — the source must opt into handled events.
    // (The Tunnel right-click router sidesteps this by running before the item.) DragEnter AND DragOver share one
    // handler: the drag device raises DragEnter (not DragOver) the first time a drag reaches a new target, so the hover
    // highlight must be computed on enter as well as on move.
    private void WireTreeDragDrop(TreeView tree)
    {
        tree.AddHandler(PointerPressedEvent, OnTreeSourcePointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        tree.AddHandler(PointerMovedEvent, OnTreeSourcePointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        tree.AddHandler(PointerReleasedEvent, OnTreeSourcePointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        DragDrop.SetAllowDrop(tree, true);
        DragDrop.AddDragEnterHandler(tree, OnTreeDragOver);
        DragDrop.AddDragOverHandler(tree, OnTreeDragOver);
        tree.AddHandler(DragDrop.DragLeaveEvent, OnTreeDragLeave);
        DragDrop.AddDropHandler(tree, OnTreeDrop);
    }

    // ── Wave 9 / A-30: drag SOURCE. A left-press on any addressable node (one with an ElementId — a product, locality,
    // pin or variable) arms a drag; a move past a small threshold starts it via DragDrop.DoDragDropAsync with the node's
    // id in the DataTransfer (via the testable BuildDragData helper). The verdict at the target decides what actually
    // happens. Headless has no OS drag loop, so the started drag reaches no target — A-P0 test 3 records that, and it is
    // why the source side is covered via BuildDragData rather than the DoDragDrop call (§0.3).
    private void OnTreeSourcePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _dragCandidate = null;
        _dragTrigger = null;
        if (e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed
            && (e.Source as Control)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is TreeNodeViewModel { ElementId: not null } node)
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
        // BuildDragData is INSIDE the guard: it reads the node's own state, and a fault there used to escape an
        // async void handler with nothing to catch it — the DoDragDropAsync catch below started one line too late.
        Exception? fault = await HandlerGuard.RunAsync(async () =>
        {
            if (TreeDragData.BuildDragData(node) is not { } data)
                return;
            DragInitiatedForTest = true;   // the source path was entered (Poc3 guards this — proves the handledEventsToo wiring)
            await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Move | DragDropEffects.Link);
        }, Logger, nameof(OnTreeSourcePointerMoved));
        if (fault is not null)
            DragSourceError = fault.GetType().Name;
    }

    private void OnTreeSourcePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragCandidate = null;
        _dragTrigger = null;
    }

    // ── Wave 9 / A-30: drop TARGET. Reads the dragged id from the DataTransfer and the target node from the control
    // under the pointer, then defers the verdict (effect + highlight) and the mutation to the view-model.
    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        if (_viewModel is null || TreeDragData.TryGetElementId(e.DataTransfer) is not { } draggedId)
        {
            _viewModel?.DragDrop.HighlightDropTarget(null);
            return;
        }
        if ((e.Source as Control)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is not TreeNodeViewModel { ElementId: { } targetId })
        {
            _viewModel.DragDrop.HighlightDropTarget(null);
            return;
        }
        DropVerdict verdict = _viewModel.DragDrop.CanDropOn(draggedId, targetId);
        e.DragEffects = ToDragDropEffects(verdict.Effect);
        _viewModel.DragDrop.HighlightDropTarget(verdict.Ok ? targetId : null);
        e.Handled = true;
    }

    // The drag left a tree without dropping — drop any lingering highlight.
    private void OnTreeDragLeave(object? sender, RoutedEventArgs e) => _viewModel?.DragDrop.HighlightDropTarget(null);

    private static DragDropEffects ToDragDropEffects(DropEffect effect) => effect switch
    {
        DropEffect.Move => DragDropEffects.Move,
        DropEffect.Link => DragDropEffects.Link,
        _ => DragDropEffects.None,
    };

    private async void OnTreeDrop(object? sender, DragEventArgs e)
    {
        _viewModel?.DragDrop.HighlightDropTarget(null);
        if (_viewModel is null || TreeDragData.TryGetElementId(e.DataTransfer) is not { } draggedId)
            return;
        if ((e.Source as Control)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is not TreeNodeViewModel { ElementId: { } targetId })
            return;
        e.Handled = true;
        // PerformDropAsync routes through the view-model's RunAsync boundary today, so this guard is a backstop
        // rather than the primary net — but an async void handler must never RELY on its callee staying guarded.
        await HandlerGuard.RunAsync(() => _viewModel.DragDrop.PerformDropAsync(draggedId, targetId),
            Logger, nameof(OnTreeDrop));
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
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.JumpedToPane -= OnJumpedToPane;
        }
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
            _viewModel.JumpedToPane += OnJumpedToPane;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    // An F4 jump moves the caret into the other pane, so keyboard focus goes with it (uxparity S-25). A named
    // handler (not a lambda) so it unsubscribes alongside CloseRequested — matching, never accumulating.
    private void OnJumpedToPane(object? sender, bool toFunctions) => FocusPane(toFunctions ? FunctionsTree : InstallationTree);

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
    // Home/End move the caret to the first / last VISIBLE row of the pane (uxparity S-29). Kept out of the
    // window's KeyBindings so they only apply inside a tree, never while typing in a dialog field.
    private void OnTreeNavigationKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;
        if (e.Key != Key.Home && e.Key != Key.End)
            return;
        // Only inside a tree: Home/End must keep their normal meaning in a text field.
        TreeView? tree = (FocusManager?.GetFocusedElement() as Control)?.FindAncestorOfType<TreeView>(includeSelf: true);
        if (tree is null || (!ReferenceEquals(tree, InstallationTree) && !ReferenceEquals(tree, FunctionsTree)))
            return;
        bool functions = ReferenceEquals(tree, FunctionsTree);
        bool home = e.Key == Key.Home;
        e.Handled = true;
        // POSTED, not called inline: the TreeView acts on Home/End itself after this handler and would
        // otherwise re-assert its own selection over ours (uxparity S-29 — the caret provably did not move
        // even though the handler ran and resolved the right pane). Explicit UIThread dispatcher, never the
        // ambient one.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (home)
                vm.SelectFirstRowCommand.Execute(functions);
            else
                vm.SelectLastVisibleRowCommand.Execute(functions);
        });
    }

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
            // ContextFlyout, not ContextMenu: the panes SHARE one MenuFlyout resource (a ContextMenu control
            // cannot be shared between two trees), so tree.ContextMenu is always null and this route silently did
            // nothing while still swallowing the key. Anchor it on the selected row's container when that row is
            // realized, so the menu opens next to the caret exactly as a right-click would.
            Control anchor = tree.GetVisualDescendants().OfType<TreeViewItem>()
                .FirstOrDefault(i => ReferenceEquals(i.DataContext, node)) ?? (Control)tree;
            tree.ContextFlyout?.ShowAt(anchor);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            TryInvokeGesture("node.properties", node);
            e.Handled = true;
        }
        else if (e.Key == Key.F4 && node.IsLinkRow)
        {
            if (TryInvokeGesture("link.jumpOpposite", node))
            {
                // Reveal the target in the opposite pane (A-6): the command expanded its ancestor chain and selected it;
                // now scroll it into view and move keyboard focus to that pane.
                TreeView target = _viewModel?.IsInstallationPaneActive == true ? InstallationTree : FunctionsTree;
                if (target.SelectedItem is { } selected)
                {
                    target.ScrollIntoView(selected);
                    target.Focus();
                }
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && TryInvokeGesture("edit.delete", node))
        {
            e.Handled = true;
        }
    }

    // Runs a registry row through its BAR-gated keyboard command (D06) — the SAME availability the
    // KeyBinding-routed gestures obey, so Delete/F2/F4, which the trees service themselves rather than through
    // <Window.KeyBindings>, cannot become a back door around it: Delete on a locked block is refused here exactly
    // as Ctrl+X is, even though the row's own gate (and so its context-menu item) still allows it (D13).
    // Returns whether it ran — a refused one needs no message from here, because the window's gesture handler
    // has already written the registry's reason to the status bar (T021 branch B).
    private bool TryInvokeGesture(string rowId, TreeNodeViewModel node)
    {
        var command = _viewModel?.Registry.GestureCommands[rowId];
        bool run = command?.CanExecute(node) == true;
        if (run)
        {
            command!.Execute(node);
        }
        return run;
    }

    // Parsed once from the registry rows' D08 gesture STRINGS — parsing to Avalonia KeyGesture is view-side
    // by design, keeping the rows headless-testable.
    private List<(KeyGesture Gesture, CommandSpec Row)>? _gestureRows;

    // D06 + T021 branch B: match the pressed gesture against the registry and, when its surface availability
    // refuses it, say WHY in the status bar (QC-06) — the refusal itself already happened, because the KeyBinding
    // is bound to the bar-gated Registry.GestureCommands entry and a disabled command is never invoked.
    // Only the KeyGesture parsing/matching is view-side (D08); WHICH refusals apply and WHY is the registry's one
    // answer (Explain), so this route can never drift from the menus.
    private void OnWindowKeyDownGateGestures(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _viewModel is not { } vm)
            return;
        _gestureRows ??= vm.Registry.Rows
            .Where(r => r.Gesture is not null)
            // PlatformGesture, not KeyGesture.Parse: on macOS the rows' "Ctrl+…" fires as Cmd+… (the KeyBindings
            // say so via {OnPlatform}), so a literal Ctrl parse here would match nothing and the refusal would go
            // unexplained — silently, since an unexplained refusal looks exactly like a working one (AP-13).
            .Select(r => (PlatformGesture.Parse(r.Gesture!), Row: r))
            .ToList();
        foreach ((KeyGesture gesture, CommandSpec row) in _gestureRows)
        {
            if (!gesture.Matches(e))
                continue;
            if (vm.Registry.Explain(row.Id) is { } reason)   // null → available: the KeyBinding route ran it
                vm.StatusText = reason;
            return;
        }
    }

    // Closing is synchronous, but the save prompt is async: cancel the first close, run the prompt, and only
    // close for real once the session confirms it is safe to quit (US-064).
    // Guarded end to end: Closing runs directly off the window message loop, so a fault here reaches NO global
    // handler (AP-06/WS-11). CanCloseAsync contains its own faults and answers false; this guard covers the rest
    // of the body (the second Close()), leaving the window open rather than half-quitting.
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose || _viewModel is null)
            return;

        e.Cancel = true;
        await HandlerGuard.RunAsync(async () =>
        {
            if (await _viewModel.CanCloseAsync())
            {
                _forceClose = true;
                Close();
            }
        }, Logger, nameof(OnClosing));
    }
}
