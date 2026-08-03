#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// The generic HTML writer (spec D1/R7): renders a mode-filtered <see cref="ReportShapeDocument"/> to a
    /// self-contained <c>text/html</c> page — UTF-8 without BOM, LF line endings (S06). Owns ALL HTML format
    /// rules: the document skeleton, the fixed house banner with the <c>#icon-logo</c> reference (D7), the
    /// shared screen+print stylesheet, tree-list nesting/indentation, and the blank-line grammar between
    /// shapes. Everything data-derived is escaped here (R11 trust boundary); the only raw pass-throughs are
    /// the icon provider's fragments and definitions block, which are trusted caller markup by contract.
    /// </summary>
    internal static class HtmlReportWriter
    {
        private const string ProductName = "IHC OpenVisual";

        /// <summary>The banner's icon key; always part of the used-key set handed to the provider.</summary>
        private const string LogoKey = "logo";

        // The one shared stylesheet all reports embed (screen + @media print), byte-pinned by the 12 HTML
        // oracles — table/tree/icon rules included up front so every report kind renders from the same sheet.
        // A raw string literal keeps its SOURCE FILE's line endings, so the trailing ReplaceLineEndings is what
        // makes the emitted report LF-only (S06) whether this file is checked out with LF or CRLF.
        private static readonly string Stylesheet = """

            body { font-family: sans-serif; font-size: 14px; line-height: 1.5; margin: 24px; }
            .banner { display: flex; align-items: center; gap: 10px; background: #2455a4; color: #fff; padding: 10px 16px; }
            .banner-logo { width: 28px; height: 28px; color: #fff; }
            hr.divider { border: none; border-top: 4px solid #ccc; margin: 6px 0; }
            h2 { font-size: 16px; }
            .kicker, .report-meta { color: #666; font-size: 12px; }
            .kicker { margin: 0; }
            .report-meta { margin: 4px 0 16px; }
            .note { color: #666; }
            ul.tree { list-style: none; padding-left: 20px; }
            ul.tree.root { padding-left: 0; }
            .row { display: flex; flex-wrap: wrap; align-items: baseline; column-gap: 6px; }
            .icon { width: 14px; height: 14px; flex: none; align-self: flex-start; margin-top: 2px; }
            .name, .eq, .value { white-space: nowrap; }
            .name { font-weight: bold; }
            .value, .id { font-family: monospace; }
            .note { flex: 1 1 260px; }

            table { border-collapse: collapse; width: 100%; margin-bottom: 16px; }
            table.people, table.locality, table.datalines, table.meta { table-layout: fixed; }
            th, td { border: 1px solid #333; padding: 6px 10px; text-align: left; }
            tr.title-row th, .party-heading { text-align: center; }
            .table-scroll { overflow-x: auto; }
            .table-scroll table { min-width: 800px; }
            .id { font-size: 12px; font-weight: normal; color: #666; }

            @page { margin: 15mm 12mm; }
            h1, h2, h3, caption { break-after: avoid; page-break-after: avoid; }
            table.people, table.locality, table.meta { break-inside: avoid; page-break-inside: avoid; }
            tr, .row { break-inside: avoid; page-break-inside: avoid; }
            thead { display: table-header-group; }
            @media print {
              body { margin: 0; }
              .banner { background: none; color: #000; border-bottom: 2px solid #000; }
              .banner-logo { color: #000; }
              .table-scroll { overflow-x: visible; }
              .table-scroll table { min-width: 0; }
            }
            """.ReplaceLineEndings("\n");

        public static byte[] Write(ReportShapeDocument document, IReportIconProvider? iconProvider)
        {
            var html = new StringBuilder();
            string title = Escape(document.Title);
            html.Append("<!doctype html>\n<html lang=\"da\">\n<head>\n");
            html.Append("<meta charset=\"utf-8\">\n");
            html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
            html.Append($"<title>{title} &mdash; {ProductName}</title>\n");
            html.Append("<style>\n").Append(Stylesheet).Append("\n</style>\n</head>\n<body>\n");

            // The provider's once-per-document definitions block sits directly after <body> (R11); the
            // default provider contributes none and the page then simply has no sprite.
            string? definitions = iconProvider?.GetDefinitionsBlock(ReportMimeTypes.Html, UsedIconKeys(document));
            if (!string.IsNullOrEmpty(definitions))
            {
                html.Append(definitions).Append('\n');
            }

            html.Append("<header class=\"banner\">\n");
            html.Append("  <svg class=\"banner-logo\" aria-hidden=\"true\"><use href=\"#icon-logo\"/></svg>\n");
            html.Append($"  <strong>{ProductName}</strong>\n");
            html.Append("</header>\n");
            html.Append($"<h1>{title}</h1>\n");

            // Run function-block sections render as ONE source line per run: blobs concatenate with no
            // separator, the run is closed by the next shape (or the tail), and it takes the generic blank
            // separator only when it does not directly follow the h1. A standalone section renders as an
            // ordinary blank-separated block instead (the two witnessed layouts, keyed by the shape's own
            // layout flag).
            bool first = true;
            bool inSectionRun = false;
            foreach (ReportShape shape in document.Shapes)
            {
                if (shape is FbBlockShape { Standalone: false } runBlock)
                {
                    if (!inSectionRun && !first)
                    {
                        html.Append('\n');
                    }
                    AppendFbSection(html, runBlock, iconProvider);
                    inSectionRun = true;
                }
                else
                {
                    if (inSectionRun)
                    {
                        html.Append('\n');
                        inSectionRun = false;
                    }
                    // The FB report's first shape sits directly under the h1 (no blank) — full mode's
                    // meta line as much as std mode's section run.
                    if (!(first && document.TitleHugsFirstShape))
                    {
                        html.Append('\n');
                    }
                    if (shape is FbBlockShape block)
                    {
                        AppendFbSection(html, block, iconProvider);
                        html.Append('\n');
                    }
                    else
                    {
                        Append(html, shape);
                    }
                }
                first = false;
            }
            html.Append("\n</body>\n</html>\n");
            return Encoding.UTF8.GetBytes(html.ToString());
        }

        // Every icon key the document will reference: the banner's logo, plus the icon-tree rows' keys in
        // first-use order (the provider owns the sprite's canonical ordering).
        private static IReadOnlyCollection<string> UsedIconKeys(ReportShapeDocument document)
        {
            var keys = new List<string> { LogoKey };
            foreach (IconTreeRow row in document.Shapes.OfType<FbBlockShape>()
                .SelectMany(b => b.Rows).OfType<IconTreeRow>())
            {
                if (!keys.Contains(row.IconKey))
                {
                    keys.Add(row.IconKey);
                }
            }
            return keys;
        }

        // ----- function-block sections -----

        /// <summary>One function block as a single-line <c>&lt;section&gt;</c> blob: divider, heading
        /// (+Full chip), the fixed kicker + description paragraphs, a second divider, and the icon tree.
        /// FB text nodes escape with the quote-including variant (the FB oracles' authored form).</summary>
        private static void AppendFbSection(StringBuilder html, FbBlockShape block, IReportIconProvider? iconProvider)
        {
            html.Append("<section><hr class=\"divider\">");
            html.Append($"<h2>{EscapeFb(block.Heading)}{Chip(block.IdToken)}</h2>");
            if (!block.Identity.IsEmpty)
            {
                // The Full identity grid: an untitled meta table on its own lines inside the blob,
                // standard-escaped like the shared meta blocks.
                html.Append("\n<table class=\"meta\">\n");
                html.Append("  <colgroup><col style=\"width:25%\"><col></colgroup>\n");
                foreach (KeyValueRow row in block.Identity)
                {
                    html.Append($"  <tr><th>{Escape(row.Key)}</th><td>{Escape(row.Value)}</td></tr>\n");
                }
                html.Append("</table>\n");
            }
            if (!block.Paragraphs.IsEmpty)
            {
                html.Append("<p class=\"kicker\">Anvendelse</p>");
                foreach (FbParagraph paragraph in block.Paragraphs)
                {
                    html.Append(paragraph.IsNote ? "<p class=\"note\">" : "<p>")
                        .Append(EscapeFb(paragraph.Text)).Append("</p>");
                }
            }
            html.Append("<hr class=\"divider\">");
            (List<TreeNode> nodes, _) = BuildForest(block.Rows, 0, 0);
            AppendFbList(html, nodes, "tree root", iconProvider);
            // A standalone section closes on its own line; a run section's blob stays single-line.
            html.Append(block.Standalone ? "\n</section>" : "</section>");
        }

        private static void AppendFbList(StringBuilder html, List<TreeNode> nodes, string cssClass, IReportIconProvider? iconProvider)
        {
            html.Append($"<ul class=\"{cssClass}\">");
            foreach (TreeNode node in nodes)
            {
                var row = (IconTreeRow)node.Row;
                string icon = DefaultReportIcons.Resolve(iconProvider, ReportMimeTypes.Html, row.IconKey, out bool isRawFragment);
                html.Append("<li><div class=\"row\">")
                    .Append(isRawFragment ? icon : EscapeFb(icon))
                    .Append($"<span class=\"name\">{EscapeFb(row.Name)}</span>")
                    .Append(Chip(row.IdToken));
                if (row.Value is { } value)
                {
                    html.Append($" <span class=\"eq\">=</span> <span class=\"value\">{EscapeFb(value)}</span>");
                }
                if (row.Note is { } note)
                {
                    html.Append($" <span class=\"note\">{EscapeFb(note)}</span>");
                }
                html.Append("</div>");
                if (node.Children.Count > 0)
                {
                    AppendFbList(html, node.Children, "tree", iconProvider);
                }
                html.Append("</li>");
            }
            html.Append("</ul>");
        }

        /// <summary>FB-section text escaping (the FB oracles' authored form): quotes and apostrophes
        /// escape too (<c>&amp;quot;</c> / <c>&amp;#x27;</c>), unlike the rest of the document.</summary>
        private static string EscapeFb(string text) =>
            Escape(text).Replace("\"", "&quot;").Replace("'", "&#x27;");

        private static void Append(StringBuilder html, ReportShape shape)
        {
            switch (shape)
            {
                case MetaLineShape meta:
                    html.Append($"<p class=\"report-meta\">Fuld rapport &mdash; Genereret: {Escape(meta.GeneratedAt)}"
                        + $" &mdash; Programmør: {Escape(meta.Programmer)}</p>\n");
                    break;
                case KeyValueBlockShape { Style: KeyValueStyle.Meta } block:
                    html.Append("<table class=\"meta\">\n");
                    html.Append("  <colgroup><col style=\"width:25%\"><col></colgroup>\n");
                    html.Append($"  <tr class=\"title-row\"><th colspan=\"2\">{Escape(block.Heading)}</th></tr>\n");
                    foreach (KeyValueRow row in block.Rows)
                    {
                        html.Append($"  <tr><th>{Escape(row.Key)}</th><td>{Escape(row.Value)}{Chip(row.IdToken)}</td></tr>\n");
                    }
                    html.Append("</table>\n");
                    break;
                case KeyValueBlockShape { Style: KeyValueStyle.People } block:
                    html.Append("  <table class=\"people\">\n");
                    html.Append("    <colgroup><col style=\"width:25%\"><col></colgroup>\n");
                    html.Append($"    <tr><td></td><th class=\"party-heading\">{Escape(block.Heading)}</th></tr>\n");
                    foreach (KeyValueRow row in block.Rows)
                    {
                        html.Append($"    <tr><th>{Escape(row.Key)}</th><td>{Escape(row.Value)}{Chip(row.IdToken)}</td></tr>\n");
                    }
                    html.Append("  </table>\n");
                    break;
                case ComponentBlockShape component:
                    AppendComponentBlock(html, component);
                    break;
                case TreeShape tree:
                    AppendTree(html, tree.Rows);
                    break;
                case SectionBreakShape sectionBreak:
                    string breakIndent = sectionBreak.Style == SectionBreakStyle.Indented ? "  " : string.Empty;
                    html.Append(breakIndent).Append("<hr class=\"divider\">\n");
                    html.Append(breakIndent).Append($"<h2>{Escape(sectionBreak.Heading)}</h2>\n");
                    break;
                case TableShape table:
                    AppendTable(html, table);
                    break;
                default:
                    throw new NotSupportedException($"The HTML writer has no rendering for shape '{shape.GetType().Name}'.");
            }
        }

        // ----- tables -----

        // The datalines colgroup: fixed column percentages, split over two source lines like the header
        // and data cells (5 columns, then 6).
        private const string DatalinesColgroupFirst =
            "<col style=\"width:5%\"><col style=\"width:18%\"><col style=\"width:16%\"><col style=\"width:6%\"><col style=\"width:12%\">";
        private const string DatalinesColgroupSecond =
            "<col style=\"width:6%\"><col style=\"width:6%\"><col style=\"width:7%\"><col style=\"width:8%\"><col style=\"width:7%\"><col style=\"width:9%\">";

        private static void AppendTable(StringBuilder html, TableShape table)
        {
            switch (table.Style)
            {
                case TableStyle.Plain:
                    html.Append("<table>\n");
                    AppendRow(html, "  ", HeaderCells(table.Columns));
                    foreach (ImmutableArray<ReportCell> row in table.Rows)
                    {
                        AppendRow(html, "  ", DataCells(row));
                    }
                    html.Append("</table>\n");
                    break;
                case TableStyle.Module:
                    html.Append("  <table>\n    <thead>\n");
                    html.Append($"    <tr class=\"title-row\"><th colspan=\"{table.Columns.Length}\">{Escape(table.Heading!)}</th></tr>\n");
                    AppendRow(html, "    ", HeaderCells(table.Columns));
                    html.Append("    </thead>\n");
                    if (!table.Rows.IsEmpty)
                    {
                        html.Append("    <tbody>\n");
                        foreach (ImmutableArray<ReportCell> row in table.Rows)
                        {
                            AppendRow(html, "    ", DataCells(row));
                        }
                        html.Append("    </tbody>\n");
                    }
                    html.Append("  </table>\n");
                    break;
                case TableStyle.Datalines:
                    AppendScrollTable(html, table, "<table class=\"datalines\">", firstChunk: 5, colgroup: true);
                    break;
                case TableStyle.Special:
                    AppendScrollTable(html, table, "<table>", firstChunk: 6, colgroup: false);
                    break;
                default:    // TableStyle.S0
                    AppendScrollTable(html, table, "<table>", firstChunk: 5, colgroup: false);
                    break;
            }
        }

        // The wide flat tables: div.table-scroll wrapper, thead with a title row, header and data cells
        // split over two source lines at the family's fixed chunk boundary; no tbody element when empty.
        private static void AppendScrollTable(StringBuilder html, TableShape table, string tableTag, int firstChunk, bool colgroup)
        {
            html.Append("  <div class=\"table-scroll\">\n    ").Append(tableTag).Append('\n');
            if (colgroup)
            {
                html.Append("      <colgroup>\n");
                html.Append("        ").Append(DatalinesColgroupFirst).Append('\n');
                html.Append("        ").Append(DatalinesColgroupSecond).Append('\n');
                html.Append("      </colgroup>\n");
            }
            html.Append("      <thead>\n");
            html.Append($"      <tr class=\"title-row\"><th colspan=\"{table.Columns.Length}\">{Escape(table.Heading!)}</th></tr>\n");
            html.Append("      <tr>\n");
            AppendCellChunks(html, HeaderCells(table.Columns), firstChunk);
            html.Append("      </tr>\n      </thead>\n");
            if (!table.Rows.IsEmpty)
            {
                html.Append("      <tbody>\n");
                foreach (ImmutableArray<ReportCell> row in table.Rows)
                {
                    html.Append("      <tr>\n");
                    AppendCellChunks(html, DataCells(row), firstChunk);
                    html.Append("      </tr>\n");
                }
                html.Append("      </tbody>\n");
            }
            html.Append("    </table>\n  </div>\n");
        }

        private static void AppendCellChunks(StringBuilder html, IEnumerable<string> renderedCells, int firstChunk)
        {
            var cells = renderedCells.ToList();
            html.Append("        ").AppendJoin(string.Empty, cells.Take(firstChunk)).Append('\n');
            html.Append("        ").AppendJoin(string.Empty, cells.Skip(firstChunk)).Append('\n');
        }

        // One table.locality per component (A8/A9): the field rows spanning both value columns, then —
        // when the product has terminals — the all-header terminal row and its data rows in the same table.
        private static void AppendComponentBlock(StringBuilder html, ComponentBlockShape component)
        {
            html.Append("  <table class=\"locality\">\n");
            html.Append("    <colgroup><col style=\"width:25%\"><col><col></colgroup>\n");
            foreach (KeyValueRow field in component.Fields)
            {
                html.Append($"    <tr><th>{Escape(field.Key)}</th><td colspan=\"2\">{Escape(field.Value)}{Chip(field.IdToken)}</td></tr>\n");
            }
            if (component.Terminals is { } terminals)
            {
                AppendRow(html, "    ", HeaderCells(terminals.Columns));
                foreach (ImmutableArray<ReportCell> row in terminals.Rows)
                {
                    AppendRow(html, "    ", DataCells(row));
                }
            }
            html.Append("  </table>\n");
        }

        /// <summary>One <c>&lt;tr&gt;</c> source line: the style's indent, the joined cells, the row close.</summary>
        private static void AppendRow(StringBuilder html, string indent, IEnumerable<string> cells) =>
            html.Append(indent).Append("<tr>").AppendJoin(string.Empty, cells).Append("</tr>\n");

        private static IEnumerable<string> HeaderCells(ImmutableArray<string> columns) =>
            columns.Select(c => $"<th>{Escape(c)}</th>");

        private static IEnumerable<string> DataCells(ImmutableArray<ReportCell> row) => row.Select(Cell);

        /// <summary>One data cell: escaped text plus the Full-only id chip when present.</summary>
        private static string Cell(ReportCell cell) => $"<td>{Escape(cell.Text)}{Chip(cell.IdToken)}</td>";

        // ----- tree lists -----

        private sealed record TreeNode(ReportTreeRow Row, List<TreeNode> Children);

        private static void AppendTree(StringBuilder html, ImmutableArray<ReportTreeRow> rows)
        {
            (List<TreeNode> nodes, _) = BuildForest(rows, 0, 0);
            AppendList(html, nodes, 0, "tree root");
        }

        // Rows arrive pre-ordered with +1 depth steps into children; rebuild the explicit forest once so
        // list rendering is a plain recursion.
        private static (List<TreeNode> Nodes, int Next) BuildForest(ImmutableArray<ReportTreeRow> rows, int index, int depth)
        {
            var nodes = new List<TreeNode>();
            int i = index;
            while (i < rows.Length && rows[i].Depth == depth)
            {
                (List<TreeNode> children, int next) = BuildForest(rows, i + 1, depth + 1);
                nodes.Add(new TreeNode(rows[i], children));
                i = next;
            }
            return (nodes, i);
        }

        private static void AppendList(StringBuilder html, List<TreeNode> nodes, int indent, string cssClass)
        {
            string pad = new(' ', indent);
            html.Append(pad).Append($"<ul class=\"{cssClass}\">\n");
            foreach (TreeNode node in nodes)
            {
                html.Append(pad).Append("  ").Append(LiOpen(node.Row)).Append(RowContent(node.Row));
                if (node.Children.Count == 0)
                {
                    html.Append("</li>\n");
                }
                else
                {
                    html.Append('\n');
                    AppendList(html, node.Children, indent + 4, "tree");
                    html.Append(pad).Append("  </li>\n");
                }
            }
            html.Append(pad).Append("</ul>\n");
        }

        private static string LiOpen(ReportTreeRow row) =>
            row is NoteTreeRow ? "<li class=\"note\">" : "<li>";

        private static string RowContent(ReportTreeRow row) => row switch
        {
            NamedTreeRow named => $"<span class=\"name\">{Escape(named.Name)}</span>{Chip(named.IdToken)}"
                + (named.Detail is null ? string.Empty : " " + Escape(named.Detail)),
            PlainTreeRow plain => Escape(plain.Text) + Chip(plain.IdToken),
            NoteTreeRow note => Escape(note.Text)
                + (note.LocalitySuffix is null ? string.Empty : $" ({Escape(note.LocalitySuffix)})"),
            _ => throw new NotSupportedException($"The HTML writer has no rendering for row '{row.GetType().Name}'."),
        };

        private static string Chip(string? idToken) =>
            idToken is null ? string.Empty : $" <span class=\"id\">(ID {Escape(idToken)})</span>";

        /// <summary>Text-node escaping for everything data-derived (R11: only icon fragments are raw).</summary>
        private static string Escape(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
