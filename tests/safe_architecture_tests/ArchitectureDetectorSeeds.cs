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
    using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The bypass must be emitted from an INSTANCE method: the detector reads the authored origin member of "
        + "each call edge, and a static seed changes the shape the production rule is being proved against.")]
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
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The bypass must be emitted from an INSTANCE method: the detector reads the authored origin member of "
        + "each call edge, and a static seed changes the shape the production rule is being proved against.")]
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

namespace ihc_openvisual.Seeded
{
    using System;
    using System.Threading.Tasks;

    // Controls for the problem-surfacing detector (OpenVisualProblemSurfacingArchitectureTests). They live in
    // the TEST assembly, so they can never reach the production scan -- which is anchored to the GUI assembly's
    // own port and helpers -- while being run through the exact same predicate, with the anchors pointed here.

    /// <summary>A stand-in for the dialog port, carrying the two member NAMES the production rule guards.</summary>
    internal interface ISeededDialogPort
    {
        Task ShowProblemAsync(string title, string problem);

        Task ShowInternalErrorAsync(string error);

        /// <summary>An ordinary dialog, on the same port. It must NOT be guarded: the whole point of scoping the
        /// rule by member is that the rest of the port is the ordinary interactive UI.</summary>
        Task ConfirmAsync(string question);
    }

    /// <summary>Negative control: the admitted helper. It reaches the port deliberately, after telling the span,
    /// and must never be flagged -- it is what every other site is supposed to go through.</summary>
    internal static class SeededFailureReport
    {
        internal static Task FailedAsync(ISeededDialogPort dialogs, string title, string problem) =>
            dialogs.ShowProblemAsync(title, problem);
    }

    /// <summary>Negative control: a workflow that routes through the helper. It never names a guarded member, so
    /// the scan must not see it at all.</summary>
    internal sealed class SeededConformingWorkflow(ISeededDialogPort dialogs)
    {
        internal Task ReportAsync() => SeededFailureReport.FailedAsync(dialogs, "title", "problem");
    }

    /// <summary>Negative control: a workflow using the ordinary dialog surface. Flagging this is the failure mode
    /// that made a port-scoped rule useless, so it is pinned as a control rather than assumed.</summary>
    internal sealed class SeededOrdinaryDialogUser(ISeededDialogPort dialogs)
    {
        internal Task AskAsync() => dialogs.ConfirmAsync("question");
    }

    /// <summary>Positive control: shows a problem straight off the port. Must be flagged.</summary>
    internal sealed class SeededBypassingWorkflow(ISeededDialogPort dialogs)
    {
        internal Task ReportAsync() => dialogs.ShowProblemAsync("title", "problem");
    }

    /// <summary>
    /// Positive control for the reason the rule matches a REFERENCE rather than a call: this site never invokes
    /// the member. It hands it over as a method group, and whoever holds the delegate calls it later, from
    /// somewhere no scan would associate with a workflow. A call-matching rule sees nothing here, which is
    /// exactly how the real hand-off went unnoticed.
    /// </summary>
    internal sealed class SeededMethodGroupHandOff(ISeededDialogPort dialogs)
    {
        internal Func<string, Task> Hand() => dialogs.ShowInternalErrorAsync;
    }
}

namespace Ihc.Safety.Seeded
{
    using System.Threading.Tasks;

    // Controls for the controller-reach guard (ControllerReachArchitectureTests). They live HERE rather than in a
    // controller-free suite for the obvious reason: the guard runs in those suites, so a seeded violator planted
    // in one would fail the very rule it exists to arm. This assembly compiles the scan but not the guard.

    /// <summary>Positive control: builds a real service through the constructor that makes its OWN transport
    /// from settings. Nothing stands between it and the network. Must be flagged.</summary>
    internal static class SeededControllerReacher
    {
        internal static AuthenticationService Build() => new(new IhcSettings { Endpoint = "http://seeded.invalid" });
    }

    /// <summary>Negative control: a stub this assembly declares itself. It carries the marker interface and
    /// answers from fields, so flagging it would indict every test that writes its own double.</summary>
    internal sealed class SeededLocalStubService : IIHCApiService
    {
        public IhcSettings IhcSettings => new();
    }

    /// <summary>Negative control: constructing that stub is not reaching a controller.</summary>
    internal static class SeededLocalStubUser
    {
        internal static SeededLocalStubService Build() => new();
    }

    /// <summary>
    /// The scan's KNOWN LIMIT, pinned rather than left to prose: a service the test never constructs itself,
    /// because a product-side factory constructed it.
    /// </summary>
    /// <remarks>
    /// <para>This is NOT an exclusion the rule wants — unlike the two above, nothing about this shape is safe by
    /// construction. It is what "the scan reads DIRECT construction" costs, and the assertion over it exists so
    /// the cost is a measured fact with a name rather than a discovery someone makes later while reading IL.
    /// The day the scan derives the factory set from the product assembly, this control flips from
    /// <c>Does.Not.Contain</c> to <c>Does.Contain</c> and the change is visible in the diff.</para>
    /// <para>Nothing here reaches a wire: the endpoint is under the reserved <c>.invalid</c> TLD and no operation
    /// is called — the same three-fold reason the admitted bridge-wiring sites carry.</para>
    /// </remarks>
    internal static class SeededFactoryReacher
    {
        internal static Ihc.Vis.ProjectAppService Build() =>
            Ihc.Vis.ProjectAppService.CreateWithControllerBridge(
                new IhcSettings { Endpoint = "https://seeded.invalid" });
    }
}
