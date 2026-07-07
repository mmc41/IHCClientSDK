#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The in-memory, <b>source-agnostic</b> <see cref="ICatalog"/>: it holds already-materialized product and
    /// function-block definitions plus the three File→New templates, and answers every lookup. It does not know how
    /// its contents were produced — <see cref="CatalogDiscovery"/> materializes one by scanning an IHC Visual install,
    /// and <c>BuiltInCatalog</c> materializes one from code-authored builder invocations. Extracting this shared core
    /// keeps the lookup semantics (below) in a single place so both sources behave identically.
    /// </summary>
    /// <remarks>
    /// Catalog keys are <b>not globally unique</b>: favorites duplicate function blocks, and a few
    /// <c>product_identifier</c>s repeat across root element types (spec §9.3.3). Lookups are therefore
    /// <b>last-wins</b> over the supplied document order — the last definition with a given key shadows earlier ones,
    /// while <see cref="Products"/>/<see cref="FunctionBlocks"/> retain every definition (duplicates included).
    /// Keyless definitions (user-saved blocks without a <c>master_type</c>) are not addressable by that key but are
    /// still listed and, for function blocks, reachable via <see cref="FunctionBlockByName"/>.
    /// </remarks>
    public sealed class MaterializedCatalog : ICatalog
    {
        private readonly ImmutableArray<ProductDefinition> products;
        private readonly ImmutableArray<FunctionBlockDefinition> functionBlocks;
        private readonly LastWinsIndex<ProductDefinition> productsByIdentifier;
        private readonly LastWinsIndex<FunctionBlockDefinition> functionBlocksByType;
        private readonly LastWinsIndex<FunctionBlockDefinition> functionBlocksByName;

        public MaterializedCatalog(
            ImmutableArray<ProductDefinition> products,
            ImmutableArray<FunctionBlockDefinition> functionBlocks,
            ProjectElement newProjectSkeleton,
            ProjectElement builtInEnumerators,
            FunctionBlockDefinition emptyFunctionBlockTemplate)
        {
            this.products = products;
            this.functionBlocks = functionBlocks;
            NewProjectSkeleton = newProjectSkeleton;
            BuiltInEnumerators = builtInEnumerators;
            EmptyFunctionBlockTemplate = emptyFunctionBlockTemplate;
            productsByIdentifier = new LastWinsIndex<ProductDefinition>(products, p => p.ProductIdentifier);
            functionBlocksByType = new LastWinsIndex<FunctionBlockDefinition>(functionBlocks, f => f.MasterType);
            functionBlocksByName = new LastWinsIndex<FunctionBlockDefinition>(functionBlocks, f => f.DisplayName);
        }

        /// <inheritdoc/>
        public ProductDefinition Product(string productIdentifier) =>
            productsByIdentifier.Get(productIdentifier)
            ?? throw new KeyNotFoundException($"No product with product_identifier '{productIdentifier}' in the catalog.");

        /// <inheritdoc/>
        public FunctionBlockDefinition FunctionBlock(string masterType) =>
            functionBlocksByType.Get(masterType)
            ?? throw new KeyNotFoundException($"No function block with master_type '{masterType}' in the catalog.");

        /// <inheritdoc/>
        public FunctionBlockDefinition FunctionBlockByName(string name) =>
            functionBlocksByName.Get(name)
            ?? throw new KeyNotFoundException($"No function block named '{name}' in the catalog.");

        /// <inheritdoc/>
        public IReadOnlyList<ProductDefinition> Products => products;

        /// <inheritdoc/>
        public IReadOnlyList<FunctionBlockDefinition> FunctionBlocks => functionBlocks;

        /// <inheritdoc/>
        public ProjectElement NewProjectSkeleton { get; }

        /// <inheritdoc/>
        public ProjectElement BuiltInEnumerators { get; }

        /// <inheritdoc/>
        public FunctionBlockDefinition EmptyFunctionBlockTemplate { get; }

        /// <summary>
        /// A tiny last-wins lookup over a definition list (catalog keys are not globally unique — favorites
        /// duplicate function blocks, and a few product_identifiers repeat across root element types, §9.3.3).
        /// </summary>
        private sealed class LastWinsIndex<T>
        {
            private readonly Dictionary<string, T> map;

            public LastWinsIndex(ImmutableArray<T> items, Func<T, string> keySelector)
            {
                map = new Dictionary<string, T>(StringComparer.Ordinal);
                foreach (T item in items)
                {
                    string key = keySelector(item);
                    if (key.Length == 0)
                    {
                        continue;   // keyless definitions (user-saved blocks without master_type) are not addressable here
                    }
                    map[key] = item;
                }
            }

            public T? Get(string key) => map.TryGetValue(key, out T? value) ? value : default;
        }
    }
}
