using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Ihc;
using Ihc.Projects;

namespace Ihc.Tests
{
    /// <summary>
    /// System tests for the project download/round-trip against a live IHC controller.
    ///
    /// The read-only test verifies the controller exposes a <c>utcs_project</c> v4.0 header carrying the
    /// <c>id1</c>/<c>id2</c>/<c>last_unique_id</c> attributes the authoring editor allocates ids against —
    /// the download half of the edit-and-reupload gating risk. It calls only <c>GetProject</c> (no state
    /// change) and is safe for this suite.
    ///
    /// The upload-preservation test is <c>[Explicit]</c>: it WRITES a project back to the controller (state
    /// changing) and depends on the Stage-2 reader/writer, so it must be run manually against a dev
    /// controller. Passing it gates ONLY the edit-and-reupload path; read-only download and file-only
    /// editing do not depend on it.
    /// </summary>
    [TestFixture]
    public class ProjectDownloadTest
    {
        private AuthenticationService authService;
        private ControllerService controllerService;

        [SetUp]
        public async Task SetupMethod()
        {
            authService = new AuthenticationService(Setup.settings);
            controllerService = new ControllerService(authService);
            await authService.Authenticate();
        }

        [TearDown]
        public async Task BaseTearDown()
        {
            await authService.Disconnect();
            authService?.Dispose();
            authService = null;
        }

        [Test]
        public async Task DownloadedProject_HasUtcsProjectV4Header_WithIdAttributes()
        {
            ProjectFile file = await controllerService.GetProject();

            Match root = Regex.Match(file.Data, "<utcs_project\\b[^>]*>");
            Assert.That(root.Success, Is.True, "root <utcs_project> element present in decompressed payload");
            Assert.Multiple(() =>
            {
                Assert.That(root.Value, Does.Contain("version_major=\"4\""));
                Assert.That(root.Value, Does.Contain("version_minor=\"0\""));
                Assert.That(Regex.IsMatch(root.Value, "id1=\"_0x[0-9a-f]+\""), Is.True, "id1 present");
                Assert.That(Regex.IsMatch(root.Value, "id2=\"_0x[0-9a-f]+\""), Is.True, "id2 present");
                Assert.That(Regex.IsMatch(root.Value, "last_unique_id=\"_0x[0-9a-f]+\""), Is.True, "last_unique_id present");
            });
        }

        [Test, Explicit("MANUAL + STATE-CHANGING: re-uploads the project to a DEV controller and needs the Stage-2 Load/Save engine. Gates the edit-and-reupload path only.")]
        public async Task EditReupload_PreservesHeaderIds_Manual()
        {
            var app = new ProjectAppService(Setup.settings);

            Project before = await app.DownloadFrom(controllerService);
            string id1 = before.Id1;
            string id2 = before.Id2;
            string lastUniqueId = before.LastUniqueId;

            // Byte-exact re-upload (no metadata re-stamp) — writes the project back to the controller's SD card.
            bool stored = await app.UploadTo(controllerService, before, ProjectSaveOptions.PreserveExistingMetadata);
            Assert.That(stored, Is.True);

            Project after = await app.DownloadFrom(controllerService);
            Assert.Multiple(() =>
            {
                Assert.That(after.Id1, Is.EqualTo(id1), "id1 preserved across controller store/retrieve");
                Assert.That(after.Id2, Is.EqualTo(id2), "id2 preserved (PreserveExistingMetadata)");
                Assert.That(after.LastUniqueId, Is.EqualTo(lastUniqueId), "last_unique_id high-water mark preserved");
            });
            // To APPLY the stored project to the controller runtime, call
            // new ConfigurationService(authService).DelayedReboot(...) as a separate step.
        }
    }
}
