using System;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// Pure (non-UI) tests for <see cref="ParameterControlRegistry"/>: singleton behaviour, registration,
    /// chain-of-responsibility selection, and the composition of the default strategy set. Control
    /// construction for the individual strategies lives in the headless Avalonia test project.
    /// </summary>
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
        public void Registry_HasExpectedDefaultStrategyCount()
        {
            // Arrange & Act
            var registry = ParameterControlRegistry.Instance;

            // Assert
            // Scalar: 3 (String, Bool, Numeric)
            // Specialized: 6 (File, ResourceValue, Enum, DateTime, TimeSpan, Array)
            // Catch-all: 1 (ComplexType)
            // Total: 10 strategies
            Assert.That(registry.StrategyCount, Is.EqualTo(10));
        }

        [Test]
        public void Register_ValidStrategy_IncreasesCount()
        {
            // Arrange
            var registry = new ParameterControlRegistry();
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
            var registry = new ParameterControlRegistry();

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

        [Test]
        public void Registry_CanHandleEnumType()
        {
            // Arrange
            var registry = ParameterControlRegistry.Instance;
            var field = new FieldMetaData("testEnum", typeof(DayOfWeek), [], "Test enum");

            // Act & Assert
            Assert.That(registry.CanHandle(field), Is.True);
            Assert.That(registry.GetStrategy(field), Is.InstanceOf<EnumParameterStrategy>());
        }

        [Test]
        public void Registry_CanHandleDateTimeType()
        {
            // Arrange
            var registry = ParameterControlRegistry.Instance;
            var field = new FieldMetaData("testDate", typeof(DateTime), [], "Test date");

            // Act & Assert
            Assert.That(registry.CanHandle(field), Is.True);
            Assert.That(registry.GetStrategy(field), Is.InstanceOf<DateTimeParameterStrategy>());
        }

        [Test]
        public void Registry_CanHandleDateTimeOffsetType()
        {
            // Arrange
            var registry = ParameterControlRegistry.Instance;
            var field = new FieldMetaData("testDateOffset", typeof(DateTimeOffset), [], "Test date offset");

            // Act & Assert
            Assert.That(registry.CanHandle(field), Is.True);
            Assert.That(registry.GetStrategy(field), Is.InstanceOf<DateTimeParameterStrategy>());
        }

        [Test]
        public void Registry_CanHandleResourceValueType()
        {
            // Arrange
            var registry = ParameterControlRegistry.Instance;
            var field = new FieldMetaData("testResource", typeof(ResourceValue), [], "Test resource");

            // Act & Assert
            Assert.That(registry.CanHandle(field), Is.True);
            Assert.That(registry.GetStrategy(field), Is.InstanceOf<ResourceValueParameterStrategy>());
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
                // Array/collection fields carry their element type as a sub-field (as the metadata layer emits);
                // the collection strategy needs that element metadata to claim the field. Scalars have none.
                FieldMetaData[] subTypes = type.IsArray
                    ? [new FieldMetaData("", type.GetElementType()!, [], "")]
                    : [];
                var field = new FieldMetaData("test", type, subTypes, "Test field");
                Assert.That(registry.CanHandle(field), Is.True, $"Registry should handle {type.Name}");
            }
        }

        [Test]
        public void GetStrategy_UnsupportedType_ThrowsNotSupportedException()
        {
            // Arrange
            var registry = new ParameterControlRegistry();
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
            var registry = new ParameterControlRegistry();
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
            var registry = new ParameterControlRegistry();

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
        public void DefaultStrategies_RegisterCatchAllLast()
        {
            // Arrange
            var registry = new ParameterControlRegistry();

            // Act - Register in same order as the default registry (without the Array strategy)
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

            // With the catch-all registered last, specific types still resolve to their own strategy.
            var enumField = new FieldMetaData("enum", typeof(DayOfWeek), [], "");
            Assert.That(registry.GetStrategy(enumField), Is.InstanceOf<EnumParameterStrategy>());

            var dateField = new FieldMetaData("date", typeof(DateTime), [], "");
            Assert.That(registry.GetStrategy(dateField), Is.InstanceOf<DateTimeParameterStrategy>());

            var resourceField = new FieldMetaData("resource", typeof(ResourceValue), [], "");
            Assert.That(registry.GetStrategy(resourceField), Is.InstanceOf<ResourceValueParameterStrategy>());
        }

        [Test]
        public void NewRegistry_IsEmpty()
        {
            // Act
            var registry = new ParameterControlRegistry();

            // Assert
            Assert.That(registry.StrategyCount, Is.EqualTo(0));
        }

        [Test]
        public void StrategyCount_AfterMultipleRegistrations_ReturnsCorrectCount()
        {
            // Arrange
            var registry = new ParameterControlRegistry();
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

            public Avalonia.Controls.Control CreateControl(FieldMetaData field, string controlName)
            {
                throw new NotImplementedException();
            }

            public void SubscribeToValueChanged(Avalonia.Controls.Control control, EventHandler handler)
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

            public FieldMetaData[] GetRenderedSubFields(FieldMetaData field) => System.Array.Empty<FieldMetaData>();
        }
    }
}
