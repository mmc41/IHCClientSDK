#nullable enable
using System.Collections.Immutable;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// Human-readable help metadata shared by a function-block and a product definition — the definition's own overview
    /// text plus a per-resource text keyed by resource display name. Mirrors the vendor help documents (a block/product
    /// "Anvendelse/Beskrivelse" section, then a description per resource under "Indgange"/"Udgange"). The single
    /// documentation record both <see cref="Ihc.Vis.FunctionBlocks.FunctionBlockDefinition"/> and
    /// <see cref="Ihc.Vis.Products.ProductDefinition"/> carry (S3: the FB and product doc records were identical).
    /// </summary>
    /// <remarks>
    /// This is <b>programmatic-lookup-only</b> metadata. It rides on the in-memory definition for a GUI to surface (a
    /// library tooltip, a per-resource help panel), but it is deliberately kept out of the serialized definition body,
    /// so it is never written into a project <c>.vis</c> or a catalog <c>.def</c>/<c>.ifb</c> file. Contrast the
    /// <c>note</c> attribute (authored via the builders' <c>Note</c> methods), which <i>is</i> serialized. Per-resource
    /// text is keyed by the resource's display <see cref="ResourceSummary.Name"/> — the same name a caller reads off the
    /// definition's resource projections — so a GUI iterating those projections looks the text up by name, without
    /// decoding placeholder id tokens.
    /// </remarks>
    /// <param name="Summary">The definition-level documentation text (the whole help document), or <c>null</c> when none.</param>
    /// <param name="Resources">Per-resource documentation keyed by resource display name; empty when none.</param>
    public sealed record DefinitionDocumentation(
        string? Summary,
        ImmutableDictionary<string, string> Resources)
    {
        /// <summary>The empty documentation — no definition text, no per-resource text — the default an as-yet-undocumented
        /// or catalog-discovered definition carries (a <c>.def</c>/<c>.ifb</c> holds no help text).</summary>
        public static DefinitionDocumentation Empty { get; } =
            new(null, ImmutableDictionary<string, string>.Empty);

        /// <summary>True when there is neither definition text nor any per-resource text, so a GUI can hide a help affordance.</summary>
        public bool IsEmpty => Summary is null && Resources.IsEmpty;

        /// <summary>The documentation text for the resource with display name <paramref name="resourceName"/>
        /// (an input/output/setting/variable or a product pin/family resource), or <c>null</c> when that resource carries none.</summary>
        public string? ForResource(string resourceName) =>
            Resources.TryGetValue(resourceName, out string? text) ? text : null;
    }
}
