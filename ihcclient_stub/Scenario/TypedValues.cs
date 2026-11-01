using System.Collections.Immutable;

namespace Ihc.Stubbing
{
    /// <summary>Checks the scenario groups share for the typed values a test assigns.</summary>
    internal static class TypedValues
    {
        /// <summary>Refuses <c>default</c>, which reads as an array but throws on first use, far from the assignment
        /// that caused it.</summary>
        internal static void RequireInitialized<T>(ImmutableArray<T> value)
        {
            if (value.IsDefault)
            {
                throw new ArgumentException("an uninitialized array is no value; assign [] for an empty one", nameof(value));
            }
        }
    }
}
