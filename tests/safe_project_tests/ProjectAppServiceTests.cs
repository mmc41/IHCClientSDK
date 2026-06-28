using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Application-service-level contracts on <see cref="ProjectAppService"/> that the format-engine tests do not
    /// cover: catalog surfacing (<see cref="ProjectAppService.GetAvailableProducts"/> /
    /// <see cref="ProjectAppService.GetAvailableFunctionBlocks"/>), the lazy-catalog guarantee (file IO never forces
    /// an IHC Visual install), the controller-bridge precondition, and the path-save overload incl. its
    /// <c>.BAK</c> backup. The byte engine itself is exercised elsewhere
    /// (<see cref="ProjectByteFidelityTests"/>, <see cref="CreateNewTests"/>).
    /// </summary>
    public class ProjectAppServiceTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero));

        private static ProjectElement EmptyBody(string tag) =>
            new(tag, null, ImmutableArray<(string, string)>.Empty, ImmutableArray<ProjectElement>.Empty);

        private static Project LoadProject1(ProjectAppService app) =>
            app.Load(new MemoryStream(TestData.ReadBytes("Project1.vis"))).GetAwaiter().GetResult();

        // ----- GetAvailableProducts / GetAvailableFunctionBlocks: the service surfaces its catalog -----

        [Test]
        public void GetAvailable_SurfacesTheInjectedCatalogDescriptors()
        {
            var products = new[]
            {
                new ProductDescriptor("_0x2101", "LK FUGA Tryk 2 tast", "Cat", EmptyBody("product_dataline")),
                new ProductDescriptor("_0x2202", "Lampeudtag", "Cat", EmptyBody("product_dataline")),
            };
            var functionBlocks = new[]
            {
                new FunctionBlockDescriptor("1.1.01", "e", "Kip tænd sluk", "1.1.01.e. Kip tænd sluk", "Cat", EmptyBody("functionblock")),
            };
            var catalog = A.Fake<ICatalog>();
            A.CallTo(() => catalog.Products).Returns(products);
            A.CallTo(() => catalog.FunctionBlocks).Returns(functionBlocks);

            var app = new ProjectAppService(Settings, catalog, Clock());

            Assert.Multiple(() =>
            {
                Assert.That(app.GetAvailableProducts(), Is.EqualTo(products));
                Assert.That(app.GetAvailableFunctionBlocks(), Is.EqualTo(functionBlocks));
            });
        }

        [Test]
        public void GetAvailable_FromInstallDir_DiscoversTheRealCatalogThroughTheService()
        {
            // The settings-only ctor builds its own lazy CatalogDiscovery from IhcVisualInstallDir; this proves
            // that production wiring end-to-end (skips when no install dir is configured).
            string dir = Settings.IhcVisualInstallDir;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Assert.Ignore($"No IHC Visual install dir configured ('{dir}'); skipping install-dir-gated test.");
            }

            var app = new ProjectAppService(Settings);

            Assert.Multiple(() =>
            {
                Assert.That(app.GetAvailableProducts(), Has.Count.GreaterThanOrEqualTo(100));
                Assert.That(app.GetAvailableProducts().Any(p => p.ProductIdentifier == "_0x2101"), Is.True,
                    "LK FUGA Tryk 2 tast is discovered");
                Assert.That(app.GetAvailableFunctionBlocks(), Has.Count.GreaterThanOrEqualTo(72));
                Assert.That(app.GetAvailableFunctionBlocks().Any(f => f.MasterType == "1.1.01"), Is.True,
                    "Kip tænd sluk is discovered");
            });
        }

        // ----- the catalog is lazy: file IO must never require an IHC Visual install -----

        [Test]
        public async Task FileIo_DoesNotForceCatalogDiscovery_EvenWithABogusInstallDir()
        {
            var settings = new IhcSettings { IhcVisualInstallDir = @"Z:\no\such\ihc-visual\dir" };
            var app = new ProjectAppService(settings);
            byte[] original = TestData.ReadBytes("Project1.vis");

            // Load + Save round-trips byte-identical without ever touching the (non-existent) catalog dir.
            Project project = await app.Load(new MemoryStream(original));
            using var ms = new MemoryStream();
            await app.Save(project, ms, ProjectSaveOptions.PreserveExistingMetadata);
            TestData.AssertBytesIdentical(original, ms.ToArray(), "file-only round-trip with bogus install dir");

            // Forcing the catalog (GetAvailableProducts) is the only thing that hits the dir — and it fails loudly,
            // confirming the round-trip above succeeded purely because discovery stayed lazy.
            Assert.That(() => app.GetAvailableProducts(), Throws.TypeOf<DirectoryNotFoundException>());
        }

        // ----- the controller bridge requires a controller-injecting ctor -----

        [Test]
        public void DownloadFrom_WithoutAController_ThrowsInvalidOperation()
        {
            var app = new ProjectAppService(Settings);   // file-only: no controller
            Assert.That(async () => await app.DownloadFrom(), Throws.InvalidOperationException);
        }

        [Test]
        public void UploadTo_WithoutAController_ThrowsInvalidOperation()
        {
            var app = new ProjectAppService(Settings);   // file-only: no controller
            Project project = new(EmptyBody("utcs_project"));
            Assert.That(async () => await app.UploadTo(project), Throws.InvalidOperationException);
        }

        // ----- Save(path) overload + the .BAK backup option -----

        [Test]
        public async Task SaveToPath_RoundTrips_AndCreateBackupRenamesThePriorFile()
        {
            var app = new ProjectAppService(Settings);
            byte[] original = TestData.ReadBytes("Project1.vis");
            Project project = LoadProject1(app);

            string path = Path.Combine(Path.GetTempPath(), "ihc-projapp-" + Guid.NewGuid().ToString("N") + ".vis");
            string backup = Path.ChangeExtension(path, ".BAK");
            try
            {
                // First (preserving) save lands byte-identical on disk.
                await app.Save(project, path, ProjectSaveOptions.PreserveExistingMetadata);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "path save is byte-identical");

                // Re-saving with CreateBackup moves the previous file to <name>.BAK before writing the new one.
                var withBackup = new ProjectSaveOptions { WriteMetadataVerbatim = true, CreateBackup = true };
                await app.Save(project, path, withBackup);

                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(backup), Is.True, "the previous file was renamed to .BAK");
                    Assert.That(File.ReadAllBytes(backup), Is.EqualTo(original), ".BAK holds the previous content");
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "the freshly written file is the preserved bytes");
                });
            }
            finally
            {
                File.Delete(path);
                File.Delete(backup);
            }
        }
    }
}
