#nullable enable
using System;
using System.Collections.Immutable;
using System.Text;

using Ihc.Vis.Schema;
namespace Ihc.Vis.Io
{
    /// <summary>
    /// Captures a <c>.vis</c>/<c>.def</c>/<c>.ifb</c> file's inline DTD as per-type canonical blocks
    /// (tag → verbatim block) — the source of grammar for the open-world round-trip (a project's own DTD) and for
    /// inserting catalog components whose element types the static registry does not declare (a descriptor's DTD).
    /// The DOCTYPE internal subset is pure ASCII grammar, so decoding the raw bytes as Latin-1 (1 byte ↔ 1 char,
    /// lossless) and slicing out <c>[ … ]&gt;</c> is byte-faithful even when the body is mis-encoded — body high
    /// bytes never form a <c>]&gt;</c> before the DTD closes.
    /// </summary>
    internal static class InlineDtd
    {
        public static ImmutableDictionary<string, string> Capture(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            // The DOCTYPE internal subset is pure ASCII grammar, so Latin-1 (1 byte ↔ 1 char) slices it byte-faithfully
            // even when the body is mis-encoded.
            return CaptureFromText(Encoding.Latin1.GetString(bytes));
        }

        /// <summary>Captures the inline-DTD blocks from already-decoded text (e.g. a definition's captured
        /// <c>Head</c>), so a generated component that carries its verbatim header — but no raw bytes — can still
        /// resolve its own grammar for insert-time default materialization.</summary>
        public static ImmutableDictionary<string, string> CaptureFromText(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            int doctype = text.IndexOf("<!DOCTYPE", StringComparison.Ordinal);
            // The '[' must open before the DOCTYPE's own '>' — otherwise this is a subset-less DOCTYPE
            // (e.g. SYSTEM) and an unbounded search would scan the document body for stray brackets.
            int doctypeEnd = doctype >= 0 ? text.IndexOf('>', doctype) : -1;
            int open = doctype >= 0 ? text.IndexOf('[', doctype) : -1;
            if (open < 0 || (doctypeEnd >= 0 && open > doctypeEnd))
            {
                return ImmutableDictionary<string, string>.Empty;
            }
            int close = text.IndexOf("]>", open, StringComparison.Ordinal);
            if (close < 0)
            {
                return ImmutableDictionary<string, string>.Empty;
            }
            string subset = text.Substring(open + 1, close - (open + 1));
            var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (string block in ProjectSchemaRegistry.SplitBlocks(subset))
            {
                builder[ProjectSchemaRegistry.ReadTag(block)] = block;
            }
            AddOrphanAttlists(subset, builder);
            return builder.ToImmutable();
        }

        // Captures each ORPHAN <!ATTLIST tag …> — one with no matching <!ELEMENT tag> of its own — keyed under its own
        // tag with a synthesized <!ELEMENT tag ANY> so the schema resolves it. Catalog .def/.ifb files carry these
        // (e.g. a "med logning" product's resource_light/resource_enum), and their ATTLIST defaults (e.g. a light's
        // inivalue "500.00") drive insert-time default materialization — the .vis registry defaults them differently.
        // A tag that already has an <!ELEMENT> block is left untouched (SplitBlocks already captured its declarations).
        private static void AddOrphanAttlists(string subset, ImmutableDictionary<string, string>.Builder builder)
        {
            const string marker = "<!ATTLIST ";
            int i = subset.IndexOf(marker, StringComparison.Ordinal);
            while (i >= 0)
            {
                int nameStart = i + marker.Length;
                int end = DeclarationEnd(subset, nameStart);
                if (end < 0)
                {
                    break;
                }
                string tag = FirstToken(subset, nameStart, end);
                if (tag.Length > 0 && !builder.ContainsKey(tag))
                {
                    builder[tag] = $"   <!ELEMENT {tag} ANY>\r\n   {subset.Substring(i, end - i + 1)}\r\n";
                }
                i = subset.IndexOf(marker, end + 1, StringComparison.Ordinal);
            }
        }

        // The index of the quote-aware '>' that closes a declaration starting at <paramref name="start"/>, or -1.
        private static int DeclarationEnd(string s, int start)
        {
            char quote = '\0';
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    if (c == quote) { quote = '\0'; }
                }
                else if (c is '"' or '\'') { quote = c; }
                else if (c == '>') { return i; }
            }
            return -1;
        }

        private static string FirstToken(string s, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(s[start])) { start++; }
            int e = start;
            while (e < end && !char.IsWhiteSpace(s[e])) { e++; }
            return s.Substring(start, e - start);
        }
    }
}
