#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis
{
    // ---- API-D read-model record types (fablerefac W1-5, moved down from the GUI) ----

    /// <summary>A party's contact details (US-039) — a <c>customer_info</c>/<c>installer_info</c> record. The reports
    /// render only Name/Address/Phone; the rest are captured for completeness.</summary>
    public sealed record ContactInfo(
        string Name, string Address, string City, string Zip, string Country, string Phone, string Mobile, string Email)
    {
        /// <summary>An all-blank contact (the default before any project is loaded).</summary>
        public static readonly ContactInfo Empty = new("", "", "", "", "", "", "", "");
    }

    /// <summary>The project-information read model (US-039): project metadata plus the customer and installer
    /// contacts. Used both to prefill the dialog and as its edit result.</summary>
    public sealed record ProjectInfoData(
        string Description, string Number, string Programmer, ContactInfo Customer, ContactInfo Installer)
    {
        /// <summary>The all-blank project information (returned when no project is open).</summary>
        public static readonly ProjectInfoData Empty = new("", "", "", ContactInfo.Empty, ContactInfo.Empty);
    }

    /// <summary>A read-only system data table (US-049): its name and its reference rows. These are the built-in
    /// (<c>typeid</c>-bearing) enum definitions — shown for reference, never edited.</summary>
    public sealed record DataTableView(string Name, ImmutableArray<string> Rows);

    /// <summary>One editable user-defined text (US-049): its element id token (for edit/delete) and its text.</summary>
    public sealed record UserText(string Id, string Text);

    /// <summary>The data-tables read model (US-049): the read-only system tables and the editable user-defined texts.</summary>
    public sealed record DataTablesModel(ImmutableArray<DataTableView> SystemTables, ImmutableArray<UserText> UserTexts)
    {
        /// <summary>The empty model (no tables, no texts) — the projection for a closed/empty document.</summary>
        public static DataTablesModel Empty { get; } =
            new(ImmutableArray<DataTableView>.Empty, ImmutableArray<UserText>.Empty);
    }

    /// <summary>One occupied module terminal (US-050): the decoded <c>line.terminal</c> address and the product
    /// terminal that occupies it.</summary>
    public sealed record ModuleAddressEntry(string Address, string Product, string Terminal);

    /// <summary>The Wired module address map (US-050): the addressed input-module and output-module terminals,
    /// read-only. Unaddressed terminals do not appear.</summary>
    public sealed record ModuleAddressMap(
        ImmutableArray<ModuleAddressEntry> InputModules, ImmutableArray<ModuleAddressEntry> OutputModules)
    {
        /// <summary>The empty map (no addressed terminals) — the projection for a closed/empty document.</summary>
        public static ModuleAddressMap Empty { get; } =
            new(ImmutableArray<ModuleAddressEntry>.Empty, ImmutableArray<ModuleAddressEntry>.Empty);
    }

    /// <summary>
    /// API-D (fablerefac W1-5): the pure read projections over a <see cref="Project"/> — project/customer/installer
    /// information, the data tables, the unlinked-wireless pre-flight list, and the Wired module address map. Moved
    /// down from <c>ProjectSession</c> so they read through the SDK read surface (<c>project.View(element)</c>)
    /// rather than hand-parsing attributes, and so they are testable controller-free. The GUI (<c>ProjectWorkflow</c>)
    /// keeps thin delegators over these: they stay, because the GUI runs commands through a per-call scratch session
    /// rather than one persistent <c>ProjectDocumentSession</c> (the thread-affinity decision D12 superseded the
    /// persistent-session goal), so there is no long-lived session for the VM to query directly.
    /// </summary>
    public static class ProjectProjections
    {
        /// <summary>The dedicated user enum definition that holds the data-tables "user-defined texts" (US-049).</summary>
        public const string UserTextsTableName = "User-defined texts";

        extension(Project project)
        {
            /// <summary>Reads the project/customer/installer information (US-039).</summary>
            public ProjectInfoData GetProjectInfo()
            {
                ProjectElement? info = project.Child("project_info");
                return new(Attr(project, info, "description"),
                    Attr(project, info, "number"),
                    Attr(project, info, "programmer"),
                    ReadContact(project, project.Child("customer_info")),
                    ReadContact(project, project.Child("installer_info")));
            }

            /// <summary>Reads the project's data tables (US-049): the read-only system tables (the built-in
            /// <c>typeid</c>-bearing enum definitions) and the editable user-defined texts (the values of the
            /// <see cref="UserTextsTableName"/> enum).</summary>
            public DataTablesModel GetDataTables()
            {
                var system = ImmutableArray.CreateBuilder<DataTableView>();
                var texts = ImmutableArray.CreateBuilder<UserText>();
                if (project.Child("enum_definitions") is { } container)
                {
                    foreach (ProjectElement def in container.ChildrenOrEmpty().Where(c => c.Tag == "enum_definition"))
                    {
                        List<ProjectElement> values = def.ChildrenOrEmpty().Where(v => v.Tag == "enum_value").ToList();
                        ElementView defView = project.View(def);
                        if (defView.Name == UserTextsTableName)
                        {
                            foreach (ProjectElement v in values)
                            {
                                if (v.Id is { } id)
                                {
                                    texts.Add(new UserText(id.ToToken(), project.View(v).Name ?? string.Empty));
                                }
                            }
                        }
                        else if ((defView.Effective("typeid") ?? ElementId.NullToken) != ElementId.NullToken)
                        {
                            system.Add(new DataTableView(defView.Name ?? string.Empty,
                                values.Select(v => project.View(v).Name ?? string.Empty).ToImmutableArray()));
                        }
                    }
                }
                return new DataTablesModel(system.ToImmutable(), texts.ToImmutable());
            }

            /// <summary>Names the wireless products in the project not yet linked to the controller (US-042
            /// pre-flight): the offline half of the "warn about unlinked wireless products before sending" check.</summary>
            public IReadOnlyList<string> GetUnlinkedWirelessProducts()
            {
                var names = new List<string>();
                foreach (ProjectElement group in project.Groups)
                {
                    foreach (ProjectElement product in group.ChildrenOrEmpty())
                    {
                        ElementView view = project.View(product);
                        if (view.IsUnlinkedWireless)
                        {
                            names.Add(DisplayName(view, product));
                        }
                    }
                }
                return names;
            }

            /// <summary>Builds the read-only Wired module address map (US-050): every addressed
            /// <c>dataline_input</c>/<c>dataline_output</c> terminal, decoded to its <c>line.terminal</c> address and
            /// paired with the occupying product terminal, split into input and output modules and sorted by
            /// address. Unaddressed terminals are omitted; wireless products carry no module addressing.</summary>
            public ModuleAddressMap GetModuleAddressMap()
            {
                var inputs = new List<(int Line, int Terminal, ModuleAddressEntry Entry)>();
                var outputs = new List<(int Line, int Terminal, ModuleAddressEntry Entry)>();
                foreach (ProjectElement group in project.Groups)
                {
                    foreach (ProjectElement product in group.ChildrenOrEmpty())
                    {
                        string productName = DisplayName(project.View(product), product);
                        foreach (ProjectElement pin in product.ChildrenOrEmpty())
                        {
                            bool isOutput = pin.Tag == "dataline_output";
                            if (pin.Tag != "dataline_input" && !isOutput)
                            {
                                continue;
                            }
                            ElementView pinView = project.View(pin);
                            if (!DatalineAddress.TryParse(pinView.Effective("address_dataline"), isOutput, out DatalineAddress addr))
                            {
                                continue;
                            }
                            var entry = new ModuleAddressEntry(
                                $"{addr.DataLine}.{addr.Terminal}", productName, DisplayName(pinView, pin));
                            (isOutput ? outputs : inputs).Add((addr.DataLine, addr.Terminal, entry));
                        }
                    }
                }
                return new ModuleAddressMap(SortByAddress(inputs), SortByAddress(outputs));
            }
        }

        // The effective attribute value, or "" when the element is absent (all the metadata attributes default to
        // "", so an absent attribute already resolves to "" through the effective-value reader).
        private static string Attr(Project project, ProjectElement? element, string name) =>
            element is null ? string.Empty : project.View(element).Effective(name) ?? string.Empty;

        private static ContactInfo ReadContact(Project project, ProjectElement? c) => new(
            Attr(project, c, "name"), Attr(project, c, "address"), Attr(project, c, "city"), Attr(project, c, "zipcode"),
            Attr(project, c, "country"), Attr(project, c, "phone"), Attr(project, c, "mobilephone"), Attr(project, c, "email"));

        // The element's display name, or its tag when the name is empty (preserving the old `?? element.Tag`
        // fallback: a canonicalized project omits an empty name, so the effective name reads back as "").
        private static string DisplayName(ElementView view, ProjectElement element) =>
            view.Name is { Length: > 0 } name ? name : element.Tag;

        private static ImmutableArray<ModuleAddressEntry> SortByAddress(
            List<(int Line, int Terminal, ModuleAddressEntry Entry)> rows) =>
            rows.OrderBy(r => r.Line).ThenBy(r => r.Terminal).Select(r => r.Entry).ToImmutableArray();
    }
}
