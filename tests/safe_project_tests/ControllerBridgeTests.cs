using FakeItEasy;
using Ihc.Projects;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Unit tests for the controller↔file bridge on <see cref="ProjectAppService"/>
    /// (<see cref="ProjectAppService.DownloadFrom"/> / <see cref="ProjectAppService.UploadTo"/>). The
    /// low-level <see cref="IControllerService"/> is mocked with FakeItEasy; a REAL
    /// <see cref="ProjectAppService"/> and a fake <see cref="ICatalog"/> are used (per the test rules: mock
    /// IHC API services, never app services). The bridge reuses <c>Load</c>/<c>Save</c>, which are Stage-1
    /// stubs, so these are <c>[Explicit]</c> and go green automatically once the Stage-2 reader/writer land.
    /// </summary>
    public class ControllerBridgeTests
    {
        // Root header values of testdata/Project1.vis.
        private const string Project1Id1 = "_0x1b0e3a1f";
        private const string Project1Id2 = "_0x1b0f051b";
        private const string Project1LastUniqueId = "_0x214";

        private static string ProjectDataPath =>
            Path.Combine(AppContext.BaseDirectory, "testdata", "Project1.vis");

        private static ProjectAppService NewService(TimeProvider? clock = null) =>
            new ProjectAppService(
                TestSetup.Settings,
                A.Fake<ICatalog>(),
                clock ?? new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        [Test, Explicit("Activates in Stage 2 once Load is implemented")]
        public async Task DownloadFrom_ParsesControllerPayload_PreservesHeaderIds()
        {
            string xml = File.ReadAllText(ProjectDataPath, ProjectFile.Encoding);
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.GetProject()).Returns(new ProjectFile("Project1.ihc", xml));

            Project project = await NewService().DownloadFrom(controller);

            Assert.Multiple(() =>
            {
                Assert.That(project.Id1, Is.EqualTo(Project1Id1));
                Assert.That(project.Id2, Is.EqualTo(Project1Id2));
                Assert.That(project.LastUniqueId, Is.EqualTo(Project1LastUniqueId));
            });
        }

        [Test, Explicit("Activates in Stage 2 once Load/Save are implemented")]
        public async Task UploadTo_PreserveExistingMetadata_StoresBytewiseIdenticalPayload()
        {
            byte[] original = File.ReadAllBytes(ProjectDataPath);
            var app = NewService();
            Project project = await app.Load(ProjectDataPath);

            ProjectFile stored = null!;
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._))
                .Invokes((ProjectFile f) => stored = f)
                .Returns(true);

            bool ok = await app.UploadTo(controller, project, ProjectSaveOptions.PreserveExistingMetadata);

            Assert.That(ok, Is.True);
            Assert.That(ProjectFile.Encoding.GetBytes(stored.Data), Is.EqualTo(original));
        }

        [Test, Explicit("Activates in Stage 2 once Load/Save are implemented")]
        public async Task UploadTo_DefaultOptions_RestampsId2_PreservesId1AndLastUniqueId()
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
            var app = NewService(clock);
            Project original = await app.Load(ProjectDataPath);

            ProjectFile stored = null!;
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._))
                .Invokes((ProjectFile f) => stored = f)
                .Returns(true);

            await app.UploadTo(controller, original, ProjectSaveOptions.Default);
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
