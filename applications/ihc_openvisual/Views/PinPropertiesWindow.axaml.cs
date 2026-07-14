using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
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

    public static Task<PinPropertiesResult?> ShowAsync(Window owner, PinPropertiesInput input)
    {
        var window = new PinPropertiesWindow { Title = input.Title };
        window.DataLineBox.Maximum = DatalineAddress.MaxDataLine(input.IsOutput);
        window.DataLineBox.Value = input.DataLine;
        window.TerminalBox.Maximum = DatalineAddress.TerminalsPerLine(input.IsOutput);
        window.TerminalBox.Value = input.Terminal;
        window.CableColourBox.Text = input.CableColour;
        window.NoteBox.Text = input.Note;
        window.InUseText.Text = input.InUseTerminals.Count > 0 ? string.Join(", ", input.InUseTerminals) : "(none)";
        window.InitialValuePanel.IsVisible = input.IsOutput;
        window.InitialValueCombo.SelectedIndex = input.InitialValueOn ? 1 : 0;
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Accept(new PinPropertiesResult(
            (int)(DataLineBox.Value ?? 1),
            (int)(TerminalBox.Value ?? 0),
            CableColourBox.Text ?? string.Empty,
            NoteBox.Text ?? string.Empty,
            InitialValueCombo.SelectedIndex == 1));
}
