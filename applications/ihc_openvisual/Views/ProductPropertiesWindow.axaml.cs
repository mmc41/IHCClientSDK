using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal product documentation-properties dialog (US-011): Name, a <i>Location</i> drop-down of localities, and
/// the free-text Note, Cable type, Cable numbering, Identification code and Light group fields. Returns the edited
/// <see cref="ProductPropertiesResult"/>, or null on Cancel.
/// </summary>
public partial class ProductPropertiesWindow : ResultDialog<ProductPropertiesResult>
{
    private string _currentLocalityId = string.Empty;

    public ProductPropertiesWindow()
    {
        InitializeComponent();
    }

    public static Task<ProductPropertiesResult?> ShowAsync(Window owner, ProductPropertiesInput input)
    {
        var window = new ProductPropertiesWindow { Title = input.Title };
        window._currentLocalityId = input.CurrentLocalityId;
        window.NameBox.Text = input.Name;
        window.NoteBox.Text = input.Note;
        window.CableTypeBox.Text = input.CableType;
        window.CableNumberBox.Text = input.CableNumber;
        window.CablingPanel.IsVisible = !input.IsWireless;   // wireless products have no cabling (US-014)
        window.AdvancedButton.IsVisible = input.IsWirelessDimmer;   // advanced dimmer settings (US-015)
        window.IdentificationBox.Text = input.IdentificationCode;
        window.LightGroupBox.Text = input.LightGroup;
        window.LocationCombo.ItemsSource = input.Localities;
        window.LocationCombo.SelectedItem = input.Localities.FirstOrDefault(l => l.Id == input.CurrentLocalityId);
        window.Opened += (_, _) =>
        {
            window.NameBox.SelectAll();
            window.NameBox.Focus();
        };
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) => CloseWith(openAdvanced: false);

    // "Advanced…" applies the documentation then signals the caller to open the advanced dimmer dialog (US-015).
    private void OnAdvanced(object? sender, RoutedEventArgs e) => CloseWith(openAdvanced: true);

    private void CloseWith(bool openAdvanced)
    {
        string localityId = (LocationCombo.SelectedItem as LocalityChoice)?.Id ?? _currentLocalityId;
        Accept(new ProductPropertiesResult(
            NameBox.Text ?? string.Empty,
            localityId,
            NoteBox.Text ?? string.Empty,
            CableTypeBox.Text ?? string.Empty,
            CableNumberBox.Text ?? string.Empty,
            IdentificationBox.Text ?? string.Empty,
            LightGroupBox.Text ?? string.Empty,
            openAdvanced));
    }
}
