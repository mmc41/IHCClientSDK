using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Exercises the programmatic-lookup-only <see cref="DefinitionDocumentation"/> help metadata carried on a
    /// <see cref="ProductDefinition"/>: the product's overview text plus a per-resource text keyed by display name
    /// (help for each I/O pin — a shape modeled on, but not copied from, any vendor source; every sample string here is
    /// synthetic and self-authored), and — the load-bearing guarantee — that attaching it leaves the serialized
    /// <see cref="ProductDefinition.Body"/> untouched, so it never reaches a project <c>.vis</c> or a product catalog
    /// <c>.def</c>. These run against a hand-built definition body (no builder, no catalog, no install dir), so they are
    /// real runnable tests independent of the Stage-1 builder stubs. The product-level peer of
    /// <see cref="FunctionBlockDocumentationTests"/>.
    /// </summary>
    public class ProductDocumentationTests
    {
        private static ProjectElement El(string tag, string name, params ProjectElement[] children) =>
            new ProjectElement(
                tag,
                null,
                ImmutableArray.Create<(string, string)>(("name", name)),
                children.Length == 0 ? ImmutableArray<ProjectElement>.Empty : ImmutableArray.Create(children));

        // The "Tryk 2 tast" push-button shape from DefinitionProjectionTests — two inputs, one output, a scenes
        // container — so documentation lookup can be tied to the same names a GUI reads off Resources.
        private static ProjectElement PushButtonBody() =>
            El("product_dataline", "Tryk 2 tast",
                El("dataline_input", "Tryk (venstre)"),
                El("dataline_input", "Tryk (højre)"),
                El("dataline_output", "Udgang"),
                El("scenes", "Scenarier"));

        private static ProductDefinition PushButtonDefinition(ProjectElement? body = null) =>
            new ProductDefinition("_0x2101", "Tryk 2 tast", "01. Tryk/2 taster", body ?? PushButtonBody());

        // Synthetic, self-authored help text (NOT copied from any copyrighted vendor source) — only its shape matches one.
        private static DefinitionDocumentation Documented(string summary) =>
            new DefinitionDocumentation(
                summary,
                ImmutableDictionary<string, string>.Empty
                    .Add(ResourceDocKey.ForProduct(0), "Opdigtet hjælpetekst: venstre tast skifter udgangens tilstand i eksemplet.")
                    .Add(ResourceDocKey.ForProduct(2), "Opdigtet hjælpetekst: eksemplets udgangssignal."));

        [Test]
        public void Empty_HasNoProductTextAndNoResourceText()
        {
            DefinitionDocumentation empty = DefinitionDocumentation.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(empty.IsEmpty, Is.True);
                Assert.That(empty.Summary, Is.Null);
                Assert.That(empty.Resources, Is.Empty);
            });
        }

        [Test]
        public void EachResourceGetsItsOwnText_AndAnUndocumentedOneGetsNone()
        {
            ProductDefinition def = PushButtonDefinition() with { Documentation = Documented("Product help.") };

            Assert.Multiple(() =>
            {
                Assert.That(def.Documentation.IsEmpty, Is.False);
                Assert.That(def.Resources[0].Documentation,
                    Is.EqualTo("Opdigtet hjælpetekst: venstre tast skifter udgangens tilstand i eksemplet."));
                Assert.That(def.Resources[1].Documentation, Is.Null, "an undocumented resource has no text");
            });
        }

        [Test]
        public void Definition_DefaultsToEmptyDocumentation()
        {
            // Catalog discovery constructs the 4-arg definition and never sets documentation (a .def has no help text),
            // so the default must be the empty sentinel, not null.
            ProductDefinition def = PushButtonDefinition();

            Assert.Multiple(() =>
            {
                Assert.That(def.Documentation, Is.SameAs(DefinitionDocumentation.Empty));
                Assert.That(def.Documentation.IsEmpty, Is.True);
            });
        }

        [Test]
        public void Definition_CarriesDocumentation_ReadOffTheProjection()
        {
            // The intended GUI flow: iterate the Resources projection and render each resource's own help text — the
            // caller handles neither a name key nor a placeholder id token. The structural <scenes> child sits between
            // the documented output and the end of the body, so this also pins that filtering it out shifts nothing.
            ProductDefinition def = PushButtonDefinition() with { Documentation = Documented("Product help.") };

            Assert.Multiple(() =>
            {
                Assert.That(def.Documentation.Summary, Is.EqualTo("Product help."));
                Assert.That(def.Resources.Select(r => r.Name), Does.Contain("Tryk (venstre)"));
                Assert.That(def.Resources.Single(r => r.Name == "Tryk (venstre)").Documentation,
                    Is.EqualTo("Opdigtet hjælpetekst: venstre tast skifter udgangens tilstand i eksemplet."));
                Assert.That(def.Resources.Single(r => r.Tag == "dataline_output").Documentation,
                    Is.EqualTo("Opdigtet hjælpetekst: eksemplets udgangssignal."));
            });
        }

        [Test]
        public void AttachingDocumentation_LeavesBodyUntouched_SoItIsNeverSerialized()
        {
            // The load-bearing guarantee. GroupRef.AddProduct (and any serializer) consume ONLY Body + InlineDtdBlocks,
            // so proving the help text lives entirely outside Body proves it can never be written into a .vis or .def.
            // Distinctive sentinels make an accidental leak into any attribute (e.g. a 'note') visible.
            ProductDefinition bare = PushButtonDefinition();
            ProductDefinition documented = bare with
            {
                Documentation = new DefinitionDocumentation(
                    "PRODUCT-HELP-SENTINEL",
                    ImmutableDictionary<string, string>.Empty
                        .Add(ResourceDocKey.ForProduct(0), "VENSTRE-HELP-SENTINEL")
                        .Add(ResourceDocKey.ForProduct(2), "UDGANG-HELP-SENTINEL")),
            };

            var attributeValuesInBody =
                new[] { documented.Body }.Concat(documented.Body.Descendants())
                    .SelectMany(e => e.Attrs.Select(a => a.Value));

            Assert.Multiple(() =>
            {
                Assert.That(documented.Body, Is.SameAs(bare.Body),
                    "attaching documentation copies the same Body reference the serializer consumes");
                Assert.That(attributeValuesInBody, Has.None.Contains("HELP-SENTINEL"),
                    "no help text is smuggled into any Body attribute");
            });
        }

        // The same guarantee one layer out, on the real serializer rather than on Body alone: what the catalog
        // generator bakes into a product factory must not be able to change a single byte of the .def it re-emits.
        // Reads a committed synthetic oracle (so the definition carries a real grammar + source encoding the writer
        // needs), writes it documented and undocumented, and compares the bytes.
        [Test]
        public void WritingADocumentedProduct_ProducesTheSameDefBytes_AsWritingItUndocumented()
        {
            byte[] file = System.IO.File.ReadAllBytes(
                TestData.PathOf("products", "synthetic", "synthetic_9f01_input.def"));
            using var reading = new System.IO.MemoryStream(file, writable: false);
            ProductDefinition bare = CatalogReader.ReadProduct(reading);
            ProductDefinition documented = bare with
            {
                Documentation = new DefinitionDocumentation(
                    "PRODUCT-HELP-SENTINEL",
                    ImmutableDictionary<string, string>.Empty.Add("Tryk", "PIN-HELP-SENTINEL")),
            };

            Assert.That(WriteDef(documented), Is.EqualTo(WriteDef(bare)),
                "help metadata is programmatic-lookup only — it never reaches the serialized .def");
        }

        private static byte[] WriteDef(ProductDefinition definition)
        {
            using var buffer = new System.IO.MemoryStream();
            CatalogFileWriter.Write(definition, buffer);
            return buffer.ToArray();
        }

        [Test]
        public void Documentation_ParticipatesInDefinitionEquality()
        {
            // Same Body instance, so the only thing that can differ is the documentation — pinning that it is part of a
            // definition's identity (a caller keying definitions by value sees a documented and an undocumented product
            // as distinct), while an undocumented clone stays equal.
            ProjectElement body = PushButtonBody();
            ProductDefinition bare = PushButtonDefinition(body);
            ProductDefinition documented = bare with { Documentation = Documented("Product help.") };

            Assert.Multiple(() =>
            {
                Assert.That(bare with { }, Is.EqualTo(bare), "an undocumented clone stays equal");
                Assert.That(documented, Is.Not.EqualTo(bare), "documentation participates in value equality");
            });
        }
    }
}
