using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal scene-value dialog (US-024/US-058): for a dimmer scene it asks a light level (%) and a ramp time
/// (minutes/seconds); for a relay/socket scene an ON/OFF state. Returns the edited <see cref="SceneValueResult"/>,
/// or null on Cancel.
/// </summary>
public partial class SceneValueWindow : Window
{
    private SceneValueResult? _result;

    public SceneValueWindow()
    {
        InitializeComponent();
    }

    public static async Task<SceneValueResult?> ShowAsync(Window owner, SceneValueInput input)
    {
        var window = new SceneValueWindow { Title = input.Title };
        window.DimmerPanel.IsVisible = input.IsDimmer;
        window.RelayPanel.IsVisible = !input.IsDimmer;
        window.LevelBox.Value = input.LevelPercent;
        window.RampMinutesBox.Value = input.RampMinutes;
        window.RampSecondsBox.Value = input.RampSeconds;
        window.StateCombo.SelectedIndex = input.On ? 1 : 0;
        await window.ShowDialog(owner);
        return window._result;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _result = new SceneValueResult(
            StateCombo.SelectedIndex == 1,
            (int)(LevelBox.Value ?? 0),
            (int)(RampMinutesBox.Value ?? 0),
            (int)(RampSecondsBox.Value ?? 0));
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _result = null;
        Close();
    }
}
