#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Ihc.Vis.Catalog;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// W1 (reader→writer identity) on the committed synthetic oracles: for every invented, vendor-format
    /// <c>testdata/products/synthetic/*.def</c> and <c>testdata/functionblocks/synthetic/*.ifb</c>, serializing the
    /// definition <see cref="CatalogReader"/> parsed from it must reproduce the file <b>after whitespace
    /// normalization</b> (whitespace outside quotes removed on both sides — the fidelity relation the final acceptance
    /// uses, since vendor hand-formatting is not reconstructable). This proves the writer is a faithful mirror of the
    /// raw body + captured header + encoding, with everything significant (structure, attribute values, escaping, ids,
    /// header) reproduced exactly. Broad per-construct coverage over the real corpus is the temp acceptance project's
    /// job; this is the committed gate.
    /// </summary>
    public class CatalogFileWriterTests
    {
        private static readonly string ProductDir = TestData.PathOf("products", "synthetic");

        private static readonly string FunctionBlockDir = TestData.PathOf("functionblocks", "synthetic");

        private static IEnumerable<string> ProductOracles() => Oracles(ProductDir, "*.def");

        private static IEnumerable<string> FunctionBlockOracles() => Oracles(FunctionBlockDir, "*.ifb");

        private static IEnumerable<string> Oracles(string dir, string pattern) =>
            Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, pattern).OrderBy(p => p, System.StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        [TestCaseSource(nameof(ProductOracles))]
        public void Product_ReaderToWriter_ReproducesFile_UpToWhitespace(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            using var reading = new MemoryStream(file, writable: false);
            ProductDefinition definition = CatalogReader.ReadProduct(reading);

            byte[] written = Write(definition);

            AssertEquivalent(file, written, path);
        }

        [TestCaseSource(nameof(FunctionBlockOracles))]
        public void FunctionBlock_ReaderToWriter_ReproducesFile_UpToWhitespace(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            using var reading = new MemoryStream(file, writable: false);
            FunctionBlockDefinition definition = CatalogReader.ReadFunctionBlock(reading);

            byte[] written = Write(definition);

            AssertEquivalent(file, written, path);
        }

        [TestCaseSource(nameof(ProductOracles))]
        public void Product_Writer_IsDeterministic(string path)
        {
            ProductDefinition definition = CatalogReader.ReadProduct(new MemoryStream(File.ReadAllBytes(path)));
            byte[] first = Write(definition);
            byte[] second = Write(definition);
            Assert.That(second, Is.EqualTo(first), "writing the same definition twice is byte-identical");
        }

        private static byte[] Write(ProductDefinition definition)
        {
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(definition, ms);
            return ms.ToArray();
        }

        private static byte[] Write(FunctionBlockDefinition definition)
        {
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(definition, ms);
            return ms.ToArray();
        }

        private static void AssertEquivalent(byte[] expected, byte[] actual, string path)
        {
            if (!CatalogTextCompare.Equivalent(expected, actual))
            {
                int offset = CatalogTextCompare.FirstDifference(expected, actual);
                Assert.Fail($"Re-emitted '{Path.GetFileName(path)}' differs from the file (whitespace-normalized) at "
                            + $"offset {offset}.\n  expected: [{CatalogTextCompare.Context(expected, offset)}]\n"
                            + $"  actual:   [{CatalogTextCompare.Context(actual, offset)}]");
            }
        }
    }
}
