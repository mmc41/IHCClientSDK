using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ihc.Vis;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The read-only Wired module address map (US-050): two lists — the wired input and output modules — each row
/// showing an occupied terminal's decoded address and the product terminal on it. Mutates nothing.
/// </summary>
public partial class ModuleMapWindow : Window
{
    public ModuleMapWindow()
    {
        InitializeComponent();
    }

    public static async Task ShowAsync(Window owner, ModuleAddressMap map)
    {
        var window = new ModuleMapWindow { DataContext = map };
        await window.ShowDialog(owner);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
