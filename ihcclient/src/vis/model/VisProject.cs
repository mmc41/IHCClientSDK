#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// A thin typed view over the <c>utcs_project</c> root <see cref="VisElement"/>: it adds authoring
    /// ergonomics (version, <c>id1</c>/<c>id2</c>/<c>last_unique_id</c>, the seven fixed children)
    /// without forking the generic-node representation. All accessors read/write the underlying bag.
    /// </summary>
    public sealed record VisProject(VisElement Root)
    {
        /// <summary>The <c>version_major.version_minor</c> of the project format (always 4.0 for v1).</summary>
        public string Version =>
            $"{Root.GetAttribute("version_major") ?? "4"}.{Root.GetAttribute("version_minor") ?? "0"}";

        /// <summary>The project creation stamp <c>id1</c> (constant for the project's life).</summary>
        public string? Id1 => Root.GetAttribute("id1");

        /// <summary>The current-save stamp <c>id2</c> (re-stamped every save).</summary>
        public string? Id2 => Root.GetAttribute("id2");

        /// <summary>The persistent high-water-mark id <c>last_unique_id</c>.</summary>
        public string? LastUniqueId => Root.GetAttribute("last_unique_id");

        /// <summary>The seven fixed root children, in document order.</summary>
        public ImmutableArray<VisElement> Children =>
            Root.Children.IsDefault ? ImmutableArray<VisElement>.Empty : Root.Children;

        /// <summary>Returns the named fixed child element (e.g. <c>groups</c>), or <c>null</c> when absent.</summary>
        public VisElement? Child(string tag) => Root.FindChild(tag);

        /// <summary>The <c>group</c> localities declared under <c>groups</c>.</summary>
        public IEnumerable<VisElement> Groups =>
            Child("groups") is { } groups && !groups.Children.IsDefaultOrEmpty
                ? groups.Children.Where(c => c.Tag == "group")
                : Enumerable.Empty<VisElement>();
    }
}
