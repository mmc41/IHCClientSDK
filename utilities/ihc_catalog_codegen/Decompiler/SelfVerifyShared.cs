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
    /// blocks): the open-world inline-DTD capture rule, the two-grammar body comparison and the byte-level
    /// re-emission gate. Both gates must apply the identical rules, so they live here rather than being copied into
    /// each gate where the two could silently drift.
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

        // The two-grammar body comparison both verify gates share. (1) canonicalize built and source bodies against
        // the file's OWN grammar — the "a builder is a code-authored CatalogReader" contract the oracle tests assert.
        // (2) canonicalize against the grammar the insert transform actually uses: the project registry PLUS the
        // open-world blocks the component carries (a purely-registry view cannot canonicalize an open-world type) —
        // this catches the catalog-vs-project DTD-default bake (B1c): an attribute that rides the file default but
        // differs from the registry default must have been baked, or the placed instance would silently change.
        // Null when both agree; else the failing VerifyResult carrying the normalized trees for a readable diff.
        public static VerifyResult? BodyMismatch(ProjectElement sourceBody, ProjectElement builtBody,
            ImmutableDictionary<string, string> blocks)
        {
            ProjectElement expectedBody = DefinitionNormalizer.Normalize(sourceBody, blocks);
            ProjectElement actualBody = DefinitionNormalizer.Normalize(builtBody, blocks);
            if (!expectedBody.Equals(actualBody))
            {
                return new VerifyResult(false, "catalog-grammar body mismatch",
                    DefinitionNormalizer.Dump(expectedBody), DefinitionNormalizer.Dump(actualBody));
            }

            ImmutableDictionary<string, string> nonRegistryBlocks = NonRegistryBlocks(sourceBody, blocks);
            ProjectElement expectedReg = DefinitionNormalizer.Normalize(sourceBody, nonRegistryBlocks);
            ProjectElement actualReg = DefinitionNormalizer.Normalize(builtBody, nonRegistryBlocks);
            if (!expectedReg.Equals(actualReg))
            {
                return new VerifyResult(false, "registry-grammar body mismatch (catalog-vs-project default bake)",
                    DefinitionNormalizer.Dump(expectedReg), DefinitionNormalizer.Dump(actualReg));
            }
            return null;
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
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                tags.Add(e.Tag);
            }
        }
    }
}
