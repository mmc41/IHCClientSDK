#nullable enable
using System;
using System.Collections.Generic;
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
    /// deep-copied catalog subtrees verbatim, so attributes added by newer IHC Visual
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
        public string? GetAttribute(string name) => GetAttribute(Attrs, name);

        /// <summary>
        /// Returns the logical value of the named attribute from a raw attribute bag, or <c>null</c> when absent.
        /// The allocation-free scan the instance <see cref="GetAttribute(string)"/> and the readers/insert transform
        /// share — those hold a bag before an element is built, so they cannot use the instance form.
        /// </summary>
        internal static string? GetAttribute(ImmutableArray<(string Name, string Value)> attrs, string name)
        {
            if (attrs.IsDefaultOrEmpty)
            {
                return null;
            }
            foreach ((string Name, string Value) attr in attrs)
            {
                if (attr.Name == name)
                {
                    return attr.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a copy with the named attribute set: the existing entry is replaced in place (registry order
        /// preserved) or, when absent, appended at the end.
        /// </summary>
        public ProjectElement WithAttribute(string name, string value)
        {
            ImmutableArray<(string Name, string Value)> attrs =
                Attrs.IsDefaultOrEmpty ? ImmutableArray<(string, string)>.Empty : Attrs;
            for (int i = 0; i < attrs.Length; i++)
            {
                if (attrs[i].Name == name)
                {
                    return this with { Attrs = attrs.SetItem(i, (name, value)) };
                }
            }
            return this with { Attrs = attrs.Add((name, value)) };
        }

        /// <summary>Returns the first direct child with the given tag, or <c>null</c> when none.</summary>
        public ProjectElement? FindChild(string tag)
        {
            foreach (ProjectElement child in ChildrenOrEmpty())
            {
                if (child.Tag == tag)
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>The children in document order, or an empty array when this element has none — the null-safe form of <see cref="Children"/>.</summary>
        public ImmutableArray<ProjectElement> ChildrenOrEmpty() =>
            Children.IsDefaultOrEmpty ? ImmutableArray<ProjectElement>.Empty : Children;

        /// <summary>The attribute bag in registry order, or an empty array when this element has none — the null-safe form of <see cref="Attrs"/>.</summary>
        public ImmutableArray<(string Name, string Value)> AttrsOrEmpty() =>
            Attrs.IsDefaultOrEmpty ? ImmutableArray<(string, string)>.Empty : Attrs;

        /// <summary>
        /// Every element below this one in document order (depth-first, pre-order), excluding this element itself.
        /// The order matches the file's top-to-bottom element order, so ids enumerate exactly as they appear on
        /// disk — the read primitive an id-addressable selection model iterates over.
        /// </summary>
        public IReadOnlyList<ProjectElement> Descendants()
        {
            var acc = new List<ProjectElement>();
            Collect(this, acc);
            return acc;
        }

        /// <summary>
        /// The first element in this subtree — this element or any descendant, pre-order — that satisfies
        /// <paramref name="match"/>, or <c>null</c> when none does. Short-circuits on the first hit without
        /// materializing the subtree, so an id-addressed lookup resolves in one early-exit walk.
        /// </summary>
        public ProjectElement? FindDescendantOrSelf(Func<ProjectElement, bool> match)
        {
            if (match(this))
            {
                return this;
            }
            foreach (ProjectElement child in ChildrenOrEmpty())
            {
                ProjectElement? found = child.FindDescendantOrSelf(match);
                if (found is not null)
                {
                    return found;
                }
            }
            return null;
        }

        private static void Collect(ProjectElement element, List<ProjectElement> acc)
        {
            if (element.Children.IsDefaultOrEmpty)
            {
                return;
            }
            foreach (ProjectElement child in element.Children)
            {
                acc.Add(child);
                Collect(child, acc);
            }
        }

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
