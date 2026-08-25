#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ihc.Tests.Shared;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The four small corrections of reportgenerality RL-6 (findings G9–G12) — each a corpus-fitted
    /// heuristic or a hand-copied constant that is right for the two oracle fixtures and wrong for a project
    /// that does the thing. Every case here is built in memory (decision D-c), and by measurement none of
    /// them moves an oracle byte: the fixtures contain no thousands-separator data line, no oversized
    /// address token, no unresolvable enum IDREF and no pin inside a settings section.
    /// </summary>
    public class ReportSmallCorrectionsTests
    {
        private static readonly (string, string)[] NoAttributes = [];

        private static readonly Lazy<ProjectAppService> Service =
            new(() => new ProjectAppService(TestSetup.Settings, new BuiltInCatalog(), ReportOracleHarness.Clock()));

        private static Project Probe(params ProjectElement[] children) =>
            new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"),
                 ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0xffff")],
                children));

        private static ProjectElement Groups(params ProjectElement[] localities) =>
            Node("groups", "_0x1000", NoAttributes, localities);

        private static async Task<string> Report(Project project, ReportKind kind)
        {
            using var output = new MemoryStream();
            await Service.Value.GenerateReport(project, kind, ReportMode.Full, ReportMimeTypes.PlainText, output);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        // ----- G9: the module-table sort accepted thousands separators -----

        [Test]
        public async Task ModuleTable_SortsByTheDataLineNumberOnly_NotAsAThousandsGroupedNumber_G9()
        {
            // "1,5" is a data line the invariant thousands separator turns into 15 under NumberStyles.Any,
            // which sorts it past the real data line 10.
            Project project = Probe(
                Node("documentation_modules", "_0x2000", NoAttributes,
                    Node("dataline_input_modules", "_0x2010", NoAttributes,
                        Node("dataline_input_module", "_0x2011", [("dataline", "1,5"), ("module_type", "Alpha")]),
                        Node("dataline_input_module", "_0x2012", [("dataline", "2"), ("module_type", "Bravo")]),
                        Node("dataline_input_module", "_0x2013", [("dataline", "10"), ("module_type", "Charlie")]))),
                Groups());

            string[] rows = ReportProbe.TableRows(await Report(project, ReportKind.Installation), "Datalinie inputmoduler");

            Assert.That(rows.Select(ReportProbe.FirstCell), Is.EqualTo(new[] { "1,5", "2", "10" }),
                "G9: a data line that is not a plain integer must not be re-read as a thousands-grouped "
                + "number — it is unparseable and sorts with the other unparseable ones, ahead of 2 and 10");
        }

        // ----- G10: display and sort disagreed for an oversized address token -----

        [Test]
        public async Task CrossReference_OversizedAddressToken_SortsWhereItDisplays_G10()
        {
            // The address label parses as int and refuses this token, so the row displays "?"; the sort key
            // parsed as long and accepted it, so the same row sorted after every real address.
            Project project = Probe(Groups(
                Node("group", "_0x3100", [("name", "Stue")],
                    Node("product_dataline", "_0x3200", [("name", "Prod")],
                        Node("dataline_input", "_0x3210", [("name", "Alpha")]),
                        Node("dataline_input", "_0x3220", [("name", "Bravo"), ("address_dataline", "_0x100000000")]),
                        Node("dataline_input", "_0x3230", [("name", "Charlie"), ("address_dataline", "_0x03")])))));

            string[] rows = ReportProbe.TableRows(await Report(project, ReportKind.Installation), "Datalinie indgange");

            Assert.Multiple(() =>
            {
                Assert.That(rows.Select(ReportProbe.FirstCell), Is.EqualTo(new[] { "?", "?", "1 . 03" }),
                    "G10: a token the address label cannot decode displays as unaddressed, so it must SORT "
                    + "as unaddressed — with the other '?' rows, in document order");
                Assert.That(rows.Select(row => row.Contains("Bravo", StringComparison.Ordinal)).ToArray(),
                    Is.EqualTo(new[] { false, true, false }),
                    "G10: and the row that moved is the oversized one, not the absent-address one");
            });
        }

        // ----- G12: the value formats hand-copied the DTD's own defaults -----

        [Test]
        public async Task VariableWithNoStoredValue_ReadsTheDtdDefault_NotAHandCopiedOne_G12()
        {
            // A pin in a settings section renders "= value" like any variable, but has no arm of its own, so
            // it fell through to the numeric-zero catch-all — while its DTD default is "off". The
            // temperature beside it pins that routing the read through the schema still reproduces the
            // decimal default the catch-all used to hard-code.
            Project project = Probe(Groups(
                Node("group", "_0x4100", [("name", "Stue")],
                    Node("functionblock", "_0x4200", [("name", "Blok")],
                        Node("settings", "_0x4210", [("name", "Indstillinger")],
                            Node("resource_output", "_0x4220", [("name", "Udgang")]),
                            Node("resource_temperature", "_0x4230", [("name", "Temperatur")]),
                            Node("resource_scene", "_0x4240", [("name", "Scenarie")]))))));

            string report = await Report(project, ReportKind.FunctionBlocks);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.RowValue(report, "Udgang"), Is.EqualTo("off"),
                    "G12: <resource_output> declares inivalue (on | off) \"off\" — the report must read that, "
                    + "not the catch-all's fabricated \"0\"");
                Assert.That(ReportProbe.RowValue(report, "Temperatur"), Is.EqualTo("0.00 C"),
                    "G12: the schema read reproduces the decimal default the old literal hard-coded");
                Assert.That(ReportProbe.RowValue(report, "Scenarie"), Is.Null,
                    "G12: <resource_scene> declares no inivalue at all, so there is no value to show — and "
                    + "no fabricated one either (G11 suppresses the bare '=')");
            });
        }
    }
}
