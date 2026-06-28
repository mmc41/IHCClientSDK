using NUnit.Framework;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// US-A6: SceneProject must implement the BinaryFile contract (incl. the copy-constructor the Lab's
    /// FileParameterStrategy uses) so it can be supplied via a file picker.
    /// </summary>
    [TestFixture]
    public class SceneProjectModelTests
    {
        [Test]
        public void SceneProject_IsBinaryFile()
        {
            var project = new SceneProject("scene.icw", new byte[] { 1, 2, 3 });
            Assert.That(project, Is.InstanceOf<BinaryFile>());
        }

        [Test]
        public void SceneProject_CopyConstructor_RoundTripsDataAndFilename()
        {
            // Any BinaryFile is a valid source for the copy-constructor (here a BackupFile).
            BinaryFile source = new BackupFile("scene.icw", new byte[] { 9, 8, 7 });

            var project = new SceneProject(source);

            Assert.That(project.Filename, Is.EqualTo("scene.icw"));
            Assert.That(project.Data, Is.EqualTo(new byte[] { 9, 8, 7 }));
        }
    }
}
