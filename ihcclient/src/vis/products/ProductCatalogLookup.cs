#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Ihc.Vis.Products
{
    /// <summary>
    /// Resolving a catalog product from what a caller actually has in hand — THE home of the D22 rule.
    /// </summary>
    public static class ProductCatalogLookup
    {
        /// <summary>
        /// The catalog product named by <paramref name="productIdentifier"/>, or null when the catalog does not
        /// carry it — or carries it TWICE and the caller cannot say which was meant.
        /// <para><b>Identifiers are not unique.</b> Eight of them name two products each (D22): <c>_0x2102</c> is
        /// both <c>LK FUGA Tryk 4 tast</c> and <c>LK OPUS Tryk 4 tast</c>. Resolving with <c>FirstOrDefault</c>
        /// placed the FUGA product when the installer picked OPUS — a different product, with its own terminals,
        /// written into the project under the wrong name and noticed only when its dialog said so (T046).</para>
        /// <para><paramref name="displayName"/> is how a caller says which: an insert-menu leaf and a placed
        /// element's own <c>name</c> both know it. Supplied and matched, it decides; supplied and matched by
        /// nothing, the identifier still decides when it is unambiguous. Omitted, the ambiguous case resolves to
        /// null rather than to a guess — refusing is the point.</para>
        /// <para>One rule, one place. It was written three times over two assemblies, in three shapes, and only
        /// the frontend's copy could disambiguate — so the SDK's own authoring door silently could not place
        /// eight of the hundred catalog products that the GUI could.</para>
        /// </summary>
        public static ProductDefinition? Resolve(
            IEnumerable<ProductDefinition> products, string? productIdentifier, string? displayName = null)
        {
            if (productIdentifier is null)
            {
                return null;
            }
            var byIdentifier = products.Where(p => p.ProductIdentifier == productIdentifier).ToList();
            return (displayName is null ? null : byIdentifier.FirstOrDefault(p => p.DisplayName == displayName))
                   ?? (byIdentifier.Count == 1 ? byIdentifier[0] : null);
        }
    }
}
