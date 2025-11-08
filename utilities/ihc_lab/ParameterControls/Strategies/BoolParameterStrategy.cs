using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling boolean parameters.
/// Creates a pair of RadioButton controls for true/false selection.
/// </summary>
public class BoolParameterStrategy : IParameterControlStrategy
{
    /// <summary>
    /// Determines if this strategy can handle boolean types.
    /// </summary>
    public bool CanHandle(FieldMetaData field)
    {
        return field.Type == typeof(bool);
    }

    /// <summary>
    /// Creates a StackPanel with two RadioButton controls for true/false selection.
    /// </summary>
    public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"BoolParameterStrategy cannot handle type '{field.Type.FullName}'");

        var stackPanel = new StackPanel
        {
            Name = controlName,
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        var trueRadio = new RadioButton
        {
            Name = controlName, // Set name so event handler can identify it
            Content = "True",
            GroupName = controlName,
            IsChecked = false
        };

        var falseRadio = new RadioButton
        {
            Name = controlName, // Set same name (they represent the same field)
            Content = "False",
            GroupName = controlName,
            IsChecked = true // Default to false
        };

        stackPanel.Children.Add(trueRadio);
        stackPanel.Children.Add(falseRadio);

        // Add tooltip if description is available
        if (!string.IsNullOrWhiteSpace(field.Description))
        {
            ToolTip.SetTip(stackPanel, field.Description);
        }

        return new ControlCreationResult
        {
            Control = stackPanel,
            IsComposite = false
        };
    }

    /// <summary>
    /// Extracts the boolean value from the RadioButton controls.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is not StackPanel stackPanel)
            throw new InvalidOperationException(
                $"Expected StackPanel control but got {control.GetType().Name}");

        // Find the "True" radio button
        var trueRadio = stackPanel.Children
            .OfType<RadioButton>()
            .FirstOrDefault(r => r.Content?.ToString() == "True");

        if (trueRadio == null)
            throw new InvalidOperationException(
                "Could not find True RadioButton in control");

        return trueRadio.IsChecked == true;
    }

    /// <summary>
    /// Sets a boolean value into the RadioButton controls.
    /// </summary>
    public void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (control is not StackPanel stackPanel)
            throw new InvalidOperationException(
                $"Expected StackPanel control but got {control.GetType().Name}");

        bool boolValue = value is bool b ? b : false;

        // Find and update the radio buttons
        foreach (var radio in stackPanel.Children.OfType<RadioButton>())
        {
            string? content = radio.Content?.ToString();
            if (content == "True")
            {
                radio.IsChecked = boolValue;
            }
            else if (content == "False")
            {
                radio.IsChecked = !boolValue;
            }
        }
    }
}
