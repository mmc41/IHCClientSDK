using System;
using Avalonia.Controls;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling DateTime and DateTimeOffset parameters.
/// Creates a DatePicker control for date selection.
/// </summary>
public class DateTimeParameterStrategy : IParameterControlStrategy
{
    /// <summary>
    /// Determines if this strategy can handle DateTime or DateTimeOffset types.
    /// </summary>
    public bool CanHandle(FieldMetaData field)
    {
        return field.Type == typeof(DateTime) || field.Type == typeof(DateTimeOffset);
    }

    /// <summary>
    /// Creates a DatePicker control for date selection.
    /// </summary>
    public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"DateTimeParameterStrategy cannot handle type '{field.Type.FullName}'");

        var datePicker = new DatePicker
        {
            Name = controlName,
            MinWidth = 200,
            SelectedDate = DateTimeOffset.Now
        };

        // Add tooltip if description is available
        if (!string.IsNullOrWhiteSpace(field.Description))
        {
            ToolTip.SetTip(datePicker, field.Description);
        }

        return new ControlCreationResult
        {
            Control = datePicker,
            IsComposite = false
        };
    }

    /// <summary>
    /// Extracts the date value from a DatePicker control.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is not DatePicker datePicker)
            throw new InvalidOperationException(
                $"Expected DatePicker control but got {control.GetType().Name}");

        if (datePicker.SelectedDate == null)
            return field.Type == typeof(DateTime) ? DateTime.Now : DateTimeOffset.Now;

        var selectedDate = datePicker.SelectedDate.Value;

        // Convert to appropriate type
        if (field.Type == typeof(DateTime))
        {
            return selectedDate.DateTime;
        }
        else if (field.Type == typeof(DateTimeOffset))
        {
            return selectedDate;
        }

        return null;
    }

    /// <summary>
    /// Sets a date value into a DatePicker control.
    /// </summary>
    public void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (control is not DatePicker datePicker)
            throw new InvalidOperationException(
                $"Expected DatePicker control but got {control.GetType().Name}");

        if (value == null)
        {
            datePicker.SelectedDate = DateTimeOffset.Now;
            return;
        }

        // Convert value to DateTimeOffset
        if (value is DateTime dt)
        {
            datePicker.SelectedDate = new DateTimeOffset(dt);
        }
        else if (value is DateTimeOffset dto)
        {
            datePicker.SelectedDate = dto;
        }
        else
        {
            // Try to parse as DateTime
            if (DateTime.TryParse(value.ToString(), out var parsedDate))
            {
                datePicker.SelectedDate = new DateTimeOffset(parsedDate);
            }
            else
            {
                datePicker.SelectedDate = DateTimeOffset.Now;
            }
        }
    }
}
