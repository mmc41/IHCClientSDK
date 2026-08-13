#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// The single, generic, immutable node every <c>.vis</c> and catalog-file (<c>.def</c>/<c>.ifb</c>) element
    /// uses: a <see cref="Tag"/>, an optional <see cref="Id"/>, an ordered attribute bag (logical/unescaped
    /// values) and ordered <see cref="Children"/>. Every element type — root, group, product, function block,
    /// resource, program leaf — shares this one shape. Bag order is meaningful: registry (ATTLIST) order in a
    /// canonicalized project tree, authored/source order in a catalog definition body (which the catalog writer
    /// emits verbatim, presence and order preserved).
    /// </summary>
    /// <remarks>
    /// This is the shape the writers/readers/insert-transform all operate on, and the shape that holds
    /// deep-copied catalog subtrees verbatim, so attributes added by newer IHC Visual
    /// versions are preserved rather than dropped. The wire-format facts (ATTLIST order, defaults,
    /// rendering) live in the schema registry and the definition's <see cref="CatalogGrammar"/>, never on the node.
    /// </remarks>
    public sealed record ProjectElement(
        string Tag,
        ElementId? Id,
        EquatableArray<(string Name, string Value)> Attrs,
        EquatableArray<ProjectElement> Children)
    {
        /// <summary>
        /// Builds an element with the <c>id</c> token leading the attribute bag (when <paramref name="id"/> is
        /// present), followed by <paramref name="attrs"/>. The shared authoring factory the builders and grammar
        /// helpers use so the id-led-bag convention lives in one place; a later canonicalize pass fixes attribute
        /// order and omits defaults.
        /// </summary>
        public static ProjectElement Create(string tag, ElementId? id,
            IEnumerable<(string Name, string Value)> attrs, IEnumerable<ProjectElement> children)
        {
            var bag = ImmutableArray.CreateBuilder<(string, string)>();
            if (id is { } value)
            {
                bag.Add(("id", value.ToToken()));
            }
            bag.AddRange(attrs);
            return new ProjectElement(tag, id, bag.ToImmutable(), children.ToImmutableArray());
        }

        /// <summary>Returns the logical value of the named attribute, or <c>null</c> when absent.</summary>
        public string? GetAttribute(string name) => GetAttribute(Attrs, name);

        /// <summary>
        /// Returns the logical value of the named attribute from a raw attribute bag, or <c>null</c> when absent.
        /// The allocation-free scan the instance <see cref="GetAttribute(string)"/> and the readers/insert transform
        /// share — those hold a bag before an element is built, so they cannot use the instance form.
        /// </summary>
        internal static string? GetAttribute(EquatableArray<(string Name, string Value)> attrs, string name)
        {
            if (attrs.IsEmpty)
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
        /// Returns a copy of a raw attribute bag with the named attribute set: replaced in place when present,
        /// appended at the end when absent — the bag-level peer of <see cref="WithAttribute"/> for callers that
        /// hold a bag before an element is built (see <see cref="GetAttribute(EquatableArray{ValueTuple{string, string}}, string)"/>).
        /// </summary>
        internal static EquatableArray<(string Name, string Value)> SetAttribute(
            EquatableArray<(string Name, string Value)> attrs, string name, string value)
        {
            // The wrapper deliberately exposes no SetItem/Add, so the one conversion to the backing array
            // lives here rather than at every caller.
            ImmutableArray<(string Name, string Value)> bag = attrs.AsImmutableArray();
            for (int i = 0; i < bag.Length; i++)
            {
                if (bag[i].Name == name)
                {
                    return bag.SetItem(i, (name, value));
                }
            }
            return bag.Add((name, value));
        }

        /// <summary>
        /// Returns a copy with the named attribute set: the existing entry is replaced in place (registry order
        /// preserved) or, when absent, appended at the end.
        /// </summary>
        public ProjectElement WithAttribute(string name, string value) =>
            this with { Attrs = SetAttribute(Attrs, name, value) };

        /// <summary>Returns the first direct child with the given tag, or <c>null</c> when none.</summary>
        public ProjectElement? FindChild(string tag)
        {
            foreach (ProjectElement child in Children)
            {
                if (child.Tag == tag)
                {
                    return child;
                }
            }
            return null;
        }

        // ChildrenOrEmpty()/AttrsOrEmpty() are gone: EquatableArray<T> reads a default instance as empty, so
        // Children and Attrs are already the null-safe form and a passthrough would only duplicate them.

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
        /// This element followed by every descendant in document order (depth-first, pre-order) — the
        /// root-inclusive companion of <see cref="Descendants"/>, for collectors that project or filter
        /// over a whole subtree.
        /// </summary>
        public IReadOnlyList<ProjectElement> DescendantsAndSelf()
        {
            var acc = new List<ProjectElement> { this };
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
            foreach (ProjectElement child in Children)
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
            if (element.Children.IsEmpty)
            {
                return;
            }
            foreach (ProjectElement child in element.Children)
            {
                acc.Add(child);
                Collect(child, acc);
            }
        }

        // Structural (value) equality over the whole subtree is the compiler's: EquatableArray<T> compares
        // Attrs and Children by content, recursing into child elements, so two elements built independently
        // from the same data are equal — and a member added later is covered without touching this file.

        public override string ToString() =>
            $"ProjectElement(Tag={Tag}, Id={Id}, Attrs=[{Attrs.Length}], Children=[{Children.Length}])";
    }
}
