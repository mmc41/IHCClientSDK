using System;
using Avalonia.Headless.NUnit;
using CsCheck;
using Ihc;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// Property-based tests for the parameter-control strategies, using CsCheck.
    ///
    /// <para>The law: writing a value into a strategy's control and reading it back yields the value
    /// the control can represent - <c>ExtractValue(SetValue(c, v), f) == Normalize(v, f)</c>, with
    /// <see cref="ParameterStrategyNormalization.Normalize"/> as the oracle. The naive
    /// <c>== v</c> form is false, because null (and, for the RadioButton pair, anything non-bool)
    /// normalizes.</para>
    ///
    /// <para>These generalize the example-based fixtures, which pinned one or two values of three of
    /// the eleven numeric types. Two things the examples did not do and these must: range over
    /// <b>every</b> handled numeric type, and range over <b>both</b> the nullable and non-nullable
    /// shape of each - the shapes take different branches in both strategies.</para>
    /// </summary>
    [TestFixture]
    public class ParameterStrategyPropertyTests : AvaloniaTestBase
    {
        // One row per type NumericParameterStrategy handles. The value generator is bounded by the
        // control's Minimum/Maximum for that type: NumericUpDown CLAMPS, and float/double use display
        // bounds of +/-999999999 rather than their real range, so an out-of-bounds sample would fail
        // for a reason unrelated to the round-trip (D03). Halves keep a fractional part that is exact
        // in binary and decimal alike, so no sample fails on representation instead of behaviour.
        private static readonly (Type Type, Gen<object> Values)[] NumericSpecs =
        [
            (typeof(byte), Gen.Byte.Select(v => (object)v)),
            (typeof(sbyte), Gen.SByte.Select(v => (object)v)),
            (typeof(short), Gen.Short.Select(v => (object)v)),
            (typeof(ushort), Gen.UShort.Select(v => (object)v)),
            (typeof(int), Gen.Int.Select(v => (object)v)),
            (typeof(uint), Gen.UInt.Select(v => (object)v)),
            (typeof(long), Gen.Long.Select(v => (object)v)),
            (typeof(ulong), Gen.ULong.Select(v => (object)v)),
            (typeof(float), Gen.Int[-999999998, 999999998].Select(v => (object)(v / 2.0f))),
            (typeof(double), Gen.Int[-999999998, 999999998].Select(v => (object)(v / 2.0))),
            (typeof(decimal), Gen.Int[-999999998, 999999998].Select(v => (object)(v / 2.0m))),
        ];

        // Every numeric type, in both shapes, with null sampled roughly one time in four so the
        // unset/zero branch is exercised without crowding out real values.
        private static Gen<(Type FieldType, object? Value)> NumericCases =>
            from index in Gen.Int[0, NumericSpecs.Length - 1]
            from nullableShape in Gen.Bool
            from nullDraw in Gen.Int[0, 3]
            from raw in NumericSpecs[index].Values
            select (
                nullableShape ? typeof(Nullable<>).MakeGenericType(NumericSpecs[index].Type) : NumericSpecs[index].Type,
                nullDraw == 0 ? null : raw);

        // bool takes the RadioButton-pair path and bool? the three-state CheckBox path.
        private static Gen<(Type FieldType, object? Value)> BoolCases =>
            from nullableShape in Gen.Bool
            from valueDraw in Gen.Int[0, 2]
            select (
                nullableShape ? typeof(bool?) : typeof(bool),
                valueDraw switch { 0 => (object?)true, 1 => false, _ => null });

        // Characters worth hitting OFTEN. A generator drawing uniformly from the ~1.1M Unicode scalars
        // effectively never produces a leading or trailing SPACE, which left this property blind to a
        // Trim() regression - and "whitespace is preserved, the value round-trips exactly as entered"
        // is a documented invariant of StringParameterStrategy. Measured: with the uniform generator a
        // deliberately trimming strategy still passed; with this pool it fails.
        private static readonly string[] InterestingCharacters =
            [" ", "  ", "\t", "a", "Z", "0", "9", "æ", "ø", "å", "Ä", "日", "🔒", "-", "_", "."];

        // Mostly drawn from the pool above, with one character in five a fully arbitrary Unicode scalar
        // (built from scalar values, so never a lone surrogate). The C0 control range is excluded: a
        // TextBox may normalize line endings, which would fail the round-trip for a reason unrelated to
        // the strategy (D03).
        private static Gen<string> Texts =>
            (from useArbitrary in Gen.Int[0, 4]
             from poolIndex in Gen.Int[0, InterestingCharacters.Length - 1]
             from scalar in Gen.Int[0x20, 0x10FFFF].Where(cp => cp < 0xD800 || cp > 0xDFFF)
             select useArbitrary == 0 ? char.ConvertFromUtf32(scalar) : InterestingCharacters[poolIndex])
            .Array[0, 40]
            .Select(parts => string.Concat(parts));

        // Null sampled roughly one time in four, so the "null becomes empty" branch is exercised.
        private static Gen<object?> TextCases =>
            from nullDraw in Gen.Int[0, 3]
            from text in Texts
            select nullDraw == 0 ? null : (object?)text;

        // Whole seconds between 1900 and ~2100, at offsets across the real UTC range. Sub-second ticks
        // are excluded (the TimePicker's granularity, not the strategy's, would decide the outcome) and
        // so are the DateTime extremes, where composing a local offset overflows the type.
        private static Gen<DateTimeOffset> Timestamps =>
            from days in Gen.Int[0, 73048]
            from seconds in Gen.Int[0, 86399]
            from offsetHours in Gen.Int[-12, 12]
            select new DateTimeOffset(
                new DateTime(1900, 1, 1).AddDays(days).AddSeconds(seconds),
                TimeSpan.FromHours(offsetHours));

        private static Gen<(Type FieldType, object Value)> DateCases =>
            from timestamp in Timestamps
            from asDateTime in Gen.Bool
            select asDateTime
                ? ((Type)typeof(DateTime), (object)timestamp.DateTime)
                : ((Type)typeof(DateTimeOffset), (object)timestamp);

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void NumericStrategy_SetThenExtract_YieldsTheNormalizedValue()
        {
            var strategy = new NumericParameterStrategy();

            // threads: 1 - the controls have thread affinity to the headless UI thread this test runs on.
            NumericCases.Sample(testCase => AssertRoundTrip(
                (control, value, field) => strategy.SetValue(control, value, field),
                (control, field) => strategy.ExtractValue(control, field),
                (field, name) => strategy.CreateControl(field, name),
                testCase.FieldType, testCase.Value), threads: 1);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void BoolStrategy_SetThenExtract_YieldsTheNormalizedValue()
        {
            var strategy = new BoolParameterStrategy();

            BoolCases.Sample(testCase => AssertRoundTrip(
                (control, value, field) => strategy.SetValue(control, value, field),
                (control, field) => strategy.ExtractValue(control, field),
                (field, name) => strategy.CreateControl(field, name),
                testCase.FieldType, testCase.Value), threads: 1);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void StringStrategy_SetThenExtract_YieldsTheNormalizedValue()
        {
            var strategy = new StringParameterStrategy();

            TextCases.Sample(value => AssertRoundTrip(
                (control, v, field) => strategy.SetValue(control, v, field),
                (control, field) => strategy.ExtractValue(control, field),
                (field, name) => strategy.CreateControl(field, name),
                typeof(string), value), threads: 1);
        }

        /// <summary>
        /// The date strategy's law is plain identity for any value it can hold: the composed
        /// date-plus-time-of-day is the value that went in, in both the DateTime and DateTimeOffset
        /// shapes. The null branch is deliberately NOT sampled - it resolves to "now", which is
        /// clock-dependent and therefore has no fixed oracle; the two example tests that pin it stay.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void DateStrategy_SetThenExtract_PreservesTheValue()
        {
            var strategy = new DateTimeParameterStrategy();

            DateCases.Sample(testCase =>
            {
                var field = new FieldMetaData("when", testCase.FieldType, [], "A date field");
                var control = strategy.CreateControl(field, "DateControl");

                strategy.SetValue(control, testCase.Value, field);
                object? actual = strategy.ExtractValue(control, field);

                string what = $"{testCase.FieldType.Name} <- {testCase.Value:O}";
                Assert.That(actual, Is.EqualTo(testCase.Value), what);
                Assert.That(actual, Is.Not.Null, what);
                Assert.That(actual!.GetType(), Is.EqualTo(testCase.Value.GetType()), what + " (boxed type)");
            }, threads: 1);
        }

        /// <summary>
        /// Asserts the round-trip law for one sample. The boxed <b>type</b> is asserted alongside the
        /// value on purpose: NUnit's <c>Is.EqualTo</c> compares numerics across types, so a strategy
        /// returning <c>(int)0</c> where the field is a <c>byte</c> would otherwise pass silently.
        /// </summary>
        private static void AssertRoundTrip(
            Action<Avalonia.Controls.Control, object?, FieldMetaData> setValue,
            Func<Avalonia.Controls.Control, FieldMetaData, object?> extractValue,
            Func<FieldMetaData, string, Avalonia.Controls.Control> createControl,
            Type fieldType,
            object? value)
        {
            var field = new FieldMetaData("testParam", fieldType, [], "Test description");
            var control = createControl(field, "TestControl");

            setValue(control, value, field);
            object? actual = extractValue(control, field);
            object? expected = ParameterStrategyNormalization.Normalize(value, field);

            string what = $"{fieldType.Name} <- {value?.ToString() ?? "null"}";
            Assert.That(actual, Is.EqualTo(expected), what);
            if (expected != null)
            {
                Assert.That(actual, Is.Not.Null, what);
                Assert.That(actual!.GetType(), Is.EqualTo(expected.GetType()), what + " (boxed type)");
            }
            else
            {
                Assert.That(actual, Is.Null, what);
            }
        }
    }
}
