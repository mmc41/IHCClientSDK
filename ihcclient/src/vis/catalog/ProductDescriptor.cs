#nullable enable
using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// A product type auto-discovered from a <c>Products\*.def</c> catalog file under the configured
    /// IHC Visual install dir. The <see cref="Body"/> is the parsed component subtree (with
    /// placeholder ids) that the insert transform deep-copies into a project (plan §3.7).
    /// </summary>
    public sealed record ProductDescriptor(
        string ProductIdentifier,
        string DisplayName,
        string CategoryPath,
        VisElement Body);
}
