using Avalonia.Controls;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Safe_Unit_Tests.ParameterControlStrategies;

[TestFixture]
public class BoolParameterStrategyTests
{
    private BoolParameterStrategy _strategy;

    [SetUp]
    public void SetUp()
    {
        _strategy = new BoolParameterStrategy();
    }

    [Test]
    public void CanHandle_BoolType_ReturnsTrue()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_StringType_ReturnsFalse()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CanHandle_IntType_ReturnsFalse()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CreateControl_ValidField_ReturnsStackPanelWithRadioButtons()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Control, Is.InstanceOf<StackPanel>());
        Assert.That(result.Control.Name, Is.EqualTo("TestControl"));
        Assert.That(result.IsComposite, Is.False);

        var stackPanel = (StackPanel)result.Control;
        var radioButtons = stackPanel.Children.OfType<RadioButton>().ToList();
        Assert.That(radioButtons, Has.Count.EqualTo(2));
        Assert.That(radioButtons[0].Content, Is.EqualTo("True"));
        Assert.That(radioButtons[1].Content, Is.EqualTo("False"));
    }

    [Test]
    public void CreateControl_InvalidField_ThrowsNotSupportedException()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _strategy.CreateControl(field, "TestControl"));
    }

    [Test]
    public void CreateControl_WithDescription_SetsTooltip()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test tooltip description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");

        // Assert
        var tooltip = ToolTip.GetTip(result.Control);
        Assert.That(tooltip, Is.EqualTo("Test tooltip description"));
    }

    [Test]
    public void CreateControl_DefaultValue_IsFalse()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");
        var extractedValue = _strategy.ExtractValue(result.Control, field);

        // Assert
        Assert.That(extractedValue, Is.False);
    }

    [Test]
    public void ExtractValue_TrueSelected_ReturnsTrue()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
        var result = _strategy.CreateControl(field, "TestControl");
        var stackPanel = (StackPanel)result.Control;
        var trueRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "True");
        trueRadio.IsChecked = true;

        // Act
        var value = _strategy.ExtractValue(stackPanel, field);

        // Assert
        Assert.That(value, Is.True);
    }

    [Test]
    public void ExtractValue_FalseSelected_ReturnsFalse()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
        var result = _strategy.CreateControl(field, "TestControl");
        var stackPanel = (StackPanel)result.Control;
        var falseRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "False");
        falseRadio.IsChecked = true;

        // Act
        var value = _strategy.ExtractValue(stackPanel, field);

        // Assert
        Assert.That(value, Is.False);
    }

    [Test]
    public void ExtractValue_InvalidControl_ThrowsInvalidOperationException()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
        var button = new Button();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _strategy.ExtractValue(button, field));
    }

    [Test]
    public void SetValue_True_SelectsTrueRadioButton()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
        var result = _strategy.CreateControl(field, "TestControl");
        var stackPanel = (StackPanel)result.Control;

        // Act
        _strategy.SetValue(stackPanel, true, field);

        // Assert
        var trueRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "True");
        var falseRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "False");
        Assert.That(trueRadio.IsChecked, Is.True);
        Assert.That(falseRadio.IsChecked, Is.False);
    }

    [Test]
    public void SetValue_False_SelectsFalseRadioButton()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
        var result = _strategy.CreateControl(field, "TestControl");
        var stackPanel = (StackPanel)result.Control;

        // Act
        _strategy.SetValue(stackPanel, false, field);

        // Assert
        var trueRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "True");
        var falseRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "False");
        Assert.That(trueRadio.IsChecked, Is.False);
        Assert.That(falseRadio.IsChecked, Is.True);
    }

    [Test]
    public void SetValue_Null_DefaultsToFalse()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
        var result = _strategy.CreateControl(field, "TestControl");
        var stackPanel = (StackPanel)result.Control;

        // Act
        _strategy.SetValue(stackPanel, null, field);

        // Assert
        var trueRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "True");
        var falseRadio = stackPanel.Children.OfType<RadioButton>().First(r => r.Content?.ToString() == "False");
        Assert.That(trueRadio.IsChecked, Is.False);
        Assert.That(falseRadio.IsChecked, Is.True);
    }

    [Test]
    public void SetValue_InvalidControl_ThrowsInvalidOperationException()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");
        var button = new Button();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _strategy.SetValue(button, true, field));
    }
}
