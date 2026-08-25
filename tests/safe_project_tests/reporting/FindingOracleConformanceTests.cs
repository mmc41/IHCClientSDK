using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The 18 oracle files as FILES: their byte shape, and whether the set is the set it should be.
    ///
    /// <para><b>Why this is separate from the byte gate.</b> <see cref="ValidationCharacterizationTests"/>
    /// asserts each file reproduces its case exactly, which pins the CONTENT of every file that exists. It says
    /// nothing about a file that should exist and does not, nothing about one that should not and does, and —
    /// because it compares against the same writer that produced them — nothing about the encoding both sides
    /// happen to agree on. Those three are what this covers.</para>
    ///
    /// <para><b>The shape assertions are about the BYTES, deliberately.</b> Every one of them is invisible to a
    /// reader that decodes first: a BOM parses away, a bare LF reads as a line break, and a wrong declaration
    /// still yields a well-formed document. They are checked on the raw bytes or not at all.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingOracleConformanceTests
    {
        /// <summary>The declaration every file opens with, exactly — the encoding is not negotiable.</summary>
        private const string Declaration = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>";

        private static ImmutableArray<string> Files => FindingOracleHarness.Files();

        /// <summary>
        /// Non-vacuity for everything below: an empty directory would satisfy "every file is well shaped" and
        /// "no file is an orphan" without a single file existing.
        /// </summary>
        [Test]
        public void TheCorpusIsRecordedInEighteenFiles() =>
            Assert.That(Files, Has.Length.EqualTo(ValidationCharacterizationTests.Corpus.Length));

        // ----- file shape -----

        /// <summary>
        /// No BOM, no bare LF, and exactly the declared encoding — asserted over every file at once so a
        /// nineteenth arrives already covered.
        ///
        /// <para><b>Each of the three has its own way of going wrong.</b> A BOM is what an editor adds when
        /// someone opens an oracle and saves it; a bare LF is what a checkout produces if the
        /// <c>.gitattributes</c> line is ever dropped; and a wrong declaration is what happens if the writer's
        /// encoding constant is changed without the bytes following. None of them changes what the document
        /// MEANS, which is exactly why nothing else catches them.</para>
        /// </summary>
        [Test]
        public void EveryFileIsBomLessCrlfAndDeclaresIso88591()
        {
            var problems = new System.Collections.Generic.List<string>();
            foreach (string path in Files)
            {
                string name = Path.GetFileName(path);
                byte[] bytes = File.ReadAllBytes(path);

                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    problems.Add($"{name}: starts with a UTF-8 BOM");
                }

                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == 0x0A && (i == 0 || bytes[i - 1] != 0x0D))
                    {
                        problems.Add($"{name}: bare LF at byte {i}");
                        break;   // one report per file; the first says everything
                    }
                }

                string opening = ProjectFile.Encoding.GetString(
                    bytes, 0, Math.Min(Declaration.Length, bytes.Length));
                if (opening != Declaration)
                {
                    problems.Add($"{name}: opens with '{opening}', not '{Declaration}'");
                }
            }

            Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems));
        }

        /// <summary>
        /// The declaration is not merely present but TRUE of the bytes: every file decodes as ISO-8859-1
        /// without loss, and the ones carrying Danish text really do hold raw high bytes rather than UTF-8
        /// sequences that would read as mojibake through the declared encoding.
        /// </summary>
        [Test]
        public void TheFilesReallyAreLatin1AndNotUtf8InDisguise()
        {
            byte[] all = [.. Files.SelectMany(File.ReadAllBytes)];

            Assert.Multiple(() =>
            {
                Assert.That(
                    all.Count(b => b > 0x7F), Is.GreaterThan(0),
                    "non-vacuity: Danish text really does reach these files");
                Assert.That(
                    () => ProjectFile.StrictEncoding.GetString(all), Throws.Nothing,
                    "every byte is in the Latin-1 repertoire the declaration names");
            });
        }

        // ----- set integrity -----

        /// <summary>
        /// The set of files is exactly the set of corpus cases: no orphan, no missing file.
        ///
        /// <para><b>Both directions matter and they fail differently.</b> A MISSING file is a case that stopped
        /// being recorded — the byte gate cannot see it, because a case with no file generates no test. An
        /// ORPHAN is a file for a case that was renamed or removed, which keeps passing its own byte test
        /// forever while describing something the corpus no longer contains.</para>
        /// </summary>
        [Test]
        public void TheFileSetIsExactlyTheCorpusWithNoOrphanAndNoMissingFile()
        {
            ImmutableArray<string> expected =
            [
                .. ValidationCharacterizationTests.Corpus
                    .Select(c => FindingOracleHarness.FileNameFor(c.Case))
                    .OrderBy(f => f, StringComparer.Ordinal),
            ];
            ImmutableArray<string> actual = [.. Files.Select(Path.GetFileName)!];

            Assert.Multiple(() =>
            {
                Assert.That(
                    actual.Except(expected), Is.Empty,
                    "orphans: a file whose case the corpus no longer contains");
                Assert.That(
                    expected.Except(actual), Is.Empty,
                    "missing: a corpus case with no oracle, which generates no byte test at all");
                Assert.That(actual, Is.EqualTo(expected), "and nothing else differs");
            });
        }

        /// <summary>
        /// Every file's <c>@source</c> names a real corpus case. The filename is derived from the source, so
        /// this and the set check together mean the three spellings of a case name — the corpus array, the
        /// filename and the attribute — cannot drift apart.
        /// </summary>
        [Test]
        public void EveryFileRecordsACaseTheCorpusActuallyHas()
        {
            ImmutableArray<string> cases = [.. ValidationCharacterizationTests.Corpus.Select(c => c.Case)];

            Assert.That(
                Files.Select(FindingOracleHarness.CaseNameIn).Except(cases), Is.Empty,
                "a file recording a case the corpus does not have describes nothing that is still produced");
        }
    }
}
