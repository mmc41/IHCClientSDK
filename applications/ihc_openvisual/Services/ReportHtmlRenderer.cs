using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Ihc.Vis;

namespace ihc_openvisual.Services.Reporting;

/// <summary>
/// Transforms the SDK's render-ready report models (<see cref="InstallationReport"/>/<see cref="EndUserReport"/>)
/// 1-to-1 into a self-contained HTML page for viewing/printing in a standard browser (US-040). This is a mechanical
/// template only — every value is already display-final (blank→"--"/empty, addresses decoded, omission + note
/// propagation resolved in the SDK). The <c>print</c> variant swaps in a compact black print stylesheet (installation
/// report = same layout; end-user report additionally drops the table-of-contents and the differing-locality suffix).
/// </summary>
/// <summary>The report tailoring switches (US-071, T029): which content sections are emitted and which detail
/// options apply within them. A section switched OFF emits nothing (not an empty heading). The renderer reads these
/// alongside the switch data already carried on the combined model; the Reports view owns them for the session.</summary>
public sealed record ReportOptions(
    bool ShowInstallation = true, bool ShowEndUser = true, bool ShowFunctionBlocks = true,
    bool ShowInternalIds = false, bool ShowWireColours = true, bool ShowLinkDisplay = true,
    bool ShowFunctionDocs = true, bool ShowEmptyFields = true)
{
    /// <summary>All sections on, default detail options.</summary>
    public static ReportOptions Default { get; } = new();
}

/// <summary>A named purpose preset (US-040/T030): a starting combination of the US-071 switches for a common report
/// purpose. Selecting one seeds the switches; the user can still adjust individual toggles afterwards.</summary>
public enum ReportPreset
{
    /// <summary>Installation / technical: the installation section with its cabling/link technical detail.</summary>
    Installation,
    /// <summary>End-user / function: the end-user section with function documentation, cabling detail dropped.</summary>
    EndUser,
    /// <summary>Function-block: the function-block logic section only.</summary>
    FunctionBlock,
    /// <summary>Full: every section and every detail option, including internal ids.</summary>
    Full,
}

public static class ReportHtmlRenderer
{
    /// <summary>The starting switch combination for a purpose preset (US-040/T030).</summary>
    public static ReportOptions ForPreset(ReportPreset preset) => preset switch
    {
        // Installation / technical: the installation section with its cabling + link technical detail.
        ReportPreset.Installation => new(ShowInstallation: true, ShowEndUser: false, ShowFunctionBlocks: false,
            ShowInternalIds: false, ShowWireColours: true, ShowLinkDisplay: true, ShowFunctionDocs: true, ShowEmptyFields: true),
        // End-user / function: the end-user section with function documentation; cabling/link/empty detail dropped.
        ReportPreset.EndUser => new(ShowInstallation: false, ShowEndUser: true, ShowFunctionBlocks: false,
            ShowInternalIds: false, ShowWireColours: false, ShowLinkDisplay: false, ShowFunctionDocs: true, ShowEmptyFields: false),
        // Function-block: the function-block logic section only.
        ReportPreset.FunctionBlock => new(ShowInstallation: false, ShowEndUser: false, ShowFunctionBlocks: true,
            ShowInternalIds: false, ShowWireColours: false, ShowLinkDisplay: false, ShowFunctionDocs: false, ShowEmptyFields: true),
        // Full: every section and detail option, including internal ids.
        _ => new(ShowInstallation: true, ShowEndUser: true, ShowFunctionBlocks: true,
            ShowInternalIds: true, ShowWireColours: true, ShowLinkDisplay: true, ShowFunctionDocs: true, ShowEmptyFields: true),
    };

    public static string RenderInstallation(InstallationReport r, bool print)
    {
        var sb = new StringBuilder();
        OpenDocument(sb, r.Heading, print);
        sb.Append("<h1 class=\"banner\">IHC OpenVisual</h1>");
        InstallationBody(sb, r);
        CloseDocument(sb);
        return sb.ToString();
    }

