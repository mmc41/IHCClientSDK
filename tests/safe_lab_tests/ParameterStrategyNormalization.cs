using System;
using System.Globalization;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// The normalization oracle for the parameter-strategy round-trip law.
    ///
    /// <para>The naive law <c>ExtractValue(SetValue(c, v), f) == v</c> is <b>false</b>: every strategy
    /// normalizes the value on the way through its control, because a control cannot represent every
    /// input. <see cref="Normalize"/> states what a value becomes, so the law the property tests assert
    /// is <c>ExtractValue(SetValue(c, v), f) == Normalize(v, f)</c>.</para>
    ///
    /// <para>Each rule below was read off the strategy implementation, not off documentation:</para>
    /// <list type="bullet">
    ///   <item><b>string</b> - <c>value?.ToString() ?? string.Empty</c>. Null normalizes to empty in
    ///   <i>both</i> directions (the setter writes <c>?? string.Empty</c>, the extractor reads
    ///   <c>Text ?? string.Empty</c>), so a string field never yields null.</item>
    ///   <item><b>bool?</b> (three-state CheckBox) - <c>value as bool?</c>. Null is the indeterminate
    ///   state and survives the round-trip; so does anything that is not a bool.</item>
    ///   <item><b>bool</b> (RadioButton pair) - <c>value is bool b ? b : false</c>. The pair has no
    ///   empty state, so null collapses to false and cannot round-trip.</item>
    ///   <item><b>nullable numeric</b> - null stays null (the NumericUpDown is left empty); otherwise
    ///   the value is converted to the field's underlying numeric type.</item>
    ///   <item><b>non-nullable numeric</b> - null becomes the <i>typed</i> zero of the field's type,
    ///   not a bare <c>0</c>. This distinction is load-bearing: <c>(byte)0</c> and <c>0</c> are not
    ///   Equals, so an oracle returning <c>0</c> would fail every non-int numeric field.</item>
    /// </list>
    ///
    /// <para><b>Range caveat.</b> A NumericUpDown clamps to its Minimum/Maximum, and the float/double
    /// controls use display bounds of +/-999999999 rather than the type's full range. Values outside a
    /// field's control bounds therefore do not round-trip. That is a generator constraint (D03: exclude
    /// inputs that fail for reasons unrelated to the unit), deliberately not modelled here - so the
    /// oracle stays a statement about normalization, not about clamping.</para>
    /// </summary>
    internal static class ParameterStrategyNormalization
    {
        /// <summary>
        /// Returns the value that <c>ExtractValue</c> yields after <c>SetValue</c> has written
        /// <paramref name="value"/> into the control for <paramref name="field"/>.
        /// </summary>
        internal static object? Normalize(object? value, FieldMetaData field)
        {
            Type type = field.Type;
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            bool isNullable = Nullable.GetUnderlyingType(type) != null;

            if (type == typeof(string))
                return value?.ToString() ?? string.Empty;

            if (underlying == typeof(bool))
                return isNullable ? value as bool? : value is bool b && b;

            if (value == null)
                return isNullable ? null : TypedZero(underlying);

            return ConvertToNumeric(value, underlying);
        }

        /// <summary>
        /// The zero of a numeric type, boxed as that exact type - what a non-nullable NumericUpDown
        /// falls back to when it is empty.
        /// </summary>
        private static object TypedZero(Type underlying) => ConvertToNumeric(0m, underlying);

        /// <summary>
        /// Mirrors the strategy's decimal round-trip: the control stores a decimal, and extraction casts
        /// that decimal back to the field's numeric type.
        /// </summary>
        private static object ConvertToNumeric(object value, Type underlying)
        {
            decimal asDecimal = Convert.ToDecimal(value, CultureInfo.InvariantCulture);

            return Type.GetTypeCode(underlying) switch
            {
                TypeCode.Byte => (byte)asDecimal,
                TypeCode.SByte => (sbyte)asDecimal,
                TypeCode.Int16 => (short)asDecimal,
                TypeCode.UInt16 => (ushort)asDecimal,
                TypeCode.Int32 => (int)asDecimal,
                TypeCode.UInt32 => (uint)asDecimal,
                TypeCode.Int64 => (long)asDecimal,
                TypeCode.UInt64 => (ulong)asDecimal,
                TypeCode.Single => (float)asDecimal,
                TypeCode.Double => (double)asDecimal,
                TypeCode.Decimal => asDecimal,
                _ => throw new NotSupportedException($"Not a supported numeric type: {underlying.Name}")
            };
        }
    }
}
