using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using IhcLab;
using IhcLab.ParameterControls;

namespace Ihc.App;

/// <summary>
/// Strategy for simple types and file types.
/// Handles direct value mapping without recursion.
/// </summary>
public class SimpleFieldSyncStrategy : IFieldSyncStrategy
{
    public bool CanHandle(FieldMetaData field)
    {
        return field.IsSimple || field.IsFile;
    }

    public object? ExtractValueFromGui(Panel parent, FieldMetaData field, string indexPath)
    {
        return OperationSupport.GetFieldValue(parent, field, indexPath);
    }

    public void SetValueInGui(Panel parent, FieldMetaData field, object? value, string indexPath)
    {
        // Find strategy control and set value using strategy
        var control = FindControlByNameRecursive(parent, indexPath);
        if (control != null)
        {
            var metadata = control.Tag as OperationSupport.ControlMetadata;
            if (metadata != null)
            {
                // Use strategy to set value based on control type
                SetValueInControl(control, value);
            }
        }
    }

    /// <summary>
    /// Recursively finds a control by name.
    /// </summary>
    private Control? FindControlByNameRecursive(Control parent, string name)
    {
        if (parent.Name == name)
            return parent;

        if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl)
                {
                    var found = FindControlByNameRecursive(childControl, name);
                    if (found != null)
                        return found;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Sets value in a control based on control type.
    /// </summary>
    private void SetValueInControl(Control control, object? value)
    {
        // First, try to use the strategy's SetValue method if metadata is available
        if (control.Tag is OperationSupport.ControlMetadata metadata)
        {
            metadata.Strategy.SetValue(control, value, metadata.Field);
            return;
        }

        // Fallback: Set value directly based on control type
        switch (control)
        {
            case TextBox textBox:
                textBox.Text = value?.ToString() ?? string.Empty;
                break;
            case NumericUpDown numeric:
                if (value != null)
                {
                    numeric.Value = Convert.ToDecimal(value);
                }
                break;
            case ComboBox combo:
                // For enums, value is the enum value - need to find matching item
                combo.SelectedItem = value;
                break;
            case DatePicker datePicker:
                if (value is DateTime dateTime)
                {
                    datePicker.SelectedDate = new DateTimeOffset(dateTime);
                }
                else if (value is DateTimeOffset dateTimeOffset)
                {
                    datePicker.SelectedDate = dateTimeOffset;
                }
                break;
            case StackPanel stackPanel when stackPanel.Children.OfType<RadioButton>().Any():
                // For bool parameters (BoolParameterStrategy creates a StackPanel with RadioButtons)
                if (value is bool boolVal)
                {
                    SetRadioButtonValueInPanel(stackPanel, boolVal);
                }
                break;
            case RadioButton radioButton:
                // For bool parameters, find the appropriate radio button by content
                if (value is bool boolValue)
                {
                    SetRadioButtonValue(radioButton, boolValue);
                }
                break;
        }
    }

    /// <summary>
    /// Sets the checked state for boolean RadioButton controls in a StackPanel.
    /// </summary>
    private void SetRadioButtonValueInPanel(StackPanel stackPanel, bool value)
    {
        var radioButtons = stackPanel.Children.OfType<RadioButton>().ToList();
        string targetContent = value ? "True" : "False";

        foreach (var rb in radioButtons)
        {
            rb.IsChecked = rb.Content?.ToString() == targetContent;
        }
    }

    /// <summary>
    /// Sets the checked state for boolean RadioButton controls.
    /// </summary>
    private void SetRadioButtonValue(RadioButton radioButton, bool value)
    {
        // Find parent panel containing both True/False radio buttons
        var parent = radioButton.Parent as Panel;
        if (parent != null)
        {
            SetRadioButtonValueInPanel(parent as StackPanel ?? new StackPanel(), value);
        }
    }
}
