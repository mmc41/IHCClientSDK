using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// An unordered, immutable set with structural (by-value) equality — the set-semantics counterpart of
    /// <see cref="EquatableArray{T}"/>.
    /// <para>
    /// Use it for a <b>stored set member of a public value record</b>. A record compares an
    /// <see cref="ImmutableHashSet{T}"/> member by <i>reference</i>, so a single set member is enough to force a
    /// handwritten <c>Equals</c>/<c>GetHashCode</c> pair that must then list every <i>other</i> member of the record
    /// too — members that were already value-equal and gain nothing from being listed. Storing the set as an
    /// <see cref="EquatableSet{T}"/> removes the whole list.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Element type, compared with <see cref="EqualityComparer{T}.Default"/>.</typeparam>
    /// <remarks>
    /// <para><b>Equality is order-independent</b> (set equality): two values are equal when each contains exactly the
    /// other's elements, whatever order they were added in. <b>Hashing</b> combines element hashes with an
    /// order-independent operation plus the count, so equal values always hash equally. Hash codes are process-local
    /// implementation details and must never be persisted.</para>
    /// <para><b><c>default</c> is empty</b>, exactly as for <see cref="EquatableArray{T}"/>: every read member
    /// behaves identically on <c>default</c> and <see cref="Empty"/>, and no representation-level state distinguishes
    /// them.</para>
    /// <para>The default comparer is assumed throughout. Converting in an <see cref="ImmutableHashSet{T}"/> built
    /// with a custom comparer is not supported: equality would follow that set's comparer rather than this type's
    /// documented contract.</para>
    /// </remarks>
    [CollectionBuilder(typeof(EquatableSet), nameof(EquatableSet.Create))]
    public readonly struct EquatableSet<T> : IReadOnlyCollection<T>, IEquatable<EquatableSet<T>>
    {
        private readonly ImmutableHashSet<T>? items;

        private EquatableSet(ImmutableHashSet<T>? items) => this.items = items;

        /// <summary>
        /// The single normalizing accessor every read member goes through, so a <c>default</c> instance reads as
        /// empty instead of dereferencing null. <see cref="ImmutableHashSet{T}.Empty"/> is a cached singleton, so
        /// this allocates nothing.
        /// </summary>
        private ImmutableHashSet<T> Items => items ?? ImmutableHashSet<T>.Empty;

        /// <summary>The empty set. Equal to <c>default(EquatableSet&lt;T&gt;)</c>.</summary>
        public static EquatableSet<T> Empty => default;

        /// <summary>The number of distinct elements.</summary>
        public int Count => Items.Count;

        /// <summary>Whether the set has no elements. True for <c>default</c>.</summary>
        public bool IsEmpty => Items.IsEmpty;

        /// <summary>
        /// Returns the concrete struct enumerator, so <c>foreach</c> over this type binds to the enumerable pattern
        /// and neither boxes an interface nor allocates. Enumeration order is unspecified.
        /// </summary>
        public ImmutableHashSet<T>.Enumerator GetEnumerator() => Items.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();

        /// <summary>Whether <paramref name="item"/> is a member of the set.</summary>
        public bool Contains(T item) => Items.Contains(item);

        /// <summary>
        /// The backing storage, normalized so a <c>default</c> instance yields an empty (never null) set.
        /// Allocation-free. This is the explicit escape hatch for set operations outside the read surface.
        /// </summary>
        public ImmutableHashSet<T> AsImmutableHashSet() => Items;

        /// <summary>
        /// Adopts immutable storage without copying elements. Implicit, for the SDK code that already materializes
        /// <see cref="ImmutableHashSet{T}"/>. The source must use the default comparer — see the type remarks.
        /// </summary>
        public static implicit operator EquatableSet<T>(ImmutableHashSet<T>? items) => new EquatableSet<T>(items);

        /// <summary>Order-independent set equality. <c>default</c> equals empty.</summary>
        public bool Equals(EquatableSet<T> other) => Items.SetEquals(other.Items);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is EquatableSet<T> other && Equals(other);

        /// <summary>
        /// Order-independent content hash, consistent with <see cref="Equals(EquatableSet{T})"/>. Element hashes are
        /// combined with XOR precisely because it is commutative — the enumeration order of a set is unspecified, so
        /// an order-sensitive combine would let two equal sets hash differently. A set holds no duplicates, so the
        /// usual XOR weakness (a repeated element cancelling itself out) cannot arise here.
        /// </summary>
        public override int GetHashCode()
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            int elements = 0;
            foreach (T item in Items)
            {
                elements ^= item is null ? 0 : comparer.GetHashCode(item);
            }
            return HashCode.Combine(elements, Items.Count);
        }

        /// <summary>Structural equality. See <see cref="Equals(EquatableSet{T})"/>.</summary>
        public static bool operator ==(EquatableSet<T> left, EquatableSet<T> right) => left.Equals(right);

        /// <summary>Structural inequality. See <see cref="Equals(EquatableSet{T})"/>.</summary>
        public static bool operator !=(EquatableSet<T> left, EquatableSet<T> right) => !left.Equals(right);
    }

    /// <summary>
    /// Factories for <see cref="EquatableSet{T}"/>. Also the collection builder that makes C# collection expressions
    /// (<c>[]</c>, <c>[a, b]</c>) construct the type directly.
    /// </summary>
    public static class EquatableSet
    {
        /// <summary>
        /// Snapshots <paramref name="values"/> into an immutable set, discarding duplicates. This is the
        /// collection-expression builder; the span is copied, so no caller buffer is retained.
        /// </summary>
        public static EquatableSet<T> Create<T>(ReadOnlySpan<T> values) => ImmutableHashSet.Create(values);

        /// <summary>
        /// Snapshots <paramref name="values"/> into an immutable set, discarding duplicates. Use at boundaries that
        /// accept caller-owned mutable collections: later mutation of the source cannot affect the returned value.
        /// </summary>
        public static EquatableSet<T> CreateRange<T>(IEnumerable<T> values) => ImmutableHashSet.CreateRange(values);
    }
}
