using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// review G1: in the Installation report's dataline cross-reference, an explicit <c>address_dataline="_0x0"</c>
    /// renders as unaddressed (<c>?</c>) exactly like an absent address — so it must SORT with the unaddressed group
    /// (key −1), keeping document order among the ties. Before the fix its sort key was 0, ordering it AFTER an
    /// absent-address terminal even though both display <c>?</c>.
    /// </summary>
    public class InstallationReportSortTests
    {
        private static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero));

        [Test]
        public async Task InstallationReport_ExplicitZeroAddress_SortsWithUnaddressed_InDocumentOrder()
        {
            // Two input terminals that both display "?" in the Adresse column, in document order: the explicit-zero
            // "Mike" first, the absent-address "Zulu" second. An addressed output terminal makes the "Datalinie
            // udgange" heading (the slice boundary) present.
            ProjectElement product = Node("product_dataline", "_0x5153",
                new[] { ("product_identifier", "_0x2202"), ("name", "Prod") },
                Node("dataline_input", "_0x605a", new[] { ("name", "Mike"), ("address_dataline", "_0x0") }),
                Node("dataline_input", "_0x615a", new[] { ("name", "Zulu") }),
                Node("dataline_output", "_0x625b", new[] { ("name", "Oscar"), ("address_dataline", "_0x03") }));
            ProjectElement root = Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x7000") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") }, product)));

            var app = new ProjectAppService(TestSetup.Settings, new BuiltInCatalog(), Clock());
            using var output = new MemoryStream();
            await app.GenerateReport(new Project(root), ReportKind.Installation, ReportMode.Full,
                ReportMimeTypes.PlainText, output);
            string text = Encoding.UTF8.GetString(output.ToArray());

            // Isolate the input cross-reference ("Datalinie indgange") from the output one so the two names are each
            // present exactly once in the window.
            int inStart = text.IndexOf("Datalinie indgange", StringComparison.Ordinal);
            int inEnd = text.IndexOf("Datalinie udgange", inStart + 1, StringComparison.Ordinal);
            Assert.That(inStart, Is.GreaterThanOrEqualTo(0), "the input cross-reference section is present");
            Assert.That(inEnd, Is.GreaterThan(inStart), "the output cross-reference heading bounds the input window");
            string inputSection = text.Substring(inStart, inEnd - inStart);

            Assert.That(inputSection.IndexOf("Mike", StringComparison.Ordinal),
                Is.LessThan(inputSection.IndexOf("Zulu", StringComparison.Ordinal)),
                "the explicit-zero address sorts with the unaddressed group in document order, not after it");
        }
    }
}
