using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The oracle-gated E2E harness for SDK-owned report generation (spec R5/AC1, backlog T003+): each
    /// committed <c>tests/testdata/reports/*.txt</c> oracle regenerates byte-identically through the public
    /// facade — in-memory generation with the DEFAULT unicode icon stand-ins (D5), UTF-8 no BOM + LF from the
    /// generator (S06), CRLF→LF normalization applied to the ORACLE side only. The <c>full-*</c> oracles pin
    /// the generation clock to 2026-07-30 12:00 via the facade's injected <see cref="TimeProvider"/> (S10).
    /// Cases are added per vertical slice; the list below is the currently-implemented coverage.
    /// </summary>
    public class ReportOracleTests
    {
        /// <summary>The pinned report clock (S10): local 2026-07-30 12:00 on every machine.</summary>
        private sealed class ReportClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private static ProjectAppService App() =>
            new(TestSetup.Settings, new BuiltInCatalog(), new ReportClock());

        private static Project Load(string name) =>
            App().Load(new MemoryStream(TestData.ReadBytes(Path.Combine("projects", name)))).GetAwaiter().GetResult();

        // One row per implemented slice combination: (oracle file, fixture project, kind, mode).
        private static readonly object[][] TxtOracleCases =
        {
            new object[] { "std-project3-KompleksWired-enduserdoc-funktionsdokumentation.txt",
                "project3-KompleksWired-enduserdoc.vis", ReportKind.Functions, ReportMode.Standard },
            new object[] { "std-project5-Dokumentation-funktionsdokumentation.txt",
                "project5-Dokumentation.vis", ReportKind.Functions, ReportMode.Standard },
            new object[] { "full-project3-KompleksWired-enduserdoc-funktionsdokumentation.txt",
                "project3-KompleksWired-enduserdoc.vis", ReportKind.Functions, ReportMode.Full },
            new object[] { "full-project5-Dokumentation-funktionsdokumentation.txt",
                "project5-Dokumentation.vis", ReportKind.Functions, ReportMode.Full },
            new object[] { "std-project3-KompleksWired-enduserdoc-installationdokumentation.txt",
                "project3-KompleksWired-enduserdoc.vis", ReportKind.Installation, ReportMode.Standard },
            new object[] { "std-project5-Dokumentation-installationdokumentation.txt",
                "project5-Dokumentation.vis", ReportKind.Installation, ReportMode.Standard },
            new object[] { "full-project3-KompleksWired-enduserdoc-installationdokumentation.txt",
                "project3-KompleksWired-enduserdoc.vis", ReportKind.Installation, ReportMode.Full },
            new object[] { "full-project5-Dokumentation-installationdokumentation.txt",
                "project5-Dokumentation.vis", ReportKind.Installation, ReportMode.Full },
            new object[] { "std-project3-KompleksWired-enduserdoc-functionblockdokumentation.txt",
                "project3-KompleksWired-enduserdoc.vis", ReportKind.FunctionBlocks, ReportMode.Standard },
            new object[] { "std-project5-Dokumentation-functionblockdokumentation.txt",
                "project5-Dokumentation.vis", ReportKind.FunctionBlocks, ReportMode.Standard },
            new object[] { "full-project3-KompleksWired-enduserdoc-functionblockdokumentation.txt",
                "project3-KompleksWired-enduserdoc.vis", ReportKind.FunctionBlocks, ReportMode.Full },
            new object[] { "full-project5-Dokumentation-functionblockdokumentation.txt",
                "project5-Dokumentation.vis", ReportKind.FunctionBlocks, ReportMode.Full },
        };

        [TestCaseSource(nameof(TxtOracleCases))]
        public async Task TxtReport_RegeneratesOracle_ByteForByte(
            string oracleFile, string projectFile, ReportKind kind, ReportMode mode)
        {
            Project project = Load(projectFile);
            using var output = new MemoryStream();

            await App().GenerateReport(project, kind, mode, ReportMimeTypes.PlainText, output);

            AssertBytesEqual(
                NormalizeOracle(TestData.ReadBytes(Path.Combine("reports", oracleFile))),
                output.ToArray(),
                oracleFile);
        }

        /// <summary>D6/S06: CRLF→LF normalization on the oracle side only (git autocrlf may check out CRLF).</summary>
        private static byte[] NormalizeOracle(byte[] bytes) =>
            bytes.Where(b => b != (byte)'\r').ToArray();

        // Byte-equality with an RCA-friendly failure message: first mismatching line, expected vs actual.
        private static void AssertBytesEqual(byte[] expected, byte[] actual, string oracleFile)
        {
            if (expected.AsSpan().SequenceEqual(actual))
            {
                return;
            }
            string[] expectedLines = System.Text.Encoding.UTF8.GetString(expected).Split('\n');
            string[] actualLines = System.Text.Encoding.UTF8.GetString(actual).Split('\n');
            int line = 0;
            while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
            {
                line++;
            }
            string expectedLine = line < expectedLines.Length ? expectedLines[line] : "<missing — generator emitted extra lines>";
            string actualLine = line < actualLines.Length ? actualLines[line] : "<missing — generator emitted too few lines>";
            Assert.Fail(
                $"{oracleFile}: generated bytes differ from the oracle (expected {expected.Length} bytes, got {actual.Length}).\n" +
                $"First difference at line {line + 1}:\n  oracle: \"{expectedLine}\"\n  actual: \"{actualLine}\"");
        }
    }
}
