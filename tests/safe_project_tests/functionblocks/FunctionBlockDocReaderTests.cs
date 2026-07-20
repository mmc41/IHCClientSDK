#nullable enable
using System.IO;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Unit tests for <see cref="FunctionBlockDocReader"/> — the <c>syn_en*.md</c> help-document parser (F1) and its
    /// sibling-doc probe (F2). Parses a fixed synthetic markdown sample (a de-branded stand-in for the vendor
    /// <c>syn_en1.1.01.md</c> shape) so the tests carry no copyrighted text; asserts the block-level Summary and the
    /// per-resource text keyed by resource display name, plus tolerance for a missing section and the probe order.
    /// </summary>
    public class FunctionBlockDocReaderTests
    {
        // A synthetic help document in the exact syn_en shape: H1 display name, a summary paragraph, then
        // **Inputs**/**Outputs** sections whose bullets are "- **name** — text" (em-dash separated).
        private const string Sample =
            "# 9.1.01.a. Toggle lamp\r\n" +
            "\r\n" +
            "Manual on/off control for a single output, toggled by one push button.\r\n" +
            "\r\n" +
            "**Inputs**\r\n" +
            "- **Tryk** — toggles the output on or off on each press\r\n" +
            "- **Tvangssluk** — forces the output off\r\n" +
            "\r\n" +
            "**Outputs**\r\n" +
            "- **Lampe** — the controlled on/off output\r\n" +
            "- **ON puls** — short pulse when switching on\r\n";

        [Test]
        public void Parse_Summary_IsFirstParagraphAfterHeading()
        {
            DefinitionDocumentation doc = FunctionBlockDocReader.Parse(Sample);

            Assert.That(doc.Summary,
                Is.EqualTo("Manual on/off control for a single output, toggled by one push button."));
        }

        [Test]
        public void Parse_Resources_KeyedByDisplayName_AcrossSections()
        {
            DefinitionDocumentation doc = FunctionBlockDocReader.Parse(Sample);

            Assert.Multiple(() =>
            {
                Assert.That(doc.ForResource("Tryk"), Is.EqualTo("toggles the output on or off on each press"));
                Assert.That(doc.ForResource("Tvangssluk"), Is.EqualTo("forces the output off"));
                Assert.That(doc.ForResource("Lampe"), Is.EqualTo("the controlled on/off output"));
                Assert.That(doc.ForResource("ON puls"), Is.EqualTo("short pulse when switching on"));
                Assert.That(doc.Resources, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public void Parse_HeadingIsNotPartOfSummary()
        {
            DefinitionDocumentation doc = FunctionBlockDocReader.Parse(Sample);

            Assert.That(doc.Summary, Does.Not.Contain("Toggle lamp"));
        }

        [Test]
        public void Parse_MissingResourceSections_YieldsSummaryOnly()
        {
            const string summaryOnly =
                "# 2.1.01.a. Clock\r\n\r\nA plain block with prose but no pin lists.\r\n";

            DefinitionDocumentation doc = FunctionBlockDocReader.Parse(summaryOnly);

            Assert.Multiple(() =>
            {
                Assert.That(doc.Summary, Is.EqualTo("A plain block with prose but no pin lists."));
                Assert.That(doc.Resources, Is.Empty);
            });
        }

        [Test]
        public void Parse_EmptyText_IsEmptyDocumentation()
        {
            Assert.That(FunctionBlockDocReader.Parse("   \r\n\r\n").IsEmpty, Is.True);
        }

        [Test]
        public void Parse_AcceptsHyphenAndEnDashSeparators()
        {
            const string mixed =
                "# X\r\n\r\nSummary.\r\n\r\n**Inputs**\r\n" +
                "- **A** – en dash text\r\n" +
                "- **B** - hyphen text\r\n";

            DefinitionDocumentation doc = FunctionBlockDocReader.Parse(mixed);

            Assert.Multiple(() =>
            {
                Assert.That(doc.ForResource("A"), Is.EqualTo("en dash text"));
                Assert.That(doc.ForResource("B"), Is.EqualTo("hyphen text"));
            });
        }

        [Test]
        public void ForFunctionBlock_ResolvesSynEnSiblingByPrefix()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fbdoc_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "1.1.01.ifb"), "<functionblock/>");
                File.WriteAllText(Path.Combine(dir, "syn_en1.1.01.md"), Sample);
                // A vendor .md sibling exists too, but syn_en must win (copyright).
                File.WriteAllText(Path.Combine(dir, "1.1.01.md"), "# vendor\r\n\r\nVendor copyrighted prose.\r\n");

                DefinitionDocumentation? doc = FunctionBlockDocReader.ForFunctionBlock(
                    Path.Combine(dir, "1.1.01.ifb"), synEnOnly: true);

                Assert.That(doc, Is.Not.Null);
                Assert.That(doc!.Summary,
                    Is.EqualTo("Manual on/off control for a single output, toggled by one push button."));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void ForFunctionBlock_NoSibling_ReturnsNull()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fbdoc_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "9.9.99.ifb"), "<functionblock/>");

                Assert.That(FunctionBlockDocReader.ForFunctionBlock(Path.Combine(dir, "9.9.99.ifb")), Is.Null);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
