using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;
using static Ihc.Vis.Tests.RuleProbe;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T053 — the four remaining DOCUMENTATION rows, two of which are scoped by their own stated CONSEQUENCE.
    ///
    /// <para><b>The claim this suite has to carry</b> is that <c>doc-project-info-blank</c> reports a project with
    /// NO masthead information rather than one with an empty customer block. The literal "project, customer or
    /// installer" reading fires on 15 of the 20 corpus files, because the vendor leaves <c>customer_info</c> blank
    /// in nearly all of them — so <see cref="AnEmptyCustomerBlockAloneIsNotAFinding"/> is the test that keeps the
    /// row off ordinary vendor content, and the row's own consequence ("EVERY masthead renders <c>--</c>") is what
    /// justifies it.</para>
    ///
    /// <para><b>And <c>doc-no-enduser-products</c> needs its guard tested from the empty side</b>: a project with no
    /// products cannot produce a full end-user report either, but it is unfinished rather than
    /// under-documented.</para>
    /// </summary>
    [TestFixture]
    public sealed class DocumentationCompletenessRulesTests
    {
        // ── name-power-group-variant ────────────────────────────────────────────────────────────────

        [Test]
        public void ALightGroupSpelledTwoWaysReportsTheSecondSpelling()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(LightGroups("Stue", "stue"), "name-power-group-variant"), Is.EqualTo(1),
                    "case alone: one physical circuit under two headings");
                Assert.That(Count(LightGroups("Stue", " Stue "), "name-power-group-variant"), Is.EqualTo(1),
                    "spacing alone");
                Assert.That(Count(LightGroups("Stue  nord", "Stue nord"), "name-power-group-variant"), Is.EqualTo(1),
                    "an inner run of spaces collapses to one");
                Assert.That(Message(LightGroups("Stue", "stue"), "name-power-group-variant"),
                    Is.EqualTo("Afvigende stavning af lysgruppe"));
            });
        }

        [Test]
        public void DeliberatelyDistinctGroupNamesAreNotVariants()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(LightGroups("Stue", "Stuen"), "name-power-group-variant"), Is.Zero,
                    "a different word is a different group, which the row's disagreement column allows");
                Assert.That(Count(LightGroups("Stue", "Stue"), "name-power-group-variant"), Is.Zero,
                    "one spelling used twice is how a light group is supposed to look");
                Assert.That(Count(LightGroups("Stue", ""), "name-power-group-variant"), Is.Zero,
                    "a blank light group is doc-power-group's finding, not a spelling variant");
            });
        }

        // ── name-note-missing ───────────────────────────────────────────────────────────────────────

        [Test]
        public void AnInputWithoutANoteIsReportedAndOtherPinsAreNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(inputNote: null), "name-note-missing"), Is.EqualTo(1));
                Assert.That(Count(Block(inputNote: "   "), "name-note-missing"), Is.EqualTo(1),
                    "whitespace prints as nothing in the note column");
                Assert.That(Count(Block(inputNote: "Tænder lyset"), "name-note-missing"), Is.Zero);
                Assert.That(Message(Block(inputNote: null), "name-note-missing"), Is.EqualTo("Mangler Note"));
            });
        }

        [Test]
        public void AnUnnotedOutputOrInternalVariableIsNotThisRow()
        {
            Project block = Block(inputNote: "Tænder lyset", noteOtherPins: false);

            Assert.That(Count(block, "name-note-missing"), Is.Zero,
                "the row names INPUTS: the output and the internal variable in this block carry no note either, "
                + "and neither is reported");
        }

        [Test]
        public void EveryLibraryBlockInputInTheCorpusCarriesItsNote()
        {
            Assert.That(Count(Authentic("project3-KompleksWired.vis"), "name-note-missing"), Is.Zero,
                "32 library-block inputs, every one carrying the vendor's own note — which is why this row reports "
                + "hand-authored blocks and nothing else");
        }

        // ── doc-project-info-blank ──────────────────────────────────────────────────────────────────

        [Test]
        public void AProjectWithNoMastheadInformationAtAllIsReportedOnce()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Mastheads(project: null, customer: null, installer: null),
                    "doc-project-info-blank"), Is.EqualTo(1), "no block present at all");
                Assert.That(Count(Mastheads(project: "", customer: "", installer: ""),
                    "doc-project-info-blank"), Is.EqualTo(1), "three blocks, every value blank");
                Assert.That(Message(Mastheads(null, null, null), "doc-project-info-blank"),
                    Is.EqualTo("Mangler projektoplysninger"));
            });
        }

        /// <summary>
        /// The row's scope, and the reason it is not the literal "or": an empty customer block is the ordinary
        /// state of an installer's own project — 15 of the 20 corpus files are exactly that.
        /// </summary>
        [Test]
        public void AnEmptyCustomerBlockAloneIsNotAFinding()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Mastheads(project: "Villa Nord", customer: null, installer: null),
                    "doc-project-info-blank"), Is.Zero, "one filled block is enough for a masthead to say something");
                Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "doc-project-info-blank"), Is.Zero,
                    "an authentic project whose customer block is blank and whose project block is not");
                Assert.That(Count(Authentic("project5-Dokumentation.vis"), "doc-project-info-blank"), Is.Zero);
            });
        }

        // ── doc-no-enduser-products ─────────────────────────────────────────────────────────────────

        [Test]
        public void AProjectWhoseProductsAreAllKeptOutOfTheEnduserReportIsReportedOnce()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Enduser(products: 3, flagged: 0), "doc-no-enduser-products"), Is.EqualTo(1));
                Assert.That(Message(Enduser(3, 0), "doc-no-enduser-products"),
                    Is.EqualTo("Ingen produkter til slutbrugerdokumentation"));
                Assert.That(Count(Enduser(products: 3, flagged: 1), "doc-no-enduser-products"), Is.Zero,
                    "one flagged product is a non-empty report");
                Assert.That(Count(Enduser(products: 0, flagged: 0), "doc-no-enduser-products"), Is.Zero,
                    "a project with no products is unfinished, not under-documented — the guard the row's own "
                    + "consequence asks for");
            });
        }

        // ── the row that is NOT implemented ─────────────────────────────────────────────────────────

        /// <summary>
        /// <c>name-helpfile-missing</c> was the fifth DOC row and is deliberately not implemented: its stated
        /// consequence is false (help resolves from the block's own <c>master_type</c>), so T011 ruled it out. This
        /// asserts the absence, because a later task reading "the DOC rows" as a list would otherwise implement it
        /// as thoroughness.
        /// </summary>
        [Test]
        public void TheRuledOutHelpfileRowHasNoRule()
        {
            ProblemCode code = new("name-helpfile-missing");

            Assert.Multiple(() =>
            {
                Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True,
                    "the id stays reserved");
                Assert.That(entry.Status, Is.EqualTo(ProblemCodeStatus.RuledOut));
                Assert.That(ProjectRules.All(ProblemCatalog.Current).Any(r => r.Entry.Code == code), Is.False,
                    "a ruled-out row must have no rule registered against it");
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(TestSetup.Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static ProjectElement Locality(params ProjectElement[] contents) =>
            Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], contents));

        /// <summary>Two products, each carrying the given light-group spelling (blank means the attribute is absent).</summary>
        private static Project LightGroups(string first, string second) =>
            Tree.WithRoot(Locality(
                Product(0x50, "Loftlampe", first), Product(0x51, "Stikkontakt", second)));

        private static ProjectElement Product(int at, string name, string lightGroup, bool flagged = false) =>
            Tree.Node("product_dataline", Token("product_dataline", at),
                [
                    ("product_identifier", "_0x2202"), ("name", name),
                    .. lightGroup.Length > 0 ? new[] { ("power_group", lightGroup) } : [],
                    .. flagged ? new[] { ("enduser_report", "yes") } : [],
                ],
                Tree.Node("dataline_output", Token("dataline_output", at + 0x100), [("name", "Udgang")]));

        /// <summary>
        /// A block with one input, one output and one internal variable. The input carries
        /// <paramref name="inputNote"/>; the other two pins carry a note only when
        /// <paramref name="noteOtherPins"/> says so.
        /// </summary>
        private static Project Block(string? inputNote, bool noteOtherPins = true)
        {
            (string, string)[] other = noteOtherPins ? [("note", "Noteret")] : [];
            return Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Trappelys")],
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                        Tree.Node("resource_input", Token("resource_input", 0x80),
                            inputNote is null
                                ? [("name", "Indgang")]
                                : [("name", "Indgang"), ("note", inputNote)])),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")],
                        Tree.Node("resource_output", Token("resource_output", 0x81),
                            [("name", "Udgang"), .. other])),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                        Tree.Node("resource_flag", Token("resource_flag", 0x82), [("name", "Flag"), .. other])),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")]))));
        }

        /// <summary>
        /// The three masthead blocks. A null argument omits the block entirely; an empty string writes it with a
        /// blank value; any other value fills it.
        /// </summary>
        private static Project Mastheads(string? project, string? customer, string? installer)
        {
            ImmutableArray<ProjectElement>.Builder blocks = ImmutableArray.CreateBuilder<ProjectElement>();
            if (project is not null)
            {
                blocks.Add(Tree.Node("project_info", null, [("description", project)]));
            }

            if (customer is not null)
            {
                blocks.Add(Tree.Node("customer_info", null, [("name", customer)]));
            }

            if (installer is not null)
            {
                blocks.Add(Tree.Node("installer_info", null, [("name", installer)]));
            }

            blocks.Add(Locality());
            return Tree.WithRoot([.. blocks]);
        }

        /// <summary>A project holding <paramref name="products"/> products, the first <paramref name="flagged"/> of
        /// them marked for the end-user report.</summary>
        private static Project Enduser(int products, int flagged)
        {
            ImmutableArray<ProjectElement>.Builder contents = ImmutableArray.CreateBuilder<ProjectElement>();
            for (int i = 0; i < products; i++)
            {
                contents.Add(Product(0x50 + i, $"Produkt {i}", "Stue", flagged: i < flagged));
            }

            return Tree.WithRoot(Locality([.. contents]));
        }
    }
}
