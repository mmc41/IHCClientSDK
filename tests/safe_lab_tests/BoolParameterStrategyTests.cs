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

        /// <summary>
        /// The description tooltip for EVERY strategy. It is applied in one place —
        /// <c>ParameterControlStrategyBase.ApplyDescriptionTooltip</c> — which each strategy calls on the control
        /// it is about to return, so this asserts shared behaviour rather than anything specific to booleans.
        /// It lives in this fixture because the boolean control is a StackPanel: the tip has to land on the
        /// container the strategy returns, not on a radio button inside it, and a strategy whose control is a bare
        /// TextBox or NumericUpDown cannot tell those two apart.
        /// </summary>
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

    }
}
