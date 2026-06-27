using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling numeric parameters.
/// Handles all numeric types: byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal.
/// Creates a NumericUpDown control for numeric input.
/// </summary>
public class NumericParameterStrategy : ParameterControlStrategyBase
{
    private const decimal DefaultNumericValue = 0m;

    // Single source of truth for the NumericUpDown bounds/step/format of every supported numeric type.
    // Keyed by Type; membership here also defines which types this strategy handles (see CanHandle).
    // Floats use reasonable display bounds rather than their full range.
    private static readonly Dictionary<Type, (decimal Minimum, decimal Maximum, decimal Increment, string FormatString)> NumericTypeInfos = new()
    {
        [typeof(byte)] = (byte.MinValue, byte.MaxValue, 1m, "F0"),
        [typeof(sbyte)] = (sbyte.MinValue, sbyte.MaxValue, 1m, "F0"),
        [typeof(short)] = (short.MinValue, short.MaxValue, 1m, "F0"),
        [typeof(ushort)] = (ushort.MinValue, ushort.MaxValue, 1m, "F0"),
        [typeof(int)] = (int.MinValue, int.MaxValue, 1m, "F0"),
        [typeof(uint)] = (uint.MinValue, uint.MaxValue, 1m, "F0"),
        [typeof(long)] = (long.MinValue, long.MaxValue, 1m, "F0"),
        [typeof(ulong)] = (ulong.MinValue, ulong.MaxValue, 1m, "F0"),
        [typeof(float)] = (-999999999m, 999999999m, 0.1m, "F2"),
        [typeof(double)] = (-999999999m, 999999999m, 0.1m, "F2"),
        [typeof(decimal)] = (decimal.MinValue, decimal.MaxValue, 0.1m, "F2")
    };

    /// <summary>
    /// Determines if this strategy can handle numeric types.
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return NumericTypeInfos.ContainsKey(field.Type);
    }

    /// <summary>
    /// Creates a NumericUpDown control for numeric input.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        var info = NumericTypeInfos[field.Type];
        var numericUpDown = new NumericUpDown
        {
            Name = controlName,
            Width = 200,
            Minimum = info.Minimum,
            Maximum = info.Maximum,
            Value = DefaultNumericValue,
            Increment = info.Increment,
            FormatString = info.FormatString
        };

        ApplyDescriptionTooltip(numericUpDown, field);

        return numericUpDown;
    }

    /// <summary>
    /// Subscribes to the NumericUpDown's ValueChanged event.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        if (control is NumericUpDown numericUpDown)
            numericUpDown.ValueChanged += (s, e) => handler(numericUpDown, EventArgs.Empty);
    }

    /// <summary>
    /// Extracts the numeric value from a NumericUpDown control.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var numericUpDown = RequireControl<NumericUpDown>(control);

        if (numericUpDown.Value == null)
            return DefaultNumericValue;

        decimal value = numericUpDown.Value.Value;

        // Convert decimal to the appropriate numeric type
        return field.Type.Name switch
        {
            nameof(Byte) => (byte)value,
            nameof(SByte) => (sbyte)value,
            nameof(Int16) => (short)value,
            nameof(UInt16) => (ushort)value,
            nameof(Int32) => (int)value,
            nameof(UInt32) => (uint)value,
            nameof(Int64) => (long)value,
            nameof(UInt64) => (ulong)value,
            nameof(Single) => (float)value,
            nameof(Double) => (double)value,
            nameof(Decimal) => value,
            _ => throw new NotSupportedException($"Numeric type '{field.Type.Name}' is not supported")
        };
    }

    /// <summary>
    /// Sets a numeric value into a NumericUpDown control.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var numericUpDown = RequireControl<NumericUpDown>(control);

        if (value == null)
        {
            numericUpDown.Value = DefaultNumericValue;
            return;
        }

        // Convert value to decimal for NumericUpDown
        numericUpDown.Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }
}
