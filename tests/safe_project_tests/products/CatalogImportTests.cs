using Ihc.Vis.Problems;
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

        private static string SyntheticFunctionBlock(string fileName) =>
            TestData.PathOf("functionblocks", "synthetic", fileName);

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
        public void ImportCatalogFile_FunctionBlockResolvesInsertsAndRoundTrips_WithoutInstall()
        {
            ProjectAppService app = NewApp();
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            int baseCount = app.GetAvailableFunctionBlocks().Count;

            app.ImportCatalogFile(SyntheticFunctionBlock("synthetic_fb01_toggle.ifb"));

            // Resolvable: a ".ifb" import lands among the available function blocks (not the products).
            Assert.That(app.GetAvailableFunctionBlocks(), Has.Count.EqualTo(baseCount + 1));
            FunctionBlockDefinition imported = app.GetAvailableFunctionBlocks().Single(fb => fb.MasterType == "9.1.01");

            // Insertable + round-trips: add the block to a room, save and reload structurally equal.
            ProjectEditor editor = blank.Edit();
            editor.Group("Stue").AddFunctionBlock(imported);
            Project built = editor.ToProject();

            using var ms = new MemoryStream();
            app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
            Project reloaded = app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();

            Assert.That(reloaded.Equals(built), Is.True, "imported function block round-trips, no install present");
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

                // The type changed with the identity, deliberately: InvalidDataException is SEALED, so the
                // refusal could not carry a code while remaining one. RefusedImportException : FormatException
                // is what a malformed .vis already throws, so the two file kinds now fail alike.
                Assert.That(() => app.ImportCatalogFile(defPath),
                    Throws.TypeOf<RefusedImportException>().With.Message.Contains("broken.def"),
                    "the import error names the offending file");
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Test]
        public void ImportCatalogFile_CallerEnumeratingAFolder_ImportsEveryDef()
        {
            // The SDK surface is single-file only: importing a whole folder is the caller's job — enumerate the
            // files and call ImportCatalogFile once each. This confirms that pattern covers the folder use-case.
            ProjectAppService app = NewApp();
            int baseCount = app.GetAvailableProducts().Count;
            string dir = TestData.PathOf("products", "synthetic");
            string[] defs = Directory.GetFiles(dir, "*.def", SearchOption.AllDirectories);

            foreach (string def in defs)
            {
                app.ImportCatalogFile(def);
            }

            Assert.Multiple(() =>
            {
                Assert.That(defs, Is.Not.Empty, "sanity: the synthetic product corpus is present");
                Assert.That(app.GetAvailableProducts(), Has.Count.EqualTo(baseCount + defs.Length), "all appear in the catalog");
                Assert.That(app.GetAvailableProducts().Any(p => p.ProductIdentifier == "_0x9f04"), Is.True, "a specific import resolves");
            });
        }
    }
}
