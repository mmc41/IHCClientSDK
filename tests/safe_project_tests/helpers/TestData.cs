using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Helpers for the byte-fidelity tests: reads the raw testdata bytes and asserts byte-array equality with a
    /// diagnostic first-difference hex dump (offset, line, and a window of both buffers) — essential when chasing
    /// a single wrong byte in an 88 KB serialization.
    /// </summary>
    internal static class TestData
    {
        // Oracle bytes cached per name across the suite (the 236 KB project3 oracle alone is read by dozens of
        // tests); cloned per call so a test mutating its buffer can never poison another.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The absolute path of a file or directory under the copied <c>testdata</c> tree.</summary>
        public static string PathOf(params string[] parts) =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", Path.Combine(parts));

        /// <summary>Strictly parses a <c>_0x</c> id token; throws on a malformed one.</summary>
        public static ElementId Id(string token) =>
            ElementId.TryParse(token, out ElementId id)
                ? id
                : throw new ArgumentException($"Bad id token '{token}'.", nameof(token));

        /// <summary>The raw numeric value of a <c>_0x</c> scalar token (e.g. <c>last_unique_id</c>); throws on a
        /// malformed one.</summary>
        public static long HexCounter(string? token) =>
            HexToken.TryParseValue(token, out long value)
                ? value
                : throw new ArgumentException($"Bad hex token '{token}'.", nameof(token));

        public static byte[] ReadBytes(string name) =>
            (byte[])Cache.GetOrAdd(name, n => File.ReadAllBytes(PathOf(n))).Clone();

        public static void AssertBytesIdentical(byte[] expected, byte[] actual, string label)
        {
            if (expected.AsSpan().SequenceEqual(actual))
            {
                return;
            }
            Assert.Fail(BuildDiffMessage(expected, actual, FirstDifference(expected, actual), label));
        }

        private static int FirstDifference(byte[] a, byte[] b)
        {
            int min = Math.Min(a.Length, b.Length);
            for (int i = 0; i < min; i++)
            {
                if (a[i] != b[i])
                {
                    return i;
                }
            }
            return min; // identical up to the shorter length; they differ in length
        }

        private static string BuildDiffMessage(byte[] expected, byte[] actual, int offset, string label)
        {
            int line = 1;
            int col = 1;
            for (int i = 0; i < offset && i < expected.Length; i++)
            {
                if (expected[i] == 0x0A)
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{label}: serialized bytes differ from the original.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  expected length: {expected.Length}, actual length: {actual.Length}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  first difference at byte 0x{offset:x} ({offset}), line {line}, column {col}");
            int start = Math.Max(0, offset - 16);
            sb.AppendLine(CultureInfo.InvariantCulture, $"  expected: {HexWindow(expected, start, 32)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  actual:   {HexWindow(actual, start, 32)}");
            return sb.ToString();
        }

        private static string HexWindow(byte[] data, int start, int count)
        {
            var hex = new StringBuilder();
            var ascii = new StringBuilder();
            for (int i = start; i < start + count && i < data.Length; i++)
            {
                hex.Append(data[i].ToString("x2", CultureInfo.InvariantCulture)).Append(' ');
                byte b = data[i];
                ascii.Append(b is >= 0x20 and < 0x7f ? (char)b : '.');
            }
            return $"@0x{start:x}  {hex}| {ascii}";
        }
    }
}
