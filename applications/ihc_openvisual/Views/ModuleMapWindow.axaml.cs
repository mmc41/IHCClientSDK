using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ihc.Vis;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The read-only data-line module map (US-050): two grids — the input and the output data lines — each row
/// giving a line's documented module type, locality and description, or marking the line unused. Mutates nothing.
/// </summary>
public partial class ModuleMapWindow : Window
{
    public ModuleMapWindow()
    {
        InitializeComponent();
    }

    public static async Task ShowAsync(Window owner, DatalineModuleMap map)
    {
        var window = new ModuleMapWindow { DataContext = map };
        await window.ShowDialog(owner);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
