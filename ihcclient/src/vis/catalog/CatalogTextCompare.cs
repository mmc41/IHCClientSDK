#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The catalog-file fidelity relation the acceptance uses: two <c>.def</c>/<c>.ifb</c> files are "equivalent" when
    /// byte-identical after <b>normalizing non-semantic serialization form</b> — (1) removing every whitespace byte
    /// outside a double-quoted string (vendor files are hand-formatted with irregular indent / blank lines / trailing
    /// spaces, which was measured to be non-reconstructable: over the 173 shipped files the best fully reconstructing
    /// writer reproduced 157 byte-exactly, the residual 16 demanding
    /// per-element verbatim whitespace echoing), and (2) collapsing an empty paired
    /// element <c>&lt;tag…&gt;&lt;/tag&gt;</c> to the self-closing <c>&lt;tag…/&gt;</c> (XML-identical; the vendor writes
    /// some empty containers paired and others self-closed), and (3) rewriting <c>&amp;apos;</c> to the literal
    /// apostrophe it denotes (XML-identical for the same value; one vendor file, <c>1.2.05.ifb</c>, escapes
    /// apostrophes where the rest of the corpus writes them literally — decision D3). Everything <em>significant</em>
    /// — element/attribute structure, attribute values, every other entity escaping, ids, and the header — is
    /// compared strictly. The rule is encoding-agnostic: whitespace, <c>"</c>, the tag syntax and the six
    /// <c>&amp;apos;</c> bytes are ASCII and cannot occur inside a multi-byte sequence, and non-ASCII value bytes
    /// never collide, so a wrong text encoding still fails.
    /// </summary>
    internal static class CatalogTextCompare
    {
        /// <summary>True when the two byte streams are identical after form normalization.</summary>
        public static bool Equivalent(byte[] a, byte[] b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            return Normalize(a).AsSpan().SequenceEqual(Normalize(b).AsSpan());
        }

        /// <summary>The bytes with <c>&amp;apos;</c> rewritten to <c>'</c>, empty paired elements collapsed to
        /// self-close, then insignificant whitespace removed. Element collapse runs before whitespace stripping — on
        /// the original bytes the tag name is delimited from its attributes by whitespace, which stripping would
        /// merge away.</summary>
        public static byte[] Normalize(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            return StripInsignificantWhitespace(CollapseEmptyElements(CollapseApostropheEntity(bytes)));
        }

        // Rewrites every "&apos;" to the literal apostrophe it denotes (D3). Unconditional and byte-level: the six
        // bytes are ASCII (cannot occur inside a multi-byte sequence), and in a well-formed catalog file the
        // sequence can only occur where an apostrophe itself is legal.
        private static byte[] CollapseApostropheEntity(byte[] bytes)
        {
            ReadOnlySpan<byte> entity = "&apos;"u8;
            var result = new List<byte>(bytes.Length);
            int i = 0;
            while (i < bytes.Length)
            {
                if (i + entity.Length <= bytes.Length && bytes.AsSpan(i, entity.Length).SequenceEqual(entity))
                {
                    result.Add((byte)'\'');
                    i += entity.Length;
                }
                else
                {
                    result.Add(bytes[i]);
                    i++;
                }
            }
            return result.ToArray();
        }

        /// <summary>The bytes with every whitespace byte (space/tab/CR/LF) outside a double-quoted region removed.</summary>
        public static byte[] StripInsignificantWhitespace(byte[] bytes)
        {
            var result = new List<byte>(bytes.Length);
            bool inQuote = false;
            foreach (byte b in bytes)
            {
                if (b == (byte)'"')
                {
                    inQuote = !inQuote;
                    result.Add(b);
                    continue;
                }
                if (!inQuote && (b == 0x20 || b == 0x09 || b == 0x0D || b == 0x0A))
                {
                    continue;
                }
                result.Add(b);
            }
            return result.ToArray();
        }

        // Rewrites every empty paired element "<tag …></tag>" (optionally with insignificant whitespace between the
        // open and close tags) to the self-closing "<tag …/>", operating on the ORIGINAL bytes so the tag name is still
        // whitespace-delimited from its attributes. DTD/prolog constructs (<! …>, <? …>) and non-empty elements are
        // copied verbatim; attribute values are quote-guarded so a '>' inside a value cannot end a tag early.
        internal static byte[] CollapseEmptyElements(byte[] s)
        {
            var outb = new List<byte>(s.Length);
            int i = 0, n = s.Length;
            while (i < n)
            {
                if (s[i] != (byte)'<' || i + 1 >= n || s[i + 1] is (byte)'/' or (byte)'!' or (byte)'?')
                {
                    outb.Add(s[i]);
                    i++;
                    continue;
                }
                int gt = FindTagEnd(s, i + 1, n);
                if (gt < 0)
                {
                    for (; i < n; i++) { outb.Add(s[i]); }
                    break;
                }
                int nameEnd = i + 1;
                while (nameEnd < gt && IsNameByte(s[nameEnd])) { nameEnd++; }
                bool selfClosed = s[gt - 1] == (byte)'/';
                int afterClose = selfClosed ? -1 : MatchEmptyClose(s, gt + 1, i + 1, nameEnd, n);
                if (afterClose >= 0)
                {
                    for (int k = i; k < gt; k++) { outb.Add(s[k]); }   // "<tag …" without the '>'
                    if (s[gt - 1] != (byte)' ') { outb.Add((byte)' '); }
                    outb.Add((byte)'/');
                    outb.Add((byte)'>');
                    i = afterClose;
                    continue;
                }
                for (int k = i; k <= gt; k++) { outb.Add(s[k]); }
                i = gt + 1;
            }
            return outb.ToArray();
        }

        private static int FindTagEnd(byte[] s, int start, int n)
        {
            bool inQuote = false;
            for (int i = start; i < n; i++)
            {
                byte b = s[i];
                if (b == (byte)'"') { inQuote = !inQuote; }
                else if (!inQuote && b == (byte)'>') { return i; }
            }
            return -1;
        }

        // If the element opened at name [nameStart,nameEnd) is empty — only insignificant whitespace, then a matching
        // "</name>" close tag, from <paramref name="pos"/> — returns the index just past that close tag; else -1.
        private static int MatchEmptyClose(byte[] s, int pos, int nameStart, int nameEnd, int n)
        {
            while (pos < n && s[pos] is 0x20 or 0x09 or 0x0D or 0x0A) { pos++; }
            int len = nameEnd - nameStart;
            if (len <= 0 || pos + 2 + len + 1 > n || s[pos] != (byte)'<' || s[pos + 1] != (byte)'/')
            {
                return -1;
            }
            for (int k = 0; k < len; k++)
            {
                if (s[pos + 2 + k] != s[nameStart + k]) { return -1; }
            }
            return s[pos + 2 + len] == (byte)'>' ? pos + 2 + len + 1 : -1;
        }

        private static bool IsNameByte(byte b) =>
            (b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'A' && b <= (byte)'Z')
            || (b >= (byte)'0' && b <= (byte)'9') || b is (byte)'_' or (byte)'-' or (byte)'.' or (byte)':';

        /// <summary>The offset of the first differing byte in the normalized streams, or -1 when equivalent.</summary>
        public static int FirstDifference(byte[] a, byte[] b)
        {
            byte[] sa = Normalize(a);
            byte[] sb = Normalize(b);
            int n = Math.Min(sa.Length, sb.Length);
            for (int i = 0; i < n; i++)
            {
                if (sa[i] != sb[i])
                {
                    return i;
                }
            }
            return sa.Length == sb.Length ? -1 : n;
        }

        /// <summary>A short printable window of the normalized stream around <paramref name="offset"/>, for diagnostics.</summary>
        public static string Context(byte[] bytes, int offset, int span = 24)
        {
            byte[] s = Normalize(bytes);
            int start = Math.Max(0, offset - 4);
            int end = Math.Min(s.Length, offset + span);
            var sb = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                char c = (char)s[i];
                sb.Append(c >= 0x20 && c < 0x7F ? c : '.');
            }
            return sb.ToString();
        }
    }
}
