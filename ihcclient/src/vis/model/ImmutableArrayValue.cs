#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Projects
{
    /// <summary>
    /// Structural (value) equality and hashing for the <see cref="ImmutableArray{T}"/> members of the project
    /// model records. A <c>record</c> compares an <see cref="ImmutableArray{T}"/> field by its backing-array
    /// reference, not by content, so two otherwise-identical records would be unequal; these helpers restore the
    /// by-value semantics a record is expected to have. Treats <c>default</c> and empty as equal (the model uses
    /// both interchangeably for "no items").
    /// </summary>
    internal static class ImmutableArrayValue
    {
        /// <summary>Order-sensitive content equality of two arrays; <c>default</c> and empty compare equal.</summary>
        public static bool Equal<T>(ImmutableArray<T> a, ImmutableArray<T> b)
        {
            bool bothEmpty = a.IsDefaultOrEmpty && b.IsDefaultOrEmpty;
            bool eitherEmpty = a.IsDefaultOrEmpty || b.IsDefaultOrEmpty;
            return bothEmpty || (!eitherEmpty && a.SequenceEqual(b));
        }

        /// <summary>Order-sensitive content hash, consistent with <see cref="Equal{T}"/>.</summary>
        public static int Hash<T>(ImmutableArray<T> items)
        {
            HashCode hash = new HashCode();
            if (!items.IsDefaultOrEmpty)
            {
                foreach (T item in items)
                {
                    hash.Add(item);
                }
            }
            return hash.ToHashCode();
        }
    }
}
