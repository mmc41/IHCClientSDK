using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace ihc_openvisual.Views;

/// <summary>
/// The <i>Rediger data tabeller</i> dialog (US-049): a thin view over <see cref="DataTablesViewModel"/> — the
/// eighteen data tables on the left, the selected table's user-defined texts on the right, and OK / Annuller.
/// The view-model edits a working copy; OK commits it, Annuller simply closes.
/// </summary>
public partial class DataTablesWindow : Window
{
    public DataTablesWindow()
    {
        InitializeComponent();
    }

    // Takes the IDataTablesDialogViewModel seam (T020); the runtime instance is a DataTablesViewModel, so the
    // window's compiled bindings (x:DataType) resolve against it as the DataContext.
    public static async Task ShowAsync(Window owner, IDataTablesDialogViewModel viewModel)
    {
        var window = new DataTablesWindow { DataContext = viewModel };
        await window.ShowDialog(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DataTablesViewModel vm)
            vm.Commit();
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
