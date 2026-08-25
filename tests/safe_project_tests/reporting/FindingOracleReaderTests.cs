using System;
using System.IO;
using System.Linq;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The shared oracle reader: what it returns, and — the half that matters — what it REFUSES.
    ///
    /// <para><b>Why refusal is the subject.</b> Every corpus-wide gate in this suite compares something against
    /// what this reader returns. A reader that answered an empty list for a corrupted file would turn a broken
    /// oracle into a passing test on all of them at once, and the failure would surface as "the corpus produces
    /// nothing", which points at the engine rather than at the file. So each way a file can be wrong is a test,
    /// and each one asserts a THROW naming the file.</para>
    ///
    /// <para><b>The fixtures are written here, not committed.</b> A deliberately corrupted oracle checked into
    /// <c>tests/testdata/</c> would be picked up by the set-integrity gate as an orphan, and by the byte gate as
    /// a case that reproduces nothing. They are written to a temp directory the test owns.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingOracleReaderTests
    {
        private string _root = string.Empty;

        [SetUp]
        public void CreateRoot() =>
            _root = Directory.CreateTempSubdirectory("ihc-findings-oracle-").FullName;

        [TearDown]
        public void RemoveRoot() => Directory.Delete(_root, recursive: true);

        /// <summary>Writes a file into the test's own root, in the encoding the format declares.</summary>
        private string Write(string fileName, string content)
        {
            string path = Path.Combine(_root, fileName);
            File.WriteAllBytes(path, ProjectFile.Encoding.GetBytes(content));
            return path;
        }

        private const string OneFinding =
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n"
            + "<ihc_project_findings version=\"1\" source=\"synthetic/ids\" generated=\"2026-07-30T12:00:00+00:00\""
            + " saved_stamp=\"_0x2\" order=\"production\" severities=\"Error Warning Info\" rules_not_run=\"\">\r\n"
            + "   <finding severity=\"Warning\" code=\"struct-locality-empty\" category=\"ProjectStructure\""
            + " locator=\"_0x2132\" message=\"Lokaliteten 'Stue' er tom.\"/>\r\n"
            + "</ihc_project_findings>\r\n";

        // ----- what it returns -----

        [Test]
        public void AWellFormedFileReadsBackItsFindings()
        {
            Write("synthetic-ids.xml", OneFinding);

            RecordedFinding finding = FindingOracles.ReadAll(_root).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Case, Is.EqualTo("synthetic/ids"), "read from the file's own source");
                Assert.That(finding.Severity, Is.EqualTo("Warning"));
                Assert.That(finding.Code, Is.EqualTo("struct-locality-empty"));
                Assert.That(finding.Category, Is.EqualTo("ProjectStructure"));
                Assert.That(finding.Locator, Is.EqualTo("_0x2132"));
                Assert.That(finding.Message, Is.EqualTo("Lokaliteten 'Stue' er tom."));
            });
        }

        /// <summary>
        /// Danish characters survive. The file is ISO-8859-1 and its high bytes are NOT valid UTF-8, so a reader
        /// that decoded the text before parsing would mangle every one of them — silently, into replacement
        /// characters that still compare equal to each other.
        /// </summary>
        [Test]
        public void TheDeclaredEncodingIsWhatDecodesTheBytes()
        {
            Write("synthetic-ids.xml", OneFinding.Replace("Stue", "Værelse på 1. sal"));

            Assert.That(
                FindingOracles.ReadAll(_root).Single().Message,
                Does.Contain("Værelse på 1. sal"));
        }

        /// <summary>A finding that names no element records no locator, and reads back as null rather than "-".</summary>
        [Test]
        public void AnAbsentLocatorReadsBackAsNull()
        {
            Write("synthetic-ids.xml", OneFinding.Replace(" locator=\"_0x2132\"", string.Empty));

            Assert.That(FindingOracles.ReadAll(_root).Single().Locator, Is.Null);
        }

        /// <summary>A per-case accessor beside the whole-recording one, for a reader that wants one file.</summary>
        [Test]
        public void OneCaseCanBeReadOnItsOwn()
        {
            Write("synthetic-ids.xml", OneFinding);
            Write("fixture-Project6-Errors.xml", OneFinding.Replace("synthetic/ids", "fixture/Project6-Errors"));

            Assert.Multiple(() =>
            {
                Assert.That(FindingOracles.Read("synthetic/ids", _root), Has.Length.EqualTo(1));
                Assert.That(FindingOracles.ReadAll(_root), Has.Length.EqualTo(2), "and both are in the whole set");
            });
        }

        /// <summary>The whole recording reads in ordinal filename order, so a run is reproducible.</summary>
        [Test]
        public void TheWholeRecordingReadsInOrdinalFileOrder()
        {
            Write("synthetic-ids.xml", OneFinding);
            Write("authentic-Project0-Tomt.xml", OneFinding.Replace("synthetic/ids", "authentic/Project0-Tomt"));

            Assert.That(
                FindingOracles.ReadAll(_root).Select(f => f.Case),
                Is.EqualTo(new[] { "authentic/Project0-Tomt", "synthetic/ids" }));
        }

        // ----- the filename is derived, not chosen -----

        /// <summary>The mechanical rule, both ways round.</summary>
        [Test]
        public void TheFileNameIsDerivedFromTheCaseName()
        {
            Write("synthetic-ids.xml", OneFinding);

            Assert.Multiple(() =>
            {
                Assert.That(FindingOracles.FileNameFor("synthetic/ids"), Is.EqualTo("synthetic-ids.xml"));
                Assert.That(
                    FindingOracles.FileNameFor("fixture/Project6-Errors"),
                    Is.EqualTo("fixture-Project6-Errors.xml"));
                Assert.That(
                    FindingOracles.CaseNameIn(Path.Combine(_root, "synthetic-ids.xml")),
                    Is.EqualTo("synthetic/ids"));
            });
        }

        /// <summary>
        /// <c>Cases()</c> asserts once per file that its name and its <c>@source</c> agree — the check that a
        /// case name has one spelling rather than three. A file copied to a new name keeps the old source, and
        /// this is what catches it.
        /// </summary>
        [Test]
        public void ACaseNameThatDisagreesWithItsFileNameIsRefused()
        {
            Write("copied-by-hand.xml", OneFinding);

            InvalidDataException thrown = Assert.Throws<InvalidDataException>(
                () => FindingOracles.Cases(_root).ToList())!;

            Assert.Multiple(() =>
            {
                Assert.That(thrown.Message, Does.Contain("copied-by-hand.xml"));
                Assert.That(thrown.Message, Does.Contain("synthetic-ids.xml"), "and names where it belongs");
            });
        }

        [Test]
        public void CasesYieldsEachFileWithItsCaseName()
        {
            Write("synthetic-ids.xml", OneFinding);

            object[] row = FindingOracles.Cases(_root).Single();

            Assert.That(row, Is.EqualTo(new object[] { "synthetic-ids.xml", "synthetic/ids" }));
        }

        // ----- what it refuses -----

        /// <summary>
        /// THE test this reader exists for: a corrupted file FAILS rather than reading as empty. Truncated
        /// mid-element is the realistic corruption — an interrupted write, a bad merge — and it is the one a
        /// lenient parser is most likely to swallow.
        /// </summary>
        [Test]
        public void ATruncatedFileIsRefusedRatherThanReadAsEmpty()
        {
            Write("synthetic-ids.xml", OneFinding[..(OneFinding.Length / 2)]);

            InvalidDataException thrown = Assert.Throws<InvalidDataException>(
                () => FindingOracles.ReadAll(_root))!;

            Assert.Multiple(() =>
            {
                Assert.That(thrown.Message, Does.Contain("synthetic-ids.xml"), "the message names the file");
                Assert.That(thrown.Message, Does.Contain("well-formed"));
            });
        }

        /// <summary>A different document in the right place is not this format.</summary>
        [Test]
        public void AFileWithTheWrongRootIsRefused()
        {
            Write("synthetic-ids.xml",
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<utcs_project version=\"1\"/>\r\n");

            Assert.That(
                Assert.Throws<InvalidDataException>(() => FindingOracles.ReadAll(_root))!.Message,
                Does.Contain("ihc_project_findings"));
        }

        /// <summary>
        /// A file from a future format version is refused rather than half-read. The version attribute is the
        /// only thing that can say the shape changed, so ignoring it would make it decoration.
        /// </summary>
        [Test]
        public void AFileFromAnUnknownFormatVersionIsRefused()
        {
            Write("synthetic-ids.xml", OneFinding.Replace("version=\"1\"", "version=\"2\""));

            Assert.That(
                Assert.Throws<InvalidDataException>(() => FindingOracles.ReadAll(_root))!.Message,
                Does.Contain("version '2'"));
        }

        /// <summary>A finding missing a fixed attribute is a broken row, not a row with a default.</summary>
        [Test]
        public void AFindingMissingAFixedAttributeIsRefused()
        {
            Write("synthetic-ids.xml", OneFinding.Replace(" code=\"struct-locality-empty\"", string.Empty));

            Assert.That(
                Assert.Throws<InvalidDataException>(() => FindingOracles.ReadAll(_root))!.Message,
                Does.Contain("'code'"));
        }

        /// <summary>An absent file is an error, not an empty case.</summary>
        [Test]
        public void ReadingACaseWithNoFileIsRefused() =>
            Assert.Throws<FileNotFoundException>(() => FindingOracles.Read("synthetic/ids", _root));

        /// <summary>
        /// An empty directory reads as no findings and does NOT throw — which is a different statement from a
        /// corrupted file, and is the state the tree is in before the oracles are generated.
        /// </summary>
        [Test]
        public void AnEmptyRootReadsAsNoFindings()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FindingOracles.ReadAll(_root), Is.Empty);
                Assert.That(FindingOracles.Files(_root), Is.Empty);
            });
        }

        // ----- the clock -----

        /// <summary>
        /// One pinned instant, borrowed from the report oracles rather than redeclared — so the two families
        /// cannot drift onto two magic dates, and neither can move without the other noticing.
        /// </summary>
        [Test]
        public void TheClockIsThePinnedInstantTheReportOraclesUse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FindingOracles.Clock().GetLocalNow(), Is.EqualTo(ReportOracles.PinnedInstant));
                Assert.That(
                    FindingOracles.Clock().GetLocalNow().Offset, Is.EqualTo(TimeSpan.Zero),
                    "UTC on every machine, which is what makes the generated stamp reproducible");
            });
        }
            // ----- how a mismatch is reported -----

        /// <summary>Identical bytes are the whole gate; nothing else runs.</summary>
        [Test]
        public void IdenticalBytesPass()
        {
            byte[] bytes = ProjectFile.Encoding.GetBytes(OneFinding);

            Assert.DoesNotThrow(() => FindingOracles.AssertMatchesOracle(bytes, bytes, "synthetic-ids.xml"));
        }

        /// <summary>
        /// A changed finding is reported as a CHANGED ROW, naming both versions — not as a byte offset. This is
        /// the difference between a message a reader can act on and one they have to decode.
        /// </summary>
        [Test]
        public void AChangedFindingIsReportedAsARow()
        {
            byte[] oracle = ProjectFile.Encoding.GetBytes(OneFinding);
            byte[] produced = ProjectFile.Encoding.GetBytes(OneFinding.Replace("Stue", "Køkken"));

            string message = Assert.Throws<AssertionException>(
                () => FindingOracles.AssertMatchesOracle(oracle, produced, "synthetic-ids.xml"))!.Message;

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("#1 changed"));
                Assert.That(message, Does.Contain("Lokaliteten 'Stue' er tom."), "the oracle's finding");
                Assert.That(message, Does.Contain("Lokaliteten 'Køkken' er tom."), "and the produced one");
                Assert.That(message, Does.Not.Contain("byte "), "no byte offsets in the structural report");
            });
        }

        /// <summary>A rule that stops reporting says so as a row, and one that starts reporting likewise.</summary>
        [Test]
        public void AddedAndRemovedFindingsAreReportedAsRows()
        {
            byte[] oracle = ProjectFile.Encoding.GetBytes(OneFinding);
            byte[] empty = ProjectFile.Encoding.GetBytes(
                OneFinding[..OneFinding.IndexOf("   <finding", StringComparison.Ordinal)]
                + "</ihc_project_findings>\r\n");

            Assert.Multiple(() =>
            {
                Assert.That(
                    Assert.Throws<AssertionException>(
                        () => FindingOracles.AssertMatchesOracle(oracle, empty, "f.xml"))!.Message,
                    Does.Contain("#1 no longer reported"));
                Assert.That(
                    Assert.Throws<AssertionException>(
                        () => FindingOracles.AssertMatchesOracle(empty, oracle, "f.xml"))!.Message,
                    Does.Contain("#1 newly reported"));
            });
        }

        /// <summary>
        /// Bytes that differ while every FINDING is identical — a header change, a re-ordered attribute, a
        /// different escape. The rows cannot explain it, so the message says where to look instead of printing
        /// an empty difference list.
        /// </summary>
        [Test]
        public void ADifferenceOutsideTheFindingsSaysWhereToLook()
        {
            byte[] oracle = ProjectFile.Encoding.GetBytes(OneFinding);
            byte[] produced = ProjectFile.Encoding.GetBytes(OneFinding.Replace("_0x2\"", "_0x9\""));

            Assert.That(
                Assert.Throws<AssertionException>(
                    () => FindingOracles.AssertMatchesOracle(oracle, produced, "f.xml"))!.Message,
                Does.Contain("the difference is in the document around them"));
        }

        /// <summary>
        /// THE fallback: when the bytes cannot be read as findings at all there are no rows to compare, so the
        /// bytes themselves are the evidence — a short hex window at the first difference, and only then.
        /// </summary>
        [Test]
        public void UnparseableBytesFallBackToAHexWindow()
        {
            byte[] oracle = ProjectFile.Encoding.GetBytes(OneFinding);
            byte[] truncated = oracle[..(oracle.Length / 2)];

            string message = Assert.Throws<AssertionException>(
                () => FindingOracles.AssertMatchesOracle(oracle, truncated, "synthetic-ids.xml"))!.Message;

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("could not be read as findings"));
                Assert.That(message, Does.Contain("shown raw"));
                Assert.That(message, Does.Contain("oracle:"));
                Assert.That(message, Does.Contain("generated:"));
                Assert.That(message, Does.Not.Contain("changed:"), "no row report, because there are no rows");
            });
        }
    }
}
