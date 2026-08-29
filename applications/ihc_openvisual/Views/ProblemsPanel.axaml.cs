using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ihc_openvisual.ViewModels;

namespace ihc_openvisual.Views;

/// <summary>
/// The Problemer panel: the shell's bottom region listing the current project's validation findings.
/// </summary>
/// <remarks>
/// A UserControl rather than more markup inside <see cref="MainWindow"/>, because the panel grows a findings
/// table, per-severity chrome and a staleness presentation of its own — and because the seam is where its
/// view-model binds. It inherits the shell's DataContext until that view-model exists.
/// </remarks>
public partial class ProblemsPanel : UserControl
{
    public ProblemsPanel()
    {
        InitializeComponent();
        // TUNNELLING, and attached here rather than declared in the markup. Enter reaches the focused ROW first
        // and the shell's default button after; a bubbling handler on the list sees it only if nothing in
        // between claims it, which is not a property this panel can rely on. Tunnelling puts the panel's own
        // meaning for Enter ahead of both.
        AddHandler(KeyDownEvent, OnRowsKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>The findings list, once the template has produced it.</summary>
    private Control? Rows => this.FindControl<Control>("ProblemsContent");

    /// <summary>
    /// The list's own view-model, or null before it binds. Read off the LIST rather than off this UserControl:
    /// the table sits under a Border that retargets DataContext to the panel view-model.
    /// </summary>
    private ProblemsPanelViewModel? Panel => Rows?.DataContext as ProblemsPanelViewModel;

    /// <summary>
    /// Double-click ACTIVATES the row — the whole route. Single click is left alone: it produces the selection
    /// and nothing else, so reading down the list never moves the trees or opens a window.
    /// </summary>
    private void OnRowsDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Panel is { } panel)
        {
            _ = panel.ActivateRowAsync(panel.SelectedRow);
        }
    }

    /// <summary>
    /// Enter activates the selected row, with exactly the effect double-click has — parity is the requirement,
    /// so both gestures call the one entry point rather than each doing its own version of the route.
    /// </summary>
    /// <remarks>
    /// The key is marked HANDLED. Enter is the shell's default-button gesture, so leaving it to bubble would let
    /// one keystroke both activate a finding and press whatever default button the surrounding window has.
    /// </remarks>
    private void OnRowsKeyDown(object? sender, KeyEventArgs e)
    {
        // Only when the keystroke came from INSIDE the list. The handler is on the whole panel, and Enter in the
        // heading's export button or a tier toggle means what it always meant there.
        if (e.Key is not Key.Enter
            || Rows is not { IsKeyboardFocusWithin: true }
            || Panel is not { SelectedRow: not null } panel)
        {
            return;
        }
        e.Handled = true;
        _ = panel.ActivateRowAsync(panel.SelectedRow);
    }
}
