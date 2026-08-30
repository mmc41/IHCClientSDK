using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T052 — the five NAMING rows, and the two exclusions without which they would report ordinary vendor content.
    ///
    /// <para><b>The gates this suite owes, both measured rather than argued.</b> The module rack
    /// (<c>dataline_*_modules</c> and the <c>dataline_*_module</c> rows inside it) ships unnamed in all 45
    /// occurrences across the corpus, and a <c>resource_*</c> element inside a <c>case_action</c> is a literal
    /// VALUE rather than a variable — all six unnamed <c>resource_*</c> elements in the corpus are of that kind.
    /// Each is asserted here from both sides: the excluded shape reports nothing, and a reporting shape in the same
    /// tree proves the rule was running.</para>
    ///
    /// <para><b>The insert-name reconstruction is the row's whole mechanism</b>, so it is tested against the case
    /// that would break a sloppier reading: <i>Kip tænd sluk (lokalt tilpasset)</i> CONTAINS its master name but is
    /// not equal to <c>{master_type}.{master_version}. {master_name}</c>, and a renamed block must stay quiet.</para>
    ///
    /// <para><b>And these are the first rows whose findings leave the engine</b>: the DOCUMENTATION category is
    /// what the Fuld report's appendix renders, which is why <see cref="TheseRowsReachTheFuldReportAppendix"/>
    /// asserts the category on all five and the label in a generated report.</para>
    /// </summary>
    [TestFixture]
    public sealed class NamingRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static ProjectValidationFinding Single(Project project, string ruleId) =>
            Validate(project).Findings.Single(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        // ── name-empty ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void EveryNameableKindIsReportedWhenItCarriesNoName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Named(name: null), "name-empty"), Is.EqualTo(5),
                    "a locality, a product, a terminal, a block and a block variable — one finding each");
                Assert.That(Count(Named(name: "   "), "name-empty"), Is.EqualTo(5),
                    "whitespace prints as nothing in a report, so the validator reads it the way the dialog does");
                Assert.That(Count(Named(name: "Navngivet"), "name-empty"), Is.Zero);
                Assert.That(Message(Named(name: null), "name-empty"), Is.EqualTo("Mangler Navn"),
                    "a fixed label in the register the documentation appendix already uses");
            });
        }

        /// <summary>
        /// The first measured exclusion: the module rack. The tree carries the whole documentation-modules shape
        /// unnamed — as every authentic file does — beside ONE unnamed product, so the count proves both that the
        /// rack is skipped and that the rule ran.
        /// </summary>
        [Test]
        public void TheModuleRackIsNeverReportedThoughItIsAlwaysUnnamed()
        {
            Project project = Tree.WithRoot(
                Tree.Node("documentation_modules", Token("documentation_modules", 0x10), [],
                    Tree.Node("dataline_input_modules", Token("dataline_input_modules", 0x11), [],
                        Tree.Node("dataline_input_module", Token("dataline_input_module", 0x12), [])),
                    Tree.Node("dataline_output_modules", Token("dataline_output_modules", 0x13), [],
                        Tree.Node("dataline_output_module", Token("dataline_output_module", 0x14), []))),
                Locality("Stue", Product(0x50, name: null)));

            Assert.That(Count(project, "name-empty"), Is.EqualTo(2),
                "the unnamed product and its unnamed terminal — and nothing from the four unnamed rack elements "
                + "around them");
        }

        /// <summary>
        /// The second measured exclusion: a <c>resource_*</c> element is a VARIABLE only where a block declares
        /// it. The same unnamed element appears twice in this tree — once as a case value, once declared — and only
        /// the declaration is a missing name.
        /// </summary>
        [Test]
        public void AProgramOperandIsNotAVariableMissingItsName()
        {
            Project operandOnly = Block(0x70, variables: [], programs: [CaseAction(0x90)]);
            Project declared = Block(0x70,
                variables: [Tree.Node("resource_enum", Token("resource_enum", 0x80), [])], programs: []);

            Assert.Multiple(() =>
            {
                Assert.That(Count(operandOnly, "name-empty"), Is.Zero,
                    "a literal case value is unnamed because it is a value, which is how the vendor stores it");
                Assert.That(Count(declared, "name-empty"), Is.EqualTo(1),
                    "the same element declared by the block IS a variable with no name");
            });
        }

        // ── name-default ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ABlockStillAtItsInsertNameIsReportedAndARenamedOneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(LibraryBlock("1.1.01.e. Kip tænd sluk"), "name-default"), Is.EqualTo(1),
                    "{master_type}.{master_version}. {master_name} — reconstructed from the block itself");
                Assert.That(Count(LibraryBlock("Kip tænd sluk (lokalt tilpasset)"), "name-default"), Is.Zero,
                    "a renamed block CONTAINS its master name, which is why equality and not containment is the "
                    + "test");
                Assert.That(Count(LibraryBlock("1.1.01.f. Kip tænd sluk"), "name-default"), Is.Zero,
                    "another version's insert name is not this block's");
                Assert.That(Single(LibraryBlock("1.1.01.e. Kip tænd sluk"), "name-default").Message,
                    Is.EqualTo("Uændret standardnavn"));
            });
        }

        /// <summary>
        /// The VERSIONLESS library form, measured in T055 and widened in T055b: two families ship a
        /// <c>master_type</c> and no <c>master_version</c>, and their insert name is
        /// <c>{master_type}. {master_name}</c>. Requiring a version made this row silent about 18 corpus blocks.
        /// </summary>
        [Test]
        public void AVersionlessLibraryBlockAtItsInsertNameIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(VersionlessBlock("4.1.01. AND (\"Og\"- blok)"), "name-default"), Is.EqualTo(1),
                    "the shape of `4.1.01. AND (\"Og\"- blok)`, which project3 holds and this row used to miss");
                Assert.That(Count(VersionlessBlock("AND-blok, stue"), "name-default"), Is.Zero,
                    "and a renamed one is still quiet — the widening did not cost the rename test");
                Assert.That(Count(VersionlessBlock("4.1.01.AND (\"Og\"- blok)"), "name-default"), Is.Zero,
                    "the form is type, full stop, SPACE, name — not a run-together");
            });
        }

        [Test]
        public void TheEmptyBlockTemplateAndTheDefaultLocalityNameAreReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(0x70, [], [], name: "Tom blok"), "name-default"), Is.EqualTo(1),
                    "the empty-block template's own name, as the catalog's fb.def writes it");
                Assert.That(Count(Block(0x70, [], [], name: "Trappelys"), "name-default"), Is.Zero);
                Assert.That(Count(Tree.WithRoot(Locality("Lokalitet")), "name-default"), Is.EqualTo(1),
                    "a locality never renamed after insertion");
                Assert.That(Count(Tree.WithRoot(Locality("Stue")), "name-default"), Is.Zero);
                Assert.That(Count(Tree.WithRoot(Locality("Lokalitet 2")), "name-default"), Is.Zero,
                    "a locality the author numbered has been touched");
            });
        }

        [Test]
        public void AnAuthoredBlockCarryingNoMasterDataHasNoTemplateNameToBeAt()
        {
            Project authored = Tree.WithRoot(Locality("Stue",
                Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "1.1.01.e. Kip tænd sluk")],
                    [.. Sections([], [])])));

            Assert.That(Count(authored, "name-default"), Is.Zero,
                "no master_type/version/name to reconstruct an insert name from, so nothing is claimed");
        }

        // ── name-duplicate-siblings ─────────────────────────────────────────────────────────────────

        [Test]
        public void TwoSiblingsSharingANameAreOneFindingWithTheFirstAsRelated()
        {
            Project siblings = Tree.WithRoot(Locality("Stue",
                Product(0x50, "Loftlampe"), Product(0x51, "Loftlampe"), Product(0x52, "Stikkontakt")));

            Assert.Multiple(() =>
            {
                Assert.That(Count(siblings, "name-duplicate-siblings"), Is.EqualTo(1),
                    "ONE fault at two sites, not two findings");
                Assert.That(Shape("name-duplicate-siblings"), Is.EqualTo(FindingShape.PrimaryWithRelated),
                    "the first holder rides along as a related location, which is what the reader compares against");
                Assert.That(Single(siblings, "name-duplicate-siblings").Message, Is.EqualTo("Dobbelt navn"));
            });
        }

        [Test]
        public void TheSameNameInTwoLocalitiesIsHowInstallationsAreNamed()
        {
            Project rooms = Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")], Product(0x50, "Loftlampe")),
                    Tree.Node("group", Token("group", 0x22), [("name", "Bad")], Product(0x51, "Loftlampe"))));

            Assert.That(Count(rooms, "name-duplicate-siblings"), Is.Zero,
                "two rooms may each hold a Loftlampe; project-wide would report ordinary practice as a fault");
        }

        [Test]
        public void TwoUnnamedSiblingsAreTwoMissingNamesRatherThanACollision()
        {
            Project unnamed = Tree.WithRoot(Locality("Stue", Product(0x50, null), Product(0x51, null)));

            Assert.Multiple(() =>
            {
                Assert.That(Count(unnamed, "name-duplicate-siblings"), Is.Zero);
                Assert.That(Count(unnamed, "name-empty"), Is.EqualTo(4),
                    "two products and the terminal under each: four missing names, no collision");
            });
        }

        // ── name-id-code-duplicate and name-cable-number-duplicate ──────────────────────────────────

        [Test]
        public void TwoProductsSharingAnIdentificationCodeAreReportedProjectWide()
        {
            Project shared = TwoProducts(("documentation_tag", "ID-7"), ("documentation_tag", "ID-7"));
            Project distinct = TwoProducts(("documentation_tag", "ID-7"), ("documentation_tag", "ID-8"));
            Project blank = TwoProducts(("documentation_tag", ""), ("documentation_tag", ""));

            Assert.Multiple(() =>
            {
                Assert.That(Count(shared, "name-id-code-duplicate"), Is.EqualTo(1),
                    "the two products sit in DIFFERENT localities: a code identifies one product documentation-wide");
                Assert.That(Shape("name-id-code-duplicate"), Is.EqualTo(FindingShape.PrimaryWithRelated));
                Assert.That(Single(shared, "name-id-code-duplicate").Message, Is.EqualTo("Dobbelt Id-kode"));
                Assert.That(Count(distinct, "name-id-code-duplicate"), Is.Zero);
                Assert.That(Count(blank, "name-id-code-duplicate"), Is.Zero,
                    "two blanks are two missing codes — doc-documentation-tag's finding, not a collision");
            });
        }

        [Test]
        public void TwoHoldersSharingACableNumberAreReported()
        {
            Project shared = TwoProducts(("cablenumber", "K-7"), ("cablenumber", "K-7"));
            Project distinct = TwoProducts(("cablenumber", "K-7"), ("cablenumber", "K-8"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(shared, "name-cable-number-duplicate"), Is.EqualTo(1));
                Assert.That(Single(shared, "name-cable-number-duplicate").Message,
                    Is.EqualTo("Dobbelt Kabelnummer"));
                Assert.That(Count(distinct, "name-cable-number-duplicate"), Is.Zero);
                Assert.That(Count(TwoProducts(("cablenumber", ""), ("cablenumber", "")),
                    "name-cable-number-duplicate"), Is.Zero);
            });
        }

        // ── the report face ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The property that makes this task the first one to move committed report oracles: all five rows are
        /// DOCUMENTATION, and the Fuld report renders that category as its appendix. Asserted end to end on the
        /// fixture whose oracles changed.
        /// </summary>
        [Test]
        public async System.Threading.Tasks.Task TheseRowsReachTheFuldReportAppendix()
        {
            string[] codes =
            [
                "name-empty", "name-default", "name-duplicate-siblings", "name-id-code-duplicate",
                "name-cable-number-duplicate",
            ];
            var app = new ProjectAppService(TestSetup.Settings);
            Project project = Authentic("project5-Dokumentation.vis");
            using var full = new MemoryStream();
            using var standard = new MemoryStream();

            await app.GenerateReport(project, ReportKind.Functions, ReportMode.Full, ReportMimeTypes.PlainText, full);
            await app.GenerateReport(
                project, ReportKind.Functions, ReportMode.Standard, ReportMimeTypes.PlainText, standard);

            Assert.Multiple(() =>
            {
                foreach (string code in codes)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry),
                        Is.True, code);
                    Assert.That(entry.Category, Is.EqualTo(ValidationCategory.Documentation), code);
                }

                Assert.That(Text(full), Does.Contain("Uændret standardnavn"),
                    "the Fuld appendix renders the documentation findings, which is why 12 oracles moved");
                Assert.That(Text(standard), Does.Not.Contain("Uændret standardnavn"),
                    "and Standard mode has no appendix, which is why the 12 std-* oracles did not move");
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Text(MemoryStream stream) => System.Text.Encoding.UTF8.GetString(stream.ToArray());

        private static FindingShape Shape(string code) =>
            ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
                ? entry.Shape
                : throw new InvalidDataException(code);

        /// <summary>
        /// One of each nameable kind — a locality, a product, a terminal, a block and a block variable — all
        /// carrying <paramref name="name"/>, or none of them carrying a name at all.
        /// </summary>
        private static Project Named(string? name) =>
            Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), Name(name),
                        Tree.Node("product_dataline", Token("product_dataline", 0x50),
                            [("product_identifier", "_0x2202"), .. Name(name)],
                            Tree.Node("dataline_output", Token("dataline_output", 0x51), Name(name))),
                        Tree.Node("functionblock", Token("functionblock", 0x70), Name(name),
                            [.. Sections(
                                [Tree.Node("resource_flag", Token("resource_flag", 0x80), Name(name))], [])]))));

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(TestSetup.Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static (string, string)[] Name(string? name) => name is null ? [] : [("name", name)];

        private static ProjectElement Locality(string? name, params ProjectElement[] contents) =>
            Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), Name(name), contents));

        private static ProjectElement Product(int at, string? name, params (string, string)[] documentation) =>
            Tree.Node("product_dataline", Token("product_dataline", at),
                [("product_identifier", "_0x2202"), .. Name(name), .. documentation],
                Tree.Node("dataline_output", Token("dataline_output", at + 0x100), Name(name is null ? null : "Udgang")));

        private static ImmutableArray<ProjectElement> Sections(
            ProjectElement[] variables, ProjectElement[] programs) =>
            [
                Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")]),
                Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")], variables),
                Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")], programs),
            ];

        private static Project Block(
            int at, ProjectElement[] variables, ProjectElement[] programs, string? name = "Blok") =>
            Tree.WithRoot(Locality("Stue",
                Tree.Node("functionblock", Token("functionblock", at), Name(name),
                    [.. Sections(variables, programs)])));

        /// <summary>A library block from a family that ships NO version: its insert name is `{type}. {name}`.</summary>
        private static Project VersionlessBlock(string name) =>
            Tree.WithRoot(Locality("Stue",
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [("name", name), ("master_type", "4.1.01"), ("master_name", "AND (\"Og\"- blok)")],
                    [.. Sections([], [])])));

        /// <summary>A library block placed from the catalogue, carrying the master data its insert name is built from.</summary>
        private static Project LibraryBlock(string name) =>
            Tree.WithRoot(Locality("Stue",
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [
                        ("name", name), ("master_type", "1.1.01"), ("master_version", "e"),
                        ("master_name", "Kip tænd sluk"),
                    ],
                    [.. Sections([], [])])));

        /// <summary>An unnamed enum operand stored as a case value — the shape all six corpus occurrences take.</summary>
        private static ProjectElement CaseAction(int at) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", "Program")],
                Tree.Node("actions", Token("actions", at + 1), [("name", "Kommandoer"), ("type", "_0x2")],
                    Tree.Node("case_action", Token("case_action", at + 2), [("name", "Case")],
                        Tree.Node("resource_enum", Token("resource_enum", at + 3), []))));

        /// <summary>Two products in two DIFFERENT localities, each carrying the given documentation attribute.</summary>
        private static Project TwoProducts((string, string) first, (string, string) second) =>
            Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Product(0x50, "Stikkontakt", first)),
                    Tree.Node("group", Token("group", 0x22), [("name", "Bad")],
                        Product(0x51, "Loftlampe", second))));
    }
}
