using System;
using Avalonia.Controls;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling string parameters.
/// Creates a TextBox control for string input.
/// </summary>
public class StringParameterStrategy : IParameterControlStrategy
{
    /// <summary>
    /// Determines if this strategy can handle string types.
    /// </summary>
    public bool CanHandle(FieldMetaData field)
    {
        return field.Type == typeof(string);
    }

    /// <summary>
    /// Creates a TextBox control for string input.
    /// </summary>
    public Control CreateControl(FieldMetaData field, string controlName)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"StringParameterStrategy cannot handle type '{field.Type.FullName}'");

        var textBox = new TextBox
        {
            Name = controlName,
            Watermark = "Enter text...",
            Width = 300
        };

        // Add tooltip if description is available
        if (!string.IsNullOrWhiteSpace(field.Description))
        {
            ToolTip.SetTip(textBox, field.Description);
        }

        return textBox;
    }

    /// <summary>
    /// Subscribes to the TextBox's TextChanged event.
    /// </summary>
    public void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        if (control is TextBox textBox)
            textBox.TextChanged += (s, e) => handler(textBox, EventArgs.Empty);
    }

    /// <summary>
    /// Extracts the text value from a TextBox control.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is not TextBox textBox)
            throw new InvalidOperationException(
                $"Expected TextBox control but got {control.GetType().Name}");

        // Treat an empty field as an empty string, not null. This keeps string handling consistent (the
        // default value for a string parameter is also string.Empty), avoids driving sync control flow via
        // exceptions, and means clearing a field syncs "" rather than silently switching the value to null.
        return textBox.Text ?? string.Empty;
    }

    /// <summary>
    /// Sets a text value into a TextBox control.
    /// </summary>
    public void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (control is not TextBox textBox)
            throw new InvalidOperationException(
                $"Expected TextBox control but got {control.GetType().Name}");

        textBox.Text = value?.ToString() ?? string.Empty;
    }
}
