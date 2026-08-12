using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace ihc_openvisual.Views;

/// <summary>
/// THE product properties dialog — one window for every family, driven entirely by a composed descriptor.
/// <para>It contains no per-family knowledge: the composer decided which groups and fields exist, what each is
/// called, what it currently holds and where it writes, and the view-model turned that into rows. Adding a family
/// is adding a preset, not a window.</para>
/// <para>Returns the edits the installer actually made — an empty list when they pressed OK without touching
/// anything, which is a COMMIT with nothing in it and never a cancellation. Cancel returns null.</para>
/// </summary>
public partial class ProductDialogWindow : ResultDialog<ProductDialogResult>
{
    public ProductDialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog for a composed descriptor and resolves to the installer's edits, or null on Cancel.</summary>
    public static Task<ProductDialogResult?> ShowAsync(Window owner, ProductDialogViewModel viewModel)
    {
        var window = new ProductDialogWindow { DataContext = viewModel, Title = viewModel.Title };
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Populates the window without showing it — the seam the headless view tests drive.</summary>
    internal void Populate(ProductDialogViewModel viewModel)
    {
        DataContext = viewModel;
        Title = viewModel.Title;
    }

    private void OnOk(object? sender, RoutedEventArgs e) => CloseWith();

    // "Avanceret" applies the documentation, then signals the caller to open the advanced dimmer dialog (US-015).
    private void OnAdvanced(object? sender, RoutedEventArgs e) =>
        CloseWith(new ProductDialogWidgetAction(DialogWidgetKind.AdvancedDimmerButton, null));

    // Double-tapping a terminal row (US-012 [R3]) addresses it: apply the documentation, then signal the caller to
    // open the terminal-addressing sub-dialog for that terminal.
    private void OnTerminalActivated(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: ProductTerminal terminal })
            ConfigureTerminal(terminal);
    }

    private void OnConfigureInput(object? sender, RoutedEventArgs e) => ConfigureSelected("InputsList");

    private void OnConfigureOutput(object? sender, RoutedEventArgs e) => ConfigureSelected("OutputsList");

    /// <summary>The selection is the only source: the grids are pre-selected on open, so "nothing selected" means
    /// the installer actively cleared it — configuring the first row anyway would address a terminal they did not
    /// pick.</summary>
    private void ConfigureSelected(string listName)
    {
        ListBox? list = this.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(l => l.Name == listName);
        if (list?.SelectedItem is ProductTerminal terminal)
            ConfigureTerminal(terminal);
    }

    private void ConfigureTerminal(ProductTerminal terminal) =>
        CloseWith(ElementId.TryParse(terminal.PinId, out ElementId pin)
            ? new ProductDialogWidgetAction(DialogWidgetKind.TerminalGrids, pin)
            : null);

    private void CloseWith(ProductDialogWidgetAction? widgetAction = null)
    {
        var viewModel = (ProductDialogViewModel)DataContext!;
        // A value that breaks its rule keeps the dialog OPEN with the refusal stated, so it can be fixed. Closing
        // and dropping the edit, or committing it, would both lose what the installer typed (US-013). This gates
        // the widget routes too: stepping into a sub-dialog commits the documentation on the way, so an invalid
        // value must not slip through the side door.
        if (!viewModel.TryCommit())
            return;
        Accept(new ProductDialogResult(viewModel.PendingEdits, widgetAction));
    }
}

/// <summary>
/// What the generic dialog produces: the edits to apply, and nothing else.
/// <para>An EMPTY list is a valid, ordinary result — the installer accepted the dialog without changing anything.
/// The caller must treat it as acceptance, not as a cancel, or an untouched OK would roll back a just-inserted
/// product (T024).</para>
/// </summary>
/// <param name="Edits">The changed fields, already resolved to the elements they write.</param>
/// <param name="WidgetAction">The composite the installer stepped into on their way out — a terminal row to
/// address, or the advanced dimmer settings — or null for a plain OK. The edits are committed FIRST either way,
/// which is what makes stepping into a sub-dialog non-destructive.</param>
public sealed record ProductDialogResult(
    ImmutableArray<ProductDialogEdit> Edits,
    ProductDialogWidgetAction? WidgetAction = null);
