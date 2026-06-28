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
    /// Determines if this strategy can handle DateTime or DateTimeOffset types (including their nullable form).
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        var underlying = UnwrapNullable(field.Type);
        return underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset);
    }

    /// <summary>
    /// Creates a DatePicker control for date selection. A nullable date starts empty (unset = null, D3).
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        var datePicker = new DatePicker
        {
            Name = controlName,
            MinWidth = 200,
            SelectedDate = IsNullableValueType(field.Type) ? (DateTimeOffset?)null : DateTimeOffset.Now
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
        var underlying = UnwrapNullable(field.Type);

        if (datePicker.SelectedDate == null)
        {
            if (IsNullableValueType(field.Type))
                return null; // empty nullable date = null (D3)
            return underlying == typeof(DateTime) ? DateTime.Now : DateTimeOffset.Now;
        }

        var selectedDate = datePicker.SelectedDate.Value;

        // Convert to appropriate type (a boxed DateTime/DateTimeOffset is assignable to its nullable parameter).
        // Separate returns (not a ?: ) so the DateTime is not implicitly widened to DateTimeOffset by the ternary.
        if (underlying == typeof(DateTime))
            return selectedDate.DateTime;
        return selectedDate;
    }

    /// <summary>
    /// Sets a date value into a DatePicker control. A null value restores the empty (unset) state for a nullable
    /// date, or "now" for a non-nullable one.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var datePicker = RequireControl<DatePicker>(control);

        if (value == null)
        {
            datePicker.SelectedDate = IsNullableValueType(field.Type) ? (DateTimeOffset?)null : DateTimeOffset.Now;
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
        else if (DateTime.TryParse(value.ToString(), out var parsedDate))
        {
            datePicker.SelectedDate = new DateTimeOffset(parsedDate);
        }
        else
        {
            datePicker.SelectedDate = IsNullableValueType(field.Type) ? (DateTimeOffset?)null : DateTimeOffset.Now;
        }
    }
}
