#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;
namespace Ihc.Vis.Products
{
    /// <summary>
    /// A product type definition — materialized by the SDK-embedded <see cref="Ihc.Vis.Catalog.BuiltInCatalog"/>,
    /// authored from code via <see cref="ProductDefinitionBuilder"/>, or read from a <c>Products\*.def</c> catalog
    /// file. The <see cref="Body"/> is the raw component subtree (placeholder ids, attributes in authored/source
    /// order) that the insert transform deep-copies into a project and
    /// <see cref="Ihc.Vis.Catalog.CatalogFileWriter"/> re-emits as a catalog file.
    /// </summary>
    /// <remarks>
    /// This is the product-level <b>type definition</b> model, distinct from the edit-session instance handle
    /// <see cref="Ihc.Vis.Editing.ProductRef"/>, which manipulates a product already placed in a project. Every
    /// producer — a generated <see cref="Ihc.Vis.Catalog.BuiltInCatalog"/> factory, a hand-authored builder, and
    /// <see cref="Ihc.Vis.Catalog.CatalogReader"/> on a file — yields this same raw shape, so insertion and catalog
    /// write fidelity hold identically regardless of provenance, and the SDK needs no IHC Visual desktop install.
    /// </remarks>
    /// <param name="ProductIdentifier">The opaque <c>product_identifier</c> token the product is looked up by, e.g. <c>_0x2101</c>.</param>
    /// <param name="DisplayName">The display name shown in the IHC Visual library/tree.</param>
    /// <param name="CategoryPath">The library category path the product was discovered under.</param>
    /// <param name="Body">The parsed component subtree (with placeholder ids) deep-copied into a project on insert.</param>
    public sealed record ProductDefinition(
        string ProductIdentifier,
        string DisplayName,
        string CategoryPath,
        ProjectElement Body)
    {
        /// <summary>
        /// The product's structured catalog grammar — prolog datum, DOCTYPE root and the ordered inline-DTD
        /// declaration records (see <see cref="CatalogGrammar"/>). <see cref="Ihc.Vis.Catalog.CatalogFileWriter"/>
        /// renders the file header from it; insert-time default materialization, IDREF re-stamping and open-world
        /// hoisting read it through the schema view. <see cref="CatalogGrammar.Empty"/> when the product was
        /// authored without any grammar (the writer then rejects it — such a definition has no on-disk form —
        /// while insert still resolves against the registry).
        /// </summary>
        public CatalogGrammar Grammar { get; init; } = CatalogGrammar.Empty;

        /// <summary>The source file's on-disk text encoding, reproduced verbatim on write (see <see cref="CatalogTextEncoding"/>).</summary>
        public CatalogTextEncoding SourceEncoding { get; init; } = CatalogTextEncoding.Utf8Bom;

        /// <summary>
        /// Human-readable help metadata for this product and its resources — <b>programmatic-lookup only</b>, and
        /// deliberately <b>not</b> part of the serialized <see cref="Body"/>: it is never written into a project
        /// <c>.vis</c> or a product catalog <c>.def</c>. Defaults to <see cref="ProductDocumentation.Empty"/> (what
        /// catalog discovery yields, since a <c>.def</c> carries no help text). Authored via
        /// <see cref="ProductDefinitionBuilder.Documentation(string)"/> and its by-name overload; see
        /// <see cref="ProductDocumentation"/>.
        /// </summary>
        public ProductDocumentation Documentation { get; init; } = ProductDocumentation.Empty;

        // A product body's direct children are its I/O pins and family resources plus a few STRUCTURAL blocks that are
        // not resources and must be kept out of the resource preview: the scenes container, an embedded enum_definition
        // (a "med logning" product's typedef block — see ProductDefinitionBuilder.RawChild), and any settings/config
        // container. The last covers the generic dataline "settings" AND every family-specific variant (dimmer_settings
        // on airlink dimmers, sms_modem_settings on rs485 modems, …); matching the "_settings" suffix keeps a new
        // family's settings block from leaking in as a bogus resource, where a hardcoded list would silently miss it.
        // (The function-block projections sidestep this by reading named containers, so they never meet these at the
        // body root; "internalsettings" is a function-block-only container and never a product-body child.)
        // A nested sub-product container that is itself a family resource — the rs485_led_dimmer_channel of a
        // channel-based dimmer, which nests its own increase/decrease/dimming pins — is deliberately NOT structural:
        // it is a resource in its own right and surfaces as one Resources entry; the shallow direct-children preview
        // simply does not descend into it (its inner pins and its own settings/scenes stay below the preview).
        private static bool IsStructuralChild(string tag) =>
            tag is "scenes" or "enum_definition" or "settings"
            || tag.EndsWith("_settings", StringComparison.Ordinal);

        /// <summary>
        /// A decoded, read-only view of the product's direct resource children (I/O pins and family-specific
        /// resources), excluding structural children (the <c>scenes</c> container, an embedded <c>enum_definition</c>,
        /// and any settings/config container — the generic <c>settings</c> or a family variant such as
        /// <c>dimmer_settings</c>/<c>sms_modem_settings</c>) — so a GUI can render a preview of an authored or
        /// catalog-discovered product without walking <see cref="Body"/> or decoding id tokens. Computed on each
        /// access; not part of record equality.
        /// <para>This is a shallow, direct-children view. A channel-based family (e.g. the
        /// <c>rs485_led_dimmer_channel</c> of an RS485 LED dimmer) is itself a family resource, so its channel surfaces
        /// as a single entry here; the projection deliberately does not flatten the control pins nested inside such a
        /// container. A preview that needs those inner pins locates the channel element in <see cref="Body"/> (by the
        /// surfaced entry's <see cref="ResourceSummary.Id"/>) and walks its children.</para>
        /// </summary>
        public IReadOnlyList<ResourceSummary> Resources =>
            Body.ChildrenOrEmpty()
                .Where(c => !IsStructuralChild(c.Tag))
                .Select(c => new ResourceSummary(c.Tag, c.GetAttribute("name") ?? string.Empty, c.Id))
                .ToArray();

        public override string ToString() =>
            $"ProductDefinition(ProductIdentifier={ProductIdentifier}, DisplayName={DisplayName}, CategoryPath={CategoryPath}, Body={Body})";
    }
}
