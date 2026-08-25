using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The findings export writer's BYTE contract, asserted against hand-built finding lists rather than a
    /// corpus run.
    ///
    /// <para><b>Why hand-built.</b> The writer is a pure formatter, so its input can be constructed directly —
    /// and that is the only way to reach the branches the corpus never witnesses. Nothing in the corpus has a
    /// null primary site, a character above U+00FF outside one message, a tab in a message, or a deliberately
    /// scrambled row order. A test that only ran the corpus would leave every one of those unpinned while
    /// looking thorough.</para>
    ///
    /// <para><b>Why bytes and not a parsed document.</b> These files are committed oracles compared byte for
    /// byte. An assertion that read the output back through an XML parser would pass on a file with a BOM, LF
    /// line ends, a different attribute order or a different escape of the same character — every one of which
    /// is a real difference to the thing that actually consumes them.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingExportWriterTests
    {
        private static ValidationFinding Finding(
            string code,
            string message,
            ValidationSeverity severity = ValidationSeverity.Warning,
            ValidationCategory category = ValidationCategory.ProjectStructure,
            string? locator = "_0x2132") =>
            new(
                new Problem(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty),
                severity,
                category,
                locator is null ? null : new FindingLocation(locator, null, null),
                EquatableArray<FindingLocation>.Empty);

        /// <summary>
        /// The default profile for these tests: no controller limits and no library, which is what an ordinary
        /// export runs under.
        /// </summary>
        private static ValidationProfile Profile => ValidationProfile.Categorized;

        private static byte[] Write(
            params ValidationFinding[] findings) =>
            Write(FindingExportOptions.Default with { SourceName = "Fixture.vis" }, findings);

        private static byte[] Write(FindingExportOptions options, params ValidationFinding[] findings) =>
            FindingExportWriter.Write(FindingExportProbe.Stamped(), findings, Profile, options, FindingExportProbe.Instant);

        /// <summary>Decodes the produced bytes as Latin-1, which is what they are.</summary>
        // ----- the document's frame -----

        /// <summary>
        /// No BOM, the ISO-8859-1 declaration, and CRLF throughout. Asserted on the BYTES: a decoded string
        /// would have hidden a BOM and a `\n` comparison would have passed on LF-only output.
        /// </summary>
        [Test]
        public void TheDocumentHasNoBomAnIso88591DeclarationAndCrlfLineEnds()
        {
            byte[] bytes = Write(Finding("struct-locality-empty", "Lokaliteten er tom."));

            Assert.Multiple(() =>
            {
                Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "no UTF-8 BOM");
                Assert.That(bytes[0], Is.EqualTo((byte)'<'), "the declaration starts at byte 0");
                Assert.That(
                    FindingExportProbe.Text(bytes),
                    Does.StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n"));

                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == 0x0A)
                    {
                        Assert.That(bytes[i - 1], Is.EqualTo(0x0D), $"bare LF at byte {i}");
                    }
                }

                Assert.That(FindingExportProbe.Text(bytes), Does.EndWith("</ihc_project_findings>\r\n"));
            });
        }

        /// <summary>Three spaces per level, and findings sit at exactly one level.</summary>
        [Test]
        public void EveryFindingIsIndentedThreeSpaces()
        {
            string text = FindingExportProbe.Text(Write(
                Finding("a-code", "En besked"),
                Finding("b-code", "En anden besked")));

            Assert.That(
                text.Split("\r\n").Where(l => l.Contains("<finding ")),
                Is.All.StartWith("   <finding "));
        }

        /// <summary>
        /// The whole document, byte for byte, for the simplest possible input. Pinned as one literal rather
        /// than as a set of substring checks, because the format is the product here and a literal is the only
        /// assertion that fails when something is silently ADDED.
        /// </summary>
        [Test]
        public void TheCompleteDocumentIsPinnedForASingleFinding()
        {
            byte[] bytes = FindingExportWriter.Write(
                FindingExportProbe.Stamped(),
                [Finding("struct-locality-empty", "Lokaliteten 'Stue' er tom.")],
                ValidationProfile.Categorized,
                FindingExportOptions.Default with { SourceName = "Project1-SimpelWired.vis" },
                FindingExportProbe.Instant);

            Assert.That(FindingExportProbe.Text(bytes), Is.EqualTo(
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n"
                + "<ihc_project_findings version=\"1\" source=\"Project1-SimpelWired.vis\""
                + " generated=\"2026-07-30T12:00:00+00:00\" saved_stamp=\"_0x2\" order=\"production\""
                + " severities=\"Error Warning Info\""
                // Seven, not the six capacity rows alone: ValidationProfile.Categorized supplies neither a
                // controller nor a library, so the one library-gated rule could not run either. An export
                // through the app service, whose profile does carry the library port, lists only the six.
                + " rules_not_run=\"capacity-input-addresses capacity-input-modules capacity-output-addresses"
                + " capacity-output-modules capacity-resources-high capacity-wireless-exceeded"
                + " logic-block-locked-content\">\r\n"
                + "   <finding severity=\"Warning\" code=\"struct-locality-empty\" category=\"ProjectStructure\""
                + " locator=\"_0x2132\" message=\"Lokaliteten 'Stue' er tom.\"/>\r\n"
                + "</ihc_project_findings>\r\n"));
        }

        // ----- attribute order -----

        /// <summary>
        /// The root's attribute order, read off the emitted bytes rather than restated: XML gives it no meaning,
        /// but the oracle does, and a reader scanning 618 lines needs the left edge column-comparable.
        /// </summary>
        [Test]
        public void TheRootEmitsItsAttributesInThePinnedOrder()
        {
            string root = FindingExportProbe.Text(Write(Finding("a-code", "x"))).Split("\r\n")[1];

            Assert.Multiple(() =>
            {
                Assert.That(
                    FindingExportProbe.AttributeNames(root),
                    Is.EqualTo(new[]
                    {
                        "version", "source", "generated", "saved_stamp", "order", "severities", "rules_not_run",
                    }));
                Assert.That(
                    FindingExportProbe.AttributeNames(root), Is.EqualTo(FindingExportWriter.RootAttributes),
                    "the writer's own declaration and what it emits must not drift");
            });
        }

        /// <summary>
        /// The finding's fixed attribute order for the ordinary case — a single resolved site, which is what
        /// almost every line is.
        /// <para>
        /// Three of the eight declared fixed attributes are conditional (a related-site list, a path, a
        /// related-path list), so this line carries five of them. What must hold is that the emitted names are
        /// a SUBSEQUENCE of the declaration: present in it, and in its order. Full equality on a line carrying
        /// all eight is asserted where those three are the subject.
        /// </para>
        /// </summary>
        [Test]
        public void AFindingEmitsItsFixedAttributesInThePinnedOrder()
        {
            ImmutableArray<string> emitted = FindingExportProbe.AttributeNames(
                FindingExportProbe.FindingLines(Write(Finding("struct-locality-empty", "Tom")))[0]);

            Assert.Multiple(() =>
            {
                Assert.That(
                    emitted, Is.EqualTo(new[] { "severity", "code", "category", "locator", "message" }));
                Assert.That(
                    emitted, Is.EqualTo(FindingExportWriter.FixedFindingAttributes.Where(emitted.Contains)),
                    "the writer's own declaration and what it emits must not drift, in membership or in order");
            });
        }

        // ----- the three locator states -----

        /// <summary>
        /// A finding about the project as a whole carries NO locator attribute — not an empty one and not a
        /// sentinel. A reader therefore tests presence, and never has to know that some string means "nowhere".
        /// </summary>
        [Test]
        public void AFindingWithNoPrimarySiteCarriesNoLocatorAttributeAtAll()
        {
            string line = FindingExportProbe.FindingLines(Write(Finding("root-children", "Uventet rækkefølge", locator: null)))[0];

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Not.Contain("locator="));
                Assert.That(FindingExportProbe.AttributeNames(line), Is.EqualTo(new[] { "severity", "code", "category", "message" }));
            });
        }

        /// <summary>A resolved token and a bare tag are both just locators; the writer does not tell them apart.</summary>
        [Test]
        public void AResolvedTokenAndATagLocatorAreEmittedTheSameWay()
        {
            string[] lines = FindingExportProbe.FindingLines(Write(
                Finding("id-wellformed", "Token", locator: "_0x2132"),
                Finding("schema-unknown-element", "Tag", locator: "bogus_element")));

            Assert.Multiple(() =>
            {
                Assert.That(lines[0], Does.Contain(" locator=\"_0x2132\""));
                Assert.That(lines[1], Does.Contain(" locator=\"bogus_element\""));
            });
        }

        // ----- enum spelling -----

        /// <summary>
        /// Severity and category are their enum member names, verbatim. Lowercasing them would oblige every
        /// reader to title-case them back, and <c>@severities</c> would have to repeat the mapping a third time.
        /// </summary>
        [Test]
        public void SeverityAndCategoryAreEmittedAsEnumMemberNamesVerbatim()
        {
            string[] lines = FindingExportProbe.FindingLines(Write(
                Finding("e", "x", ValidationSeverity.Error, ValidationCategory.FileIntegrity),
                Finding("w", "x", ValidationSeverity.Warning, ValidationCategory.Documentation),
                Finding("i", "x", ValidationSeverity.Info, ValidationCategory.ProjectStructure)));

            Assert.Multiple(() =>
            {
                Assert.That(lines[0], Does.Contain(" severity=\"Error\" code=\"e\" category=\"FileIntegrity\""));
                Assert.That(lines[1], Does.Contain(" severity=\"Warning\" code=\"w\" category=\"Documentation\""));
                Assert.That(lines[2], Does.Contain(" severity=\"Info\" code=\"i\" category=\"ProjectStructure\""));
            });
        }

        // ----- escaping -----

        /// <summary>
        /// The apostrophe stays LITERAL. Danish findings quote the user's own names constantly, so escaping it
        /// would fill three lines in five with <c>&amp;apos;</c> for no reader's benefit, and XML does not
        /// require it inside a double-quoted value.
        /// </summary>
        [Test]
        public void TheApostropheIsNotEscaped()
        {
            string line = FindingExportProbe.FindingLines(Write(Finding("c", "Lokaliteten 'Stue' er tom.")))[0];

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Contain("message=\"Lokaliteten 'Stue' er tom.\""));
                Assert.That(line, Does.Not.Contain("&apos;"));
            });
        }

        /// <summary>The four XML specials and the three whitespace control characters, each as one case.</summary>
        [TestCase("&", "&amp;")]
        [TestCase("<", "&lt;")]
        [TestCase(">", "&gt;")]
        [TestCase("\"", "&quot;")]
        [TestCase("\r", "&#xD;")]
        [TestCase("\n", "&#xA;")]
        [TestCase("\t", "&#x9;")]
        public void TheSharedSpecialsEscapeAsTheVisWritersEscapeThem(string raw, string escaped)
        {
            string line = FindingExportProbe.FindingLines(Write(Finding("c", $"a{raw}b")))[0];

            Assert.That(line, Does.Contain($"message=\"a{escaped}b\""));
        }

        /// <summary>
        /// A Latin-1 character is written as its RAW byte, not escaped. That is what makes the file mojibake in
        /// a UTF-8 viewer, and it is the accepted trade: escaping every non-ASCII character would change what a
        /// USER's exported file looks like, not just the oracle's.
        /// </summary>
        [Test]
        public void ALatin1CharacterIsWrittenAsItsRawByte()
        {
            byte[] bytes = Write(Finding("c", "blåbærgrød"));

            Assert.Multiple(() =>
            {
                Assert.That(bytes, Does.Contain((byte)0xE5), "å");
                Assert.That(bytes, Does.Contain((byte)0xE6), "æ");
                Assert.That(bytes, Does.Contain((byte)0xF8), "ø");
                Assert.That(FindingExportProbe.Text(bytes), Does.Contain("message=\"blåbærgrød\""));
                Assert.That(FindingExportProbe.Text(bytes), Does.Not.Contain("&#"), "nothing here needs a numeric reference");
            });
        }

        /// <summary>
        /// Above U+00FF there IS no Latin-1 byte, so the character becomes a numeric reference. The corpus
        /// forces this: one message carries a euro sign, and a writer inheriting the strict encoder's
        /// exception fallback would throw while generating that case's oracle.
        /// </summary>
        [Test]
        public void ACharacterAboveLatin1BecomesANumericReference()
        {
            byte[] bytes = Write(Finding("c", "Pris " + (char)0x20AC));

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.Text(bytes), Does.Contain("message=\"Pris &#8364;\""));
                Assert.That(bytes, Is.All.LessThan((byte)0x80), "the reference is pure ASCII, so this line reads fine anywhere");
            });
        }

        /// <summary>
        /// A surrogate PAIR is one character and gets one reference. Emitting each half separately would
        /// produce two references no parser recombines, which corrupts the character while looking escaped.
        /// </summary>
        [Test]
        public void ASurrogatePairIsCombinedBeforeBeingEscaped()
        {
            string line = FindingExportProbe.FindingLines(Write(Finding("c", "ikon " + char.ConvertFromUtf32(0x1F600))))[0];

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Contain("message=\"ikon &#128512;\""));
                Assert.That(line, Does.Not.Contain("&#55357;"), "the high surrogate must never be emitted alone");
            });
        }

        /// <summary>An unpaired high surrogate is still one code unit, and must not swallow the character after it.</summary>
        [Test]
        public void AnUnpairedSurrogateIsEscapedAsItself()
        {
            string line = FindingExportProbe.FindingLines(Write(Finding("c", "a" + (char)0xD83D + "b")))[0];

            Assert.That(line, Does.Contain("message=\"a&#55357;b\""));
        }

        // ----- sequence -----

        /// <summary>
        /// The writer emits what it is handed, in that sequence: it neither re-sorts, drops nor duplicates. Fed
        /// a deliberately non-production order, the file is in exactly that order and <c>@order</c> carries the
        /// caller's own label for it.
        /// </summary>
        [Test]
        public void TheWriterEmitsTheCallerSequenceVerbatimAndRecordsItsLabel()
        {
            ValidationFinding[] scrambled =
            [
                Finding("z-last", "3", ValidationSeverity.Info),
                Finding("a-first", "1", ValidationSeverity.Error),
                Finding("m-middle", "2", ValidationSeverity.Warning),
            ];

            byte[] bytes = Write(
                FindingExportOptions.Default with { SourceName = "s", Order = "host:code desc" }, scrambled);
            string text = FindingExportProbe.Text(bytes);

            Assert.Multiple(() =>
            {
                Assert.That(
                    FindingExportProbe.FindingLines(bytes).Select(Code),
                    Is.EqualTo(new[] { "z-last", "a-first", "m-middle" }),
                    "not re-sorted into any order of the writer's own");
                Assert.That(text, Does.Contain("order=\"host:code desc\""));
                Assert.That(FindingExportProbe.FindingLines(bytes), Has.Length.EqualTo(3), "nothing dropped, nothing duplicated");
            });
        }

        /// <summary>An empty list is a legal export: the frame is written and there are no children.</summary>
        [Test]
        public void AnEmptyFindingListStillProducesTheCompleteFrame()
        {
            byte[] bytes = Write();

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.FindingLines(bytes), Is.Empty);
                Assert.That(FindingExportProbe.Text(bytes), Does.Contain("<ihc_project_findings version=\"1\""));
                Assert.That(FindingExportProbe.Text(bytes), Does.EndWith("</ihc_project_findings>\r\n"));
            });
        }

        // ----- what the caller supplies -----

        /// <summary>
        /// <c>source</c> and <c>saved_stamp</c> are the two facts the writer cannot derive. A null source is an
        /// EMPTY attribute rather than a missing one, so the root's shape never varies between exports.
        /// </summary>
        [Test]
        public void AnUnnamedSourceAndAnUnstampedProjectStillEmitTheirAttributes()
        {
            byte[] bytes = FindingExportWriter.Write(
                new Project(new ProjectElement("utcs_project", null, EquatableArray<(string, string)>.Empty,
                    EquatableArray<ProjectElement>.Empty)),
                [],
                Profile,
                FindingExportOptions.Default,
                FindingExportProbe.Instant);

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.Text(bytes), Does.Contain(" source=\"\""));
                Assert.That(FindingExportProbe.Text(bytes), Does.Contain(" saved_stamp=\"\""));
                Assert.That(FindingExportProbe.AttributeNames(FindingExportProbe.Text(bytes).Split("\r\n")[1]), Is.EqualTo(FindingExportWriter.RootAttributes));
            });
        }

        /// <summary>The stamp is read from the project's <c>id2</c> — the save this list belongs to.</summary>
        [Test]
        public void TheSavedStampComesFromTheProjectsCurrentSaveStamp()
        {
            byte[] bytes = FindingExportWriter.Write(
                FindingExportProbe.Stamped("_0x1a2b3c"), [], Profile, FindingExportOptions.Default, FindingExportProbe.Instant);

            Assert.That(FindingExportProbe.Text(bytes), Does.Contain(" saved_stamp=\"_0x1a2b3c\""));
        }

        /// <summary>
        /// The generated stamp is invariant-formatted from the clock it is handed, so two machines exporting the
        /// same list at the same instant write the same byte.
        /// </summary>
        [Test]
        public void TheGeneratedStampIsInvariantAndComesFromTheSuppliedInstant()
        {
            byte[] bytes = FindingExportWriter.Write(
                FindingExportProbe.Stamped(), [], Profile, FindingExportOptions.Default,
                new DateTimeOffset(2026, 12, 24, 18, 5, 9, TimeSpan.FromHours(2)));

            Assert.That(FindingExportProbe.Text(bytes), Does.Contain(" generated=\"2026-12-24T18:05:09+02:00\""));
        }

        // ----- helpers -----

        private static string Code(string findingLine) =>
            findingLine.Split(" code=\"")[1].Split('"')[0];
    }
}
