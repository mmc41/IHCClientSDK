using System.Threading.Tasks;
using FakeItEasy;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The save/upload postcondition (<see cref="ProjectSaveOptions.VerifyRoundTrip"/>): the just-serialized bytes
    /// are re-parsed and compared with the written model, TOLERATING the benign omit-if-default asymmetry (an
    /// attribute equal to its DTD default is dropped on write and never re-materialized), so a foreign file that
    /// explicitly carries such an attribute still round-trips while genuinely non-representable state is refused.
    /// The controller bridge auto-authenticates and always verifies — controller EPROM has no <c>.BAK</c>.
    /// </summary>
    public class RoundTripVerificationTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        // group@icon defaults to "_0x0": physically present and equal to that default, it is dropped on write
        // (omit-if-default) and never re-materialized. The tolerant round-trip check treats this as a FAITHFUL
        // write — the exact foreign-file shape the pre-fix forced verification wrongly rejected.
        private static Project DefaultEqualAttrProject() => new(Node("utcs_project", null,
            new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x40") },
            Node("groups", "_0x2031", new[] { ("name", "L") },
                Node("group", "_0x2132", new[] { ("name", "Stue"), ("icon", "_0x0") }))));

        // A group note holding a code point above Latin-1's 0xFF ceiling (U+20AC), built at runtime so the source
        // stays plain ASCII — a scalar the .vis wire encoding genuinely cannot represent, so the write must be
        // refused before it reaches the controller.
        private static readonly string NonLatin1Char = ((char)0x20AC).ToString();

        private static Project NonLatin1Project() => new(Node("utcs_project", null,
            new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x40") },
            Node("groups", "_0x2031", new[] { ("name", "L") },
                Node("group", "_0x2132", new[] { ("name", "Stue"), ("note", NonLatin1Char) }))));

        [Test]
        public async Task Save_WithVerifyRoundTrip_OnAnAuthenticFile_Succeeds()
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load("testdata/projects/Project1-SimpelWired.vis");
            using var ms = new MemoryStream();

            var options = new ProjectSaveOptions { WriteMetadataVerbatim = true, VerifyRoundTrip = true };
            await app.Save(project, ms, options);

            Assert.That(ms.Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task Save_WithVerifyRoundTrip_ToleratesExplicitDefaultEqualAttribute()
        {
            // Finding 10: a physically-present attribute equal to its DTD default is a faithful (representable)
            // write — the tolerant check must not reject it.
            var app = new ProjectAppService(Settings);
            using var ms = new MemoryStream();
            var options = new ProjectSaveOptions { WriteMetadataVerbatim = true, VerifyRoundTrip = true };

            await app.Save(DefaultEqualAttrProject(), ms, options);

            Assert.That(ms.Length, Is.GreaterThan(0), "the benign omit-if-default asymmetry round-trips, never throws");
        }

        [Test]
        public async Task UploadTo_ForeignFileWithDefaultEqualAttribute_IsStored()
        {
            // Finding 10: the documented validate:false "re-upload a foreign file the vendor tooling tolerates" path
            // must no longer be foreclosed by the (now tolerant) forced round-trip verification.
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).Returns(true);
            var app = Fakes.BridgeService(controller);

            bool ok = await app.UploadTo(DefaultEqualAttrProject(), ProjectSaveOptions.PreserveExistingMetadata, validate: false);

            Assert.That(ok, Is.True);
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void UploadTo_GenuinelyUnrepresentableModel_NeverReachesController()
        {
            // The safety pin stays: a model the .vis format genuinely cannot persist (a non-Latin1 scalar the wire
            // encoding cannot hold) must never reach controller EPROM.
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).Returns(true);
            var app = Fakes.BridgeService(controller);

            Assert.That(async () => await app.UploadTo(NonLatin1Project(), ProjectSaveOptions.PreserveExistingMetadata, validate: false),
                Throws.Exception, "an unrepresentable model is refused at the write");
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).MustNotHaveHappened();
        }

        [Test]
        public void DownloadFrom_WhenNotAuthenticated_AuthenticatesBeforeCallingController()
        {
            // Finding 9: the controller bridge auto-authenticates (like AdminAppService/InformationAppService)
            // before any controller call. GetProject returns null so DownloadFrom throws afterwards — but only
            // after EnsureAuthenticated has already logged in.
            var auth = A.Fake<IAuthenticationService>();
            A.CallTo(() => auth.IsAuthenticated()).Returns(false);
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.GetProject()).Returns((ProjectFile?)null);
            var app = Fakes.BridgeService(controller, auth: auth);

            Assert.That(async () => await app.DownloadFrom(), Throws.InstanceOf<InvalidOperationException>());
            A.CallTo(() => auth.Authenticate()).MustHaveHappenedOnceExactly();
        }
    }
}
