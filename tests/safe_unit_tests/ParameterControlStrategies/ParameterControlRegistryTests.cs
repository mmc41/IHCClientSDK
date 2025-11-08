using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Safe_Unit_Tests.ParameterControlStrategies;

[TestFixture]
public class ParameterControlRegistryTests
{
    [Test]
    public void Instance_IsNotNull()
    {
        // Act
        var instance = ParameterControlRegistry.Instance;

        // Assert
        Assert.That(instance, Is.Not.Null);
    }

    [Test]
    public void Instance_IsSingleton()
    {
        // Act
        var instance1 = ParameterControlRegistry.Instance;
        var instance2 = ParameterControlRegistry.Instance;

        // Assert
        Assert.That(instance1, Is.SameAs(instance2));
    }

    [Test]
    public void Instance_HasDefaultStrategiesRegistered()
    {
        // Act
        var instance = ParameterControlRegistry.Instance;

        // Assert
        Assert.That(instance.StrategyCount, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void Register_ValidStrategy_IncreasesCount()
    {
        // Arrange
        var registry = ParameterControlRegistry.CreateEmpty();
        int initialCount = registry.StrategyCount;
        var strategy = new StringParameterStrategy();

        // Act
        registry.Register(strategy);

        // Assert
        Assert.That(registry.StrategyCount, Is.EqualTo(initialCount + 1));
    }

    [Test]
    public void Register_NullStrategy_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = ParameterControlRegistry.CreateEmpty();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Test]
    public void GetStrategy_StringField_ReturnsStringStrategy()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

        // Act
        var strategy = registry.GetStrategy(field);

        // Assert
        Assert.That(strategy, Is.InstanceOf<StringParameterStrategy>());
    }

    [Test]
    public void GetStrategy_BoolField_ReturnsBoolStrategy()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

        // Act
        var strategy = registry.GetStrategy(field);

        // Assert
        Assert.That(strategy, Is.InstanceOf<BoolParameterStrategy>());
    }

    [Test]
    public void GetStrategy_IntField_ReturnsNumericStrategy()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

        // Act
        var strategy = registry.GetStrategy(field);

        // Assert
        Assert.That(strategy, Is.InstanceOf<NumericParameterStrategy>());
    }

    [Test]
    public void GetStrategy_UnsupportedType_ThrowsNotSupportedException()
    {
        // Arrange
        var registry = ParameterControlRegistry.CreateEmpty();
        var field = new FieldMetaData("testParam", typeof(object), [], "Test description");

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => registry.GetStrategy(field));
        Assert.That(ex!.Message, Does.Contain("No strategy found"));
        Assert.That(ex.Message, Does.Contain(field.Type.FullName));
    }

    [Test]
    public void GetStrategy_NullField_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.GetStrategy(null!));
    }

    [Test]
    public void CanHandle_SupportedType_ReturnsTrue()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

        // Act
        bool result = registry.CanHandle(field);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_UnsupportedType_ReturnsFalse()
    {
        // Arrange
        var registry = ParameterControlRegistry.CreateEmpty();
        var field = new FieldMetaData("testParam", typeof(object), [], "Test description");

        // Act
        bool result = registry.CanHandle(field);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CanHandle_NullField_ReturnsFalse()
    {
        // Arrange
        var registry = ParameterControlRegistry.Instance;

        // Act
        bool result = registry.CanHandle(null!);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void GetStrategy_RegistrationOrder_ReturnsFirstMatch()
    {
        // Arrange
        var registry = ParameterControlRegistry.CreateEmpty();

        // Create a custom strategy that handles all types
        var catchAllStrategy = new TestCatchAllStrategy();
        var stringStrategy = new StringParameterStrategy();

        // Register catch-all first, then specific
        registry.Register(catchAllStrategy);
        registry.Register(stringStrategy);

        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

        // Act
        var strategy = registry.GetStrategy(field);

        // Assert - Should return the first registered (catch-all)
        Assert.That(strategy, Is.SameAs(catchAllStrategy));
    }

    [Test]
    public void CreateEmpty_ReturnsEmptyRegistry()
    {
        // Act
        var registry = ParameterControlRegistry.CreateEmpty();

        // Assert
        Assert.That(registry.StrategyCount, Is.EqualTo(0));
    }

    [Test]
    public void StrategyCount_AfterMultipleRegistrations_ReturnsCorrectCount()
    {
        // Arrange
        var registry = ParameterControlRegistry.CreateEmpty();
        registry.Register(new StringParameterStrategy());
        registry.Register(new BoolParameterStrategy());
        registry.Register(new NumericParameterStrategy());

        // Act
        int count = registry.StrategyCount;

        // Assert
        Assert.That(count, Is.EqualTo(3));
    }

    // Helper strategy for testing registration order
    private class TestCatchAllStrategy : IParameterControlStrategy
    {
        public bool CanHandle(FieldMetaData field) => true;

        public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
        {
            throw new NotImplementedException();
        }

        public object? ExtractValue(Avalonia.Controls.Control control, FieldMetaData field)
        {
            throw new NotImplementedException();
        }

        public void SetValue(Avalonia.Controls.Control control, object? value, FieldMetaData field)
        {
            throw new NotImplementedException();
        }
    }
}
