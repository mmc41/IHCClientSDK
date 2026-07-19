#nullable enable
using Ihc.Vis.Model;

namespace Ihc.Vis
{
    /// <summary>
    /// fablerefac W1-1 compile spike: proves the C# 14 extension-member syntax
    /// (<c>extension(ReceiverType) { ... }</c>) compiles under this repo's SDK and resolves as a
    /// parenthesis-free property, <em>before</em> the Wave-1 read surface
    /// (<c>element.Kind</c>/<c>.Name</c>/<c>.Effective</c>/…) commits 200+ GUI call sites to it. This is the
    /// seed class for that surface: <c>SpikeTag</c> is renamed away and replaced by the real read
    /// members in W1-2..W1-4.
    /// </summary>
    public static class ProjectElementRead
    {
        extension(ProjectElement element)
        {
            /// <summary>
            /// Spike-only echo of <see cref="ProjectElement.Tag"/>: exercised by the W1-1 test to prove an
            /// extension <c>property</c> (accessed with no <c>()</c>) resolves off a
            /// <see cref="ProjectElement"/>. Removed in W1-2.
            /// </summary>
            public string SpikeTag => element.Tag;
        }
    }
}
