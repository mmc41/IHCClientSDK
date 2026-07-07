#nullable enable
using System.Text;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// Renders a string as a C# double-quoted literal for the generated builder source. The catalog carries Danish
    /// display names (æøå) which the repo's <c>.cs</c> files hold as literal UTF-8 (the generated files are UTF-8 too),
    /// so only the structural characters — the quote, the backslash and the ASCII control chars — are escaped; every
    /// other code point, including the Latin-1 letters, is emitted verbatim. This keeps the generated names readable
    /// and the file diff-stable regardless of the platform's default console/file encoding.
    /// </summary>
    internal static class CSharpLiteral
    {
        /// <summary>Returns <paramref name="value"/> as a quoted C# string literal (e.g. <c>a"b</c> → <c>"a\"b"</c>).</summary>
        public static string Quote(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }
    }
}
