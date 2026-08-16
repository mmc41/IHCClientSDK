#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;
namespace Ihc.Vis.FunctionBlocks
{
    /// <summary>
    /// A function-block type definition — materialized by the SDK-embedded
    /// <see cref="Ihc.Vis.Catalog.BuiltInCatalog"/>, authored from code via
    /// <see cref="FunctionBlockDefinitionBuilder"/>, or read from a <c>FunctionBlocks\*.ifb</c> catalog file. The
    /// <see cref="Body"/> is the raw <c>functionblock</c> subtree (placeholder ids, attributes in authored/source
    /// order) that the insert transform deep-copies into a project and
    /// <see cref="Ihc.Vis.Catalog.CatalogFileWriter"/> re-emits as a catalog file.
    /// </summary>
    /// <remarks>
    /// This is the function-block-level <b>type definition</b> model, distinct from the edit-session instance
    /// handle <see cref="Ihc.Vis.Editing.FunctionBlockRef"/>, which manipulates a block already placed in a
    /// project. Every producer — a generated <see cref="Ihc.Vis.Catalog.BuiltInCatalog"/> factory, a hand-authored
    /// builder, and <see cref="Ihc.Vis.Catalog.CatalogReader"/> on a file — yields this same raw shape, so
    /// insertion and catalog write fidelity hold identically regardless of provenance, and the SDK needs no
    /// IHC Visual desktop install.
    /// </remarks>
    /// <param name="MasterType">The catalog key, e.g. <c>1.1.01</c> (the <c>master_type</c> attribute).</param>
    /// <param name="MasterVersion">The variant letter, e.g. <c>e</c> (the <c>master_version</c> attribute).</param>
    /// <param name="MasterName">The bare block name, e.g. <c>Kip tænd sluk</c> (the <c>master_name</c> attribute,
    /// reproduced verbatim incl. any vendor trailing space).</param>
    /// <param name="DisplayName">The composed label shown in the IHC Visual library/tree and stored as the
    /// inserted block's <c>name</c> attribute — <c>"{MasterType}.{MasterVersion}. {MasterName}"</c>
    /// (e.g. <c>1.1.01.e. Kip tænd sluk</c>). A caller/GUI uses this directly and never hand-builds the prefix.</param>
    /// <param name="CategoryPath">The library category path the block was discovered under.</param>
    /// <param name="Body">The parsed catalog subtree deep-copied on insert.</param>
    public sealed record FunctionBlockDefinition(
        string MasterType,
        string MasterVersion,
        string MasterName,
        string DisplayName,
        string CategoryPath,
        ProjectElement Body)
    {
        /// <summary>
        /// The block's structured catalog grammar — prolog datum, DOCTYPE root and the ordered inline-DTD
        /// declaration records (see <see cref="CatalogGrammar"/>). <see cref="Ihc.Vis.Catalog.CatalogFileWriter"/>
        /// renders the file header from it; insert-time default materialization, IDREF re-stamping and open-world
        /// hoisting read it through the schema view. <see cref="CatalogGrammar.Empty"/> when the block was authored
        /// without any grammar (the writer then rejects it — such a definition has no on-disk form — while insert
        /// still resolves against the registry).
        /// </summary>
        public CatalogGrammar Grammar { get; init; } = CatalogGrammar.Empty;

        /// <summary>The source file's on-disk text encoding, reproduced verbatim on write (see <see cref="CatalogTextEncoding"/>).</summary>
        public CatalogTextEncoding SourceEncoding { get; init; } = CatalogTextEncoding.Latin1;

        /// <summary>
        /// Ids of elements the writer must close with an explicit end tag even though they now have no children
        /// (<c>&lt;x …&gt;&lt;/x&gt;</c> rather than <c>&lt;x …/&gt;</c>). Set by the save-to-library export, whose
        /// wiring-row strip empties some pins: the vendor keeps the two-tag form for exactly those, so an element
        /// that never had children stays self-closing and one that was emptied does not (uxparity S-22). Empty for a
        /// definition read from a catalog file, where what is on disk is already the truth.
        /// </summary>
        public EquatableSet<ElementId> ExplicitCloseIds { get; init; } = [];

        /// <summary>
        /// True only for the catalog's empty "Tom blok" scaffold (<c>Data\fb.def</c>).
        /// <see cref="Ihc.Vis.Editing.GroupRef.AddEmptyFunctionBlock"/> requires it, so a full catalog block cannot be passed
        /// as a "template" and silently have its identity forged by the rename/re-date that follows.
        /// </summary>
        public bool IsEmptyTemplate { get; init; }

        /// <summary>
        /// Human-readable help metadata for this block and its pins — <b>programmatic-lookup only</b>, and deliberately
        /// <b>not</b> part of the serialized <see cref="Body"/>: it is never written into a project <c>.vis</c> or a
        /// function-block description <c>.ifb</c>. Defaults to <see cref="DefinitionDocumentation.Empty"/> (what
        /// catalog discovery yields, since an <c>.ifb</c> carries no help text). The summary is authored via
        /// <see cref="DefinitionBuilderBase{TSelf}.Documentation(string)"/>; per-resource text is authored ON the
        /// resource — <see cref="FbResourceDefBuilder.Documentation"/> inside the
        /// <c>AddInput</c>/<c>AddOutput</c>/<c>AddSetting</c>/<c>AddInternalVariable</c> configurator; see
        /// <see cref="DefinitionDocumentation"/>.
        /// </summary>
        public DefinitionDocumentation Documentation { get; init; } = DefinitionDocumentation.Empty;

        /// <summary>A decoded, read-only view of the block's <c>inputs</c> container children — for GUI preview
        /// without walking <see cref="Body"/>. Computed on access; not part of record equality.</summary>
        public IReadOnlyList<ResourceSummary> Inputs => Container("inputs");

        /// <summary>A decoded, read-only view of the block's <c>outputs</c> container children.</summary>
        public IReadOnlyList<ResourceSummary> Outputs => Container("outputs");

        /// <summary>A decoded, read-only view of the block's <c>settings</c> (public value variables) children.</summary>
        public IReadOnlyList<ResourceSummary> Settings => Container("settings");

        /// <summary>A decoded, read-only view of the block's <c>internalsettings</c> (private value variables) children.</summary>
        public IReadOnlyList<ResourceSummary> InternalVariables => Container("internalsettings");

        private IReadOnlyList<ResourceSummary> Container(string container) =>
            Body.FindChild(container) is { } holder
                ? holder.Children
                        .Select((c, index) => new ResourceSummary(c.Tag, c.GetAttribute("name") ?? string.Empty, c.Id,
                                                                  Documentation.ForKey(ResourceDocKey.ForBlock(container, index))))
                        .ToArray()
                : Array.Empty<ResourceSummary>();

        // review F2 (resolved): ExplicitCloseIds was an ImmutableHashSet, which has no value Equals, so the
        // synthesized record equality compared it BY REFERENCE and two content-equal definitions with
        // independently-built close-id sets came out unequal. That single member forced a handwritten pair
        // listing all ELEVEN members — every other one was already value-equal and gained nothing from being
        // listed, while the list itself had to be kept in sync by hand. EquatableSet<ElementId> compares by set
        // content, so the pair is gone and a member added later is covered automatically.
        // The Inputs/Outputs/Settings/InternalVariables views stay out of equality for free: they are computed,
        // so there is no backing field for the compiler to compare.

        public override string ToString() =>
            $"FunctionBlockDefinition(MasterType={MasterType}, MasterVersion={MasterVersion}, MasterName={MasterName}, DisplayName={DisplayName}, CategoryPath={CategoryPath}, Body={Body})";
    }
}
