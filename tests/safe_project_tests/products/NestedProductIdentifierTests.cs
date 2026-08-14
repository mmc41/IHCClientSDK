#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// A <c>product_identifier</c> is not unique WITHIN one <c>.def</c> either: a product may repeat the attribute on
    /// DESCENDANT elements, carrying a different value. The vendor's <c>IHC LED Dimmer 2 kanaler</c> is the live
    /// example — the root declares the product's own identifier while its two
    /// <c>rs485_led_dimmer_channel</c> children declare a different one for the channel type.
    ///
    /// <para>A product's identity is therefore the ROOT element's attribute and nothing else. Any reader that scans a
    /// <c>.def</c> as text rather than as a tree can pick up a descendant's value — or the
    /// <c>product_identifier CDATA #REQUIRED</c> line in the inline DTD, which is a DECLARATION and not a value at
    /// all. <see cref="CatalogReader"/> is immune by construction because it asks the parsed root element, and
    /// <c>ProjectEditor</c>'s paste path gates on device-root placement rather than on attribute presence for the
    /// same reason. These tests pin that, so a future reader cannot regress to a text scan unnoticed.</para>
    ///
    /// <para>Nothing here names a product or an identifier: the hazardous products are DISCOVERED from whichever
    /// catalog is loaded, so a newly added product with nested identifiers is covered the day it lands.</para>
    /// </summary>
    public class NestedProductIdentifierTests
    {
        private const string Attr = "product_identifier";

        /// <summary>Every descendant-declared identifier of <paramref name="product"/>, root excluded.</summary>
        private static IReadOnlyList<string> NestedIdentifiersOf(ProductDefinition product) =>
            product.Body.Descendants()
                .Select(e => e.GetAttribute(Attr))
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!)
                .ToList();

        /// <summary>The products of <paramref name="catalog"/> that actually exhibit the hazard.</summary>
        private static IReadOnlyList<ProductDefinition> HazardousProductsOf(ICatalog catalog) =>
            catalog.Products
                .Where(p => NestedIdentifiersOf(p).Any(v => v != p.Body.GetAttribute(Attr)))
                .ToList();

        /// <summary>
        /// The premise. If the catalog ever stops carrying products whose descendants declare their own
        /// <c>product_identifier</c>, the rule below is moot and this file should be deleted rather than left
        /// passing vacuously.
        /// </summary>
        [Test]
        public void TheCatalogReallyDoesCarryProductsWithNestedIdentifiers()
        {
            IReadOnlyList<ProductDefinition> hazardous = HazardousProductsOf(new BuiltInCatalog());

            Assert.That(hazardous, Is.Not.Empty,
                $"no catalog product declares a differing {Attr} on a descendant; the nested-identifier rule is moot");
            TestContext.Out.WriteLine(
                "products with nested identifiers: " + string.Join(", ",
                    hazardous.Select(p => $"{p.ProductIdentifier} ({p.DisplayName}) -> "
                                          + string.Join("/", NestedIdentifiersOf(p).Distinct()))));
        }

        /// <summary>
        /// THE rule, over every product of the embedded catalog: the definition's identifier is the ROOT element's
        /// attribute — never a descendant's. Reference-independent, so it holds in CI where no vendor install is
        /// configured and the disk differential self-ignores.
        /// </summary>
        [Test]
        public void EveryProductsIdentifier_IsItsRootsAndNeverADescendants()
        {
            var wrong = new List<string>();
            foreach (ProductDefinition product in new BuiltInCatalog().Products)
            {
                string? root = product.Body.GetAttribute(Attr);
                if (product.ProductIdentifier != root)
                {
                    wrong.Add($"{product.DisplayName}: definition says '{product.ProductIdentifier}', "
                              + $"root says '{root}'");
                    continue;
                }
                // Belt and braces: for a hazardous product the identifier must not be one that ONLY a descendant
                // declares, which is what a text scan would have returned.
                IEnumerable<string> descendantOnly = NestedIdentifiersOf(product).Where(v => v != root);
                if (descendantOnly.Contains(product.ProductIdentifier))
                {
                    wrong.Add($"{product.DisplayName}: identifier '{product.ProductIdentifier}' is declared only by a "
                              + "descendant, not by the root");
                }
            }

            Assert.That(wrong, Is.Empty, string.Join(" | ", wrong));
        }

        /// <summary>
        /// The same rule on the READ path, which is where a text scan would actually be written. Every
        /// <c>.def</c> in the shipped synthetic corpus is read through <see cref="CatalogReader"/> and must yield its
        /// root's identifier; the corpus includes at least one file with a differing nested identifier, asserted here
        /// so this cannot pass because the fixtures happen to be flat.
        /// </summary>
        [Test]
        public void ReadingADefYieldsItsRootIdentifier_EvenWhenDescendantsDeclareTheirOwn()
        {
            string[] files = Directory.GetFiles(TestData.PathOf("products", "synthetic"), "*.def");
            Assert.That(files, Is.Not.Empty, "the synthetic .def corpus is missing");

            var wrong = new List<string>();
            int withNested = 0;
            foreach (string file in files)
            {
                ProductDefinition read = CatalogReader.ReadProduct(file);
                string? root = read.Body.GetAttribute(Attr);
                if (NestedIdentifiersOf(read).Any(v => v != root))
                {
                    withNested++;
                }
                if (read.ProductIdentifier != root)
                {
                    wrong.Add($"{Path.GetFileName(file)}: read '{read.ProductIdentifier}', root declares '{root}'");
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(wrong, Is.Empty, string.Join(" | ", wrong));
                Assert.That(withNested, Is.GreaterThan(0),
                    "no synthetic fixture declares a differing nested identifier, so the read-path rule was not exercised");
            });
        }

        /// <summary>
        /// Positive control for the two rules above: they must FAIL on a product whose identifier is a descendant's.
        /// Without this an always-true predicate would report a clean sweep of ~100 products.
        /// </summary>
        [Test]
        public void TheRule_FailsOnAProductWhoseIdentifierComesFromADescendant()
        {
            ProductDefinition seeded = new ProductDefinition(
                "_0xchild",                     // what a text scan might have picked up
                "Seeded violator",
                string.Empty,
                Tree.Node("product_dataline", "_0x01", new[] { (Attr, "_0xroot") },
                    Tree.Node("resource_input", "_0x02", new[] { (Attr, "_0xchild") })));

            Assert.Multiple(() =>
            {
                Assert.That(seeded.ProductIdentifier, Is.Not.EqualTo(seeded.Body.GetAttribute(Attr)),
                    "the seeded violator must not satisfy the root rule");
                Assert.That(NestedIdentifiersOf(seeded).Where(v => v != seeded.Body.GetAttribute(Attr)),
                    Does.Contain(seeded.ProductIdentifier),
                    "the descendant-only check must recognise the seeded violator");
            });
        }
    }
}
