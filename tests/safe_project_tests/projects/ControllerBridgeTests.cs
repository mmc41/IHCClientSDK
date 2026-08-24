using System.Collections.Immutable;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Unit tests for the controller↔file bridge on <see cref="ProjectAppService"/>
    /// (<see cref="ProjectAppService.DownloadFrom"/> / <see cref="ProjectAppService.UploadTo"/>). The
    /// low-level <see cref="IControllerService"/> is mocked with FakeItEasy; a REAL
    /// <see cref="ProjectAppService"/> and a fake <see cref="ICatalog"/> are used (per the test rules: mock
    /// IHC API services, never app services). The bridge reuses the now-implemented <c>Load</c>/<c>Save</c>
    /// byte engine, so it exercises the same byte-exact reader/writer the file path uses.
    /// </summary>
    public class ControllerBridgeTests
    {
        // Root header values of testdata/projects/Project1-SimpelWired.vis.
        private const string Project1Id1 = "_0x1b0e3a1f";
        private const string Project1Id2 = "_0x1b0f051b";
        private const string Project1LastUniqueId = "_0x214";

        private static string ProjectDataPath =>
            Path.Combine(AppContext.BaseDirectory, "testdata", "projects", "Project1-SimpelWired.vis");

        // Serializable (all #REQUIRED attrs present) but invalid: scenes@scene_resource dangles.
        private static Project InvalidButSerializableProject() => new(
            Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x60") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") },
                        Node("product_dataline", "_0x5153", new[] { ("product_identifier", "_0x2202"), ("name", "P") },
                            Node("scenes", "_0x5349", new[] { ("name", "Scenarier"), ("scene_resource", "_0xdead52") }))))));

        // --- R0: the controller bridge must authenticate the SAME cookie session the controller rides ---
        // (refac2ana.md section 3.7 / R0 / D02). A ControllerService rides the cookie handler of the
        // IAuthenticationService it is built from (controllerService.cs); if ProjectAppService self-builds a
        // second AuthenticationService, EnsureAuthenticated logs into a session the controller never uses. These
        // wiring tests build the REAL AuthenticationService/ControllerService (construction does no network I/O),
        // so they need a real (non-mock) endpoint rather than the suite's default mock:// settings.
        private static IhcSettings RealEndpointSettings() =>
            TestSetup.Settings with { Endpoint = "https://ihc.invalid" };

        [Test]
        public void InjectingControllerWithoutMatchingAuth_IsRejected_NeverAuthenticatesAForeignSession()
        {
            IhcSettings settings = RealEndpointSettings();
            // The controller rides the cookie session of the auth it was built from.
            var auth = new AuthenticationService(settings);
            var controller = new ControllerService(auth);

            // Pre-fix the service silently self-built a SECOND AuthenticationService here, authenticating a
            // session the injected controller never uses (R0). The bridge must instead demand the matching auth.
            Assert.That(() => new ProjectAppService(settings, A.Fake<ICatalog>(),
                    new FakeTimeProvider(DateTimeOffset.UnixEpoch), controller, authService: null),
                Throws.ArgumentException.With.Message.Contains(nameof(IAuthenticationService)),
                "a controller-injecting bridge must be given the auth the controller rides, not self-build a foreign one");
        }

        [Test]
        public void MatchedPairBridge_AuthenticatesTheExactAuthTheControllerWasBuiltFrom()
        {
            IhcSettings settings = RealEndpointSettings();
            var auth = new AuthenticationService(settings);
            var controller = new ControllerService(auth);   // rides auth.GetCookieHandler()

            var app = new ProjectAppService(settings, controller, auth);

            Assert.That(app.BridgeAuthentication, Is.SameAs(auth),
                "the bridge authenticates the same auth the controller rides — one shared cookie session");
        }

        [Test]
        public void CreateWithControllerBridge_WiresANonNullSharedAuthentication()
        {
            // The settings-based bridge builds ONE AuthenticationService and the ControllerService that rides its
            // cookie handler, so DownloadFrom/UploadTo authenticate exactly the controller's session.
            ProjectAppService app = ProjectAppService.CreateWithControllerBridge(RealEndpointSettings());

            Assert.That(app.BridgeAuthentication, Is.InstanceOf<AuthenticationService>(),
                "the bridge must authenticate a real, shared AuthenticationService");
        }

        [Test]
        public void UploadTo_InvalidProject_ThrowsProjectValidationException_AndNeverStores()
        {
            var controller = A.Fake<IControllerService>();
            var app = Fakes.BridgeService(controller);

            Assert.That(async () => await app.UploadTo(InvalidButSerializableProject(), ProjectSaveOptions.PreserveExistingMetadata),
                Throws.TypeOf<ProjectValidationException>()
                    .With.Property(nameof(ProjectValidationException.Result))
                    .Property(nameof(ProjectValidationResult.IsValid)).False,
                "a structurally broken project must never reach controller EPROM");
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).MustNotHaveHappened();
        }

        [Test]
        public async Task UploadTo_InvalidProject_WithValidateFalse_StillStores()
        {
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).Returns(true);
            var app = Fakes.BridgeService(controller);

            bool ok = await app.UploadTo(InvalidButSerializableProject(), ProjectSaveOptions.PreserveExistingMetadata,
                                         validate: false);

            Assert.That(ok, Is.True, "the escape hatch re-uploads deviant-but-serializable files");
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task UploadTo_ControllerDeclines_ThrowsProjectUploadException()
        {
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).Returns(false);
            var app = Fakes.BridgeService(controller);
            Project project = await app.Load(ProjectDataPath);

            Assert.That(async () => await app.UploadTo(project, ProjectSaveOptions.PreserveExistingMetadata),
                Throws.TypeOf<ProjectUploadException>(),
                "a declined store must surface as an exception, not an easily-ignored false");
        }

        [Test]
        public void DownloadFrom_NoProjectOnController_ThrowsInvalidOperation()
        {
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.GetProject()).Returns((ProjectFile?)null);
            var app = Fakes.BridgeService(controller);

            // InstanceOf, not TypeOf: the refusal now carries a code (import-controller-no-project under
            // bridge.download) and is the derived RefusedOperationException. Still an InvalidOperationException,
            // which is what this assertion is here to keep.
            Assert.That(async () => await app.DownloadFrom(),
                Throws.InstanceOf<InvalidOperationException>()
                    .With.Message.Contains(nameof(IControllerService.IsIHCProjectAvailable)),
                "an empty controller must produce an actionable error, not a NullReferenceException");
        }

        [Test]
        public async Task DownloadFrom_ParsesControllerPayload_PreservesHeaderIds()
        {
            string xml = File.ReadAllText(ProjectDataPath, ProjectFile.Encoding);
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.GetProject()).Returns(new ProjectFile("Project1.ihc", xml));

            Project project = await Fakes.BridgeService(controller).DownloadFrom();

            Assert.Multiple(() =>
            {
                Assert.That(project.Id1, Is.EqualTo(Project1Id1));
                Assert.That(project.Id2, Is.EqualTo(Project1Id2));
                Assert.That(project.LastUniqueId, Is.EqualTo(Project1LastUniqueId));
            });
        }

        [Test]
        public async Task UploadTo_PreserveExistingMetadata_StoresBytewiseIdenticalPayload()
        {
            byte[] original = File.ReadAllBytes(ProjectDataPath);

            ProjectFile stored = null!;
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._))
                .Invokes((ProjectFile f) => stored = f)
                .Returns(true);

            var app = Fakes.BridgeService(controller);
            Project project = await app.Load(ProjectDataPath);

            bool ok = await app.UploadTo(project, ProjectSaveOptions.PreserveExistingMetadata);

            Assert.That(ok, Is.True);
            Assert.That(ProjectFile.Encoding.GetBytes(stored.Data), Is.EqualTo(original));
        }

        [Test]
        public async Task UploadTo_DefaultOptions_RestampsId2_PreservesId1AndLastUniqueId()
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));

            ProjectFile stored = null!;
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._))
                .Invokes((ProjectFile f) => stored = f)
                .Returns(true);

            var app = Fakes.BridgeService(controller, clock);
            Project original = await app.Load(ProjectDataPath);

            await app.UploadTo(original, ProjectSaveOptions.Default);
            Project reparsed = await app.Load(new MemoryStream(ProjectFile.Encoding.GetBytes(stored.Data)));

            Assert.Multiple(() =>
            {
                Assert.That(reparsed.Id1, Is.EqualTo(original.Id1), "id1 (creation stamp) preserved");
                Assert.That(reparsed.LastUniqueId, Is.EqualTo(original.LastUniqueId), "high-water mark preserved");
                Assert.That(reparsed.Id2, Is.Not.EqualTo(original.Id2), "id2 re-stamped from the clock");
            });
        }
    }
}
