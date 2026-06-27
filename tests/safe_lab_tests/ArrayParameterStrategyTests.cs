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
    public class ArrayParameterStrategyTests : AvaloniaTestBase
    {
        private ArrayParameterStrategy strategy;

        [SetUp]
        public void SetUp()
        {
            strategy = new ArrayParameterStrategy();
        }

        [Test]
        public void CanHandle_ArrayType_ReturnsTrue()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");

            // Act
            bool result = strategy.CanHandle(field);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanHandle_NonArrayType_ReturnsFalse()
        {
            // Arrange
            var field = new FieldMetaData("testParam", typeof(int), [], "Test parameter");

            // Act
            bool result = strategy.CanHandle(field);

            // Assert
            Assert.That(result, Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_ValidArrayField_ReturnsStackPanel()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<StackPanel>());
            Assert.That(result.Name, Is.EqualTo("TestControl"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_HasAddButton()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");

            // Act
            var result = strategy.CreateControl(field, "TestControl");
            var mainPanel = (StackPanel)result;

            // Assert
            var headerPanel = mainPanel.Children.OfType<StackPanel>().FirstOrDefault();
            Assert.That(headerPanel, Is.Not.Null);

            var addButton = headerPanel!.Children.OfType<Button>().FirstOrDefault();
            Assert.That(addButton, Is.Not.Null);
            Assert.That(addButton!.Content, Is.EqualTo("+ Add"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_HasItemsPanel()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");

            // Act
            var result = strategy.CreateControl(field, "TestControl");
            var mainPanel = (StackPanel)result;

            // Assert
            var itemsPanel = mainPanel.Children
                .OfType<StackPanel>()
                .FirstOrDefault(p => p.Name == "TestControl.Items");
            Assert.That(itemsPanel, Is.Not.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_InitiallyShowsZeroItems()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("numbers", typeof(int[]), [elementField], "Number array");

            // Act
            var result = strategy.CreateControl(field, "TestControl");
            var mainPanel = (StackPanel)result;

            // Assert
            var headerPanel = mainPanel.Children.OfType<StackPanel>().FirstOrDefault();
            var label = headerPanel?.Children.OfType<TextBlock>().FirstOrDefault();
            Assert.That(label, Is.Not.Null);
            Assert.That(label!.Text, Does.Contain("0 item"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_EmptyArray_ReturnsEmptyArray()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var result = strategy.CreateControl(field, "TestControl");

            // Act
            var value = strategy.ExtractValue(result, field);

            // Assert
            Assert.That(value, Is.InstanceOf<int[]>());
            var array = (int[])value!;
            Assert.That(array.Length, Is.EqualTo(0));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_IntArray_CreatesControls()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var result = strategy.CreateControl(field, "TestControl");
            var arrayValue = new int[] { 1, 2, 3 };

            // Act
            strategy.SetValue(result, arrayValue, field);

            // Assert
            var mainPanel = (StackPanel)result;
            var itemsPanel = mainPanel.Children
                .OfType<StackPanel>()
                .FirstOrDefault(p => p.Name == "TestControl.Items");

            Assert.That(itemsPanel, Is.Not.Null);
            Assert.That(itemsPanel!.Children.Count, Is.EqualTo(3));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_UpdatesItemCount()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("numbers", typeof(int[]), [elementField], "Number array");
            var result = strategy.CreateControl(field, "TestControl");
            var arrayValue = new int[] { 10, 20 };

            // Act
            strategy.SetValue(result, arrayValue, field);

            // Assert
            var mainPanel = (StackPanel)result;
            var headerPanel = mainPanel.Children.OfType<StackPanel>().FirstOrDefault();
            var label = headerPanel?.Children.OfType<TextBlock>().FirstOrDefault();

            Assert.That(label, Is.Not.Null);
            Assert.That(label!.Text, Does.Contain("2 items"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_NullValue_ClearsItems()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var result = strategy.CreateControl(field, "TestControl");

            // First set some values
            strategy.SetValue(result, new int[] { 1, 2, 3 }, field);

            // Act - set to null
            strategy.SetValue(result, null, field);

            // Assert
            var mainPanel = (StackPanel)result;
            var itemsPanel = mainPanel.Children
                .OfType<StackPanel>()
                .FirstOrDefault(p => p.Name == "TestControl.Items");

            Assert.That(itemsPanel!.Children.Count, Is.EqualTo(0));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_StringArray_CreatesTextBoxControls()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(string), [], "");
            var field = new FieldMetaData("testArray", typeof(string[]), [elementField], "String array");
            var result = strategy.CreateControl(field, "TestControl");
            var arrayValue = new string[] { "hello", "world" };

            // Act
            strategy.SetValue(result, arrayValue, field);

            // Assert
            var mainPanel = (StackPanel)result;
            var itemsPanel = mainPanel.Children
                .OfType<StackPanel>()
                .FirstOrDefault(p => p.Name == "TestControl.Items");

            Assert.That(itemsPanel!.Children.Count, Is.EqualTo(2));

            // Verify first item contains a TextBox
            var firstItemContainer = itemsPanel.Children[0] as StackPanel;
            Assert.That(firstItemContainer, Is.Not.Null);

            var textBox = firstItemContainer!.Children.OfType<TextBox>().FirstOrDefault();
            Assert.That(textBox, Is.Not.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_BoolArray_CreatesRadioButtonControls()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(bool), [], "");
            var field = new FieldMetaData("testArray", typeof(bool[]), [elementField], "Bool array");
            var result = strategy.CreateControl(field, "TestControl");
            var arrayValue = new bool[] { true, false };

            // Act
            strategy.SetValue(result, arrayValue, field);

            // Assert
            var mainPanel = (StackPanel)result;
            var itemsPanel = mainPanel.Children
                .OfType<StackPanel>()
                .FirstOrDefault(p => p.Name == "TestControl.Items");

            Assert.That(itemsPanel!.Children.Count, Is.EqualTo(2));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_AfterSetValue_ReturnsCorrectArray()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var result = strategy.CreateControl(field, "TestControl");
            var originalArray = new int[] { 5, 10, 15 };

            // Act
            strategy.SetValue(result, originalArray, field);
            var extractedValue = strategy.ExtractValue(result, field);

            // Assert
            Assert.That(extractedValue, Is.InstanceOf<int[]>());
            var extractedArray = (int[])extractedValue!;
            Assert.That(extractedArray, Is.EqualTo(originalArray));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_NoSubTypes_ThrowsInvalidOperationException()
        {
            // Arrange
            var field = new FieldMetaData("testArray", typeof(int[]), [], "Test array");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                strategy.CreateControl(field, "TestControl"));

            Assert.That(ex!.Message, Does.Contain("has no SubTypes"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_InvalidControl_ThrowsInvalidOperationException()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var button = new Button();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                strategy.ExtractValue(button, field));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_InvalidControl_ThrowsInvalidOperationException()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var button = new Button();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                strategy.SetValue(button, new int[] { 1, 2, 3 }, field));
        }
    }
}
