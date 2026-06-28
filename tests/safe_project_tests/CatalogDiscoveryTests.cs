using System.IO;
using System.Linq;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Phase-3 gate (a), install-dir-gated: every discovered <c>.def</c>/<c>.ifb</c> parses, and the catalog
    /// surfaces the expected products/function blocks by their stable lookup keys (spec ch. 09). Skips gracefully
    /// when no IHC Visual install is configured.
    /// </summary>
    public class CatalogDiscoveryTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static CatalogDiscovery RequireCatalog()
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
            CatalogDiscovery catalog = RequireCatalog();

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
            CatalogDiscovery catalog = RequireCatalog();

            ProductDescriptor fuga = catalog.Product("_0x2101");
            FunctionBlockDescriptor kip = catalog.FunctionBlock("1.1.01");

            Assert.Multiple(() =>
            {
                Assert.That(fuga.DisplayName, Is.EqualTo("LK FUGA Tryk 2 tast"), "NN# menu prefix stripped");
                Assert.That(fuga.Body.GetAttribute("locked"), Is.EqualTo("yes"), "catalog DTD default materialized");
                Assert.That(kip.MasterName, Is.EqualTo("Kip tænd sluk"));
                Assert.That(kip.MasterVersion, Is.EqualTo("e"));
                Assert.That(kip.DisplayName, Does.Contain("Kip tænd sluk"));
            });
        }
    }
}
