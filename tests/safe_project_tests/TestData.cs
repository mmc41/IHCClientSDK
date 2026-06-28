using System;
using System.IO;
using System.Text;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Helpers for the byte-fidelity tests: reads the raw testdata bytes and asserts byte-array equality with a
    /// diagnostic first-difference hex dump (offset, line, and a window of both buffers) — essential when chasing
    /// a single wrong byte in an 88 KB serialization.
    /// </summary>
    internal static class TestData
    {
        public static byte[] ReadBytes(string name) =>
            File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", name));

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
            sb.AppendLine($"{label}: serialized bytes differ from the original.");
            sb.AppendLine($"  expected length: {expected.Length}, actual length: {actual.Length}");
            sb.AppendLine($"  first difference at byte 0x{offset:x} ({offset}), line {line}, column {col}");
            int start = Math.Max(0, offset - 16);
            sb.AppendLine($"  expected: {HexWindow(expected, start, 32)}");
            sb.AppendLine($"  actual:   {HexWindow(actual, start, 32)}");
            return sb.ToString();
        }

        private static string HexWindow(byte[] data, int start, int count)
        {
            var hex = new StringBuilder();
            var ascii = new StringBuilder();
            for (int i = start; i < start + count && i < data.Length; i++)
            {
                hex.Append(data[i].ToString("x2")).Append(' ');
                byte b = data[i];
                ascii.Append(b is >= 0x20 and < 0x7f ? (char)b : '.');
            }
            return $"@0x{start:x}  {hex}| {ascii}";
        }
    }
}
