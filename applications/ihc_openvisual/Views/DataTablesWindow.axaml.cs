using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.ViewModels;

namespace ihc_openvisual.Views;

/// <summary>
/// The Data tables dialog (US-049): a thin view over <see cref="DataTablesViewModel"/> — read-only system tables on
/// the left, editable user-defined texts (Add/Edit/Delete) on the right.
/// </summary>
public partial class DataTablesWindow : Window
{
    public DataTablesWindow()
    {
        InitializeComponent();
    }

    public static async Task ShowAsync(Window owner, DataTablesViewModel viewModel)
    {
        var window = new DataTablesWindow { DataContext = viewModel };
        await window.ShowDialog(owner);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
