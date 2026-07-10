#nullable enable
using System.IO;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The writer's own well-formedness guarantee: the raw body verbs accept arbitrary tag/attribute text, so
    /// malformed XML is reachable through the public API and the whitespace-normalized fidelity relation cannot see
    /// it — <see cref="CatalogFileWriter"/> therefore reparses the complete assembled document and refuses with the
    /// typed <see cref="CatalogFormatException"/> <b>before any byte reaches the destination stream</b>. Also pins
    /// the writer's structural refusals: a definition without any grammar has no on-disk form, and an explicit
    /// DOCTYPE root must equal the body root.
    /// </summary>
    public class CatalogFileWriterGateTests
    {
        private static CatalogGrammar MinimalGrammar(string rootTag) => CatalogGrammar.Create(new[]
        {
            GrammarDeclaration.Element(rootTag,
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", "")),
        });

        private static ProductDefinition Definition(ProjectElement body) =>
            new("_0x9fff", "Gate probe", string.Empty, body) { Grammar = MinimalGrammar(body.Tag) };

        private static ProjectElement Root(params ProjectElement[] children) =>
            ProjectElement.Create("product_dataline", new ElementId(0x01, 0x53),
                new[] { ("product_identifier", "_0x9fff"), ("name", "Gate probe") }, children);

        [Test]
        public void MalformedBody_ThroughRawVerbs_IsRefusedTyped_AndDestinationStaysEmpty()
        {
            // "note<" cannot appear in an attribute NAME of well-formed XML; the model and comparer are blind to it.
            ProjectElement malformedChild = ProjectElement.Create("dataline_input", new ElementId(0x02, 0x11),
                new[] { ("note<", "broken") }, System.Array.Empty<ProjectElement>());
            ProductDefinition definition = Definition(Root(malformedChild));

            using var output = new MemoryStream();
            Assert.Multiple(() =>
            {
                Assert.Throws<CatalogFormatException>(() => CatalogFileWriter.Write(definition, output),
                    "output that does not reparse must be refused with the typed exception");
                Assert.That(output.Length, Is.Zero, "the refusal provably leaves the destination untouched");
            });
        }

        [Test]
        public void MalformedTagName_IsRefusedTyped_AndDestinationStaysEmpty()
        {
            ProjectElement malformedChild = ProjectElement.Create("data line", new ElementId(0x02, 0x11),
                new[] { ("name", "x") }, System.Array.Empty<ProjectElement>());
            ProductDefinition definition = Definition(Root(malformedChild));

            using var output = new MemoryStream();
            Assert.Multiple(() =>
            {
                Assert.Throws<CatalogFormatException>(() => CatalogFileWriter.Write(definition, output));
                Assert.That(output.Length, Is.Zero);
            });
        }

        [Test]
        public void EmptyGrammarDefinition_IsRejected_AsHavingNoOnDiskForm()
        {
            var definition = new ProductDefinition("_0x9fff", "Gate probe", string.Empty, Root());

            using var output = new MemoryStream();
            Assert.Multiple(() =>
            {
                Assert.Throws<System.InvalidOperationException>(() => CatalogFileWriter.Write(definition, output),
                    "no grammar → no on-disk form (the pre-existing no-captured-header seam)");
                Assert.That(output.Length, Is.Zero);
            });
        }

        [Test]
        public void ExplicitDoctypeRoot_MustEqualBodyRoot()
        {
            CatalogGrammar grammar = CatalogGrammar.Create(
                new[] { GrammarDeclaration.Element("product_airlink", GrammarAttr.Id("id")) },
                CatalogGrammar.DefaultDeclaredEncoding,
                doctypeRoot: "product_airlink");
            ProductDefinition definition = new("_0x9fff", "Gate probe", string.Empty, Root())
            {
                Grammar = grammar,
            };

            using var output = new MemoryStream();
            Assert.Multiple(() =>
            {
                Assert.Throws<CatalogFormatException>(() => CatalogFileWriter.Write(definition, output),
                    "a DOCTYPE root disagreeing with the body root would write an inconsistent document");
                Assert.That(output.Length, Is.Zero);
            });
        }

        [Test]
        public void NonLatin1Text_UnderLatin1Encoding_IsRefusedTyped_AndDestinationStaysEmpty()
        {
            // € (U+20AC) is outside ISO-8859-1. A replacement-fallback encoder would silently write '?' — and the
            // corrupted document still reparses clean, so only the encoder itself can catch it. D4: refuse, don't
            // transcode (mirrors ProjectSerializer.Encode / ProjectFile.StrictEncoding for the .vis wire).
            ProjectElement child = ProjectElement.Create("dataline_input", new ElementId(0x02, 0x11),
                new[] { ("name", "Pris"), ("note", "Pris 10 €") }, System.Array.Empty<ProjectElement>());
            ProductDefinition definition = new("_0x9fff", "Gate probe", string.Empty, Root(child))
            {
                Grammar = MinimalGrammar("product_dataline"),
                SourceEncoding = CatalogTextEncoding.Latin1,
            };

            using var output = new MemoryStream();
            Assert.Multiple(() =>
            {
                Assert.Throws<CatalogFormatException>(() => CatalogFileWriter.Write(definition, output),
                    "text the file's own encoding cannot represent must be refused, never silently transcoded");
                Assert.That(output.Length, Is.Zero, "the refusal provably leaves the destination untouched");
            });
        }

        [Test]
        public void WellFormedDefinition_StillWrites()
        {
            ProjectElement child = ProjectElement.Create("dataline_input", new ElementId(0x02, 0x11),
                new[] { ("name", "Tryk æøå"), ("note", "5 < 6 & 7 > 2") }, System.Array.Empty<ProjectElement>());
            ProductDefinition definition = Definition(Root(child));

            using var output = new MemoryStream();
            CatalogFileWriter.Write(definition, output);

            Assert.That(output.Length, Is.GreaterThan(0));
            Assert.That(CatalogWellFormedness.Check(output.ToArray()), Is.Null, "what was written reparses clean");
        }
    }
}
