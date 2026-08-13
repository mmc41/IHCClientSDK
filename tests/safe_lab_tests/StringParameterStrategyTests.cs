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

    }
}
