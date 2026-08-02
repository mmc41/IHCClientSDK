using System.IO;
using System.Threading.Tasks;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The oracle-gated E2E harness for SDK-owned report generation (spec R5/AC1, backlog T003+): each
    /// committed <c>tests/testdata/reports/*.txt</c> oracle regenerates byte-identically through the public
    /// facade — in-memory generation with the DEFAULT unicode icon stand-ins (D5), UTF-8 no BOM + LF from the
    /// generator (S06), CRLF→LF normalization applied to the ORACLE side only. The coverage matrix, oracle
    /// naming, pinned generation clock (S10) and byte assert are the shared <see cref="ReportOracles"/>
    /// harness; the HTML half of the same contract lives in <c>safe_unit_tests</c>.
    /// </summary>
    public class ReportOracleTests
    {
        private static ProjectAppService App() =>
            new(TestSetup.Settings, new BuiltInCatalog(), ReportOracles.Clock());

        private static Project Load(string name) =>
            App().Load(new MemoryStream(TestData.ReadBytes(Path.Combine("projects", name)))).GetAwaiter().GetResult();

        private static object[][] TxtOracleCases() => [.. ReportOracles.Cases("txt")];

        [TestCaseSource(nameof(TxtOracleCases))]
        public async Task TxtReport_RegeneratesOracle_ByteForByte(
            string oracleFile, string projectFile, ReportKind kind, ReportMode mode)
        {
            Project project = Load(projectFile);
            using var output = new MemoryStream();

            await App().GenerateReport(project, kind, mode, ReportMimeTypes.PlainText, output);

            ReportOracles.AssertMatchesOracle(
                TestData.ReadBytes(Path.Combine("reports", oracleFile)), output.ToArray(), oracleFile);
        }
    }
}
