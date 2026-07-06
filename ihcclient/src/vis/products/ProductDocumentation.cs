#nullable enable
using System.Collections.Immutable;

using Ihc.Vis.Model;
namespace Ihc.Vis.Products
{
    /// <summary>
    /// Human-readable help metadata for a <see cref="ProductDefinition"/> — the product's own overview text plus a
    /// per-resource text keyed by resource display name (help for each I/O pin or family resource) — the product-level
    /// peer of <see cref="Ihc.Vis.FunctionBlocks.FunctionBlockDocumentation"/>.
    /// </summary>
    /// <remarks>
    /// This is <b>programmatic-lookup-only</b> metadata. It rides on the in-memory <see cref="ProductDefinition"/> for a
    /// GUI to surface (a library tooltip, a per-pin help panel), but it is deliberately kept out of the serialized
    /// <see cref="ProductDefinition.Body"/>, so it is never written into a project <c>.vis</c> or a product catalog
    /// <c>.def</c> file. Contrast the <c>note</c> attribute (authored via <see cref="ProductDefinitionBuilder.Note"/> /
    /// <see cref="ProductResourceDefBuilder.Note"/>), which <i>is</i> serialized. Per-resource text is keyed by the
    /// resource's display <see cref="ResourceSummary.Name"/> — the same name a caller reads off
    /// <see cref="ProductDefinition.Resources"/> — so a GUI iterating that projection looks the text up by name, without
    /// decoding placeholder id tokens.
    /// </remarks>
    /// <param name="Summary">The product-level documentation text (the whole help document), or <c>null</c> when none.</param>
    /// <param name="Resources">Per-resource documentation keyed by resource display name; empty when none.</param>
    public sealed record ProductDocumentation(
        string? Summary,
        ImmutableDictionary<string, string> Resources)
    {
        /// <summary>The empty documentation — no product text, no per-resource text — the default an as-yet-undocumented
        /// or catalog-discovered <see cref="ProductDefinition"/> carries (a <c>.def</c> holds no help text).</summary>
        public static ProductDocumentation Empty { get; } =
            new(null, ImmutableDictionary<string, string>.Empty);

        /// <summary>True when there is neither product text nor any per-resource text, so a GUI can hide a help affordance.</summary>
        public bool IsEmpty => Summary is null && Resources.IsEmpty;

        /// <summary>The documentation text for the resource with display name <paramref name="resourceName"/>
        /// (an I/O pin or family resource), or <c>null</c> when that resource carries none.</summary>
        public string? ForResource(string resourceName) =>
            Resources.TryGetValue(resourceName, out string? text) ? text : null;
    }
}
