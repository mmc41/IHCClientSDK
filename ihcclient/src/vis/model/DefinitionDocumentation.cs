#nullable enable
using System;
using System.Collections.Generic;
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

        // review C1: the synthesized record equality would compare Resources (an ImmutableDictionary, which has no
        // value Equals) BY REFERENCE, so two content-identical documentations compare unequal — and that propagates
        // to FunctionBlockDefinition/ProductDefinition, whose Documentation member feeds their record equality. Give
        // this record content-based value equality (order-independent over the dictionary). Summary is Ordinal
        // (help text is data, not culture).
        //
        // DELIBERATE SURVIVOR: this is MAP equality, and no EquatableDictionary<TKey,TValue> exists — the ordered
        // EquatableArray<T> and the unordered EquatableSet<T> both have the wrong semantics for a keyed lookup, so
        // this pair stays by design rather than by oversight. Adding that wrapper is the one change that would
        // retire it; until then, a member added to this record must still be added to both methods below.
        public bool Equals(DefinitionDocumentation? other) =>
            other is not null
            && string.Equals(Summary, other.Summary, StringComparison.Ordinal)
            && ResourcesEqual(Resources, other.Resources);

        public override int GetHashCode()
        {
            int hash = Summary is null ? 0 : StringComparer.Ordinal.GetHashCode(Summary);
            foreach (KeyValuePair<string, string> entry in Resources)
            {
                // XOR combine so the per-entry contribution is order-independent (a dictionary has no stable order).
                hash ^= (StringComparer.Ordinal.GetHashCode(entry.Key) * 397) ^ StringComparer.Ordinal.GetHashCode(entry.Value);
            }
            return hash;
        }

        private static bool ResourcesEqual(ImmutableDictionary<string, string> a, ImmutableDictionary<string, string> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a.Count != b.Count)
            {
                return false;
            }
            foreach (KeyValuePair<string, string> entry in a)
            {
                if (!b.TryGetValue(entry.Key, out string? value) || !string.Equals(value, entry.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
