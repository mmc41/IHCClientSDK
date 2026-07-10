#nullable enable
using System.Collections.Immutable;
using System.IO;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Component-test glue that compares a builder-authored definition body directly against its synthetic oracle
    /// (<c>Products\*.def</c> / <c>FunctionBlocks\*.ifb</c>) — no project, no install dir. The design contract is
    /// "a builder is a code-authored <c>CatalogReader</c>": <c>builder.Build().Body</c> must be the same component the
    /// oracle file yields once both are reduced to a canonical form. The normalize/renumber/compare core lives in the
    /// shared <see cref="DefinitionNormalizer"/> (so the catalog code-generator's self-verify runs the identical
    /// logic); this harness only handles reading the oracle file and reporting the assertion.
    /// </summary>
    internal static class SyntheticOracle
    {
        internal static (ProjectElement Body, ImmutableDictionary<string, string> Blocks) Read(string relativePath)
        {
            byte[] bytes = TestData.ReadBytes(relativePath);
            using var stream = new MemoryStream(bytes);
            ProjectElement body = CatalogReader.Read(stream);
            return (body, InlineDtd.Capture(bytes));
        }

        /// <summary>Asserts the builder-authored <paramref name="builtBody"/> reduces to the same canonical form as the
        /// oracle at <paramref name="oraclePath"/>. On mismatch, dumps both trees for a readable structural diff.</summary>
        internal static void AssertMatchesOracle(ProjectElement builtBody, string oraclePath)
        {
            (ProjectElement oracleBody, ImmutableDictionary<string, string> blocks) = Read(oraclePath);
            ProjectElement expected = DefinitionNormalizer.Normalize(oracleBody, blocks);
            ProjectElement actual = DefinitionNormalizer.Normalize(builtBody, blocks);
            if (!expected.Equals(actual))
            {
                Assert.Fail(
                    $"Builder output does not match oracle '{oraclePath}'.\n\n=== EXPECTED (oracle) ===\n{DefinitionNormalizer.Dump(expected)}\n" +
                    $"=== ACTUAL (builder) ===\n{DefinitionNormalizer.Dump(actual)}");
            }
        }

        /// <summary>Asserts the code-authored <paramref name="definition"/> writes bytes equivalent to the oracle
        /// file (the whitespace-normalized fidelity relation; the writer's own well-formedness gate covers the
        /// reparse half). When <paramref name="oracleIdTokens"/> is given, the built body is first re-stamped with
        /// the oracle's document-order ids (exactly the generated-catalog mechanism); pass none when the builder's
        /// natural allocation already matches (a generated oracle).</summary>
        internal static void AssertWritesOracleBytes(ProductDefinition definition, string oraclePath,
            params string[] oracleIdTokens)
        {
            if (oracleIdTokens.Length > 0)
            {
                definition = definition with
                {
                    Body = CatalogIds.StampDocumentOrder(definition.Body, oracleIdTokens, definition.Grammar),
                };
            }
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(definition, ms);
            AssertBytesEquivalent(ms.ToArray(), oraclePath);
        }

        /// <inheritdoc cref="AssertWritesOracleBytes(ProductDefinition, string, string[])"/>
        internal static void AssertWritesOracleBytes(FunctionBlockDefinition definition, string oraclePath,
            params string[] oracleIdTokens)
        {
            if (oracleIdTokens.Length > 0)
            {
                definition = definition with
                {
                    Body = CatalogIds.StampDocumentOrder(definition.Body, oracleIdTokens, definition.Grammar),
                };
            }
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(definition, ms);
            AssertBytesEquivalent(ms.ToArray(), oraclePath);
        }

        private static void AssertBytesEquivalent(byte[] written, string oraclePath)
        {
            byte[] expected = TestData.ReadBytes(oraclePath);
            if (!CatalogTextCompare.Equivalent(expected, written))
            {
                int offset = CatalogTextCompare.FirstDifference(expected, written);
                Assert.Fail($"Code-authored definition does not byte-reproduce '{oraclePath}' " +
                            $"(whitespace-normalized) at offset {offset}.\n" +
                            $"  expected: [{CatalogTextCompare.Context(expected, offset)}]\n" +
                            $"  actual:   [{CatalogTextCompare.Context(written, offset)}]");
            }
        }
    }
}
