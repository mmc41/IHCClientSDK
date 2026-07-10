#nullable enable
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
        // One process-wide materialization: the factory instance below exists only as the build context the partial
        // registration hooks write into; every BuiltInCatalog instance forwards to the same immutable value.
        private static readonly Lazy<MaterializedCatalog> materialized =
            new(() => new BuiltInCatalog().Materialize(), LazyThreadSafetyMode.ExecutionAndPublication);

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
        partial void RegisterProducts(ImmutableArray<ProductDefinition>.Builder products);

        /// <summary>Generated function-block factories (FB plan) append here — the plan's <c>AllFunctionBlocks()</c> hook.</summary>
        partial void RegisterFunctionBlocks(ImmutableArray<FunctionBlockDefinition>.Builder functionBlocks);

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
        public ProductDefinition Product(string productIdentifier) => materialized.Value.Product(productIdentifier);

        /// <inheritdoc/>
        public FunctionBlockDefinition FunctionBlock(string masterType) => materialized.Value.FunctionBlock(masterType);

        /// <inheritdoc/>
        public FunctionBlockDefinition FunctionBlockByName(string name) => materialized.Value.FunctionBlockByName(name);

        /// <inheritdoc/>
        public IReadOnlyList<ProductDefinition> Products => materialized.Value.Products;

        /// <inheritdoc/>
        public IReadOnlyList<FunctionBlockDefinition> FunctionBlocks => materialized.Value.FunctionBlocks;

        /// <inheritdoc/>
        public ProjectElement NewProjectSkeleton => materialized.Value.NewProjectSkeleton;

        /// <inheritdoc/>
        public ProjectElement BuiltInEnumerators => materialized.Value.BuiltInEnumerators;

        /// <inheritdoc/>
        public FunctionBlockDefinition EmptyFunctionBlockTemplate => materialized.Value.EmptyFunctionBlockTemplate;
    }
}
