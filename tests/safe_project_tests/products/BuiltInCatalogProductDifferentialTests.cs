#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Phase B fidelity gate for the generated <see cref="BuiltInCatalog"/> product catalog.
    /// <list type="bullet">
    /// <item><b>Reference-catalog differential</b> — every code-authored product must reduce to the same canonical
    /// component <see cref="CatalogDiscovery.FromInstallDir"/> loads from the matching reference <c>.def</c>. The two
    /// catalogs are position-paired (both scan path-sorted/ordinal), so all ~100 products are checked — duplicates
    /// included — not just the last-wins winners. Bodies are compared canonicalized against the source file's own
    /// grammar (<see cref="DefinitionNormalizer"/>), the same reduction the builder-oracle tests use.</item>
    /// <item><b>Reference-independent round-trip</b> — the SDK-embedded catalog materializes and a product inserts into a fresh
    /// project, saves and re-loads structurally equal without reading the configured reference directory.</item>
    /// </list>
    /// </summary>
    public class BuiltInCatalogProductDifferentialTests
    {
        [Test]
        public void EveryProduct_MatchesReferenceCatalog()
        {
            ICatalog reference = ReferenceCatalog.OpenOrIgnore("product differential");
            ICatalog built = new BuiltInCatalog();

            Assert.That(built.Products.Count, Is.EqualTo(reference.Products.Count),
                "the generated catalog registers exactly the discovered products (duplicates included)");

            for (int i = 0; i < reference.Products.Count; i++)
            {
                ProductDefinition expected = reference.Products[i];
                ProductDefinition actual = built.Products[i];
                Assert.Multiple(() =>
                {
                    Assert.That(actual.ProductIdentifier, Is.EqualTo(expected.ProductIdentifier), $"[{i}] product_identifier");
                    Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName), $"[{i}] display name");
                    Assert.That(actual.CategoryPath, Is.EqualTo(expected.CategoryPath), $"[{i}] category path");
                });
                // The structured grammar is value-comparable: the generated product must carry the exact grammar the
                // reference-catalog parse yields (declarations, prolog datum, DOCTYPE root — the D1 primary model).
                Assert.That(actual.Grammar, Is.EqualTo(expected.Grammar), $"[{i}] structured grammar");
                // Canonicalize both against the source file's own inline-DTD grammar and compare structurally.
                ReferenceCatalog.AssertStructural(
                    $"Generated product '{expected.ProductIdentifier}' differs from the reference-catalog .def.",
                    DefinitionNormalizer.Normalize(expected.Body, expected.Grammar),
                    DefinitionNormalizer.Normalize(actual.Body, expected.Grammar));
            }
        }

        [Test]
        public void EmittedProduct_InsertsAndRoundTrips_WithoutReferenceCatalog()
        {
            var catalog = new BuiltInCatalog();   // no reference catalog directory involved
            var app = new ProjectAppService(TestSetup.Settings, catalog,
                new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
                    new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero)));
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            ProductDefinition product = catalog.Product("_0x2101");   // resolved without reading the reference catalog
            ProjectEditor editor = blank.Edit();
            editor.Group("Stue").AddProduct(product);
            Project built = editor.ToProject();

            using var ms = new MemoryStream();
            app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
            Project reloaded = app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();

            Assert.That(reloaded.Equals(built), Is.True,
                "the inserted product round-trips structurally without reading the reference catalog");
        }

        [Test]
        public void EveryProduct_CarriesBakedDanishDocumentation_WithoutReferenceCatalog()
        {
            ICatalog catalog = new BuiltInCatalog();

            // The Danish help text is baked into the generated source, so no reference catalog is read.
            ProductDefinition tryk = catalog.Product("_0x2101");
            Assert.Multiple(() =>
            {
                Assert.That(tryk.Documentation.IsEmpty, Is.False, "_0x2101 carries baked documentation");
                Assert.That(tryk.Documentation.Summary, Is.Not.Null.And.Not.Empty);
                Assert.That(tryk.Documentation.Resources, Is.Not.Empty, "per-resource help is baked in");

                // Per-resource text is keyed by the resource's display name, so a GUI looks it up by that name.
                string firstResource = tryk.Resources[0].Name;
                Assert.That(tryk.Documentation.ForResource(firstResource), Is.Not.Null.And.Not.Empty,
                    $"'{firstResource}' has per-resource help reachable by display name");
            });

            // Every product is documented — the duplicate product_identifier entries included, since the
            // catalog registers each .def separately and each carries its own help text.
            int documented = catalog.Products.Count(p => p.Documentation.Summary is { Length: > 0 });
            Assert.That(documented, Is.EqualTo(catalog.Products.Count),
                "every product carries a baked summary");
        }

    }
}
