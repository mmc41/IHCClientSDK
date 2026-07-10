#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Schema;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// The fidelity checks shared by <see cref="SelfVerify"/> (products) and <see cref="FbSelfVerify"/> (function
    /// blocks): the open-world inline-DTD capture rule and the byte-level re-emission gate. Both gates must apply the
    /// identical rules, so they live here rather than being copied into each gate where the two could silently drift.
    /// </summary>
    internal static class SelfVerifyShared
    {
        // The source's inline-DTD blocks for element types that are (a) actually instantiated in the body and (b) not
        // declared by the static registry — the open-world grammar that must ride along with the component (and that the
        // insert transform merges into the project). A block DECLARED in the source DTD but never used in the body is a
        // vendor leftover (e.g. a mis-cased 'resource_Light' block above a body that uses registry 'resource_light') and
        // is deliberately excluded — the component neither needs it nor carries it.
        public static ImmutableDictionary<string, string> NonRegistryBlocks(ProjectElement body,
            ImmutableDictionary<string, string> blocks)
        {
            var usedTags = new HashSet<string>(StringComparer.Ordinal);
            CollectTags(body, usedTags);
            return blocks
                .Where(kv => usedTags.Contains(kv.Key) && ProjectSchemaRegistry.TryGet(kv.Key) is null)
                .ToImmutableDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        }

        public static byte[] WriteBytes(ProductDefinition definition)
        {
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(definition, ms);
            return ms.ToArray();
        }

        public static byte[] WriteBytes(FunctionBlockDefinition definition)
        {
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(definition, ms);
            return ms.ToArray();
        }

        // The byte-fidelity gate: the re-emitted definition must equal the source catalog file under the fidelity
        // relation of record (byte-identical after whitespace-normalization + empty-element collapse + the D3
        // &apos; ≡ ' forgiveness — see CatalogTextCompare, the single implementation of the relation; every other
        // escaping difference and every id stays strictly compared). Returns a one-line reason with the
        // first-divergence offset and context on mismatch, or null when faithful.
        public static string? WrittenBytesMismatch(byte[] sourceFile, byte[] written)
        {
            if (CatalogTextCompare.Equivalent(sourceFile, written))
            {
                return null;
            }
            int offset = CatalogTextCompare.FirstDifference(sourceFile, written);
            return "re-emitted bytes differ from the source file (whitespace-normalized) at offset "
                + $"{offset}: expected [{CatalogTextCompare.Context(sourceFile, offset)}] "
                + $"actual [{CatalogTextCompare.Context(written, offset)}]";
        }

        private static void CollectTags(ProjectElement element, HashSet<string> tags)
        {
            tags.Add(element.Tag);
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                CollectTags(child, tags);
            }
        }
    }
}
