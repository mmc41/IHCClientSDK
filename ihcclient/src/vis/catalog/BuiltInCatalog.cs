using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The SDK-embedded <see cref="ICatalog"/>: it materializes the full IHC Visual catalog from code-authored
    /// builder invocations — products (Phase B, generated), function blocks (FB plan, generated), and the three
    /// File→New templates (Phase C, hand-authored) — instead of scanning a vendor install, so the SDK can create and
    /// insert components with no desktop application present. It shares its lookup semantics with the install-dir
    /// path by producing a <see cref="MaterializedCatalog"/>; the eight <see cref="ICatalog"/> members below forward
    /// to that materialized value, which is the point at which the catalog is actually built.
    /// </summary>
    /// <remarks>
    /// <para><b>Deferred, shared materialization.</b> Building ~173 components is not free, and a <c>BuiltInCatalog</c>
    /// may be constructed (e.g. wrapped in a <c>CompositeCatalog</c>) long before — or without ever — being queried. So
    /// the <see cref="MaterializedCatalog"/> is built lazily on first member access; and because its content is 100%
    /// deterministic code-authored data, the one materialization is process-wide — every instance forwards to the same
    /// immutable value instead of re-running the factories.</para>
    /// <para><b>Extension points.</b> This is a <c>partial</c> class. Generated files implement
    /// <see cref="RegisterProducts"/> / <see cref="RegisterFunctionBlocks"/> (the hooks the catalog plans call
    /// <c>AllProducts()</c>/<c>AllFunctionBlocks()</c>) to append their factory outputs, and a hand-authored Phase C
    /// file implements <see cref="AuthorTemplates"/> to supply the three File→New templates. All three are classic
    /// <c>void</c> partial methods: absent until an implementing file exists, so an as-yet-ungenerated catalog
    /// materializes empty/partial rather than failing to compile.</para>
    /// </remarks>
    public sealed partial class BuiltInCatalog : ICatalog
    {
        // The materialized value; null means "not built yet". One process-wide materialization: every
        // BuiltInCatalog instance forwards to this same immutable value.
        private static MaterializedCatalog? snapshot;

        // Created on demand by LazyInitializer, which is why this is neither readonly nor pre-assigned.
        private static object? gate;

        /// <summary>
        /// The process-wide materialized catalog, built on first use.
        /// </summary>
        /// <remarks>
        /// <para><b>Built once, waited on rather than duplicated.</b> A second thread arriving while a first is
        /// materializing blocks and takes that result — which is what makes <see cref="Warmup"/> worth starting
        /// at all. Under the <c>PublicationOnly</c> <see cref="Lazy{T}"/> this replaced a caller never waited, it
        /// ran the factory itself, so an unfinished warm-up bought nothing but a second core doing the same
        /// work.</para>
        /// <para><b>A failure is still not cached.</b> A throwing factory leaves <c>snapshot</c> null, so the next
        /// access retries — the property <c>PublicationOnly</c> was chosen for, and the reason this is not
        /// <c>ExecutionAndPublication</c>, which caches the exception forever.</para>
        /// <para>Those two properties together are exactly
        /// <see cref="LazyInitializer.EnsureInitialized{T}(ref T, ref object, System.Func{T})"/> and no other
        /// primitive: <c>Lazy</c> caches the exception, and an <c>Interlocked</c> publication is
        /// <c>PublicationOnly</c> by another name.</para>
        /// </remarks>
        private static MaterializedCatalog Materialized =>
            LazyInitializer.EnsureInitialized(ref snapshot, ref gate, MaterializeTimed);

        /// <summary>
        /// Materializes the process-wide catalog now, so a later first use finds it already built.
        /// </summary>
        /// <remarks>
        /// <para><b>The mechanism, not the door.</b> A host asks for this through
        /// <see cref="ProjectAppService.Warmup"/>, which is the facade's single "get ready" entry and the
        /// only one a frontend should know about; this stays internal so warming the SDK never requires naming
        /// an engine type. Started on a background thread at launch, the component factories run while the
        /// caller gets on with something else, and the first real catalog query returns a built value instead of
        /// paying for one.</para>
        /// <para>What it costs a caller — it BLOCKS for the whole materialization, so a UI thread must not call
        /// it — is the door's contract and is stated there, once. Do not restate it here: the two copies drifting
        /// is not hypothetical, it is what left the comment below describing a parenting this change had
        /// replaced.</para>
        /// <para><b>It changes no outcome</b>, and that part IS local: the catalog's content is code-authored and
        /// deterministic, so this only moves WHEN the one materialization happens, and a call that faults leaves
        /// the catalog unbuilt for the real first use to build and report. What a warm-up does NOT make
        /// retryable is a fault below this type's own gate — see the forkability rule in <c>ARCHITECTURE.md</c>,
        /// which is where that limit is stated once.</para>
        /// </remarks>
        internal static void Warmup() => _ = Materialized;

        /// <summary>
        /// Materializes the catalog and records how long it took. One caller pays for the whole component set —
        /// a host's <see cref="Warmup"/> where there is one, and otherwise whatever operation touched the
        /// catalog first — and without this the cost is attributed to that caller rather than to the catalog.
        /// <para>Why the instrument is a histogram rather than a once-only counter is stated where the
        /// instrument is declared - <c>SdkTelemetryRegistry.CatalogMaterializationDuration</c>.</para>
        /// </summary>
        private static MaterializedCatalog MaterializeTimed() =>
            // A SPAN as well as the histogram, and through the one seam that records both. The histogram says
            // how long materialization cost; only the span says WHERE it was paid, and the two answers differ by
            // host: warmed from a launch there is no ambient operation, so the span is a trace ROOT, while an
            // unwarmed host pays inside whichever operation touched the catalog first — which without a span of
            // its own simply reads slow, once per process, for no visible reason. The core also measures it on
            // the same monotonic clock the hand-rolled Stopwatch here used, so the recorded number is unchanged.
            Telemetry.Run("Materialize", _ => new BuiltInCatalog().Materialize(), MaterializationMetrics);

        /// <summary>The catalog's entry point into the instrumentation core.</summary>
        private static readonly OperationTelemetry Telemetry =
            new OperationTelemetry(SdkTelemetryRegistry.Surface, nameof(BuiltInCatalog));

        /// <summary>The binding is IMMUTABLE and its instrument is static, so it is built once rather than per operation.</summary>
        private static readonly MetricBinding MaterializationMetrics =
            MetricBinding.For(SdkTelemetryRegistry.CatalogMaterializationDuration);

        // File→New templates, reassigned by AuthorTemplates() (Phase C) before Materialize() reads them. The empty
        // defaults keep the catalog materializable while Phase C is outstanding; the Phase C byte tests are what
        // actually exercise their correctness.
        private ProjectElement newProjectSkeleton = EmptyElement("project");
        private ProjectElement builtInEnumerators = EmptyElement("enum_definitions");
        private FunctionBlockDefinition emptyFunctionBlockTemplate = EmptyBlockTemplate();

        private MaterializedCatalog Materialize()
        {
            var products = ImmutableArray.CreateBuilder<ProductDefinition>();
            var functionBlocks = ImmutableArray.CreateBuilder<FunctionBlockDefinition>();
            RegisterProducts(products);
            RegisterFunctionBlocks(functionBlocks);
            AuthorTemplates();
            return new MaterializedCatalog(products.ToImmutable(), functionBlocks.ToImmutable(),
                newProjectSkeleton, builtInEnumerators, emptyFunctionBlockTemplate);
        }

        /// <summary>Generated product factories (Phase B) append here — the plan's <c>AllProducts()</c> hook.</summary>
        static partial void RegisterProducts(ImmutableArray<ProductDefinition>.Builder products);

        /// <summary>Generated function-block factories (FB plan) append here — the plan's <c>AllFunctionBlocks()</c> hook.</summary>
        static partial void RegisterFunctionBlocks(ImmutableArray<FunctionBlockDefinition>.Builder functionBlocks);

        /// <summary>Phase C assigns <c>newProjectSkeleton</c>, <c>builtInEnumerators</c>, <c>emptyFunctionBlockTemplate</c>.</summary>
        partial void AuthorTemplates();

        private static ProjectElement EmptyElement(string tag) =>
            ProjectElement.Create(tag, null, Array.Empty<(string, string)>(), Array.Empty<ProjectElement>());

        private static FunctionBlockDefinition EmptyBlockTemplate() =>
            new FunctionBlockDefinition(string.Empty, string.Empty, "Tom blok", "Tom blok", string.Empty,
                EmptyElement("functionblock"))
            {
                IsEmptyTemplate = true,
            };

        /// <inheritdoc/>
        public ProductDefinition Product(string productIdentifier) => Materialized.Product(productIdentifier);

        /// <inheritdoc/>
        public FunctionBlockDefinition FunctionBlock(string masterType) => Materialized.FunctionBlock(masterType);

        /// <inheritdoc/>
        public FunctionBlockDefinition FunctionBlockByName(string name) => Materialized.FunctionBlockByName(name);

        /// <inheritdoc/>
        public IReadOnlyList<ProductDefinition> Products => Materialized.Products;

        /// <inheritdoc/>
        public IReadOnlyList<FunctionBlockDefinition> FunctionBlocks => Materialized.FunctionBlocks;

        /// <inheritdoc/>
        public ProjectElement NewProjectSkeleton => Materialized.NewProjectSkeleton;

        /// <inheritdoc/>
        public ProjectElement BuiltInEnumerators => Materialized.BuiltInEnumerators;

        /// <inheritdoc/>
        public FunctionBlockDefinition EmptyFunctionBlockTemplate => Materialized.EmptyFunctionBlockTemplate;
    }
}
