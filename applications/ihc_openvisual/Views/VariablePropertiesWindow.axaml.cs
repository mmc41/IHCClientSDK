using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Session;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal ordinary-variable Properties dialog (US-027, T016): a <i>Name</i> field, a <i>Note</i> field, and a
/// type-appropriate <i>initial value</i> control — a checkbox for a bool, a number box for a counter/integer, or an
/// H/M/S(/ms) group for a timer/time. The control shown is chosen from the input value's
/// <see cref="ResourceValueKind"/>; a <see cref="ResourceValueKind.None"/> variable shows no value control. Returns
/// the edited <see cref="VariablePropertiesResult"/>, or null on Cancel.
/// </summary>
public partial class VariablePropertiesWindow : ResultDialog<VariablePropertiesResult>
{
    private ResourceValueKind _kind = ResourceValueKind.None;

    /// <summary>How many decimals the value field shows — and therefore rounds to on commit. Per type, supplied by
    /// the caller (F-41): kW/kWh 3, Kommatal 2, Fugtighed/Temperatur 1, W/Wh 0.</summary>
    private int _decimalPlaces = 2;

    /// <summary>The value field's culture: Danish, with a comma separator, matching both the original's field and
    /// the tree row. The FILE is invariant and period-separated — that conversion is the engine's, not this
    /// dialog's, and keeping them apart is what stops a machine's locale reaching a project.</summary>
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    public VariablePropertiesWindow()
    {
        InitializeComponent();
    }

