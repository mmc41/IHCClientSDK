namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Install-free tests for <see cref="BuiltInCatalog"/> — the SDK-embedded <see cref="ICatalog"/> that materializes
    /// from code-authored builders rather than a vendor install. Both the product factories (Phase B) and the
    /// function-block factories (FB plan Phase G/H) are now generated, so the catalog materializes with the full
    /// component set <b>without touching any IHC Visual install dir</b>, backed by the shared
    /// <see cref="MaterializedCatalog"/>. The install-gated differentials are the fidelity gate; this suite proves the
    /// catalog constructs, materializes and resolves with no install present.
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
                Assert.That(catalog.FunctionBlocks, Is.Not.Empty, "generated function-block factories populate the catalog (FB plan)");
                Assert.That(catalog.EmptyFunctionBlockTemplate.IsEmptyTemplate, Is.True,
                    "the empty 'Tom blok' template placeholder is always available");
            });

            // Every emitted product resolves by its product_identifier through the last-wins index.
            var first = catalog.Products[0];
            Assert.That(catalog.Product(first.ProductIdentifier).ProductIdentifier, Is.EqualTo(first.ProductIdentifier));

            // Every emitted function block resolves by its master_type through the last-wins index.
            var block = catalog.FunctionBlocks[0];
            Assert.That(catalog.FunctionBlock(block.MasterType).MasterType, Is.EqualTo(block.MasterType));
        }
    }
}
