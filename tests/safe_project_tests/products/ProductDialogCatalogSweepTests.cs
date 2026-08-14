using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CsCheck;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The batch-vs-sequence dialog law (see <see cref="ProductDialogMetamorphicTests"/> for what it says and why),
    /// swept across EVERY product the built-in catalog offers rather than one representative per family.
    /// <para>
    /// One product per family would be the cheap version of this test, and it would miss the thing worth finding.
    /// A family's preset is one shape, but a descriptor is that preset resolved against a PLACED element — its
    /// fields, their targets, their ranges and their rules all come from the individual product's own definition,
    /// so two products of one family do not compose the same dialog. The interesting products are exactly the ones
    /// nobody would pick as a representative.
    /// </para>
    /// <para>
    /// Reported, not just asserted: the sweep prints its coverage — how many products it placed, how many offered
    /// writable fields, and any it could not place — because a sweep that silently covered three products would
    /// pass just as green as one that covered a hundred.
    /// </para>
    /// </summary>
    public class ProductDialogCatalogSweepTests
    {
        /// <summary>
        /// Iterations per product. Deliberately far below the 100 a single-product property runs: the law is being
        /// checked across ~100 dialogs rather than deeply within one, and the whole suite has a runtime ceiling
        /// (D02). Raising this trades sweep breadth for depth — the breadth is the point here.
        /// </summary>
        private const int IterationsPerProduct = 10;

        [Test]
        public async Task EveryPlaceableProductsDialog_SubmitsTheSameWhetherBatchedOrOneFieldAtATime()
        {
            IReadOnlyList<ProductDefinition> catalog = ProductDialogHarness.App.GetAvailableProducts();
            var unplaceable = new List<string>();
            var withoutWritableFields = new List<string>();
            var diverged = new List<string>();
            int placed = 0;
            int fieldsExercised = 0;

            foreach (ProductDefinition definition in catalog)
            {
                PlacedProduct product = await ProductDialogHarness.PlaceAsync(definition);
                if (!product.Placed)
                {
                    unplaceable.Add($"{definition.ProductIdentifier} ({definition.DisplayName}): {product.UnplaceableReason}");
                    continue;
                }
                placed++;
                try
                {
                    int fields = ProductDialogHarness.CheckBatchEqualsSequence(product, IterationsPerProduct);
                    fieldsExercised += fields;
                    if (fields == 0)
                    {
                        withoutWritableFields.Add($"{definition.ProductIdentifier} ({definition.DisplayName})");
                    }
                }
                catch (CsCheckException ex)
                {
                    // Collected rather than thrown, so ONE divergent product does not hide the other ninety-nine.
                    // The message carries the reproducing seed.
                    diverged.Add($"{definition.ProductIdentifier} ({definition.DisplayName}): {ex.Message}");
                }
            }

            TestContext.Out.WriteLine(
                $"swept {placed}/{catalog.Count} placeable products, {fieldsExercised} writable fields total, "
                + $"{IterationsPerProduct} iterations each");
            foreach (string note in unplaceable.Concat(withoutWritableFields))
            {
                TestContext.Out.WriteLine("  note: " + note);
            }

            Assert.Multiple(() =>
            {
                Assert.That(diverged, Is.Empty,
                    "a batched submit must leave the same project as one-field-at-a-time submits:\n  "
                    + string.Join("\n  ", diverged));
                // Coverage guards: without them this test would pass by sweeping nothing at all.
                Assert.That(catalog.Count, Is.GreaterThan(90), "the built-in catalog should offer ~100 products");
                Assert.That(placed, Is.EqualTo(catalog.Count),
                    "every catalogued product should be placeable:\n  " + string.Join("\n  ", unplaceable));
                Assert.That(fieldsExercised, Is.GreaterThan(placed),
                    "the swept dialogs should offer more writable fields than there are products");
            });
        }
    }
}
