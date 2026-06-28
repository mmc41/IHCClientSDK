using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling DateTime and DateTimeOffset parameters (including their nullable form). Renders a
/// <see cref="DatePicker"/> and a <see cref="TimePicker"/> side by side so the full timestamp is captured - the
/// time-of-day is no longer silently dropped to midnight.
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
    /// Creates a date + time picker pair. A nullable value starts empty (unset = null, D3); a non-nullable one
    /// starts at "now".
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        bool nullable = IsNullableValueType(field.Type);

        var datePicker = new DatePicker
        {
            MinWidth = 200,
            SelectedDate = nullable ? (DateTimeOffset?)null : DateTimeOffset.Now
        };

        var timePicker = new TimePicker
        {
            ClockIdentifier = "24HourClock",
            SelectedTime = nullable ? (TimeSpan?)null : DateTimeOffset.Now.TimeOfDay
        };

        var panel = new StackPanel
        {
            Name = controlName,
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { datePicker, timePicker }
        };

        ApplyDescriptionTooltip(panel, field);

        return panel;
    }

    /// <summary>
    /// Subscribes to both pickers' change events so editing either the date or the time raises the handler.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        if (control is not StackPanel panel)
            return;

        // Both pickers are leaf editors; the shared helper owns the picker -> change-event mapping. The panel is
        // always the sender so the consumer resolves the parameter index from its Name (decision D9).
        foreach (var child in panel.Children.OfType<Control>())
            SubscribeLeafChange(child, (s, e) => handler(panel, EventArgs.Empty));
    }

    /// <summary>
    /// Extracts the value by composing the picked date and time-of-day. An unset date extracts as null for a
    /// nullable parameter (D3), or "now" otherwise.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var panel = RequireControl<StackPanel>(control);
        var underlying = UnwrapNullable(field.Type);
        var datePicker = DatePickerOf(panel);

        if (datePicker?.SelectedDate == null)
        {
            if (IsNullableValueType(field.Type))
                return null; // empty nullable date = null (D3)
            return underlying == typeof(DateTime) ? (object)DateTime.Now : DateTimeOffset.Now;
        }

        var date = datePicker.SelectedDate.Value;
        var time = TimePickerOf(panel)?.SelectedTime ?? TimeSpan.Zero;
        // Compose the date (offset preserved) at midnight plus the picked time-of-day.
        var composed = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset).Add(time);

        // Separate returns (not a ?:) so the DateTime is not implicitly widened to DateTimeOffset by the ternary.
        if (underlying == typeof(DateTime))
            return composed.DateTime;
        return composed;
    }

    /// <summary>
    /// Sets a value into both pickers. A null OR unparseable value restores the empty (unset) state for a nullable
    /// parameter (empty = null, D3) or "now" for a non-nullable one, consistent with the value==null branch and
    /// <see cref="ExtractValue"/>.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var panel = RequireControl<StackPanel>(control);
        var datePicker = DatePickerOf(panel);
        var timePicker = TimePickerOf(panel);

        DateTimeOffset? dto = value switch
        {
            null => null,
            DateTime dt => new DateTimeOffset(dt),
            DateTimeOffset off => off,
            _ => DateTime.TryParse(value.ToString(), out var parsed) ? new DateTimeOffset(parsed) : (DateTimeOffset?)null
        };

        if (dto == null)
        {
            // Null/unparseable: honour the empty=null convention (D3) for a nullable field rather than forcing "now".
            bool nullable = IsNullableValueType(field.Type);
            if (datePicker != null) datePicker.SelectedDate = nullable ? (DateTimeOffset?)null : DateTimeOffset.Now;
            if (timePicker != null) timePicker.SelectedTime = nullable ? (TimeSpan?)null : DateTimeOffset.Now.TimeOfDay;
            return;
        }

        if (datePicker != null) datePicker.SelectedDate = dto.Value;
        if (timePicker != null) timePicker.SelectedTime = dto.Value.TimeOfDay;
    }

    private static DatePicker? DatePickerOf(StackPanel panel) => panel.Children.OfType<DatePicker>().FirstOrDefault();

    private static TimePicker? TimePickerOf(StackPanel panel) => panel.Children.OfType<TimePicker>().FirstOrDefault();
}
