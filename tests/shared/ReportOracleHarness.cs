using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Ihc.Vis;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// The report-oracle harness shared by the two suites that own a format's byte contract —
    /// <c>safe_project_tests</c> the <c>*.txt</c> oracles (default unicode stand-ins), <c>safe_unit_tests</c>
    /// the <c>*.html</c> ones (OpenVisual's SVG provider, so they need the app assembly). Both drive the same
    /// fixture × kind × mode matrix against the same oracle-naming convention, so the matrix, the naming rule,
    /// the pinned clock and the byte assert live here once. Compiled into both suites through a
    /// <c>&lt;Compile Include&gt;</c> link, mirroring the shared fixture copy in <c>tests/TestData.props</c>.
    /// </summary>
    internal static class ReportOracleHarness
    {
        /// <summary>The instant every <c>full-*</c> oracle's generation timestamp is pinned to (S10):
        /// 2026-07-30 12:00, local on every machine.
        /// <para>Internal rather than private because the findings oracles pin the SAME instant. Two constants
        /// would be two magic dates to remember and one more thing that can silently diverge; one instant, read
        /// by both families, cannot.</para></summary>
        internal static readonly DateTimeOffset PinnedInstant = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

        // The report-kind stem each oracle filename carries.
        private static readonly (ReportKind Kind, string Stem)[] Kinds =
        {
            (ReportKind.Functions, "funktionsdokumentation"),
            (ReportKind.Installation, "installationdokumentation"),
            (ReportKind.FunctionBlocks, "functionblockdokumentation"),
        };

        // The fixture projects the oracles were captured from (their .vis stem).
        private static readonly string[] Fixtures =
        {
            "project3-KompleksWired-enduserdoc",
            "project5-Dokumentation",
        };

        /// <summary>The pinned report clock: <see cref="TimeProvider.GetLocalNow"/> answers
        /// <see cref="PinnedInstant"/> on every machine (<c>FakeTimeProvider</c> defaults its local zone to
        /// UTC), which is what makes the <c>full-*</c> oracles reproducible.</summary>
        public static TimeProvider Clock() => new FakeTimeProvider(PinnedInstant);

        /// <summary>
        /// Every fixture × kind × mode case for one output format, as NUnit
        /// <c>(oracle file, fixture project, kind, mode)</c> rows. The oracle filename is fully mechanical —
        /// <c>{std|full}-{fixture}-{kind stem}.{extension}</c> — so the 12 rows per format are generated
        /// rather than retyped, and a missing combination shows up as a missing file, not a missing row.
        /// </summary>
        public static IEnumerable<object[]> Cases(string extension) =>
            from fixture in Fixtures
            from kind in Kinds
            from mode in new[] { ReportMode.Standard, ReportMode.Full }
            select new object[]
            {
                $"{(mode == ReportMode.Full ? "full" : "std")}-{fixture}-{kind.Stem}.{extension}",
                fixture + ".vis",
                kind.Kind,
                mode,
            };

        /// <summary>
        /// Writes one freshly generated report into <c>reports.generated/</c> beside the test binary, and returns
        /// its path. The oracles are REGENERATED, never retyped (D04): a task whose rules add rows to the Fuld
        /// report's documentation appendix runs its suite's <c>[Explicit]</c> regeneration test, diffs the emitted
        /// files against <c>tests/testdata/reports/</c>, explains every changed line, and only then copies them
        /// over — the same discipline the validation characterization recording follows.
        /// <para>LF is written exactly as the generator emitted it: <c>.gitattributes</c> pins these oracles to
        /// <c>eol=lf</c>, so the bytes that land here are the bytes that get committed.</para>
        /// </summary>
        public static string WriteGenerated(string oracleFile, byte[] generated) =>
            WriteGeneratedInto("reports.generated", oracleFile, generated);

        /// <summary>
        /// Writes one freshly generated oracle into <paramref name="subdirectory"/> beside the test binary and
        /// returns its path, so a regeneration can be diffed against the committed file before it is adopted.
        /// The bytes are written EXACTLY as the generator emitted them, because those are the bytes that get
        /// committed.
        /// <para>Internal and parameterised because BOTH oracle families do this, differing only in the
        /// directory they write to.</para>
        /// </summary>
        internal static string WriteGeneratedInto(string subdirectory, string oracleFile, byte[] generated)
        {
            string directory = Path.Combine(TestContext.CurrentContext.TestDirectory, subdirectory);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, oracleFile);
            File.WriteAllBytes(path, generated);
            return path;
        }

        /// <summary>
        /// Asserts the generated bytes reproduce the oracle exactly, with an RCA-friendly message naming the
        /// first differing line. CRLF→LF normalization is applied to the ORACLE side only (D6/S06: the
        /// generator always emits LF, but git autocrlf may check the oracle out with CRLF).
        /// </summary>
        public static void AssertMatchesOracle(byte[] oracleBytes, byte[] generated, string oracleFile) =>
            AssertBytesMatch(
                [.. oracleBytes.Where(b => b != (byte)'\r')], generated, oracleFile, Encoding.UTF8, "\n");

        /// <summary>
        /// Asserts generated bytes reproduce an oracle exactly, naming the first differing line when they do not.
        /// <para>ONE body for BOTH oracle families, so they fail the same way by construction rather than by two
        /// copies being kept in step. A family supplies only what actually differs: how its bytes decode, where
        /// its lines end, and — via <paramref name="expected"/> — whatever normalization its own byte contract
        /// allows. Byte equality of what is passed in is the gate; this method normalizes nothing itself.</para>
        /// </summary>
        /// <param name="expected">The committed oracle, already normalized as its family's contract permits.</param>
        /// <param name="generated">What the generator produced this run.</param>
        /// <param name="oracleFile">The oracle's file name, for the message.</param>
        /// <param name="encoding">The family's encoding — it must decode every byte, so rendering a failure
        /// cannot itself fail on a mangled file.</param>
        /// <param name="newline">The family's line separator.</param>
        internal static void AssertBytesMatch(
            byte[] expected, byte[] generated, string oracleFile, Encoding encoding, string newline)
        {
            if (expected.AsSpan().SequenceEqual(generated))
            {
                return;
            }
            string[] expectedLines = encoding.GetString(expected).Split(newline);
            string[] actualLines = encoding.GetString(generated).Split(newline);
            int line = 0;
            while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
            {
                line++;
            }
            string expectedLine = line < expectedLines.Length
                ? expectedLines[line]
                : "<missing — generator emitted extra lines>";
            string actualLine = line < actualLines.Length
                ? actualLines[line]
                : "<missing — generator emitted too few lines>";
            Assert.Fail(
                $"{oracleFile}: generated bytes differ from the oracle (expected {expected.Length} bytes, got {generated.Length}).\n" +
                $"First difference at line {line + 1}:\n  oracle: \"{expectedLine}\"\n  actual: \"{actualLine}\"");
        }
    }
}
