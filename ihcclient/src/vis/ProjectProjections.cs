using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
        string Description, string Number, string Programmer, string Type, string Drawing,
        ContactInfo Customer, ContactInfo Installer)
    {
        /// <summary>The all-blank project information (returned when no project is open).</summary>
        public static readonly ProjectInfoData Empty =
            new("", "", "", "", "", ContactInfo.Empty, ContactInfo.Empty);
    }

    /// <summary>A read-only system data table (US-049): its name and its reference rows. These are the built-in
    /// (<c>typeid</c>-bearing) enum definitions — shown for reference, never edited.</summary>
    public sealed record DataTableView(string Name, EquatableArray<string> Rows);

    /// <summary>One editable user-defined text (US-049): its element id token (for edit/delete) and its text.</summary>
    public sealed record UserText(string Id, string Text);

    /// <summary>One enumerator type as the <i>Rediger Enumerator typer</i> editor sees it: its stored name, its
    /// ordered value labels, and whether it is a <c>typeid</c>-bearing built-in. IHC Visual shows a built-in as
    /// "<c>&lt;name&gt; [read only]</c>" and greys every mutation on it — <see cref="DisplayName"/> is that label, so
    /// the suffix is derived in ONE place and never stored in the project.</summary>
    public sealed record EnumTypeView(string Name, bool IsReadOnly, EquatableArray<string> Values)
    {
        /// <summary>What the type list shows: the stored name, plus IHC Visual's "[read only]" marker for a built-in.</summary>
        public string DisplayName => IsReadOnly ? Name + " [read only]" : Name;
    }

    /// <summary>An <c>enum_value</c>'s ordinal: its <c>index</c> attribute, or 0 when absent — the DTD elides
    /// <c>index="0"</c>, so a missing attribute is the zero value and not a missing ordinal. The ONE reader of that
    /// rule, shared by the read projection and the editor's re-numbering.</summary>
    internal static class EnumValueIndex
    {
        internal static int Of(ProjectElement value) =>
            int.TryParse(value.GetAttribute("index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : 0;
    }

    /// <summary>How the enumerator-type list is ordered — alphabetically, as the vendor's dialog does it. Danish
    /// collation, because this application is Danish-only and da-DK sorts Æ/Ø/Å after Z where the invariant
    /// comparer would not. Named rather than inlined so the ONE ordering rule is visible and testable.</summary>
    internal static class EnumTypeDisplayOrder
    {
        private static readonly StringComparer Comparer =
            StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), ignoreCase: true);

        internal static int Compare(string a, string b) => Comparer.Compare(a, b);
    }

    /// <summary>The data-tables read model (US-049): the read-only system tables and the editable user-defined texts.</summary>
    public sealed record DataTablesModel(EquatableArray<DataTableView> SystemTables, EquatableArray<UserText> UserTexts)
    {
        /// <summary>The empty model (no tables, no texts) — the projection for a closed/empty document.</summary>
        public static DataTablesModel Empty { get; } = new([], []);
    }

    /// <summary>One occupied module terminal (US-050): the decoded <c>line.terminal</c> address and the product
    /// terminal that occupies it.</summary>
    public sealed record ModuleAddressEntry(string Address, string Product, string Terminal);

    /// <summary>One data-line module slot (US-050): the data line, plus the module documented on it — its type,
    /// where it sits and its description, as the <c>documentation_modules</c> block records them. A line with no
    /// module documented on it carries the three blank and reads <see cref="InUse"/> <c>false</c>.</summary>
    public sealed record DatalineModule(int DataLine, string ModuleType, string Location, string Description)
    {
        /// <summary>Whether a module is documented on this data line.</summary>
        public bool InUse => ModuleType.Length > 0;
    }

    /// <summary>The data-line module map (US-050): every input and output data line the addressing model defines,
    /// each with the module documented on it (if any). The full slot inventory, not just the documented lines, so
    /// a reader sees which lines are still free.</summary>
    public sealed record DatalineModuleMap(
        EquatableArray<DatalineModule> InputModules, EquatableArray<DatalineModule> OutputModules)
    {
        /// <summary>The map of a closed/empty document — every slot present, none in use.</summary>
        public static DatalineModuleMap Empty { get; } = ProjectProjections.BuildDatalineModuleMap([]);
    }

    /// <summary>The Wired module address map (US-050): the addressed input-module and output-module terminals,
    /// read-only. Unaddressed terminals do not appear.</summary>
    public sealed record ModuleAddressMap(
        EquatableArray<ModuleAddressEntry> InputModules, EquatableArray<ModuleAddressEntry> OutputModules)
    {
        /// <summary>The empty map (no addressed terminals) — the projection for a closed/empty document.</summary>
        public static ModuleAddressMap Empty { get; } = new([], []);
    }

    /// <summary>
    /// API-D (fablerefac W1-5): the pure read projections over a <see cref="Project"/> — project/customer/installer
    /// information, the data tables, the unlinked-wireless pre-flight list, and the Wired module address map. Moved
    /// down from <c>ProjectSession</c> so they read through the SDK read surface (<c>project.View(element)</c>)
    /// rather than hand-parsing attributes, and so they are testable controller-free. The GUI (<c>ProjectWorkflow</c>)
    /// keeps thin delegators over these and evaluates them over the immutable <c>Current</c> snapshot exposed by its
    /// long-lived <c>IProjectDocument</c>. The document owns command execution and history; these projections remain
    /// pure read operations on a snapshot and do not need session state.
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
                    Attr(project, info, "type"),
                    Attr(project, info, "drawing"),
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
                    foreach (ProjectElement def in container.Children.Where(c => c.Tag == "enum_definition"))
                    {
                        List<ProjectElement> values = def.Children.Where(v => v.Tag == "enum_value").ToList();
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

            /// <summary>The enumerator types available as a variable's type (US-030 enum-type picker, PG-4): every
            /// project-global enum definition — the two built-ins and any user-authored type — EXCEPT the
            /// user-defined-texts data table. Returns each type's display name in document order, for the
            /// insert-variable picker (choosing one references its def-id; no new type is authored).</summary>
            public IReadOnlyList<string> GetEnumeratorTypes()
            {
                var types = new List<string>();
                foreach ((_, string name) in EnumeratorDefinitions(project))
                {
                    types.Add(name);
                }
                return types;
            }

            /// <summary>
            /// The enumerator types WITH their values, for IHC Visual's <i>Bibliotek ▸ Rediger Enumerator typer</i>
            /// two-pane editor. Same set as <see cref="GetEnumeratorTypes"/> (the user-texts data table stays out —
            /// it has its own dialog), plus each type's ordered values and whether it is a <c>typeid</c>-bearing
            /// built-in, which the vendor lists as "<c>[read only]</c>" and refuses every mutation on.
            /// </summary>
            /// <remarks>
            /// NEITHER list is in document order, and that is measured, not assumed (vendor session 2026-08-04):
            /// <list type="bullet">
            /// <item>TYPES come out alphabetically. Creating "IdxTest" in the vendor's dialog inserted it between
            /// "Hustilstand" and "Komfort-lys", not at the end.</item>
            /// <item>VALUES come out by <c>index</c>, which the file does NOT store in order: "Dimmer status" is
            /// written Reguléret(2), Sidste niveau(1), Slukket(0), … and the vendor lists it Slukket, Sidste niveau,
            /// Reguléret, … A document-order read shows the right names in the wrong order — and since the editor
            /// addresses a value by its POSITION in this list, it would also delete and rename the wrong one.</item>
            /// </list>
            /// <c>index</c> is absent on the 0 value (elided as the DTD default), so a missing attribute reads as 0.
            /// </remarks>
            public IReadOnlyList<EnumTypeView> GetEnumeratorTypeViews()
            {
                var types = new List<EnumTypeView>();
                foreach ((ProjectElement def, string name) in EnumeratorDefinitions(project))
                {
                    bool readOnly = (def.GetAttribute("typeid") ?? ElementId.NullToken) != ElementId.NullToken;
                    types.Add(new EnumTypeView(name, readOnly,
                        def.Children
                            .Where(v => v.Tag == "enum_value")
                            .OrderBy(v => EnumValueIndex.Of(v))
                            .Select(v => project.View(v).Name ?? string.Empty)
                            .ToImmutableArray()));
                }
                types.Sort((a, b) => EnumTypeDisplayOrder.Compare(a.DisplayName, b.DisplayName));
                return types;
            }

            /// <summary>Names the wireless products in the project not yet linked to the controller (US-042
            /// pre-flight): the offline half of the "warn about unlinked wireless products before sending" check.</summary>
            public IReadOnlyList<string> GetUnlinkedWirelessProducts()
            {
                var names = new List<string>();
                foreach (ProjectElement group in project.Groups)
                {
                    foreach (ProjectElement product in group.Children)
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
                    foreach (ProjectElement product in group.Children)
                    {
                        string productName = DisplayName(project.View(product), product);
                        foreach (ProjectElement pin in product.Children)
                        {
                            if (pin.Kind != ElementKind.DatalinePin)
                            {
                                continue;
                            }
                            bool isOutput = pin.IsOutputPin;
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

            /// <summary>Builds the read-only data-line module map (US-050): every input and output data line, each
            /// carrying the <c>documentation_modules</c> entry documented on it — module type, location and
            /// description. Lines with nothing documented on them are still listed, blank. A module whose
            /// <c>dataline</c> is unparseable or outside its direction's range is dropped; a second module on the
            /// same line loses to the first, so a line is one slot.</summary>
            public DatalineModuleMap GetDatalineModuleMap() =>
                BuildDatalineModuleMap(project.Root.DescendantsAndSelf());
        }

        // Shared by the projection and DatalineModuleMap.Empty, so the empty map is the same shape as a real one
        // (every slot present) rather than two empty arrays that would render as a grid with no rows.
        internal static DatalineModuleMap BuildDatalineModuleMap(IReadOnlyList<ProjectElement> all) =>
            new(Slots(all, "dataline_input_module", isOutput: false),
                Slots(all, "dataline_output_module", isOutput: true));

        private static ImmutableArray<DatalineModule> Slots(
            IReadOnlyList<ProjectElement> all, string tag, bool isOutput)
        {
            var documented = new Dictionary<int, DatalineModule>();
            int lines = DatalineAddress.MaxDataLine(isOutput);
            foreach (ProjectElement module in all.Where(e => e.Tag == tag))
            {
                if (!int.TryParse(module.GetAttribute("dataline"), NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int line) || line < 1 || line > lines || documented.ContainsKey(line))
                {
                    continue;
                }
                documented[line] = new DatalineModule(line,
                    module.GetAttribute("module_type") ?? string.Empty,
                    module.GetAttribute("location") ?? string.Empty,
                    module.GetAttribute("note") ?? string.Empty);
            }
            var slots = ImmutableArray.CreateBuilder<DatalineModule>(lines);
            for (int line = 1; line <= lines; line++)
            {
                slots.Add(documented.TryGetValue(line, out DatalineModule? m) ? m : new DatalineModule(line, "", "", ""));
            }
            return slots.MoveToImmutable();
        }

        /// <summary>
        /// The project-global enum definitions a type list offers, each with its display name: every
        /// <c>enum_definition</c> under <c>enum_definitions</c> except the user-defined-texts data table, which
        /// has its own dialog.
        /// </summary>
        /// <remarks>
        /// One walk behind both projections, because <c>GetEnumeratorTypeViews</c> promises the same set as
        /// <c>GetEnumeratorTypes</c> — a promise two hand-maintained copies of this filter can only keep by
        /// coincidence.
        /// </remarks>
        private static IEnumerable<(ProjectElement Definition, string Name)> EnumeratorDefinitions(Project project)
        {
            if (project.Child("enum_definitions") is not { } container)
            {
                yield break;
            }

            foreach (ProjectElement def in container.Children.Where(c => c.Tag == "enum_definition"))
            {
                if (project.View(def).Name is { } name && name != UserTextsTableName)
                {
                    yield return (def, name);
                }
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
