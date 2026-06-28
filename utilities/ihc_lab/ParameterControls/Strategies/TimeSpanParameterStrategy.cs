using System;
using Avalonia.Controls;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for <see cref="TimeSpan"/> (and <c>TimeSpan?</c>) parameters. Renders a shared <see cref="DurationInput"/>
/// (an <c>hh:mm:ss</c> box with inline validation), so a mistyped duration is rejected rather than silently coerced.
/// Reachable via <c>ResourceValue.UnionValue.TimeValue</c> (TimeSpan?).
/// </summary>
public class TimeSpanParameterStrategy : ParameterControlStrategyBase
{
    /// <summary>
    /// Determines if this strategy can handle TimeSpan (including <c>TimeSpan?</c>).
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return UnwrapNullable(field.Type) == typeof(TimeSpan);
    }

    /// <summary>
    /// Creates a <see cref="DurationInput"/>. A nullable TimeSpan starts empty (unset = null, D3); a non-nullable
    /// one starts at zero.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        var input = new DurationInput { Name = controlName };
        if (!IsNullableValueType(field.Type))
            input.SetValue(TimeSpan.Zero);

        ApplyDescriptionTooltip(input, field);
        return input;
    }

    /// <summary>
    /// Subscribes to the duration input's value-changed event, passing the control as the sender.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
        => SubscribeLeafChange(control, handler);

    /// <summary>
    /// Extracts the TimeSpan. Empty text is null for a nullable parameter (D3) or <see cref="TimeSpan.Zero"/>
    /// otherwise. Unparseable text shows the inline error and throws (via <see cref="DurationInput.GetValueOrThrow"/>),
    /// so no wrong value is extracted.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var input = RequireControl<DurationInput>(control);
        TimeSpan? value = input.GetValueOrThrow() ?? (IsNullableValueType(field.Type) ? (TimeSpan?)null : TimeSpan.Zero);
        return value;
    }

    /// <summary>
    /// Sets a TimeSpan into the input. A null value clears it for a nullable parameter or restores zero otherwise.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var input = RequireControl<DurationInput>(control);
        TimeSpan? toSet = value as TimeSpan? ?? (IsNullableValueType(field.Type) ? (TimeSpan?)null : TimeSpan.Zero);
        input.SetValue(toSet);
    }
}
