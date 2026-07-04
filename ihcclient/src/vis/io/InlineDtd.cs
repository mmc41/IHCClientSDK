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
            string text = Encoding.Latin1.GetString(bytes);
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
            return builder.ToImmutable();
        }
    }
}
