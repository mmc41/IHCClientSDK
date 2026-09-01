using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Materializes an <see cref="ICatalog"/> by auto-discovering the products and function blocks installed with
    /// IHC Visual — scanning <c>Products\**\*.def</c> (~100) and <c>FunctionBlocks\**\*.ifb</c> (~73) under the
    /// configured install dir — and loading the <c>Data\</c> File→New templates. These catalog files are the source
    /// of truth for instance specifics; a <c>.vis</c> is fully self-sufficient once a component has been inserted
    /// (spec ch. 09). The scan produces a <see cref="MaterializedCatalog"/>, the source-agnostic in-memory catalog
    /// whose lookup semantics it shares with the SDK-embedded <c>BuiltInCatalog</c>.
    /// </summary>
    public static class CatalogDiscovery
    {
        /// <summary>Builds a catalog by scanning the given IHC Visual install directory (parsed eagerly).</summary>
        public static MaterializedCatalog FromInstallDir(string installDir)
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
            return new MaterializedCatalog(products, functionBlocks, skeleton, enums, emptyTemplate);
        }

        // A File→New template (NewDoc.idf / EnumeratorDefinitions.def / fb.def) is used to BUILD a project, not
        // re-emitted as a catalog file, so — unlike a product/function-block descriptor, whose raw body the writer must
        // reproduce — it carries the EFFECTIVE body: the file's own DTD ATTLIST defaults materialized. Those defaults
        // become real values the saved project keeps (e.g. a group's icon default the .vis registry defaults
        // differently), so materializing here reproduces what a fresh project contains and matches the hand-authored
        // BuiltInCatalog templates.
        private static ProjectElement ReadCatalogFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            ProjectElement raw = CatalogReader.ParseCatalogFile(path, () => CatalogReader.Read(bytes));
            return CatalogDefaults.Materialize(raw,
                ProjectSchemaView.For(CatalogDtdParser.ParseLenient(CatalogDtdParser.CaptureHeadText(bytes))));
        }

        private static FunctionBlockDefinition ReadEmptyFunctionBlockTemplate(string fbDefPath)
        {
            byte[] bytes = File.ReadAllBytes(fbDefPath);
            CatalogGrammar grammar = CatalogDtdParser.ParseLenient(CatalogDtdParser.CaptureHeadText(bytes));
            ProjectElement raw = CatalogReader.ParseCatalogFile(fbDefPath, () => CatalogReader.Read(bytes));
            ProjectElement body = CatalogDefaults.Materialize(raw, ProjectSchemaView.For(grammar));
            string name = MenuPrefix.Strip(body.GetAttribute("name") ?? "Tom blok");
            return new FunctionBlockDefinition(string.Empty, string.Empty, name, name, string.Empty, body)
            {
                Grammar = grammar,
                IsEmptyTemplate = true,
            };
        }

        private static ImmutableArray<ProductDefinition> DiscoverProducts(string productsDir)
        {
            var builder = ImmutableArray.CreateBuilder<ProductDefinition>();
            foreach (string path in EnumerateFilesSorted(productsDir, "*.def"))
            {
                byte[] bytes = File.ReadAllBytes(path);
                builder.Add(CatalogReader.ParseCatalogFile(path,
                    () => CatalogReader.BuildProduct(bytes, CategoryPathFor(productsDir, path), documentation: null)));
            }
            return builder.ToImmutable();
        }

        private static ImmutableArray<FunctionBlockDefinition> DiscoverFunctionBlocks(string functionBlocksDir)
        {
            var builder = ImmutableArray.CreateBuilder<FunctionBlockDefinition>();
            foreach (string path in EnumerateFilesSorted(functionBlocksDir, "*.ifb"))
            {
                byte[] bytes = File.ReadAllBytes(path);
                builder.Add(CatalogReader.ParseCatalogFile(path,
                    () => CatalogReader.BuildFunctionBlock(bytes, CategoryPathFor(functionBlocksDir, path), documentation: null)));
            }
            return builder.ToImmutable();
        }

        private static IEnumerable<string> EnumerateFilesSorted(string root, string pattern) =>
            Directory.Exists(root)
                ? Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        /// <summary>
        /// The catalog-tree location of a scanned file, as a <c>CategoryPath</c>.
        /// <para>
        /// <c>CategoryPath</c> is a catalog DATA convention and always <c>\</c>-separated — the built-in catalog
        /// embeds it literally (<c>"01. Lysstyring\1.1 Generelt"</c>) and every consumer splits on <c>\</c>. It is
        /// therefore NOT a host path, which is the whole reason this is a named method: <see cref="Path.GetDirectoryName(string)"/>
        /// returns the HOST separator, so on Linux/macOS this produced <c>/</c>-separated values and a scanned
        /// catalog's nested categories collapsed into a single flat folder name. The scan is the boundary where a
        /// host path stops and catalog data begins, so the normalization belongs exactly here.
        /// </para>
        /// </summary>
        internal static string CategoryPathFor(string root, string filePath) =>
            (Path.GetDirectoryName(Path.GetRelativePath(root, filePath)) ?? string.Empty)
                .Replace('/', CategorySeparator);

        /// <summary>The separator every <c>CategoryPath</c> uses, on every OS.</summary>
        internal const char CategorySeparator = '\\';
    }
}
