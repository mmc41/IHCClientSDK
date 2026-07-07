#nullable enable
using System.Collections.Immutable;
using System.IO;

using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// A single catalog product file loaded the same way <c>CatalogDiscovery</c> loads it: the parsed
    /// <see cref="ProductDefinition"/> (identity + DTD-materialized <c>Body</c>) plus the file's captured inline-DTD
    /// <see cref="Blocks"/>. The <see cref="Definition"/> is the exact component the install-dir path yields, so it is
    /// the authoritative target the decompiled recipe must reproduce; the <see cref="Blocks"/> are the grammar the
    /// self-verify normalizes both sides against.
    /// </summary>
    internal sealed record ProductSource(ProductDefinition Definition, ImmutableDictionary<string, string> Blocks);

    /// <summary>Reads a <c>Products\*.def</c> into a <see cref="ProductSource"/>, reproducing <c>CatalogDiscovery</c>'s
    /// per-file identity derivation (menu-prefix-stripped display name, captured inline DTD).</summary>
    internal static class CatalogSourceFile
    {
        public static ProductSource ReadProduct(string path, string categoryPath)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            ProjectElement body = CatalogReader.Read(stream);
            ImmutableDictionary<string, string> blocks = InlineDtd.Capture(bytes);
            string identifier = body.GetAttribute("product_identifier") ?? string.Empty;
            string displayName = MenuPrefix.Strip(body.GetAttribute("name") ?? string.Empty);
            var definition = new ProductDefinition(identifier, displayName, categoryPath, body)
            {
                InlineDtdBlocks = blocks,
            };
            return new ProductSource(definition, blocks);
        }
    }
}
