using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The Reports view (US-040 / T021): the single project-documentation window that replaces the six direct report
/// commands. It binds to the app's <c>ReportsViewModel</c> (through the <see cref="IReportsDialogViewModel"/> seam),
/// offering the on-screen / printer-friendly toggle, a preview of the rendered combined HTML, and <i>Open in
/// browser</i>. The report content and its navigation are produced entirely by the view-model / SDK.
/// </summary>
public partial class ReportsWindow : Window
{
    public ReportsWindow()
    {
        InitializeComponent();
    }

    public static async Task ShowAsync(Window owner, IReportsDialogViewModel viewModel)
    {
        var window = new ReportsWindow { DataContext = viewModel };
        await window.ShowDialog(owner);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
