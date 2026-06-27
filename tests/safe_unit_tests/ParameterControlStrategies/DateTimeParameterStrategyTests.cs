using System;
using Avalonia.Controls;
using Ihc;
using IhcLab.ParameterControls.Strategies;

namespace Safe_Unit_Tests.ParameterControlStrategies;

/// <summary>
/// Round-trip tests for <see cref="DateTimeParameterStrategy"/>, the single DatePicker control used for
/// DateTime / DateTimeOffset parameters (e.g. TimeManagerSettings.TimeAndDateInUTC). Covers the
/// value-preserving paths and the null-default ("now") branch the service-&gt;GUI restore relies on.
/// </summary>
[TestFixture]
public class DateTimeParameterStrategyTests
{
    private static readonly TimeSpan NowTolerance = TimeSpan.FromMinutes(5);

    private DateTimeParameterStrategy _strategy;

    [SetUp]
    public void SetUp()
    {
        _strategy = new DateTimeParameterStrategy();
    }

    [TestCase(typeof(DateTime))]
    [TestCase(typeof(DateTimeOffset))]
    public void CanHandle_DateTypes_ReturnsTrue(Type dateType)
    {
        // Arrange
        var field = new FieldMetaData("when", dateType, [], "A date field");

        // Act & Assert
        Assert.That(_strategy.CanHandle(field), Is.True);
    }

    [Test]
    public void RoundTrip_DateTimeOffset_PreservesValue()
    {
        // Arrange
        var field = new FieldMetaData("when", typeof(DateTimeOffset), [], "A date field");
        var control = _strategy.CreateControl(field, "DateControl").Control;
        var known = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);

        // Act
        _strategy.SetValue(control, known, field);
        var result = _strategy.ExtractValue(control, field);

        // Assert
        Assert.That(result, Is.EqualTo(known));
    }

    [Test]
    public void RoundTrip_DateTime_PreservesValue()
    {
        // Arrange
        var field = new FieldMetaData("when", typeof(DateTime), [], "A date field");
        var control = _strategy.CreateControl(field, "DateControl").Control;
        var known = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        _strategy.SetValue(control, known, field);
        var result = _strategy.ExtractValue(control, field);

        // Assert
        Assert.That(result, Is.EqualTo(known));
    }

    [Test]
    public void SetValue_Null_DefaultsSelectedDateToNow()
    {
        // Arrange
        var field = new FieldMetaData("when", typeof(DateTimeOffset), [], "A date field");
        var datePicker = (DatePicker)_strategy.CreateControl(field, "DateControl").Control;

        // Act - the null branch defaults to "now" rather than leaving the picker blank.
        _strategy.SetValue(datePicker, null, field);

        // Assert
        Assert.That(datePicker.SelectedDate, Is.Not.Null);
        Assert.That(datePicker.SelectedDate!.Value, Is.EqualTo(DateTimeOffset.Now).Within(NowTolerance));
    }

    [Test]
    public void ExtractValue_NullSelectedDate_ReturnsNow()
    {
        // Arrange - a picker with no selection extracts "now" instead of null.
        var field = new FieldMetaData("when", typeof(DateTimeOffset), [], "A date field");
        var datePicker = new DatePicker { SelectedDate = null };

        // Act
        var result = _strategy.ExtractValue(datePicker, field);

        // Assert
        Assert.That(result, Is.InstanceOf<DateTimeOffset>());
        Assert.That((DateTimeOffset)result!, Is.EqualTo(DateTimeOffset.Now).Within(NowTolerance));
    }
}
