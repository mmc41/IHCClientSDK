using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Tests for the pre-serialize validator: a real vendor file validates clean, and the checklist catches a
    /// dangling IDREF and non-ISO-8859-1 text (spec ch. 10 §10.5). Uses bundled testdata only (no install dir).
    /// </summary>
    public class ProjectValidatorTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

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

        [Test]
        public void Validate_RealVendorFile_IsClean()
        {
            using var ms = new MemoryStream(TestData.ReadBytes("Project1.vis"));
            var app = new ProjectAppService(Settings);
            Project project = app.Load(ms).GetAwaiter().GetResult();

            ProjectValidationResult result = app.Validate(project);

            Assert.That(result.IsValid, Is.True, "errors: " + string.Join(" | ", result.Errors));
        }

        [Test]
        public void Validate_DanglingSceneResource_IsReported()
        {
            // scenes@scene_resource is an IDREF; pointing it at a non-existent id must be flagged.
            ProjectElement root = Node("utcs_project", null, new[] { ("version_major", "4"), ("version_minor", "0"), ("last_unique_id", "_0x60") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") },
                        Node("product_dataline", "_0x5153", new[] { ("product_identifier", "_0x2202"), ("name", "P") },
                            Node("scenes", "_0x5349", new[] { ("name", "Scenarier"), ("scene_resource", "_0xdead52") })))));

            ProjectValidationResult result = ProjectValidator.Validate(new Project(root));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("scene_resource") && e.Contains("_0xdead52")), Is.True);
            });
        }

        [Test]
        public void Validate_NonLatin1Text_IsReported()
        {
            ProjectElement root = Node("utcs_project", null, new[] { ("version_major", "4"), ("version_minor", "0"), ("last_unique_id", "_0x40") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "€uro") })));   // € is outside ISO-8859-1

            ProjectValidationResult result = ProjectValidator.Validate(new Project(root));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("non-ISO-8859-1")), Is.True);
            });
        }

        // ----- registry-backed conformance: #REQUIRED presence + enumerated-attribute value range -----

        // A fully-formed root with every #REQUIRED attribute present and every enumerated value in range.
        private static ProjectElement ValidRoot(params (string, string)[] productDatalineAttrs) =>
            Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x60") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") },
                        Node("product_dataline", "_0x5153", productDatalineAttrs))));

        [Test]
        public void Validate_AllRequiredPresentAndEnumsInRange_IsClean()
        {
            // product_dataline with both #REQUIRED attributes (id via Node + product_identifier) and valid enums.
            ProjectValidationResult result = ProjectValidator.Validate(new Project(
                ValidRoot(("product_identifier", "_0x2202"), ("name", "P"), ("locked", "yes"), ("enduser_report", "no"))));

            Assert.That(result.IsValid, Is.True, "errors: " + string.Join(" | ", result.Errors));
        }

        [Test]
        public void Validate_MissingRequiredAttribute_IsReported()
        {
            // product_dataline@product_identifier is #REQUIRED; omitting it must be flagged.
            ProjectValidationResult result = ProjectValidator.Validate(new Project(
                ValidRoot(("name", "P"), ("locked", "yes"))));   // no product_identifier

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("product_identifier") && e.Contains("product_dataline")), Is.True);
            });
        }

        [Test]
        public void Validate_OutOfRangeEnumValue_IsReported()
        {
            // product_dataline@locked is the enumeration (yes | no); any other value must be flagged.
            ProjectValidationResult result = ProjectValidator.Validate(new Project(
                ValidRoot(("product_identifier", "_0x2202"), ("name", "P"), ("locked", "sometimes"))));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("locked") && e.Contains("sometimes")), Is.True);
            });
        }
    }
}
