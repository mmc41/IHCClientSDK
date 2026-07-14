using System;
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

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        DataContextChanged += (_, _) => HookViewModel();
        // Right-click selects the node under the pointer before its context menu opens, so the menu's
        // commands (Delete/Properties/Insert product) act on the right-clicked locality (US-008/009/010).
        InstallationTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
        FunctionsTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
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

    private void HookViewModel()
    {
        if (_viewModel is not null)
            _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
            _viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    // Tree keyboard shortcuts (US-044/US-045): F6 switches panes; Shift+F10 opens the context menu; F2 Properties;
    // F4 jumps to a link's opposite end; Delete removes a selected link row. (Arrow keys use the TreeView's native
    // expand/collapse — Right=expand, Left=collapse, per the platform convention the R-note asks us to follow.)
    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        TreeView? tree = sender as TreeView;
        if (e.Key == Key.F6)
        {
            (ReferenceEquals(tree, InstallationTree) ? FunctionsTree : InstallationTree).Focus();
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
