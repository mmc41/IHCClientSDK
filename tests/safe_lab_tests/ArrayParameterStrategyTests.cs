using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
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
        public void CanHandle_CollectionWithNoElementMetadata_ReturnsFalse()
        {
            // US-A7/US-D: a collection field the one-level metadata expansion left with no element sub-field cannot
            // be rendered, so the strategy must NOT claim it - that is what lets the filter exclude the operation
            // (rather than let it crash at selection with "No strategy found").
            var field = new FieldMetaData("testArray", typeof(int[]), [], "Test array");

            Assert.That(strategy.CanHandle(field), Is.False);
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

        #region Generic collection support (US-A1)

        private static FieldMetaData CollectionField() =>
            new FieldMetaData("ids", typeof(IReadOnlyList<int>),
                [new FieldMetaData("", typeof(int), [], "")], "Resource ids");

        [Test]
        public void CanHandle_GenericCollection_ReturnsTrue()
        {
            Assert.That(strategy.CanHandle(CollectionField()), Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_GenericCollection_RoundTripsValues()
        {
            // Arrange
            var field = CollectionField();
            var control = strategy.CreateControl(field, "0");

            // Act - set then extract (materialized int[] is assignable to IReadOnlyList<int>)
            strategy.SetValue(control, new int[] { 5, 6, 7 }, field);
            var value = strategy.ExtractValue(control, field) as int[];

            // Assert
            Assert.That(value, Is.EqualTo(new[] { 5, 6, 7 }));
        }

        #endregion

        #region Two-way sync wiring (US-A2)

        private static Button FindAddButton(StackPanel mainPanel) =>
            mainPanel.Children.OfType<StackPanel>().First().Children.OfType<Button>().First();

        private static StackPanel ItemsPanelOf(StackPanel mainPanel) =>
            mainPanel.Children.OfType<StackPanel>().First(p => p.Name == $"{mainPanel.Name}.Items");

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SubscribeToValueChanged_AddButton_RaisesHandlerAndGrowsArray()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var control = (StackPanel)strategy.CreateControl(field, "0");

            int raised = 0;
            strategy.SubscribeToValueChanged(control, (s, e) => raised++);

            // Act - click the Add button
            FindAddButton(control).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Assert
            Assert.That(raised, Is.GreaterThanOrEqualTo(1), "Add must raise the parameter-changed handler");
            var value = (int[])strategy.ExtractValue(control, field)!;
            Assert.That(value.Length, Is.EqualTo(1));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SubscribeToValueChanged_EditElement_RaisesHandlerAndReExtracts()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var control = (StackPanel)strategy.CreateControl(field, "0");
            strategy.SetValue(control, new int[] { 1, 2 }, field);

            int raised = 0;
            strategy.SubscribeToValueChanged(control, (s, e) => raised++);

            // Act - edit the first element
            var firstNumeric = ItemsPanelOf(control).Children.OfType<StackPanel>().First()
                .Children.OfType<NumericUpDown>().First();
            firstNumeric.Value = 9m;

            // Assert
            Assert.That(raised, Is.GreaterThanOrEqualTo(1), "Editing an element must raise the handler");
            var value = (int[])strategy.ExtractValue(control, field)!;
            Assert.That(value, Is.EqualTo(new[] { 9, 2 }));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SubscribeToValueChanged_RemoveButton_RaisesHandlerAndShrinksArray()
        {
            // Arrange
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var control = (StackPanel)strategy.CreateControl(field, "0");
            strategy.SetValue(control, new int[] { 1, 2, 3 }, field);

            int raised = 0;
            strategy.SubscribeToValueChanged(control, (s, e) => raised++);

            // Act - remove the second item ([1] = value 2)
            var secondContainer = ItemsPanelOf(control).Children.OfType<StackPanel>().ElementAt(1);
            secondContainer.Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Assert
            Assert.That(raised, Is.GreaterThanOrEqualTo(1), "Removing an element must raise the handler");
            var value = (int[])strategy.ExtractValue(control, field)!;
            Assert.That(value, Is.EqualTo(new[] { 1, 3 }));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_AfterSubscribe_DoesNotRaiseHandler()
        {
            // A service-driven restore must NOT echo back as a user edit (D9): SetValue wires elements only after
            // setting their value, so the restore itself raises nothing.
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var control = (StackPanel)strategy.CreateControl(field, "0");

            int raised = 0;
            strategy.SubscribeToValueChanged(control, (s, e) => raised++);

            // Act
            strategy.SetValue(control, new int[] { 1, 2, 3 }, field);

            // Assert - no echo, AND the restore actually populated the control (not a silent no-op).
            Assert.That(raised, Is.EqualTo(0), "SetValue (restore) must not raise the parameter-changed handler");
            var restored = (int[])strategy.ExtractValue(control, field)!;
            Assert.That(restored, Is.EqualTo(new[] { 1, 2, 3 }), "SetValue (restore) must populate the control");
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_SameCount_ReusesElementControlsAndUpdatesValues()
        {
            // A same-count restore (the GUI->service->GUI round-trip that fires on every element edit, because an
            // array compares by reference and always looks "changed") must update the existing element controls in
            // place, NOT tear them down and recreate them - otherwise the control the user is editing is destroyed
            // mid-keystroke (US-A2 focus preservation).
            var elementField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
            var control = (StackPanel)strategy.CreateControl(field, "0");
            strategy.SetValue(control, new int[] { 1, 2, 3 }, field);

            var before = ItemsPanelOf(control).Children.OfType<StackPanel>()
                .Select(row => row.Children.OfType<NumericUpDown>().First()).ToList();

            // Act - restore a different value of the same length
            strategy.SetValue(control, new int[] { 4, 5, 6 }, field);

            // Assert - the same control instances are reused, and the values are updated
            var after = ItemsPanelOf(control).Children.OfType<StackPanel>()
                .Select(row => row.Children.OfType<NumericUpDown>().First()).ToList();
            Assert.That(after, Has.Count.EqualTo(3));
            for (int i = 0; i < 3; i++)
                Assert.That(ReferenceEquals(before[i], after[i]), Is.True, $"element {i} control must be reused, not recreated");
            Assert.That((int[])strategy.ExtractValue(control, field)!, Is.EqualTo(new[] { 4, 5, 6 }));
        }

        #endregion
    }
}
