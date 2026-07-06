#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
namespace Ihc.Vis.FunctionBlocks
{
    /// <summary>
    /// A function-block type auto-discovered from a <c>FunctionBlocks\*.ifb</c> catalog file under the
    /// configured IHC Visual install dir. The <see cref="Body"/> is the parsed <c>functionblock</c>
    /// subtree (with placeholder ids) that the insert transform deep-copies into a project.
    /// </summary>
    /// <remarks>
    /// This is the function-block-level <b>type definition</b> model. Today it is produced by catalog discovery
    /// from an <c>.ifb</c> file; <see cref="FunctionBlockDefinitionBuilder"/> in this <c>Ihc.Vis.FunctionBlocks</c>
    /// namespace — the function-block-level peer of <see cref="Ihc.Vis.Projects.NewProjectBuilder"/> — will
    /// additionally author one from code (its surface is defined; the implementation lands in a later session), so
    /// the SDK need not depend on the IHC Visual desktop application for function-block definitions. Distinct from
    /// the edit-session instance handle
    /// <see cref="Ihc.Vis.Editing.FunctionBlockRef"/>, which manipulates a block already placed in a project.
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
        /// The block's own inline-DTD blocks (tag → verbatim block), captured from its <c>.ifb</c> file, so an
        /// element type the static registry does not declare can still be inserted and saved (open-world): on insert
        /// the non-registry blocks are merged into the project's <see cref="Ihc.Vis.Projects.Project.InlineDtdBlocks"/>. Empty when
        /// the descriptor was hand-built without a source file.
        /// </summary>
        public ImmutableDictionary<string, string> InlineDtdBlocks { get; init; } = ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// True only for the catalog's empty "Tom blok" scaffold (<c>Data\fb.def</c>).
        /// <see cref="Ihc.Vis.Editing.GroupRef.AddEmptyFunctionBlock"/> requires it, so a full catalog block cannot be passed
        /// as a "template" and silently have its identity forged by the rename/re-date that follows.
        /// </summary>
        public bool IsEmptyTemplate { get; init; }

        /// <summary>
        /// Human-readable help metadata for this block and its pins — <b>programmatic-lookup only</b>, and deliberately
        /// <b>not</b> part of the serialized <see cref="Body"/>: it is never written into a project <c>.vis</c> or a
        /// function-block description <c>.ifb</c>. Defaults to <see cref="FunctionBlockDocumentation.Empty"/> (what
        /// catalog discovery yields, since an <c>.ifb</c> carries no help text). Authored via
        /// <see cref="FunctionBlockDefinitionBuilder.Documentation(string)"/> and its by-handle overload; see
        /// <see cref="FunctionBlockDocumentation"/>.
        /// </summary>
        public FunctionBlockDocumentation Documentation { get; init; } = FunctionBlockDocumentation.Empty;

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
                ? holder.ChildrenOrEmpty()
                        .Select(c => new ResourceSummary(c.Tag, c.GetAttribute("name") ?? string.Empty, c.Id))
                        .ToArray()
                : Array.Empty<ResourceSummary>();

        public override string ToString() =>
            $"FunctionBlockDefinition(MasterType={MasterType}, MasterVersion={MasterVersion}, MasterName={MasterName}, DisplayName={DisplayName}, CategoryPath={CategoryPath}, Body={Body})";
    }
}
