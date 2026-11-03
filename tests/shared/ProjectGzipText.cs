using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Ihc.Tests
{
    /// <summary>
    /// A project's text as a controller transfers it: its ISO-8859-1 bytes as one gzip stream, whole or cut into
    /// slices of which only the first carries the gzip header. Encoded and decoded with the framework's own gzip, so a
    /// check of the SDK's or the stub's gzip handling does not restate the code under test.
    /// </summary>
    internal static class ProjectGzipText
    {
        /// <summary>The gzip stream of <paramref name="text"/>'s ISO-8859-1 bytes.</summary>
        internal static byte[] Compress(string text)
        {
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionMode.Compress))
            {
                gzip.Write(ProjectFile.Encoding.GetBytes(text));
            }
            return compressed.ToArray();
        }

        /// <summary><paramref name="slices"/> joined in order and decompressed once, read as ISO-8859-1 text.</summary>
        internal static string Decompress(IEnumerable<byte[]> slices)
        {
            using var text = new StreamReader(
                new GZipStream(new MemoryStream([.. slices.SelectMany(slice => slice)]), CompressionMode.Decompress), ProjectFile.Encoding);
            return text.ReadToEnd();
        }

        /// <summary>Whether <paramref name="slice"/> starts with the gzip magic, as only a stream's first slice does.</summary>
        internal static bool StartsWithGzipMagic(byte[] slice) => slice.Length >= 2 && slice[0] == 0x1F && slice[1] == 0x8B;
    }
}
