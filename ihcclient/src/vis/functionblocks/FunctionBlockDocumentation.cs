#nullable enable
using System.Collections.Immutable;

using Ihc.Vis.Model;
namespace Ihc.Vis.FunctionBlocks
{
    /// <summary>
    /// Human-readable help metadata for a <see cref="FunctionBlockDefinition"/> — the block's own overview text plus a
    /// per-resource text keyed by resource display name — mirroring the vendor <c>FunctionBlocks\*.md</c> help documents
    /// (a block "Anvendelse/Beskrivelse" section, then a description per pin under "Indgange"/"Udgange").
    /// </summary>
    /// <remarks>
    /// This is <b>programmatic-lookup-only</b> metadata. It rides on the in-memory <see cref="FunctionBlockDefinition"/>
    /// for a GUI to surface (a library tooltip, a pin help panel), but it is deliberately kept out of the serialized
    /// <see cref="FunctionBlockDefinition.Body"/>, so it is never written into a project <c>.vis</c> or a function-block
    /// description <c>.ifb</c> file. Contrast the <c>note</c> attribute (authored via
    /// <see cref="FunctionBlockDefinitionBuilder.Note"/> / <see cref="FbResourceDefBuilder.Note"/>), which <i>is</i>
    /// serialized. Per-resource text is keyed by the resource's display <see cref="ResourceSummary.Name"/> — the same
    /// name a caller reads off <see cref="FunctionBlockDefinition.Inputs"/>/<see cref="FunctionBlockDefinition.Outputs"/>
    /// — so a GUI iterating those projections looks the text up by name, without decoding placeholder id tokens.
    /// </remarks>
    /// <param name="Summary">The block-level documentation text (the whole help document), or <c>null</c> when none.</param>
    /// <param name="Resources">Per-resource documentation keyed by resource display name; empty when none.</param>
    public sealed record FunctionBlockDocumentation(
        string? Summary,
        ImmutableDictionary<string, string> Resources)
    {
        /// <summary>The empty documentation — no block text, no per-resource text — the default an as-yet-undocumented
        /// or catalog-discovered <see cref="FunctionBlockDefinition"/> carries (an <c>.ifb</c> holds no help text).</summary>
        public static FunctionBlockDocumentation Empty { get; } =
            new(null, ImmutableDictionary<string, string>.Empty);

        /// <summary>True when there is neither block text nor any per-resource text, so a GUI can hide a help affordance.</summary>
        public bool IsEmpty => Summary is null && Resources.IsEmpty;

        /// <summary>The documentation text for the resource with display name <paramref name="resourceName"/>
        /// (an input/output/setting/variable), or <c>null</c> when that resource carries none.</summary>
        public string? ForResource(string resourceName) =>
            Resources.TryGetValue(resourceName, out string? text) ? text : null;
    }
}
