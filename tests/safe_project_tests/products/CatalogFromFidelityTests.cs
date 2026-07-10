#nullable enable
using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// <c>From(existing)</c> fidelity: reopening a read definition and rebuilding it must lose neither its
    /// grammar (per-file irregular order and variants included) nor its physical text encoding — proven as
    /// read → <c>From</c> → <c>Build</c> → <c>Write</c> ≡ source bytes over the grammar-envelope oracles.
    /// An explicit <c>.Grammar(...)</c> after <c>From</c> replaces the carried grammar; <c>.ExtendGrammar(...)</c>
    /// starts from it.
    /// </summary>
    public class CatalogFromFidelityTests
    {
        private static string OraclePath(string relative) =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", relative);

        [Test]
        public void From_IrregularGrammarBlock_RebuildsToSourceBytes()
        {
            FunctionBlockDefinition read = CatalogReader.ReadFunctionBlock(
                OraclePath("functionblocks/synthetic/synthetic_fb07_grammar.ifb"));

            FunctionBlockDefinition rebuilt = FunctionBlockDefinitionBuilder.From(read).Build();

            SyntheticOracle.AssertWritesOracleBytes(rebuilt, "functionblocks/synthetic/synthetic_fb07_grammar.ifb",
                "_0x5128", "_0x5223", "_0x5311", "_0x5424", "_0x5512", "_0x5625", "_0x570a", "_0x5829", "_0x5926",
                "_0x5a1e", "_0x5b64", "_0x5cc8", "_0x5d66", "_0x5eca");
        }

        [Test]
        public void From_CaseSkewProduct_RebuildsToSourceBytes()
        {
            ProductDefinition read = CatalogReader.ReadProduct(
                OraclePath("products/synthetic/synthetic_9f12_caseskew.def"));

            ProductDefinition rebuilt = ProductDefinitionBuilder.From(read).Build();

            SyntheticOracle.AssertWritesOracleBytes(rebuilt, "products/synthetic/synthetic_9f12_caseskew.def",
                "_0x01", "_0x02");
        }

        [Test]
        public void From_Utf8NoBomProduct_RebuildsToSourceBytes()
        {
            ProductDefinition read = CatalogReader.ReadProduct(
                OraclePath("products/synthetic/synthetic_9f13_utf8nobom.def"));
            Assert.That(read.SourceEncoding, Is.EqualTo(CatalogTextEncoding.Utf8), "classified UTF-8 without BOM");

            ProductDefinition rebuilt = ProductDefinitionBuilder.From(read).Build();

            Assert.That(rebuilt.SourceEncoding, Is.EqualTo(CatalogTextEncoding.Utf8), "From carries the encoding");
            SyntheticOracle.AssertWritesOracleBytes(rebuilt, "products/synthetic/synthetic_9f13_utf8nobom.def",
                "_0x01", "_0x02");
        }

        [Test]
        public void ExplicitGrammar_AfterFrom_ReplacesTheCarriedGrammar()
        {
            ProductDefinition read = CatalogReader.ReadProduct(
                OraclePath("products/synthetic/synthetic_9f12_caseskew.def"));
            CatalogGrammar replacement = CatalogGrammar.Create(new[] { OracleGrammars.DatalineRootLean7 });

            ProductDefinition rebuilt = ProductDefinitionBuilder.From(read).Grammar(replacement).Build();

            Assert.That(rebuilt.Grammar, Is.EqualTo(replacement));
        }

        [Test]
        public void ExtendGrammar_AfterFrom_StartsFromTheCarriedGrammar()
        {
            ProductDefinition read = CatalogReader.ReadProduct(
                OraclePath("products/synthetic/synthetic_9f12_caseskew.def"));

            ProductDefinition rebuilt = ProductDefinitionBuilder.From(read)
                .ExtendGrammar(g => g.ElementOnly("resource_extra"))
                .Build();

            Assert.Multiple(() =>
            {
                Assert.That(rebuilt.Grammar.Declarations.Length, Is.EqualTo(read.Grammar.Declarations.Length + 1));
                Assert.That(rebuilt.Grammar.Declarations.Select(d => d.Tag).Take(read.Grammar.Declarations.Length),
                    Is.EqualTo(read.Grammar.Declarations.Select(d => d.Tag)), "the carried declarations lead, in order");
                Assert.That(rebuilt.Grammar.TryGetDeclaration("resource_extra"), Is.Not.Null);
            });
        }
    }
}
