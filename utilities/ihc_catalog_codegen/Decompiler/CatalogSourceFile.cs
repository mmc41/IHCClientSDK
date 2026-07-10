#nullable enable
using System.Collections.Immutable;
using System.IO;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// A single catalog product file loaded the same way <c>CatalogDiscovery</c> loads it: the parsed
    /// <see cref="ProductDefinition"/> (identity + DTD-materialized <c>Body</c>) plus the file's captured inline-DTD
    /// <see cref="Blocks"/> and its raw <see cref="FileBytes"/>. The <see cref="Definition"/> is the exact component the
    /// install-dir path yields, so it is the authoritative target the decompiled recipe must reproduce; the
    /// <see cref="Blocks"/> are the grammar the self-verify normalizes both sides against; the <see cref="FileBytes"/>
    /// are what the byte-fidelity gate compares the re-emitted definition to.
    /// </summary>
    internal sealed record ProductSource(ProductDefinition Definition, ImmutableDictionary<string, string> Blocks,
        byte[] FileBytes);

    /// <summary>
    /// A single function-block catalog file (<c>FunctionBlocks\*.ifb</c>) loaded as <c>CatalogDiscovery</c> loads it:
    /// the parsed <see cref="FunctionBlockDefinition"/> plus its captured inline-DTD <see cref="Blocks"/> and its raw
    /// <see cref="FileBytes"/> (the byte-fidelity gate's expected side). Documentation is attached separately (from the
    /// sibling <c>syn_en*.md</c>), so it does not ride on <see cref="Definition"/> here.
    /// </summary>
    internal sealed record FunctionBlockSource(FunctionBlockDefinition Definition,
        ImmutableDictionary<string, string> Blocks, byte[] FileBytes);

    /// <summary>Reads a <c>Products\*.def</c> or <c>FunctionBlocks\*.ifb</c> into its source record, reproducing
    /// <c>CatalogDiscovery</c>'s per-file identity derivation (menu-prefix-stripped product display name / master
    /// identity, captured inline DTD).</summary>
    internal static class CatalogSourceFile
    {
        public static ProductSource ReadProduct(string path, string categoryPath)
        {
            byte[] bytes = File.ReadAllBytes(path);
            ImmutableDictionary<string, string> blocks = InlineDtd.Capture(bytes);
            // Build via CatalogReader so the definition carries the captured verbatim Head + source encoding the
            // generator bakes (matching CatalogDiscovery / the FB path).
            ProductDefinition definition = CatalogReader.BuildProduct(bytes, categoryPath, documentation: null);
            return new ProductSource(definition, blocks, bytes);
        }

        public static FunctionBlockSource ReadFunctionBlock(string path, string categoryPath)
        {
            byte[] bytes = File.ReadAllBytes(path);
            ImmutableDictionary<string, string> blocks = InlineDtd.Capture(bytes);
            FunctionBlockDefinition definition = CatalogReader.BuildFunctionBlock(bytes, categoryPath, documentation: null);
            return new FunctionBlockSource(definition, blocks, bytes);
        }
    }
}
