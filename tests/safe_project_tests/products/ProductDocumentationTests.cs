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
                    .Add("Tryk (venstre)", "Opdigtet hjælpetekst: venstre tast skifter udgangens tilstand i eksemplet.")
                    .Add("Udgang", "Opdigtet hjælpetekst: eksemplets udgangssignal."));

        [Test]
        public void Empty_HasNoProductTextAndNoResourceText()
        {
            DefinitionDocumentation empty = DefinitionDocumentation.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(empty.IsEmpty, Is.True);
                Assert.That(empty.Summary, Is.Null);
                Assert.That(empty.ForResource("Tryk (venstre)"), Is.Null);
            });
        }

        [Test]
        public void ForResource_ReturnsText_ForDocumentedName_AndNull_ForUndocumented()
        {
            DefinitionDocumentation doc = Documented("Product help.");

            Assert.Multiple(() =>
            {
                Assert.That(doc.IsEmpty, Is.False);
                Assert.That(doc.ForResource("Tryk (venstre)"),
                    Is.EqualTo("Opdigtet hjælpetekst: venstre tast skifter udgangens tilstand i eksemplet."));
                Assert.That(doc.ForResource("Tryk (højre)"), Is.Null, "an undocumented resource has no text");
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
        public void Definition_CarriesDocumentation_LookedUpByProjectionName()
        {
            // The intended GUI flow: iterate the Resources projection, then look the help text up by the same display
            // name — no placeholder id tokens on the caller.
            ProductDefinition def = PushButtonDefinition() with { Documentation = Documented("Product help.") };

            Assert.Multiple(() =>
            {
                Assert.That(def.Documentation.Summary, Is.EqualTo("Product help."));
                Assert.That(def.Resources.Select(r => r.Name), Does.Contain("Tryk (venstre)"));
                Assert.That(def.Documentation.ForResource("Tryk (venstre)"),
                    Is.EqualTo("Opdigtet hjælpetekst: venstre tast skifter udgangens tilstand i eksemplet."));
                Assert.That(def.Documentation.ForResource(def.Resources.First(r => r.Tag == "dataline_output").Name),
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
                        .Add("Tryk (venstre)", "VENSTRE-HELP-SENTINEL")
                        .Add("Udgang", "UDGANG-HELP-SENTINEL")),
            };

            var attributeValuesInBody =
                new[] { documented.Body }.Concat(documented.Body.Descendants())
                    .SelectMany(e => e.AttrsOrEmpty().Select(a => a.Value));

            Assert.Multiple(() =>
            {
                Assert.That(documented.Body, Is.SameAs(bare.Body),
                    "attaching documentation copies the same Body reference the serializer consumes");
                Assert.That(attributeValuesInBody, Has.None.Contains("HELP-SENTINEL"),
                    "no help text is smuggled into any Body attribute");
            });
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
