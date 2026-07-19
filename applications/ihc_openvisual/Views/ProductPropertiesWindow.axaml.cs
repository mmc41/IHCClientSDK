using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis.Session;

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
        window.NameBox.IsEnabled = !input.NameLocked;   // a locked library product's name is fixed (A-15)
        window.PlaceringBox.Text = input.Position;
        window.NoteBox.Text = input.Note;
        window.CableTypeBox.Text = input.CableType;
        window.CableNumberBox.Text = input.CableNumber;
        window.CablingPanel.IsVisible = !input.IsWireless;   // wireless products have no cabling (US-014)
        window.AdvancedButton.IsVisible = input.IsWirelessDimmer;   // advanced dimmer settings (US-015)
        window.IdentificationBox.Text = input.IdentificationCode;
        window.LightGroupBox.Text = input.LightGroup;
        window.EndUserReportCheck.IsChecked = input.EndUserReport;

        var terminals = input.Terminals ?? System.Array.Empty<ProductTerminal>();
        var inputs = terminals.Where(t => !t.IsOutput).ToList();
        var outputs = terminals.Where(t => t.IsOutput).ToList();
        window.InputsList.ItemsSource = inputs;
        window.OutputsList.ItemsSource = outputs;
        window.ConfigInputButton.IsEnabled = inputs.Count > 0;    // disabled when the product has no input terminals
        window.ConfigOutputButton.IsEnabled = outputs.Count > 0;
        window.TerminalsPanel.IsVisible = terminals.Count > 0;
        window.Opened += (_, _) =>
        {
            window.NameBox.SelectAll();
            window.NameBox.Focus();
        };
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) => CloseWith();

    // "Advanced…" applies the documentation then signals the caller to open the advanced dimmer dialog (US-015).
    private void OnAdvanced(object? sender, RoutedEventArgs e) => CloseWith(openAdvanced: true);

    // Double-tapping a terminal row (US-012 [R3]) addresses it: apply the documentation and signal the caller to
    // open the terminal-addressing sub-dialog for that terminal.
    private void OnTerminalActivated(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: ProductTerminal terminal })
            CloseWith(configureTerminalPinId: terminal.PinId);
    }

    private void OnConfigureInput(object? sender, RoutedEventArgs e) => ConfigureSelected(InputsList);

    private void OnConfigureOutput(object? sender, RoutedEventArgs e) => ConfigureSelected(OutputsList);

    private void ConfigureSelected(ListBox list)
    {
        if ((list.SelectedItem as ProductTerminal ?? list.ItemsSource?.OfType<ProductTerminal>().FirstOrDefault())
            is { } terminal)
        {
            CloseWith(configureTerminalPinId: terminal.PinId);
        }
    }

    private void CloseWith(bool openAdvanced = false, string? configureTerminalPinId = null)
    {
        // The product keeps its current locality — moving it is a tree operation, not a dialog field (A-13).
        Accept(new ProductPropertiesResult(
            NameBox.Text ?? string.Empty,
            _currentLocalityId,
            NoteBox.Text ?? string.Empty,
            CableTypeBox.Text ?? string.Empty,
            CableNumberBox.Text ?? string.Empty,
            IdentificationBox.Text ?? string.Empty,
            LightGroupBox.Text ?? string.Empty,
            openAdvanced,
            configureTerminalPinId,
            PlaceringBox.Text ?? string.Empty,
            EndUserReportCheck.IsChecked ?? false));
    }
}
