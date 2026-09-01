using System;
using System.Collections.Generic;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Reads a generated PLAIN-TEXT report back as data, for the reporting tests that assert model-to-report
    /// coverage (the generality sweep and the scope probes). Parsing the text form is stable because 12
    /// oracles pin it byte-for-byte: a tree row is indented <c>depth + 1</c> spaces, a table is heading /
    /// column row / dash rule / rows-until-blank, and Full mode chips every element that owns a row with
    /// <c>(ID _0x…)</c> — an exact, collision-free "this element's row was emitted" probe, where a name
    /// substring collides with any other row that happens to contain the same text.
    /// </summary>
    internal static class ReportProbe
    {
        /// <summary>The full-width rule the writer separates sections and function blocks with.</summary>
        public static readonly string Rule = new('-', 78);

        public static string Chip(string idToken) => $"(ID {idToken})";

        /// <summary>Whether the element carrying <paramref name="idToken"/> owns a row in the report.</summary>
        public static bool Renders(string report, string idToken) =>
            report.Contains(Chip(idToken), StringComparison.Ordinal);

        /// <summary>How many rows the element carrying <paramref name="idToken"/> owns — more than one means
        /// it was listed under two localities.</summary>
        public static int RenderCount(string report, string idToken) => Occurrences(report, Chip(idToken));

        public static int Occurrences(string text, string token)
        {
            int count = 0;
            for (int at = text.IndexOf(token, StringComparison.Ordinal); at >= 0;
                 at = text.IndexOf(token, at + token.Length, StringComparison.Ordinal))
            {
                count++;
            }
            return count;
        }

        /// <summary>
        /// Whether the functions report renders a tree row of exactly <paramref name="text"/> at
        /// <paramref name="depth"/> (that writer indents two spaces per level and right-trims). A whole-LINE
        /// match, because the alternative — a name substring — collides with any row containing the same
        /// text, and short terminal names like "Op" collide with almost everything.
        /// <para>This is the Standard-mode probe. The <c>(ID _0x…)</c> chip is a Full-only FIELD that the
        /// mode filter strips, so chip membership says nothing about a Standard rendering.</para>
        /// </summary>
        public static bool HasTreeLine(string report, int depth, string text) =>
            report.Split('\n').Any(line => line == new string(' ', depth * 2) + text.TrimEnd());

        /// <summary>A tree row's depth from its indentation, or -1 for a line that is not a tree row.</summary>
        public static int RowDepth(string line)
        {
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ')
            {
                indent++;
            }
            return indent > 0 && indent < line.Length ? indent - 1 : -1;
        }

        /// <summary>The label of a tree row (everything after its icon column), or null for a non-row line.</summary>
        public static string? RowLabel(string line)
        {
            string? label = null;
            if (RowDepth(line) >= 0)
            {
                string content = line.TrimStart(' ');
                int icon = content.IndexOf(' ');
                label = icon < 0 ? string.Empty : content[(icon + 1)..].TrimStart(' ');
            }
            return label;
        }

        /// <summary>The installation report from its "Datalinjer" section break on — the flat cross-reference
        /// tables, excluding the per-locality component blocks above them.</summary>
        public static string CrossReferenceSection(string report)
        {
            int start = report.IndexOf("\nDatalinjer\n", StringComparison.Ordinal);
            return start < 0 ? string.Empty : report[start..];
        }

        /// <summary>The rendered row count of the text table under <paramref name="heading"/>: the heading line
        /// is followed by the column row and its dash rule, then one line per row until the blank line that
        /// separates the next shape. -1 when the table is absent, so it never equals a model count.</summary>
        public static int TableRowCount(string report, string heading)
        {
            string[] lines = report.Split('\n');
            int start = Array.IndexOf(lines, heading);
            int rows = -1;
            if (start >= 0)
            {
                rows = 0;
                for (int line = start + 3; line < lines.Length && lines[line].Length > 0; line++)
                {
                    rows++;
                }
            }
            return rows;
        }

        /// <summary>The rendered rows of the text table under <paramref name="heading"/>, in render order.</summary>
        public static string[] TableRows(string report, string heading)
        {
            string[] lines = report.Split('\n');
            int start = Array.IndexOf(lines, heading);
            return start < 0
                ? []
                : [.. lines.Skip(start + 3).TakeWhile(line => line.Length > 0)];
        }

        /// <summary>
        /// The rows of the Full-mode "Fejl i dokumentation" appendix, split into their four cells. The cells
        /// are sliced at the column spans of the table's own dash rule rather than on whitespace runs: the
        /// appendix leaves non-applicable cells BLANK, and splitting on whitespace would swallow an empty cell
        /// and shift every column after it — which is exactly the mistake a four-cell assertion exists to catch.
        /// </summary>
        public static string[][] AppendixRows(string report)
        {
            string[] lines = report.Split('\n');
            int at = Array.IndexOf(lines, "Fejl i dokumentation");
            string[][] rows = [];
            if (at >= 0 && at + 3 < lines.Length)
            {
                // the section-break heading, a blank, the column row, then the dash rule at +3
                (int Start, int Length)[] columns = [.. ColumnSpans(lines[at + 3])];
                rows = [.. lines.Skip(at + 4).TakeWhile(line => line.Length > 0).Select(line => Cells(line, columns))];
            }
            return rows;
        }

        private static IEnumerable<(int Start, int Length)> ColumnSpans(string dashRule)
        {
            for (int at = 0; at < dashRule.Length;)
            {
                int start = dashRule.IndexOf('-', at);
                if (start < 0)
                {
                    break;
                }
                int end = start;
                while (end < dashRule.Length && dashRule[end] == '-')
                {
                    end++;
                }
                yield return (start, end - start);
                at = end;
            }
        }

        // Rows are right-trimmed, so a trailing blank cell is off the end of the line rather than padded.
        private static string[] Cells(string row, (int Start, int Length)[] columns) =>
        [
            .. columns.Select(column => column.Start >= row.Length
                ? string.Empty
                : row.Substring(column.Start, Math.Min(column.Length, row.Length - column.Start)).Trim()),
        ];

        /// <summary>A table row's first column: the text before the gap that separates it from the next
        /// column (columns are padded apart by at least two spaces).</summary>
        public static string FirstCell(string row)
        {
            string cells = row.TrimStart(' ');
            int gap = cells.IndexOf("  ", StringComparison.Ordinal);
            return gap < 0 ? cells : cells[..gap];
        }

        /// <summary>The <c>= value</c> a tree row whose label starts with <paramref name="name"/> carries, or
        /// null when it carries none. (For a row that also carries a note, the note is part of the returned
        /// text — the probes that use this assert on note-free rows.)</summary>
        public static string? RowValue(string report, string name)
        {
            string? value = null;
            foreach (string line in report.Split('\n'))
            {
                if (RowLabel(line)?.StartsWith(name, StringComparison.Ordinal) == true)
                {
                    int after = line.IndexOf(name, StringComparison.Ordinal) + name.Length;
                    int equals = line.IndexOf("= ", after, StringComparison.Ordinal);
                    value = equals < 0 ? null : line[(equals + 2)..].Trim();
                    break;
                }
            }
            return value;
        }

        /// <summary>
        /// How many child rows each of a function block's rendered section rows carries. A block's tree is the
        /// run between the rule that closes its description and the next rule (the following block, or the
        /// findings appendix); within it a section is a depth-0 row and its children are the depth-1 rows that
        /// follow it.
        /// </summary>
        /// <param name="headingLine">The block's heading line exactly as rendered — <c>name (ID …)</c>
        /// in Full, the bare <c>name</c> in Standard, where the mode filter has stripped the chip.</param>
        public static int[] SectionChildCounts(string report, string headingLine)
        {
            string[] lines = report.Split('\n');
            var counts = new List<int>();
            int line = Array.IndexOf(lines, headingLine);
            if (line >= 0)
            {
                while (++line < lines.Length && lines[line] != Rule)
                {
                    // the identity grid and the description paragraphs, skipped wholesale
                }
                while (++line < lines.Length && lines[line] != Rule)
                {
                    switch (RowDepth(lines[line]))
                    {
                        case 0:
                            counts.Add(0);
                            break;
                        case 1 when counts.Count > 0:
                            counts[^1]++;
                            break;
                    }
                }
            }
            return counts.ToArray();
        }
    }
}
