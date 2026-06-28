using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Ihc;
using IhcLab;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// US-A6: a SceneProject parameter (now a BinaryFile) is rendered as a file picker by
    /// FileParameterStrategy, and a picked file round-trips into a SceneProject argument.
    /// </summary>
    [TestFixture]
    public class FileParameterStrategyTests : AvaloniaTestBase
    {
        private FileParameterStrategy strategy;

        private static FieldMetaData SceneProjectField() => new FieldMetaData(
            "project",
            typeof(SceneProject),
            [
                new FieldMetaData("Data", typeof(byte[]), [], ""),
                new FieldMetaData("Filename", typeof(string), [], "")
            ],
            "Scene project to store");

        [SetUp]
        public void SetUp()
        {
            strategy = new FileParameterStrategy();
        }

        [Test]
        public void CanHandle_SceneProject_ReturnsTrue()
        {
            Assert.That(strategy.CanHandle(SceneProjectField()), Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_SceneProject_ReturnsBinaryFilePicker()
        {
            var control = strategy.CreateControl(SceneProjectField(), "TestControl");

            Assert.That(control, Is.InstanceOf<BinaryFilePicker>());
            Assert.That(control.Name, Is.EqualTo("TestControl"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_NoDescription_HasNoTooltip()
        {
            // FOUND-09: file pickers use the shared tooltip rule - no tooltip when there is no description.
            var field = new FieldMetaData(
                "project",
                typeof(SceneProject),
                [
                    new FieldMetaData("Data", typeof(byte[]), [], ""),
                    new FieldMetaData("Filename", typeof(string), [], "")
                ],
                "");

            var control = strategy.CreateControl(field, "TestControl");

            Assert.That(ToolTip.GetTip(control), Is.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_PickedFile_BuildsSceneProjectFromBytes()
        {
            // Arrange
            var field = SceneProjectField();
            var control = (BinaryFilePicker)strategy.CreateControl(field, "TestControl");
            var bytes = new byte[] { 10, 20, 30, 40 };

            // Act - simulate the user picking a file, then extract the parameter value
            control.ApplyPickedFile("scene.icw", bytes);
            var project = strategy.ExtractValue(control, field) as SceneProject;

            // Assert - a SceneProject carrying the exact picked bytes + filename
            Assert.That(project, Is.Not.Null);
            Assert.That(project!.Data, Is.EqualTo(bytes));
            Assert.That(project!.Filename, Is.EqualTo("scene.icw"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_SceneProject_RestoresIntoPickerAndReExtracts()
        {
            // Arrange - a previously entered SceneProject value
            var field = SceneProjectField();
            var control = (BinaryFilePicker)strategy.CreateControl(field, "TestControl");
            var stored = new SceneProject("restored.icz", new byte[] { 7, 7, 7 });

            // Act - restore it into the control, then re-extract (two-way sync)
            strategy.SetValue(control, stored, field);
            var value = strategy.ExtractValue(control, field) as SceneProject;

            // Assert
            Assert.That(value, Is.Not.Null);
            Assert.That(value!.Data, Is.EqualTo(new byte[] { 7, 7, 7 }));
            Assert.That(value!.Filename, Is.EqualTo("restored.icz"));
        }
    }
}
