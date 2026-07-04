#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Io;
namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Auto-discovers the products and function blocks installed with IHC Visual by scanning
    /// <c>Products\**\*.def</c> (~100) and <c>FunctionBlocks\**\*.ifb</c> (~73) under the configured install dir,
    /// and loads the <c>Data\</c> File→New templates. These catalog files are the source of truth for instance
    /// specifics; a <c>.vis</c> is fully self-sufficient once a component has been inserted (spec ch. 09).
    /// </summary>
    public sealed class CatalogDiscovery : ICatalog
    {
        private readonly ImmutableArray<ProductDefinition> products;
        private readonly ImmutableArray<FunctionBlockDefinition> functionBlocks;
        private readonly FrozenDictionaryLike<ProductDefinition> productsByIdentifier;
        private readonly FrozenDictionaryLike<FunctionBlockDefinition> functionBlocksByType;
        private readonly FrozenDictionaryLike<FunctionBlockDefinition> functionBlocksByName;

        private CatalogDiscovery(
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
            productsByIdentifier = new FrozenDictionaryLike<ProductDefinition>(
                products, p => p.ProductIdentifier);
            functionBlocksByType = new FrozenDictionaryLike<FunctionBlockDefinition>(
                functionBlocks, f => f.MasterType);
            functionBlocksByName = new FrozenDictionaryLike<FunctionBlockDefinition>(
                functionBlocks, f => f.DisplayName);
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
            foreach ((string dir, string name) in new[] { (productsDir, "Products"), (functionBlocksDir, "FunctionBlocks"), (dataDir, "Data") })
            {
                if (!Directory.Exists(dir))
                {
                    throw new DirectoryNotFoundException(
                        $"IHC Visual install dir '{installDir}' has no '{name}' subdirectory ('{dir}'); the " +
                        "configured path does not point at a complete installation. A silently empty catalog " +
                        "would misreport every product as unsupported.");
                }
            }

            ImmutableArray<ProductDefinition> products = DiscoverProducts(productsDir);
            ImmutableArray<FunctionBlockDefinition> functionBlocks = DiscoverFunctionBlocks(functionBlocksDir);
            ProjectElement skeleton = ReadCatalogFile(Path.Combine(dataDir, "NewDoc.idf"));
            ProjectElement enums = ReadCatalogFile(Path.Combine(dataDir, "EnumeratorDefinitions.def"));
            FunctionBlockDefinition emptyTemplate = ReadEmptyFunctionBlockTemplate(Path.Combine(dataDir, "fb.def"));
            return new CatalogDiscovery(products, functionBlocks, skeleton, enums, emptyTemplate);
        }

        // One malformed vendor file must abort discovery with the offending PATH in the message — the raw
        // XmlException/IOException names neither the file nor that a catalog scan was in progress.
        private static ProjectElement ReadCatalogFile(string path)
        {
            try
            {
                return CatalogReader.ReadFile(path);
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
            {
                throw new InvalidDataException($"Failed to parse IHC Visual catalog file '{path}': {ex.Message}", ex);
            }
        }

        private static ProjectElement ReadCatalogFile(string path, byte[] bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                return CatalogReader.Read(ms);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new InvalidDataException($"Failed to parse IHC Visual catalog file '{path}': {ex.Message}", ex);
            }
        }

        private static FunctionBlockDefinition ReadEmptyFunctionBlockTemplate(string fbDefPath)
        {
            byte[] bytes = File.ReadAllBytes(fbDefPath);
            ProjectElement body = ReadCatalogFile(fbDefPath, bytes);
            string name = MenuPrefix.Strip(body.GetAttribute("name") ?? "Tom blok");
            return new FunctionBlockDefinition(string.Empty, string.Empty, name, name, string.Empty, body)
            {
                InlineDtdBlocks = InlineDtd.Capture(bytes),
                IsEmptyTemplate = true,
            };
        }

        private static ImmutableArray<ProductDefinition> DiscoverProducts(string productsDir)
        {
            var builder = ImmutableArray.CreateBuilder<ProductDefinition>();
            foreach (string path in EnumerateFilesSorted(productsDir, "*.def"))
            {
                byte[] bytes = File.ReadAllBytes(path);
                ProjectElement body = ReadCatalogFile(path, bytes);
                string identifier = body.GetAttribute("product_identifier") ?? string.Empty;
                string displayName = MenuPrefix.Strip(body.GetAttribute("name") ?? string.Empty);
                builder.Add(new ProductDefinition(identifier, displayName, RelativeDir(productsDir, path), body)
                {
                    InlineDtdBlocks = InlineDtd.Capture(bytes),
                });
            }
            return builder.ToImmutable();
        }

        private static ImmutableArray<FunctionBlockDefinition> DiscoverFunctionBlocks(string functionBlocksDir)
        {
            var builder = ImmutableArray.CreateBuilder<FunctionBlockDefinition>();
            foreach (string path in EnumerateFilesSorted(functionBlocksDir, "*.ifb"))
            {
                byte[] bytes = File.ReadAllBytes(path);
                ProjectElement body = ReadCatalogFile(path, bytes);
                string masterType = body.GetAttribute("master_type") ?? string.Empty;
                string masterVersion = body.GetAttribute("master_version") ?? string.Empty;
                string masterName = body.GetAttribute("master_name") ?? string.Empty;
                string displayName = body.GetAttribute("name") ?? masterName;
                builder.Add(new FunctionBlockDefinition(
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
                    string key = keySelector(item);
                    if (key.Length == 0)
                    {
                        continue;   // keyless descriptors (user-saved blocks without master_type) are not addressable here
                    }
                    map[key] = item;
                }
            }

            public T? Get(string key) => map.TryGetValue(key, out T? value) ? value : default;
        }
    }
}
