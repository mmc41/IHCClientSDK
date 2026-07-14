using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal advanced wireless-dimmer properties dialog (US-015): Soft on/off-time (ms), Manual ramp time (s),
/// Minimum/Maximum value (%) and the Load characteristic (Inductive / Capacitive / Auto). Numeric fields clamp to
/// their documented ranges. Returns the edited <see cref="AdvancedDimmerResult"/>, or null on Cancel.
/// </summary>
public partial class AdvancedDimmerWindow : ResultDialog<AdvancedDimmerResult>
{
    // The load-characteristic combo order maps to the stored dimmer_setting_load_mode tokens.
    private static readonly string[] LoadModes = { "rl", "rc", "auto" };   // Inductive / Capacitive / Auto

    public AdvancedDimmerWindow()
    {
        InitializeComponent();
    }

    public static Task<AdvancedDimmerResult?> ShowAsync(Window owner, AdvancedDimmerInput input)
    {
        var window = new AdvancedDimmerWindow();
        window.SoftOnBox.Value = input.SoftOnMs;
        window.SoftOffBox.Value = input.SoftOffMs;
        window.ManualRampBox.Value = input.ManualRampS;
        window.MinimumBox.Value = input.MinimumPercent;
        window.MaximumBox.Value = input.MaximumPercent;
        int index = System.Array.IndexOf(LoadModes, input.LoadMode);
        window.LoadModeCombo.SelectedIndex = index >= 0 ? index : 2;   // default Auto
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        int loadIndex = LoadModeCombo.SelectedIndex is >= 0 and < 3 ? LoadModeCombo.SelectedIndex : 2;
        Accept(new AdvancedDimmerResult(
            (int)(SoftOnBox.Value ?? 700),
            (int)(SoftOffBox.Value ?? 700),
            (int)(ManualRampBox.Value ?? 2),
            (int)(MinimumBox.Value ?? 0),
            (int)(MaximumBox.Value ?? 100),
            LoadModes[loadIndex]));
    }
}
