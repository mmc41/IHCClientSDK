using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Interactivity;
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
public partial class ProductDialogWindow : ResultDialog<ProductDialogEdits>
{
    public ProductDialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog for a composed descriptor and resolves to the installer's edits, or null on Cancel.</summary>
    public static Task<ProductDialogEdits?> ShowAsync(
        Window owner, ProductDialogViewModel viewModel, ProductDialogShowOptions? options = null,
        ProductDialogStep? onStep = null)
    {
        var window = new ProductDialogWindow();
        window.Populate(viewModel, options, onStep);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Populates the window without showing it — the seam the headless view tests drive.</summary>
    internal void Populate(
        ProductDialogViewModel viewModel, ProductDialogShowOptions? options = null,
        ProductDialogStep? onStep = null)
    {
        DataContext = viewModel;
        Title = viewModel.Title;
        _onStep = onStep;
        Arrive(viewModel, options ?? ProductDialogShowOptions.None);
    }

    /// <summary>
    /// Where a composite the installer activated is handled while this window STAYS OPEN.
    /// <para>Every composite goes through it now. It stays nullable only because a test may populate the window
    /// to inspect its shape without driving a visit; with no handler a composite gesture does nothing, which is
    /// the honest outcome — the window cannot open a sub-dialog by itself.</para>
    /// </summary>
    private ProductDialogStep? _onStep;

    /// <summary>Where the route asked the dialog to be when it appears.</summary>
    /// <remarks>
    /// Order matters: the row is picked BEFORE the step into a sub-dialog, because the step addresses whatever
    /// the grid has selected. Focus is registered last and only when nothing was stepped into — a caret in a
    /// window that is about to be covered is not where the installer is looking.
    /// </remarks>
    private void Arrive(ProductDialogViewModel viewModel, ProductDialogShowOptions options)
    {
        if (options.SelectTerminalPin is { } pin)
        {
            SelectTerminal(viewModel, pin);
        }
        if (options.SelectSettingId is { } setting)
        {
            SelectSetting(viewModel, setting);
        }
        if (options.InitialAction is { } action)
        {
            // ONCE, as the window opens, and by the same door the installer's own gesture uses — so a route
            // cannot reach an outcome the installer could not.
            Opened += OnceOnOpen;
            return;

            void OnceOnOpen(object? sender, System.EventArgs e)
            {
                Opened -= OnceOnOpen;
                // Step(), NOT CloseWith(). The arrival is the installer's own gesture performed for them, and
                // that gesture leaves this window OPEN with the sub-dialog over it. Closing here made the route
                // open the product dialog and dismiss it in the same breath, so the sub-dialog never appeared
                // and the visit committed empty — which looked, from outside, exactly like a route that did
                // nothing at all.
                Step(action);
            }
        }
        if (options.FocusAutomationId is { } id)
        {
            FocusField(id);
        }
    }

    /// <summary>
    /// Focuses the field carrying this <c>dlg.*</c> id, once the window is open, and scrolls it into view.
    /// <para>An id the dialog does not contain focuses NOTHING. A route that promised a field the descriptor did
    /// not compose was wrong, and landing on some other control would hide that instead of showing it.</para>
    /// </summary>
    internal void FocusField(string automationId)
    {
        Opened += (_, _) =>
        {
            Control? target = this.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId);
            target?.BringIntoView();
            target?.Focus();
        };
    }

    /// <summary>Pre-selects the terminal row for this pin, in whichever of the two grids holds it.</summary>
    private static void SelectTerminal(ProductDialogViewModel viewModel, string pinId)
    {
        foreach (ProductDialogGroupViewModel group in viewModel.Groups)
        {
            foreach (ProductDialogTerminalGridViewModel grid in group.TerminalGrids)
            {
                if (grid.Rows.FirstOrDefault(r => r.PinId == pinId) is { } row)
                {
                    grid.SelectedRow = row;
                    return;
                }
            }
        }
    }

