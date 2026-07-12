#nullable enable
using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Phase D runtime-import tests for <see cref="ProjectAppService"/> (Phase D3/D4), all <b>install-free</b> over a
    /// code-authored <see cref="BuiltInCatalog"/> base: a component file read at runtime must resolve through the
    /// catalog and insert + round-trip like a built-in, and the optional documentation probe (incl. the ready-made
    /// sibling-file hook) must attach help metadata. Imported-wins precedence is covered directly in
    /// <see cref="CompositeCatalogTests"/> (the catalog the service composes over).
    /// </summary>
    public class CatalogImportTests
    {
        private static string SyntheticProduct(string fileName) =>
            TestData.PathOf("products", "synthetic", fileName);

        private static ProjectAppService NewApp() =>
            new(TestSetup.Settings, new BuiltInCatalog(),
                new FakeTimeProvider(new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero)));

        [Test]
        public void ImportCatalogFile_ProductResolvesInsertsAndRoundTrips_WithoutInstall()
        {
            ProjectAppService app = NewApp();
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            int baseCount = app.GetAvailableProducts().Count;

            app.ImportCatalogFile(SyntheticProduct("synthetic_9f01_input.def"));

            // Resolvable: the imported product now appears among the available products.
            Assert.That(app.GetAvailableProducts(), Has.Count.EqualTo(baseCount + 1));
            ProductDefinition imported = app.GetAvailableProducts().Single(p => p.ProductIdentifier == "_0x9f01");

            // Insertable + round-trips: insert into a fresh project, save and reload structurally equal.
            ProjectEditor editor = blank.Edit();
            editor.Group("Stue").AddProduct(imported);
            Project built = editor.ToProject();

            using var ms = new MemoryStream();
            app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
            Project reloaded = app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();

            Assert.That(reloaded.Equals(built), Is.True, "imported product round-trips, no install present");
        }

        [Test]
        public void ImportCatalogFile_WithDocumentationProbe_AttachesSummary()
        {
            ProjectAppService app = NewApp();

            app.ImportCatalogFile(SyntheticProduct("synthetic_9f01_input.def"), _ => "Imported product help");

            ProductDefinition imported = app.GetAvailableProducts().Single(p => p.ProductIdentifier == "_0x9f01");
            Assert.That(imported.Documentation.Summary, Is.EqualTo("Imported product help"));
        }

        [Test]
        public void ImportCatalogFile_WithSiblingDocumentationProbe_ReadsSiblingSynEn()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ihc_import_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string defPath = Path.Combine(tempDir, "widget.def");
                File.Copy(SyntheticProduct("synthetic_9f01_input.def"), defPath);
                File.WriteAllText(Path.Combine(tempDir, "widget.syn_en"), "Sibling help text");

                ProjectAppService app = NewApp();
                app.ImportCatalogFile(defPath, ProjectAppService.ReadSiblingDocumentation);

                ProductDefinition imported = app.GetAvailableProducts().Single(p => p.ProductIdentifier == "_0x9f01");
                Assert.That(imported.Documentation.Summary, Is.EqualTo("Sibling help text"));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Test]
        public void ImportCatalogFile_MalformedFile_ThrowsInvalidDataException_NamingTheFile()
        {
            // Finding 14: a malformed/truncated catalog file must surface an error naming the offending file (as
            // CatalogDiscovery.ParseCatalogFile does), not a bare XmlException that hides which of hundreds failed.
            string tempDir = Path.Combine(Path.GetTempPath(), "ihc_import_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string defPath = Path.Combine(tempDir, "broken.def");
                File.WriteAllText(defPath,
                    "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<product_dataline id=\"_0x1\" name=\"X\">");   // root never closed

                ProjectAppService app = NewApp();

                Assert.That(() => app.ImportCatalogFile(defPath),
                    Throws.TypeOf<InvalidDataException>().With.Message.Contains("broken.def"),
                    "the import error names the offending file");
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Test]
        public void ImportCatalogDirectory_ImportsEveryDefAndReportsCount()
        {
            ProjectAppService app = NewApp();
            int baseCount = app.GetAvailableProducts().Count;
            string dir = TestData.PathOf("products", "synthetic");
            int expected = Directory.GetFiles(dir, "*.def", SearchOption.AllDirectories).Length;

            int imported = app.ImportCatalogDirectory(dir);

            Assert.Multiple(() =>
            {
                Assert.That(expected, Is.GreaterThan(0), "sanity: the synthetic product corpus is present");
                Assert.That(imported, Is.EqualTo(expected), "reports the number of files imported");
                Assert.That(app.GetAvailableProducts(), Has.Count.EqualTo(baseCount + expected), "all appear in the catalog");
                Assert.That(app.GetAvailableProducts().Any(p => p.ProductIdentifier == "_0x9f04"), Is.True, "a specific import resolves");
            });
        }
    }
}
