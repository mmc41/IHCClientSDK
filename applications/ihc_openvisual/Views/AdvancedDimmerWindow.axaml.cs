using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis.Session;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal advanced wireless-dimmer properties dialog (US-015): Soft on/off-time (ms), Manual ramp time (s),
/// Minimum/Maximum value (%) and the Load characteristic. Returns the edited
/// <see cref="AdvancedDimmerResult"/>, or null on Cancel.
/// <para>Each numeric field clamps to the bounds the CATALOG declares for its setting, carried in on the input
/// through the SDK's dialog-metadata face (T045) — this window states none of its own. What stays here is the
/// interaction: the commit, the combo's order, and what an emptied box falls back to.</para>
/// </summary>
public partial class AdvancedDimmerWindow : ResultDialog<AdvancedDimmerResult>
{
    // The load-characteristic combo matches the original IHC Visual: Auto detektion / RC / RL, with Auto (the stored
    // default) first. Order and index map 1:1 to the combo items and to the stored dimmer_setting_load_mode tokens
    // (the vendor .vis serialization is auto | rc | rl).
    internal static readonly string[] LoadModes = { "auto", "rc", "rl" };   // Auto / Capacitive (RC) / Inductive (RL)

    // The values the dialog opened with. An EMPTIED box commits the value it was opened with rather than a
    // constant repeated here: the factory defaults already live at the read site, and a second copy in the view
    // could disagree with them.
    private AdvancedDimmerInput _opened = new(0, 0, 0, 0, 0, "auto");

    public AdvancedDimmerWindow()
    {
        InitializeComponent();
    }

    public static Task<AdvancedDimmerResult?> ShowAsync(Window owner, AdvancedDimmerInput input)
    {
        var window = new AdvancedDimmerWindow { _opened = input };
        NumericFieldBounds.Apply(window.SoftOnBox, input.SoftOn);
        NumericFieldBounds.Apply(window.SoftOffBox, input.SoftOff);
        NumericFieldBounds.Apply(window.ManualRampBox, input.ManualRamp);
        NumericFieldBounds.Apply(window.MinimumBox, input.Minimum);
        NumericFieldBounds.Apply(window.MaximumBox, input.Maximum);
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
            (int)(SoftOnBox.Value ?? _opened.SoftOnMs),
            (int)(SoftOffBox.Value ?? _opened.SoftOffMs),
            (int)(ManualRampBox.Value ?? _opened.ManualRampS),
            (int)(MinimumBox.Value ?? _opened.MinimumPercent),
            (int)(MaximumBox.Value ?? _opened.MaximumPercent),
            LoadModes[loadIndex]));
    }
}
