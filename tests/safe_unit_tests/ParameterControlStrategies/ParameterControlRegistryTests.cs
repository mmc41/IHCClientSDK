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
            var registry = new ParameterControlRegistry();
            int initialCount = registry.StrategyCount;
            var strategy = new StringParameterStrategy();

            // Act
            registry.Register(strategy);

            // Assert
            Assert.That(registry.StrategyCount, Is.EqualTo(initialCount + 1));
        }

        /// <summary>
        /// The default registry's whole type-to-strategy map, one case per pair, so a mapping that regresses
        /// fails under its own name rather than as one unmessaged assert among several. Both faces are asserted
        /// together because they are one decision asked twice: <c>CanHandle</c> says some strategy claims the
        /// field, and <c>GetStrategy</c> returns the one that claimed it. Selection is first-match over the
        /// registration order, so a pair moving here means a strategy started claiming a type that is not its own.
        /// </summary>
        /// <param name="fieldType">The field type the metadata layer emits.</param>
        /// <param name="expectedStrategy">The strategy the default registry must select for it.</param>
        [TestCase(typeof(string), typeof(StringParameterStrategy))]
        [TestCase(typeof(bool), typeof(BoolParameterStrategy))]
        [TestCase(typeof(int), typeof(NumericParameterStrategy))]
        [TestCase(typeof(float), typeof(NumericParameterStrategy))]
        [TestCase(typeof(DayOfWeek), typeof(EnumParameterStrategy))]
        [TestCase(typeof(DateTime), typeof(DateTimeParameterStrategy))]
        [TestCase(typeof(DateTimeOffset), typeof(DateTimeParameterStrategy))]
        [TestCase(typeof(ResourceValue), typeof(ResourceValueParameterStrategy))]
        [TestCase(typeof(int[]), typeof(ArrayParameterStrategy))]
        public void GetStrategy_SupportedType_ReturnsItsStrategy(Type fieldType, Type expectedStrategy)
        {
            // Arrange
            var registry = ParameterControlRegistry.Instance;
            var field = new FieldMetaData("testParam", fieldType, SubFieldsFor(fieldType), "Test description");

            // Act & Assert
            Assert.That(registry.CanHandle(field), Is.True, $"Registry should handle {fieldType.Name}");
            Assert.That(registry.GetStrategy(field), Is.InstanceOf(expectedStrategy));
        }

        /// <summary>
        /// An array or collection field carries its element type as a sub-field, the way the metadata layer emits
        /// it; the collection strategy needs that element metadata to claim the field at all. A scalar has none.
        /// </summary>
        private static FieldMetaData[] SubFieldsFor(Type fieldType) =>
            fieldType.IsArray ? [new FieldMetaData("element", fieldType.GetElementType()!, [], "")] : [];

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
        private sealed class TestCatchAllStrategy : IParameterControlStrategy
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
