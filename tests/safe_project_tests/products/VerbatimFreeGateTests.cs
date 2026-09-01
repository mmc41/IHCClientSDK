using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The verbatim-free gate (Phase 4c of the plan of record), scoped to where verbatim vendor header text could
    /// actually hide: the three committed <c>BuiltInCatalog.*.cs</c> definition files and both definition builders
    /// must contain <b>no</b> DTD/prolog token text at all; within <c>ihcclient/src/vis/catalog/**</c> those tokens
    /// may appear only in <c>CatalogDtdEmitter</c>/<c>CatalogDtdParser</c> (their own syntax constants). Explicitly
    /// out of scope: <c>ProjectSchemaRegistry</c>'s curated <c>.vis</c> registry blocks, <c>InlineDtd</c>/
    /// <c>ProjectSerializer</c> (<c>.vis</c> header machinery), and test inputs — none of which are catalog-file
    /// copies. Additionally asserts the Phase 3 compatibility bridge members no longer exist.
    /// </summary>
    public class VerbatimFreeGateTests
    {
        private static readonly string[] DtdTokens = { "<?xml", "<!DOCTYPE", "<!ELEMENT", "<!ATTLIST" };

        private static readonly string[] DefinitionFiles =
        {
            "BuiltInCatalog.Grammar.cs", "BuiltInCatalog.Products.cs", "BuiltInCatalog.FunctionBlocks.cs",
        };

        private static string DefinitionsDir =>
            Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src", "vis", "catalog", "definitions");

        [Test]
        public void EmbeddedCatalog_AndBuilders_CarryNoDtdText()
        {
            var offenders = new List<string>();
            IEnumerable<string> targets = DefinitionFiles.Select(f => Path.Combine(DefinitionsDir, f)).Concat(new[]
            {
                Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src", "vis", "products", "ProductDefinitionBuilder.cs"),
                Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src", "vis", "functionblocks", "FunctionBlockDefinitionBuilder.cs"),
            });
            foreach (string path in targets)
            {
                string text = File.ReadAllText(path);
                foreach (string token in DtdTokens.Where(text.Contains))
                {
                    offenders.Add($"{Path.GetFileName(path)}: contains '{token}'");
                }
            }
            Assert.That(offenders, Is.Empty,
                "no verbatim DTD/prolog text may exist in the catalog definition files or the builders:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void CatalogNamespace_CarriesDtdDeclarationTokens_OnlyInEmitterAndParser()
        {
            string catalogDir = Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src", "vis", "catalog");
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CatalogDtdEmitter.cs", "CatalogDtdParser.cs",
            };
            // The declaration tokens only: any verbatim vendor header would necessarily carry them. "<?xml" is
            // deliberately not scanned here — CatalogReader's encoding sniff legitimately matches a prolog prefix,
            // which is detection syntax, not copied header text (the generated files + builders scan keeps it).
            string[] declarationTokens = { "<!DOCTYPE", "<!ELEMENT", "<!ATTLIST" };
            var offenders = new List<string>();
            foreach (string path in Directory.EnumerateFiles(catalogDir, "*.cs", SearchOption.AllDirectories))
            {
                if (allowed.Contains(Path.GetFileName(path)))
                {
                    continue;
                }
                string text = File.ReadAllText(path);
                foreach (string token in declarationTokens.Where(text.Contains))
                {
                    offenders.Add($"{Path.GetRelativePath(catalogDir, path)}: contains '{token}'");
                }
            }
            Assert.That(offenders, Is.Empty,
                "within the catalog namespace only the DTD emitter/parser own the declaration tokens:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void Phase3BridgeMembers_NoLongerExist()
        {
            Assert.Multiple(() =>
            {
                Assert.That(typeof(ProductDefinition).GetProperty("Head",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), Is.Null,
                    "ProductDefinition.Head (the regeneration bridge) must be deleted");
                Assert.That(typeof(FunctionBlockDefinition).GetProperty("Head",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), Is.Null,
                    "FunctionBlockDefinition.Head (the regeneration bridge) must be deleted");
                bool stringOverload = typeof(CatalogIds)
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Any(m => m.Name == "StampDocumentOrder"
                              && m.GetParameters() is { Length: 3 } p && p[2].ParameterType == typeof(string));
                Assert.That(stringOverload, Is.False,
                    "CatalogIds.StampDocumentOrder(…, string head) (the regeneration bridge) must be deleted");
            });
        }
    }
}
