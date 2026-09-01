using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// The generic plain-text writer (spec D1/R7): renders a mode-filtered <see cref="ReportShapeDocument"/>
    /// to <c>text/plain</c> bytes — UTF-8 without BOM, LF line endings (S06). Owns ALL text format rules:
    /// the fixed house banner, the <c>=</c>-underlined title, two-space tree indentation, the
    /// <c>(ID …)</c> chip and <c>(locality)</c> suffix syntax, blank-line separation between shapes, and
    /// the no-trailing-blank EOF rule. Every emitted line is right-trimmed. Text is emitted verbatim
    /// (attribute values are already decoded in the model; plain text needs no escaping).
    /// </summary>
    internal static class TextReportWriter
    {
        private const string Banner = "IHC OpenVisual";

        /// <summary>The full-width section rule (matches every oracle's 78-dash divider).</summary>
        private const int SeparatorWidth = 78;

        /// <summary>Gap between a key/value grid's key column (and between table columns) in spaces.</summary>
        private const int ColumnGap = 2;

        public static byte[] Write(ReportShapeDocument document, IReportIconProvider? iconProvider)
        {
            var text = new StringBuilder();
            Line(text, Banner);
            Line(text, string.Empty);
            Line(text, document.Title);
            Line(text, new string('=', document.Title.Length));

            foreach (ReportShape shape in document.Shapes)
            {
                if (shape is not MetaLineShape)
                {
                    Line(text, string.Empty);   // shapes are blank-separated; the meta line hugs the underline
                }
                Append(text, shape, iconProvider);
            }
            return Encoding.UTF8.GetBytes(text.ToString());
        }

        private static void Append(StringBuilder text, ReportShape shape, IReportIconProvider? iconProvider)
        {
            switch (shape)
            {
                case MetaLineShape meta:
                    Line(text, $"Fuld rapport — Genereret: {meta.GeneratedAt} — Programmør: {meta.Programmer}");
                    break;
                case KeyValueBlockShape block:
                    AppendKeyValueBlock(text, block);
                    break;
                case ComponentBlockShape component:
                    AppendComponentBlock(text, component);
                    break;
                case TreeShape tree:
                    foreach (ReportTreeRow row in tree.Rows)
                    {
                        Line(text, new string(' ', row.Depth * 2) + Render(row));
                    }
                    break;
                case SectionBreakShape sectionBreak:
                    Line(text, new string('-', SeparatorWidth));
                    Line(text, sectionBreak.Heading);
                    break;
                case FbBlockShape block:
                    AppendFbBlock(text, block, iconProvider);
                    break;
                case TableShape table:
                    AppendTable(text, table, margin: 2);
                    break;
                default:
                    throw new NotSupportedException($"The text writer has no rendering for shape '{shape.GetType().Name}'.");
            }
        }

        // Heading at column 0; rows indented two, keys padded to the widest key plus the column gap.
        private static void AppendKeyValueBlock(StringBuilder text, KeyValueBlockShape block)
        {
            Line(text, block.Heading);
            AppendKeyValueRows(text, block.Rows, indent: 2);
        }

        // A component block (A8/A9): the field grid without a heading, then — for a dataline product with
        // terminals — a blank line and the terminal sub-table nested one level deeper.
        private static void AppendComponentBlock(StringBuilder text, ComponentBlockShape component)
        {
            AppendKeyValueRows(text, component.Fields, indent: 2);
            if (component.Terminals is { } terminals)
            {
                Line(text, string.Empty);
                AppendTable(text, terminals, margin: 4);
            }
        }

        private static void AppendKeyValueRows(StringBuilder text, ImmutableArray<KeyValueRow> rows, int indent)
        {
            int keyWidth = rows.Max(r => r.Key.Length) + ColumnGap;
            foreach (KeyValueRow row in rows)
            {
                Line(text, new string(' ', indent) + row.Key.PadRight(keyWidth) + row.Value + Chip(row.IdToken));
            }
        }

        // Optional heading at column 0 directly above; margin-indented column grid with widths driven by
        // header and cell content; dash underline row; right-trimming (Line) keeps the last column unpadded.
        // Widths are computed over the RENDERED cell text (Full-mode chip included), so a chip widens its
        // column exactly as the oracles witness.
        private static void AppendTable(StringBuilder text, TableShape table, int margin)
        {
            if (table.Heading is { } heading)
            {
                Line(text, heading);
            }
            List<string[]> rendered = table.Rows
                .Select(row => row.Select(c => c.Text + Chip(c.IdToken)).ToArray())
                .ToList();
            int[] widths = table.Columns.Select(c => c.Length).ToArray();
            foreach (string[] row in rendered)
            {
                for (int i = 0; i < widths.Length; i++)
                {
                    widths[i] = Math.Max(widths[i], row[i].Length);
                }
            }
            Line(text, TableLine(table.Columns, widths, margin));
            Line(text, TableLine(widths.Select(w => new string('-', w)), widths, margin));
            foreach (string[] row in rendered)
            {
                Line(text, TableLine(row, widths, margin));
            }
        }

        private static string TableLine(IEnumerable<string> cells, int[] widths, int margin) =>
            new string(' ', margin) + string.Concat(cells.Select((cell, i) => cell.PadRight(widths[i] + ColumnGap)));

        /// <summary>The fixed kicker label above a function block's description paragraphs.</summary>
        private const string FbKicker = "Anvendelse";

        // One function block (§7 of the text-format rules): dash rule, heading (+Full chip), the optional
        // kicker + paragraph run, a second dash rule, a blank, then the icon tree.
        private static void AppendFbBlock(StringBuilder text, FbBlockShape block, IReportIconProvider? iconProvider)
        {
            Line(text, new string('-', SeparatorWidth));
            Line(text, block.Heading + Chip(block.IdToken));
            if (!block.Identity.IsEmpty)
            {
                Line(text, string.Empty);
                AppendKeyValueRows(text, block.Identity, indent: 2);
            }
            if (!block.Paragraphs.IsEmpty)
            {
                Line(text, string.Empty);
                Line(text, FbKicker);
                foreach (FbParagraph paragraph in block.Paragraphs)
                {
                    Line(text, paragraph.Text);
                }
            }
            Line(text, new string('-', SeparatorWidth));
            Line(text, string.Empty);
            AppendIconTree(text, block.Rows, iconProvider);
        }

        // The icon-tree layout: a fixed-width icon column (depth-0 icons pad to 2), then per-BLOCK
        // alignment columns — E for the `=`/note column (floored at 24) computed over annotated rows
        // only, and N for the note column of rows carrying both a value and a note.
        private static void AppendIconTree(StringBuilder text, ImmutableArray<ReportTreeRow> rows, IReportIconProvider? iconProvider)
        {
            var lines = rows.Cast<IconTreeRow>()
                .Select(row =>
                {
                    string icon = DefaultReportIcons.Resolve(iconProvider, ReportMimeTypes.PlainText, row.IconKey, out _);
                    string label = new string(' ', row.Depth + 1)
                        + (row.Depth == 0 ? icon.PadRight(2) : icon)
                        + " " + row.Name + Chip(row.IdToken);
                    return (Label: label, row.Value, row.Note);
                })
                .ToList();

            var annotated = lines.Where(l => l.Value is not null || l.Note is not null).ToList();
            int eqColumn = Math.Max(24, annotated.Count == 0 ? 0 : annotated.Max(l => l.Label.Length) + 1);
            var both = lines.Where(l => l.Value is not null && l.Note is not null).ToList();
            int noteColumn = both.Count == 0 ? 0 : both.Max(l => eqColumn + 2 + l.Value!.Length) + 2;

            foreach ((string label, string? value, string? note) in lines)
            {
                Line(text, (value, note) switch
                {
                    (not null, null) => label.PadRight(eqColumn) + "= " + value,
                    (not null, not null) => (label.PadRight(eqColumn) + "= " + value).PadRight(noteColumn) + note,
                    (null, not null) => label.PadRight(eqColumn + 1) + note,
                    _ => label,
                });
            }
        }

        private static string Render(ReportTreeRow row) => row switch
        {
            NamedTreeRow named => named.Name + Chip(named.IdToken) + (named.Detail is null ? string.Empty : " " + named.Detail),
            PlainTreeRow plain => plain.Text + Chip(plain.IdToken),
            NoteTreeRow note => note.Text + (note.LocalitySuffix is null ? string.Empty : $" ({note.LocalitySuffix})"),
            _ => throw new NotSupportedException($"The text writer has no rendering for row '{row.GetType().Name}'."),
        };

        private static string Chip(string? idToken) => idToken is null ? string.Empty : $" (ID {idToken})";

        // Every line is right-trimmed and LF-terminated — never Environment.NewLine (S06 pins LF on all OSes).
        private static void Line(StringBuilder text, string content) =>
            text.Append(content.TrimEnd(' ')).Append('\n');
    }
}
