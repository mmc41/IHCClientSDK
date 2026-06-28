using System;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Ihc;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// US-A4: leaf strategies handle Nullable&lt;T&gt; by unwrapping the underlying type; an empty/unset control
    /// extracts as null (the "empty = null" convention, D3), and bool? uses a three-state checkbox whose
    /// indeterminate state is null.
    /// </summary>
    [TestFixture]
    public class NullableParameterStrategyTests : AvaloniaTestBase
    {
        // ---------- Numeric: int? ----------

        [Test]
        public void Numeric_CanHandle_NullableInt_ReturnsTrue()
        {
            Assert.That(new NumericParameterStrategy().CanHandle(new FieldMetaData("n", typeof(int?), [], "")), Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void Numeric_NullableInt_EmptyExtractsNull()
        {
            var strategy = new NumericParameterStrategy();
            var field = new FieldMetaData("n", typeof(int?), [], "");
            var control = strategy.CreateControl(field, "0");

            Assert.That(strategy.ExtractValue(control, field), Is.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void Numeric_NullableInt_ValueRoundTripsAndBackToNull()
        {
            var strategy = new NumericParameterStrategy();
            var field = new FieldMetaData("n", typeof(int?), [], "");
            var control = strategy.CreateControl(field, "0");

            strategy.SetValue(control, 7, field);
            Assert.That(strategy.ExtractValue(control, field), Is.EqualTo(7));

            strategy.SetValue(control, null, field);
            Assert.That(strategy.ExtractValue(control, field), Is.Null);
        }

        // ---------- Bool: bool? (three-state checkbox) ----------

        [Test]
        public void Bool_CanHandle_NullableBool_ReturnsTrue()
        {
            Assert.That(new BoolParameterStrategy().CanHandle(new FieldMetaData("b", typeof(bool?), [], "")), Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void Bool_NullableBool_ThreeStateCheckBox_IndeterminateExtractsNull()
        {
            var strategy = new BoolParameterStrategy();
            var field = new FieldMetaData("b", typeof(bool?), [], "");
            var control = strategy.CreateControl(field, "0");

            Assert.That(control, Is.InstanceOf<CheckBox>());
            Assert.That(((CheckBox)control).IsThreeState, Is.True);
            Assert.That(strategy.ExtractValue(control, field), Is.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void Bool_NullableBool_ValueRoundTripsAndBackToNull()
        {
            var strategy = new BoolParameterStrategy();
            var field = new FieldMetaData("b", typeof(bool?), [], "");
            var control = strategy.CreateControl(field, "0");

            strategy.SetValue(control, true, field);
            Assert.That(strategy.ExtractValue(control, field), Is.EqualTo(true));

            strategy.SetValue(control, null, field);
            Assert.That(strategy.ExtractValue(control, field), Is.Null);
        }

        // ---------- DateTime: DateTimeOffset? ----------

        [Test]
        public void DateTime_CanHandle_NullableDateTimeOffset_ReturnsTrue()
        {
            Assert.That(new DateTimeParameterStrategy().CanHandle(new FieldMetaData("d", typeof(DateTimeOffset?), [], "")), Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void DateTime_NullableDateTimeOffset_EmptyExtractsNull()
        {
            var strategy = new DateTimeParameterStrategy();
            var field = new FieldMetaData("d", typeof(DateTimeOffset?), [], "");
            var control = strategy.CreateControl(field, "0");

            Assert.That(strategy.ExtractValue(control, field), Is.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void DateTime_NullableDateTimeOffset_SetNullRestoresEmpty()
        {
            var strategy = new DateTimeParameterStrategy();
            var field = new FieldMetaData("d", typeof(DateTimeOffset?), [], "");
            var control = strategy.CreateControl(field, "0");

            // Give it a value, then clear it back to null (unset)
            strategy.SetValue(control, new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero), field);
            Assert.That(strategy.ExtractValue(control, field), Is.Not.Null);

            strategy.SetValue(control, null, field);
            Assert.That(strategy.ExtractValue(control, field), Is.Null);
        }
    }
}
