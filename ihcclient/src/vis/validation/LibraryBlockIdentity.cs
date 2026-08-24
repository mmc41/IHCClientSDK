#nullable enable
using System;
using System.Globalization;

using Ihc.Vis.Model;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// What a function block says about the library entry it came from, read off the block itself. Shared by the
    /// rows that ask two different questions about the same three attributes: whether a block is still at its
    /// insert name (<c>name-default</c>) and whether it has been changed away from it
    /// (<c>logic-master-block-modified</c>). One definition, so the two can never disagree.
    /// </summary>
    internal static class LibraryBlockIdentity
    {
        /// <summary>Whether the block claims to come from a library entry at all.</summary>
        internal static bool HasMasterIdentity(ProjectElement block) =>
            block.GetAttribute("master_name") is { Length: > 0 };

        /// <summary>
        /// The name a library block carries at insert, in the TWO forms the vendor's library uses:
        /// <c>{master_type}.{master_version}. {master_name}</c> where the entry is versioned, and
        /// <c>{master_type}. {master_name}</c> where it is not. Both are reconstructed from the block's own
        /// attributes, so no catalog is needed and a renamed block cannot be mistaken for an untouched one.
        /// <para>
        /// THE VERSIONLESS FORM IS MEASURED, not assumed: two families ship a <c>master_type</c> and no
        /// <c>master_version</c> — <c>4.1.01. AND ("Og"- blok)</c> and <c>4.1.04. Driftstimetæller</c>, 18
        /// occurrences across the corpus — and their <c>name</c> is exactly <c>master_type</c>, a full stop, a
        /// space and <c>master_name</c>. Requiring a version made both rows silent about those two families; T055
        /// found that and T055b widened it, naming the report-oracle impact up front.
        /// </para>
        /// <para>
        /// Null when there is no <c>master_type</c> at all: an authored block, or one the user saved to the library
        /// (which keeps <c>master_name</c> but gets no type, and IS its own library entry).
        /// </para>
        /// </summary>
        internal static string? InsertName(ProjectElement block) =>
            (block.GetAttribute("master_type"), block.GetAttribute("master_version"),
                block.GetAttribute("master_name")) switch
            {
                ({ Length: > 0 } type, { Length: > 0 } version, { Length: > 0 } master) =>
                    string.Create(CultureInfo.InvariantCulture, $"{type}.{version}. {master}"),
                ({ Length: > 0 } type, _, { Length: > 0 } master) =>
                    string.Create(CultureInfo.InvariantCulture, $"{type}. {master}"),
                _ => null,
            };
    }
}
