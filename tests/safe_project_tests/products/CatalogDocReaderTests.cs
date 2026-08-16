#nullable enable
using System.IO;

using Ihc.Vis.Catalog;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Unit tests for <see cref="CatalogDocReader"/> — the <c>syn_en*.md</c> help-document parser (F1) and its
    /// sibling-doc probe (F2), which serves <b>both</b> catalog definition kinds: a function block's <c>.ifb</c> and a
    /// product's <c>.def</c>. Parses fixed synthetic markdown samples (de-branded stand-ins for the vendor
    /// <c>syn_en*.md</c> shape) so the tests carry no copyrighted text; asserts the definition-level Summary and the
    /// per-resource text keyed by resource display name (the reader's own, name-keyed HelpDocument), plus tolerance for a missing section and the probe order.
    /// </summary>
    public class CatalogDocReaderTests
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

        // The same shape for a product: the pin names are the product's resource display names, so a GUI looks the
        // help text up by the very name it reads off ProductDefinition.Resources.
        private const string ProductSample =
            "# Tryk 2 tast\r\n" +
            "\r\n" +
            "Opdigtet produkthjælp: en trykkontakt med to taster i eksemplet.\r\n" +
            "\r\n" +
            "**Inputs**\r\n" +
            "- **Tryk (venstre)** — venstre tast i eksemplet\r\n" +
            "- **Tryk (højre)** — højre tast i eksemplet\r\n";

        [Test]
        public void Parse_Summary_IsFirstParagraphAfterHeading()
        {
            HelpDocument doc = CatalogDocReader.Parse(Sample);

            Assert.That(doc.Summary,
                Is.EqualTo("Manual on/off control for a single output, toggled by one push button."));
        }

        [Test]
        public void Parse_Resources_KeyedByDisplayName_AcrossSections()
        {
            HelpDocument doc = CatalogDocReader.Parse(Sample);

            Assert.Multiple(() =>
            {
                Assert.That(doc.ForName("Tryk"), Is.EqualTo("toggles the output on or off on each press"));
                Assert.That(doc.ForName("Tvangssluk"), Is.EqualTo("forces the output off"));
                Assert.That(doc.ForName("Lampe"), Is.EqualTo("the controlled on/off output"));
                Assert.That(doc.ForName("ON puls"), Is.EqualTo("short pulse when switching on"));
                Assert.That(doc.ByName, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public void Parse_HeadingIsNotPartOfSummary()
        {
            HelpDocument doc = CatalogDocReader.Parse(Sample);

            Assert.That(doc.Summary, Does.Not.Contain("Toggle lamp"));
        }

        [Test]
        public void Parse_MissingResourceSections_YieldsSummaryOnly()
        {
            const string summaryOnly =
                "# 2.1.01.a. Clock\r\n\r\nA plain block with prose but no pin lists.\r\n";

            HelpDocument doc = CatalogDocReader.Parse(summaryOnly);

            Assert.Multiple(() =>
            {
                Assert.That(doc.Summary, Is.EqualTo("A plain block with prose but no pin lists."));
                Assert.That(doc.ByName, Is.Empty);
            });
        }

        [Test]
        public void Parse_EmptyText_IsEmptyDocumentation()
        {
            Assert.That(CatalogDocReader.Parse("   \r\n\r\n").IsEmpty, Is.True);
        }

        [Test]
        public void Parse_AcceptsHyphenAndEnDashSeparators()
        {
            const string mixed =
                "# X\r\n\r\nSummary.\r\n\r\n**Inputs**\r\n" +
                "- **A** – en dash text\r\n" +
                "- **B** - hyphen text\r\n";

            HelpDocument doc = CatalogDocReader.Parse(mixed);

            Assert.Multiple(() =>
            {
                Assert.That(doc.ForName("A"), Is.EqualTo("en dash text"));
                Assert.That(doc.ForName("B"), Is.EqualTo("hyphen text"));
            });
        }

        [Test]
        public void ForDefinitionFile_ResolvesSynEnSiblingByPrefix_ForAFunctionBlock()
        {
            using var dir = new TempDir();
            File.WriteAllText(dir.File("1.1.01.ifb"), "<functionblock/>");
            File.WriteAllText(dir.File("syn_en1.1.01.md"), Sample);
            // A vendor .md sibling exists too, but syn_en must win (copyright).
            File.WriteAllText(dir.File("1.1.01.md"), "# vendor\r\n\r\nVendor copyrighted prose.\r\n");

            HelpDocument? doc = CatalogDocReader.ForDefinitionFile(dir.File("1.1.01.ifb"), synEnOnly: true);

            Assert.That(doc, Is.Not.Null);
            Assert.That(doc!.Summary,
                Is.EqualTo("Manual on/off control for a single output, toggled by one push button."));
        }

        // The product half of the same probe: a Products\*.def gets its help text from the same syn_en{base}.md
        // sibling convention, so the catalog generator can bake documentation into a product exactly as it does for a
        // function block. Nothing about the probe is .ifb-specific — this pins that.
        [Test]
        public void ForDefinitionFile_ResolvesSynEnSiblingByPrefix_ForAProduct()
        {
            using var dir = new TempDir();
            File.WriteAllText(dir.File("product2101.def"), "<product_dataline/>");
            File.WriteAllText(dir.File("syn_enproduct2101.md"), ProductSample);

            HelpDocument? doc = CatalogDocReader.ForDefinitionFile(dir.File("product2101.def"), synEnOnly: true);

            Assert.That(doc, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(doc!.Summary,
                    Is.EqualTo("Opdigtet produkthjælp: en trykkontakt med to taster i eksemplet."));
                Assert.That(doc.ForName("Tryk (venstre)"), Is.EqualTo("venstre tast i eksemplet"));
                Assert.That(doc.ForName("Tryk (højre)"), Is.EqualTo("højre tast i eksemplet"));
            });
        }

        [Test]
        public void ForDefinitionFile_NoSibling_ReturnsNull()
        {
            using var dir = new TempDir();
            File.WriteAllText(dir.File("9.9.99.ifb"), "<functionblock/>");
            File.WriteAllText(dir.File("product9999.def"), "<product_dataline/>");

            Assert.Multiple(() =>
            {
                Assert.That(CatalogDocReader.ForDefinitionFile(dir.File("9.9.99.ifb")), Is.Null);
                Assert.That(CatalogDocReader.ForDefinitionFile(dir.File("product9999.def")), Is.Null);
            });
        }

        // A throwaway directory for the probe cases, removed even when an assertion fails.
        private sealed class TempDir : System.IDisposable
        {
            private readonly string path =
                Path.Combine(Path.GetTempPath(), "catalogdoc_" + System.Guid.NewGuid().ToString("N"));

            public TempDir() => Directory.CreateDirectory(path);

            public string File(string name) => Path.Combine(path, name);

            public void Dispose() => Directory.Delete(path, recursive: true);
        }
    }
}
