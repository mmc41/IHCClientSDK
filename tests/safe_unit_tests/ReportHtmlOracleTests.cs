using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Projects;

namespace safe_unit_tests;

/// <summary>
/// The oracle-gated E2E harness for the HTML report format (spec R5/AC1, D5/D9): each committed
/// <c>tests/testdata/reports/*.html</c> oracle regenerates byte-identically through the public facade using
/// the app's REAL <see cref="SvgReportIconProvider"/> — the oracles embed OpenVisual's inline
/// <c>&lt;symbol&gt;</c> sprite and <c>#icon-logo</c> banner, so the HTML byte contract lives here in
/// <c>safe_unit_tests</c> (which references the app assembly) rather than in the SDK-only suite. The provider
/// must be constructible WITHOUT a running Avalonia platform (S09 — plain embedded-resource read). Generator
/// emits UTF-8 no BOM + LF; CRLF→LF normalization is applied to the ORACLE side only (S06). Cases are added
/// per vertical slice.
/// </summary>
public class ReportHtmlOracleTests
{
    /// <summary>The pinned report clock (S10): local 2026-07-30 12:00 on every machine.</summary>
    private sealed class ReportClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static ProjectAppService App() => new(new IhcSettings(), new BuiltInCatalog(), new ReportClock());

    private static string TestData(params string[] parts) =>
        Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "testdata" }.Concat(parts).ToArray());

    private static Task<Project> Load(string projectFile) => App().Load(TestData("projects", projectFile));

    // One row per implemented slice combination: (oracle file, fixture project, kind, mode).
    private static readonly object[][] HtmlOracleCases =
    {
        new object[] { "std-project3-KompleksWired-enduserdoc-funktionsdokumentation.html",
            "project3-KompleksWired-enduserdoc.vis", ReportKind.Functions, ReportMode.Standard },
        new object[] { "std-project5-Dokumentation-funktionsdokumentation.html",
            "project5-Dokumentation.vis", ReportKind.Functions, ReportMode.Standard },
        new object[] { "full-project3-KompleksWired-enduserdoc-funktionsdokumentation.html",
            "project3-KompleksWired-enduserdoc.vis", ReportKind.Functions, ReportMode.Full },
        new object[] { "full-project5-Dokumentation-funktionsdokumentation.html",
            "project5-Dokumentation.vis", ReportKind.Functions, ReportMode.Full },
        new object[] { "std-project3-KompleksWired-enduserdoc-installationdokumentation.html",
            "project3-KompleksWired-enduserdoc.vis", ReportKind.Installation, ReportMode.Standard },
        new object[] { "std-project5-Dokumentation-installationdokumentation.html",
            "project5-Dokumentation.vis", ReportKind.Installation, ReportMode.Standard },
        new object[] { "full-project3-KompleksWired-enduserdoc-installationdokumentation.html",
            "project3-KompleksWired-enduserdoc.vis", ReportKind.Installation, ReportMode.Full },
        new object[] { "full-project5-Dokumentation-installationdokumentation.html",
            "project5-Dokumentation.vis", ReportKind.Installation, ReportMode.Full },
        new object[] { "std-project3-KompleksWired-enduserdoc-functionblockdokumentation.html",
            "project3-KompleksWired-enduserdoc.vis", ReportKind.FunctionBlocks, ReportMode.Standard },
        new object[] { "std-project5-Dokumentation-functionblockdokumentation.html",
            "project5-Dokumentation.vis", ReportKind.FunctionBlocks, ReportMode.Standard },
        new object[] { "full-project3-KompleksWired-enduserdoc-functionblockdokumentation.html",
            "project3-KompleksWired-enduserdoc.vis", ReportKind.FunctionBlocks, ReportMode.Full },
        new object[] { "full-project5-Dokumentation-functionblockdokumentation.html",
            "project5-Dokumentation.vis", ReportKind.FunctionBlocks, ReportMode.Full },
    };

    [TestCaseSource(nameof(HtmlOracleCases))]
    public async Task HtmlReport_RegeneratesOracle_ByteForByte(
        string oracleFile, string projectFile, ReportKind kind, ReportMode mode)
    {
        Project project = await Load(projectFile);
        using var output = new MemoryStream();

        await App().GenerateReport(project, kind, mode, ReportMimeTypes.Html, output, new SvgReportIconProvider());

        AssertBytesEqual(
            NormalizeOracle(File.ReadAllBytes(TestData("reports", oracleFile))),
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
