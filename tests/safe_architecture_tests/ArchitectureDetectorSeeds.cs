namespace Ihc.Vis.Editing.Seeded
{
    // Test-only types in a nested engine namespace. Architecture detector controls use them to prove that namespace
    // subtree checks and generic-constraint traversal do not stop at the exact Ihc.Vis.Editing namespace.
    internal interface INestedEngineContract { }

    internal sealed class NestedEngineType : INestedEngineContract { }
}

namespace Ihc.Transport.Seeded
{
    // Seeded violator for the SDK transport rule's System.ServiceModel half, whose production verdict is empty.
    // Hand-written code outside the generated SOAP layer reaching a WCF type is exactly what that rule refuses.
    internal static class SeededServiceModelReference
    {
        internal static object Binding() => new System.ServiceModel.BasicHttpBinding();
    }

    // Seeded violator for the controller stub's outbound rule, whose production verdict is empty: a client built
    // with no handler, whose socket is the BCL's own.
    internal static class SeededOutboundClient
    {
        internal static System.Net.Http.HttpClient Build() => new();
    }

    // Seeded violators for the stub-surface rule's halves that the SDK leaves empty: a second reader of the stub
    // predicate, and a stub implemented outside the stub namespace.
    internal static class SeededPredicateReader
    {
        internal static bool Read(IhcSettings settings) => settings.IsStubConfigured();
    }

    internal sealed class SeededControllerStub : Ihc.Stubbing.IControllerStub
    {
        public System.Threading.Tasks.Task<Ihc.Stubbing.ControllerStubResponse> HandleAsync(
            Ihc.Stubbing.ControllerStubRequest request, System.Threading.CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(new Ihc.Stubbing.ControllerStubResponse(200, string.Empty));
    }

    // Seed for the Lab friend-grant rule's constructor half: a PUBLIC type with a public constructor and an
    // internal transport overload, the shape the SDK's services have, and a caller of each. Only the internal
    // overload's call may be reported, which a scan that judged the type or the member name could not tell apart.
    public sealed class SeededTransportService
    {
        public SeededTransportService(string endpoint)
        {
            Endpoint = endpoint;
        }

        internal SeededTransportService(string endpoint, System.Net.Http.HttpClient transport)
            : this(endpoint)
        {
            Transport = transport;
        }

        public string Endpoint { get; }

        internal System.Net.Http.HttpClient? Transport { get; }
    }

    internal static class SeededTransportServiceCallers
    {
        internal static SeededTransportService Public() => new("https://seeded.invalid");

        internal static SeededTransportService ThroughTheTransportOverload(System.Net.Http.HttpClient transport) =>
            new("https://seeded.invalid", transport);
    }

    // Seeded violators for the stub-endpoint literal scan: one spelling written in place, and one a const the
    // compiler inlines at its use, which is the shape a copied prefix constant takes.
    internal static class SeededStubEndpointLiterals
    {
        private const string StubHost = "http://seeded.controllerstub.invalid";

        internal static string MockKey() => "mock://seeded";

        internal static string InlinedStubHost() => StubHost;
    }
}

namespace Ihc.Vis.Io.Seeded
{
    // Test-only type in a nested IO namespace, the sibling of the editing pair above. It arms the port-surface
    // control's IO half on a type the SDK does not own, which is what lets that control keep expressing the leak
    // once the real IO implementation types are internal.
    internal sealed class NestedIoType { }
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

    // Controls for the element-model immutability detector (ValueCollectionArchitectureTests). Same reason as
    // above: the production scan is anchored to the ihcclient assembly's model namespace, so these can never
    // reach it, while the predicate they are run through is the very one that scan uses.

    /// <summary>
    /// Positive control: both ways an instance can change after construction — a settable property and a
    /// writable field. Must be flagged, twice.
    /// </summary>
    public sealed record SeededMutableRecord(string Name)
    {
        public string Label { get; set; } = Name;

        // Internal rather than public only to satisfy CA1051, which bans an externally visible instance field.
        // The detector scans public AND non-public members, so this is the same shape it must catch in the model.
        internal int Counter = 1;
    }

