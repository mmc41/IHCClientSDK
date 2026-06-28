using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for <see cref="TimeSpan"/> (and <c>TimeSpan?</c>) parameters. Renders a single text box with an
/// <c>hh:mm:ss</c> watermark (decision D2), parsed via <see cref="TimeSpan.Parse(string, IFormatProvider)"/>,
/// plus an invalid-input message shown when the text cannot be parsed. Reachable via
/// <c>ResourceValue.UnionValue.TimeValue</c> (TimeSpan?).
/// </summary>
public class TimeSpanParameterStrategy : ParameterControlStrategyBase
{
    private const string Watermark = "hh:mm:ss";
    private const string TimeSpanFormat = "c"; // constant ("[-][d.]hh:mm:ss[.fffffff]") - round-trips with Parse

    /// <summary>
    /// Determines if this strategy can handle TimeSpan (including <c>TimeSpan?</c>).
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return UnwrapNullable(field.Type) == typeof(TimeSpan);
    }

    /// <summary>
    /// Creates a StackPanel holding the text box (with hh:mm:ss watermark) and a hidden invalid-input message.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        var stackPanel = new StackPanel
        {
            Name = controlName,
            Orientation = Orientation.Vertical,
            Spacing = 2
        };

        var textBox = new TextBox
        {
            Width = 200,
            Watermark = Watermark,
            // A nullable TimeSpan starts empty (unset = null, D3); a non-nullable one starts at zero.
            Text = IsNullableValueType(field.Type) ? string.Empty : TimeSpan.Zero.ToString(TimeSpanFormat)
        };

        var errorMessage = new TextBlock
        {
            Text = $"Invalid duration - use {Watermark}",
            Foreground = Brushes.Red,
            IsVisible = false
        };

        stackPanel.Children.Add(textBox);
        stackPanel.Children.Add(errorMessage);

        ApplyDescriptionTooltip(stackPanel, field);

        return stackPanel;
    }

    /// <summary>
    /// Subscribes to the text box's TextChanged event, passing the owning StackPanel as the sender.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        if (control is StackPanel stackPanel && TextBoxOf(stackPanel) is TextBox textBox)
            textBox.TextChanged += (s, e) => handler(stackPanel, EventArgs.Empty);
    }

    /// <summary>
    /// Extracts the TimeSpan from the text box. Empty text is null for a nullable parameter (D3) or
    /// <see cref="TimeSpan.Zero"/> otherwise. Unparseable text shows the invalid-input message and throws,
    /// so no wrong value is extracted.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var stackPanel = RequireControl<StackPanel>(control);
        var textBox = TextBoxOf(stackPanel) ?? throw new InvalidOperationException("TimeSpan control has no text box");
        var errorMessage = ErrorOf(stackPanel);

        string text = textBox.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            SetError(errorMessage, false);
            return IsNullableValueType(field.Type) ? null : TimeSpan.Zero;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var timeSpan))
        {
            SetError(errorMessage, false);
            return timeSpan;
        }

        SetError(errorMessage, true);
        throw new FormatException($"'{text}' is not a valid duration; expected {Watermark}.");
    }

    /// <summary>
    /// Sets a TimeSpan into the text box (formatted hh:mm:ss). A null value restores the empty/unset state for a
    /// nullable parameter.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var stackPanel = RequireControl<StackPanel>(control);
        var textBox = TextBoxOf(stackPanel) ?? throw new InvalidOperationException("TimeSpan control has no text box");

        SetError(ErrorOf(stackPanel), false);

        if (value is TimeSpan timeSpan)
        {
            textBox.Text = timeSpan.ToString(TimeSpanFormat);
            return;
        }

        textBox.Text = IsNullableValueType(field.Type) ? string.Empty : TimeSpan.Zero.ToString(TimeSpanFormat);
    }

    private static TextBox? TextBoxOf(StackPanel stackPanel) => stackPanel.Children.OfType<TextBox>().FirstOrDefault();

    private static TextBlock? ErrorOf(StackPanel stackPanel) => stackPanel.Children.OfType<TextBlock>().FirstOrDefault();

    private static void SetError(TextBlock? errorMessage, bool visible)
    {
        if (errorMessage != null)
            errorMessage.IsVisible = visible;
    }
}
