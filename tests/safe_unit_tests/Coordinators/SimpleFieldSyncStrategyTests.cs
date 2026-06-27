using System;
using Ihc;
using Ihc.App;

namespace Safe_Unit_Tests.Coordinators;

/// <summary>
/// Tests for <see cref="SimpleFieldSyncStrategy.CanHandle"/>, the gate that decides whether a field is
/// restored as a single leaf control during service-&gt;GUI synchronization. DateTime / DateTimeOffset map
/// to one DatePicker, so they must be treated as leaves rather than being mis-routed to the complex,
/// sub-field-recursing strategy (which would recurse into a date's non-existent sub-controls, or skip a
/// date sub-field entirely).
/// </summary>
[TestFixture]
public class SimpleFieldSyncStrategyTests
{
    private SimpleFieldSyncStrategy _strategy;

    [SetUp]
    public void SetUp()
    {
        _strategy = new SimpleFieldSyncStrategy();
    }

    [TestCase(typeof(DateTime))]
    [TestCase(typeof(DateTimeOffset))]
    public void CanHandle_DateTypes_ReturnsTrue(Type dateType)
    {
        // Arrange
        var field = new FieldMetaData("when", dateType, [], "A date field");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.True);
    }

    [TestCase(typeof(string))]
    [TestCase(typeof(int))]
    [TestCase(typeof(bool))]
    [TestCase(typeof(DayOfWeek))]
    public void CanHandle_SimpleTypes_ReturnsTrue(Type simpleType)
    {
        // Arrange - existing behavior: primitives, strings and enums are leaves.
        var field = new FieldMetaData("value", simpleType, [], "A simple field");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_ComplexRecord_ReturnsFalse()
    {
        // Arrange - a non-leaf record must stay with the complex strategy (guards against over-broadening).
        var field = new FieldMetaData("settings", typeof(NetworkSettings), [], "A complex field");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.False);
    }
}
