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

        /// <summary>
        /// Regenerates all twelve <c>*.txt</c> oracles into <c>reports.generated/</c> beside the test binary.
        /// <see cref="ExplicitAttribute"/> so it never runs in the gate: adopting the output is the deliberate act
        /// of diffing it against <c>tests/testdata/reports/</c>, explaining every changed line by a rule the same
        /// change introduced, and copying it over — which the Definition of Done only permits in a task that names
        /// its oracle impact up front. A DOCUMENTATION-category rule changes these files, because the Fuld report
        /// renders that category as its appendix.
        /// </summary>
        [Test]
        [Explicit("Regenerates the checked-in *.txt report oracles. Run deliberately, then diff the emitted files "
            + "against tests/testdata/reports/ before copying them over.")]
        [Category("OracleRegeneration")]
        public async Task Regenerate_TheTxtOracles()
        {
            foreach (object[] oracleCase in TxtOracleCases())
            {
                Project project = Load((string)oracleCase[1]);
                using var output = new MemoryStream();

                await App().GenerateReport(
                    project, (ReportKind)oracleCase[2], (ReportMode)oracleCase[3], ReportMimeTypes.PlainText, output);

                TestContext.Out.WriteLine(ReportOracles.WriteGenerated((string)oracleCase[0], output.ToArray()));
            }
        }
    }
}
