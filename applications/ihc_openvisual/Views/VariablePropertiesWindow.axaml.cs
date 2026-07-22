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

    public VariablePropertiesWindow()
    {
        InitializeComponent();
    }

    public static Task<VariablePropertiesResult?> ShowAsync(Window owner, VariablePropertiesInput input)
    {
        var window = new VariablePropertiesWindow { Title = input.Title };
        window.NameBox.Text = input.Name;
        window.NoteBox.Text = input.Note;
        window.ApplyKind(input.Current);
        window.FocusOnOpen(window.NameBox);
        return window.ShowDialogForResult(owner);
    }

    private void ApplyKind(ResourceInitialValue value)
    {
        _kind = value.Kind;
        BoolBox.IsVisible = value.Kind == ResourceValueKind.Bool;
        NumberPanel.IsVisible = value.Kind == ResourceValueKind.Number;
        TimePanel.IsVisible = value.Kind == ResourceValueKind.Time;
        switch (value.Kind)
        {
            case ResourceValueKind.Bool:
                BoolBox.IsChecked = value.Bool;
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

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Accept(new VariablePropertiesResult(NameBox.Text ?? string.Empty, NoteBox.Text ?? string.Empty, ReadValue()));

    private ResourceInitialValue ReadValue() => _kind switch
    {
        ResourceValueKind.Bool => ResourceInitialValue.OfBool(BoolBox.IsChecked == true),
        ResourceValueKind.Number => ResourceInitialValue.OfNumber(ParseLong(NumberBox.Text)),
        ResourceValueKind.Time => ResourceInitialValue.OfTime(
            ParseInt(HourBox.Text), ParseInt(MinuteBox.Text), ParseInt(SecondBox.Text), ParseInt(MsBox.Text)),
        _ => ResourceInitialValue.None,
    };

    private static long ParseLong(string? text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;

    private static int ParseInt(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
}
