using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// One finding as an oracle file records it — the projection every reader of these oracles shares.
    ///
    /// <para><b>Why a record and not six loose strings.</b> Six readers consume these files, and four of them
    /// join several cells into a comparison row. When the cells were parsed independently at each site, renaming
    /// one was six edits nothing checked. Now it is a compile error.</para>
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
    /// link, exactly as <see cref="ReportOracles"/> is.
    ///
    /// <para><b>One projection, not six parsers.</b> Before this existed, each reader parsed the recording for
    /// itself and one of them walked up the directory tree into the source checkout to find it. Renaming a field
    /// was a silent break in whichever readers happened to be <c>[Explicit]</c>. Here it is a compile error.</para>
    ///
    /// <para><b>It takes a ROOT PATH.</b> Not a suite-local path helper: the whole point is that two assemblies
    /// share it, and a helper that only one of them has would put the harness back in one suite. The default is
    /// the copy beside the test binary, which is what <c>TestData.props</c> puts there.</para>
    /// </summary>
    internal static class FindingOracles
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
        public static TimeProvider Clock() => new FakeTimeProvider(ReportOracles.PinnedInstant);

        /// <summary>Where the oracle files sit beside the running test binary.</summary>
        public static string DefaultRoot =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "validation", "findings");

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
        /// <param name="root">Where the files are; null means <see cref="DefaultRoot"/>.</param>
        public static IEnumerable<object[]> Cases(string? root = null)
        {
            foreach (string path in Files(root))
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
        /// <param name="root">Where the files are; null means <see cref="DefaultRoot"/>.</param>
        public static ImmutableArray<string> Files(string? root = null)
        {
            string directory = root ?? DefaultRoot;
            return Directory.Exists(directory)
                ? [.. Directory.EnumerateFiles(directory, "*.xml").OrderBy(f => f, StringComparer.Ordinal)]
                : [];
        }

        /// <summary>One case's recorded findings, in file order.</summary>
        /// <param name="caseName">The corpus case name.</param>
        /// <param name="root">Where the files are; null means <see cref="DefaultRoot"/>.</param>
        public static ImmutableArray<RecordedFinding> Read(string caseName, string? root = null) =>
            ReadFile(Path.Combine(root ?? DefaultRoot, FileNameFor(caseName)));

        /// <summary>
        /// Every recorded finding across every case, in the order the files and their lines read — the whole
        /// recording, which is what the corpus-wide gates compare against.
        /// </summary>
        /// <param name="root">Where the files are; null means <see cref="DefaultRoot"/>.</param>
        public static ImmutableArray<RecordedFinding> ReadAll(string? root = null) =>
            [.. Files(root).SelectMany(path => ReadFile(path))];

        /// <summary>
        /// One file's findings.
        /// <para>
        /// It REFUSES rather than degrades: a file that is not well-formed, does not carry the expected root, or
        /// records a version this reader does not know throws with the filename in the message. A reader that
        /// quietly returned an empty list would turn a corrupted oracle into a passing test on every gate that
        /// compares against it.
        /// </para>
        /// </summary>
        public static ImmutableArray<RecordedFinding> ReadFile(string path) =>
            ReadFindings(Root(path), Path.GetFileName(path));

        /// <summary>The reader proper, over a root already parsed — the one body both a file and bytes go through.</summary>
        private static ImmutableArray<RecordedFinding> ReadFindings(XElement root, string name)
        {
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
        public static string WriteGenerated(string oracleFile, byte[] generated)
        {
            string directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "findings.generated");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, oracleFile);
            File.WriteAllBytes(path, generated);
            return path;
        }

        /// <summary>
        /// Asserts the generated bytes reproduce an oracle exactly, and — when they do not — says WHICH FINDINGS
        /// differ rather than which byte does.
        ///
        /// <para><b>Why structural.</b> These files are one finding per line, so a rule that changes what it
        /// reports moves whole lines. "Differs at byte 4 217" names a position; "3 findings changed, 1 no longer
        /// reported" names the change, and a reader can tell at once whether it is the change they made. A
        /// first-differing-byte report also hides everything after the first difference, which for a moved rule
        /// is the entire rest of the file.</para>
        ///
        /// <para><b>The hex window is the FALLBACK, not the report.</b> It appears only when the bytes cannot be
        /// parsed into findings at all — a corrupted or truncated file — because that is the one case where
        /// there are no rows to compare and the bytes themselves are the evidence.</para>
        /// </summary>
        /// <param name="oracleBytes">The committed oracle.</param>
        /// <param name="generated">What the writer produced this run.</param>
        /// <param name="oracleFile">The oracle's file name, for the message.</param>
        public static void AssertMatchesOracle(byte[] oracleBytes, byte[] generated, string oracleFile)
        {
            // Byte equality is the gate. Everything below only explains a failure; it never decides one, so no
            // normalization of any kind happens here: these oracles are CRLF and their line endings are part of
            // what is being asserted.
            if (oracleBytes.AsSpan().SequenceEqual(generated))
            {
                return;
            }

            bool oracleParsed = TryParse(oracleBytes, out ImmutableArray<RecordedFinding> expected, out string? oracleFault);
            bool generatedParsed = TryParse(generated, out ImmutableArray<RecordedFinding> actual, out string? generatedFault);
            if (oracleParsed && generatedParsed)
            {
                Assert.Fail(
                    $"{oracleFile}: the export no longer reproduces the oracle "
                    + $"({expected.Length} recorded, {actual.Length} produced)." + Environment.NewLine
                    + Differences(expected, actual));
                return;
            }

            Assert.Fail(
                $"{oracleFile}: the bytes could not be read as findings, so they are shown raw "
                + $"({oracleFault ?? generatedFault})." + Environment.NewLine
                + $"  oracle:    {Window(oracleBytes, generated)}" + Environment.NewLine
                + $"  generated: {Window(generated, oracleBytes)}");
        }

        /// <summary>The rows that changed, disappeared or appeared, as a reader would describe them.</summary>
        private static string Differences(
            ImmutableArray<RecordedFinding> expected, ImmutableArray<RecordedFinding> actual)
        {
            var problems = new List<string>();
            for (int i = 0; i < Math.Min(expected.Length, actual.Length); i++)
            {
                if (expected[i] != actual[i])
                {
                    problems.Add($"  #{i + 1} changed:" + Environment.NewLine
                        + $"      oracle:    {Describe(expected[i])}" + Environment.NewLine
                        + $"      produced:  {Describe(actual[i])}");
                }
            }

            for (int i = actual.Length; i < expected.Length; i++)
            {
                problems.Add($"  #{i + 1} no longer reported: {Describe(expected[i])}");
            }

            for (int i = expected.Length; i < actual.Length; i++)
            {
                problems.Add($"  #{i + 1} newly reported: {Describe(actual[i])}");
            }

            // The byte comparison already failed, so equal ROWS means the difference is in something a finding
            // does not carry — the header, the attribute order, an escape. Saying so beats an empty list.
            if (problems.Count == 0)
            {
                return "  every finding is identical, so the difference is in the document around them: the "
                    + "root's attributes, the attribute order, or an escape.";
            }

            return string.Join(Environment.NewLine, problems.Take(40))
                + (problems.Count > 40
                    ? $"{Environment.NewLine}  … and {problems.Count - 40} more"
                    : string.Empty);
        }

        private static string Describe(RecordedFinding finding) =>
            $"{finding.Severity} {finding.Code} [{finding.Category}] @{finding.Locator ?? "<none>"} "
            + $"\"{finding.Message}\"";

        /// <summary>Parses bytes held in memory, reporting the fault instead of throwing.</summary>
        private static bool TryParse(byte[] bytes, out ImmutableArray<RecordedFinding> findings, out string? fault)
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                findings = ReadFindings(Root(stream, "<generated>"), "<generated>");
                fault = null;
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException or XmlException)
            {
                findings = [];
                fault = ex.Message;
                return false;
            }
        }

        /// <summary>A short hex window around the first byte that differs — the last-resort evidence.</summary>
        private static string Window(byte[] bytes, byte[] other)
        {
            int at = 0;
            while (at < bytes.Length && at < other.Length && bytes[at] == other[at])
            {
                at++;
            }

            int from = Math.Max(0, at - 8);
            int length = Math.Min(24, bytes.Length - from);
            return length <= 0
                ? $"<{bytes.Length} bytes, ends before offset {at}>"
                : $"@{at}: " + Convert.ToHexString(bytes, from, length);
        }

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
            return Root(stream, Path.GetFileName(path));
        }

        /// <summary>
        /// The same parse over any stream, so bytes held in memory are read exactly as a file is — through the
        /// declared encoding and the same refusals — rather than by writing them to disk first.
        /// </summary>
        private static XElement Root(Stream stream, string name)
        {
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
