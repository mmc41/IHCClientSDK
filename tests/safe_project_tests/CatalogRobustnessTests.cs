using System.Collections.Immutable;
using System.Text;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Catalog discovery/parsing must fail with the offending path (never a context-free crash or a silently
    /// empty catalog), decode UTF-8-without-BOM component files correctly, recover from transient failures
    /// (no permanently poisoned <c>Lazy</c>), and seed File→New defensively (skeleton high-water mark, no
    /// half-seeded project from an empty enum template).
    /// </summary>
    public class CatalogRobustnessTests
    {
        private static ProjectElement Node(string tag, string? id, (string, string)[] attrs, params ProjectElement[] children)
        {
            ElementId? parsed = id is not null && ElementId.TryParse(id, out ElementId p) ? p : null;
            var bag = ImmutableArray.CreateBuilder<(string, string)>();
            if (id is not null)
            {
                bag.Add(("id", id));
            }
            bag.AddRange(attrs);
            return new ProjectElement(tag, parsed, bag.ToImmutable(), children.ToImmutableArray());
        }

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

        // ----- the lazy catalog recovers once the install dir appears -----

        [Test]
        public void GetAvailableProducts_TransientFailure_IsRetriedOnceTheInstallDirExists()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ihc-cat-" + Guid.NewGuid().ToString("N"));
            var app = new ProjectAppService(new IhcSettings { IhcVisualInstallDir = dir });
            try
            {
                Assert.That(() => app.GetAvailableProducts(), Throws.TypeOf<DirectoryNotFoundException>(),
                    "first call: the dir does not exist yet");

                WriteMinimalInstallDir(dir);

                Assert.That(app.GetAvailableProducts(), Is.Empty,
                    "the first failure must not permanently poison the service instance");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        private static void WriteMinimalInstallDir(string dir)
        {
            Directory.CreateDirectory(Path.Combine(dir, "Products"));
            Directory.CreateDirectory(Path.Combine(dir, "FunctionBlocks"));
            Directory.CreateDirectory(Path.Combine(dir, "Data"));
            const string prolog = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n";
            File.WriteAllText(Path.Combine(dir, "Data", "NewDoc.idf"),
                prolog + "<utcs_project last_unique_id=\"_0x40\"><enum_definitions id=\"_0x3046\" name=\"E\"/>" +
                "<groups id=\"_0x2031\" name=\"L\"/></utcs_project>",
                Encoding.Latin1);
            File.WriteAllText(Path.Combine(dir, "Data", "EnumeratorDefinitions.def"),
                prolog + "<enumerator_definitions><enum_definition id=\"_0x147\" typeid=\"_0x10\" name=\"X\">" +
                "<enum_value id=\"_0x248\" typeid=\"_0x11\" name=\"V\"/></enum_definition></enumerator_definitions>",
                Encoding.Latin1);
            File.WriteAllText(Path.Combine(dir, "Data", "fb.def"),
                prolog + "<functionblock id=\"_0x129\" name=\"Tom blok\"/>",
                Encoding.Latin1);
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
