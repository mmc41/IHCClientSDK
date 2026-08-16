#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// Human-readable help metadata shared by a function-block and a product definition — the definition's own overview
    /// text plus one text per resource. Mirrors the vendor help documents (a block/product
    /// "Anvendelse/Beskrivelse" section, then a description per resource under "Indgange"/"Udgange"). The single
    /// documentation record both <see cref="Ihc.Vis.FunctionBlocks.FunctionBlockDefinition"/> and
    /// <see cref="Ihc.Vis.Products.ProductDefinition"/> carry (S3: the FB and product doc records were identical).
    /// </summary>
    /// <remarks>
    /// <para>This is <b>programmatic-lookup-only</b> metadata. It rides on the in-memory definition for a GUI to
    /// surface (a library tooltip, a per-resource help panel), but it is deliberately kept out of the serialized
    /// definition body, so it is never written into a project <c>.vis</c> or a catalog <c>.def</c>/<c>.ifb</c> file.
    /// Contrast the <c>note</c> attribute (authored via the builders' <c>Note</c> methods), which <i>is</i>
    /// serialized.</para>
    /// <para><b>Do not read <see cref="Resources"/> directly.</b> Its keys are opaque position tokens minted by
    /// <c>ResourceDocKey</c> — deliberately not display names, since a name identifies no single resource (see that
    /// type). Every resource's text reaches a caller already attached to the resource, on
    /// <see cref="ResourceSummary.Documentation"/>, by iterating the definition's own resource projections. That
    /// indirection is exactly what lets two pins sharing a name carry independent texts.</para>
    /// </remarks>
    /// <param name="Summary">The definition-level documentation text (the whole help document), or <c>null</c> when none.</param>
    /// <param name="Resources">Per-resource documentation keyed by opaque position token; empty when none.</param>
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

        // The definition projections' lookup seam: they mint the same position token through ResourceDocKey while
        // enumerating the body, and hand the text out on the ResourceSummary. Not public — a caller holding a key
        // would be a second, drift-prone spelling of the format (see ResourceDocKey).
        internal string? ForKey(string key) =>
            Resources.TryGetValue(key, out string? text) ? text : null;

        // review C1: the synthesized record equality would compare Resources (an ImmutableDictionary, which has no
        // value Equals) BY REFERENCE, so two content-identical documentations compare unequal — and that propagates
        // to FunctionBlockDefinition/ProductDefinition, whose Documentation member feeds their record equality. Give
        // this record content-based value equality (order-independent over the dictionary). Summary is Ordinal
        // (help text is data, not culture).
        //
        // DELIBERATE SURVIVOR: this is MAP equality, and no EquatableDictionary<TKey,TValue> exists — the ordered
        // EquatableArray<T> and the unordered EquatableSet<T> both have the wrong semantics for a keyed lookup, so
        // this pair stays by design rather than by oversight. Adding that wrapper is the one change that would
        // retire it; until then, a member added to this record must still be added to both methods below. The map
        // comparison itself lives in OrdinalStringMap, shared with HelpDocument, which has the same problem.
        public bool Equals(DefinitionDocumentation? other) =>
            other is not null
            && string.Equals(Summary, other.Summary, StringComparison.Ordinal)
            && OrdinalStringMap.Equals(Resources, other.Resources);

        public override int GetHashCode() => OrdinalStringMap.GetHashCode(Summary, Resources);
    }
}
