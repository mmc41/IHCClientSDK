#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// Shared byte-level helpers for the <c>.vis</c>/<c>.def</c>/<c>.ifb</c> readers: draining a stream to bytes and
    /// extracting the XML declaration's declared encoding from a file head. Each reader keeps its own policy on what
    /// the declared name means (the project reader rejects foreign encodings, the catalog reader resolves them).
    /// </summary>
    internal static class XmlProlog
    {
        // XML permits whitespace around the '=' in an attribute (encoding = "…"); allow it so the declared-encoding
        // read (and the project reader's foreign-encoding guard that depends on it) is not bypassed by a legal spacing (review C3).
        private static readonly Regex DeclaredEncoding = new("encoding\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.Compiled);

        /// <summary>Drains the stream to a byte array (MemoryStream fast path).</summary>
        public static byte[] ReadAllBytes(Stream stream)
        {
            if (stream is MemoryStream memory)
            {
                return memory.ToArray();
            }
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        /// <summary>The file's first bytes (up to 200) decoded as Latin-1 — the ASCII window the declaration sits in.</summary>
        public static string Head(byte[] bytes) =>
            Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 200));

        /// <summary>
        /// The encoding name the head's XML declaration carries, or <c>null</c> when the head has no complete
        /// declaration or the declaration names no encoding.
        /// </summary>
        public static string? TryGetDeclaredEncoding(string head)
        {
            if (!head.StartsWith("<?xml", StringComparison.Ordinal))
            {
                return null;
            }
            int declarationEnd = head.IndexOf("?>", StringComparison.Ordinal);
            if (declarationEnd < 0)
            {
                return null;
            }
            Match encoding = DeclaredEncoding.Match(head.Substring(0, declarationEnd));
            return encoding.Success ? encoding.Groups[1].Value : null;
        }
    }
}
