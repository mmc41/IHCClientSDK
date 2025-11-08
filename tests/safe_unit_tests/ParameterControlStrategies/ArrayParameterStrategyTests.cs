using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Safe_Unit_Tests.ParameterControlStrategies;

[TestFixture]
public class ArrayParameterStrategyTests
{
    private ArrayParameterStrategy _strategy;

    [SetUp]
    public void SetUp()
    {
        _strategy = new ArrayParameterStrategy();
    }

    [Test]
    public void CanHandle_ArrayType_ReturnsTrue()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_NonArrayType_ReturnsFalse()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test parameter");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CreateControl_ValidArrayField_ReturnsStackPanel()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Control, Is.InstanceOf<StackPanel>());
        Assert.That(result.Control.Name, Is.EqualTo("TestControl"));
        Assert.That(result.IsComposite, Is.True);
    }

    [Test]
    public void CreateControl_HasAddButton()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");
        var mainPanel = (StackPanel)result.Control;

        // Assert
        var headerPanel = mainPanel.Children.OfType<StackPanel>().FirstOrDefault();
        Assert.That(headerPanel, Is.Not.Null);

        var addButton = headerPanel!.Children.OfType<Button>().FirstOrDefault();
        Assert.That(addButton, Is.Not.Null);
        Assert.That(addButton!.Content, Is.EqualTo("+ Add"));
    }

    [Test]
    public void CreateControl_HasItemsPanel()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");
        var mainPanel = (StackPanel)result.Control;

        // Assert
        var itemsPanel = mainPanel.Children
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == "TestControl.Items");
        Assert.That(itemsPanel, Is.Not.Null);
    }

    [Test]
    public void CreateControl_InitiallyShowsZeroItems()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("numbers", typeof(int[]), [elementField], "Number array");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");
        var mainPanel = (StackPanel)result.Control;

        // Assert
        var headerPanel = mainPanel.Children.OfType<StackPanel>().FirstOrDefault();
        var label = headerPanel?.Children.OfType<TextBlock>().FirstOrDefault();
        Assert.That(label, Is.Not.Null);
        Assert.That(label!.Text, Does.Contain("0 item"));
    }

    [Test]
    public void ExtractValue_EmptyArray_ReturnsEmptyArray()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
        var result = _strategy.CreateControl(field, "TestControl");

        // Act
        var value = _strategy.ExtractValue(result.Control, field);

        // Assert
        Assert.That(value, Is.InstanceOf<int[]>());
        var array = (int[])value!;
        Assert.That(array.Length, Is.EqualTo(0));
    }

    [Test]
    public void SetValue_IntArray_CreatesControls()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
        var result = _strategy.CreateControl(field, "TestControl");
        var arrayValue = new int[] { 1, 2, 3 };

        // Act
        _strategy.SetValue(result.Control, arrayValue, field);

        // Assert
        var mainPanel = (StackPanel)result.Control;
        var itemsPanel = mainPanel.Children
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == "TestControl.Items");

        Assert.That(itemsPanel, Is.Not.Null);
        Assert.That(itemsPanel!.Children.Count, Is.EqualTo(3));
    }

    [Test]
    public void SetValue_UpdatesItemCount()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("numbers", typeof(int[]), [elementField], "Number array");
        var result = _strategy.CreateControl(field, "TestControl");
        var arrayValue = new int[] { 10, 20 };

        // Act
        _strategy.SetValue(result.Control, arrayValue, field);

        // Assert
        var mainPanel = (StackPanel)result.Control;
        var headerPanel = mainPanel.Children.OfType<StackPanel>().FirstOrDefault();
        var label = headerPanel?.Children.OfType<TextBlock>().FirstOrDefault();

        Assert.That(label, Is.Not.Null);
        Assert.That(label!.Text, Does.Contain("2 items"));
    }

    [Test]
    public void SetValue_NullValue_ClearsItems()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
        var result = _strategy.CreateControl(field, "TestControl");

        // First set some values
        _strategy.SetValue(result.Control, new int[] { 1, 2, 3 }, field);

        // Act - set to null
        _strategy.SetValue(result.Control, null, field);

        // Assert
        var mainPanel = (StackPanel)result.Control;
        var itemsPanel = mainPanel.Children
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == "TestControl.Items");

        Assert.That(itemsPanel!.Children.Count, Is.EqualTo(0));
    }

    [Test]
    public void SetValue_StringArray_CreatesTextBoxControls()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(string), [], "");
        var field = new FieldMetaData("testArray", typeof(string[]), [elementField], "String array");
        var result = _strategy.CreateControl(field, "TestControl");
        var arrayValue = new string[] { "hello", "world" };

        // Act
        _strategy.SetValue(result.Control, arrayValue, field);

        // Assert
        var mainPanel = (StackPanel)result.Control;
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

    [Test]
    public void SetValue_BoolArray_CreatesRadioButtonControls()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(bool), [], "");
        var field = new FieldMetaData("testArray", typeof(bool[]), [elementField], "Bool array");
        var result = _strategy.CreateControl(field, "TestControl");
        var arrayValue = new bool[] { true, false };

        // Act
        _strategy.SetValue(result.Control, arrayValue, field);

        // Assert
        var mainPanel = (StackPanel)result.Control;
        var itemsPanel = mainPanel.Children
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == "TestControl.Items");

        Assert.That(itemsPanel!.Children.Count, Is.EqualTo(2));
    }

    [Test]
    public void ExtractValue_AfterSetValue_ReturnsCorrectArray()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
        var result = _strategy.CreateControl(field, "TestControl");
        var originalArray = new int[] { 5, 10, 15 };

        // Act
        _strategy.SetValue(result.Control, originalArray, field);
        var extractedValue = _strategy.ExtractValue(result.Control, field);

        // Assert
        Assert.That(extractedValue, Is.InstanceOf<int[]>());
        var extractedArray = (int[])extractedValue!;
        Assert.That(extractedArray, Is.EqualTo(originalArray));
    }

    [Test]
    public void CreateControl_NoSubTypes_ThrowsInvalidOperationException()
    {
        // Arrange
        var field = new FieldMetaData("testArray", typeof(int[]), [], "Test array");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _strategy.CreateControl(field, "TestControl"));

        Assert.That(ex!.Message, Does.Contain("has no SubTypes"));
    }

    [Test]
    public void ExtractValue_InvalidControl_ThrowsInvalidOperationException()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
        var button = new Button();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _strategy.ExtractValue(button, field));
    }

    [Test]
    public void SetValue_InvalidControl_ThrowsInvalidOperationException()
    {
        // Arrange
        var elementField = new FieldMetaData("", typeof(int), [], "");
        var field = new FieldMetaData("testArray", typeof(int[]), [elementField], "Test array");
        var button = new Button();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _strategy.SetValue(button, new int[] { 1, 2, 3 }, field));
    }
}
