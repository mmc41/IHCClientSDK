using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    [TestFixture]
    public class BoolParameterStrategyTests : AvaloniaTestBase
    {
        private BoolParameterStrategy strategy;

        [SetUp]
        public void SetUp()
        {
            strategy = new BoolParameterStrategy();
        }

        [Test]
        public void CanHandle_BoolType_ReturnsTrue()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

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
        public void CanHandle_IntType_ReturnsFalse()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

            // Act
            bool result = strategy.CanHandle(field);

            // Assert
            Assert.That(result, Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_ValidField_ReturnsStackPanelWithRadioButtons()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<StackPanel>());
            Assert.That(result.Name, Is.EqualTo("TestControl"));

            var stackPanel = (StackPanel)result;
            var radioButtons = stackPanel.Children.OfType<RadioButton>().ToList();
            Assert.That(radioButtons, Has.Count.EqualTo(2));
            Assert.That(radioButtons[0].Content, Is.EqualTo("True"));
            Assert.That(radioButtons[1].Content, Is.EqualTo("False"));
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
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test tooltip description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            var tooltip = ToolTip.GetTip(result);
            Assert.That(tooltip, Is.EqualTo("Test tooltip description"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_DefaultValue_IsFalse()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

            // Act
            var result = strategy.CreateControl(field, "TestControl");
            var extractedValue = strategy.ExtractValue(result, field);

            // Assert
            Assert.That(extractedValue, Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_TrueSelected_ReturnsTrue()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
            var result = strategy.CreateControl(field, "TestControl");
            var stackPanel = (StackPanel)result;
            var trueRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "True");
            trueRadio.IsChecked = true;

            // Act
            var value = strategy.ExtractValue(stackPanel, field);

            // Assert
            Assert.That(value, Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_FalseSelected_ReturnsFalse()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
            var result = strategy.CreateControl(field, "TestControl");
            var stackPanel = (StackPanel)result;
            var falseRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "False");
            falseRadio.IsChecked = true;

            // Act
            var value = strategy.ExtractValue(stackPanel, field);

            // Assert
            Assert.That(value, Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_InvalidControl_ThrowsInvalidOperationException()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
            var button = new Button();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => strategy.ExtractValue(button, field));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_True_SelectsTrueRadioButton()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
            var result = strategy.CreateControl(field, "TestControl");
            var stackPanel = (StackPanel)result;

            // Act
            strategy.SetValue(stackPanel, true, field);

            // Assert
            var trueRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "True");
            var falseRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "False");
            Assert.That(trueRadio.IsChecked, Is.True);
            Assert.That(falseRadio.IsChecked, Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_False_SelectsFalseRadioButton()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
            var result = strategy.CreateControl(field, "TestControl");
            var stackPanel = (StackPanel)result;

            // Act
            strategy.SetValue(stackPanel, false, field);

            // Assert
            var trueRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "True");
            var falseRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "False");
            Assert.That(trueRadio.IsChecked, Is.False);
            Assert.That(falseRadio.IsChecked, Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_Null_DefaultsToFalse()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
            var result = strategy.CreateControl(field, "TestControl");
            var stackPanel = (StackPanel)result;

            // Act
            strategy.SetValue(stackPanel, null, field);

            // Assert
            var trueRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "True");
            var falseRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "False");
            Assert.That(trueRadio.IsChecked, Is.False);
            Assert.That(falseRadio.IsChecked, Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_InvalidControl_ThrowsInvalidOperationException()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
            var button = new Button();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => strategy.SetValue(button, true, field));
        }
    }
}
