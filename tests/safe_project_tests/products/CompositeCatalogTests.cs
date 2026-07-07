using System;
using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Deterministic (no install dir) tests for <see cref="CompositeCatalog"/>: it must delegate unimported lookups to
    /// its base, let a runtime import shadow a base component with the same key (imported-wins) while keeping both in
    /// the listing (append-only), and pick up an import even after a prior read cached the composed snapshot.
    /// </summary>
    public class CompositeCatalogTests
    {
        private static ProjectElement Empty(string tag) =>
            ProjectElement.Create(tag, null, Array.Empty<(string, string)>(), Array.Empty<ProjectElement>());

        private static ProductDefinition Product(string identifier, string displayName) =>
            new(identifier, displayName, string.Empty, Empty("product_dataline"));

        private static FunctionBlockDefinition Block(string masterType, string displayName) =>
            new(masterType, "e", "Master", displayName, string.Empty, Empty("functionblock"));

        // A tiny base with one product and one function block plus the three templates.
        private static MaterializedCatalog Base() =>
            new(ImmutableArray.Create(Product("_0x2101", "BaseSocket")),
                ImmutableArray.Create(Block("1.1.01", "1.1.01.e. BaseBlock")),
                Empty("project"),
                Empty("enum_definitions"),
                new FunctionBlockDefinition(string.Empty, string.Empty, "Tom blok", "Tom blok", string.Empty, Empty("functionblock")) { IsEmptyTemplate = true });

        [Test]
        public void Lookups_And_Templates_DelegateToBase()
        {
            var composite = new CompositeCatalog(Base());

            Assert.Multiple(() =>
            {
                Assert.That(composite.Product("_0x2101").DisplayName, Is.EqualTo("BaseSocket"));
                Assert.That(composite.FunctionBlock("1.1.01").DisplayName, Is.EqualTo("1.1.01.e. BaseBlock"));
                Assert.That(composite.NewProjectSkeleton.Tag, Is.EqualTo("project"));
                Assert.That(composite.BuiltInEnumerators.Tag, Is.EqualTo("enum_definitions"));
                Assert.That(composite.EmptyFunctionBlockTemplate.IsEmptyTemplate, Is.True);
            });
        }

        [Test]
        public void Import_ProductWithSameKey_ImportedWins_BothListed()
        {
            var composite = new CompositeCatalog(Base());

            composite.Import(Product("_0x2101", "ImportedSocket"));

            Assert.Multiple(() =>
            {
                Assert.That(composite.Product("_0x2101").DisplayName, Is.EqualTo("ImportedSocket"), "imported shadows base by key");
                Assert.That(composite.Products, Has.Count.EqualTo(2), "append-only: base + import both retained");
            });
        }

        [Test]
        public void Import_NewProduct_IsResolvableAndListed()
        {
            var composite = new CompositeCatalog(Base());

            composite.Import(Product("_0x9999", "Novel"));

            Assert.Multiple(() =>
            {
                Assert.That(composite.Product("_0x9999").DisplayName, Is.EqualTo("Novel"));
                Assert.That(composite.Products, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public void Import_FunctionBlock_ResolvableByTypeAndByName()
        {
            var composite = new CompositeCatalog(Base());

            composite.Import(Block("2.2.02", "2.2.02.e. Imported"));

            Assert.Multiple(() =>
            {
                Assert.That(composite.FunctionBlock("2.2.02").DisplayName, Is.EqualTo("2.2.02.e. Imported"));
                Assert.That(composite.FunctionBlockByName("2.2.02.e. Imported").MasterType, Is.EqualTo("2.2.02"));
                Assert.That(composite.FunctionBlocks, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public void Import_AfterFirstRead_InvalidatesCachedSnapshot()
        {
            var composite = new CompositeCatalog(Base());
            _ = composite.Products;   // materialize + cache the composed snapshot

            composite.Import(Product("_0x2101", "ImportedSocket"));

            Assert.That(composite.Product("_0x2101").DisplayName, Is.EqualTo("ImportedSocket"),
                "an import after a cached read must invalidate the snapshot");
        }
    }
}
