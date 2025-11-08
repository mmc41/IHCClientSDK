using System;
using Ihc;
using Ihc.App;
using IhcLab;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Safe_Unit_Tests.ParameterControlStrategies;

/// <summary>
/// Integration Tests: Verify registry behavior and strategy pattern implementation.
/// UI-dependent tests are in safe_lab_tests. These tests focus on non-UI aspects.
/// </summary>
[TestFixture]
public class Phase4IntegrationTests
{
    #region Registry Integration Tests

    [Test]
    public void Registry_HasCorrectNumberOfStrategies_AfterPhase4()
    {
        // Arrange & Act
        var registry = ParameterControlRegistry.Instance;

        // Assert
        // Phase 1: 3 strategies (String, Bool, Numeric)
        // Phase 2: 5 strategies (File, ResourceValue, Enum, DateTime, ComplexType)
        // Phase 3: 1 strategy (Array)
        // Total: 9 strategies (no new strategies in Phase 4)
        Assert.That(registry.StrategyCount, Is.EqualTo(9));
    }

    [Test]
    public void Registry_CanHandleAllBasicTypes()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var types = new[]
        {
            typeof(string),
            typeof(bool),
            typeof(int),
            typeof(float),
            typeof(DayOfWeek),
            typeof(DateTime),
            typeof(int[]),
        };

        // Act & Assert
        foreach (var type in types)
        {
            var field = new FieldMetaData("test", type, [], "Test field");
            Assert.That(registry.CanHandle(field), Is.True, $"Registry should handle {type.Name}");
        }
    }

    [Test]
    public void Registry_GetStrategy_ReturnsCorrectStrategyType()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;

        // Act & Assert
        var stringField = new FieldMetaData("test", typeof(string), [], "");
        Assert.That(registry.GetStrategy(stringField), Is.InstanceOf<StringParameterStrategy>());

        var boolField = new FieldMetaData("test", typeof(bool), [], "");
        Assert.That(registry.GetStrategy(boolField), Is.InstanceOf<BoolParameterStrategy>());

        var intField = new FieldMetaData("test", typeof(int), [], "");
        Assert.That(registry.GetStrategy(intField), Is.InstanceOf<NumericParameterStrategy>());

        var enumField = new FieldMetaData("test", typeof(DayOfWeek), [], "");
        Assert.That(registry.GetStrategy(enumField), Is.InstanceOf<EnumParameterStrategy>());

        var dateField = new FieldMetaData("test", typeof(DateTime), [], "");
        Assert.That(registry.GetStrategy(dateField), Is.InstanceOf<DateTimeParameterStrategy>());

        var arrayField = new FieldMetaData("test", typeof(int[]), [new FieldMetaData("element", typeof(int), [], "")], "");
        Assert.That(registry.GetStrategy(arrayField), Is.InstanceOf<ArrayParameterStrategy>());
    }

    #endregion

    #region Strategy Pattern Method Tests

    [Test]
    public void OperationSupport_HasStrategyPatternMethods()
    {
        // Assert - Verify strategy pattern methods exist via reflection
        var type = typeof(OperationSupport);

        var setUp = type.GetMethod("SetUpParameterControls");
        Assert.That(setUp, Is.Not.Null, "SetUpParameterControls should exist");

        var addField = type.GetMethod("AddFieldControls");
        Assert.That(addField, Is.Not.Null, "AddFieldControls should exist");

        var getValue = type.GetMethod("GetFieldValue");
        Assert.That(getValue, Is.Not.Null, "GetFieldValue should exist");

        var getValues = type.GetMethod("GetParameterValues");
        Assert.That(getValues, Is.Not.Null, "GetParameterValues should exist");
    }

    #endregion
}
