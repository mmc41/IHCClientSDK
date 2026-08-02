using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis.Session;
using Ihc.Vis.Addressing;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal terminal-addressing dialog for a product input/output pin (US-012): the data line and terminal, the
/// terminals already in use (in the same direction), the cable colour and note, and — for an output — the initial
/// value (OFF = normally-open / ON = normally-closed). Returns the edited <see cref="PinPropertiesResult"/> or null.
/// </summary>
public partial class PinPropertiesWindow : ResultDialog<PinPropertiesResult>
{
    public PinPropertiesWindow()
    {
        InitializeComponent();
    }

    private Func<PinPropertiesResult, Task>? _onApply;

    public static Task<PinPropertiesResult?> ShowAsync(Window owner, PinPropertiesInput input,
        Func<PinPropertiesResult, Task>? onApply = null)
    {
        var window = new PinPropertiesWindow { Title = input.Title, _onApply = onApply };
        window.DataLineBox.Maximum = DatalineAddress.MaxDataLine(input.IsOutput);
        window.DataLineBox.Value = input.DataLine;
        window.TerminalBox.Maximum = DatalineAddress.TerminalsPerLine(input.IsOutput);
        window.TerminalBox.Value = input.Terminal;
        window.CableColourBox.Text = input.CableColour;
        window.NoteBox.Text = input.Note;
        window.InUseText.Text = input.InUseTerminals.Count > 0 ? string.Join(", ", input.InUseTerminals) : "(none)";
        window.NameBox.Text = input.Name;
        window.InitialValuePanel.IsVisible = input.IsOutput;
        window.InitialValueCombo.SelectedIndex = input.InitialValueOn ? 1 : 0;
        window.PowerFailurePanel.IsVisible = input.IsOutput;
        window.SaveValueCheck.IsChecked = input.SaveOnPowerFailure;
        window.ApplyButton.IsVisible = onApply is not null;
        return window.ShowDialogForResult(owner);
    }

    private PinPropertiesResult BuildResult() =>
        new((int)(DataLineBox.Value ?? 1),
            (int)(TerminalBox.Value ?? 0),
            CableColourBox.Text ?? string.Empty,
            NoteBox.Text ?? string.Empty,
            InitialValueCombo.SelectedIndex == 1,
            SaveValueCheck.IsChecked ?? false);

    // Apply commits the current values and leaves the dialog open, so several terminals can be addressed in one
    // visit (the vendor's Anvend).
    private async void OnApply(object? sender, RoutedEventArgs e)
    {
        if (_onApply is { } apply)
            await apply(BuildResult());
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Accept(BuildResult());
}
