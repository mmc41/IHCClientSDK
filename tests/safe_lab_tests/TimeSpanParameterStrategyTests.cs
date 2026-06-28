using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Ihc;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// US-A5: a TimeSpan parameter renders a single hh:mm:ss text box (D2), parsed via TimeSpan.Parse, with a
    /// visible invalid-input message. Nullable TimeSpan? extracts null when empty (D3).
    /// </summary>
    [TestFixture]
    public class TimeSpanParameterStrategyTests : AvaloniaTestBase
    {
        private TimeSpanParameterStrategy strategy;

        [SetUp]
        public void SetUp() => strategy = new TimeSpanParameterStrategy();

        private static TextBox InputBox(Control control) =>
            ((StackPanel)control).Children.OfType<TextBox>().First();

        private static TextBlock ErrorBlock(Control control) =>
            ((StackPanel)control).Children.OfType<TextBlock>().First();

        [Test]
        public void CanHandle_TimeSpan_ReturnsTrue()
        {
            Assert.That(strategy.CanHandle(new FieldMetaData("d", typeof(TimeSpan), [], "")), Is.True);
        }

        [Test]
        public void CanHandle_NullableTimeSpan_ReturnsTrue()
        {
            Assert.That(strategy.CanHandle(new FieldMetaData("d", typeof(TimeSpan?), [], "")), Is.True);
        }

        [Test]
        public void CanHandle_Int_ReturnsFalse()
        {
            Assert.That(strategy.CanHandle(new FieldMetaData("d", typeof(int), [], "")), Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_HasHhMmSsWatermark()
        {
            var control = strategy.CreateControl(new FieldMetaData("d", typeof(TimeSpan), [], ""), "0");
            Assert.That(InputBox(control).Watermark, Is.EqualTo("hh:mm:ss"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_ValidTime_ReturnsTimeSpan()
        {
            // Arrange
            var field = new FieldMetaData("d", typeof(TimeSpan), [], "");
            var control = strategy.CreateControl(field, "0");
            InputBox(control).Text = "01:30:00";

            // Act
            var value = strategy.ExtractValue(control, field);

            // Assert
            Assert.That(value, Is.EqualTo(new TimeSpan(1, 30, 0)));
            Assert.That(ErrorBlock(control).IsVisible, Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_InvalidText_ShowsErrorAndThrows()
        {
            // Arrange
            var field = new FieldMetaData("d", typeof(TimeSpan), [], "");
            var control = strategy.CreateControl(field, "0");
            InputBox(control).Text = "not-a-time";

            // Act & Assert - no wrong value extracted; error message visible
            Assert.Throws<FormatException>(() => strategy.ExtractValue(control, field));
            Assert.That(ErrorBlock(control).IsVisible, Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_EmptyNullable_ReturnsNull()
        {
            // Arrange - nullable TimeSpan? starts empty
            var field = new FieldMetaData("d", typeof(TimeSpan?), [], "");
            var control = strategy.CreateControl(field, "0");

            // Act
            var value = strategy.ExtractValue(control, field);

            // Assert
            Assert.That(value, Is.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void RoundTrip_SetValue_DisplaysHhMmSsAndReExtractsEqual()
        {
            // Arrange
            var field = new FieldMetaData("d", typeof(TimeSpan), [], "");
            var control = strategy.CreateControl(field, "0");
            var stored = new TimeSpan(2, 15, 30);

            // Act
            strategy.SetValue(control, stored, field);
            var displayed = InputBox(control).Text;
            var value = strategy.ExtractValue(control, field);

            // Assert
            Assert.That(displayed, Is.EqualTo("02:15:30"));
            Assert.That(value, Is.EqualTo(stored));
        }
    }
}
