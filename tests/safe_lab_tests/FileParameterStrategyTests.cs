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

        /// <summary>
        /// StoreSceneProject upload: a SceneProject parameter renders a BinaryFilePicker whose dialog defaults to
        /// the scene project's canonical *.icw/*.icz extensions rather than showing every file.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_SceneProject_SetsIcwIczFilter()
        {
            var control = strategy.CreateControl(SceneProjectField(), "TestControl");

            var picker = (BinaryFilePicker)control;
            Assert.That(picker.FileTypeExtensions, Is.EqualTo(new[] { "icw", "icz" }));
        }

        /// <summary>
        /// StoreSceneProject upload: the upload button names the concrete file type ("Upload *.icw/*.icz File")
        /// instead of the misleading generic "Upload Binary File", so the user can see what the picker accepts.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_SceneProject_UploadButtonNamesIcwIczExtensions()
        {
            var control = strategy.CreateControl(SceneProjectField(), "TestControl");

            var uploadButton = control.FindControl<Button>("UploadButton");
            Assert.That(uploadButton, Is.Not.Null, "BinaryFilePicker must expose its UploadButton");
            Assert.That(uploadButton!.Content, Is.EqualTo("Upload *.icw/*.icz File"));
        }

        private static FieldMetaData ProjectFileField() => new FieldMetaData(
            "project",
            typeof(ProjectFile),
            [
                new FieldMetaData("Data", typeof(string), [], ""),
                new FieldMetaData("Filename", typeof(string), [], "")
            ],
            "Project to store");

        /// <summary>
        /// StoreProject upload: a ProjectFile parameter renders a TextFilePicker configured to read as the
        /// project's own encoding (ISO-8859-1) and to default its file dialog to *.vis - not UTF-8 / any file.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_ProjectFile_UsesLatin1AndVisFilter()
        {
            var control = strategy.CreateControl(ProjectFileField(), "TestControl");

            Assert.That(control, Is.InstanceOf<TextFilePicker>());
            var picker = (TextFilePicker)control;
            Assert.That(picker.TextEncoding, Is.EqualTo(ProjectFile.Encoding), "project upload must read as ISO-8859-1");
            Assert.That(picker.FileTypeExtension, Is.EqualTo("vis"), "project upload dialog must default to *.vis");
        }

        /// <summary>
        /// StoreProject upload: the upload button names the concrete project file type ("Upload *.vis File")
        /// instead of the misleading generic "Upload Text File", so the user can see they are uploading a .vis
        /// project file rather than any text file.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_ProjectFile_UploadButtonNamesVisExtension()
        {
            var control = strategy.CreateControl(ProjectFileField(), "TestControl");

            var uploadButton = control.FindControl<Button>("UploadButton");
            Assert.That(uploadButton, Is.Not.Null, "TextFilePicker must expose its UploadButton");
            Assert.That(uploadButton!.Content, Is.EqualTo("Upload *.vis File"));
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
