#nullable enable
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Row-level mode membership (reportgenerality T008): the prerequisite for any ruling that ADDS content
    /// to Full mode. The shape vocabulary could tag whole shapes and strip Full-only FIELDS, but not add a
    /// row — so a Full-only row would have had to be added to Standard as well, breaking the vendor-parity
    /// contract that Standard is.
    /// <para>The load-bearing case is the subtree one. Rows are depth-encoded, and the HTML writer rebuilds
    /// its nested lists by consuming rows while their depth equals the depth it is reading — so a surviving
    /// child of a dropped parent is an orphan the forest builder stops at, silently discarding everything
    /// after it in that block. Dropping a Full-only row therefore has to drop its whole subtree.</para>
    /// </summary>
    public class ReportModeMembershipTests
    {
        private static ReportTreeRow Row(int depth, string name, ReportMembership membership = ReportMembership.Common) =>
            new NamedTreeRow(depth, name, Detail: null, IdToken: null) { Membership = membership };

        private static ReportShapeDocument Document(params ReportTreeRow[] rows) =>
            new("Titel", ImmutableArray.Create<ReportShape>(new TreeShape([.. rows])));

        private static ImmutableArray<ReportTreeRow> Rows(ReportShapeDocument document, ReportMode mode) =>
            ((TreeShape)ReportModeFilter.Select(document, mode).Shapes.Single()).Rows;

        private static string Render(ReportShapeDocument document, ReportMode mode, string mimeType)
        {
            ReportShapeDocument selected = ReportModeFilter.Select(document, mode);
            return Encoding.UTF8.GetString(mimeType == ReportMimeTypes.Html
                ? HtmlReportWriter.Write(selected, iconProvider: null)
                : TextReportWriter.Write(selected, iconProvider: null));
        }

        [Test]
        public void ARowSaysNothingUnlessItMeansTo_SoTheDefaultIsCommon()
        {
            ReportShapeDocument document = Document(Row(0, "Lokalitet"), Row(1, "Produkt"));

            Assert.That(Rows(document, ReportMode.Standard).Select(row => row.Membership),
                Is.All.EqualTo(ReportMembership.Common),
                "an untagged row is Common, so adding the field changes no existing builder's output");
        }

        [Test]
        public void AFullOnlyRow_IsDroppedInStandard_AndKeptInFull()
        {
            ReportShapeDocument document = Document(
                Row(0, "Lokalitet"),
                Row(1, "Kun i fuld", ReportMembership.FullOnly),
                Row(1, "Altid"));

            Assert.Multiple(() =>
            {
                Assert.That(Rows(document, ReportMode.Full).Select(RowName),
                    Is.EqualTo(new[] { "Lokalitet", "Kun i fuld", "Altid" }), "Full passes every row through");
                Assert.That(Rows(document, ReportMode.Standard).Select(RowName),
                    Is.EqualTo(new[] { "Lokalitet", "Altid" }), "Standard drops the Full-only row");
            });
        }

        [Test]
        public void AFullOnlyParent_DropsItsWholeSubtree_SoTheRowsAfterItSurvive()
        {
            ReportShapeDocument document = Document(
                Row(0, "Lokalitet"),
                Row(1, "Kun i fuld", ReportMembership.FullOnly),
                Row(2, "Dens terminal"),
                Row(3, "Dens note"),
                Row(1, "Altid"),
                Row(2, "Terminal"));

            Assert.Multiple(() =>
            {
                Assert.That(Rows(document, ReportMode.Standard).Select(RowName),
                    Is.EqualTo(new[] { "Lokalitet", "Altid", "Terminal" }),
                    "the Full-only row takes its whole subtree with it — and nothing beyond it");
                Assert.That(Rows(document, ReportMode.Standard).Select(row => row.Depth),
                    Is.EqualTo(new[] { 0, 1, 2 }),
                    "what survives is still contiguous: every depth step is at most +1");
            });
        }

        // The reason the subtree rule exists, stated as an observation of the rendered document rather than
        // of the row list: an orphaned child would make the forest builder stop early and drop the rest.
        [Test]
        public void AFullOnlyParent_LeavesTheRenderedStandardDocumentWellFormed()
        {
            ReportShapeDocument document = Document(
                Row(0, "Lokalitet"),
                Row(1, "Kun i fuld", ReportMembership.FullOnly),
                Row(2, "Dens terminal"),
                Row(1, "Altid"),
                Row(2, "Terminal"));

            string html = Render(document, ReportMode.Standard, ReportMimeTypes.Html);
            string text = Render(document, ReportMode.Standard, ReportMimeTypes.PlainText);

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Contain("Altid").And.Contain("Terminal"),
                    "the rows AFTER the dropped subtree still render — the forest builder was not truncated");
                Assert.That(html, Does.Not.Contain("Kun i fuld").And.Not.Contain("Dens terminal"),
                    "and the dropped subtree is gone entirely");
                Assert.That(ReportProbe.Occurrences(html, "<ul"), Is.EqualTo(ReportProbe.Occurrences(html, "</ul>")),
                    "the nested lists stay balanced");
                Assert.That(text, Does.Contain("Altid").And.Not.Contain("Kun i fuld"),
                    "the text writer agrees — mode lives in the filter, not in either writer");
            });
        }

        [Test]
        public void FunctionBlockRows_CarryMembershipToo()
        {
            var document = new ReportShapeDocument("Titel", ImmutableArray.Create<ReportShape>(
                new FbBlockShape("Blok", IdToken: null, ImmutableArray<KeyValueRow>.Empty,
                    ImmutableArray<FbParagraph>.Empty,
                    [
                        new IconTreeRow(0, "unknown", "Sektion", Value: null, Note: null, IdToken: null),
                        new IconTreeRow(1, "unknown", "Kun i fuld", "1", null, null) { Membership = ReportMembership.FullOnly },
                        new IconTreeRow(1, "unknown", "Altid", "2", null, null),
                    ], Standalone: true)));

            ImmutableArray<ReportTreeRow> standard =
                ((FbBlockShape)ReportModeFilter.Select(document, ReportMode.Standard).Shapes.Single()).Rows;

            Assert.That(standard.Cast<IconTreeRow>().Select(row => row.Name),
                Is.EqualTo(new[] { "Sektion", "Altid" }),
                "the FB report's variable rows filter by the same rule — this is what lets a variable type "
                + "be added to Full without adding it to Standard");
        }

        private static string RowName(ReportTreeRow row) => ((NamedTreeRow)row).Name;
    }
}
