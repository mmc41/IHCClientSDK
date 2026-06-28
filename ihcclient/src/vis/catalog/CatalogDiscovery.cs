#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ihc.Projects
{
    /// <summary>
    /// The catalog of products and function blocks available for insertion, plus the File→New templates. Modelled
    /// as an interface so the editor/service can be driven by a fake catalog in tests (matching the SDK's
    /// interface-injection convention) without a real IHC Visual install on disk. <see cref="CatalogDiscovery"/>
    /// is the install-dir-backed implementation.
    /// </summary>
    public interface ICatalog
    {
        /// <summary>Looks a product up by its opaque <c>product_identifier</c> token (e.g. <c>_0x2101</c>).</summary>
        ProductDescriptor Product(string productIdentifier);

        /// <summary>Looks a function block up by its <c>master_type</c> key (e.g. <c>1.1.01</c>).</summary>
        FunctionBlockDescriptor FunctionBlock(string masterType);

        /// <summary>All discovered products.</summary>
        IReadOnlyList<ProductDescriptor> Products { get; }

        /// <summary>All discovered function blocks.</summary>
        IReadOnlyList<FunctionBlockDescriptor> FunctionBlocks { get; }

        /// <summary>
        /// The parsed <c>Data\NewDoc.idf</c> File→New skeleton (legacy v1 format, DTD defaults applied) — the
        /// source of the ten default rooms and the fixed template ids used by <see cref="ProjectAppService.CreateNew"/>.
        /// </summary>
        ProjectElement NewProjectSkeleton { get; }

        /// <summary>
        /// The parsed <c>Data\EnumeratorDefinitions.def</c> — the two built-in enums (Persienne tilstand, Logning)
        /// seeded into every new project, matched by <c>typeid</c>.
        /// </summary>
        ProjectElement BuiltInEnumerators { get; }
    }

    /// <summary>
    /// Auto-discovers the products and function blocks installed with IHC Visual by scanning
    /// <c>Products\**\*.def</c> (~100) and <c>FunctionBlocks\**\*.ifb</c> (~73) under the configured install dir,
    /// and loads the <c>Data\</c> File→New templates. These catalog files are the source of truth for instance
    /// specifics; a <c>.vis</c> is fully self-sufficient once a component has been inserted (spec ch. 09).
    /// </summary>
    public sealed class CatalogDiscovery : ICatalog
    {
        private static readonly Regex MenuPrefix = new(@"^\d+#", RegexOptions.Compiled);

        private readonly ImmutableArray<ProductDescriptor> products;
        private readonly ImmutableArray<FunctionBlockDescriptor> functionBlocks;
        private readonly FrozenDictionaryLike<ProductDescriptor> productsByIdentifier;
        private readonly FrozenDictionaryLike<FunctionBlockDescriptor> functionBlocksByType;

        private CatalogDiscovery(
            ImmutableArray<ProductDescriptor> products,
            ImmutableArray<FunctionBlockDescriptor> functionBlocks,
            ProjectElement newProjectSkeleton,
            ProjectElement builtInEnumerators)
        {
            this.products = products;
            this.functionBlocks = functionBlocks;
            NewProjectSkeleton = newProjectSkeleton;
            BuiltInEnumerators = builtInEnumerators;
            productsByIdentifier = new FrozenDictionaryLike<ProductDescriptor>(
                products, p => p.ProductIdentifier);
            functionBlocksByType = new FrozenDictionaryLike<FunctionBlockDescriptor>(
                functionBlocks, f => f.MasterType);
        }

        /// <summary>Builds a catalog by scanning the given IHC Visual install directory (results are cached).</summary>
        public static CatalogDiscovery FromInstallDir(string installDir)
        {
            if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            {
                throw new DirectoryNotFoundException(
                    $"The configured IHC Visual install dir '{installDir}' does not exist; set " +
                    $"{nameof(IhcSettings)}.{nameof(IhcSettings.IhcVisualInstallDir)} to a real installation.");
            }

            string productsDir = Path.Combine(installDir, "Products");
            string functionBlocksDir = Path.Combine(installDir, "FunctionBlocks");
            string dataDir = Path.Combine(installDir, "Data");

            ImmutableArray<ProductDescriptor> products = DiscoverProducts(productsDir);
            ImmutableArray<FunctionBlockDescriptor> functionBlocks = DiscoverFunctionBlocks(functionBlocksDir);
            ProjectElement skeleton = CatalogReader.ReadFile(Path.Combine(dataDir, "NewDoc.idf"));
            ProjectElement enums = CatalogReader.ReadFile(Path.Combine(dataDir, "EnumeratorDefinitions.def"));
            return new CatalogDiscovery(products, functionBlocks, skeleton, enums);
        }

        private static ImmutableArray<ProductDescriptor> DiscoverProducts(string productsDir)
        {
            var builder = ImmutableArray.CreateBuilder<ProductDescriptor>();
            foreach (string path in EnumerateFilesSorted(productsDir, "*.def"))
            {
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                ProjectElement body = CatalogReader.Read(ms);
                string identifier = body.GetAttribute("product_identifier") ?? string.Empty;
                string displayName = StripMenuPrefix(body.GetAttribute("name") ?? string.Empty);
                builder.Add(new ProductDescriptor(identifier, displayName, RelativeDir(productsDir, path), body)
                {
                    InlineDtdBlocks = InlineDtd.Capture(bytes),
                });
            }
            return builder.ToImmutable();
        }

        private static ImmutableArray<FunctionBlockDescriptor> DiscoverFunctionBlocks(string functionBlocksDir)
        {
            var builder = ImmutableArray.CreateBuilder<FunctionBlockDescriptor>();
            foreach (string path in EnumerateFilesSorted(functionBlocksDir, "*.ifb"))
            {
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                ProjectElement body = CatalogReader.Read(ms);
                string masterType = body.GetAttribute("master_type") ?? string.Empty;
                string masterVersion = body.GetAttribute("master_version") ?? string.Empty;
                string masterName = body.GetAttribute("master_name") ?? string.Empty;
                string displayName = body.GetAttribute("name") ?? masterName;
                builder.Add(new FunctionBlockDescriptor(
                    masterType, masterVersion, masterName, displayName, RelativeDir(functionBlocksDir, path), body)
                {
                    InlineDtdBlocks = InlineDtd.Capture(bytes),
                });
            }
            return builder.ToImmutable();
        }

        private static IEnumerable<string> EnumerateFilesSorted(string root, string pattern) =>
            Directory.Exists(root)
                ? Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        private static string RelativeDir(string root, string filePath) =>
            Path.GetDirectoryName(Path.GetRelativePath(root, filePath)) ?? string.Empty;

        private static string StripMenuPrefix(string name) => MenuPrefix.Replace(name, string.Empty);

        /// <inheritdoc/>
        public ProductDescriptor Product(string productIdentifier) =>
            productsByIdentifier.Get(productIdentifier)
            ?? throw new KeyNotFoundException($"No product with product_identifier '{productIdentifier}' in the catalog.");

        /// <inheritdoc/>
        public FunctionBlockDescriptor FunctionBlock(string masterType) =>
            functionBlocksByType.Get(masterType)
            ?? throw new KeyNotFoundException($"No function block with master_type '{masterType}' in the catalog.");

        /// <inheritdoc/>
        public IReadOnlyList<ProductDescriptor> Products => products;

        /// <inheritdoc/>
        public IReadOnlyList<FunctionBlockDescriptor> FunctionBlocks => functionBlocks;

        /// <inheritdoc/>
        public ProjectElement NewProjectSkeleton { get; }

        /// <inheritdoc/>
        public ProjectElement BuiltInEnumerators { get; }

        /// <summary>
        /// A tiny last-wins lookup over a descriptor list (catalog keys are not globally unique — favorites
        /// duplicate function blocks, and a few product_identifiers repeat across root element types, §9.3.3).
        /// </summary>
        private sealed class FrozenDictionaryLike<T>
        {
            private readonly Dictionary<string, T> map;

            public FrozenDictionaryLike(ImmutableArray<T> items, Func<T, string> keySelector)
            {
                map = new Dictionary<string, T>(StringComparer.Ordinal);
                foreach (T item in items)
                {
                    map[keySelector(item)] = item;
                }
            }

            public T? Get(string key) => map.TryGetValue(key, out T? value) ? value : default;
        }
    }
}
