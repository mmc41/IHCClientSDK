using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The scope probes (reportgenerality T002): the six shapes the 27-fixture corpus never exercises, so
    /// the LATENT findings G6/G7/G8/G11/G14 have a permanent home instead of living only in the review.
    /// Every assertion states <b>today's</b> behaviour and cites its finding id, and each one a ruling will
    /// flip is named <c>…_Pending&lt;ruling&gt;</c> so the diff that flips it is unmistakable.
    /// <para>The probes are built <b>in memory</b> (decision D-c as amended by C-5(b)): they need no file on
    /// disk, so they add nothing to the fixture corpus and cannot perturb a suite that enumerates it — and
    /// no existing <c>.vis</c> is read-modify-written into a probe (C-5(a)).</para>
    /// <para>Assertions read the <b>Full</b> text rendering and probe membership by the <c>(ID _0x…)</c>
    /// chip, which is exact where a name substring is not (see <see cref="ReportProbe"/>).</para>
    /// </summary>
    public class ReportScopeProbeTests
    {
        private static readonly (string, string)[] NoAttributes = [];

        private static readonly Lazy<ProjectAppService> Service =
            new(() => new ProjectAppService(TestSetup.Settings, new BuiltInCatalog(), ReportOracleHarness.Clock()));

        /// <summary>A minimal well-formed project whose <c>groups</c> holds the given localities.</summary>
        private static Project Probe(params ProjectElement[] localities) =>
            new(Tree.Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"),
                 ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0xffff")],
                Tree.Node("groups", "_0x1000", NoAttributes, localities)));

        private static async Task<string> Report(Project project, ReportKind kind)
        {
            using var output = new MemoryStream();
            await Service.Value.GenerateReport(project, kind, ReportMode.Full, ReportMimeTypes.PlainText, output);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        // ----- G6: a terminal nested below its product -----

        // The vendor's own sensor products nest their terminals inside a settings container, so this is a
        // catalog-standard shape, not an exotic one.
        private static Project NestedTerminalProbe() =>
            Probe(Tree.Node("group", "_0x2100", [("name", "Stue")],
                Tree.Node("product_dataline", "_0x2200",
                    [("name", "Sensor"), ("enduser_report", "yes"), ("position", "Ved dor")],
                    Tree.Node("dataline_input", "_0x2210", [("name", "Direkte terminal")]),
                    Tree.Node("settings", "_0x2220", [("name", "Indstillinger")],
                        Tree.Node("dataline_input", "_0x2230", [("name", "Indlejret terminal")])))));

        [Test]
        public async Task NestedTerminal_ReachesTheEndUserReport_G6()
        {
            string report = await Report(NestedTerminalProbe(), ReportKind.Functions);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.Renders(report, "_0x2210"), Is.True,
                    "a terminal that is a direct child of its product reaches the end-user report");
                Assert.That(ReportProbe.Renders(report, "_0x2230"), Is.True,
                    "G6: a terminal nested one level deeper reaches it too — the end-user report locates "
                    + "terminals by descent, as the installation report always has");
            });
        }

        [Test]
        public async Task NestedTerminal_ReachesTheInstallationReportToo_SoTheTwoReportsAgree_G6()
        {
            string report = await Report(NestedTerminalProbe(), ReportKind.Installation);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.Renders(report, "_0x2230"), Is.True,
                    "G6: the installation report locates terminals by descent — this is the answer the "
                    + "end-user report now agrees with, where the two used to disagree about one tree");
                Assert.That(ReportProbe.CrossReferenceSection(report), Does.Contain("Indlejret terminal"),
                    "G6: and it carries a cross-reference row too");
            });
        }

        // ----- CL-7: a product in a nested group belongs to that nested locality ONLY -----

        [Test]
        public async Task ProductInANestedGroup_IsListedOnceUnderTheNestedLocality_CL7()
        {
            Project project = Probe(Tree.Node("group", "_0x3100", [("name", "Etage 1")],
                Tree.Node("group", "_0x3110", [("name", "Stue")],
                    Tree.Node("product_dataline", "_0x3120", [("name", "Kontakt"), ("enduser_report", "yes")],
                        Tree.Node("dataline_input", "_0x3130", [("name", "Tryk")])))));

            string report = await Report(project, ReportKind.Functions);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.RenderCount(report, "_0x3120"), Is.EqualTo(1),
                    "CL-7: TreeIndex.Localities already flattens a nested group into its own locality, so a "
                    + "product under it must be listed once — not once per ancestor locality");
                Assert.That(ReportProbe.RenderCount(report, "_0x3130"), Is.EqualTo(1),
                    "CL-7: and its terminal once with it");
            });
        }

        // ----- G8: function blocks found only as direct children of a group -----

        [Test]
        public async Task FunctionBlockBelowANonGroupContainer_ReachesTheReport_G8()
        {
            Project project = Probe(Tree.Node("group", "_0x4100", [("name", "Stue")],
                Tree.Node("functionblock", "_0x4200", [("name", "Direkte blok")]),
                Tree.Node("custom_widget", "_0x4300", [("name", "Beholder")],
                    Tree.Node("functionblock", "_0x4310", [("name", "Begravet blok")]))));

            string report = await Report(project, ReportKind.FunctionBlocks);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.RenderCount(report, "_0x4200"), Is.EqualTo(1),
                    "a block that is a direct child of its locality reaches the FB report");
                Assert.That(ReportProbe.RenderCount(report, "_0x4310"), Is.EqualTo(1),
                    "G8: a block one container deeper reaches it too, exactly once — it used to be absent "
                    + "entirely, with no warning to say so");
            });
        }

        [Test]
        public async Task FunctionBlockUnderANestedGroup_IsListedExactlyOnce_G8_CL7()
        {
            Project project = Probe(Tree.Node("group", "_0x4400", [("name", "Etage 1")],
                Tree.Node("group", "_0x4410", [("name", "Stue")],
                    Tree.Node("functionblock", "_0x4420", [("name", "Indlejret blok")]))));

            string report = await Report(project, ReportKind.FunctionBlocks);

            Assert.That(ReportProbe.RenderCount(report, "_0x4420"), Is.EqualTo(1),
                "G8/CL-7: a block under a nested group belongs to that nested locality only — plain descent "
                + "would list it under the parent locality as well, which T006 must not do");
        }

        // ----- G7: an open-world product root is half-present -----

        // A product_-prefixed root the report's closed family table does not know: the shared classifier
        // still recognises it as a product, so its terminals carry full product metadata across.
        private static Project OpenWorldProductProbe() =>
            Probe(Tree.Node("group", "_0x5100", [("name", "Stue")],
                Tree.Node("product_customsensor", "_0x5200",
                    [("name", "Egen sensor"), ("enduser_report", "yes"), ("position", "Ved hoveddor"),
                     ("documentation_tag", "S9-01"), ("cabletype", "IHC LINK-10"), ("cablenumber", "42"),
                     ("power_group", "F/5")],
                    Tree.Node("dataline_input", "_0x5210",
                        [("name", "Egen indgang"), ("cable_colour", "Rod"), ("address_dataline", "_0x1801")]))));

        [Test]
        public async Task OpenWorldProductRoot_GetsAGenericInstallationComponentBlock_G7a()
        {
            string report = await Report(OpenWorldProductProbe(), ReportKind.Installation);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.Renders(report, "_0x5200"), Is.True,
                    "G7(a): an unrecognised product root gets a component block of its own — it used to get "
                    + "none at all, while its terminals were documented in full in the cross-reference");
                Assert.That(ReportProbe.Renders(report, "_0x5210"), Is.True,
                    "G7(a): carrying the terminal sub-table, because the product has dataline descendants "
                    + "(the settled RL-2c sub-ruling)");
                // "Identifikationskode" and "Serie nummer" are component-block FIELD labels and appear
                // nowhere else; the wire-colour labels are also Specielle Produkter column headers, so
                // their presence would prove nothing.
                Assert.That(report, Does.Not.Contain("Identifikationskode").And.Not.Contain("Serie nummer"),
                    "G7(a): GENERIC means the three shared rows and no family rows — an unrecognised root "
                    + "must not be handed some known family's fields");
            });
        }

        // The hazard the generic arm has to dodge: s0_device IS a product root by the shared classifier, but
        // it renders through its own "S0 Device" table and never as a locality component. Both oracle
        // fixtures carry one, so admitting it here would have moved bytes.
        [Test]
        public async Task S0Device_StaysOutOfTheComponentBlocks_AndKeepsItsOwnTable_G7a()
        {
            Project project = Probe(Tree.Node("group", "_0x5300", [("name", "Stue")],
                Tree.Node("s0_device", "_0x5400",
                    [("name", "Energimaaler"), ("documentation_tag", "S0-01"), ("position", "Ved tavle")])));

            string report = await Report(project, ReportKind.Installation);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.TableRowCount(report, "S0 Device"), Is.EqualTo(1),
                    "the S0 device is documented by its own table");
                Assert.That(ReportProbe.RenderCount(report, "_0x5400"), Is.EqualTo(1),
                    "exactly once — the generic component block must not claim it as well");
            });
        }

        [Test]
        public async Task OpenWorldProductTerminal_StillCarriesItsProductMetadataIntoTheCrossReference_G7a()
        {
            string crossReference = ReportProbe.CrossReferenceSection(
                await Report(OpenWorldProductProbe(), ReportKind.Installation));

            Assert.Multiple(() =>
            {
                Assert.That(crossReference, Does.Contain("Egen indgang"),
                    "G7(a): the cross-reference is scoped by terminal tag, so the terminal is there");
                Assert.That(crossReference, Does.Contain("Egen sensor").And.Contain("S9-01"),
                    "G7(a): and NearestProduct resolves the unrecognised root, so its name and Id-kode cross "
                    + "over — which is the answer its own component block now agrees with");
            });
        }

        [Test]
        public async Task OpenWorldProductRoot_IsAdmittedToTheEndUserReportWhenFlagged_G7b()
        {
            string report = await Report(OpenWorldProductProbe(), ReportKind.Functions);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.Renders(report, "_0x5100"), Is.True,
                    "its locality renders");
                Assert.That(ReportProbe.Renders(report, "_0x5200"), Is.True,
                    "G7(b): admission is by product root, not by an exact two-tag match, so a flagged "
                    + "open-world product enters the end-user report");
                Assert.That(ReportProbe.Renders(report, "_0x5210"), Is.True,
                    "G7(b): with its terminals under it — admitting the product but rendering it as a bare "
                    + "name with no children would move the same contradiction one level down");
            });
        }

        // ----- G11: an enum whose inivalue IDREF resolves to nothing -----

        [Test]
        public async Task EnumWithUnresolvableInivalue_RendersNoValueAtAll_G11()
        {
            Project project = Probe(Tree.Node("group", "_0x6100", [("name", "Stue")],
                Tree.Node("functionblock", "_0x6200", [("name", "Blok")],
                    Tree.Node("settings", "_0x6210", [("name", "Indstillinger")],
                        Tree.Node("resource_enum", "_0x6220", [("name", "Tilstand"), ("inivalue", "_0x0")])))));

            string report = await Report(project, ReportKind.FunctionBlocks);

            string? row = Array.Find(report.Split('\n'), line => ReportProbe.RowLabel(line)?.StartsWith("Tilstand", StringComparison.Ordinal) == true);

            Assert.Multiple(() =>
            {
                Assert.That(row, Is.Not.Null, "the enum still renders its row");
                Assert.That(row, Does.Not.EndWith("="),
                    "G11: the null/dangling IDREF formats to nothing, and a value that resolves to nothing "
                    + "suppresses the '=' rather than leaving a bare equals sign with no value after it");
                Assert.That(ReportProbe.RowValue(report, "Tilstand"), Is.Null,
                    "G11: and the row carries no value column at all");
            });
        }

        // ----- G14: a section container with no name -----

        [Test]
        public async Task UnnamedSectionContainer_RendersABareIconRow_G14_DeferredRL7()
        {
            Project project = Probe(Tree.Node("group", "_0x7100", [("name", "Stue")],
                Tree.Node("functionblock", "_0x7200", [("name", "Blok")],
                    Tree.Node("settings", "_0x7210", NoAttributes,   // the DTD default for @name is empty
                        Tree.Node("resource_flag", "_0x7220", [("name", "Flag"), ("inivalue", "on")])))));

            string report = await Report(project, ReportKind.FunctionBlocks);

            Assert.Multiple(() =>
            {
                Assert.That(Array.Exists(report.Split('\n'),
                        line => ReportProbe.RowDepth(line) == 0 && ReportProbe.RowLabel(line)?.Length == 0),
                    Is.True,
                    "G14: an unnamed section renders as a bare icon with no label. RL-7 is DEFERRED — there "
                    + "is no evidence for what it should say instead (C-1 forbids the measurement), so this "
                    + "pins today's behaviour rather than blessing it");
                Assert.That(ReportProbe.SectionChildCounts(report, "Blok " + ReportProbe.Chip("_0x7200")), Is.EqualTo(new[] { 1 }),
                    "G14: the unlabelled row is still a real section row that owns its children");
            });
        }
    }
}
