using System;
using Avalonia.Controls;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling DateTime and DateTimeOffset parameters.
/// Creates a DatePicker control for date selection.
/// </summary>
public class DateTimeParameterStrategy : ParameterControlStrategyBase
{
    /// <summary>
    /// Determines if this strategy can handle DateTime or DateTimeOffset types.
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return field.Type == typeof(DateTime) || field.Type == typeof(DateTimeOffset);
    }

    /// <summary>
    /// Creates a DatePicker control for date selection.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        var datePicker = new DatePicker
        {
            Name = controlName,
            MinWidth = 200,
            SelectedDate = DateTimeOffset.Now
        };

        ApplyDescriptionTooltip(datePicker, field);

        return datePicker;
    }

    /// <summary>
    /// Subscribes to the DatePicker's SelectedDateChanged event.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        if (control is DatePicker datePicker)
            datePicker.SelectedDateChanged += (s, e) => handler(datePicker, EventArgs.Empty);
    }

    /// <summary>
    /// Extracts the date value from a DatePicker control.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var datePicker = RequireControl<DatePicker>(control);

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
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var datePicker = RequireControl<DatePicker>(control);

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
