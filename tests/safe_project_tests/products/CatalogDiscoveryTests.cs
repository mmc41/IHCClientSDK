using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Phase-3 gate (a), install-dir-gated: every discovered <c>.def</c>/<c>.ifb</c> parses, and the catalog
    /// surfaces the expected products/function blocks by their stable lookup keys (spec ch. 09). Skips gracefully
    /// when no IHC Visual install is configured.
    /// </summary>
    public class CatalogDiscoveryTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static ICatalog RequireCatalog()
        {
            string dir = Settings.IhcVisualInstallDir;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Assert.Ignore($"No IHC Visual install dir configured ('{dir}'); skipping install-dir-gated test.");
            }
            return CatalogDiscovery.FromInstallDir(dir);
        }

        [Test]
        public void Discovery_ParsesEveryProductAndFunctionBlock()
        {
            // FromInstallDir parses every file eagerly; a parse failure would throw here. Assert the counts too.
            ICatalog catalog = RequireCatalog();

            Assert.Multiple(() =>
            {
                Assert.That(catalog.Products.Count, Is.GreaterThanOrEqualTo(100), "≈100 product .def files");
                Assert.That(catalog.FunctionBlocks.Count, Is.GreaterThanOrEqualTo(72), "≈72 function-block .ifb files");
                Assert.That(catalog.Products.All(p => p.Body.Tag.StartsWith("product") || p.Body.Tag.Contains("device")),
                    Is.True, "every product body is a product/device root");
                Assert.That(catalog.FunctionBlocks.All(f => f.Body.Tag == "functionblock"), Is.True);
            });
        }

        [Test]
        public void Discovery_FindsTestProductsAndFunctionBlocks_ByLookupKey()
        {
            ICatalog catalog = RequireCatalog();

            ProductDefinition fuga = catalog.Product("_0x2101");
            FunctionBlockDefinition kip = catalog.FunctionBlock("1.1.01");

            Assert.Multiple(() =>
            {
                Assert.That(fuga.DisplayName, Is.EqualTo("LK FUGA Tryk 2 tast"), "NN# menu prefix stripped");
                Assert.That(fuga.Body.GetAttribute("product_identifier"), Is.EqualTo("_0x2101"), "raw body carries the present identity attributes");
                Assert.That(fuga.Body.GetAttribute("locked"), Is.Null, "catalog DTD defaults are no longer materialized on read (raw body)");
                Assert.That(kip.MasterName, Is.EqualTo("Kip tænd sluk"));
                Assert.That(kip.MasterVersion, Is.EqualTo("e"));
                Assert.That(kip.DisplayName, Does.Contain("Kip tænd sluk"));
            });
        }

        // M3 / 3.3 — user-saved library blocks (e.g. AutoProof) carry no master_type, so they are unreachable via
        // FunctionBlock(masterType) and must be found by name (BL-E3 / project2 inserts AutoProof).
        [Test]
        public void FunctionBlockByName_FindsUserSavedAutoProof_WithNoMasterType()
        {
            ICatalog catalog = RequireCatalog();

            FunctionBlockDefinition autoProof = catalog.FunctionBlockByName("AutoProof");

            Assert.Multiple(() =>
            {
                Assert.That(autoProof.DisplayName, Is.EqualTo("AutoProof"));
                Assert.That(autoProof.MasterType, Is.Empty, "user-saved block has no master_type — only a name lookup finds it");
                Assert.That(autoProof.Body.Tag, Is.EqualTo("functionblock"), "descriptor body loaded");
                Assert.That(autoProof.Body.GetAttribute("locked"), Is.EqualTo("yes"));
            });
        }
    }
}
