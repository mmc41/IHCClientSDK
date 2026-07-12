using System.IO;
using System.Linq;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Tests for the pre-serialize validator: a real vendor file validates clean, and the checklist catches a
    /// dangling IDREF and non-ISO-8859-1 text (spec ch. 10 §10.5). Uses bundled testdata only (no install dir).
    /// </summary>
    public class ProjectValidatorTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        [Test]
        public void Validate_RealVendorFile_IsClean()
        {
            using var ms = new MemoryStream(TestData.ReadBytes("projects/Project1-SimpelWired.vis"));
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

        [Test]
        public void Validate_UnwiredIdRef_NullToken_IsNotFlaggedAsDangling()
        {
            // _0x0 is the null-token sentinel for an unwired IDREF (StampRequiredNullTokens stamps it,
            // ValidateSceneBijection blesses it); the generic idref-dangling rule must not contradict them.
            ProjectElement root = Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x60") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") },
                        Node("product_dataline", "_0x5153", new[] { ("product_identifier", "_0x2202"), ("name", "P") },
                            Node("scenes", "_0x5349", new[] { ("name", "S"), ("scene_resource", "_0x0") })))));

            ProjectValidationResult result = ProjectValidator.Validate(new Project(root));

            Assert.That(result.Errors.Any(e => e.Contains("dangling")), Is.False,
                "an unwired _0x0 IDREF is a legitimate authored state; errors: " + string.Join(" | ", result.Errors));
        }

        [Test]
        public void Validate_CrossBlockProgramCaseLink_IsReported()
        {
            // Finding 15: program_case@link is an FB-local switch-variable IDREF; one pointing OUTSIDE its function
            // block must be flagged. It was silently accepted while locality was gated by attribute NAME (link was
            // absent from the whitelist, and it cannot be added blindly — link_from/to_resource@link is non-local).
            ProjectElement root = Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x7000") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") },
                        Node("product_dataline", "_0x5153", new[] { ("product_identifier", "_0x2202"), ("name", "P") },
                            Node("dataline_output", "_0x900", new[] { ("name", "Out") })),   // the cross-block target
                        Node("functionblock", "_0x6001",
                            new[] { ("master_type", "9"), ("master_version", "9"), ("master_name", "FB"), ("name", "FB") },
                            Node("programs", "_0x6002", new[] { ("name", "programs") },
                                Node("program_simple", "_0x6003", new[] { ("name", "Program") },
                                    Node("actions", "_0x6004", new[] { ("name", "actions") },
                                        Node("program_case", "_0x6005", new[] { ("name", "Switch"), ("link", "_0x900") }))))))));

            ProjectValidationResult result = ProjectValidator.Validate(new Project(root));

            Assert.That(result.Errors.Any(e => e.Contains("program_case") && e.Contains("outside")), Is.True,
                "a cross-block program_case@link is a locality violation; errors: " + string.Join(" | ", result.Errors));
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
