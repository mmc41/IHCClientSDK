#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// Decoding of the five predefined XML entities and numeric character references in attribute/default text —
    /// the single implementation shared by the schema registry's DTD-block parsing and the catalog grammar model
    /// (<see cref="GrammarAttr"/>), so "decoded value" can never mean two different things.
    /// </summary>
    internal static class XmlText
    {
        /// <summary>Decodes <c>&amp;amp; &amp;lt; &amp;gt; &amp;quot; &amp;apos;</c> and numeric character
        /// references; an unrecognized <c>&amp;…;</c> sequence stays verbatim.</summary>
        public static string Unescape(string s)
        {
            if (s.IndexOf('&') < 0)
            {
                return s;
            }
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '&')
                {
                    sb.Append(c);
                    continue;
                }
                int semi = s.IndexOf(';', i + 1);
                string? decoded = semi < 0 ? null : s.Substring(i + 1, semi - i - 1) switch
                {
                    "amp" => "&",
                    "lt" => "<",
                    "gt" => ">",
                    "quot" => "\"",
                    "apos" => "'",
                    string entity => DecodeCharRef(entity),
                };
                if (decoded is null)
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(decoded);
                    i = semi;
                }
            }
            return sb.ToString();
        }

        /// <summary>True when every <c>&amp;</c> in <paramref name="s"/> begins one of the five predefined entities
        /// or a numeric character reference — i.e. the text contains no <em>bare</em> ampersand that would make an
        /// emitted attribute or default literal malformed.</summary>
        public static bool HasOnlyWellFormedReferences(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '&')
                {
                    continue;
                }
                int semi = s.IndexOf(';', i + 1);
                if (semi < 0)
                {
                    return false;
                }
                string entity = s.Substring(i + 1, semi - i - 1);
                bool known = entity is "amp" or "lt" or "gt" or "quot" or "apos" || DecodeCharRef(entity) is not null;
                if (!known)
                {
                    return false;
                }
                i = semi;
            }
            return true;
        }

        /// <summary>
        /// Appends <paramref name="value"/> to <paramref name="sb"/> with the <c>.vis</c>/<c>.def</c> XML escaping —
        /// the encode peer of <see cref="Unescape"/>: the specials <c>&amp; &lt; &gt; &quot;</c> (and, when
        /// <paramref name="escapeApostrophe"/>, the apostrophe as <c>&amp;apos;</c>) plus numeric refs for the
        /// whitespace control chars CR/LF/TAB; any other char below U+0020 is refused (XML 1.0 cannot represent it,
        /// so a raw write would open in no parser). The apostrophe flag is the sole difference between the project
        /// serializer (escapes it) and the catalog writer (leaves it literal per the corpus-measured vendor rule, D3).
        /// </summary>
        public static void AppendEscaped(StringBuilder sb, string value, bool escapeApostrophe)
        {
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'' when escapeApostrophe: sb.Append("&apos;"); break;
                    case '\r': sb.Append("&#xD;"); break;
                    case '\n': sb.Append("&#xA;"); break;
                    case '\t': sb.Append("&#x9;"); break;   // a raw tab silently becomes a space on re-read (§3.3.3)
                    default:
                        if (c < 0x20)
                        {
                            // XML 1.0 forbids these outright — written raw, the file opens in no parser.
                            throw new InvalidOperationException(
                                $"Attribute value contains control character U+{(int)c:X4}, which XML cannot " +
                                "represent; remove it from the offending text.");
                        }
                        sb.Append(c);
                        break;
                }
            }
        }

        /// <summary>
        /// The index of the <c>]</c> that closes a DTD internal subset, scanning quote- AND comment-aware from
        /// <paramref name="start"/> (just inside the subset): a quoted default literal may hold <c>]&gt;</c> and a
        /// comment may hold a lone apostrophe, a <c>]</c> or a <c>&gt;</c> (e.g. <c>&lt;!-- Peter's dimmer --&gt;</c>
        /// would flip a naive quote tracker), so both are skipped wholesale. <paramref name="afterEnd"/> receives the
        /// index just past the terminating <c>&gt;</c>. With <paramref name="allowWhitespaceBeforeGt"/> the
        /// vendor-tolerant <c>]</c> + whitespace + <c>&gt;</c> form also terminates (catalog <c>.def</c>/<c>.ifb</c>
        /// heads); otherwise only an adjacent <c>]&gt;</c> does (<c>.vis</c> capture). Returns -1 (and -1 in
        /// <paramref name="afterEnd"/>) when the subset never closes, including an unterminated comment.
        /// </summary>
        public static int FindDtdSubsetClose(string s, int start, bool allowWhitespaceBeforeGt, out int afterEnd)
        {
            afterEnd = -1;
            char quote = '\0';
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    if (c == quote) { quote = '\0'; }
                    continue;
                }
                if (c == '<' && i + 3 < s.Length && s[i + 1] == '!' && s[i + 2] == '-' && s[i + 3] == '-')
                {
                    int end = s.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        return -1;   // unterminated comment → no valid subset end
                    }
                    i = end + 2;     // resume just after the closing "-->" (the loop's i++ steps past it)
                }
                else if (c is '"' or '\'')
                {
                    quote = c;
                }
                else if (c == ']')
                {
                    int gt = i + 1;
                    while (allowWhitespaceBeforeGt && gt < s.Length && char.IsWhiteSpace(s[gt])) { gt++; }
                    if (gt < s.Length && s[gt] == '>')
                    {
                        afterEnd = gt + 1;
                        return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// T027: the index of the quote-aware <c>&gt;</c> that closes a markup declaration (e.g. an
        /// <c>&lt;!ATTLIST&gt;</c>) starting at <paramref name="start"/> — a single- or double-quoted default literal
        /// may itself hold a <c>&gt;</c>, so quoted spans are skipped. Returns -1 when no closing <c>&gt;</c> is found.
        /// The one shared declaration-close scan behind the schema registry's ATTLIST parse and the inline-DTD
        /// orphan-ATTLIST capture (each supplies its own not-found behaviour).
        /// </summary>
        public static int FindDeclarationClose(string s, int start)
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

        private static string? DecodeCharRef(string entity)
        {
            if (entity.Length < 2 || entity[0] != '#')
            {
                return null;
            }
            bool hex = entity[1] is 'x' or 'X';
            ReadOnlySpan<char> digits = entity.AsSpan(hex ? 2 : 1);
            // Reject surrogate code points (0xD800–0xDFFF) alongside out-of-range ones: they are not valid Unicode
            // scalars, and char.ConvertFromUtf32 throws (not returns) on them — so a lone "&#xD800;" in a DTD
            // default must decode to null (a malformed/verbatim ref), never crash the schema/catalog parse untyped.
            return int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int code)
                && code is >= 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)
                    ? char.ConvertFromUtf32(code)
                    : null;
        }
    }
}
