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
            byte[] bytes = File.ReadAllBytes(
                Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", relativePath));
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
    }
}
