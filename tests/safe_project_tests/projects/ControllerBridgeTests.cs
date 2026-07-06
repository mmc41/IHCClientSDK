using System.Collections.Immutable;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

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

        private static ProjectAppService NewService(IControllerService controller, TimeProvider? clock = null) =>
            new ProjectAppService(
                TestSetup.Settings,
                A.Fake<ICatalog>(),
                clock ?? new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                controller);

        private static ProjectElement Node(string tag, string? id, (string, string)[] attrs, params ProjectElement[] children)
        {
            ElementId? parsed = id is not null && ElementId.TryParse(id, out ElementId p) ? p : null;
            var bag = System.Collections.Immutable.ImmutableArray.CreateBuilder<(string, string)>();
            if (id is not null)
            {
                bag.Add(("id", id));
            }
            bag.AddRange(attrs);
            return new ProjectElement(tag, parsed, bag.ToImmutable(), children.ToImmutableArray());
        }

        // Serializable (all #REQUIRED attrs present) but invalid: scenes@scene_resource dangles.
        private static Project InvalidButSerializableProject() => new(
            Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x60") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") },
                        Node("product_dataline", "_0x5153", new[] { ("product_identifier", "_0x2202"), ("name", "P") },
                            Node("scenes", "_0x5349", new[] { ("name", "Scenarier"), ("scene_resource", "_0xdead52") }))))));

        [Test]
        public void UploadTo_InvalidProject_ThrowsProjectValidationException_AndNeverStores()
        {
            var controller = A.Fake<IControllerService>();
            var app = NewService(controller);

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
            var app = NewService(controller);

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
            var app = NewService(controller);
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
            var app = NewService(controller);

            Assert.That(async () => await app.DownloadFrom(),
                Throws.InvalidOperationException.With.Message.Contains(nameof(IControllerService.IsIHCProjectAvailable)),
                "an empty controller must produce an actionable error, not a NullReferenceException");
        }

        [Test]
        public async Task DownloadFrom_ParsesControllerPayload_PreservesHeaderIds()
        {
            string xml = File.ReadAllText(ProjectDataPath, ProjectFile.Encoding);
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.GetProject()).Returns(new ProjectFile("Project1.ihc", xml));

            Project project = await NewService(controller).DownloadFrom();

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

            var app = NewService(controller);
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

            var app = NewService(controller, clock);
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
