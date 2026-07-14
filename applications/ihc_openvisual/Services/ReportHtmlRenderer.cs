using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Ihc.Vis.Reporting;

namespace ihc_openvisual.Services;

/// <summary>One variable section of a function block in the FB documentation report (US-041): its label and the
/// names of the variables it holds.</summary>
public sealed record FbReportSection(string Label, ImmutableArray<string> Variables);

/// <summary>One function block in the FB documentation report (US-041): its name and its variable sections.</summary>
public sealed record FbReportBlock(string Name, ImmutableArray<FbReportSection> Sections);

/// <summary>The function-block ("Functionsblok dokumentation") report model (US-041). A minimal listing —
/// blocks in Installation/Functions-pane document order, each with its variable sections — pending the SDK
/// report model's deep per-field internal layout (intentionally unspecified; see the story's Readiness note).</summary>
public sealed record FbReport(string Heading, ImmutableArray<FbReportBlock> Blocks);

/// <summary>
/// Transforms the SDK's render-ready report models (<see cref="InstallationReport"/>/<see cref="EndUserReport"/>)
/// 1-to-1 into a self-contained HTML page for viewing/printing in a standard browser (US-040). This is a mechanical
/// template only — every value is already display-final (blank→"--"/empty, addresses decoded, omission + note
/// propagation resolved in the SDK). The <c>print</c> variant swaps in a compact black print stylesheet (installation
/// report = same layout; end-user report additionally drops the table-of-contents and the differing-locality suffix).
/// </summary>
public static class ReportHtmlRenderer
{
    public static string RenderInstallation(InstallationReport r, bool print)
    {
        var sb = new StringBuilder();
        OpenDocument(sb, r.Heading, print);
        sb.Append("<h1 class=\"banner\">IHC OpenVisual</h1>");
        sb.Append("<h2>").Append(Esc(r.Heading)).Append("</h2>");

        Masthead(sb, "Installatør", r.Installer);
        Masthead(sb, "Kunde", r.Customer);

        ModuleTable(sb, "Datalinie input-moduler", r.InputModules);
        ModuleTable(sb, "Datalinie output-moduler", r.OutputModules);

        foreach (ProductDetailTable t in r.ProductDetails)
            ProductDetail(sb, t);
        foreach (ProductDetailTable t in r.ModemDetails)
            ProductDetail(sb, t);

        CrossReference(sb, "Datalinie indgange", r.DatalineInputs);
        CrossReference(sb, "Datalinie udgange", r.DatalineOutputs);

        if (r.SpecialProducts.Length > 0)
        {
            sb.Append("<h3>Specielle Produkter</h3><table><tr>")
              .Append("<th>Produkt</th><th>Terminal</th><th>Note</th><th>Lokalitet</th><th>Placering</th>")
              .Append("<th>Id-kode</th><th>0V</th><th>24V</th><th>RS485-</th><th>RS485+</th></tr>");
            foreach (SpecialProductRow s in r.SpecialProducts)
                Row(sb, s.Product, s.Terminal, s.Note, s.Locality, s.Position, s.IdCode,
                    s.WireColour0V, s.WireColour24V, s.WireColourRs485Minus, s.WireColourRs485Plus);
            sb.Append("</table>");
        }

        if (r.S0Devices.Length > 0)
        {
            sb.Append("<h3>S0 Device</h3><table><tr>")
              .Append("<th>Produkt</th><th>Note</th><th>Lokalitet</th><th>Placering</th><th>Id-kode</th>")
              .Append("<th>S0-</th><th>S0+</th></tr>");
            foreach (S0DeviceRow s in r.S0Devices)
                Row(sb, s.Product, s.Note, s.Locality, s.Position, s.IdCode, s.CableColourS0Minus, s.CableColourS0Plus);
            sb.Append("</table>");
        }

        CloseDocument(sb);
        return sb.ToString();
    }

    public static string RenderEndUser(EndUserReport r, bool print)
    {
        var sb = new StringBuilder();
        OpenDocument(sb, r.Heading, print);
        sb.Append("<h1 class=\"banner\">IHC OpenVisual</h1>");
        sb.Append("<h2>").Append(Esc(r.Heading)).Append("</h2>");

        if (!print)   // screen-only table of contents (anchor links)
        {
            sb.Append("<ul class=\"toc\">");
            foreach (EndUserLocality loc in r.Localities)
                sb.Append("<li><a href=\"#").Append(Esc(loc.AnchorId)).Append("\">").Append(Esc(loc.Name)).Append("</a></li>");
            sb.Append("</ul>");
        }

        foreach (EndUserLocality loc in r.Localities)
        {
            sb.Append("<h3");
            if (!print)
                sb.Append(" id=\"").Append(Esc(loc.AnchorId)).Append('"');
            sb.Append('>').Append(Esc(loc.Name)).Append("</h3>");
            foreach (EndUserProduct product in loc.Products)
            {
                string title = string.IsNullOrEmpty(product.Position) ? product.Name : $"{product.Name} {product.Position}";
                sb.Append("<div class=\"product\"><strong>").Append(Esc(title)).Append("</strong>");
                foreach (EndUserTerminal terminal in product.Terminals)
                {
                    sb.Append("<div class=\"terminal\">&#8226; ").Append(Esc(terminal.Name)).Append("</div>");
                    foreach (EndUserNote note in terminal.Notes)
                    {
                        string suffix = !print && !string.IsNullOrEmpty(note.FbLocality) ? $" ({note.FbLocality})" : string.Empty;
                        sb.Append("<div class=\"note\">- ").Append(Esc(note.Text + suffix)).Append("</div>");
                    }
                }
                sb.Append("</div>");
            }
        }

        CloseDocument(sb);
        return sb.ToString();
    }

