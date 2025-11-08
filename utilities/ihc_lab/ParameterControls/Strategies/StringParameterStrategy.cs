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
    public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
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

        return new ControlCreationResult
        {
            Control = textBox,
            IsComposite = false
        };
    }

    /// <summary>
    /// Extracts the text value from a TextBox control.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is not TextBox textBox)
            throw new InvalidOperationException(
                $"Expected TextBox control but got {control.GetType().Name}");

        // Return null for empty strings to match typical parameter behavior
        return string.IsNullOrEmpty(textBox.Text) ? null : textBox.Text;
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
