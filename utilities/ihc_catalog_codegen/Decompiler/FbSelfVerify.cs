#nullable enable
using System;
using System.Collections.Immutable;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// The generator's fidelity gate for function blocks: proves a decompiled <see cref="FunctionBlockRecipe"/>
    /// reproduces the <c>.ifb</c> it came from, using the same normalize/compare (<see cref="DefinitionNormalizer"/>)
    /// the oracle tests use. It replays the recipe against the real builder, canonicalizes both the built and the source
    /// body against the file's own grammar and against the project registry, checks the master identity fields, and
    /// verifies the open-world inline-DTD capture. No block is emitted unless this passes.
    /// </summary>
    internal static class FbSelfVerify
    {
        public static VerifyResult Verify(FunctionBlockRecipe recipe, FunctionBlockSource source)
        {
            FunctionBlockDefinition actual;
            try
            {
                actual = recipe.Build();
            }
            catch (Exception ex)
            {
                return new VerifyResult(false, $"Build() threw: {ex.GetType().Name}: {ex.Message}");
            }

            ProjectElement expectedBody = DefinitionNormalizer.Normalize(source.Definition.Body, source.Blocks);
            ProjectElement actualBody = DefinitionNormalizer.Normalize(actual.Body, source.Blocks);
            if (!expectedBody.Equals(actualBody))
            {
                return new VerifyResult(false, "catalog-grammar body mismatch",
                    DefinitionNormalizer.Dump(expectedBody), DefinitionNormalizer.Dump(actualBody));
            }

            ImmutableDictionary<string, string> nonRegistryBlocks =
                SelfVerifyShared.NonRegistryBlocks(source.Definition.Body, source.Blocks);
            ProjectElement expectedReg = DefinitionNormalizer.Normalize(source.Definition.Body, nonRegistryBlocks);
            ProjectElement actualReg = DefinitionNormalizer.Normalize(actual.Body, nonRegistryBlocks);
            if (!expectedReg.Equals(actualReg))
            {
                return new VerifyResult(false, "registry-grammar body mismatch (catalog-vs-project default bake)",
                    DefinitionNormalizer.Dump(expectedReg), DefinitionNormalizer.Dump(actualReg));
            }

            FunctionBlockDefinition expected = source.Definition;
            if (actual.MasterType != expected.MasterType)
            {
                return new VerifyResult(false, $"master_type '{actual.MasterType}' != '{expected.MasterType}'");
            }
            if (actual.MasterVersion != expected.MasterVersion)
            {
                return new VerifyResult(false, $"master_version '{actual.MasterVersion}' != '{expected.MasterVersion}'");
            }
            if (actual.MasterName != expected.MasterName)
            {
                return new VerifyResult(false, $"master_name '{actual.MasterName}' != '{expected.MasterName}'");
            }
            if (actual.DisplayName != expected.DisplayName)
            {
                return new VerifyResult(false, $"display name '{actual.DisplayName}' != '{expected.DisplayName}'");
            }
            if (actual.CategoryPath != expected.CategoryPath)
            {
                return new VerifyResult(false, $"category path '{actual.CategoryPath}' != '{expected.CategoryPath}'");
            }

            // Structured grammar: strict parse (the recipe) must equal the lenient read (the definition) — value
            // equality; replaces the old InlineDtdBlocks used∩non-registry subset check.
            if (!actual.Grammar.Equals(expected.Grammar))
            {
                return new VerifyResult(false, "structured grammar mismatch (strict parse vs lenient read)");
            }

            // Byte fidelity: CatalogFileWriter-serializing the built block must reproduce the source .ifb under the
            // fidelity relation of record — the same check the final acceptance ran (accptest.md §10), kept here so
            // every regeneration re-proves it. The one vendor escaping inconsistency (1.2.05.ifb's &apos;) is
            // handled by the relation itself (CatalogTextCompare, D3), not by a gate-local tolerance.
            if (SelfVerifyShared.WrittenBytesMismatch(source.FileBytes, SelfVerifyShared.WriteBytes(actual)) is { } byteReason)
            {
                return new VerifyResult(false, byteReason);
            }
            return VerifyResult.Pass;
        }
    }
}
