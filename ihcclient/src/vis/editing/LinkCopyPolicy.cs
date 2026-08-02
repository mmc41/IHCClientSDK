#nullable enable

namespace Ihc.Vis.Editing
{
    /// <summary>
    /// How <see cref="ProjectEditor.CopySubtree"/> treats a copied reciprocal half — a follow-link half
    /// (<c>link_from_resource</c>/<c>link_to_resource</c>) or a scene row (<c>scene_relay</c>/<c>scene_dimmer</c>/
    /// <c>scene_shutter</c>/<c>scene_link</c>) — whose partner lies <b>outside</b> the copied subtree. Internal
    /// pairs (both halves inside the copy) are always deep-copied and remapped to the new ids regardless of policy;
    /// this only governs the cross-boundary halves the clone would otherwise leave pointing at the source's partners.
    /// </summary>
    public enum LinkCopyPolicy
    {
        /// <summary>
        /// Drop each copied half whose partner is outside the subtree, so the paste carries only fully-internal
        /// pairs and validates clean. The safe default for clipboard paste.
        /// </summary>
        DropExternal,

        /// <summary>
        /// Keep such halves as copied — their <c>link</c> still points at the source's partner, so the paste is
        /// wired one-way and will not validate until the caller reconnects or removes them. For callers that clone
        /// raw and fix links up themselves.
        /// </summary>
        KeepExternal,

        /// <summary>
        /// Drop <b>every</b> copied half, internal pairs included, so the duplicate arrives completely unwired.
        /// This is what IHC Visual's clipboard paste does — measured on a whole-locality copy, where the source
        /// room carried six link halves and the pasted duplicate carried none (uxparity S-10). Distinct from
        /// <see cref="DropExternal"/>, which keeps and remaps a pair whose two ends are both inside the copy.
        /// </summary>
        DropAll,
    }
}
