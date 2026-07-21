using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis.Session;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal SMS-modem properties dialog (US-013): Name, a Location drop-down of localities, Note and Identification
/// code; the four RS485 cabling wire colours; the SIM PIN code; and telephone numbers 1–4. Returns the edited
/// <see cref="ModemPropertiesResult"/>, or null on Cancel.
/// </summary>
public partial class ModemPropertiesWindow : ResultDialog<ModemPropertiesResult>
{
    private string _currentLocalityId = string.Empty;

    public ModemPropertiesWindow()
    {
        InitializeComponent();
    }

    public static Task<ModemPropertiesResult?> ShowAsync(Window owner, ModemPropertiesInput input)
    {
        var window = new ModemPropertiesWindow { Title = input.Title };
        window._currentLocalityId = input.CurrentLocalityId;
        window.NameBox.Text = input.Name;
        window.NoteBox.Text = input.Note;
        window.IdentificationBox.Text = input.IdentificationCode;
        window.Cable0VBox.Text = input.Cable0V;
        window.Cable24VBox.Text = input.Cable24V;
        window.CableRS485MinusBox.Text = input.CableRS485Minus;
        window.CableRS485PlusBox.Text = input.CableRS485Plus;
        window.PinCodeBox.Text = input.PinCode;
        window.Phone1Box.Text = input.PhoneNumbers.ElementAtOrDefault(0) ?? string.Empty;
        window.Phone2Box.Text = input.PhoneNumbers.ElementAtOrDefault(1) ?? string.Empty;
        window.Phone3Box.Text = input.PhoneNumbers.ElementAtOrDefault(2) ?? string.Empty;
        window.Phone4Box.Text = input.PhoneNumbers.ElementAtOrDefault(3) ?? string.Empty;
        window.LocationCombo.ItemsSource = input.Localities;
        window.LocationCombo.SelectedItem = input.Localities.FirstOrDefault(l => l.Id == input.CurrentLocalityId);
        window.FocusOnOpen(window.NameBox);
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        string localityId = (LocationCombo.SelectedItem as LocalityChoice)?.Id ?? _currentLocalityId;
        var phones = new List<string>
        {
            Phone1Box.Text ?? string.Empty,
            Phone2Box.Text ?? string.Empty,
            Phone3Box.Text ?? string.Empty,
            Phone4Box.Text ?? string.Empty,
        };
        Accept(new ModemPropertiesResult(
            NameBox.Text ?? string.Empty, localityId, NoteBox.Text ?? string.Empty, IdentificationBox.Text ?? string.Empty,
            Cable0VBox.Text ?? string.Empty, Cable24VBox.Text ?? string.Empty,
            CableRS485MinusBox.Text ?? string.Empty, CableRS485PlusBox.Text ?? string.Empty,
            PinCodeBox.Text ?? string.Empty, phones));
    }
}
