#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Schema;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>The outcome of self-verifying one decompiled recipe: whether it reproduced the source file, and — on
    /// failure — a one-line reason plus (for a body mismatch) the two normalized trees for a readable diff.</summary>
    internal sealed record VerifyResult(bool Ok, string? Reason = null, string? Expected = null, string? Actual = null)
    {
        public static VerifyResult Pass { get; } = new(true);
    }

    /// <summary>
    /// The generator's fidelity gate: proves a decompiled <see cref="ProductRecipe"/> reproduces the catalog file it
    /// came from, using the <em>same</em> normalize/compare (<see cref="DefinitionNormalizer"/>) the oracle tests use.
    /// It replays the recipe against the real builder, canonicalizes both the built and the source body against the
    /// file's own grammar, and additionally checks the discovered identity fields. No component is emitted unless this
    /// passes, so the committed catalog can never drift from the vendor source.
    /// </summary>
    internal static class SelfVerify
    {
        public static VerifyResult Verify(ProductRecipe recipe, ProductSource source)
        {
            ProductDefinition actual;
            try
            {
                actual = recipe.Build();
            }
            catch (Exception ex)
            {
                return new VerifyResult(false, $"Build() threw: {ex.GetType().Name}: {ex.Message}");
            }

            // (1) Canonicalize against the file's own (catalog) grammar — the "a builder is a code-authored
            // CatalogReader" contract the oracle tests assert.
            ProjectElement expectedBody = DefinitionNormalizer.Normalize(source.Definition.Body, source.Blocks);
            ProjectElement actualBody = DefinitionNormalizer.Normalize(actual.Body, source.Blocks);
            if (!expectedBody.Equals(actualBody))
            {
                return new VerifyResult(false, "catalog-grammar body mismatch",
                    DefinitionNormalizer.Dump(expectedBody), DefinitionNormalizer.Dump(actualBody));
            }

            // (2) Canonicalize against the grammar the insert transform actually uses: the project registry PLUS the
            // open-world blocks the product carries (a purely-registry view cannot canonicalize an open-world type).
            // This catches the catalog-vs-project DTD-default bake (B1c): an attribute that rides the .def default but
            // differs from the registry default must have been baked, or the placed instance would silently change.
            ImmutableDictionary<string, string> nonRegistryBlocks = NonRegistryBlocks(source);
            ProjectElement expectedReg = DefinitionNormalizer.Normalize(source.Definition.Body, nonRegistryBlocks);
            ProjectElement actualReg = DefinitionNormalizer.Normalize(actual.Body, nonRegistryBlocks);
            if (!expectedReg.Equals(actualReg))
            {
                return new VerifyResult(false, "registry-grammar body mismatch (catalog-vs-project default bake)",
                    DefinitionNormalizer.Dump(expectedReg), DefinitionNormalizer.Dump(actualReg));
            }

            ProductDefinition expected = source.Definition;
            if (actual.ProductIdentifier != expected.ProductIdentifier)
            {
                return new VerifyResult(false,
                    $"product_identifier '{actual.ProductIdentifier}' != '{expected.ProductIdentifier}'");
            }
            if (actual.DisplayName != expected.DisplayName)
            {
                return new VerifyResult(false, $"display name '{actual.DisplayName}' != '{expected.DisplayName}'");
            }
            if (actual.CategoryPath != expected.CategoryPath)
            {
                return new VerifyResult(false, $"category path '{actual.CategoryPath}' != '{expected.CategoryPath}'");
            }

            // (3) Open-world capture: the built product must carry an inline-DTD block for exactly the element types its
            // source uses that the registry does not declare — no more (registry-declared blocks are dead weight the
            // insert transform never merges), no fewer (a missing block makes the component unsaveable once inserted).
            if (BlocksMismatch(actual.InlineDtdBlocks, nonRegistryBlocks) is { } reason)
            {
                return new VerifyResult(false, reason);
            }
            return VerifyResult.Pass;
        }

        // The source's inline-DTD blocks for element types that are (a) actually instantiated in the body and (b) not
        // declared by the static registry — the open-world grammar that must ride along with the component (and that the
        // insert transform merges into the project). A block DECLARED in the .def DTD but never used in the body is a
        // vendor leftover (e.g. a mis-cased 'resource_Light' block above a body that uses registry 'resource_light') and
        // is deliberately excluded — the component neither needs it nor carries it.
        private static ImmutableDictionary<string, string> NonRegistryBlocks(ProductSource source)
        {
            var usedTags = new HashSet<string>(StringComparer.Ordinal);
            CollectTags(source.Definition.Body, usedTags);
            return source.Blocks
                .Where(kv => usedTags.Contains(kv.Key) && ProjectSchemaRegistry.TryGet(kv.Key) is null)
                .ToImmutableDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        }

        private static void CollectTags(ProjectElement element, HashSet<string> tags)
        {
            tags.Add(element.Tag);
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                CollectTags(child, tags);
            }
        }

        // Compares the built product's inline-DTD blocks against the expected non-registry set (tag → verbatim block).
        // Returns a one-line reason on mismatch, or null when they agree.
        private static string? BlocksMismatch(ImmutableDictionary<string, string> actual,
            ImmutableDictionary<string, string> expected)
        {
            foreach (string tag in expected.Keys)
            {
                if (!actual.TryGetValue(tag, out string? block))
                {
                    return $"inline-DTD block for open-world type '{tag}' not captured";
                }
                if (block != expected[tag])
                {
                    return $"inline-DTD block for '{tag}' differs from source";
                }
            }
            foreach (string tag in actual.Keys)
            {
                if (!expected.ContainsKey(tag))
                {
                    return $"inline-DTD block '{tag}' captured but registry already declares it (dead weight)";
                }
            }
            return null;
        }
    }
}
