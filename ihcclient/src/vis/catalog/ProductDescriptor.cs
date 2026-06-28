#nullable enable
using System.Collections.Immutable;

namespace Ihc.Projects
{
    /// <summary>
    /// A product type auto-discovered from a <c>Products\*.def</c> catalog file under the configured
    /// IHC Visual install dir. The <see cref="Body"/> is the parsed component subtree (with
    /// placeholder ids) that the insert transform deep-copies into a project.
    /// </summary>
    /// <param name="ProductIdentifier">The opaque <c>product_identifier</c> token the product is looked up by, e.g. <c>_0x2101</c>.</param>
    /// <param name="DisplayName">The display name shown in the IHC Visual library/tree.</param>
    /// <param name="CategoryPath">The library category path the product was discovered under.</param>
    /// <param name="Body">The parsed component subtree (with placeholder ids) deep-copied into a project on insert.</param>
    public sealed record ProductDescriptor(
        string ProductIdentifier,
        string DisplayName,
        string CategoryPath,
        ProjectElement Body)
    {
        /// <summary>
        /// The component's own inline-DTD blocks (tag → verbatim block), captured from its <c>.def</c> file, so an
        /// element type the static registry does not declare can still be inserted and saved (open-world): on insert
        /// the non-registry blocks are merged into the project's <see cref="Project.InlineDtdBlocks"/>. Empty when
        /// the descriptor was hand-built without a source file.
        /// </summary>
        public ImmutableDictionary<string, string> InlineDtdBlocks { get; init; } = ImmutableDictionary<string, string>.Empty;

        public override string ToString() =>
            $"ProductDescriptor(ProductIdentifier={ProductIdentifier}, DisplayName={DisplayName}, CategoryPath={CategoryPath}, Body={Body})";
    }
}
