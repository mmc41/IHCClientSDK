using Avalonia.Controls;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Safe_Unit_Tests.ParameterControlStrategies;

[TestFixture]
public class StringParameterStrategyTests
{
    private StringParameterStrategy _strategy;

    [SetUp]
    public void SetUp()
    {
        _strategy = new StringParameterStrategy();
    }

    [Test]
    public void CanHandle_StringType_ReturnsTrue()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.True);
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
    public void CanHandle_BoolType_ReturnsFalse()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(bool), [], "Test description");

        // Act
        bool result = _strategy.CanHandle(field);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CreateControl_ValidField_ReturnsTextBox()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Control, Is.InstanceOf<TextBox>());
        Assert.That(result.Control.Name, Is.EqualTo("TestControl"));
        Assert.That(result.IsComposite, Is.False);
    }

    [Test]
    public void CreateControl_InvalidField_ThrowsNotSupportedException()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _strategy.CreateControl(field, "TestControl"));
    }

    [Test]
    public void CreateControl_WithDescription_SetsTooltip()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test tooltip description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");

        // Assert
        var tooltip = ToolTip.GetTip(result.Control);
        Assert.That(tooltip, Is.EqualTo("Test tooltip description"));
    }

    [Test]
    public void ExtractValue_TextBoxWithValue_ReturnsText()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
        var textBox = new TextBox { Text = "Hello World" };

        // Act
        var value = _strategy.ExtractValue(textBox, field);

        // Assert
        Assert.That(value, Is.EqualTo("Hello World"));
    }

    [Test]
    public void ExtractValue_TextBoxEmpty_ReturnsNull()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
        var textBox = new TextBox { Text = "" };

        // Act
        var value = _strategy.ExtractValue(textBox, field);

        // Assert
        Assert.That(value, Is.Null);
    }

    [Test]
    public void ExtractValue_InvalidControl_ThrowsInvalidOperationException()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
        var button = new Button();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _strategy.ExtractValue(button, field));
    }

    [Test]
    public void SetValue_ValidValue_SetsTextBoxText()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
        var textBox = new TextBox();

        // Act
        _strategy.SetValue(textBox, "Test Value", field);

        // Assert
        Assert.That(textBox.Text, Is.EqualTo("Test Value"));
    }

    [Test]
    public void SetValue_NullValue_SetsEmptyString()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
        var textBox = new TextBox { Text = "Initial Value" };

        // Act
        _strategy.SetValue(textBox, null, field);

        // Assert
        Assert.That(textBox.Text, Is.EqualTo(string.Empty));
    }

    [Test]
    public void SetValue_InvalidControl_ThrowsInvalidOperationException()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(string), [], "Test description");
        var button = new Button();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _strategy.SetValue(button, "Test", field));
    }
}
