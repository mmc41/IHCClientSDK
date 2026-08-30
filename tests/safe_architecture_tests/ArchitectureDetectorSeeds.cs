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

namespace Ihc.Telemetry.Seeded
{
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;

    // Controls for the instrumentation-core bypass detector (TelemetryCoreArchitectureTests). They live in the
    // TEST assembly, so they can never reach the production scan — which is anchored to the ihcclient and
    // ihc_openvisual assemblies — while being run through the exact same edge scan the production rules use.
    //
    // These matter more than the usual seed pair, because the production roster for this rule is EMPTY: every
    // SDK and GUI site already goes through the core. A rule whose exemption list is empty and whose subject
    // is clean is indistinguishable from a rule that detects nothing at all, so the only thing separating
    // "enforced" from "broken" here is that these seeds are flagged.

    /// <summary>Positive control: starts a span from a raw source instead of the core. Must be flagged.</summary>
    internal sealed class SeededDirectSpanStarter
    {
        private static readonly ActivitySource Source = new("Seeded.Bypass");

        public void Bypass()
        {
            using Activity? activity = Source.StartActivity("Seeded.Bypass");
        }
    }

    /// <summary>
    /// Positive control for the async reach: the bypass is written inside an async body, so the call is
    /// emitted on a compiler-generated state machine rather than on this type. A scan that only looked at
    /// authored types would miss it, which is precisely how a bypass would be introduced in real code — every
    /// workflow method that would want one is async.
    /// </summary>
    internal sealed class SeededAsyncDirectSpanStarter
    {
        private static readonly ActivitySource Source = new("Seeded.Bypass");

        public async Task BypassAsync()
        {
            using Activity? activity = Source.StartActivity("Seeded.BypassAsync");
            await Task.Yield();
        }
    }

    /// <summary>Positive control: a second meter outside the registries. Must be flagged.</summary>
    internal sealed class SeededMeterOwner
    {
        public Meter Rogue { get; } = new("Seeded.Rogue");
    }

    /// <summary>Positive control: an instrument built outside the registries. Must be flagged.</summary>
    internal sealed class SeededInstrumentOwner(Meter meter)
    {
        public Counter<long> Rogue { get; } = meter.CreateCounter<long>("seeded.rogue");
    }

    /// <summary>
    /// Negative control: the migrated form. It reaches the same outcome through the core, so it holds an
    /// <see cref="OperationTelemetry"/>, calls <c>Start</c>, and touches an <see cref="Activity"/> — none of
    /// which may be flagged, or the rule would forbid the very shape it exists to require.
    /// </summary>
    internal sealed class SeededCoreUser(TelemetrySurface surface)
    {
        private readonly OperationTelemetry telemetry = new(surface, nameof(SeededCoreUser));

        public void Instrumented()
        {
            using OperationScope scope = telemetry.Start(nameof(Instrumented));
            scope.Activity?.SetTag("seeded.tag", 1);
        }
    }
}
