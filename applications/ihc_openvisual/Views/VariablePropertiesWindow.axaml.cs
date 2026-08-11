using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
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
        var window = new VariablePropertiesWindow { Title = input.Title };
        window.Populate(input);
        window.FocusOnOpen(window.NameBox);
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
        _choiceLabels = input.ChoiceOptions is { Count: > 0 } supplied ? [.. supplied] : WeekdayLabels;
        // Only an enum's states are project data the installer may edit; a weekday's seven are the format's.
        EditEnumTypeButton.IsVisible = input.ChoiceOptions is { Count: > 0 };
        ApplyKind(input.Current);
    }

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
                ChoiceBox.ItemsSource = _choiceLabels;
                // A weekday's token is a format token resolved through WeekdayTokens; an enum's is the state's own
                // LABEL, because its stored value is an IDREF the dialog never sees. Both land on an index.
                ChoiceBox.SelectedIndex = System.Math.Max(0,
                    ReferenceEquals(_choiceLabels, WeekdayLabels)
                        ? System.Array.IndexOf(WeekdayTokens, value.Token)
                        : System.Array.IndexOf(_choiceLabels, value.Token));
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

    /// <summary>The seven day TOKENS the format stores, in the order the reference application lists them, and
    /// the Danish labels this app shows for them. The token travels to the file; the label is ours to spell, so
    /// renaming one can never change a project (F-41).</summary>
    /// <summary>The two bool states, in the original's order — OFF first, so index 1 is ON (F-30).</summary>
    private static readonly string[] BoolOptions = ["OFF", "ON"];

    private static readonly string[] WeekdayTokens =
        ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];

    private static readonly string[] WeekdayLabels =
        ["Mandag", "Tirsdag", "Onsdag", "Torsdag", "Fredag", "Lørdag", "Søndag"];

    /// <summary>The labels the choice combo currently offers — the weekday's seven, or an enum type's states.</summary>
    private string[] _choiceLabels = WeekdayLabels;

    /// <summary>The value the dialog would commit right now — the parity tests' read of the choice mapping.</summary>
    internal ResourceInitialValue ResultForTest() => ReadValue();

    private ResourceInitialValue ReadValue() => _kind switch
    {
        ResourceValueKind.Bool => ResourceInitialValue.OfBool(BoolBox.SelectedIndex == 1),
        ResourceValueKind.Choice => ResourceInitialValue.OfChoice(
            ReferenceEquals(_choiceLabels, WeekdayLabels)
                ? WeekdayTokens[System.Math.Clamp(ChoiceBox.SelectedIndex, 0, WeekdayTokens.Length - 1)]
                : _choiceLabels[System.Math.Clamp(ChoiceBox.SelectedIndex, 0, _choiceLabels.Length - 1)]),
        ResourceValueKind.Date => ResourceInitialValue.OfDate(ParseInt(DayBox.Text), ParseInt(MonthBox.Text)),
        // Rounded to the FIELD's own precision, which is what turns 42,7 typed into a W into 43 — the original
        // does the same, and without it a W would carry a fraction the type never shows.
        ResourceValueKind.Decimal => ResourceInitialValue.OfDecimal(
            System.Math.Round(ParseDouble(DecimalBox.Text), _decimalPlaces, System.MidpointRounding.AwayFromZero)),
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
