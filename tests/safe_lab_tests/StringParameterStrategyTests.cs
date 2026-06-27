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
    public class StringParameterStrategyTests : AvaloniaTestBase
    {
        private StringParameterStrategy strategy;

        [SetUp]
        public void SetUp()
        {
            strategy = new StringParameterStrategy();
        }

        [Test]
        public void CanHandle_StringType_ReturnsTrue()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

            // Act
            bool result = strategy.CanHandle(field);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanHandle_IntType_ReturnsFalse()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

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
        public void CreateControl_ValidField_ReturnsTextBox()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<TextBox>());
            Assert.That(result.Name, Is.EqualTo("TestControl"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_InvalidField_ThrowsNotSupportedException()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => strategy.CreateControl(field, "TestControl"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_WithDescription_SetsTooltip()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test tooltip description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            var tooltip = ToolTip.GetTip(result);
            Assert.That(tooltip, Is.EqualTo("Test tooltip description"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_TextBoxWithValue_ReturnsText()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
            var textBox = new TextBox { Text = "Hello World" };

            // Act
            var value = strategy.ExtractValue(textBox, field);

            // Assert
            Assert.That(value, Is.EqualTo("Hello World"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_TextBoxEmpty_ReturnsEmptyString()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
            var textBox = new TextBox { Text = "" };

            // Act
            var value = strategy.ExtractValue(textBox, field);

            // Assert - an empty field extracts as an empty string, not null, so empty values sync consistently
            // (matching the string parameter default of string.Empty) rather than silently switching to null.
            Assert.That(value, Is.EqualTo(string.Empty));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_InvalidControl_ThrowsInvalidOperationException()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
            var button = new Button();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => strategy.ExtractValue(button, field));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_ValidValue_SetsTextBoxText()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
            var textBox = new TextBox();

            // Act
            strategy.SetValue(textBox, "Test Value", field);

            // Assert
            Assert.That(textBox.Text, Is.EqualTo("Test Value"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_NullValue_SetsEmptyString()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
            var textBox = new TextBox { Text = "Initial Value" };

            // Act
            strategy.SetValue(textBox, null, field);

            // Assert
            Assert.That(textBox.Text, Is.EqualTo(string.Empty));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_InvalidControl_ThrowsInvalidOperationException()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
            var button = new Button();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => strategy.SetValue(button, "Test", field));
        }
    }
}
