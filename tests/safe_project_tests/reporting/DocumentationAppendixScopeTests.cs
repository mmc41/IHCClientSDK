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
    /// The acceptance criteria for reportgenerality T004 (RL-1 / finding G5): the Full-mode "Fejl i
    /// dokumentation" appendix must search the same tree its report body does. It used to visit only
    /// top-level groups, their DIRECT product children and those products' DIRECT terminal children — so a
    /// vendor-catalog-standard sensor, which nests its <c>dataline_input</c> inside a <c>settings</c>
    /// container, raised no terminal-level finding at all while the installation body documented it in full.
    /// <para>The two halves of the fix are asserted together on purpose. Widening the validator alone would
    /// have produced WRONG rows rather than missing ones: the appendix resolved a terminal's product as its
    /// immediate parent, which for a nested terminal is the <c>settings</c> container — printing the
    /// container as the <i>Produkt</i> and the real product as the <i>Lokalitet</i>. Both cells are asserted
    /// here, positionally.</para>
    /// </summary>
    public class DocumentationAppendixScopeTests
    {
        private static readonly (string, string)[] NoAttributes = [];

        /// <summary>Every documentation field a product is checked for, filled — so a probe raises only the
        /// terminal-level findings it is built to raise.</summary>
        private static readonly (string, string)[] DocumentedProduct =
        [
            ("name", "Sensor"), ("documentation_tag", "S1-10"), ("power_group", "F/5"),
            ("cabletype", "IHC LINK-10"), ("cablenumber", "21"), ("position", "Ved dor"),
        ];

        private static readonly Lazy<ProjectAppService> Service =
            new(() => new ProjectAppService(TestSetup.Settings, new BuiltInCatalog(), ReportOracleHarness.Clock()));

        private static Project Probe(params ProjectElement[] localities) =>
            new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"),
                 ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0xffff")],
                Node("groups", "_0x1000", NoAttributes, localities)));

        private static async Task<string[][]> Appendix(Project project)
        {
            using var output = new MemoryStream();
            await Service.Value.GenerateReport(project, ReportKind.Installation, ReportMode.Full,
                ReportMimeTypes.PlainText, output);
            return ReportProbe.AppendixRows(Encoding.UTF8.GetString(output.ToArray()));
        }

        [Test]
        public async Task TerminalNestedInsideItsProduct_IsChecked_AndResolvesAllFourCells_G5()
        {
            // The vendor's sensor shape: the terminal sits under a settings container, self-closed (so
            // unlinked), with no cable colour and no data-line address — three findings the old scope missed.
            Project project = Probe(Node("group", "_0x2100", [("name", "Stue")],
                Node("product_dataline", "_0x2200", DocumentedProduct,
                    Node("settings", "_0x2210", [("name", "Indstillinger")],
                        Node("dataline_input", "_0x2220", [("name", "Temperatur sensor indgang")])))));

            string[][] rows = await Appendix(project);

            Assert.That(rows, Is.EqualTo(new[]
            {
                new[] { "Stue", "Sensor", "Temperatur sensor indgang", "Ikke forbundet" },
                new[] { "Stue", "Sensor", "Temperatur sensor indgang", "Mangler Ledningsfarve" },
                new[] { "Stue", "Sensor", "Temperatur sensor indgang", "Mangler Adresse" },
            }), "G5: the appendix reaches the nested terminal, and resolves Produkt to the PRODUCT (not the "
                + "settings container it sits in) and Lokalitet to the group (not the product)");
        }

        [Test]
        public async Task ProductUnderANestedGroup_IsCheckedOnce_UnderItsNearestLocality_G5_CL7()
        {
            // A product two groups deep: the old scope never reached it at all, and a widening that visited
            // every ancestor locality would report each of its findings twice.
            Project project = Probe(Node("group", "_0x3100", [("name", "Etage 1")],
                Node("group", "_0x3110", [("name", "Stue")],
                    Node("product_dataline", "_0x3120",
                        [("name", "Kontakt"), ("power_group", "A"), ("cabletype", "T"),
                         ("cablenumber", "7"), ("position", "Ved dor")],
                        Node("dataline_input", "_0x3130",
                            [("name", "Tryk"), ("cable_colour", "Hvid"), ("address_dataline", "_0x03")])))));

            string[][] rows = await Appendix(project);

            Assert.Multiple(() =>
            {
                Assert.That(rows, Is.EqualTo(new[]
                {
                    new[] { "Stue", "Kontakt", string.Empty, "Mangler Id-kode" },
                    new[] { "Stue", "Kontakt", "Tryk", "Ikke forbundet" },
                }), "G5/CL-7: reached exactly once, owned by its NEAREST locality — not once per ancestor "
                    + "group, and not filed under the outer one");
                Assert.That(rows.Select(row => string.Join("|", row)).Distinct(), Has.Exactly(rows.Length).Items,
                    "no finding is reported twice");
            });
        }

        [Test]
        public async Task ProductLevelFinding_LeavesTheTerminalCellBlank_R10()
        {
            // The blank-cell convention is what makes positional cell assertions necessary at all: a
            // whitespace split would swallow this empty cell and shift the Fejl column into it.
            Project project = Probe(Node("group", "_0x4100", [("name", "Stue")],
                Node("product_dataline", "_0x4200", [("name", "Kontakt")])));

            string[][] rows = await Appendix(project);

            Assert.That(rows.Select(row => row[2]).Distinct(), Is.EqualTo(new[] { string.Empty }),
                "R10: a product-level finding names no terminal, so its Terminal cell stays blank");
            Assert.That(rows, Has.Length.EqualTo(5), "all five product-level checks fire on a bare product");
        }
    }
}
