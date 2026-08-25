using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// One finding as an oracle file records it — the projection every reader of these oracles shares.
    ///
    /// <para><b>Why a record and not six loose strings.</b> Several readers join these cells into comparison
    /// rows; when the cells were parsed independently at each site, renaming one was many edits nothing
    /// checked. Now it is a compile error.</para>
    /// </summary>
    /// <param name="Case">The corpus case the finding came from — the file's own <c>@source</c>.</param>
    /// <param name="Severity">The tier, as the enum member name.</param>
    /// <param name="Code">The finding's kebab-case or dotted code.</param>
    /// <param name="Category">The check family, as the enum member name.</param>
    /// <param name="Locator">
    /// The raw id token or tag, or <c>null</c> for a finding that names no element. NULL, not a sentinel: the
    /// format records absence as a MISSING attribute, so a reader tests for null rather than knowing which
    /// string means "nowhere".
    /// </param>
    /// <param name="Message">The Danish sentence, exactly as the catalogue bound it.</param>
    internal sealed record RecordedFinding(
        string Case,
        string Severity,
        string Code,
        string Category,
        string? Locator,
        string Message);

    /// <summary>
    /// The findings-oracle harness shared by the two suites that read these files — <c>safe_project_tests</c>,
    /// which owns their byte contract, and <c>safe_visual_tests</c>, whose end-to-end panel tests check
    /// themselves against the same recorded findings. Compiled into both through a <c>&lt;Compile Include&gt;</c>
    /// link, exactly as <see cref="ReportOracleHarness"/> is.
    ///
    /// <para><b>It finds the files itself.</b> Not through a suite-local path helper: the whole point is that two
    /// assemblies share it, and a helper that only one of them has would put the harness back in one suite. It
    /// reads the copy beside the running test binary, which is what <c>TestData.props</c> puts there.</para>
    /// </summary>
    internal static class FindingOracleHarness
    {
        /// <summary>The document element every oracle file carries.</summary>
        internal const string RootTag = "ihc_project_findings";

        /// <summary>The export format version these oracles were written at.</summary>
        internal const string FormatVersion = "1";

        /// <summary>
        /// The pinned export clock, so <c>@generated</c> is the same byte on every machine.
        /// <para>
        /// The SAME instant the report oracles use, borrowed rather than redeclared: two constants would be two
        /// magic dates to remember, and the coupling they would guard against — one family's regeneration moving
        /// the other's bytes — does not exist, because nothing shares a generator.
        /// </para>
        /// </summary>
        public static TimeProvider Clock() => new FakeTimeProvider(ReportOracleHarness.PinnedInstant);

        /// <summary>Where the oracle files sit beside the running test binary.</summary>
        public static string DefaultRoot =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "validation");

        /// <summary>
        /// The file a corpus case is recorded in. Fully mechanical — the case's <c>/</c> becomes <c>-</c> — so
        /// the 18 filenames are derived rather than retyped, and a case with no file shows up as a missing file
        /// rather than as a missing row.
        /// </summary>
        /// <param name="caseName">The corpus case name, e.g. <c>fixture/Project6-Errors</c>.</param>
        public static string FileNameFor(string caseName) => caseName.Replace('/', '-') + ".xml";

        /// <summary>
        /// The case name a file records, read from its own <c>@source</c> — the inverse of
        /// <see cref="FileNameFor"/>, and the reason a case name has ONE spelling rather than three (the
        /// filename, the attribute and the corpus array).
        /// </summary>
        public static string CaseNameIn(string path) => CaseNameIn(Root(path), Path.GetFileName(path));

        /// <summary>
        /// The same question asked of a root already in hand, so a reader that has parsed the file does not open
        /// and parse it a second time to learn its case name.
        /// </summary>
        private static string CaseNameIn(XElement root, string name) => root.Attribute("source")?.Value
            ?? throw new InvalidDataException($"{name}: the root carries no 'source' attribute.");

        /// <summary>
        /// Every oracle file present, as <c>(file name, case name)</c> rows — and, once per file, the assertion
        /// that the two agree. A file whose <c>@source</c> does not derive its own name is a file that was copied
        /// or renamed by hand, which is exactly the state the mechanical rule exists to prevent.
        /// </summary>
        public static IEnumerable<object[]> Cases()
        {
            foreach (string path in Files())
            {
                string caseName = CaseNameIn(path);
                string expected = FileNameFor(caseName);
                string actual = Path.GetFileName(path);
                if (expected != actual)
                {
                    throw new InvalidDataException(
                        $"{actual}: records source '{caseName}', which belongs in '{expected}'. The filename is "
                        + "derived from the source, so the two cannot be chosen independently.");
                }

                yield return [actual, caseName];
            }
        }

        /// <summary>Every oracle file present, in ordinal filename order so a run is reproducible.</summary>
        public static ImmutableArray<string> Files() =>
            Directory.Exists(DefaultRoot)
                ? [.. Directory.EnumerateFiles(DefaultRoot, "*.xml").OrderBy(f => f, StringComparer.Ordinal)]
                : [];

        /// <summary>
        /// Every recorded finding across every case, in the order the files and their lines read — the whole
        /// recording, which is what the corpus-wide gates compare against.
        /// </summary>
        public static ImmutableArray<RecordedFinding> ReadAll() =>
            [.. Files().SelectMany(path => ReadFile(path))];

        /// <summary>
        /// One file's findings.
        /// <para>
        /// It REFUSES rather than degrades: a file that is not well-formed, does not carry the expected root, or
        /// records a version this reader does not know throws with the filename in the message. A reader that
        /// quietly returned an empty list would turn a corrupted oracle into a passing test on every gate that
        /// compares against it.
        /// </para>
        /// </summary>
        private static ImmutableArray<RecordedFinding> ReadFile(string path)
        {
            XElement root = Root(path);
            string name = Path.GetFileName(path);

            string version = root.Attribute("version")?.Value
                ?? throw new InvalidDataException($"{name}: the root carries no 'version' attribute.");
            if (version != FormatVersion)
            {
                throw new InvalidDataException(
                    $"{name}: records format version '{version}', but this reader knows version {FormatVersion}.");
            }

            string caseName = CaseNameIn(root, name);
            return
            [
                .. root.Elements("finding").Select(finding => new RecordedFinding(
                    caseName,
                    Required(finding, "severity", name),
                    Required(finding, "code", name),
                    Required(finding, "category", name),
                    // Absent, not empty: a finding that names no element carries no locator attribute at all.
                    finding.Attribute("locator")?.Value,
                    Required(finding, "message", name))),
            ];
        }

        /// <summary>
        /// Writes one freshly generated oracle into <c>findings.generated/</c> beside the test binary and returns
        /// its path, so a regeneration can be diffed against the committed file before it is adopted. The bytes
        /// are written EXACTLY as the writer emitted them — CRLF and all — because that is what gets committed.
        /// </summary>
        public static string WriteGenerated(string oracleFile, byte[] generated) =>
            ReportOracleHarness.WriteGeneratedInto("findings.generated", oracleFile, generated);

        /// <summary>
        /// Asserts the generated bytes reproduce an oracle exactly, naming the first differing line when they do
        /// not — literally the same body <see cref="ReportOracleHarness.AssertMatchesOracle"/> runs, so the two
        /// oracle families fail the same way by construction rather than by two copies being kept in step. The
        /// full picture of an intended change comes from the regeneration workflow: run the <c>[Explicit]</c>
        /// regenerator and diff the emitted files.
        /// <para>Byte equality is the gate and NO normalization is applied: these oracles are CRLF and
        /// ISO-8859-1, and both are part of what is being asserted — which is the one thing this family passes
        /// differently from the report family, whose oracles tolerate a CRLF checkout.</para>
        /// </summary>
        /// <param name="oracleBytes">The committed oracle.</param>
        /// <param name="generated">What the writer produced this run.</param>
        /// <param name="oracleFile">The oracle's file name, for the message.</param>
        public static void AssertMatchesOracle(byte[] oracleBytes, byte[] generated, string oracleFile) =>
            // Latin-1 decodes every byte, so rendering the failure cannot itself fail on a mangled file.
            ReportOracleHarness.AssertBytesMatch(
                oracleBytes, generated, oracleFile, Encoding.Latin1, "\r\n");

        private static string Required(XElement finding, string attribute, string file) =>
            finding.Attribute(attribute)?.Value
            ?? throw new InvalidDataException($"{file}: a <finding> carries no '{attribute}' attribute.");

        /// <summary>
        /// The document element, read through the declared encoding.
        /// <para>
        /// The stream is handed to the parser unread, so the <c>ISO-8859-1</c> declaration is what decodes the
        /// bytes. Reading the file as text first and parsing the string would decode it as UTF-8 and mangle
        /// every Danish character before the parser ever saw it.
        /// </para>
        /// </summary>
        private static XElement Root(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"No findings oracle at '{path}'.", path);
            }

            using FileStream stream = File.OpenRead(path);
            string name = Path.GetFileName(path);

            XDocument document;
            try
            {
                document = XDocument.Load(stream);
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException($"{name}: not well-formed XML — {ex.Message}", ex);
            }

            XElement root = document.Root
                ?? throw new InvalidDataException($"{name}: the document has no root element.");
            return root.Name.LocalName == RootTag
                ? root
                : throw new InvalidDataException(
                    $"{name}: the root is <{root.Name.LocalName}>, not <{RootTag}>.");
        }
    }
}
