using System.Text;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Catalog discovery/parsing must fail with the offending path (never a context-free crash or a silently
    /// empty catalog), decode UTF-8-without-BOM component files correctly, recover from transient failures
    /// (no permanently poisoned <c>Lazy</c>), and seed File→New defensively (skeleton high-water mark, no
    /// half-seeded project from an empty enum template).
    /// </summary>
    public class CatalogRobustnessTests
    {
        private static string T(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        // ----- CatalogReader encoding -----

        [Test]
        public void CatalogReader_Utf8WithoutBom_DecodesPerItsDeclaration()
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
                "<product_dataline id=\"_0x153\" product_identifier=\"_0x2101\" name=\"Tænd/sluk æøå\"/>");

            using var ms = new MemoryStream(bytes);
            ProjectElement body = CatalogReader.Read(ms);

            Assert.That(body.GetAttribute("name"), Is.EqualTo("Tænd/sluk æøå"),
                "a UTF-8-without-BOM catalog file must not be mojibaked through a Latin-1 decode");
        }

        // ----- discovery failure surfacing -----

        [Test]
        public void FromInstallDir_MissingSubdirectory_NamesIt()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ihc-cat-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "Products"));   // FunctionBlocks and Data missing
            try
            {
                Assert.That(() => CatalogDiscovery.FromInstallDir(dir),
                    Throws.TypeOf<DirectoryNotFoundException>().With.Message.Contains("FunctionBlocks"),
                    "a missing subdirectory must fail loudly, never yield a silently empty catalog");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        // ----- CategoryPath is catalog data, not a host path -----

        // A scanned CategoryPath must be \-separated on EVERY OS: the built-in catalog embeds "01. Lysstyring\1.1
        // Generelt" literally and every consumer splits on '\', so a /-separated value collapses a nested category
        // into one flat folder name. Path.GetDirectoryName returns the HOST separator, which made the scan produce
        // exactly that on Linux/macOS.
        //
        // White-box on purpose: reaching this through FromInstallDir needs a COMPLETE install dir (the Data\
        // File→New templates), and no such fixture exists outside a real IHC Visual installation — every test that
        // has one is install-dir-gated and therefore skips on the Linux CI leg, which is precisely the leg where
        // this defect lives. The assertion is exact on Linux/macOS and tautological on Windows (where
        // GetDirectoryName already yields '\'), so it is the non-Windows CI legs that carry it.
        [Test]
        public void ScannedCategoryPath_IsBackslashSeparated_OnEveryOS()
        {
            string root = Path.Combine(Path.GetTempPath(), "ihc-cat-root");
            string nested = Path.Combine(root, "01. Lysstyring", "1.1 Generelt", "kip.def");

            string categoryPath = CatalogDiscovery.CategoryPathFor(root, nested);

            Assert.Multiple(() =>
            {
                Assert.That(categoryPath, Is.EqualTo(@"01. Lysstyring\1.1 Generelt"),
                    "the scanned location is catalog data in the built-in catalog's own shape");
                Assert.That(categoryPath.Split(CatalogDiscovery.CategorySeparator), Has.Length.EqualTo(2),
                    "so a consumer splitting on '\\' sees the two folder levels, not one flat name");
                Assert.That(categoryPath, Does.Not.Contain("/"),
                    "no host separator survives into catalog data");
            });
        }

        [Test]
        public void ScannedCategoryPath_ForATopLevelFile_IsEmpty()
        {
            // The uncategorised case the menu builder relies on: no segments, so the product stays reachable at the
            // top of the insert menu rather than under a folder named after the scan root.
            string root = Path.Combine(Path.GetTempPath(), "ihc-cat-root");

            Assert.That(CatalogDiscovery.CategoryPathFor(root, Path.Combine(root, "kip.def")), Is.Empty);
        }

        [Test]
        public void FromInstallDir_MalformedCatalogFile_NamesThePath()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ihc-cat-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "Products"));
            Directory.CreateDirectory(Path.Combine(dir, "FunctionBlocks"));
            Directory.CreateDirectory(Path.Combine(dir, "Data"));
            string garbage = Path.Combine(dir, "Products", "garbage.def");
            File.WriteAllText(garbage, "this is not xml <");
            try
            {
                Assert.That(() => CatalogDiscovery.FromInstallDir(dir),
                    Throws.TypeOf<InvalidDataException>().With.Message.Contains("garbage.def"));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        // ----- File→New seeding -----

        private static ICatalog FakeCatalog(ProjectElement skeleton, ProjectElement enumTemplate)
        {
            var catalog = A.Fake<ICatalog>();
            A.CallTo(() => catalog.NewProjectSkeleton).Returns(skeleton);
            A.CallTo(() => catalog.BuiltInEnumerators).Returns(enumTemplate);
            return catalog;
        }

        private static ProjectElement Skeleton(string lastUniqueId, int groupCounter) =>
            Node("utcs_project", null, new[] { ("last_unique_id", lastUniqueId) },
                Node("enum_definitions", T("enum_definitions", 0x30), new[] { ("name", "Enumerator definitioner") }),
                Node("groups", T("groups", 0x20), new[] { ("name", "Lokaliteter") },
                    Node("group", T("group", groupCounter), new[] { ("name", "Stue") })));

        [Test]
        public void CreateNew_SkeletonIdsAboveItsLastUniqueId_DoNotCollide()
        {
            // The template claims last_unique_id=_0x40 but carries a group with counter 0x43 — trusted blindly,
            // the third allocation would mint a duplicate id.
            ProjectElement enumTemplate = Node("enumerator_definitions", null, System.Array.Empty<(string, string)>(),
                Node("enum_definition", T("enum_definition", 0x10), new[] { ("typeid", "_0x10"), ("name", "X") },
                    Node("enum_value", T("enum_value", 0x11), new[] { ("typeid", "_0x11"), ("name", "V") })));
            var app = new ProjectAppService(TestSetup.Settings, FakeCatalog(Skeleton("_0x40", 0x43), enumTemplate),
                new FakeTimeProvider(new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero)));

            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            ProjectValidationResult result = app.Validate(project);
            Assert.That(result.Errors, Has.None.Contains("duplicate id counter"),
                "the allocator seeds from the skeleton's true high-water mark: " + string.Join(" | ", result.Errors));
        }

        [Test]
        public void CreateNew_EmptyEnumTemplate_Throws()
        {
            ProjectElement emptyTemplate = Node("enumerator_definitions", null, System.Array.Empty<(string, string)>());
            var app = new ProjectAppService(TestSetup.Settings, FakeCatalog(Skeleton("_0x40", 0x21), emptyTemplate),
                new FakeTimeProvider(new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero)));

            Assert.That(() => app.CreateNew(new ProjectDetails("P", "I", "DK")),
                Throws.InstanceOf<InvalidDataException>().With.Message.Contains("EnumeratorDefinitions"),
                "a half-seeded project would break every catalog insert that references the built-in enums");
        }
    }
}
