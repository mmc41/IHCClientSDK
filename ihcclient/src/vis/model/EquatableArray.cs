using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// An ordered, immutable sequence with structural (by-value) equality.
    /// <para>
    /// This is the collection type to use for a <b>stored ordered sequence member of a public value record</b>.
    /// A record compares an <see cref="ImmutableArray{T}"/> member by its backing-array <i>reference</i>, so two
    /// otherwise-identical records compare unequal and the only fix is a handwritten <c>Equals</c>/<c>GetHashCode</c>
    /// pair listing every member — a list that silently rots as members are added. Storing the sequence as an
    /// <see cref="EquatableArray{T}"/> instead lets compiler-generated record equality cover every current and
    /// future member automatically.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Element type. Elements are compared with <see cref="EqualityComparer{T}.Default"/>, so
    /// nested value records recurse into their own structural equality.</typeparam>
    /// <remarks>
    /// <para><b>Equality</b> is order-sensitive and element-wise. <b>Hashing</b> walks the same elements in the same
    /// order with the same comparer, so equal values always hash equally. Hash codes are process-local
    /// implementation details and must never be persisted or asserted on by exact value.</para>
    /// <para><b><c>default</c> is empty.</b> <c>default(EquatableArray&lt;T&gt;)</c> and <see cref="Empty"/> are the
    /// same logical value: they compare equal, hash equally, and every read member behaves identically on both.
    /// No representation-level <c>IsDefault</c> state is exposed, because observing it would distinguish two values
    /// that equality says are the same.</para>
    /// <para><b>Conversions</b> are deliberately asymmetric. <see cref="ImmutableArray{T}"/> converts <i>in</i>
    /// implicitly and without allocating, for builders and parsers that already materialize immutable storage;
    /// converting back out is the explicit <see cref="AsImmutableArray"/>, which keeps dependence on the concrete
    /// backing type visible and avoids overload ambiguity.</para>
    /// <para>This is a small read surface, not a mirror of <see cref="ImmutableArray{T}"/>. For a mutation-style
    /// immutable operation, call <see cref="AsImmutableArray"/> and work on the array.</para>
    /// </remarks>
    [CollectionBuilder(typeof(EquatableArray), nameof(EquatableArray.Create))]
    public readonly struct EquatableArray<T> : IReadOnlyList<T>, IEquatable<EquatableArray<T>>
    {
        private readonly ImmutableArray<T> items;

        private EquatableArray(ImmutableArray<T> items) => this.items = items;

        /// <summary>
        /// The single normalizing accessor every read member goes through, so a <c>default</c> instance reads as
        /// empty instead of throwing. <see cref="ImmutableArray{T}.Empty"/> is a cached singleton, so this allocates
        /// nothing.
        /// </summary>
        private ImmutableArray<T> Items => items.IsDefault ? ImmutableArray<T>.Empty : items;

        /// <summary>The empty sequence. Equal to <c>default(EquatableArray&lt;T&gt;)</c>.</summary>
        public static EquatableArray<T> Empty => default;

        /// <summary>The number of elements.</summary>
        public int Count => Items.Length;

        /// <summary>The number of elements. Alias of <see cref="Count"/>, for parity with array-shaped call sites.</summary>
        public int Length => Items.Length;

        /// <summary>Whether the sequence has no elements. True for <c>default</c>.</summary>
        public bool IsEmpty => Items.IsEmpty;

        /// <summary>The element at <paramref name="index"/>.</summary>
        public T this[int index] => Items[index];

        /// <summary>
        /// Returns the concrete struct enumerator, so <c>foreach</c> over this type binds to the enumerable pattern
        /// and neither boxes an interface nor allocates.
        /// </summary>
        public ImmutableArray<T>.Enumerator GetEnumerator() => Items.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();

        /// <summary>Whether <paramref name="item"/> occurs in the sequence, per <see cref="EqualityComparer{T}.Default"/>.</summary>
        public bool Contains(T item) => Items.Contains(item);

        /// <summary>The index of the first occurrence of <paramref name="item"/>, or -1.</summary>
        public int IndexOf(T item) => Items.IndexOf(item);

        /// <summary>
        /// The backing storage, normalized so a <c>default</c> instance yields an empty (never default) array.
        /// Allocation-free. This is the explicit escape hatch for operations outside the small read surface.
        /// </summary>
        public ImmutableArray<T> AsImmutableArray() => Items;

        /// <summary>
        /// Adopts immutable storage without copying elements. Implicit because SDK builders, readers and parsers
        /// already produce <see cref="ImmutableArray{T}"/> and the conversion cannot lose or alias mutable state.
        /// </summary>
        public static implicit operator EquatableArray<T>(ImmutableArray<T> items) => new EquatableArray<T>(items);

        /// <summary>
        /// Order-sensitive, element-wise equality over <see cref="EqualityComparer{T}.Default"/>. <c>default</c>
        /// equals empty. Delegates to the <see cref="ImmutableArray{T}"/> overload rather than looping here, so a
        /// value compared against a copy that still shares its backing array settles in O(1) on the array reference
        /// — the common case after a path-copying commit, where most elements are new instances holding the same
        /// children and attributes.
        /// </summary>
        public bool Equals(EquatableArray<T> other) => Items.SequenceEqual(other.Items);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        /// <summary>Order-sensitive content hash, consistent with <see cref="Equals(EquatableArray{T})"/>.</summary>
        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            foreach (T item in Items)
            {
                hash.Add(item);
            }
            return hash.ToHashCode();
        }

        /// <summary>Structural equality. See <see cref="Equals(EquatableArray{T})"/>.</summary>
        public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

        /// <summary>Structural inequality. See <see cref="Equals(EquatableArray{T})"/>.</summary>
        public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
    }

    /// <summary>
    /// Factories for <see cref="EquatableArray{T}"/>. Also the collection builder that makes C# collection
    /// expressions (<c>[]</c>, <c>[a, b]</c>) construct the type directly.
    /// </summary>
    public static class EquatableArray
    {
        /// <summary>
        /// Snapshots <paramref name="values"/> into immutable storage. This is the collection-expression builder;
        /// the span is copied, so no caller buffer is retained.
        /// </summary>
        public static EquatableArray<T> Create<T>(ReadOnlySpan<T> values) => ImmutableArray.Create(values);

        /// <summary>
        /// Snapshots <paramref name="values"/> into immutable storage. Use at boundaries that accept caller-owned
        /// mutable collections (<see cref="IReadOnlyList{T}"/>, <see cref="List{T}"/>): later mutation of the source
        /// cannot affect the returned value.
        /// </summary>
        public static EquatableArray<T> CreateRange<T>(IEnumerable<T> values) => ImmutableArray.CreateRange(values);
    }
}
