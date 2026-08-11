using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis.Session;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal advanced wireless-dimmer properties dialog (US-015): Soft on/off-time (ms), Manual ramp time (s),
/// Minimum/Maximum value (%) and the Load characteristic. Numeric fields clamp to their documented ranges.
/// Returns the edited <see cref="AdvancedDimmerResult"/>, or null on Cancel.
/// </summary>
public partial class AdvancedDimmerWindow : ResultDialog<AdvancedDimmerResult>
{
    // The load-characteristic combo matches the original IHC Visual: Auto detektion / RC / RL, with Auto (the stored
    // default) first. Order and index map 1:1 to the combo items and to the stored dimmer_setting_load_mode tokens
    // (the vendor .vis serialization is auto | rc | rl).
    internal static readonly string[] LoadModes = { "auto", "rc", "rl" };   // Auto / Capacitive (RC) / Inductive (RL)

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
        window.LoadModeCombo.SelectedIndex = index >= 0 ? index : 0;   // default Auto
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        int loadIndex = LoadModeCombo.SelectedIndex is >= 0 and < 3 ? LoadModeCombo.SelectedIndex : 0;
        Accept(new AdvancedDimmerResult(
            (int)(SoftOnBox.Value ?? 700),
            (int)(SoftOffBox.Value ?? 700),
            (int)(ManualRampBox.Value ?? 5),
            (int)(MinimumBox.Value ?? 0),
            (int)(MaximumBox.Value ?? 100),
            LoadModes[loadIndex]));
    }
}
