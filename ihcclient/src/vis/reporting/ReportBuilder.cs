#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Builds the render-ready report backing models (<see cref="InstallationReport"/>, <see cref="EndUserReport"/>)
    /// from a loaded <see cref="Project"/>. This is where ALL report business logic lives — section order, the
    /// blank→"--"/empty decisions, the end-user omission filter, note propagation and data-line address decoding —
    /// so a GUI transforms the result 1-to-1 into HTML with no logic of its own. The field/order/omission contract
    /// is REPORT-P3 output-spec.md, itself traced to the vendor report stylesheets (the layout oracle, not a
    /// runtime dependency).
    /// </summary>
    public static class ReportBuilder
    {
        private const string BlankPlaceholder = "--";
        private const string InstallationHeading = "Installationsdokumentation";
        private const string EndUserHeading = "Funktionsdokumentation";
        private const string FunctionBlockHeading = "Functionsblok dokumentation";
        private const string Unknown = "?";
        private static readonly ImmutableHashSet<string> DetailProductTags =
            ImmutableHashSet.Create(StringComparer.Ordinal, "product_dataline", "product_airlink", "product_rs485_led_dimmer");
        private static readonly ImmutableHashSet<string> ModemTags =
            ImmutableHashSet.Create(StringComparer.Ordinal, "product_rs485_modem", "product_rs485_sms_modem");

        /// <summary>The <c>ver</c>-template value: the raw value when it has non-whitespace content, else "--".</summary>
        private static string Ver(string? value) => string.IsNullOrWhiteSpace(value) ? BlankPlaceholder : value;

        /// <summary>The raw <c>value-of</c> value: the value verbatim, or the empty string when the attribute is absent.</summary>
        private static string Raw(string? value) => value ?? string.Empty;

        /// <summary>Builds the installation ("Installationsdokumentation") report model.</summary>
        public static InstallationReport BuildInstallation(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            var index = new TreeIndex(project.Root);
            IReadOnlyList<ProjectElement> all = project.Root.DescendantsAndSelf();

            ImmutableArray<ProductDetailTable> details = all
                .Where(e => DetailProductTags.Contains(e.Tag))
                .Select(p => BuildProductDetail(p, index))
                .ToImmutableArray();
            ImmutableArray<ProductDetailTable> modems = all
                .Where(e => ModemTags.Contains(e.Tag))
                .Select(p => BuildProductDetail(p, index))
                .ToImmutableArray();
            ImmutableArray<DatalineCrossReferenceRow> inputs = all
                .Where(e => e.Tag == "dataline_input")
                .OrderBy(AddressSortKey, StringComparer.Ordinal)
                .Select(t => BuildCrossReferenceRow(t, index, isOutput: false))
                .ToImmutableArray();
            ImmutableArray<DatalineCrossReferenceRow> outputs = all
                .Where(e => e.Tag == "dataline_output")
                .OrderBy(AddressSortKey, StringComparer.Ordinal)
                .Select(t => BuildCrossReferenceRow(t, index, isOutput: true))
                .ToImmutableArray();
            ImmutableArray<SpecialProductRow> special = all
                .Where(e => ModemTags.Contains(e.Tag))
                .OrderBy(AddressSortKey, StringComparer.Ordinal)
                .Select(m => BuildSpecialProductRow(m, index))
                .ToImmutableArray();
            ImmutableArray<S0DeviceRow> s0 = all
                .Where(e => e.Tag == "s0_device")
                .OrderBy(AddressSortKey, StringComparer.Ordinal)
                .Select(d => BuildS0DeviceRow(d, index))
                .ToImmutableArray();

            return new InstallationReport(
                InstallationHeading,
                BuildParty(project.Child("installer_info")),
                BuildParty(project.Child("customer_info")),
                BuildModuleRows(all, "dataline_input_module"),
                BuildModuleRows(all, "dataline_output_module"),
                details,
                modems,
                inputs,
                outputs,
                special,
                s0);
        }

        /// <summary>Builds the end-user ("Funktionsdokumentation") report model.</summary>
        public static EndUserReport BuildEndUser(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            var index = new TreeIndex(project.Root);
            ImmutableArray<EndUserLocality> localities = project.Groups
                .Select(g => BuildEndUserLocality(g, index))
                .ToImmutableArray();
            return new EndUserReport(EndUserHeading + Raw(project.Root.GetAttribute("name")), localities);
        }

        /// <summary>Builds the function-block ("Functionsblok dokumentation") report model (US-041): every function
        /// block in Installation/Functions-pane document order, each with its variable sections and their variables.</summary>
        public static FunctionBlockReport BuildFunctionBlock(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            var blocks = ImmutableArray.CreateBuilder<FunctionBlockReportEntry>();
            foreach (ProjectElement group in project.Groups)
            {
                foreach (ProjectElement fb in group.ChildrenOrEmpty().Where(c => c.Tag == "functionblock"))
                {
                    var sections = ImmutableArray.CreateBuilder<FunctionBlockReportSection>();
                    foreach ((string container, string label) in FunctionBlockSections.All)
                    {
                        ImmutableArray<string> vars = fb.FindChild(container)?.ChildrenOrEmpty()
                            .Select(v => v.GetAttribute("name") ?? v.Tag).ToImmutableArray() ?? ImmutableArray<string>.Empty;
                        sections.Add(new FunctionBlockReportSection(label, vars));
                    }
                    blocks.Add(new FunctionBlockReportEntry(fb.GetAttribute("name") ?? "block", sections.ToImmutable()));
                }
            }
            return new FunctionBlockReport(FunctionBlockHeading, blocks.ToImmutable());
        }

        // ----- installation: masthead & section rows -----

        private static ReportPartyInfo BuildParty(ProjectElement? info) =>
            new(Ver(info?.GetAttribute("name")), Ver(info?.GetAttribute("address")), Ver(info?.GetAttribute("phone")));

        private static ImmutableArray<ModuleRow> BuildModuleRows(IReadOnlyList<ProjectElement> all, string tag) =>
            all.Where(e => e.Tag == tag)
                .OrderBy(e => double.TryParse(e.GetAttribute("dataline"), NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0d)
                .Select(m => new ModuleRow(
                    Ver(m.GetAttribute("dataline")), Ver(m.GetAttribute("module_type")),
                    Ver(m.GetAttribute("location")), Ver(m.GetAttribute("note"))))
                .ToImmutableArray();

        private static ProductDetailTable BuildProductDetail(ProjectElement product, TreeIndex index)
        {
            string locality = Ver(index.Parent(product)?.GetAttribute("name"));
            string position = Ver(product.GetAttribute("position"));
            string component = Ver(product.GetAttribute("name"));
            var rows = ImmutableArray.CreateBuilder<ReportLabelValue>();
            rows.Add(new ReportLabelValue("Lokalitet", locality));
            rows.Add(new ReportLabelValue("Placering", position));
            rows.Add(new ReportLabelValue("Komponent", component));

            ReportProductKind kind = KindOf(product.Tag);
            ImmutableArray<ReportTerminalRow> terminals = ImmutableArray<ReportTerminalRow>.Empty;
            switch (kind)
            {
                case ReportProductKind.Dataline:
                    rows.Add(new ReportLabelValue("Identifikationskode", Ver(product.GetAttribute("documentation_tag"))));
                    rows.Add(new ReportLabelValue("Kabelnummer", Ver(product.GetAttribute("cablenumber"))));
                    rows.Add(new ReportLabelValue("Kabeltype", Ver(product.GetAttribute("cabletype"))));
                    rows.Add(new ReportLabelValue("Lysgruppe", Ver(product.GetAttribute("power_group"))));
                    terminals = BuildTerminalRows(product);
                    break;
                case ReportProductKind.Airlink:
                    rows.Add(new ReportLabelValue("Identifikationskode", Ver(product.GetAttribute("documentation_tag"))));
                    rows.Add(new ReportLabelValue("Serie nummer", Ver(product.GetAttribute("serialnumber"))));
                    rows.Add(new ReportLabelValue("Lysgruppe", Ver(product.GetAttribute("power_group"))));
                    break;
                case ReportProductKind.Rs485LedDimmer:
                    rows.Add(new ReportLabelValue("Serie nummer", Ver(product.GetAttribute("serialnumber"))));
                    break;
                default: // Rs485Modem / Rs485SmsModem
                    rows.Add(new ReportLabelValue("Identifikationskode", Ver(product.GetAttribute("documentation_tag"))));
                    rows.Add(new ReportLabelValue("Ledningsfarve 0V", Ver(product.GetAttribute("cablecolour_0V"))));
                    rows.Add(new ReportLabelValue("Ledningsfarve 24V", Ver(product.GetAttribute("cablecolour_24V"))));
                    rows.Add(new ReportLabelValue("Ledningsfarve RS485Minus", Ver(product.GetAttribute("cablecolour_RS485Minus"))));
                    rows.Add(new ReportLabelValue("Ledningsfarve RS485Plus", Ver(product.GetAttribute("cablecolour_RS485Plus"))));
                    break;
            }
            return new ProductDetailTable(kind, rows.ToImmutable(), terminals);
        }

        // The vendor per-product terminal sub-table walks .//dataline_input | .//dataline_output (any depth),
        // in document order (a node-set union renders document-ordered).
        private static ImmutableArray<ReportTerminalRow> BuildTerminalRows(ProjectElement product) =>
            product.Descendants()
                .Where(e => e.Tag == "dataline_input" || e.Tag == "dataline_output")
                .Select(t =>
                {
                    bool isInput = t.Tag == "dataline_input";
                    string direction = isInput ? "Indgang" : "Udgang";
                    string address = DatalineAddress.ToVendorLabel(t.GetAttribute("address_dataline"), isOutput: !isInput);
                    return new ReportTerminalRow(Ver(t.GetAttribute("name")), direction + " " + address, Ver(t.GetAttribute("cable_colour")));
                })
                .ToImmutableArray();

        private static DatalineCrossReferenceRow BuildCrossReferenceRow(ProjectElement terminal, TreeIndex index, bool isOutput)
        {
            ProjectElement? product = index.NearestProduct(terminal);
            string ProductAttr(string name) => product is null ? Unknown : Raw(product.GetAttribute(name));
            string locality = product is null ? Unknown : Raw(index.Parent(product)?.GetAttribute("name"));
            return new DatalineCrossReferenceRow(
                DatalineAddress.ToVendorLabel(terminal.GetAttribute("address_dataline"), isOutput),
                product is null ? Unknown : Raw(product.GetAttribute("name")),
                Raw(terminal.GetAttribute("name")),
                Raw(terminal.GetAttribute("note")),
                locality,
                ProductAttr("position"),
                ProductAttr("documentation_tag"),
                ProductAttr("cabletype"),
                ProductAttr("cablenumber"),
                ProductAttr("power_group"),
                Raw(terminal.GetAttribute("cable_colour")));
        }

        private static SpecialProductRow BuildSpecialProductRow(ProjectElement modem, TreeIndex index) =>
            new(Raw(modem.GetAttribute("name")),
                Raw(modem.GetAttribute("name")),
                Raw(modem.GetAttribute("note")),
                Raw(index.Parent(modem)?.GetAttribute("name")),
                Raw(modem.GetAttribute("position")),
                Raw(modem.GetAttribute("documentation_tag")),
                Raw(modem.GetAttribute("cablecolour_0V")),
                Raw(modem.GetAttribute("cablecolour_24V")),
                Raw(modem.GetAttribute("cablecolour_RS485Minus")),
                Raw(modem.GetAttribute("cablecolour_RS485Plus")));

        private static S0DeviceRow BuildS0DeviceRow(ProjectElement s0, TreeIndex index) =>
            new(Raw(s0.GetAttribute("name")),
                Raw(s0.GetAttribute("note")),
                Raw(index.Parent(s0)?.GetAttribute("name")),
                Raw(s0.GetAttribute("position")),
                Raw(s0.GetAttribute("documentation_tag")),
                Raw(s0.GetAttribute("cable_colour_minus")),
                Raw(s0.GetAttribute("cable_colour_plus")));

        // ----- end-user: locality → product → terminal → note -----

        private static EndUserLocality BuildEndUserLocality(ProjectElement group, TreeIndex index)
        {
            string localityName = Raw(group.GetAttribute("name"));
            ImmutableArray<EndUserProduct> products = group.ChildrenOrEmpty()
                .Where(c => (c.Tag == "product_dataline" || c.Tag == "product_airlink")
                            && c.GetAttribute("enduser_report") == "yes")
                .Select(p => BuildEndUserProduct(p, localityName, index))
                .ToImmutableArray();
            return new EndUserLocality(localityName, Raw(group.GetAttribute("id")), products);
        }

        private static EndUserProduct BuildEndUserProduct(ProjectElement product, string localityName, TreeIndex index)
        {
            string? position = product.GetAttribute("position");
            ImmutableArray<ProjectElement> children = product.ChildrenOrEmpty();
            IEnumerable<ProjectElement> terminals = product.Tag == "product_airlink"
                ? children.Where(c => c.Tag == "airlink_input")
                : children.Where(c => c.Tag == "dataline_input").Concat(children.Where(c => c.Tag == "dataline_output"));
            return new EndUserProduct(
                Raw(product.GetAttribute("name")),
                string.IsNullOrWhiteSpace(position) ? string.Empty : position,
                Raw(product.GetAttribute("product_identifier")),
                terminals.Select(t => BuildEndUserTerminal(t, localityName, index)).ToImmutableArray());
        }

        private static EndUserTerminal BuildEndUserTerminal(ProjectElement terminal, string localityName, TreeIndex index)
        {
            // A note is reached through the link: the driving side is link_to_resource for a product output,
            // link_from_resource for an input (dataline or airlink). id(@link) resolves to the reciprocal row on
            // the function-block side, whose parent (the FB resource) carries the propagated @note.
            string linkTag = terminal.Tag == "dataline_output" ? "link_to_resource" : "link_from_resource";
            var notes = ImmutableArray.CreateBuilder<EndUserNote>();
            foreach (ProjectElement linkRow in terminal.ChildrenOrEmpty().Where(c => c.Tag == linkTag))
            {
                ProjectElement? target = index.ById(linkRow.GetAttribute("link"));
                if (target is null)
                {
                    continue;   // dangling IDREF: the vendor's id(@link) yields an empty node set → no sub-line
                }
                string note = Raw(index.Parent(target)?.GetAttribute("note"));
                string fbLocality = Raw(index.Ancestor(target, 4)?.GetAttribute("name"));
                // Screen-only suffix: shown only when the driving block's locality is not a prefix of the
                // product's locality (an empty fb-locality is a prefix of everything → no suffix).
                bool showSuffix = !localityName.StartsWith(fbLocality, StringComparison.Ordinal);
                notes.Add(new EndUserNote(note, showSuffix ? fbLocality : string.Empty));
            }
            return new EndUserTerminal(Raw(terminal.GetAttribute("name")), notes.ToImmutable());
        }

        // ----- shared helpers -----

        // The report's section membership stays the closed DetailProductTags/ModemTags filters above; KindOf only
        // LABELS an already-admitted element, delegating to the shared SDK classifier's exact result. An admitted
        // element is always one of the five known tags, so the _ => Rs485Modem remnant is reachable only for the one
        // closed-set product_rs485_modem tag, exactly as before (open-world Other never reaches a report section).
        private static ReportProductKind KindOf(string tag) => ProductClassifier.Classify(tag) switch
        {
            ProductFamily.Dataline => ReportProductKind.Dataline,
            ProductFamily.Airlink => ReportProductKind.Airlink,
            ProductFamily.Rs485LedDimmer => ReportProductKind.Rs485LedDimmer,
            ProductFamily.Rs485SmsModem => ReportProductKind.Rs485SmsModem,
            _ => ReportProductKind.Rs485Modem,
        };

        // The vendor sort key: the hex after '_0x' left-padded with zeros to width 4, compared as a string
        // (a stable sort, so equal keys — e.g. every unassigned '_0x0' → "0000" — keep document order).
        private static string AddressSortKey(ProjectElement e)
        {
            string token = e.GetAttribute("address_dataline") ?? string.Empty;
            string hex = token.StartsWith("_0x", StringComparison.Ordinal) ? token.Substring(3) : string.Empty;
            return hex.Length < 4 ? hex.PadLeft(4, '0') : hex;
        }

        /// <summary>
        /// Parent pointers, id resolution and ancestor walks over an immutable project tree (which has no parent
        /// links), built once per report. Keyed by reference so distinct nodes never collide.
        /// </summary>
        private sealed class TreeIndex
        {
            private readonly Dictionary<ProjectElement, ProjectElement> parents = new(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<string, ProjectElement> byId = new(StringComparer.Ordinal);

            public TreeIndex(ProjectElement root) => Walk(root);

            private void Walk(ProjectElement element)
            {
                string? id = element.GetAttribute("id");
                if (id is not null)
                {
                    byId.TryAdd(id, element);   // first-wins, matching XPath id() on a well-formed unique-id tree
                }
                foreach (ProjectElement child in element.ChildrenOrEmpty())
                {
                    parents[child] = element;
                    Walk(child);
                }
            }

            public ProjectElement? Parent(ProjectElement element) =>
                parents.TryGetValue(element, out ProjectElement? parent) ? parent : null;

            public ProjectElement? ById(string? idToken) =>
                idToken is not null && byId.TryGetValue(idToken, out ProjectElement? element) ? element : null;

            public ProjectElement? Ancestor(ProjectElement element, int levels)
            {
                ProjectElement? current = element;
                for (int i = 0; i < levels && current is not null; i++)
                {
                    current = Parent(current);
                }
                return current;
            }

            // The vendor get_product_* ancestry: the node itself, its parent, or its grandparent, whichever is
            // the first product_* element (matching name()/parent/../parent). Null when none — the "?" case.
            public ProjectElement? NearestProduct(ProjectElement terminal)
            {
                ProjectElement? current = terminal;
                for (int i = 0; i < 3 && current is not null; i++)
                {
                    if (current.Tag.StartsWith("product_", StringComparison.Ordinal))
                    {
                        return current;
                    }
                    current = Parent(current);
                }
                return null;
            }
        }
    }
}
