using System;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    [TestFixture]
    public class NumericParameterStrategyTests : AvaloniaTestBase
    {
        private NumericParameterStrategy strategy;

        [SetUp]
        public void SetUp()
        {
            strategy = new NumericParameterStrategy();
        }

        [TestCase(typeof(byte))]
        [TestCase(typeof(sbyte))]
        [TestCase(typeof(short))]
        [TestCase(typeof(ushort))]
        [TestCase(typeof(int))]
        [TestCase(typeof(uint))]
        [TestCase(typeof(long))]
        [TestCase(typeof(ulong))]
        [TestCase(typeof(float))]
        [TestCase(typeof(double))]
        [TestCase(typeof(decimal))]
        public void CanHandle_NumericTypes_ReturnsTrue(Type numericType)
        {
            // Arrange
            var field = new FieldMetaData("testParam", numericType, [], "Test description");

            // Act
            bool result = strategy.CanHandle(field);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanHandle_StringType_ReturnsFalse()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

            // Act
            bool result = strategy.CanHandle(field);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void CanHandle_BoolType_ReturnsFalse()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

            // Act
            bool result = strategy.CanHandle(field);

            // Assert
            Assert.That(result, Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_ValidField_ReturnsNumericUpDown()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<NumericUpDown>());
            Assert.That(result.Name, Is.EqualTo("TestControl"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_InvalidField_ThrowsNotSupportedException()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => strategy.CreateControl(field, "TestControl"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_WithDescription_SetsTooltip()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test tooltip description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            var tooltip = ToolTip.GetTip(result);
            Assert.That(tooltip, Is.EqualTo("Test tooltip description"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_IntType_SetsCorrectMinMax()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");
            var numericUpDown = (NumericUpDown)result;

            // Assert
            Assert.That(numericUpDown.Minimum, Is.EqualTo(int.MinValue));
            Assert.That(numericUpDown.Maximum, Is.EqualTo(int.MaxValue));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_ByteType_SetsCorrectMinMax()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(byte), [], "Test description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");
            var numericUpDown = (NumericUpDown)result;

            // Assert
            Assert.That(numericUpDown.Minimum, Is.EqualTo(byte.MinValue));
            Assert.That(numericUpDown.Maximum, Is.EqualTo(byte.MaxValue));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_IntValue_ReturnsInt()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
            var numericUpDown = new NumericUpDown { Value = 42 };

            // Act
            var value = strategy.ExtractValue(numericUpDown, field);

            // Assert
            Assert.That(value, Is.InstanceOf<int>());
            Assert.That(value, Is.EqualTo(42));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_FloatValue_ReturnsFloat()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(float), [], "Test description");
            var numericUpDown = new NumericUpDown { Value = 3.14m };

            // Act
            var value = strategy.ExtractValue(numericUpDown, field);

            // Assert
            Assert.That(value, Is.InstanceOf<float>());
            Assert.That((float)value!, Is.EqualTo(3.14f).Within(0.01));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_DoubleValue_ReturnsDouble()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(double), [], "Test description");
            var numericUpDown = new NumericUpDown { Value = 2.718m };

            // Act
            var value = strategy.ExtractValue(numericUpDown, field);

            // Assert
            Assert.That(value, Is.InstanceOf<double>());
            Assert.That((double)value!, Is.EqualTo(2.718).Within(0.001));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_NullValue_ReturnsDefaultValue()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
            var numericUpDown = new NumericUpDown { Value = null };

            // Act
            var value = strategy.ExtractValue(numericUpDown, field);

            // Assert
            Assert.That(value, Is.EqualTo(0));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_InvalidControl_ThrowsInvalidOperationException()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
            var button = new Button();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => strategy.ExtractValue(button, field));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_IntValue_SetsNumericUpDownValue()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
            var numericUpDown = new NumericUpDown();

            // Act
            strategy.SetValue(numericUpDown, 100, field);

            // Assert
            Assert.That(numericUpDown.Value, Is.EqualTo(100));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_FloatValue_SetsNumericUpDownValue()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(float), [], "Test description");
            var numericUpDown = new NumericUpDown();

            // Act
            strategy.SetValue(numericUpDown, 1.5f, field);

            // Assert
            Assert.That(numericUpDown.Value, Is.EqualTo(1.5m));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_NullValue_SetsDefaultValue()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
            var numericUpDown = new NumericUpDown { Value = 42 };

            // Act
            strategy.SetValue(numericUpDown, null, field);

            // Assert
            Assert.That(numericUpDown.Value, Is.EqualTo(0));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_InvalidControl_ThrowsInvalidOperationException()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
            var button = new Button();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => strategy.SetValue(button, 42, field));
        }

        // Data-driven over the integer types, but kept as a single [AvaloniaTest] that loops internally:
        // control construction must run on the Avalonia UI thread, and [AvaloniaTheory] (the data-driven
        // headless attribute) is incompatible with the NUnit version in use here.
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_VariousIntegerTypes_ReturnsCorrectType()
        {
            var cases = new (Type NumericType, object ExpectedValue)[]
            {
                (typeof(byte), (byte)250),
                (typeof(sbyte), (sbyte)-100),
                (typeof(short), (short)-30000),
                (typeof(ushort), (ushort)60000),
                (typeof(int), -123456),
                (typeof(uint), 123456u),
                (typeof(long), -9876543210L),
                (typeof(ulong), 9876543210UL),
            };

            foreach (var (numericType, expectedValue) in cases)
            {
                // Arrange
                var field = new FieldMetaData("testParam", numericType, [], "Test description");
                var numericUpDown = new NumericUpDown { Value = Convert.ToDecimal(expectedValue) };

                // Act
                var value = strategy.ExtractValue(numericUpDown, field);

                // Assert
                Assert.That(value, Is.InstanceOf(numericType));
                Assert.That(value, Is.EqualTo(expectedValue));
            }
        }
    }
}
