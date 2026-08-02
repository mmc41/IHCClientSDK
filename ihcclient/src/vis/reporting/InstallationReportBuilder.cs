#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Builds the "Installationsdokumentation" (installer) report as a shape document (spec R4/§14):
    /// installer/customer mastheads (A1 <c>--</c> placeholders), the module tables (A7 numeric sort by
    /// data-line number, U10 union over every <c>documentation_modules</c> container), the per-locality
    /// component blocks with the four family row shapes (A8) — non-modem products in document order with
    /// the RS-485 modems hoisted last (U1), locality = nearest ancestor group (U12), dataline products
    /// carrying the descendant-scoped unsorted terminal sub-table (A9) — and the flat cross-reference
    /// tables (A7 address sort with unaddressed rows first, ties in document order; B4 numeric comparison;
    /// A1 blank-cell convention). Addresses display as <c>module . position</c> per A2 with the corrected
    /// full-hex decode (B3), case-insensitive hex (B5) and <c>?</c> for unaddressed/zero (B6). Full mode
    /// adds the common meta/Projekt/appendix shapes.
    /// </summary>
    internal static class InstallationReportBuilder
    {
        private static readonly string Title = ReportTitles.For(ReportKind.Installation);
        private const string Unknown = "?";

        // The component-block families (A8): the detail products every block renders, and the modem tags
        // whose blocks hoist to the end (U1). product_rs485_modem is an open-world tag recognized here.
        private static readonly ImmutableHashSet<string> DetailProductTags =
            ImmutableHashSet.Create(StringComparer.Ordinal, "product_dataline", "product_airlink", "product_rs485_led_dimmer");
        private static readonly ImmutableHashSet<string> ModemTags =
            ImmutableHashSet.Create(StringComparer.Ordinal, "product_rs485_modem", "product_rs485_sms_modem");

        public static ReportShapeDocument Build(Project project, DateTimeOffset generatedAt)
        {
            ArgumentNullException.ThrowIfNull(project);
            var index = new TreeIndex(project.Root);
            IReadOnlyList<ProjectElement> all = project.Root.DescendantsAndSelf();

            var shapes = ImmutableArray.CreateBuilder<ReportShape>();
            shapes.Add(FullModeShapes.MetaLine(project, generatedAt));
            shapes.Add(FullModeShapes.ProjektBlock(project));
            shapes.Add(Party("Installatør", project.InstallerName, project.InstallerAddress, project.InstallerPhone));
            shapes.Add(Party("Kunde", project.CustomerName, project.CustomerAddress, project.CustomerPhone));
            shapes.Add(ModuleTable("Datalinie inputmoduler", all, "dataline_input_module"));
            shapes.Add(ModuleTable("Datalinie outputmoduler", all, "dataline_output_module"));

            shapes.Add(new SectionBreakShape("Lokaliteter og komponenter", SectionBreakStyle.Indented, ReportMembership.Common));
            foreach (ProjectElement product in all.Where(e => DetailProductTags.Contains(e.Tag))
                .Concat(all.Where(e => ModemTags.Contains(e.Tag))))   // U1: modems hoisted after all others
            {
                shapes.Add(ComponentBlock(product, index));
            }

            shapes.Add(new SectionBreakShape("Datalinjer", SectionBreakStyle.Indented, ReportMembership.Common));
            shapes.Add(CrossReferenceTable("Datalinie indgange", "Indgang", all, index, isOutput: false));
            shapes.Add(CrossReferenceTable("Datalinie udgange", "Udgang", all, index, isOutput: true));
            shapes.Add(SpecialProductsTable(all, index));
            shapes.Add(S0DeviceTable(all, index));

            shapes.AddRange(TerminalConnections(all, index));
            shapes.AddRange(FullModeShapes.FindingsAppendix(project, index));
            return new ReportShapeDocument(Title, shapes.ToImmutable());
        }

        // ----- mastheads and module tables -----

        private static KeyValueBlockShape Party(string heading, string? name, string? address, string? phone) =>
            new(heading, ImmutableArray.Create(
                    new KeyValueRow("Navn", ReportText.Display(name)),
                    new KeyValueRow("Adresse", ReportText.Display(address)),
                    new KeyValueRow("Telefon", ReportText.Display(phone))),
                KeyValueStyle.People,
                ReportMembership.Common);

        private static TableShape ModuleTable(string heading, IReadOnlyList<ProjectElement> all, string tag) =>
            new(heading,
                ImmutableArray.Create("Datalinie", "Modul type", "Lokalitet", "Beskrivelse"),
                all.Where(e => e.Tag == tag)
                    .OrderBy(e => double.TryParse(e.GetAttribute("dataline"), NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0d)
                    .Select(m => ImmutableArray.Create<ReportCell>(
                        ReportText.Display(m.GetAttribute("dataline")),
                        new ReportCell(ReportText.Display(m.GetAttribute("module_type")), m.GetAttribute("id")),
                        ReportText.Display(m.GetAttribute("location")),
                        ReportText.Display(m.GetAttribute("note"))))
                    .ToImmutableArray(),
                TableStyle.Module,
                ReportMembership.Common);

        // ----- per-locality component blocks (A8/A9) -----

        private static ComponentBlockShape ComponentBlock(ProjectElement product, TreeIndex index)
        {
            var fields = ImmutableArray.CreateBuilder<KeyValueRow>();
            // The only site needing the locality ELEMENT (its id chips the row), not just its name (U12).
            ProjectElement? locality = index.NearestAncestorOrSelf(product, "group");
            fields.Add(new KeyValueRow("Lokalitet", ReportText.Display(locality?.GetAttribute("name")),
                locality?.GetAttribute("id")));
            fields.Add(new KeyValueRow("Placering", ReportText.Display(product.GetAttribute("position"))));
            fields.Add(new KeyValueRow("Komponent", ReportText.Display(product.GetAttribute("name")),
                product.GetAttribute("id")));

            void Field(string label, string attribute) =>
                fields.Add(new KeyValueRow(label, ReportText.Display(product.GetAttribute(attribute))));

            TableShape? terminals = null;
            switch (product.Tag)
            {
                case "product_dataline":
                    Field("Identifikationskode", "documentation_tag");
                    Field("Kabelnummer", "cablenumber");
                    Field("Kabeltype", "cabletype");
                    Field("Lysgruppe", "power_group");
                    terminals = TerminalSubTable(product);
                    break;
                case "product_airlink":
                    Field("Identifikationskode", "documentation_tag");
                    Field("Serie nummer", "serialnumber");
                    Field("Lysgruppe", "power_group");
                    break;
                case "product_rs485_led_dimmer":
                    Field("Serie nummer", "serialnumber");
                    break;
                default:    // the modem family (U1-hoisted)
                    Field("Identifikationskode", "documentation_tag");
                    Field("Ledningsfarve 0V", "cablecolour_0V");
                    Field("Ledningsfarve 24V", "cablecolour_24V");
                    Field("Ledningsfarve RS485Minus", "cablecolour_RS485Minus");
                    Field("Ledningsfarve RS485Plus", "cablecolour_RS485Plus");
                    break;
            }
            return new ComponentBlockShape(fields.ToImmutable(), terminals);
        }

        // A9: the dataline terminal sub-table is descendant-scoped and UNSORTED (document order).
        private static TableShape? TerminalSubTable(ProjectElement product)
        {
            ImmutableArray<ImmutableArray<ReportCell>> rows = product.Descendants()
                .Where(e => e.Tag is "dataline_input" or "dataline_output")
                .Select(t => ImmutableArray.Create<ReportCell>(
                    new ReportCell(ReportText.Display(t.GetAttribute("name")), t.GetAttribute("id")),
                    Direction(t) + " " + AddressLabel(t),
                    ReportText.Display(t.GetAttribute("cable_colour"))))
                .ToImmutableArray();
            return rows.IsEmpty
                ? null
                : new TableShape(Heading: null, ImmutableArray.Create("Terminal", "Adresse", "Ledning"), rows,
                    TableStyle.Plain, ReportMembership.Common);
        }

        // ----- flat cross-reference tables (blank-cell convention) -----

        private static TableShape CrossReferenceTable(string heading, string terminalColumn,
            IReadOnlyList<ProjectElement> all, TreeIndex index, bool isOutput)
        {
            string tag = isOutput ? "dataline_output" : "dataline_input";
            ImmutableArray<ImmutableArray<ReportCell>> rows = all
                .Where(e => e.Tag == tag)
                .OrderBy(SortValue)   // A7/B4: unaddressed first (-1), then numeric packed value; ties keep document order
                .Select(t =>
                {
                    ProjectElement? product = index.NearestProduct(t);
                    string ProductAttr(string name) =>
                        product is null ? Unknown : ReportText.SingleLine(product.GetAttribute(name));
                    return ImmutableArray.Create<ReportCell>(
                        AddressLabel(t),
                        ProductAttr("name"),
                        ReportText.SingleLine(t.GetAttribute("name")),
                        ReportText.SingleLine(t.GetAttribute("note")),
                        product is null ? Unknown : ReportText.SingleLine(index.LocalityName(product)),
                        ProductAttr("position"),
                        ProductAttr("documentation_tag"),
                        ProductAttr("cabletype"),
                        ProductAttr("cablenumber"),
                        ProductAttr("power_group"),
                        ReportText.SingleLine(t.GetAttribute("cable_colour")));
                })
                .ToImmutableArray();
            return new TableShape(heading,
                ImmutableArray.Create("Adresse", "Produkt", terminalColumn, "Note", "Lokalitet",
                    "Placering", "Id-kode", "Kabeltype", "Kabelnummer", "Lysgruppe", "Ledningsfarve"),
                rows, TableStyle.Datalines, ReportMembership.Common);
        }

        private static TableShape SpecialProductsTable(IReadOnlyList<ProjectElement> all, TreeIndex index) =>
            new("Specielle Produkter",
                ImmutableArray.Create("Produkt", "Indgang", "Note", "Lokalitet", "Placering", "Id-kode",
                    "Ledningsfarve 0V", "Ledningsfarve 24V", "Ledningsfarve RS485Minus", "Ledningsfarve RS485Plus"),
                all.Where(e => ModemTags.Contains(e.Tag))
                    .Select(m => ImmutableArray.Create<ReportCell>(
                        ReportText.SingleLine(m.GetAttribute("name")),
                        ReportText.SingleLine(m.GetAttribute("name")),   // the vendor prints the name in both columns
                        ReportText.SingleLine(m.GetAttribute("note")),
                        ReportText.SingleLine(index.LocalityName(m)),
                        ReportText.SingleLine(m.GetAttribute("position")),
                        ReportText.SingleLine(m.GetAttribute("documentation_tag")),
                        ReportText.SingleLine(m.GetAttribute("cablecolour_0V")),
                        ReportText.SingleLine(m.GetAttribute("cablecolour_24V")),
                        ReportText.SingleLine(m.GetAttribute("cablecolour_RS485Minus")),
                        ReportText.SingleLine(m.GetAttribute("cablecolour_RS485Plus"))))
                    .ToImmutableArray(),
                TableStyle.Special,
                ReportMembership.Common);

        private static TableShape S0DeviceTable(IReadOnlyList<ProjectElement> all, TreeIndex index) =>
            new("S0 Device",
                ImmutableArray.Create("Produkt", "Note", "Lokalitet", "Placering", "Id-kode",
                    "Cable Colour S0Minus", "Cable Colour S0Plus"),
                all.Where(e => e.Tag == "s0_device")
                    .Select(d => ImmutableArray.Create<ReportCell>(
                        new ReportCell(ReportText.SingleLine(d.GetAttribute("name")), d.GetAttribute("id")),
                        ReportText.SingleLine(d.GetAttribute("note")),
                        ReportText.SingleLine(index.LocalityName(d)),
                        ReportText.SingleLine(d.GetAttribute("position")),
                        ReportText.SingleLine(d.GetAttribute("documentation_tag")),
                        ReportText.SingleLine(d.GetAttribute("cable_colour_minus")),
                        ReportText.SingleLine(d.GetAttribute("cable_colour_plus"))))
                    .ToImmutableArray(),
                TableStyle.S0,
                ReportMembership.Common);

        // The Full-only "Terminal-forbindelser" cross-reference (R4): every LINKED dataline product
        // terminal in document order, its first link resolved across to the FB pin — the connection path
        // "-> pin -> block -> locality" and the pin's behaviour note. Placed before the error appendix.
        private static IEnumerable<ReportShape> TerminalConnections(IReadOnlyList<ProjectElement> all, TreeIndex index)
        {
            var rows = ImmutableArray.CreateBuilder<ImmutableArray<ReportCell>>();
            foreach (ProjectElement product in all.Where(e => e.Tag == "product_dataline"))
            {
                foreach (ProjectElement terminal in product.Descendants()
                    .Where(e => e.Tag is "dataline_input" or "dataline_output"))
                {
                    if (index.LinkTargets(terminal).FirstOrDefault() is not { } target)
                    {
                        continue;   // unlinked (or dangling) terminals carry no connection row
                    }
                    ProjectElement? pin = index.Parent(target);
                    ProjectElement? block = pin is null ? null : index.NearestAncestorOrSelf(pin, "functionblock");
                    ProjectElement? locality = block is null ? null : index.NearestAncestorOrSelf(block, "group");
                    rows.Add(ImmutableArray.Create<ReportCell>(
                        ReportText.SingleLine(product.GetAttribute("name")),
                        ReportText.SingleLine(terminal.GetAttribute("name")),
                        $"-> {ReportText.SingleLine(pin?.GetAttribute("name"))}"
                            + $" -> {ReportText.SingleLine(block?.GetAttribute("name"))}"
                            + $" -> {ReportText.SingleLine(locality?.GetAttribute("name"))}",
                        ReportText.SingleLine(pin?.GetAttribute("note"))));
                }
            }
            yield return new SectionBreakShape("Terminal-forbindelser", SectionBreakStyle.Flush, ReportMembership.FullOnly);
            yield return new TableShape(Heading: null,
                ImmutableArray.Create("Produkt", "Terminal", "Forbindelse", "Funktion"),
                rows.ToImmutable(), TableStyle.Plain, ReportMembership.FullOnly);
        }

        // ----- shared helpers -----

        private static string Direction(ProjectElement terminal) =>
            terminal.Tag == "dataline_output" ? "Udgang" : "Indgang";

        /// <summary>A2/B3/B5/B6: <c>module . position</c> — the addressing layer's terminal label with this
        /// report's spaced separator; <c>?</c> for an unaddressed/zero/unparseable token.</summary>
        private static string AddressLabel(ProjectElement terminal)
        {
            bool isOutput = terminal.Tag == "dataline_output";
            return DatalineAddress.TryParse(terminal.GetAttribute("address_dataline"), isOutput, out DatalineAddress address)
                ? address.DataLine.ToString(CultureInfo.InvariantCulture) + " . " + DatalineAddress.TerminalLabel(address.Terminal)
                : Unknown;
        }

        // The numeric packed address value for sorting (B4); unaddressed/unparseable sorts first (A7). A packed value
        // of 0 (an explicit "_0x0") renders as unaddressed through AddressLabel (DatalineAddress.TryParse refuses a
        // value <= 0), so it must SORT as unaddressed too — floor any value <= 0 to the same -1 key an absent token
        // gets, or two identically-displayed "?" rows would order inconsistently (review G1).
        private static long SortValue(ProjectElement terminal)
        {
            long value = HexToken.ParseValueOrDefault(terminal.GetAttribute("address_dataline"), -1);
            return value <= 0 ? -1 : value;
        }
    }
}
