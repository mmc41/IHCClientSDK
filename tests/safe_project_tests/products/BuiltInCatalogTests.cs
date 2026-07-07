namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Install-free tests for <see cref="BuiltInCatalog"/> — the SDK-embedded <see cref="ICatalog"/> that materializes
    /// from code-authored builders rather than a vendor install. Phase B has generated the product factories, so the
    /// catalog now materializes with the full product set <b>without touching any IHC Visual install dir</b>, backed by
    /// the shared <see cref="MaterializedCatalog"/>; the function-block set stays empty until the FB plan populates its
    /// hook. The install-gated differential (<see cref="BuiltInCatalogProductDifferentialTests"/>) is the fidelity gate;
    /// this suite proves the catalog constructs, materializes and resolves with no install present.
    /// </summary>
    public class BuiltInCatalogTests
    {
        [Test]
        public void Materializes_ProductCatalog_WithoutInstall()
        {
            var catalog = new BuiltInCatalog();   // no IhcVisualInstallDir involved

            Assert.Multiple(() =>
            {
                Assert.That(catalog.Products, Is.Not.Empty, "generated product factories populate the catalog (Phase B)");
                Assert.That(catalog.FunctionBlocks, Is.Empty, "no function-block factories generated yet (FB plan)");
                Assert.That(catalog.EmptyFunctionBlockTemplate.IsEmptyTemplate, Is.True,
                    "the empty 'Tom blok' template placeholder is always available");
            });

            // Every emitted product resolves by its product_identifier through the last-wins index.
            var first = catalog.Products[0];
            Assert.That(catalog.Product(first.ProductIdentifier).ProductIdentifier, Is.EqualTo(first.ProductIdentifier));
        }
    }
}
