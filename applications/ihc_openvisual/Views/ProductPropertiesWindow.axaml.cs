using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis.Session;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal product documentation-properties dialog (US-011): Name, Position, and the free-text Note, Cable type,
/// Cable numbering, Identification code and Light group fields, plus the terminal-addressing grids (US-012).
/// Returns the edited <see cref="ProductPropertiesResult"/>, or null on Cancel.
/// <para>There is deliberately no <i>Location</i> field: moving a product to another locality is a tree operation,
/// not a dialog field (A-13). The product's current locality is carried through the dialog untouched.</para>
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
        window.Populate(input);
        window.FocusOnOpen(window.NameBox);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Fills the dialog from <paramref name="input"/>. Separate from <see cref="ShowAsync"/> so the
    /// parity tests can exercise the dialog's shape — the terminal rows' accessible names among them — without a
    /// parent window to show it over.</summary>
    internal void Populate(ProductPropertiesInput input)
    {
        _currentLocalityId = input.CurrentLocalityId;
        NameBox.Text = input.Name;
        NameBox.IsEnabled = !input.NameLocked;   // a locked library product's name is fixed (A-15)
        PlaceringBox.Text = input.Position;
        NoteBox.Text = input.Note;
        CableTypeBox.Text = input.CableType;
        CableNumberBox.Text = input.CableNumber;
        CablingPanel.IsVisible = !input.IsWireless;   // wireless products have no cabling (US-014)
        AdvancedButton.IsVisible = input.IsWirelessDimmer;   // advanced dimmer settings (US-015)
        IdentificationBox.Text = input.IdentificationCode;
        LightGroupBox.Text = input.LightGroup;
        EndUserReportCheck.IsChecked = input.EndUserReport;

        IReadOnlyList<ProductTerminal> terminals = input.Terminals ?? [];
        var inputs = terminals.Where(t => !t.IsOutput).ToList();
        var outputs = terminals.Where(t => t.IsOutput).ToList();
        InputsList.ItemsSource = inputs;
        OutputsList.ItemsSource = outputs;
        // Pre-select the first row of each grid so "Configure input/output" always acts on a terminal the installer
        // can SEE selected. The button used to silently fall back to the first row when nothing was selected, which
        // addressed terminal #1 with no indication that it was the one being configured.
        InputsList.SelectedIndex = inputs.Count > 0 ? 0 : -1;
        OutputsList.SelectedIndex = outputs.Count > 0 ? 0 : -1;
        ConfigInputButton.IsEnabled = inputs.Count > 0;    // disabled when the product has no input terminals
        ConfigOutputButton.IsEnabled = outputs.Count > 0;
        TerminalsPanel.IsVisible = terminals.Count > 0;
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
        // The selection is the only source: the grids are pre-selected on open, so "nothing selected" now means the
        // installer actively cleared it — configuring the first row anyway would address a terminal they did not pick.
        if (list.SelectedItem is ProductTerminal terminal)
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
