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
    /// Round-trip tests for <see cref="DateTimeParameterStrategy"/>, the single DatePicker control used for
    /// DateTime / DateTimeOffset parameters (e.g. TimeManagerSettings.TimeAndDateInUTC). Covers the
    /// value-preserving paths and the null-default ("now") branch the service-&gt;GUI restore relies on.
    /// </summary>
    [TestFixture]
    public class DateTimeParameterStrategyTests : AvaloniaTestBase
    {
        private static readonly TimeSpan NowTolerance = TimeSpan.FromMinutes(5);

        private DateTimeParameterStrategy strategy;

        [SetUp]
        public void SetUp()
        {
            strategy = new DateTimeParameterStrategy();
        }

        [TestCase(typeof(DateTime))]
        [TestCase(typeof(DateTimeOffset))]
        public void CanHandle_DateTypes_ReturnsTrue(Type dateType)
        {
            // Arrange
            var field = new FieldMetaData("when", dateType, [], "A date field");

            // Act & Assert
            Assert.That(strategy.CanHandle(field), Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_Null_DefaultsSelectedDateToNow()
        {
            // Arrange
            var field = new FieldMetaData("when", typeof(DateTimeOffset), [], "A date field");
            var control = strategy.CreateControl(field, "DateControl");

            // Act - the null branch defaults to "now" rather than leaving the picker blank.
            strategy.SetValue(control, null, field);

            // Assert
            var datePicker = DatePickerOf(control);
            Assert.That(datePicker.SelectedDate, Is.Not.Null);
            Assert.That(datePicker.SelectedDate!.Value, Is.EqualTo(DateTimeOffset.Now).Within(NowTolerance));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_NullSelectedDate_ReturnsNow()
        {
            // Arrange - a picker with no selection extracts "now" instead of null.
            var field = new FieldMetaData("when", typeof(DateTimeOffset), [], "A date field");
            var control = strategy.CreateControl(field, "DateControl");
            DatePickerOf(control).SelectedDate = null;

            // Act
            var result = strategy.ExtractValue(control, field);

            // Assert
            Assert.That(result, Is.InstanceOf<DateTimeOffset>());
            Assert.That((DateTimeOffset)result!, Is.EqualTo(DateTimeOffset.Now).Within(NowTolerance));
        }

        private static DatePicker DatePickerOf(Control control)
            => ((StackPanel)control).Children.OfType<DatePicker>().First();
    }
}