    public static Task<VariablePropertiesResult?> ShowAsync(Window owner, VariablePropertiesInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var window = new VariablePropertiesWindow { Title = input.Title };
        window.Populate(input);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Fills the dialog from <paramref name="input"/>. Separate from <see cref="ShowAsync"/> so the
    /// parity tests can exercise the editor's shape without a parent window to show it over.</summary>
    internal void Populate(VariablePropertiesInput input)
    {
        NameBox.Text = input.Name;
        NoteBox.Text = input.Note;
        HelpNoteBox.Text = input.HelpNote;
        SaveOnPowerLossBox.IsChecked = input.SaveOnPowerLoss;   // F-27
        // F-42: the millisecond field belongs only to the types that declare one. The label goes with its box —
        // a lone "ms" caption beside nothing is worse than neither.
        MsLabel.IsVisible = input.ShowMilliseconds;
        MsBox.IsVisible = input.ShowMilliseconds;
        _decimalPlaces = input.DecimalPlaces;
        // F-50: an enum variable's choices are its own type's states; a weekday's are the seven the format
        // declares. The dialog knows the weekday's because the TOKEN it stores is the format's, not the app's —
        // an enum's states are project data and can only come from the caller.
        //
        // Both arrive as (token, label) pairs so the editor is type-agnostic: it SHOWS the label and COMMITS the
        // token. For an enum the two coincide — its stored value is an IDREF this dialog never sees, so the caller
        // resolves it to a state name on both sides.
        _choices = input.ChoiceOptions is { Count: > 0 } supplied
            ? [.. supplied.Select(state => (state, state))]
            : [.. VariableValueFormat.Weekdays];
        // Only an enum's states are project data the installer may edit; a weekday's seven are the format's.
        EditEnumTypeButton.IsVisible = input.ChoiceOptions is { Count: > 0 };
        ApplyKind(input.Current);
        // The route's field when it asked for one, the NAME otherwise — which is where this dialog has always
        // opened, and still is for every ordinary Egenskaber. Registered here rather than in ShowAsync, as the
        // terminal editor does it, so a test that populates the window without a modal loop drives the same
        // wiring the application does.
        FocusOnOpen(FocusTarget(input.Focus) ?? NameBox);
    }

    /// <summary>
    /// The window's own map from a route's field key to the control that holds that value.
    /// <para>By compiled <c>x:Name</c> reference, never by an automation-id string: a rename is then a compile
    /// error rather than a focus that silently lands nowhere. The map lives HERE because the controls are this
    /// window's business — the coordinator knows only the key.</para>
    /// <para><see cref="VariableDialogField.InitialValue"/> resolves through the variable's KIND, because the
    /// six value panels are mutually exclusive and only one is showing. A key naming a control instead would
    /// have made the caller guess which — and land the caret in a hidden panel for every type it guessed wrong.</para>
    /// <para>The kind is what decides visibility, so keying on it IS the visibility test — the panels are
    /// switched by the same value. A variable with no editable value answers null and the dialog falls back to
    /// the name.</para>
    /// </summary>
    internal Control? FocusTarget(VariableDialogField? field) =>
        field is { } wanted && ControlFor(wanted) is { IsEnabled: true } target ? target : null;

    private Control? ControlFor(VariableDialogField field) => field switch
    {
        VariableDialogField.Name => NameBox,
        VariableDialogField.Note => NoteBox,
        VariableDialogField.HelpNote => HelpNoteBox,
        VariableDialogField.Backup => SaveOnPowerLossBox,
        VariableDialogField.InitialValue => _kind switch
        {
            ResourceValueKind.Bool => BoolBox,
            ResourceValueKind.Number => NumberBox,
            ResourceValueKind.Decimal => DecimalBox,
            ResourceValueKind.Choice => ChoiceBox,
            ResourceValueKind.Date => DayBox,
            ResourceValueKind.Time => HourBox,
            // ResourceValueKind.None: the variable has no editable value, so there is nothing to focus and the
            // route honestly falls back to the name.
            _ => null,
        },
        _ => null,
    };

    private void ApplyKind(ResourceInitialValue value)
    {
        _kind = value.Kind;
        BoolPanel.IsVisible = value.Kind == ResourceValueKind.Bool;
        NumberPanel.IsVisible = value.Kind == ResourceValueKind.Number;
        TimePanel.IsVisible = value.Kind == ResourceValueKind.Time;
        ChoicePanel.IsVisible = value.Kind == ResourceValueKind.Choice;
        DatePanel.IsVisible = value.Kind == ResourceValueKind.Date;
        DecimalPanel.IsVisible = value.Kind == ResourceValueKind.Decimal;
        switch (value.Kind)
        {
            case ResourceValueKind.Decimal:
                DecimalBox.Text = value.Decimal.ToString("F" + _decimalPlaces, Danish);
                break;
            case ResourceValueKind.Date:
                DayBox.Text = value.Day.ToString(CultureInfo.InvariantCulture);
                MonthBox.Text = value.Month.ToString(CultureInfo.InvariantCulture);
                break;
            case ResourceValueKind.Bool:
                BoolBox.ItemsSource = BoolOptions;
                BoolBox.SelectedIndex = value.Bool ? 1 : 0;
                break;
            case ResourceValueKind.Choice:
                ChoiceBox.ItemsSource = Array.ConvertAll(_choices, c => c.Label);
                // An unrecognised token lands on the first entry, which is the declared default for both families.
                ChoiceBox.SelectedIndex = Math.Max(0, Array.FindIndex(_choices, c => c.Token == value.Token));
                break;
            case ResourceValueKind.Number:
                NumberBox.Text = value.Number.ToString(CultureInfo.InvariantCulture);
                break;
            case ResourceValueKind.Time:
                HourBox.Text = value.Hour.ToString(CultureInfo.InvariantCulture);
                MinuteBox.Text = value.Minute.ToString(CultureInfo.InvariantCulture);
                SecondBox.Text = value.Second.ToString(CultureInfo.InvariantCulture);
                MsBox.Text = value.Millisecond.ToString(CultureInfo.InvariantCulture);
                break;
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Accept(Result(editEnumType: false));

    // "Rediger" COMMITS what is on screen and asks for the type editor: the original applies the variable's own
    // edits and then opens "Enumerator typer og værdier", so nothing typed here is lost on the way.
    private void OnEditEnumType(object? sender, RoutedEventArgs e) => Accept(Result(editEnumType: true));

    private VariablePropertiesResult Result(bool editEnumType) =>
        new(NameBox.Text ?? string.Empty, NoteBox.Text ?? string.Empty, ReadValue(),
            HelpNoteBox.Text ?? string.Empty, SaveOnPowerLossBox.IsChecked == true, editEnumType);

    /// <summary>The two bool states, in the original's order — OFF first, so index 1 is ON (F-30).</summary>
    private static readonly string[] BoolOptions = ["OFF", "ON"];

    /// <summary>The (token, label) pairs the choice combo currently offers — <see cref="VariableValueFormat.Weekdays"/>
    /// for a weekday, or an enum type's states. The combo shows the labels; the result carries the tokens.</summary>
    private (string Token, string Label)[] _choices = [.. VariableValueFormat.Weekdays];

    /// <summary>The value the dialog would commit right now — the parity tests' read of the choice mapping, which
    /// is otherwise only observable by driving a modal to OK.</summary>
    internal ResourceInitialValue ReadValue() => _kind switch
    {
        ResourceValueKind.Bool => ResourceInitialValue.OfBool(BoolBox.SelectedIndex == 1),
        ResourceValueKind.Choice => ResourceInitialValue.OfChoice(
            _choices[Math.Clamp(ChoiceBox.SelectedIndex, 0, _choices.Length - 1)].Token),
        ResourceValueKind.Date => ResourceInitialValue.OfDate(ParseInt(DayBox.Text), ParseInt(MonthBox.Text)),
        // Rounded to the FIELD's own precision, which is what turns 42,7 typed into a W into 43 — the original
        // does the same, and without it a W would carry a fraction the type never shows.
        ResourceValueKind.Decimal => ResourceInitialValue.OfDecimal(
            Math.Round(ParseDouble(DecimalBox.Text), _decimalPlaces, MidpointRounding.AwayFromZero)),
        ResourceValueKind.Number => ResourceInitialValue.OfNumber(ParseLong(NumberBox.Text)),
        ResourceValueKind.Time => ResourceInitialValue.OfTime(
            ParseInt(HourBox.Text), ParseInt(MinuteBox.Text), ParseInt(SecondBox.Text), ParseInt(MsBox.Text)),
        _ => ResourceInitialValue.None,
    };

    private static long ParseLong(string? text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;

    // Read in DANISH, matching what the field displays: a comma is the decimal separator here, and an invariant
    // parse would read "-12,5" as nothing (falling to 0) or, worse, as -125.
    private static double ParseDouble(string? text) =>
        double.TryParse(text, NumberStyles.Float, Danish, out double value) ? value : 0;

    private static int ParseInt(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
}
