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
    /// RF-9: a Documentation finding whose code is NOT in the appendix's declared order sorts LAST within its
    /// element, not first.
    ///
    /// <para>The appendix orders each element's findings by their rank in
    /// <c>DocumentationRules.ProductChecksInReportOrder</c> plus <c>TerminalChecksInReportOrder</c>, which is the
    /// sequence the vendor appendix witnesses. A code absent from both lists ranked <c>-1</c> — the value
    /// <see cref="System.Collections.Immutable.ImmutableArray{T}.IndexOf(T)"/> returns for "not found" — and −1
    /// sorts ahead of every witnessed row. So the rows whose order is MEASURED were displaced by the rows whose
    /// order was never declared, which is exactly backwards.</para>
    ///
    /// <para>The Documentation category is larger than the eight ranked codes: the <c>name-*</c> rows and
    /// <c>doc-project-info-blank</c> / <c>doc-no-enduser-products</c> are all Documentation and all unranked, so
    /// this is reachable with ordinary content rather than a contrived code.</para>
    /// </summary>
    public class AppendixUnrankedOrderTests
    {
        private static readonly (string, string)[] NoAttributes = [];

        private static readonly Lazy<ProjectAppService> Service =
            new(() => new ProjectAppService(TestSetup.Settings, new BuiltInCatalog(), ReportOracles.Clock()));

        private static async Task<string[][]> Appendix(Project project)
        {
            using var output = new MemoryStream();
            await Service.Value.GenerateReport(project, ReportKind.Installation, ReportMode.Full,
                ReportMimeTypes.PlainText, output);
            return ReportProbe.AppendixRows(Encoding.UTF8.GetString(output.ToArray()));
        }

        /// <summary>
        /// A product carrying BOTH an unranked finding and a ranked one. Its name is blank, which is
        /// <c>name-empty</c> — Documentation category, in neither order list — and its documentation tag is
        /// missing, which is <c>doc-documentation-tag</c>, the FIRST row of the declared product order.
        /// </summary>
        private static Project ProductWithRankedAndUnrankedFindings() =>
            new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"),
                 ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0xffff")],
                Node("groups", "_0x1000", NoAttributes,
                    Node("group", "_0x2100", [("name", "Stue")],
                        Node("product_dataline", "_0x5100",
                            [("name", ""), ("product_identifier", "_0x1f")])))));

        [Test]
        public async Task AVendorWitnessedRowPrintsBeforeAnUnrankedOneOnTheSameElement()
        {
            string[][] rows = await Appendix(ProductWithRankedAndUnrankedFindings());

            string[] messages = [.. rows.Select(r => r[^1])];
            // "Mangler Id-kode" is doc-documentation-tag, the FIRST row of the declared product order.
            // "Mangler Navn" is name-empty, which is Documentation and in neither order list.
            int ranked = Array.IndexOf(messages, "Mangler Id-kode");
            int unranked = Array.IndexOf(messages, "Mangler Navn");

            Assert.Multiple(() =>
            {
                Assert.That(ranked, Is.GreaterThanOrEqualTo(0),
                    "the fixture must raise the ranked row, or this gate is vacuous: " + string.Join(" | ", messages));
                Assert.That(unranked, Is.GreaterThanOrEqualTo(0),
                    "and the unranked one: " + string.Join(" | ", messages));
                Assert.That(ranked, Is.LessThan(unranked),
                    "the row whose position the vendor appendix witnesses comes first; the row whose position was "
                    + "never declared goes last. Before the fix an unranked code ranked -1 and led: "
                    + string.Join(" | ", messages));
            });
        }
    }
}
