using System;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// PARITY for the eight DOCUMENTATION rules — the only rules in the run that already reach a user, and the
    /// only ones with nothing to translate.
    ///
    /// <para><b>Why these are the strictest of the migrations.</b> Their Danish labels are printed verbatim in
    /// the full report's documentation appendix, which twenty-four oracle files pin byte for byte. Every other
    /// migrated rule moves an English sentence into its diagnostic and gains a Danish label; these have always
    /// had the label and have no English sentence at all. A single changed character here would move oracles, so
    /// the message is asserted EXACTLY rather than declared as changed.</para>
    ///
    /// <para><b>Scope by descent is load-bearing.</b> The checks once counted only top-level groups as localities
    /// and only a group's direct product children, so the appendix could omit products the report body documented
    /// in full — a missing row in a document whose whole purpose is completeness. A descendant scan visits each
    /// product once, which is also what makes the order document order.</para>
    /// </summary>
    [TestFixture]
    public sealed class DocumentationRuleParityTests
    {
        private static RuleSet Rules() =>
            RuleSet.Create(ProblemCatalog.Current, DocumentationRules.All(ProblemCatalog.Current));

        /// <summary>
        /// The one group with NO English diagnostic, asserted as such. Adding one would not be harmless: it would
        /// imply the Danish label is a translation of something, when the label has always been the whole message.
        /// </summary>
        [Test]
        public void TheEightLabelsAreDanishAlreadyAndCarryNoEnglishSentence()
        {
            (string Code, string Label)[] expected =
            [
                ("doc-documentation-tag", "Mangler Id-kode"),
                ("doc-power-group", "Mangler Lysgruppe"),
                ("doc-cabletype", "Mangler Kabeltype"),
                ("doc-cablenumber", "Mangler Kabelnummer"),
                ("doc-position", "Mangler Placering"),
                ("doc-not-linked", "Ikke forbundet"),
                ("doc-cable-colour", "Mangler Ledningsfarve"),
                ("doc-address", "Mangler Adresse"),
            ];

            Assert.Multiple(() =>
            {
                foreach ((string code, string label) in expected)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry),
                        Is.True, code);
                    Assert.That(entry.MessageTemplate, Is.EqualTo(label), code + ": pinned byte-exact by 24 oracles");
                    Assert.That(entry.Diagnostic, Is.Null, code + ": there is no English sentence to relocate");
                    Assert.That(entry.Category, Is.EqualTo(ValidationCategory.Documentation), code);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Warning), code);
                }
            });
        }

        /// <summary>
        /// The report's row order, declared where the checks are. The engine orders by document position and then
        /// by code, which is NOT this order — so a renderer reproducing the vendor's appendix reads this sequence
        /// rather than the engine's.
        /// </summary>
        [Test]
        public void TheReportRowOrderIsDeclaredAndIsNotTheEnginesOrder()
        {
            string[] declared = [.. DocumentationRules.ProductChecksInReportOrder.Select(c => c.Value)];
            string[] byCode = [.. declared.OrderBy(c => c, StringComparer.Ordinal)];

            Assert.Multiple(() =>
            {
                Assert.That(declared, Is.EqualTo(new[]
                {
                    "doc-documentation-tag", "doc-power-group", "doc-cabletype", "doc-cablenumber", "doc-position",
                }).AsCollection, "the order the vendor appendix witnesses");
                Assert.That(DocumentationRules.TerminalChecksInReportOrder.Select(c => c.Value), Is.EqualTo(new[]
                {
                    "doc-not-linked", "doc-cable-colour", "doc-address",
                }).AsCollection);

                Assert.That(declared, Is.Not.EqualTo(byCode).AsCollection,
                    "stated explicitly: sorting by code would NOT reproduce the appendix, which is why the order "
                    + "is declared rather than assumed to fall out of the engine");
            });
        }

        /// <summary>
        /// A product reached by DESCENT rather than as a direct child is still checked. This is the gap the scope
        /// once had, and it produced a report whose appendix disagreed with its own body.
        /// </summary>
        [Test]
        public void AProductInANestedLocalityAndUnderAContainerIsStillChecked()
        {
            Project project = new(Tree.Node("utcs_project", null, [],
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", [("name", "Etage"),],
                        Tree.Node("group", "_0x2122", [("name", "Stue")],
                            Tree.Node("product_dataline", "_0x5151", [("name", "Dybt")]))))));

            string[] locators = [.. new WholeProjectValidator(Rules())
                .Validate(project, ValidationProfile.Categorized).Findings
                .Select(f => f.Primary!.Locator!)
                .Distinct()];

            Assert.That(locators, Is.EqualTo(new[] { "_0x5151" }).AsCollection,
                "a nested locality is still a locality, and the product under it is still documented");
        }

        /// <summary>
        /// Whitespace is blank here, and only here. The schema's notion of required is about the attribute
        /// EXISTING; a documentation field of three spaces satisfies that and tells a reader nothing.
        /// </summary>
        [Test]
        public void AWhitespaceOnlyDocumentationFieldIsBlank()
        {
            Project project = new(Tree.Node("utcs_project", null, [],
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", [],
                        Tree.Node("product_dataline", "_0x5151",
                        [
                            ("documentation_tag", "   "), ("power_group", "L1"),
                            ("cabletype", "5G1.5"), ("cablenumber", "12"), ("position", "Loft"),
                        ])))));

            string[] produced = [.. new WholeProjectValidator(Rules())
                .Validate(project, ValidationProfile.Categorized).Findings.Select(f => f.Code.Value)];

            Assert.That(produced, Is.EqualTo(new[] { "doc-documentation-tag" }).AsCollection);
        }

        /// <summary>
        /// These eight are the reason the profile has an audience axis: a structural run must not report them,
        /// or every project mid-commissioning would fail a save gate over incomplete paperwork.
        /// </summary>
        [Test]
        public void AStructuralRunDoesNotReportDocumentationGaps()
        {
            Project project = MigrationParity.CorpusCase("authentic/project5-Dokumentation");
            WholeProjectValidator engine = new(Rules());

            Assert.Multiple(() =>
            {
                Assert.That(engine.Validate(project, ValidationProfile.ProjectOnly).Findings, Is.Empty);
                Assert.That(engine.Validate(project, ValidationProfile.Categorized).Findings, Is.Not.Empty,
                    "precondition: the case does carry documentation gaps");
            });
        }
    }
}
