using System;
using System.Text;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// The on-disk text encoding of a catalog component file (<c>Products\*.def</c> / <c>FunctionBlocks\*.ifb</c>),
    /// captured per file so <see cref="Ihc.Vis.Catalog.CatalogFileWriter"/> re-encodes byte-faithfully. The vendor
    /// corpus is split cleanly: every <c>.def</c> is UTF-8 with a BOM (bytes are UTF-8 despite the file declaring
    /// <c>ISO-8859-1</c> — the documented "UTF-8-under-Latin-1" trap), every <c>.ifb</c> is real Latin-1.
    /// <see cref="Utf8"/> (no BOM) is kept for completeness. This is a fidelity datum, not a choice: the writer
    /// reproduces the source encoding rather than transcoding.
    /// </summary>
    public enum CatalogTextEncoding
    {
        /// <summary>ISO-8859-1 / Latin-1, no BOM (the <c>.ifb</c> family).</summary>
        Latin1,

        /// <summary>UTF-8 preceded by an <c>EF BB BF</c> byte-order mark (the <c>.def</c> family).</summary>
        Utf8Bom,

        /// <summary>UTF-8 without a BOM.</summary>
        Utf8,
    }

    /// <summary>Encoding helpers for <see cref="CatalogTextEncoding"/> — the single place the enum maps to concrete
    /// bytes, shared by the reader (classification) and the writer (emission).</summary>
    public static class CatalogTextEncodingExtensions
    {
        private static readonly byte[] Utf8Preamble = { 0xEF, 0xBB, 0xBF };
        private static readonly byte[] NoPreamble = Array.Empty<byte>();

        /// <summary>T027: whether the raw file bytes begin with the UTF-8 byte-order mark (<c>EF BB BF</c>) — the one
        /// shared BOM test, co-located with the preamble bytes it checks for, used by the catalog and project readers
        /// and by <see cref="Classify"/> below.</summary>
        public static bool HasUtf8Bom(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            return bytes.Length >= Utf8Preamble.Length
                && bytes[0] == Utf8Preamble[0] && bytes[1] == Utf8Preamble[1] && bytes[2] == Utf8Preamble[2];
        }

        /// <summary>The BOM bytes to emit before the text for this encoding (empty when none).</summary>
        public static byte[] Preamble(this CatalogTextEncoding encoding) =>
            encoding == CatalogTextEncoding.Utf8Bom ? Utf8Preamble : NoPreamble;

        /// <summary>The <see cref="Encoding"/> (never emitting its own BOM) that encodes the text body for this kind.
        /// Encoding is strict: text the target repertoire cannot represent throws <see cref="EncoderFallbackException"/>
        /// instead of silently substituting '?' (decision D4 — mirrors the .vis wire's strict Latin-1); decoding keeps
        /// the tolerant replacement fallback.</summary>
        public static Encoding TextEncoding(this CatalogTextEncoding encoding) =>
            encoding == CatalogTextEncoding.Latin1 ? StrictLatin1 : StrictUtf8;

        private static readonly Encoding StrictLatin1 = Encoding.GetEncoding(
            Encoding.Latin1.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);

        private static readonly Encoding StrictUtf8 = Encoding.GetEncoding(
            Encoding.UTF8.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);

        /// <summary>Classifies a file's raw bytes into its <see cref="CatalogTextEncoding"/> (BOM → Utf8Bom;
        /// else valid UTF-8 with a non-ASCII byte → Utf8; else Latin1).</summary>
        public static CatalogTextEncoding Classify(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (HasUtf8Bom(bytes))
            {
                return CatalogTextEncoding.Utf8Bom;
            }
            return IsNonAsciiUtf8(bytes) ? CatalogTextEncoding.Utf8 : CatalogTextEncoding.Latin1;
        }

        // A BOM-less file that decodes as valid UTF-8 AND carries a multi-byte sequence is genuine UTF-8; a
        // pure-ASCII file (or one with lone high bytes) is treated as Latin-1 (the .ifb default). This keeps a
        // Latin-1 file whose bytes happen to be ASCII-only from being mislabeled — either encoding reproduces it.
        private static bool IsNonAsciiUtf8(byte[] bytes)
        {
            // Ask the cheap question first: with no high byte the answer is Latin-1 whatever the UTF-8 validity, so
            // the pure-ASCII majority never pays a decode at all. Then validate in place — Utf8.IsValid inspects the
            // bytes without materializing a full-file string, and without the thrown-and-caught
            // DecoderFallbackException a genuine Latin-1 .ifb used to cost on every one of this method's three
            // callers per file.
            bool anyHighByte = false;
            foreach (byte b in bytes)
            {
                if (b >= 0x80)
                {
                    anyHighByte = true;
                    break;
                }
            }
            return anyHighByte && System.Text.Unicode.Utf8.IsValid(bytes);
        }
    }
}