    /// <summary>
    /// Negative control: the immutable forms the model actually uses. A positional parameter, an <c>init</c>
    /// property and a computed one are all closed after construction, so an over-broad "has a setter" rule
    /// would false-positive here on every record in the model.
    /// </summary>
    public sealed record SeededImmutableRecord(string Name)
    {
        public string? Label { get; init; }

        public int Length => Name.Length;

        public static int Shared { get; set; }
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
    /// Positive control for the OTHER half of the transport exclusion. A constructor handed its transport is
    /// exempt because it reaches only where that transport does — which holds exactly as long as a suite
    /// cannot build a live one. This builds one: an HttpClient given no handler talks to whatever address it
    /// is asked for, over the BCL's own socket. Must be flagged.
    /// </summary>
    internal static class SeededLiveTransportBuilder
    {
        internal static System.Net.Http.HttpClient Build() => new();
    }

    /// <summary>
    /// Positive control for the raw half of the live-transport rule: a socket client of its own, with no HTTP layer
    /// above it, so no route and no latch stand in its way. Must be flagged.
    /// </summary>
    internal static class SeededRawSocketBuilder
    {
        internal static System.Net.Sockets.TcpClient Build() => new();
    }

    /// <summary>
    /// Positive control for a live transport built behind a static factory rather than a constructor: the legacy
    /// request API creates its client inside <c>WebRequest.Create</c>. Must be flagged.
    /// </summary>
    internal static class SeededWebRequestBuilder
    {
#pragma warning disable SYSLIB0014 // The obsolete factory is the very shape this seed exists to show.
        internal static System.Net.WebRequest Build() => System.Net.WebRequest.Create("http://seeded-request.invalid");
#pragma warning restore SYSLIB0014
    }

    /// <summary>A handler this assembly declares, answering from itself: the shape a suite is SUPPOSED to
    /// substitute at the socket.</summary>
    internal sealed class SeededStubHandler : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken) =>
            Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }

    /// <summary>
    /// Negative control: a client built OVER that stub reaches only where the stub does, so neither the
    /// handler nor the client is a live transport. Flagging this would indict the substitution the safety rule
    /// prescribes, exactly as flagging the local stub above would.
    /// </summary>
    internal static class SeededStubTransportUser
    {
        // CA2000: the handler is handed to the HttpClient, which owns it from here on - the same ownership
        // transfer Client.CreateHttpClient documents.
#pragma warning disable CA2000
        internal static System.Net.Http.HttpClient Build() => new(new SeededStubHandler());
#pragma warning restore CA2000
    }

    /// <summary>
    /// Positive control for the subclass rule on the transport side: a handler this assembly declares over the
    /// BCL's own client handler. Declared here or not, it IS that socket handler, so its constructor, which builds
    /// the base, builds a live transport. Must be flagged at that constructor.
    /// </summary>
    internal sealed class SeededClientHandlerSubclass : System.Net.Http.HttpClientHandler
    {
    }

    /// <summary>A client over the subclass above, used the way a suite would use it. Flagged by the outbound rule
    /// the stub and the executables are held to, which counts any client; not by the reach scan, which judges the
    /// subclass where it is built.</summary>
    internal static class SeededClientHandlerSubclassUser
    {
        // CA2000: the handler is handed to the HttpClient, which owns it from here on. CA5399: the seed is never run,
        // only read as IL, so no certificate is ever checked through it.
#pragma warning disable CA2000, CA5399
        internal static System.Net.Http.HttpClient Build() => new(new SeededClientHandlerSubclass());
#pragma warning restore CA2000, CA5399
    }

    /// <summary>
    /// Positive control for the subclass rule on the service side: a service this assembly declares over a real
    /// one, built from settings. The SDK's services are not sealed, and such a subclass makes its own transport
    /// exactly as its base does. Must be flagged at its constructor.
    /// </summary>
    internal sealed class SeededServiceSubclass : AuthenticationService
    {
        internal SeededServiceSubclass()
            : base(new IhcSettings { Endpoint = "http://seeded.invalid" })
        {
        }
    }

    /// <summary>Builds the subclass above, so the seed is used as a suite would use it.</summary>
    internal static class SeededServiceSubclassUser
    {
        internal static AuthenticationService Build() => new SeededServiceSubclass();
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
