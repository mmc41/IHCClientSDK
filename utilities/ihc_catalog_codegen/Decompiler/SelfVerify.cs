#nullable enable
using System;
using System.Collections.Immutable;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

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
            ImmutableDictionary<string, string> nonRegistryBlocks =
                SelfVerifyShared.NonRegistryBlocks(source.Definition.Body, source.Blocks);
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
            if (SelfVerifyShared.BlocksMismatch(actual.InlineDtdBlocks, nonRegistryBlocks) is { } reason)
            {
                return new VerifyResult(false, reason);
            }
            return VerifyResult.Pass;
        }
    }
}
