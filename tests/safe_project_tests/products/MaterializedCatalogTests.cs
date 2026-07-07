using System;
using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Install-free unit tests pinning the source-agnostic lookup semantics of <see cref="MaterializedCatalog"/> —
    /// the shared core extracted from <see cref="CatalogDiscovery"/> that <c>BuiltInCatalog</c> will also produce.
    /// Catalog keys are not globally unique (favorites duplicate function blocks; a few <c>product_identifier</c>s
    /// repeat across root element types, §9.3.3), so keyed lookups must be <b>last-wins</b> while the full
    /// <see cref="MaterializedCatalog.Products"/>/<see cref="MaterializedCatalog.FunctionBlocks"/> lists retain every
    /// definition. These behaviours must not drift when a second source (the embedded catalog) reuses this core.
    /// </summary>
    public class MaterializedCatalogTests
    {
        private static ProjectElement Body(string tag) =>
            ProjectElement.Create(tag, null, Array.Empty<(string, string)>(), Array.Empty<ProjectElement>());

        private static ProductDefinition Product(string identifier, string displayName) =>
            new ProductDefinition(identifier, displayName, string.Empty, Body("product_dataline"));

        private static FunctionBlockDefinition FunctionBlock(string masterType, string displayName) =>
            new FunctionBlockDefinition(masterType, string.Empty, displayName, displayName, string.Empty, Body("functionblock"));

        private static MaterializedCatalog Catalog(
            ImmutableArray<ProductDefinition> products,
            ImmutableArray<FunctionBlockDefinition> functionBlocks) =>
            new MaterializedCatalog(products, functionBlocks, Body("project"), Body("enum_definitions"),
                FunctionBlock(string.Empty, "Tom blok"));

        [Test]
        public void Product_DuplicateIdentifier_LastWins_ButBothStayListed()
        {
            // Two roots (auto vs. rl) sharing one product_identifier, the §9.3.3 duplicate case (e.g. product4304).
            ProductDefinition auto = Product("_0x4304", "AutoVariant");
            ProductDefinition rl = Product("_0x4304", "RlVariant");
            MaterializedCatalog catalog = Catalog(
                ImmutableArray.Create(auto, rl), ImmutableArray<FunctionBlockDefinition>.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(catalog.Product("_0x4304").DisplayName, Is.EqualTo("RlVariant"), "last definition wins");
                Assert.That(catalog.Products, Has.Count.EqualTo(2), "both duplicates retained in the full list");
            });
        }

        [Test]
        public void FunctionBlock_DuplicateMasterType_LastWins()
        {
            // Favorites duplicate a block under the same master_type; the later registration shadows the earlier.
            FunctionBlockDefinition first = FunctionBlock("1.1.01", "First");
            FunctionBlockDefinition favorite = FunctionBlock("1.1.01", "Favorite");
            MaterializedCatalog catalog = Catalog(
                ImmutableArray<ProductDefinition>.Empty, ImmutableArray.Create(first, favorite));

            Assert.That(catalog.FunctionBlock("1.1.01").DisplayName, Is.EqualTo("Favorite"));
        }

        [Test]
        public void FunctionBlock_KeylessBlock_NotTypeIndexedButListedAndFoundByName()
        {
            // A user-saved block (e.g. AutoProof) carries no master_type: it must not be addressable by the empty
            // type key, yet must remain listed and reachable via FunctionBlockByName (BL-E3 / project2).
            FunctionBlockDefinition keyless = FunctionBlock(string.Empty, "AutoProof");
            FunctionBlockDefinition keyed = FunctionBlock("1.1.01", "Kip");
            MaterializedCatalog catalog = Catalog(
                ImmutableArray<ProductDefinition>.Empty, ImmutableArray.Create(keyless, keyed));

            Assert.Multiple(() =>
            {
                Assert.That(catalog.FunctionBlockByName("AutoProof"), Is.SameAs(keyless), "name lookup finds the keyless block");
                Assert.That(catalog.FunctionBlocks, Has.Count.EqualTo(2), "keyless block still listed");
                Assert.That(catalog.FunctionBlock("1.1.01").DisplayName, Is.EqualTo("Kip"),
                    "the empty master_type of the keyless block did not shadow a real key");
            });
        }
    }
}
