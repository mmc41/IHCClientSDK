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

        private static string? DecodeCharRef(string entity)
        {
            if (entity.Length < 2 || entity[0] != '#')
            {
                return null;
            }
            bool hex = entity[1] is 'x' or 'X';
            ReadOnlySpan<char> digits = entity.AsSpan(hex ? 2 : 1);
            return int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int code)
                && code is >= 0 and <= 0x10FFFF
                    ? char.ConvertFromUtf32(code)
                    : null;
        }
    }
}
