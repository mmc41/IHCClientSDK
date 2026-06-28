using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace IhcLab;

/// <summary>
/// Reusable duration (<c>hh:mm:ss</c>) editor: a watermark text box plus an initially-hidden, red error label.
/// It centralises the parse-or-error behaviour that <see cref="TimeSpan"/> input needs so every duration field in
/// the Lab validates identically - a mistyped value is rejected (the error shows and <see cref="GetValueOrThrow"/>
/// throws) rather than being silently coerced to zero.
///
/// Used by the standalone TimeSpan parameter strategy and by the ResourceValue TIME payload editor. The control
/// itself carries the editor <see cref="Control.Name"/> so container name-lookup (which searches one level deep)
/// keeps finding it after the switch from a bare TextBox.
/// </summary>
public class DurationInput : UserControl
{
    private const string Watermark = "hh:mm:ss";
    private const string DurationFormat = "c"; // constant ("[-][d.]hh:mm:ss[.fffffff]") - round-trips with Parse

    private readonly TextBox textBox;
    private readonly TextBlock errorLabel;

    /// <summary>Raised on every text edit so container strategies can route the change back to their main panel.</summary>
    public event EventHandler? ValueChanged;

    public DurationInput()
    {
        textBox = new TextBox { Width = 120, Watermark = Watermark };
        errorLabel = new TextBlock
        {
            // The glyph keeps the error perceivable without relying on colour alone.
            Text = $"⚠ Invalid duration - use {Watermark}",
            Foreground = Brushes.Red,
            IsVisible = false
        };

        textBox.TextChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);

        Content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2,
            Children = { textBox, errorLabel }
        };
    }

    /// <summary>The raw text currently in the box.</summary>
    public string Text
    {
        get => textBox.Text ?? string.Empty;
        set => textBox.Text = value;
    }

    /// <summary>
    /// Parses the current text. Empty/whitespace returns <c>null</c>; a valid <c>hh:mm:ss</c> returns the
    /// <see cref="TimeSpan"/>; anything else shows the error label and throws <see cref="FormatException"/> so no
    /// wrong value is ever extracted.
    /// </summary>
    public TimeSpan? GetValueOrThrow()
    {
        string text = textBox.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            errorLabel.IsVisible = false;
            return null;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value))
        {
            errorLabel.IsVisible = false;
            return value;
        }

        errorLabel.IsVisible = true;
        throw new FormatException($"'{text}' is not a valid duration; expected {Watermark}.");
    }

    /// <summary>
    /// Sets the duration. A <c>null</c> value clears the box. To stay caret-stable during the two-way round-trip
    /// (which fires on every keystroke), an existing non-empty entry is left untouched while it is a partial entry
    /// that does not parse yet, or already equals the incoming value - only a genuinely different value is pushed.
    /// </summary>
    public void SetValue(TimeSpan? value)
    {
        errorLabel.IsVisible = false;

        if (value == null)
        {
            textBox.Text = string.Empty;
            return;
        }

        string text = textBox.Text ?? string.Empty;
        bool midEditOrUnchanged = !string.IsNullOrWhiteSpace(text)
            && (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var current) || current == value.Value);
        if (midEditOrUnchanged)
            return;

        textBox.Text = value.Value.ToString(DurationFormat);
    }
}
