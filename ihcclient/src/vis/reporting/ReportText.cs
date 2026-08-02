#nullable enable
using System;
using System.Text;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Content-side text semantics shared by the report builders. The byte-fidelity engine preserves
    /// line breaks inside attribute values (e.g. an entity-encoded <c>&#38;#xD;&#38;#xA;</c> in a note), but
    /// the vendor's parse/render pipeline showed such text on ONE line — so single-line report slots
    /// (names, placering, tree note rows) collapse each run of line-break/tab characters to a single
    /// space. Multi-line slots (the FB report's paragraph blocks) keep their line structure and never
    /// use this.
    /// </summary>
    internal static class ReportText
    {
        /// <summary>The masthead/per-locality display placeholder for blank values (A1).</summary>
        private const string BlankPlaceholder = "--";

        /// <summary>Masthead-family display value (A1): <c>--</c> when blank, else the single-line text.</summary>
        public static string Display(string? value) =>
            string.IsNullOrWhiteSpace(value) ? BlankPlaceholder : SingleLine(value);

        /// <summary>
        /// The value with EVERY whitespace run (spaces, tabs, line breaks) collapsed to a single space and
        /// the ends trimmed — the function-block report's text treatment (the vendor pipeline rendered
        /// through HTML, which collapses runs). The other reports keep interior spacing verbatim and use
        /// <see cref="SingleLine"/> instead.
        /// </summary>
        public static string Collapse(string? value)
        {
            string result = SingleLine(value);
            if (result.Contains("  ", StringComparison.Ordinal))
            {
                var text = new StringBuilder(result.Length);
                bool inRun = false;
                foreach (char c in result)
                {
                    if (c == ' ')
                    {
                        if (!inRun)
                        {
                            text.Append(' ');
                            inRun = true;
                        }
                    }
                    else
                    {
                        text.Append(c);
                        inRun = false;
                    }
                }
                result = text.ToString();
            }
            return result.Trim();
        }

        /// <summary>The value as one line: each maximal run of CR/LF/TAB characters becomes one space.</summary>
        public static string SingleLine(string? value)
        {
            string result = value ?? string.Empty;
            if (result.AsSpan().IndexOfAny('\r', '\n', '\t') >= 0)
            {
                var text = new StringBuilder(result.Length);
                bool inBreak = false;
                foreach (char c in result)
                {
                    if (c is '\r' or '\n' or '\t')
                    {
                        if (!inBreak)
                        {
                            text.Append(' ');
                            inBreak = true;
                        }
                    }
                    else
                    {
                        text.Append(c);
                        inBreak = false;
                    }
                }
                result = text.ToString();
            }
            return result;
        }
    }
}
