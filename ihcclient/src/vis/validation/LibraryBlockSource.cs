#nullable enable
using Ihc.Vis.Model;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// What a rule may ask about the LIBRARY a placed function block claims to come from: give me the body the
    /// library holds for this master type and version.
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
    }
}
