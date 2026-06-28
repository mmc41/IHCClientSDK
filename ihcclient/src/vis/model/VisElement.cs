#nullable enable
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Model
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
    public sealed record VisElement(
        string Tag,
        ElementId? Id,
        ImmutableArray<(string Name, string Value)> Attrs,
        ImmutableArray<VisElement> Children)
    {
        /// <summary>Returns the logical value of the named attribute, or <c>null</c> when absent.</summary>
        public string? GetAttribute(string name) =>
            Attrs.IsDefaultOrEmpty ? null : Attrs.FirstOrDefault(a => a.Name == name).Value;

        /// <summary>Returns the first direct child with the given tag, or <c>null</c> when none.</summary>
        public VisElement? FindChild(string tag) =>
            Children.IsDefaultOrEmpty ? null : Children.FirstOrDefault(c => c.Tag == tag);
    }
}
