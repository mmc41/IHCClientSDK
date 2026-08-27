#nullable enable
using Ihc.Vis.Model;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// What a rule may ask about the LIBRARY a placed function block claims to come from: give me the body the
    /// library holds for this master type and version, or tell me which versions of that type it holds at all.
    ///
    /// <para><b>Why a port and not the catalog itself.</b> The validation context may carry the catalog, and this
    /// is the narrowest shape that satisfies that: the port is declared HERE, in the
    /// validation layer, and speaks only <see cref="ProjectElement"/> — so the layer's dependency set does not
    /// widen by one namespace, and the L1–L5 layer rules (<c>ARCHITECTURE.md</c>, challenge 5) are untouched. The composition root
    /// (<c>ProjectAppService</c>) already holds a catalog and adapts it; a caller with no catalog supplies nothing
    /// and the rule that needs it is not evaluated, exactly as a capacity rule behaves without controller
    /// limits.</para>
    ///
    /// <para><b>What it deliberately does NOT offer:</b> the catalog's product side, its grammar, its discovery or
    /// its import. A rule asking a library question needs one lookup, and a port that offered more would invite a
    /// rule to reach for it.</para>
    ///
    /// <para><b>The two members answer two DIFFERENT questions, which is why there are two.</b>
    /// <see cref="TryGetBody"/> is about CONTENT — what does the library say this block should look like — and is
    /// the question <c>logic-block-locked-content</c> asks. <see cref="TryGetVersions"/> is about PRESENCE, and
    /// deliberately hands back no body: a rule asking whether the library has a type at all, or which revisions of
    /// it, has no business reading one. Neither can be derived from the other — a body lookup keyed on an exact
    /// identity cannot say "absent at every version", and a version list cannot say what the body holds.</para>
    /// </summary>
    public interface ILibraryBlockSource
    {
        /// <summary>
        /// The library block's body for that master identity, or <see langword="false"/> when the library holds no
        /// such entry — a block claiming an identity the library does not have is not this port's finding to make.
        /// </summary>
        /// <param name="masterType">The <c>master_type</c> the placed block carries.</param>
        /// <param name="masterVersion">The <c>master_version</c> it carries; may be empty for a versionless family.</param>
        /// <param name="body">The library body, when the library holds one.</param>
        bool TryGetBody(string masterType, string masterVersion, out ProjectElement body);

        /// <summary>
        /// Every version the library holds of one master TYPE, or <see langword="false"/> when it holds none at
        /// any version.
        /// <para>
        /// IMPLEMENTERS: the list is DISTINCT and ORDINAL-ASCENDING, and is never empty when this returns
        /// <see langword="true"/>. The order is part of the contract because a rule binds an element of it into a
        /// user-facing sentence, and a message whose wording depended on a dictionary's enumeration order would
        /// differ between two runs over the same project. On a miss, <paramref name="versions"/> is the empty
        /// list rather than <c>default</c>, so a caller that ignores the return value still reads something safe.
        /// </para>
        /// <para>
        /// PLURAL BY CONTRACT, not by observation. Today's built-in library holds each of its types at exactly one
        /// version, but that is a fact about that data: an installed library may ship two revisions of one block
        /// side by side, and a single-version answer would encode the accident as the interface.
        /// </para>
        /// </summary>
        /// <param name="masterType">The <c>master_type</c> to look for, at any version.</param>
        /// <param name="versions">The versions held, distinct and ordinal-ascending; empty when none is.</param>
        bool TryGetVersions(string masterType, out EquatableArray<string> versions);
    }
}
