using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Projects;
using Ihc.Vis.Io;

namespace Ihc.Tests
{
    /// <summary>
    /// System test for the project download path against a live IHC controller: it verifies the controller
    /// exposes a <c>utcs_project</c> v4.0 header carrying the <c>id1</c>/<c>id2</c>/<c>last_unique_id</c>
    /// attributes the authoring editor allocates ids against — the download half of the edit-and-reupload
    /// gating risk. It calls only <c>GetProject</c> (no state change) and is safe for this suite.
    ///
    /// An upload-preservation round-trip is intentionally NOT here: writing a project back to the controller
    /// changes its state, which the suite rule ("no harmful side effects on the controller, including changing
    /// state") forbids even under <c>[Explicit]</c> (name-filtered runs still execute explicit tests). Exercise
    /// that manually via a dev utility instead.
    /// </summary>
    [TestFixture]
    public class ProjectDownloadTest : AuthenticatedSystemTest
    {
        // Assigned by CreateServices before every test, and never observed before that;
        // NUnit constructs the fixture itself, so there is no constructor to assign it in.
        private ControllerService controllerService = null!;

        protected override void CreateServices(AuthenticationService session)
        {
            controllerService = new ControllerService(session);
        }

        [Test]
        public async Task DownloadedProject_HasUtcsProjectV4Header_WithIdAttributes()
        {
            ProjectFile? file = await controllerService.GetProject();
            Assert.That(file, Is.Not.Null, "the controller must have a project stored for this test");

            Match root = Regex.Match(file!.Data, "<utcs_project\\b[^>]*>");
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
    }
}
