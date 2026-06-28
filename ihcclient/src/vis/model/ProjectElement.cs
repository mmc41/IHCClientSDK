#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Projects
{
    /// <summary>
    /// The single, generic, immutable node every <c>.vis</c> element uses: a <see cref="Tag"/>, an
    /// optional <see cref="Id"/>, an ordered attribute bag (logical/unescaped values, in registry
    /// order) and ordered <see cref="Children"/>. Every element type — root, group, product,
    /// function block, resource, program leaf — shares this one shape.
    /// </summary>
    /// <remarks>
    /// This is the shape the writer/reader/insert-transform all operate on, and the shape that holds
    /// deep-copied catalog subtrees verbatim (plan §3.7), so attributes added by newer IHC Visual
    /// versions are preserved rather than dropped. The wire-format facts (ATTLIST order, defaults,
    /// rendering) live in the schema registry, never on the node.
    /// </remarks>
    public sealed record ProjectElement(
        string Tag,
        ElementId? Id,
        ImmutableArray<(string Name, string Value)> Attrs,
        ImmutableArray<ProjectElement> Children)
    {
        /// <summary>Returns the logical value of the named attribute, or <c>null</c> when absent.</summary>
        public string? GetAttribute(string name) =>
            Attrs.IsDefaultOrEmpty ? null : Attrs.FirstOrDefault(a => a.Name == name).Value;

        /// <summary>Returns the first direct child with the given tag, or <c>null</c> when none.</summary>
        public ProjectElement? FindChild(string tag) =>
            Children.IsDefaultOrEmpty ? null : Children.FirstOrDefault(c => c.Tag == tag);

        /// <summary>
        /// Structural (value) equality over the whole subtree. The synthesized record equality would compare
        /// <see cref="Attrs"/>/<see cref="Children"/> by backing-array reference; this overload compares them by
        /// content (recursing into children), so two elements built independently from the same data are equal.
        /// </summary>
        public bool Equals(ProjectElement? other) =>
            other is not null
            && Tag == other.Tag
            && Id == other.Id
            && ImmutableArrayValue.Equal(Attrs, other.Attrs)
            && ImmutableArrayValue.Equal(Children, other.Children);

        public override int GetHashCode() =>
            HashCode.Combine(Tag, Id, ImmutableArrayValue.Hash(Attrs), ImmutableArrayValue.Hash(Children));

        public override string ToString() =>
            $"ProjectElement(Tag={Tag}, Id={Id}, Attrs=[{(Attrs.IsDefaultOrEmpty ? 0 : Attrs.Length)}], Children=[{(Children.IsDefaultOrEmpty ? 0 : Children.Length)}])";
    }
}