    public static string RenderEndUser(EndUserReport r, bool print)
    {
        var sb = new StringBuilder();
        OpenDocument(sb, r.Heading, print);
        sb.Append("<h1 class=\"banner\">IHC OpenVisual</h1>");
        EndUserBody(sb, r, print);
        CloseDocument(sb);
        return sb.ToString();
    }

    public static string RenderFunctionBlocks(FunctionBlockReport r, bool print)
    {
        var sb = new StringBuilder();
        OpenDocument(sb, r.Heading, print);
        sb.Append("<h1 class=\"banner\">IHC OpenVisual</h1>");
        FunctionBlocksBody(sb, r);
        CloseDocument(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Renders the COMBINED project-documentation model (D14/T021) as ONE navigable HTML document — the three sections
    /// in fixed order. The SCREEN variant adds the report navigation: a top overview linking to each section, a
    /// per-section anchor, and a back-to-top link after each section. The PRINT variant drops all of it (no overview,
    /// no anchors, no back-to-top). Each section's content is the same 1-to-1 transform as the standalone reports.
    /// </summary>
    public static string RenderProjectDocumentation(ProjectDocumentationReport report, bool print, ReportOptions? optionsOrNull = null)
    {
        ReportOptions options = optionsOrNull ?? ReportOptions.Default;
        var sb = new StringBuilder();
        OpenDocument(sb, "Projektdokumentation", print);
        sb.Append("<h1 class=\"banner\" id=\"top\">IHC OpenVisual</h1>");
        // Heading metadata (T022): the report generation timestamp and the programmer (empty-fields switch, T029).
        sb.Append("<p class=\"report-meta\">Genereret: ").Append(Esc(report.GeneratedAt))
            .Append(" &mdash; Programmør: ").Append(Esc(Cell(report.Projekt.Programmer, options))).Append("</p>");

        // The Projekt identity block, rendered as section 2 (US-039 / T023): description / number / programmer.
        sb.Append("<section class=\"projekt\"><h2>Projekt</h2><table class=\"masthead\">");
        sb.Append("<tr><th>Beskrivelse</th><td>").Append(Esc(Cell(report.Projekt.Description, options))).Append("</td></tr>");
        sb.Append("<tr><th>Nummer</th><td>").Append(Esc(Cell(report.Projekt.Number, options))).Append("</td></tr>");
        sb.Append("<tr><th>Programmør</th><td>").Append(Esc(Cell(report.Projekt.Programmer, options))).Append("</td></tr>");
        sb.Append("</table></section>");

        // Internal ids (US-071 detail option): the per-element ids, shown only when the switch is on.
        if (options.ShowInternalIds && report.Elements.Length > 0)
        {
            sb.Append("<section class=\"internal-ids\"><h2>Interne id'er</h2><table><tr><th>Element</th><th>Id</th></tr>");
            foreach (ReportElementRef e in report.Elements)
                Row(sb, e.Name, e.Id);
            sb.Append("</table></section>");
        }

        // Screen overview: one link per section that is switched ON (US-071 — an off section is absent, not empty).
        if (!print)
        {
            var onSections = report.Sections.Where(s => SectionOn(s.Kind, options)).ToList();
            if (onSections.Count > 0)
            {
                sb.Append("<ul class=\"toc overview\">");
                foreach (ReportSectionEntry s in onSections)
                    sb.Append("<li><a href=\"#").Append(Esc(s.Id)).Append("\">").Append(Esc(s.Heading)).Append("</a></li>");
                sb.Append("</ul>");
            }
        }

        if (options.ShowInstallation)
            Section(sb, report, ReportSectionKind.Installation, print, () =>
            {
                InstallationBody(sb, report.Installation);
                ModuleMapTables(sb, report.ModuleMap);
                TerminalDetailsTable(sb, report.TerminalDetails, options);
                KablerTable(sb, report.Kabler, options);
            });
        if (options.ShowEndUser)
            Section(sb, report, ReportSectionKind.EndUser, print, () => EndUserBody(sb, report.EndUser, print));
        if (options.ShowFunctionBlocks)
            Section(sb, report, ReportSectionKind.FunctionBlock, print, () => DeepFunctionBlocks(sb, report.FunctionBlocks));
        CompletenessSection(sb, report.Completeness);

        CloseDocument(sb);
        return sb.ToString();
    }

    private static bool SectionOn(ReportSectionKind kind, ReportOptions o) => kind switch
    {
        ReportSectionKind.Installation => o.ShowInstallation,
        ReportSectionKind.EndUser => o.ShowEndUser,
        _ => o.ShowFunctionBlocks,
    };

    // A ReportValue cell honours the show-empty-fields switch: the placeholder display ("--" for a blank) when ON, the
    // raw value (empty when blank) when OFF.
    private static string Cell(ReportValue value, ReportOptions options) => options.ShowEmptyFields ? value.Display : value.Raw;

    // Wraps a section body with its screen anchor id and a back-to-top link (both screen-only); the print variant
    // emits the body alone, with no navigation.
    private static void Section(StringBuilder sb, ProjectDocumentationReport report, ReportSectionKind kind, bool print, Action body)
    {
        ReportSectionEntry entry = report.Sections.First(s => s.Kind == kind);
        sb.Append("<section");
        if (!print)
            sb.Append(" id=\"").Append(Esc(entry.Id)).Append('"');
        sb.Append('>');
        body();
        if (!print)
            sb.Append("<p class=\"back-to-top\"><a href=\"#top\">&#8593; Til top</a></p>");
        sb.Append("</section>");
    }

    // The technical terminal-connections table (T024): one row per linked terminal with its link-display path and the
    // driving FB input's behaviour note. Omitted when there are no linked terminals.
    private static void TerminalDetailsTable(StringBuilder sb, ImmutableArray<ReportTerminalDetail> details, ReportOptions options)
    {
        if (details.Length == 0)
            return;
        sb.Append("<h3>Terminal-forbindelser</h3><table class=\"terminal-detail\">");
        sb.Append("<tr><th>Produkt</th><th>Terminal</th><th>Forbindelse</th><th>Funktion</th></tr>");
        foreach (ReportTerminalDetail d in details)
            sb.Append("<tr><td>").Append(Esc(d.Product)).Append("</td><td>").Append(Esc(d.Terminal))
              // US-071 detail options: the link display and the function documentation each hide when switched off.
              .Append("</td><td>").Append(Esc(options.ShowLinkDisplay ? d.LinkDisplay : string.Empty))
              .Append("</td><td>").Append(Esc(options.ShowFunctionDocs ? d.FunctionNote.Display : string.Empty)).Append("</td></tr>");
        sb.Append("</table>");
    }

    // The deep function-block logic section (US-041, T028): per block — description, input/output notes, settings and
    // internal variables as name=value, and the flattened program outline; an unprogrammed block shows "Tom blok".
    private static void DeepFunctionBlocks(StringBuilder sb, ImmutableArray<ReportFbBlock> blocks)
    {
        sb.Append("<h2>Functionsblok dokumentation</h2>");
        foreach (ReportFbBlock b in blocks)
        {
            sb.Append("<h3>").Append(Esc(b.Name)).Append("</h3>");
            if (b.IsEmpty)
                sb.Append("<p class=\"empty-block\">Tom blok</p>");
            if (b.Description.Length > 0)
                sb.Append("<p class=\"fb-description\">").Append(Esc(b.Description)).Append("</p>");
            FbPinTable(sb, "Input", b.Inputs);
            FbPinTable(sb, "Output", b.Outputs);
            FbVarTable(sb, "Indstillinger", b.Settings);
            FbVarTable(sb, "Interne variable", b.InternalVariables);
            if (b.Outline.Length > 0)
            {
                sb.Append("<pre class=\"fb-outline\">");
                foreach (string line in b.Outline)
                    sb.Append(Esc(line)).Append('\n');
                sb.Append("</pre>");
            }
        }
    }

    private static void FbPinTable(StringBuilder sb, string heading, ImmutableArray<ReportFbPin> pins)
    {
        if (pins.Length == 0)
            return;
        sb.Append("<table class=\"fb-pins\"><caption>").Append(Esc(heading)).Append("</caption><tr><th>Navn</th><th>Note</th></tr>");
        foreach (ReportFbPin p in pins)
            Row(sb, p.Name, p.Note);
        sb.Append("</table>");
    }

    private static void FbVarTable(StringBuilder sb, string heading, ImmutableArray<ReportFbVariable> variables)
    {
        if (variables.Length == 0)
            return;
        sb.Append("<table class=\"fb-vars\"><caption>").Append(Esc(heading)).Append("</caption>");
        foreach (ReportFbVariable v in variables)
            sb.Append("<tr><td>").Append(Esc(v.Name)).Append(" = ").Append(Esc(v.Value)).Append("</td></tr>");
        sb.Append("</table>");
    }

    // The documentation-completeness section (T027): a table of every missing/blank item located by locality →
    // product → terminal, or "Ingen fejl fundet." when the project is fully documented.
    private static void CompletenessSection(StringBuilder sb, ImmutableArray<ReportCompletenessRow> rows)
    {
        sb.Append("<section class=\"completeness\"><h2>Fejl i dokumentation</h2>");
        if (rows.Length == 0)
        {
            sb.Append("<p class=\"none-found\">Ingen fejl fundet.</p></section>");
            return;
        }
        sb.Append("<table><tr><th>Lokalitet</th><th>Produkt</th><th>Terminal</th><th>Fejl</th></tr>");
        foreach (ReportCompletenessRow r in rows)
            Row(sb, r.Locality, r.Product, r.Terminal, r.Problem);
        sb.Append("</table></section>");
    }

    // The per-terminal module address map (T025→T026): which product terminal occupies each address, per input and
    // per output module (a table, no diagram). Each side is omitted when it has no addressed terminals.
    private static void ModuleMapTables(StringBuilder sb, ModuleAddressMap map)
    {
        ModuleOccupancy(sb, "Input-modul adressekort", map.InputModules);
        ModuleOccupancy(sb, "Output-modul adressekort", map.OutputModules);
    }

    private static void ModuleOccupancy(StringBuilder sb, string heading, ImmutableArray<ModuleAddressEntry> entries)
    {
        if (entries.Length == 0)
            return;
        sb.Append("<h3>").Append(Esc(heading)).Append("</h3><table class=\"module-map\">");
        sb.Append("<tr><th>Adresse</th><th>Produkt</th><th>Terminal</th></tr>");
        foreach (ModuleAddressEntry e in entries)
            Row(sb, e.Address, e.Product, e.Terminal);
        sb.Append("</table>");
    }

    // The consolidated Kabler cabling table (T025): one row per addressed terminal, ten vendor columns. Omitted when
    // there are no addressed terminals.
    private static void KablerTable(StringBuilder sb, ImmutableArray<ReportKablerRow> rows, ReportOptions options)
    {
        if (rows.Length == 0)
            return;
        sb.Append("<h3>Kabler</h3><table class=\"kabler\"><tr>");
        foreach (string header in new[] { "Ledningsfarve", "Adresse", "Modul", "Modul-lokation", "Lysgruppe",
            "Id-kode", "Lokalitet", "Placering", "Produkt", "Ind-Udgang" })
            sb.Append("<th>").Append(Esc(header)).Append("</th>");
        sb.Append("</tr>");
        foreach (ReportKablerRow r in rows)
            // US-071 detail option: the wire colour hides when switched off.
            Row(sb, options.ShowWireColours ? r.Ledningsfarve : string.Empty,
                r.Adresse, r.Modul, r.ModulLokation, r.Lysgruppe, r.IdKode, r.Lokalitet, r.Placering, r.Produkt, r.IndUdgang);
        sb.Append("</table>");
    }

    private static void InstallationBody(StringBuilder sb, InstallationReport r)
    {
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
        Table(sb, "Specielle Produkter",
            ["Produkt", "Terminal", "Note", "Lokalitet", "Placering", "Id-kode", "0V", "24V", "RS485-", "RS485+"],
            r.SpecialProducts, s => [s.Product, s.Terminal, s.Note, s.Locality, s.Position, s.IdCode,
                s.WireColour0V, s.WireColour24V, s.WireColourRs485Minus, s.WireColourRs485Plus]);
        Table(sb, "S0 Device",
            ["Produkt", "Note", "Lokalitet", "Placering", "Id-kode", "S0-", "S0+"],
            r.S0Devices, s => [s.Product, s.Note, s.Locality, s.Position, s.IdCode,
                s.CableColourS0Minus, s.CableColourS0Plus]);
    }

    private static void EndUserBody(StringBuilder sb, EndUserReport r, bool print)
    {
        sb.Append("<h2>").Append(Esc(r.Heading)).Append("</h2>");
        if (!print)   // screen-only per-locality table of contents (anchor links)
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
                // D16: image-free product identity = type name (resolved catalog text) + name/placement; no icon.
                if (!string.IsNullOrEmpty(product.ProductType))
                    sb.Append(" <span class=\"product-type\">").Append(Esc(product.ProductType)).Append("</span>");
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
    }

    private static void FunctionBlocksBody(StringBuilder sb, FunctionBlockReport r)
    {
        sb.Append("<h2>").Append(Esc(r.Heading)).Append("</h2>");
        foreach (FunctionBlockReportEntry block in r.Blocks)   // Installation/Functions-pane document order
        {
            sb.Append("<h3>").Append(Esc(block.Name)).Append("</h3>");
            foreach (FunctionBlockReportSection section in block.Sections)
            {
                sb.Append("<table class=\"fb-section\"><caption>").Append(Esc(section.Label)).Append("</caption>");
                if (section.Variables.Length == 0)
                    sb.Append("<tr><td>--</td></tr>");
                foreach (string variable in section.Variables)
                    sb.Append("<tr><td>").Append(Esc(variable)).Append("</td></tr>");
                sb.Append("</table>");
            }
        }
    }

    private static void Masthead(StringBuilder sb, string caption, ReportPartyInfo p)
    {
        sb.Append("<table class=\"masthead\"><caption>").Append(Esc(caption)).Append("</caption>");
        sb.Append("<tr><th>Navn</th><td>").Append(Esc(p.Navn)).Append("</td></tr>");
        sb.Append("<tr><th>Adresse</th><td>").Append(Esc(p.Adresse)).Append("</td></tr>");
        sb.Append("<tr><th>Telefon</th><td>").Append(Esc(p.Telefon)).Append("</td></tr></table>");
    }

    private static void ModuleTable(StringBuilder sb, string heading, ImmutableArray<ModuleRow> rows) =>
        Table(sb, heading, ["Datalinie", "Modultype", "Lokalitet", "Beskrivelse"],
            rows, m => [m.Dataline, m.ModuleType, m.Locality, m.Description]);

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

    private static void CrossReference(StringBuilder sb, string heading, ImmutableArray<DatalineCrossReferenceRow> rows) =>
        Table(sb, heading,
            ["Adresse", "Produkt", "Terminal", "Note", "Lokalitet", "Placering", "Id-kode",
             "Kabeltype", "Kabelnummer", "Lysgruppe", "Ledningsfarve"],
            rows, c => [c.Address, c.Product, c.Terminal, c.Note, c.Locality, c.Position, c.IdCode,
                c.CableType, c.CableNumber, c.PowerGroup, c.WireColour]);

    /// <summary>One flat report table — heading, header row, a <see cref="Row"/> per item — omitted entirely
    /// when it has no rows (an empty section never renders).</summary>
    private static void Table<T>(
        StringBuilder sb, string heading, string[] headers, ImmutableArray<T> rows, Func<T, string[]> cells)
    {
        if (rows.Length == 0)
            return;
        sb.Append("<h3>").Append(Esc(heading)).Append("</h3><table><tr>");
        foreach (string header in headers)
            sb.Append("<th>").Append(Esc(header)).Append("</th>");
        sb.Append("</tr>");
        foreach (T row in rows)
            Row(sb, cells(row));
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
        "ul.toc{columns:2;}ul.overview{columns:auto;font-weight:bold;}section{margin-bottom:24px;}" +
        "p.back-to-top{margin:6px 0 0;font-size:11px;}" +
        "div.product{margin:8px 0;}div.terminal{margin-left:12px;}div.note{margin-left:28px;color:#333;}";

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
