namespace Ihc.Vis.Editing.Seeded
{
    // Test-only types in a nested engine namespace. Architecture detector controls use them to prove that namespace
    // subtree checks and generic-constraint traversal do not stop at the exact Ihc.Vis.Editing namespace.
    internal interface INestedEngineContract { }

    internal sealed class NestedEngineType : INestedEngineContract { }
}

namespace Ihc.Vis.Seeded
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Ihc.Vis.Model;

    // Controls for the value-collection backing-field detector (ValueCollectionArchitectureTests). They live in
    // the TEST assembly, so they can never reach the production scan — which is anchored to the ihcclient
    // assembly — while still being run through the exact same predicate the production scan uses.

    /// <summary>Positive control: a stored ordered sequence as a raw <c>ImmutableArray</c>. Must be flagged.</summary>
    public sealed record SeededRawImmutableArrayRecord(ImmutableArray<string> Rows);

    /// <summary>Positive control: a stored ordered sequence as a raw array. Must be flagged.</summary>
    public sealed record SeededRawArrayRecord(string[] Rows);

    /// <summary>Positive control: a stored ordered sequence as <c>IReadOnlyList</c>. Must be flagged.</summary>
    public sealed record SeededRawReadOnlyListRecord(IReadOnlyList<string> Rows);

    /// <summary>Negative control: the migrated form. Must NOT be flagged.</summary>
    public sealed record SeededWrappedRecord(EquatableArray<string> Rows);

    /// <summary>Negative control: set semantics are out of scope for an ORDERED-sequence rule.</summary>
    public sealed record SeededSetRecord(ImmutableHashSet<string> Ids);

    /// <summary>Negative control: map semantics are out of scope.</summary>
    public sealed record SeededMapRecord(ImmutableDictionary<string, string> Lookup);

    /// <summary>
    /// Negative control: a COMPUTED collection view. It has no backing field, so the rule must ignore it — the
    /// shape real production types use (<c>ProjectValidationResult.Warnings</c>,
    /// <c>FunctionBlockDefinition.Inputs</c>), and the one an over-broad property-based rule would false-positive on.
    /// </summary>
    public sealed record SeededComputedViewRecord(string Name)
    {
        public ImmutableArray<string> Rows => [Name];

        // Also a static lookup table and a method with sequence parameters/locals: none are instance state, so
        // none may be flagged.
        public static readonly ImmutableArray<string> Lookup = ["a", "b"];

        public int Count(IReadOnlyList<string> input)
        {
            string[] local = [.. input];
            return local.Length;
        }
    }
}