    /// <summary>Pre-selects the Indstillinger row for this setting element (T047).</summary>
    /// <remarks>
    /// The selection is what the editor is opened ON: the step handler reads the grid's SelectedRow, so a route
    /// that stepped in without selecting first would have edited whatever the installer had picked last — or
    /// nothing at all, since this grid opens with no selection.
    /// </remarks>
    private static void SelectSetting(ProductDialogViewModel viewModel, string settingId)
    {
        foreach (ProductDialogSettingsGridViewModel grid in
            viewModel.Groups.SelectMany(g => g.SettingsSection))
        {
            if (grid.Rows.FirstOrDefault(r => r.Id.ToToken() == settingId) is { } row)
            {
                grid.SelectedRow = row;
                return;
            }
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e) => CloseWith();

    // Double-tapping a terminal row (US-012 [R3]) addresses it: apply the documentation, then signal the caller to
    // open the terminal-addressing sub-dialog for that terminal.
    private void OnTerminalActivated(object? sender, TappedEventArgs e) => ConfigureSelected(sender);

    private void OnConfigure(object? sender, RoutedEventArgs e) => ConfigureSelected(sender);

    /// <summary>
    /// Addresses the terminal selected in the grid the event came from — the list itself when a row was
    /// double-tapped, the button's own section when <i>Konfigurer</i> was pressed. Both carry that section as
    /// their DataContext, since Indgange and Udgange render from one template.
    /// <para>The selection is the only source: the grids are pre-selected on open, so "nothing selected" means
    /// the installer actively cleared it — configuring the first row anyway would address a terminal they did
    /// not pick.</para>
    /// </summary>
    private void ConfigureSelected(object? sender)
    {
        if (sender is Control { DataContext: ProductDialogTerminalGridViewModel grid }
            && grid.SelectedRow is ProductTerminal terminal)
            ConfigureTerminal(terminal);
    }

    /// <summary>
    /// The settings grid, found on whichever group hosts it. Resolved from the view-model rather than from the
    /// sender's DataContext — the opposite of the terminal grids, and for a plain reason: there are two of those
    /// and only ever one of these, so "the grid the event came from" is a question with no second answer.
    /// </summary>
    private ProductDialogSettingsGridViewModel? SettingsGrid =>
        (DataContext as ProductDialogViewModel)?.Groups.SelectMany(g => g.SettingsSection).FirstOrDefault();

    // Double-tapping a settings row opens Rediger konstant for it (T040) — one of the vendor's two routes.
    private void OnSettingActivated(object? sender, TappedEventArgs e) => EditSelectedSetting();

    // The other: right-click, then Egenskaber. The same window, because they are the same act.
    private void OnSettingActivated(object? sender, RoutedEventArgs e) => EditSelectedSetting();

    /// <summary>
    /// Makes the row the pointer is over the selection before its menu opens.
    /// <para>A <see cref="ListBox"/> selects on the LEFT button only, so without this <i>Egenskaber</i> would
    /// edit whatever was selected beforehand — the wrong constant, silently, which is worse than nothing
    /// happening.</para>
    /// </summary>
    private void OnSettingContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is ListBox list && e.Source is Avalonia.Visual source
            && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: ProductSetting row })
        {
            list.SelectedItem = row;
        }
    }

    /// <summary>
    /// Opens the constant editor on the selected setting.
    /// <para>The selection is the only source, as it is for the terminal grids — but this grid is not
    /// pre-selected, so an empty selection here means the installer activated nothing rather than that they
    /// cleared it. Either way there is nothing to edit.</para>
    /// </summary>
    private void EditSelectedSetting()
    {
        if (SettingsGrid?.SelectedRow is { } setting)
        {
            Step(new ProductDialogWidgetAction(DialogWidgetKind.SettingsGrid, setting.Id));
        }
    }

    private void ConfigureTerminal(ProductTerminal terminal)
    {
        if (!ElementId.TryParse(terminal.PinId, out ElementId pin))
        {
            return;   // an unparseable row addresses nothing; closing the dialog over it would lose the visit
        }
        Step(new ProductDialogWidgetAction(DialogWidgetKind.TerminalGrids, pin));
    }

    /// <summary>
    /// Steps into a composite: the window stays open and the sub-dialog appears over it.
    /// </summary>
    /// <remarks>
    /// The value check runs FIRST. Stepping into a sub-dialog is a route out of this one, and a value that
    /// breaks its rule must not slip through the side door any more than through OK.
    /// </remarks>
    private async void Step(ProductDialogWidgetAction action)
    {
        // No handler wired: this window cannot open a sub-dialog by itself, so the gesture does nothing.
        if (_onStep is not { } step)
        {
            return;
        }
        var viewModel = (ProductDialogViewModel)DataContext!;
        if (!viewModel.TryCommit())
        {
            return;
        }
        // Guarded: the handler is application code and this is an async void event path, so an unguarded fault
        // would be raised with nothing to catch it (AP-06/WS-11). It goes to the LOG, not to the dialog — an
        // exception message is an English developer diagnostic, and this window's refusal line is Danish text
        // the installer reads.
        ProductDialogRefresh? refreshed = null;
        await HandlerGuard.RunAsync(async () => refreshed = await step(action),
            Program.LoggerFactory?.CreateLogger("Ihc.OpenVisual.Views.ProductDialogWindow"), nameof(Step));

        // RE-PROJECTED, never re-read from what is on screen. The caller computed these from the visit's pending
        // values over the document; this window's rendered rows are a rendering, and deriving values back out of
        // them is the point at which a formatting change silently becomes a data change.
        if (refreshed is { } state)
        {
            viewModel.Refresh(state.Terminals, state.Settings);
        }
    }

    private void CloseWith()
    {
        var viewModel = (ProductDialogViewModel)DataContext!;
        // A value that breaks its rule keeps the dialog OPEN with the refusal stated, so it can be fixed. Closing
        // and dropping the edit, or committing it, would both lose what the installer typed (US-013). This gates
        // the widget routes too: stepping into a sub-dialog commits the documentation on the way, so an invalid
        // value must not slip through the side door.
        if (!viewModel.TryCommit())
            return;
        // The edits are committed FIRST on every route, which is what makes stepping into a sub-dialog
        // non-destructive.
        Accept(new ProductDialogEdits(viewModel.PendingEdits));
    }
}
