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
    /// T060 — the remaining PROJECT-STRUCTURE rows, and the deliberate absence beside them.
    ///
    /// <para><b>The measurement that shaped this set</b> is <c>struct-product-no-terminals</c>: reading it as "no
    /// <c>dataline_*</c> child" reports every RS485 dimmer and every logging bus sensor in the corpus, because their
    /// wirable members are channels and measured values. Reading it as "nothing wirable at all" reports the SMS
    /// modem and nothing else. <see cref="ADimmerAndABusSensorAreNotTerminalLess"/> is that claim.</para>
    ///
    /// <para><b><c>struct-modified-stale</c> has no rule</b>, and <see cref="TheModifiedStaleRowHasNoRule"/> keeps
    /// that deliberate: <c>modified</c> is re-stamped on every save and no edit route touches it, so the condition
    /// cannot hold in a saved file.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProjectStructureRulesTests
    {
        // ── struct-product-no-terminals ─────────────────────────────────────────────────────────────

        [Test]
        public void AProductWithNothingWirableIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Product("sms_modem_settings"), "struct-product-no-terminals"), Is.EqualTo(1));
                Assert.That(Message(Product("sms_modem_settings"), "struct-product-no-terminals"),
                    Is.EqualTo("Produktet 'Produkt' har ingen klemmer."));
                Assert.That(Count(Product("dataline_input"), "struct-product-no-terminals"), Is.Zero);
            });
        }

        /// <summary>
        /// The measurement that shaped the row: a dimmer's channels and a bus sensor's measured values are what an
        /// author wires, so neither product is terminal-less.
        /// </summary>
        [Test]
        public void ADimmerAndABusSensorAreNotTerminalLess()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Product("rs485_led_dimmer_channel"), "struct-product-no-terminals"), Is.Zero,
                    "a channel is wirable");
                Assert.That(Count(Product("resource_temperature"), "struct-product-no-terminals"), Is.Zero,
                    "so is a bus sensor's measured value");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "struct-product-no-terminals"), Is.Zero,
                    "project3 holds a dimmer and two logging sensors, and reports none of them");
                Assert.That(Count(Authentic("project5-Dokumentation.vis"), "struct-product-no-terminals"),
                    Is.EqualTo(1), "only the SMS modem");
            });
        }

        // ── struct-icon-default ─────────────────────────────────────────────────────────────────────

        [Test]
        public void AnElementWithoutAnIconIsReportedOnlyWhereItsKindHasOne()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Icons(secondHasIcon: false), "struct-icon-default"), Is.Zero,
                    "no element of that kind carries an icon, so there is nothing to deviate from");
                Assert.That(Count(Icons(secondHasIcon: true), "struct-icon-default"), Is.EqualTo(1),
                    "one locality has an icon and the other does not");
                Assert.That(Message(Icons(secondHasIcon: true), "struct-icon-default"),
                    Is.EqualTo("Elementet 'Uden ikon' har ikke fået et ikon."));
            });
        }

        [Test]
        public void TheNullIconTokenCountsAsNoIcon()
        {
            Assert.That(Count(Icons(secondHasIcon: true, explicitNullToken: true), "struct-icon-default"),
                Is.EqualTo(1),
                "the DTD's default IS the null token, so carrying it explicitly is the same state as omitting it");
        }

        // ── the row that is NOT implemented ─────────────────────────────────────────────────────────

        [Test]
        public void TheModifiedStaleRowHasNoRule()
        {
            ProblemCode code = new("struct-modified-stale");

            Assert.Multiple(() =>
            {
                Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True,
                    "the id stays reserved");
                Assert.That(entry.Status, Is.EqualTo(ProblemCodeStatus.RuledOut),
                    "`modified` is re-stamped on every save and no edit route touches it, so the condition cannot "
                    + "hold in a saved file");
                Assert.That(ProjectRules.All(ProblemCatalog.Current).Any(r => r.Entry.Code == code), Is.False);
            });
        }

        // ── root-version-minor ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A file written by a newer MINOR revision of the format can carry content this model does not know:
        /// opening it is safe, but a save can silently drop that content — which is what the vendor tool's own
        /// load-time prompt warns about.
        ///
        /// <para><b>The three partitions of one predicate</b>, and the middle one is what the row is for: at the
        /// supported minor nothing is reported, above it the file is reported, and a minor that is not an integer
        /// is passed over exactly as <c>root-version</c> passes over an unparseable major.</para>
        /// </summary>
        [Test]
        public void AMinorVersionAheadOfTheSupportedOneIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Versioned("4", "1"), "root-version-minor"), Is.EqualTo(1));
                Assert.That(Count(Versioned("4", "9"), "root-version-minor"), Is.EqualTo(1));
                Assert.That(Count(Versioned("4", "0"), "root-version-minor"), Is.Zero,
                    "AT the supported minor: 4.0 is what every committed file carries");
                Assert.That(Count(Versioned("4", "x"), "root-version-minor"), Is.Zero,
                    "a minor that is not an integer is passed over, not guessed at");
                Assert.That(Message(Versioned("4", "3"), "root-version-minor"),
                    Is.EqualTo("Projektets formatversion 4.3 er nyere end den understøttede 4.0; "
                        + "ukendte oplysninger kan gå tabt ved gemning."));
            });
        }

        /// <summary>
        /// THE MAJOR GATE, and the coverage edge it deliberately leaves. The predicate requires
        /// <c>version_major == 4</c>, so a v5 file is <c>root-version</c>'s row alone and a v3 file whose minor is
        /// ahead reports nothing at all.
        ///
        /// <para>The v3 case is the deliberate one: the measured vendor contract is <i>current-or-older yes,
        /// newer no</i>, so an older major is accepted input, and reporting a minor on top of an already-superseded
        /// major would say nothing useful. It is a real coverage edge rather than an accident, which is why it is
        /// asserted here instead of merely written down.</para>
        /// </summary>
        [Test]
        public void OnlyTheSupportedMajorCarriesAMinorWorthReporting()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Versioned("5", "1"), "root-version-minor"), Is.Zero,
                    "a major ahead is root-version's row, and reporting both would say one thing twice");
                Assert.That(Count(Versioned("5", "1"), "root-version"), Is.EqualTo(1),
                    "which that row does report — the two are complementary, not overlapping");
                Assert.That(Count(Versioned("3", "7"), "root-version-minor"), Is.Zero,
                    "an OLDER major is accepted input; a minor on top of a superseded major says nothing");
                Assert.That(Count(Versioned("3", "7"), "root-version"), Is.Zero,
                    "and nothing else reports it either — the deliberate coverage edge");
            });
        }

        /// <summary>
        /// Both compared numbers are declared. The minor bound is the row's own subject; the MAJOR is declared too
        /// because the predicate compares it — a rule body carries no numeric literal, whatever the number is for.
        /// </summary>
        [Test]
        public void BothVersionBoundsAreDeclaredAsDataOnTheEntry()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("root-version-minor"),
                out ProblemCatalogEntry entry), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entry.Thresholds.Single(t => t.Name == "SupportedVersionMinor").Value, Is.Zero);
                Assert.That(entry.Thresholds.Single(t => t.Name == "SupportedVersionMajor").Value, Is.EqualTo(4));
                Assert.That(entry.Thresholds.Select(t => t.Confidence),
                    Has.All.EqualTo(ThresholdConfidence.VendorDocumented),
                    "both come from the same scanned-every-file baseline");
                Assert.That(entry.Evidence, Is.EqualTo(EvidenceMark.Unknown),
                    "matching root-version: no build newer than the evidenced one exists anywhere in the "
                    + "document set, so the state has neither been authored nor observed");
            });
        }

        // ── root-version ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE SIBLING'S BOUND IS DECLARED TOO. <c>root-version</c> compares the major just as
        /// <c>root-version-minor</c> does, so the number it compares against is data on its entry rather than a
        /// literal in the rule — the rule body carries no version number at all.
        ///
        /// <para><b>And the two rows declare ONE bound, not two.</b> They partition this number between them:
        /// ABOVE it is this row, AT it is the sibling. Two independently written figures would not partition it
        /// — raise one alone and a major becomes either reported twice or reported by neither, which is exactly
        /// the gap <see cref="OnlyTheSupportedMajorCarriesAMinorWorthReporting"/> asserts must not exist.</para>
        /// </summary>
        [Test]
        public void TheSupportedMajorIsOneDeclaredBoundSharedByBothVersionRows()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("root-version"),
                out ProblemCatalogEntry major), Is.True);
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("root-version-minor"),
                out ProblemCatalogEntry minor), Is.True);

            DeclaredThreshold declared = major.Thresholds.Single(t => t.Name == "SupportedVersionMajor");

            Assert.Multiple(() =>
            {
                Assert.That(declared.Value, Is.EqualTo(4));
                Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.VendorDocumented));
                Assert.That(declared.Value,
                    Is.EqualTo(minor.Thresholds.Single(t => t.Name == "SupportedVersionMajor").Value),
                    "the major one row reports ABOVE is the major the other speaks AT — one bound, two rows");
            });
        }

        /// <summary>
        /// The predicate is strictly greater, and reading the bound from the entry did not move where it sits:
        /// the supported major itself is what every committed file carries, one above it is the finding, and a
        /// major that is not an integer is passed over rather than guessed at.
        /// </summary>
        [Test]
        public void AMajorAboveTheSupportedOneIsReportedAndTheSupportedOneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Versioned("4", "0"), "root-version"), Is.Zero,
                    "AT the supported major: 4 is what every committed file carries");
                Assert.That(Count(Versioned("5", "0"), "root-version"), Is.EqualTo(1));
                Assert.That(Count(Versioned("x", "0"), "root-version"), Is.Zero,
                    "a major that is not an integer is passed over, not guessed at");
                Assert.That(Message(Versioned("5", "0"), "root-version"),
                    Is.EqualTo("Nyere projektversion: version_major='5' er nyere end version 4, "
                        + "som dette værktøj understøtter."),
                    "the token is printed as written, and the sentence is unchanged by declaring the bound");
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        /// <summary>A project whose root carries the given version pair and nothing else worth reporting.</summary>
        /// <param name="major">The <c>version_major</c> token.</param>
        /// <param name="minor">The <c>version_minor</c> token.</param>
        private static Project Versioned(string major, string minor) =>
            new(Tree.Node("utcs_project", null,
                [("version_major", major), ("version_minor", minor), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0xffff")],
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")])));

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(TestSetup.Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static Project InGroups(params ProjectElement[] localities) =>
            Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")], localities));

        /// <summary>A product whose only child is of the given tag.</summary>
        private static Project Product(string childTag) =>
            InGroups(
                Tree.Node("group", Token("group", 0x21), [("name", "Stue"), ("icon", "_0x15")],
                    Tree.Node("product_dataline", Token("product_dataline", 0x40),
                        [("product_identifier", "_0x2202"), ("name", "Produkt"), ("icon", "_0x83")],
                        Tree.Node(childTag, Token(childTag, 0x50), [("name", "Del"), ("icon", "_0x83")]))));

        /// <summary>Two localities; the second either carries an icon, or the first's is the only one.</summary>
        private static Project Icons(bool secondHasIcon, bool explicitNullToken = false)
        {
            (string, string)[] without = explicitNullToken
                ? [("name", "Uden ikon"), ("icon", ElementId.NullToken)]
                : [("name", "Uden ikon")];

            return InGroups(
                Tree.Node("group", Token("group", 0x21),
                    secondHasIcon ? [("name", "Med ikon"), ("icon", "_0x15")] : [("name", "Med ikon")],
                    Tree.Node("product_dataline", Token("product_dataline", 0x40),
                        [("product_identifier", "_0x2202"), ("name", "Produkt"), ("icon", "_0x83")],
                        Tree.Node("dataline_input", Token("dataline_input", 0x50),
                            [("name", "Klemme"), ("icon", "_0x83")]))),
                Tree.Node("group", Token("group", 0x22), without,
                    Tree.Node("product_dataline", Token("product_dataline", 0x41),
                        [("product_identifier", "_0x2202"), ("name", "Produkt 2"), ("icon", "_0x83")],
                        Tree.Node("dataline_input", Token("dataline_input", 0x51),
                            [("name", "Klemme 2"), ("icon", "_0x83")]))));
        }
    }
}
