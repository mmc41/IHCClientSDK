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
public class NumericParameterStrategy : IParameterControlStrategy
{
    private static readonly HashSet<Type> NumericTypes = new()
    {
        typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal)
    };

    /// <summary>
    /// Determines if this strategy can handle numeric types.
    /// </summary>
    public bool CanHandle(FieldMetaData field)
    {
        return NumericTypes.Contains(field.Type);
    }

    /// <summary>
    /// Creates a NumericUpDown control for numeric input.
    /// </summary>
    public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"NumericParameterStrategy cannot handle type '{field.Type.FullName}'");

        var numericUpDown = new NumericUpDown
        {
            Name = controlName,
            Width = 200,
            Minimum = GetMinValue(field.Type),
            Maximum = GetMaxValue(field.Type),
            Value = GetDefaultValue(field.Type),
            Increment = GetIncrement(field.Type),
            FormatString = GetFormatString(field.Type)
        };

        // Add tooltip if description is available
        if (!string.IsNullOrWhiteSpace(field.Description))
        {
            ToolTip.SetTip(numericUpDown, field.Description);
        }

        return new ControlCreationResult
        {
            Control = numericUpDown,
            IsComposite = false
        };
    }

    /// <summary>
    /// Extracts the numeric value from a NumericUpDown control.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is not NumericUpDown numericUpDown)
            throw new InvalidOperationException(
                $"Expected NumericUpDown control but got {control.GetType().Name}");

        if (numericUpDown.Value == null)
            return GetDefaultValue(field.Type);

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
    public void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (control is not NumericUpDown numericUpDown)
            throw new InvalidOperationException(
                $"Expected NumericUpDown control but got {control.GetType().Name}");

        if (value == null)
        {
            numericUpDown.Value = GetDefaultValue(field.Type);
            return;
        }

        // Convert value to decimal for NumericUpDown
        numericUpDown.Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the minimum value for the numeric type.
    /// </summary>
    private static decimal GetMinValue(Type type)
    {
        return type.Name switch
        {
            nameof(Byte) => byte.MinValue,
            nameof(SByte) => sbyte.MinValue,
            nameof(Int16) => short.MinValue,
            nameof(UInt16) => ushort.MinValue,
            nameof(Int32) => int.MinValue,
            nameof(UInt32) => uint.MinValue,
            nameof(Int64) => long.MinValue,
            nameof(UInt64) => 0, // ulong.MinValue
            nameof(Single) => -999999999m, // Use reasonable bounds for float
            nameof(Double) => -999999999m, // Use reasonable bounds for double
            nameof(Decimal) => decimal.MinValue,
            _ => decimal.MinValue
        };
    }

    /// <summary>
    /// Gets the maximum value for the numeric type.
    /// </summary>
    private static decimal GetMaxValue(Type type)
    {
        return type.Name switch
        {
            nameof(Byte) => byte.MaxValue,
            nameof(SByte) => sbyte.MaxValue,
            nameof(Int16) => short.MaxValue,
            nameof(UInt16) => ushort.MaxValue,
            nameof(Int32) => int.MaxValue,
            nameof(UInt32) => uint.MaxValue,
            nameof(Int64) => long.MaxValue,
            nameof(UInt64) => (decimal)ulong.MaxValue,
            nameof(Single) => 999999999m, // Use reasonable bounds for float
            nameof(Double) => 999999999m, // Use reasonable bounds for double
            nameof(Decimal) => decimal.MaxValue,
            _ => decimal.MaxValue
        };
    }

    /// <summary>
    /// Gets the default value for the numeric type.
    /// </summary>
    private static decimal GetDefaultValue(Type type)
    {
        return 0;
    }

    /// <summary>
    /// Gets the increment value for the numeric type.
    /// </summary>
    private static decimal GetIncrement(Type type)
    {
        return type.Name switch
        {
            nameof(Single) => 0.1m,
            nameof(Double) => 0.1m,
            nameof(Decimal) => 0.1m,
            _ => 1m
        };
    }

    /// <summary>
    /// Gets the format string for displaying the numeric type.
    /// </summary>
    private static string GetFormatString(Type type)
    {
        return type.Name switch
        {
            nameof(Single) => "F2",
            nameof(Double) => "F2",
            nameof(Decimal) => "F2",
            _ => "F0"
        };
    }
}
