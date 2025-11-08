using Avalonia.Controls;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Safe_Unit_Tests.ParameterControlStrategies;

[TestFixture]
public class NumericParameterStrategyTests
{
    private NumericParameterStrategy _strategy;

    [SetUp]
    public void SetUp()
    {
        _strategy = new NumericParameterStrategy();
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
    public void CreateControl_ValidField_ReturnsNumericUpDown()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Control, Is.InstanceOf<NumericUpDown>());
        Assert.That(result.Control.Name, Is.EqualTo("TestControl"));
        Assert.That(result.IsComposite, Is.False);
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
        var field = new FieldMetaData("testParam", typeof(int), [], "Test tooltip description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");

        // Assert
        var tooltip = ToolTip.GetTip(result.Control);
        Assert.That(tooltip, Is.EqualTo("Test tooltip description"));
    }

    [Test]
    public void CreateControl_IntType_SetsCorrectMinMax()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");
        var numericUpDown = (NumericUpDown)result.Control;

        // Assert
        Assert.That(numericUpDown.Minimum, Is.EqualTo(int.MinValue));
        Assert.That(numericUpDown.Maximum, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void CreateControl_ByteType_SetsCorrectMinMax()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(byte), [], "Test description");

        // Act
        var result = _strategy.CreateControl(field, "TestControl");
        var numericUpDown = (NumericUpDown)result.Control;

        // Assert
        Assert.That(numericUpDown.Minimum, Is.EqualTo(byte.MinValue));
        Assert.That(numericUpDown.Maximum, Is.EqualTo(byte.MaxValue));
    }

    [Test]
    public void ExtractValue_IntValue_ReturnsInt()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
        var numericUpDown = new NumericUpDown { Value = 42 };

        // Act
        var value = _strategy.ExtractValue(numericUpDown, field);

        // Assert
        Assert.That(value, Is.InstanceOf<int>());
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public void ExtractValue_FloatValue_ReturnsFloat()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(float), [], "Test description");
        var numericUpDown = new NumericUpDown { Value = 3.14m };

        // Act
        var value = _strategy.ExtractValue(numericUpDown, field);

        // Assert
        Assert.That(value, Is.InstanceOf<float>());
        Assert.That((float)value!, Is.EqualTo(3.14f).Within(0.01));
    }

    [Test]
    public void ExtractValue_DoubleValue_ReturnsDouble()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(double), [], "Test description");
        var numericUpDown = new NumericUpDown { Value = 2.718m };

        // Act
        var value = _strategy.ExtractValue(numericUpDown, field);

        // Assert
        Assert.That(value, Is.InstanceOf<double>());
        Assert.That((double)value!, Is.EqualTo(2.718).Within(0.001));
    }

    [Test]
    public void ExtractValue_NullValue_ReturnsDefaultValue()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
        var numericUpDown = new NumericUpDown { Value = null };

        // Act
        var value = _strategy.ExtractValue(numericUpDown, field);

        // Assert
        Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void ExtractValue_InvalidControl_ThrowsInvalidOperationException()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
        var button = new Button();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _strategy.ExtractValue(button, field));
    }

    [Test]
    public void SetValue_IntValue_SetsNumericUpDownValue()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
        var numericUpDown = new NumericUpDown();

        // Act
        _strategy.SetValue(numericUpDown, 100, field);

        // Assert
        Assert.That(numericUpDown.Value, Is.EqualTo(100));
    }

    [Test]
    public void SetValue_FloatValue_SetsNumericUpDownValue()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(float), [], "Test description");
        var numericUpDown = new NumericUpDown();

        // Act
        _strategy.SetValue(numericUpDown, 1.5f, field);

        // Assert
        Assert.That(numericUpDown.Value, Is.EqualTo(1.5m));
    }

    [Test]
    public void SetValue_NullValue_SetsDefaultValue()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
        var numericUpDown = new NumericUpDown { Value = 42 };

        // Act
        _strategy.SetValue(numericUpDown, null, field);

        // Assert
        Assert.That(numericUpDown.Value, Is.EqualTo(0));
    }

    [Test]
    public void SetValue_InvalidControl_ThrowsInvalidOperationException()
    {
        // Arrange
        var field = new FieldMetaData("testParam", typeof(int), [], "Test description");
        var button = new Button();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _strategy.SetValue(button, 42, field));
    }

    [TestCase(typeof(byte), (byte)250)]
    [TestCase(typeof(sbyte), (sbyte)-100)]
    [TestCase(typeof(short), (short)-30000)]
    [TestCase(typeof(ushort), (ushort)60000)]
    [TestCase(typeof(int), -123456)]
    [TestCase(typeof(uint), 123456u)]
    [TestCase(typeof(long), -9876543210L)]
    [TestCase(typeof(ulong), 9876543210UL)]
    public void ExtractValue_VariousIntegerTypes_ReturnsCorrectType(Type numericType, object expectedValue)
    {
        // Arrange
        var field = new FieldMetaData("testParam", numericType, [], "Test description");
        var numericUpDown = new NumericUpDown { Value = Convert.ToDecimal(expectedValue) };

        // Act
        var value = _strategy.ExtractValue(numericUpDown, field);

        // Assert
        Assert.That(value, Is.InstanceOf(numericType));
        Assert.That(value, Is.EqualTo(expectedValue));
    }
}
