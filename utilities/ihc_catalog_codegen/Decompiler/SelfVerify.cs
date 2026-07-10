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

            // (3) Structured grammar: the recipe's strict-parsed grammar must equal the grammar the lenient read
            // path yields for the same file (value equality over declarations, prolog datum and DOCTYPE root) —
            // proving the two parse modes agree over the corpus and the generated reference will resolve/insert
            // exactly as an install-dir read does. Replaces the old InlineDtdBlocks used∩non-registry subset check.
            if (!actual.Grammar.Equals(expected.Grammar))
            {
                return new VerifyResult(false, "structured grammar mismatch (strict parse vs lenient read)");
            }

            // (4) Byte fidelity: CatalogFileWriter-serializing the built product must reproduce the source .def under
            // the fidelity relation of record. This is the same check the final acceptance ran (accptest.md §10), kept
            // here so every regeneration re-proves it without the disposable acceptance project.
            if (SelfVerifyShared.WrittenBytesMismatch(source.FileBytes, SelfVerifyShared.WriteBytes(actual)) is { } byteReason)
            {
                return new VerifyResult(false, byteReason);
            }
            return VerifyResult.Pass;
        }
    }
}
