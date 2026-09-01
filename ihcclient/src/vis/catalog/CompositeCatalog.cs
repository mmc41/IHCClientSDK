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
    /// An <see cref="ICatalog"/> that layers runtime-imported components on top of a base catalog (typically a
    /// <see cref="BuiltInCatalog"/>): the base supplies the ~173 stock products/function blocks and the File→New
    /// templates, and callers <see cref="Import(ProductDefinition)"/> extra components (e.g. a <c>.def</c>/<c>.ifb</c>
    /// read via <see cref="CatalogReader.ReadProduct(string, HelpDocument?)"/>) at runtime without touching the
    /// embedded catalog. This is the seam the app-service uses so a user-supplied component resolves and inserts
    /// alongside the built-ins.
    /// </summary>
    /// <remarks>
    /// <para><b>Imported-wins.</b> An imported component with the same key (<c>product_identifier</c> / <c>master_type</c>
    /// / display name) as a base one shadows it. This falls out of <see cref="MaterializedCatalog"/>'s last-wins rule
    /// (§9.3.3): the composed view lists base components first, then imports, so the later import wins the lookup while
    /// both remain in <see cref="Products"/>/<see cref="FunctionBlocks"/> (append-only — imports are never removed).</para>
    /// <para><b>Deferred + concurrent-read safe.</b> The composed <see cref="MaterializedCatalog"/> is built lazily and
    /// cached in an immutable snapshot; reads take a lock-free volatile read of that reference. An
    /// <see cref="Import(ProductDefinition)"/> appends under a lock and invalidates the snapshot, which the next read
    /// recomposes once. Until the first read or import, the base is not touched at all — so wrapping a
    /// lazily-materializing <see cref="BuiltInCatalog"/> does not force it to materialize.</para>
    /// </remarks>
    public sealed class CompositeCatalog : ICatalog
    {
        private readonly ICatalog @base;
        private readonly object gate = new();
        private ImmutableArray<ProductDefinition> importedProducts = ImmutableArray<ProductDefinition>.Empty;
        private ImmutableArray<FunctionBlockDefinition> importedFunctionBlocks = ImmutableArray<FunctionBlockDefinition>.Empty;
        // The composed base+overlays view; null means "not built yet or invalidated by an import" — recomposed on the
        // next read. Published under `gate`; read lock-free via Volatile.Read on the hot lookup path.
        private MaterializedCatalog? snapshot;

        public CompositeCatalog(ICatalog @base)
        {
            ArgumentNullException.ThrowIfNull(@base);
            this.@base = @base;
        }

        /// <summary>Adds an imported product; it shadows a base product with the same <c>product_identifier</c>
        /// (imported-wins) and appears in <see cref="Products"/>.</summary>
        public void Import(ProductDefinition product)
        {
            ArgumentNullException.ThrowIfNull(product);
            lock (gate)
            {
                importedProducts = importedProducts.Add(product);
                snapshot = null;
            }
        }

        /// <summary>Adds an imported function block; it shadows a base block with the same <c>master_type</c>/name
        /// (imported-wins) and appears in <see cref="FunctionBlocks"/>.</summary>
        public void Import(FunctionBlockDefinition functionBlock)
        {
            ArgumentNullException.ThrowIfNull(functionBlock);
            lock (gate)
            {
                importedFunctionBlocks = importedFunctionBlocks.Add(functionBlock);
                snapshot = null;
            }
        }

        private MaterializedCatalog Current()
        {
            MaterializedCatalog? current = Volatile.Read(ref snapshot);
            if (current is not null)
            {
                return current;
            }
            lock (gate)
            {
                return snapshot ??= Compose();
            }
        }

        // Reuses MaterializedCatalog rather than duplicating last-wins/keyless/by-name semantics: base components first,
        // imports last → last-wins gives imported-wins, and templates ride the base unchanged (imports never add them).
        private MaterializedCatalog Compose()
        {
            var products = ImmutableArray.CreateBuilder<ProductDefinition>(@base.Products.Count + importedProducts.Length);
            products.AddRange(@base.Products);
            products.AddRange(importedProducts);
            var functionBlocks = ImmutableArray.CreateBuilder<FunctionBlockDefinition>(@base.FunctionBlocks.Count + importedFunctionBlocks.Length);
            functionBlocks.AddRange(@base.FunctionBlocks);
            functionBlocks.AddRange(importedFunctionBlocks);
            return new MaterializedCatalog(products.ToImmutable(), functionBlocks.ToImmutable(),
                @base.NewProjectSkeleton, @base.BuiltInEnumerators, @base.EmptyFunctionBlockTemplate);
        }

        /// <inheritdoc/>
        public ProductDefinition Product(string productIdentifier) => Current().Product(productIdentifier);

        /// <inheritdoc/>
        public FunctionBlockDefinition FunctionBlock(string masterType) => Current().FunctionBlock(masterType);

        /// <inheritdoc/>
        public FunctionBlockDefinition FunctionBlockByName(string name) => Current().FunctionBlockByName(name);

        /// <inheritdoc/>
        public IReadOnlyList<ProductDefinition> Products => Current().Products;

        /// <inheritdoc/>
        public IReadOnlyList<FunctionBlockDefinition> FunctionBlocks => Current().FunctionBlocks;

        /// <inheritdoc/>
        public ProjectElement NewProjectSkeleton => Current().NewProjectSkeleton;

        /// <inheritdoc/>
        public ProjectElement BuiltInEnumerators => Current().BuiltInEnumerators;

        /// <inheritdoc/>
        public FunctionBlockDefinition EmptyFunctionBlockTemplate => Current().EmptyFunctionBlockTemplate;
    }
}