    public static string RenderFunctionBlocks(FbReport r, bool print)
    {
        var sb = new StringBuilder();
        OpenDocument(sb, r.Heading, print);
        sb.Append("<h1 class=\"banner\">IHC OpenVisual</h1>");
        sb.Append("<h2>").Append(Esc(r.Heading)).Append("</h2>");
        foreach (FbReportBlock block in r.Blocks)   // Installation/Functions-pane document order
        {
            sb.Append("<h3>").Append(Esc(block.Name)).Append("</h3>");
            foreach (FbReportSection section in block.Sections)
            {
                sb.Append("<table class=\"fb-section\"><caption>").Append(Esc(section.Label)).Append("</caption>");
                if (section.Variables.Length == 0)
                    sb.Append("<tr><td>--</td></tr>");
                foreach (string variable in section.Variables)
                    sb.Append("<tr><td>").Append(Esc(variable)).Append("</td></tr>");
                sb.Append("</table>");
            }
        }
        CloseDocument(sb);
        return sb.ToString();
    }

    private static void Masthead(StringBuilder sb, string caption, ReportPartyInfo p)
    {
        sb.Append("<table class=\"masthead\"><caption>").Append(Esc(caption)).Append("</caption>");
        sb.Append("<tr><th>Navn</th><td>").Append(Esc(p.Navn)).Append("</td></tr>");
        sb.Append("<tr><th>Adresse</th><td>").Append(Esc(p.Adresse)).Append("</td></tr>");
        sb.Append("<tr><th>Telefon</th><td>").Append(Esc(p.Telefon)).Append("</td></tr></table>");
    }

    private static void ModuleTable(StringBuilder sb, string heading, ImmutableArray<ModuleRow> rows)
    {
        if (rows.Length == 0)
            return;
        sb.Append("<h3>").Append(Esc(heading)).Append("</h3><table>")
          .Append("<tr><th>Datalinie</th><th>Modultype</th><th>Lokalitet</th><th>Beskrivelse</th></tr>");
        foreach (ModuleRow m in rows)
            Row(sb, m.Dataline, m.ModuleType, m.Locality, m.Description);
        sb.Append("</table>");
    }

    private static void ProductDetail(StringBuilder sb, ProductDetailTable t)
    {
        sb.Append("<table class=\"product-detail\">");
        foreach (ReportLabelValue lv in t.Rows)
            sb.Append("<tr><th>").Append(Esc(lv.Label)).Append("</th><td>").Append(Esc(lv.Value)).Append("</td></tr>");
        sb.Append("</table>");
        if (t.Terminals.Length > 0)
        {
            sb.Append("<table class=\"terminals\"><tr><th>Terminal</th><th>Adresse</th><th>Ledning</th></tr>");
            foreach (ReportTerminalRow tr in t.Terminals)
                Row(sb, tr.Terminal, tr.Address, tr.Wire);
            sb.Append("</table>");
        }
    }

    private static void CrossReference(StringBuilder sb, string heading, ImmutableArray<DatalineCrossReferenceRow> rows)
    {
        if (rows.Length == 0)
            return;
        sb.Append("<h3>").Append(Esc(heading)).Append("</h3><table><tr>")
          .Append("<th>Adresse</th><th>Produkt</th><th>Terminal</th><th>Note</th><th>Lokalitet</th>")
          .Append("<th>Placering</th><th>Id-kode</th><th>Kabeltype</th><th>Kabelnummer</th>")
          .Append("<th>Lysgruppe</th><th>Ledningsfarve</th></tr>");
        foreach (DatalineCrossReferenceRow c in rows)
            Row(sb, c.Address, c.Product, c.Terminal, c.Note, c.Locality, c.Position, c.IdCode,
                c.CableType, c.CableNumber, c.PowerGroup, c.WireColour);
        sb.Append("</table>");
    }

    private static void Row(StringBuilder sb, params string[] cells)
    {
        sb.Append("<tr>");
        foreach (string cell in cells)
            sb.Append("<td>").Append(Esc(cell)).Append("</td>");
        sb.Append("</tr>");
    }

    private static void OpenDocument(StringBuilder sb, string title, bool print)
    {
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>").Append(Esc(title)).Append("</title><style>");
        sb.Append(print ? PrintCss : ScreenCss);
        sb.Append("</style></head><body class=\"").Append(print ? "print" : "screen").Append("\">");
    }

    private static void CloseDocument(StringBuilder sb) => sb.Append("</body></html>");

    private const string ScreenCss =
        "body{font-family:Inter,Segoe UI,sans-serif;margin:24px;color:#111;}" +
        "h1.banner{font-size:20px;margin:0 0 4px;}h2{font-size:16px;}h3{font-size:14px;margin-top:18px;}" +
        "table{border-collapse:collapse;margin:8px 0;}caption{font-weight:bold;text-align:left;padding:4px 0;}" +
        "th,td{border:1px solid #bbb;padding:3px 8px;text-align:left;font-size:12px;}" +
        "ul.toc{columns:2;}div.product{margin:8px 0;}div.terminal{margin-left:12px;}div.note{margin-left:28px;color:#333;}";

    private const string PrintCss =
        "body{font-family:sans-serif;margin:8px;color:#000;font-size:xx-small;}" +
        "h1.banner{font-size:12px;margin:0;}h2{font-size:11px;}h3{font-size:10px;margin-top:10px;}" +
        "table{border-collapse:collapse;margin:4px 0;page-break-inside:avoid;}" +
        "caption{font-weight:bold;text-align:left;}" +
        "th,td{border:1px solid #000;padding:1px 4px;text-align:left;font-size:xx-small;}" +
        "div.product{margin:4px 0;}div.terminal{margin-left:8px;}div.note{margin-left:18px;}";

    private static string Esc(string? s) => (s ?? string.Empty)
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
