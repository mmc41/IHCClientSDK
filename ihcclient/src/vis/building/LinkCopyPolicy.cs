#nullable enable

namespace Ihc.Projects
{
    /// <summary>
    /// How <see cref="ProjectEditor.CopySubtree"/> treats a copied follow-link half (<c>link_from_resource</c>/
    /// <c>link_to_resource</c>) whose reciprocal partner lies <b>outside</b> the copied subtree. Internal links
    /// (both halves inside the copy) are always deep-copied and remapped to the new ids regardless of policy; this
    /// only governs the cross-boundary halves the clone would otherwise leave pointing at the source's partners.
    /// </summary>
    public enum LinkCopyPolicy
    {
        /// <summary>
        /// Drop each copied half whose partner is outside the subtree, so the paste carries only fully-internal
        /// links and validates clean. The safe default for clipboard paste.
        /// </summary>
        DropExternal,

        /// <summary>
        /// Keep such halves as copied — their <c>link</c> still points at the source's partner, so the paste is
        /// wired one-way and will not validate until the caller reconnects or removes them. For callers that clone
        /// raw and fix links up themselves.
        /// </summary>
        KeepExternal,
    }
}
