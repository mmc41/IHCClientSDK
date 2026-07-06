using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The spec ch. 10 §10.5 checklist rules added on top of the original validator: programming-reference
    /// locality, embedded-constant agreement, scene-row bijection, dataline addressing, resource_enum
    /// typedef/inivalue consistency, last_unique_id well-formedness and version gating — plus the structured
    /// finding shape (severity/rule-id/locator) and the error/warning split (warnings never invalidate).
    /// Authentic vendor files must stay error-free under every new rule.
    /// </summary>
    public class ValidatorCoverageTests
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

        private static string T(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static (string, string)[] A(params (string, string)[] attrs) => attrs;

        private static ProjectElement FunctionBlock(int baseCounter, string name, ProjectElement[] inputs, ProjectElement? eventLeaf)
        {
            ProjectElement events = Node("events", T("events", baseCounter + 5), A(("name", "E")),
                eventLeaf is null ? System.Array.Empty<ProjectElement>() : new[] { eventLeaf });
            return Node("functionblock", T("functionblock", baseCounter), A(("name", name)),
                Node("inputs", T("inputs", baseCounter + 1), A(("name", "I")), inputs),
                Node("outputs", T("outputs", baseCounter + 2), A(("name", "O"))),
                Node("settings", T("settings", baseCounter + 3), A(("name", "S"))),
                Node("internalsettings", T("internalsettings", baseCounter + 4), A(("name", "IS"))),
                Node("programs", T("programs", baseCounter + 6), A(("name", "P")),
                    Node("program_simple", T("program_simple", baseCounter + 7), A(("name", "PS")),
                        events,
                        Node("actions", T("actions", baseCounter + 8), A(("name", "A"))))));
        }

        private static Project WithRoot(params ProjectElement[] rootChildren) => new(
            Node("utcs_project", null,
                A(("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0xfff")),
                rootChildren));

        // ----- authentic files stay error-free under every new rule -----

        [TestCase("Project1-SimpelWired.vis")]
        [TestCase("project2-CustomBlock.vis")]
        [TestCase("project3-KompleksWired.vis")]
        public async Task Validate_AuthenticVendorFile_HasNoErrors(string oracle)
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load("testdata/" + oracle);

            ProjectValidationResult result = app.Validate(project);

            Assert.That(result.IsValid, Is.True, "errors: " + string.Join(" | ", result.Errors));
        }

        // ----- programming-reference locality + embedded constants -----

        [Test]
        public void Validate_CrossFunctionBlockLink1_IsReported()
        {
            ProjectElement foreignPin = Node("resource_input", T("resource_input", 0x70), A(("name", "Kip")));
            ProjectElement eventLeaf = Node("event", T("event", 0x90),
                A(("name", "E1"), ("link1", T("resource_input", 0x70))));
            Project project = WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")),
                        FunctionBlock(0x60, "FB A", new[] { foreignPin }, eventLeaf: null),
                        FunctionBlock(0x80, "FB B", System.Array.Empty<ProjectElement>(), eventLeaf))));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("link1") && e.Contains("outside its function")),
                    Is.True, "errors: " + string.Join(" | ", result.Errors));
            });
        }

        [Test]
        public void Validate_EmbeddedConstantNotReferencedByLink2_IsReported()
        {
            ProjectElement operand = Node("resource_enum", T("resource_enum", 0x91),
                A(("name", "Konst"), ("typedef", "_0x0"), ("inivalue", "_0x0")));
            ProjectElement condition = Node("condition", T("condition", 0x90),
                A(("name", "C1"), ("link2", "_0x9928")), operand);   // link2 names a different id than the operand
            // place the condition inside a valid FB skeleton via the programs container
            ProjectElement fb = Node("functionblock", T("functionblock", 0x60), A(("name", "FB")),
                Node("inputs", T("inputs", 0x61), A(("name", "I")),
                    Node("resource_input", "_0x9928", A(("name", "P")))),
                Node("outputs", T("outputs", 0x62), A(("name", "O"))),
                Node("settings", T("settings", 0x63), A(("name", "S"))),
                Node("internalsettings", T("internalsettings", 0x64), A(("name", "IS"))),
                Node("programs", T("programs", 0x65), A(("name", "P")),
                    Node("program_simple", T("program_simple", 0x66), A(("name", "PS")),
                        Node("events", T("events", 0x67), A(("name", "E"))),
                        Node("actions", T("actions", 0x68), A(("name", "A")), condition))));
            Project project = WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")), fb)));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.That(result.Errors.Any(e => e.Contains("embedded constant") && e.Contains("link2")),
                Is.True, "errors: " + string.Join(" | ", result.Errors));
        }

        // ----- scene bijection -----

        [Test]
        public void Validate_SceneRowLinkingToMissingRow_IsReported()
        {
            Project project = WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")),
                        Node("product_dataline", T("product_dataline", 0x51), A(("product_identifier", "_0x2202"), ("name", "P")),
                            Node("dataline_output", T("dataline_output", 0x52), A(("name", "Udgang"))),
                            Node("scenes", T("scenes", 0x53), A(("name", "Scenarier"), ("scene_resource", T("dataline_output", 0x52))),
                                Node("scene_link", T("scene_link", 0x54), A(("name", "S"), ("link", "_0xdead49"))))))));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.That(result.Errors.Any(e => e.Contains("scene") && e.Contains("_0xdead49")),
                Is.True, "errors: " + string.Join(" | ", result.Errors));
        }

        // ----- dataline addressing -----

        [Test]
        public void Validate_DuplicateAndOutOfRangeDatalineAddresses_AreReported()
        {
            Project project = WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")),
                        Node("product_dataline", T("product_dataline", 0x51), A(("product_identifier", "_0x2101"), ("name", "P1")),
                            Node("dataline_input", T("dataline_input", 0x52), A(("name", "A"), ("address_dataline", "_0x5"))),
                            Node("dataline_input", T("dataline_input", 0x53), A(("name", "B"), ("address_dataline", "_0x5"))),
                            Node("dataline_input", T("dataline_input", 0x54), A(("name", "C"), ("address_dataline", "_0x9c")))))));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.Multiple(() =>
            {
                Assert.That(result.Errors.Any(e => e.Contains("duplicates the address")), Is.True,
                    "errors: " + string.Join(" | ", result.Errors));
                Assert.That(result.Errors.Any(e => e.Contains("1–128")), Is.True,
                    "errors: " + string.Join(" | ", result.Errors));
            });
        }

        // ----- resource_enum consistency -----

        [Test]
        public void Validate_InivalueFromADifferentEnum_IsReported()
        {
            ProjectElement defA = Node("enum_definition", T("enum_definition", 0x41), A(("name", "A")),
                Node("enum_value", T("enum_value", 0x42), A(("name", "A1"))));
            ProjectElement defB = Node("enum_definition", T("enum_definition", 0x43), A(("name", "B")),
                Node("enum_value", T("enum_value", 0x44), A(("name", "B1"))));
            ProjectElement consumer = Node("resource_enum", T("resource_enum", 0x91),
                A(("name", "Valg"), ("typedef", T("enum_definition", 0x41)), ("inivalue", T("enum_value", 0x44))));
            Project project = WithRoot(
                Node("enum_definitions", T("enum_definitions", 0x30), A(("name", "E")), defA, defB),
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")), consumer)));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.That(result.Errors.Any(e => e.Contains("inivalue") && e.Contains("not a value of")),
                Is.True, "errors: " + string.Join(" | ", result.Errors));
        }

        // ----- root invariants + structured shape -----

        [Test]
        public void Validate_MalformedLastUniqueId_IsReported()
        {
            Project project = new(Node("utcs_project", null,
                A(("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0xzz"))));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.That(result.Errors.Any(e => e.Contains("last_unique_id") && e.Contains("_0xzz")), Is.True,
                "errors: " + string.Join(" | ", result.Errors));
        }

        [Test]
        public void Validate_VersionMajorAboveFour_IsReported()
        {
            Project project = new(Node("utcs_project", null,
                A(("version_major", "5"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x40"))));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.That(result.Errors.Any(e => e.Contains("version_major") && e.Contains("above 4")), Is.True,
                "errors: " + string.Join(" | ", result.Errors));
        }

        [Test]
        public void Validate_WarningsAlone_LeaveTheProjectValid()
        {
            // A minimal authored project: structurally sound but without the seven fixed root children — a
            // vendor-tolerated deviation that must surface as a warning, not an error.
            Project project = WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")))));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.True, "errors: " + string.Join(" | ", result.Errors));
                Assert.That(result.Warnings, Is.Not.Empty, "the root-children deviation is reported as a warning");
                Assert.That(result.Findings.Any(f => f.RuleId == "root-children"), Is.True);
            });
        }

        [Test]
        public void Findings_CarryRuleIdAndLocator()
        {
            Project project = WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")),
                        Node("product_dataline", T("product_dataline", 0x51),
                            A(("product_identifier", "_0x2202"), ("name", "P")),
                            Node("scenes", T("scenes", 0x53), A(("name", "S"), ("scene_resource", "_0xdead52")))))));

            ProjectValidationResult result = ProjectValidator.Validate(project);

            ProjectValidationFinding dangling = result.Findings.Single(f => f.RuleId == "idref-dangling");
            Assert.That(dangling.Locator, Is.EqualTo(T("scenes", 0x53)),
                "a GUI navigates to the offending element via the locator");
        }
    }
}
