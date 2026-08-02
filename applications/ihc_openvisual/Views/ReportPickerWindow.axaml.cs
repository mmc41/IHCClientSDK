using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>The shared report picker dialog (R12/D4): thin shell over <c>ReportPickerViewModel</c> — the
/// window only hosts the bindings and closes itself; all behavior lives in the view-model.</summary>
public partial class ReportPickerWindow : Window
{
    public ReportPickerWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the picker modally over <paramref name="owner"/> bound to <paramref name="viewModel"/>.</summary>
    public static Task ShowAsync(Window owner, IReportPickerViewModel viewModel)
    {
        var window = new ReportPickerWindow { DataContext = viewModel };
        return window.ShowDialog(owner);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
