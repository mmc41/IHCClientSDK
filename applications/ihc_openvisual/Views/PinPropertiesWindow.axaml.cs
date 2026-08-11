using System;
using System.Collections.Generic;
using System.Globalization;
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
        // F-32: the commit stays WITHHELD until a field changes, as the reference application's does (measured:
        // OK and Anvend both enabled=false on open, both true after picking a Ledningsfarve) and as story 03/US-012
        // requires — "so an editor opened to read an address cannot accidentally rewrite it". Annuller stays live,
        // so a dialog opened by mistake is never a trap.
        //
        // Wired in the CONSTRUCTOR rather than in ShowAsync so the window is consistent however it is created —
        // the parity tests new it up directly, and a rule that only holds on one construction path is not a rule.
        OkButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;
        // The text fields watch the PROPERTY, not the TextChanged event: a direct `Text = "..."` set does not raise
        // TextChanged synchronously, so an event subscription looks wired and silently misses programmatic edits
        // (it failed exactly that way in test before this was changed).
        WatchText(CableColourBox);
        WatchText(NoteBox);
        DataLineList.SelectionChanged += OnDataLineChanged;
        TerminalList.SelectionChanged += MarkDirty;
        InitialValueCombo.SelectionChanged += MarkDirty;
        SaveValueCheck.IsCheckedChanged += MarkDirty;
    }

    // True while ShowAsync is filling the fields in: pre-filling is not the installer changing anything, and
    // without this every editor would open already armed — which is the defect, just reached differently.
    private bool _loading;

    private void WatchText(TextBox box) =>
        box.PropertyChanged += (s, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                MarkDirty(s, e);
        };

    private void MarkDirty(object? sender, EventArgs e)
    {
        if (_loading)
            return;
        _dirty = true;
        RefreshCommit();
    }

    // F-32/F-33: the commit follows DIRTY *and* a COMPLETE address. Measured on the vendor: choosing a data line
    // alone put OK and Anvend back to disabled, and only choosing the terminal re-armed them — so a half-set
    // address is not an address. "Not configured" IS complete: it is how a terminal is un-addressed.
    private bool _dirty;

    private bool AddressIsComplete =>
        DataLineList.SelectedIndex <= 0 || TerminalList.SelectedIndex >= 0;

    private void RefreshCommit()
    {
        bool armed = _dirty && AddressIsComplete;
        OkButton.IsEnabled = armed;
        ApplyButton.IsEnabled = armed;
    }

    // Choosing a line refills the terminal list for THAT line — including which of its ports are already taken —
    // and clears any port picked for the previous one, because port 3 of line 1 is not port 3 of line 2.
    private void OnDataLineChanged(object? sender, EventArgs e)
    {
        FillTerminals();
        MarkDirty(sender, e);
    }

    private void FillTerminals()
    {
        int line = DataLineList.SelectedIndex;
        var items = new List<string>();
        for (int terminal = 1; terminal <= DatalineAddress.TerminalsPerLine(_isOutput); terminal++)
        {
            items.Add(_inUse.Contains(new DatalineAddress(line, terminal))
                ? $"{terminal} (i brug)"
                : terminal.ToString(CultureInfo.InvariantCulture));
        }
        TerminalList.ItemsSource = line > 0 ? items : new List<string>();
        TerminalList.IsEnabled = line > 0;
        // Every caller re-selects explicitly on the next line, and choosing a line must clear the port picked for
        // the previous one — port 3 of line 1 is not port 3 of line 2.
        TerminalList.SelectedIndex = -1;
    }

    private Func<PinPropertiesResult, Task>? _onApply;

    public static Task<PinPropertiesResult?> ShowAsync(Window owner, PinPropertiesInput input,
        Func<PinPropertiesResult, Task>? onApply = null)
    {
        var window = new PinPropertiesWindow { Title = input.Title, _onApply = onApply };
        window.Populate(input);
        window.ApplyButton.IsVisible = onApply is not null;
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Fills the dialog from <paramref name="input"/>. Separate from <see cref="ShowAsync"/> so the
    /// parity tests can populate a window without a parent to show it over — the address rules are the window's,
    /// and a rule that can only be exercised through a modal show is a rule that does not get tested.</summary>
    internal void Populate(PinPropertiesInput input)
    {
        _loading = true;
        _isOutput = input.IsOutput;
        _inUse = [.. input.InUseTerminals];
        var lines = new List<string> { NotConfigured };
        for (int line = 1; line <= DatalineAddress.MaxDataLine(input.IsOutput); line++)
            lines.Add(line.ToString(CultureInfo.InvariantCulture));
        DataLineList.ItemsSource = lines;
        // Terminal 0 is the existing "unaddressed" convention, and it selects the not-configured entry.
        DataLineList.SelectedIndex = input.Terminal > 0 ? input.DataLine : 0;
        FillTerminals();
        TerminalList.SelectedIndex = input.Terminal > 0 ? input.Terminal - 1 : -1;
        TerminalLabel.Text = input.IsOutput ? "Udgang" : "Indgang";
        CableColourBox.Text = input.CableColour;
        NoteBox.Text = input.Note;
        NameBox.Text = input.Name;
        InitialValuePanel.IsVisible = input.IsOutput;
        InitialValueCombo.SelectedIndex = input.InitialValueOn ? 1 : 0;
        PowerFailurePanel.IsVisible = input.IsOutput;
        SaveValueCheck.IsChecked = input.SaveOnPowerFailure;
        _loading = false;   // everything above is pre-fill, not an edit
        _dirty = false;
        RefreshCommit();
    }

    private const string NotConfigured = "ikke konfigureret";
    private bool _isOutput = true;
    private HashSet<DatalineAddress> _inUse = [];

    /// <summary>The values the dialog would commit right now — the parity tests' read of the address mapping,
    /// which is otherwise only observable by driving a modal to OK.</summary>
    internal PinPropertiesResult BuildResult() =>
        new(DataLineList.SelectedIndex > 0 ? DataLineList.SelectedIndex : 1,
            DataLineList.SelectedIndex > 0 ? TerminalList.SelectedIndex + 1 : 0,
            CableColourBox.Text ?? string.Empty,
            NoteBox.Text ?? string.Empty,
            InitialValueCombo.SelectedIndex == 1,
            SaveValueCheck.IsChecked ?? false);

    // Apply commits the current values and leaves the dialog open, so several terminals can be addressed in one
    // visit (the vendor's Anvend). Guarded: the callback is arbitrary application code and this is an async void
    // handler, so an unguarded fault would be raised with nothing to catch it (AP-06/WS-11).
    private async void OnApply(object? sender, RoutedEventArgs e)
    {
        if (_onApply is { } apply)
            await HandlerGuard.RunAsync(() => apply(BuildResult()),
                Program.LoggerFactory?.CreateLogger("Ihc.OpenVisual.Views.PinPropertiesWindow"), nameof(OnApply));
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Accept(BuildResult());
}
