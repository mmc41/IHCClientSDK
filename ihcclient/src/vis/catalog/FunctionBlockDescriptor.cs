#nullable enable
using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// A function-block type auto-discovered from a <c>FunctionBlocks\*.ifb</c> catalog file under the
    /// configured IHC Visual install dir. The <see cref="Body"/> is the parsed <c>functionblock</c>
    /// subtree (with placeholder ids) that the insert transform deep-copies into a project (plan §3.7).
    /// </summary>
    /// <param name="MasterType">The catalog key, e.g. <c>1.1.01</c> (the <c>master_type</c> attribute).</param>
    /// <param name="MasterVersion">The variant letter, e.g. <c>e</c> (the <c>master_version</c> attribute).</param>
    /// <param name="MasterName">The bare block name, e.g. <c>Kip tænd sluk</c> (the <c>master_name</c> attribute,
    /// reproduced verbatim incl. any vendor trailing space).</param>
    /// <param name="DisplayName">The composed label shown in the IHC Visual library/tree and stored as the
    /// inserted block's <c>name</c> attribute — <c>"{MasterType}.{MasterVersion}. {MasterName}"</c>
    /// (e.g. <c>1.1.01.e. Kip tænd sluk</c>). A caller/GUI uses this directly and never hand-builds the prefix.</param>
    /// <param name="CategoryPath">The library category path the block was discovered under.</param>
    /// <param name="Body">The parsed catalog subtree deep-copied on insert.</param>
    public sealed record FunctionBlockDescriptor(
        string MasterType,
        string MasterVersion,
        string MasterName,
        string DisplayName,
        string CategoryPath,
        VisElement Body);
}
