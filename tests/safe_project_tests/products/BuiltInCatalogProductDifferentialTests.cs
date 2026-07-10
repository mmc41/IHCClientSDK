#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Phase B fidelity gate for the generated <see cref="BuiltInCatalog"/> product catalog.
    /// <list type="bullet">
    /// <item><b>Install-gated differential</b> — every code-authored product must reduce to the same canonical
    /// component <see cref="CatalogDiscovery.FromInstallDir"/> loads from the matching vendor <c>.def</c>. The two
    /// catalogs are position-paired (both scan path-sorted/ordinal), so all ~100 products are checked — duplicates
    /// included — not just the last-wins winners. Bodies are compared canonicalized against the source file's own
    /// grammar (<see cref="DefinitionNormalizer"/>), the same reduction the builder-oracle tests use.</item>
    /// <item><b>Install-free round-trip</b> — the SDK-embedded catalog materializes and a product inserts into a fresh
    /// project, saves and re-loads structurally equal, with no IHC Visual install present at all.</item>
    /// </list>
    /// </summary>
    public class BuiltInCatalogProductDifferentialTests
    {
        [Test]
        public void EveryProduct_MatchesInstallDir()
        {
            ICatalog installed = VendorCorpus.InstalledOrIgnore(VendorCorpus.ResolveInstallThenCorpus(), "product differential");
            ICatalog built = new BuiltInCatalog();

            Assert.That(built.Products.Count, Is.EqualTo(installed.Products.Count),
                "the generated catalog registers exactly the discovered products (duplicates included)");

            for (int i = 0; i < installed.Products.Count; i++)
            {
                ProductDefinition expected = installed.Products[i];
                ProductDefinition actual = built.Products[i];
                Assert.Multiple(() =>
                {
                    Assert.That(actual.ProductIdentifier, Is.EqualTo(expected.ProductIdentifier), $"[{i}] product_identifier");
                    Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName), $"[{i}] display name");
                    Assert.That(actual.CategoryPath, Is.EqualTo(expected.CategoryPath), $"[{i}] category path");
                });
                // The structured grammar is value-comparable: the generated product must carry the exact grammar the
                // install-dir parse yields (declarations, prolog datum, DOCTYPE root — the D1 primary model).
                Assert.That(actual.Grammar, Is.EqualTo(expected.Grammar), $"[{i}] structured grammar");
                // Canonicalize both against the source file's own inline-DTD grammar and compare structurally.
                VendorCorpus.AssertStructural(
                    $"Generated product '{expected.ProductIdentifier}' differs from the install-dir .def.",
                    DefinitionNormalizer.Normalize(expected.Body, expected.Grammar),
                    DefinitionNormalizer.Normalize(actual.Body, expected.Grammar));
            }
        }

        [Test]
        public void EmittedProduct_InsertsAndRoundTrips_WithoutInstall()
        {
            var catalog = new BuiltInCatalog();   // no IhcVisualInstallDir involved
            var app = new ProjectAppService(TestSetup.Settings, catalog,
                new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
                    new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero)));
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            ProductDefinition product = catalog.Product("_0x2101");   // a stock dataline product, resolved install-free
            ProjectEditor editor = blank.Edit();
            editor.Group("Stue").AddProduct(product);
            Project built = editor.ToProject();

            using var ms = new MemoryStream();
            app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
            Project reloaded = app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();

            Assert.That(reloaded.Equals(built), Is.True, "the inserted product round-trips structurally, no install present");
        }

    }
}
