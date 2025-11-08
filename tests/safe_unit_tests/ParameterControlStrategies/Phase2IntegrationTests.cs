using System;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Safe_Unit_Tests.ParameterControlStrategies;

[TestFixture]
public class Phase2IntegrationTests
{
    [Test]
    public void Registry_HasCorrectNumberOfStrategies()
    {
        // Arrange & Act
        var registry = ParameterControlRegistry.Instance;

        // Assert
        // Phase 1: 3 strategies (String, Bool, Numeric)
        // Phase 2: 5 strategies (File, ResourceValue, Enum, DateTime, ComplexType)
        // Phase 3: 1 strategy (Array)
        // Total: 9 strategies
        Assert.That(registry.StrategyCount, Is.EqualTo(9));
    }

    [Test]
    public void Registry_CanHandleEnumType()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var field = new FieldMetaData("testEnum", typeof(DayOfWeek), [], "Test enum");

        // Act
        bool canHandle = registry.CanHandle(field);
        var strategy = registry.GetStrategy(field);

        // Assert
        Assert.That(canHandle, Is.True);
        Assert.That(strategy, Is.InstanceOf<EnumParameterStrategy>());
    }

    [Test]
    public void Registry_CanHandleDateTimeType()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var field = new FieldMetaData("testDate", typeof(DateTime), [], "Test date");

        // Act
        bool canHandle = registry.CanHandle(field);
        var strategy = registry.GetStrategy(field);

        // Assert
        Assert.That(canHandle, Is.True);
        Assert.That(strategy, Is.InstanceOf<DateTimeParameterStrategy>());
    }

    [Test]
    public void Registry_CanHandleDateTimeOffsetType()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var field = new FieldMetaData("testDateOffset", typeof(DateTimeOffset), [], "Test date offset");

        // Act
        bool canHandle = registry.CanHandle(field);
        var strategy = registry.GetStrategy(field);

        // Assert
        Assert.That(canHandle, Is.True);
        Assert.That(strategy, Is.InstanceOf<DateTimeParameterStrategy>());
    }

    [Test]
    public void Registry_CanHandleResourceValueType()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var field = new FieldMetaData("testResource", typeof(ResourceValue), [], "Test resource");

        // Act
        bool canHandle = registry.CanHandle(field);
        var strategy = registry.GetStrategy(field);

        // Assert
        Assert.That(canHandle, Is.True);
        Assert.That(strategy, Is.InstanceOf<ResourceValueParameterStrategy>());
    }

    [Test]
    public void EnumStrategy_CreatesControlSuccessfully()
    {
        // Arrange
        var strategy = new EnumParameterStrategy();
        var field = new FieldMetaData("dayOfWeek", typeof(DayOfWeek), [], "Day of week");

        // Act
        var result = strategy.CreateControl(field, "TestControl");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Control, Is.Not.Null);
        Assert.That(result.Control.Name, Is.EqualTo("TestControl"));
    }

    [Test]
    public void DateTimeStrategy_CreatesControlSuccessfully()
    {
        // Arrange
        var strategy = new DateTimeParameterStrategy();
        var field = new FieldMetaData("testDate", typeof(DateTime), [], "Test date");

        // Act
        var result = strategy.CreateControl(field, "TestControl");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Control, Is.Not.Null);
        Assert.That(result.Control.Name, Is.EqualTo("TestControl"));
    }

    [Test]
    public void ResourceValueStrategy_CreatesControlSuccessfully()
    {
        // Arrange
        var strategy = new ResourceValueParameterStrategy();
        var field = new FieldMetaData("testResource", typeof(ResourceValue), [], "Test resource");

        // Act
        var result = strategy.CreateControl(field, "TestControl");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Control, Is.Not.Null);
        Assert.That(result.Control.Name, Is.EqualTo("TestControl"));
    }

    [Test]
    public void Phase2Strategies_AllRegisterInCorrectOrder()
    {
        // Arrange
        var registry = ParameterControlRegistry.CreateEmpty();

        // Act - Register in same order as default registry
        registry.Register(new StringParameterStrategy());
        registry.Register(new BoolParameterStrategy());
        registry.Register(new NumericParameterStrategy());
        registry.Register(new FileParameterStrategy());
        registry.Register(new ResourceValueParameterStrategy());
        registry.Register(new EnumParameterStrategy());
        registry.Register(new DateTimeParameterStrategy());
        registry.Register(new ComplexTypeParameterStrategy());

        // Assert
        Assert.That(registry.StrategyCount, Is.EqualTo(8));

        // Verify correct strategy selection (order matters!)
        var enumField = new FieldMetaData("enum", typeof(DayOfWeek), [], "");
        Assert.That(registry.GetStrategy(enumField), Is.InstanceOf<EnumParameterStrategy>());

        var dateField = new FieldMetaData("date", typeof(DateTime), [], "");
        Assert.That(registry.GetStrategy(dateField), Is.InstanceOf<DateTimeParameterStrategy>());

        var resourceField = new FieldMetaData("resource", typeof(ResourceValue), [], "");
        Assert.That(registry.GetStrategy(resourceField), Is.InstanceOf<ResourceValueParameterStrategy>());
    }
}
