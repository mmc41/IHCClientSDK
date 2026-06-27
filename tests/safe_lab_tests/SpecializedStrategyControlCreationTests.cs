using System;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Ihc;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// Verifies that the specialized (non-scalar) strategies build their Avalonia controls successfully.
    /// These exercise control construction and therefore live in the headless Avalonia test project; the
    /// pure registry-composition assertions live in <see cref="ParameterControlRegistryTests"/>.
    /// </summary>
    [TestFixture]
    public class SpecializedStrategyControlCreationTests : AvaloniaTestBase
    {
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void EnumStrategy_CreatesControlSuccessfully()
        {
            // Arrange
            var strategy = new EnumParameterStrategy();
            var field = new FieldMetaData("dayOfWeek", typeof(DayOfWeek), [], "Day of week");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("TestControl"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void DateTimeStrategy_CreatesControlSuccessfully()
        {
            // Arrange
            var strategy = new DateTimeParameterStrategy();
            var field = new FieldMetaData("testDate", typeof(DateTime), [], "Test date");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("TestControl"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ResourceValueStrategy_CreatesControlSuccessfully()
        {
            // Arrange
            var strategy = new ResourceValueParameterStrategy();
            var field = new FieldMetaData("testResource", typeof(ResourceValue), [], "Test resource");

            // Act
            var result = strategy.CreateControl(field, "TestControl");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("TestControl"));
        }
    }
}
