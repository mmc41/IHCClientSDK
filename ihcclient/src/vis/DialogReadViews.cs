#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace Ihc.Vis
{
    /// <summary>
    /// fablerefac W3-8: typed read views for the Properties dialogs — the read-side peers of the write
    /// <c>Ref</c> handles (<c>ProductRef</c>/<c>ResourceRef</c>/…). Each wraps a <see cref="Project"/> and one
    /// <see cref="ProjectElement"/> like <see cref="ElementView"/> and exposes that node's dialog fields as typed
    /// properties, so the attribute-name string literals the GUI used to assemble its dialog DTOs live here,
    /// SDK-side, not in the view-model / coordinator.
    /// <para><b>Raw, not effective:</b> the documentation/cable/address fields report the element's own stored value
    /// (the old raw <c>GetAttribute</c>), NOT the DTD-default-resolved <see cref="ElementView.Effective"/> — an
    /// editable dialog shows blank for an unset field and lets the SDK apply defaults on write (fablerefac W1-3).
    /// The flags (<c>Locked</c>/<c>EnduserReport</c>/initial value) and the dimmer settings read effective.
    /// <c>Name</c> reads through the shared <see cref="ElementView.Name"/> surface (so the <c>"name"</c> literal lives
    /// SDK-side in one place); because the <c>name</c> attribute's DTD default is the empty string, that effective
    /// read equals the raw value for every named element these views wrap and presents blank for an unset one.</para>
    /// </summary>
    public readonly record struct PinView(Project Project, ProjectElement Element)
    {
        private ElementView View => Project.View(Element);

        /// <summary>The pin's name (via the shared <see cref="ElementView.Name"/> read surface).</summary>
        public string? Name => View.Name;

        /// <summary>The pin's documentation note.</summary>
        public string? Note => Element.GetAttribute("note");

        /// <summary>The pin's cable colour.</summary>
        public string? CableColour => Element.GetAttribute("cable_colour");

        /// <summary>Whether this is an output pin (a <c>dataline_output</c>) — read through the shared
        /// <c>IsOutputPin</c> predicate; exact here since a PinView is always a dataline pin.</summary>
        public bool IsOutput => Element.IsOutputPin;

        /// <summary>Whether the pin's power-up initial value is on (US-012).</summary>
        public bool InitialValueOn => View.InitialValue == "on";

        /// <summary>Whether the output resumes its last value after a power failure rather than its initial value
        /// (the vendor's <i>Ved strømsvigt ▸ Gem aktuel værdi</i>). Meaningless for an input.</summary>
        public bool Backup => View.Backup;

        /// <summary>The raw stored data-line address token, for vendor-label formatting.</summary>
        public string? AddressToken => Element.GetAttribute("address_dataline");

        /// <summary>The decoded data-line address, or null when unassigned/invalid.</summary>
        public DatalineAddress? Address =>
            DatalineAddress.TryParse(AddressToken, IsOutput, out DatalineAddress a) ? a : null;

        /// <summary>The pin element's id.</summary>
        public ElementId? Id => Element.Id;
    }

    /// <summary>Typed read view of a product for its documentation dialog (US-011/US-012).</summary>
    public readonly record struct ProductView(Project Project, ProjectElement Element)
    {
        private ElementView View => Project.View(Element);

        public string? Name => View.Name;
        public string? Note => Element.GetAttribute("note");
        public string? CableType => Element.GetAttribute("cabletype");
        public string? CableNumber => Element.GetAttribute("cablenumber");
        public string? DocumentationTag => Element.GetAttribute("documentation_tag");
        public string? PowerGroup => Element.GetAttribute("power_group");
        public string? Position => Element.GetAttribute("position");

        /// <summary>The catalog product identifier used to title the dialog with the product TYPE (A-8/F-015).</summary>
        public string? ProductIdentifier => Element.GetAttribute("product_identifier");

        /// <summary>Whether the product is a locked (library) instance — its name is fixed (A-15/F-032).</summary>
        public bool Locked => View.Locked;

        /// <summary>Whether the product is included in the end-user report.</summary>
        public bool EnduserReport => View.EnduserReport;

        public bool IsWireless => ProductClassifier.IsWireless(Element.Tag);

        /// <summary>Whether this is a wireless product with an advanced dimmer configuration (US-015).</summary>
        public bool IsWirelessDimmer =>
            IsWireless && Element.FindDescendantOrSelf(e => e.Tag == "dimmer_settings") is not null;

        /// <summary>The product's input/output terminals (the addressing grids, US-012) as typed pin views.</summary>
        public IEnumerable<PinView> Terminals
        {
            get
            {
                Project project = Project;   // struct 'this' can't be captured by the projection lambda
                return Element.DescendantsAndSelf()
                    .Where(e => e.Kind == ElementKind.DatalinePin)
                    .Select(e => new PinView(project, e));
            }
        }

        /// <summary>
        /// The product's configurable SETTING RESOURCES — the rows of the vendor's <i>Indstillinger</i> grid, in
        /// declared order, each row's caller rendering the value its own way.
        /// <para>A setting is any resource the catalog marked <c>setting="yes"</c>, whatever its resource
        /// type: the six sensors that have them use <c>resource_temperature</c>, <c>resource_humidity</c>
        /// and <c>resource_light</c>. Keyed on the attribute rather than on a tag list, so a sensor type
        /// the SDK has not met still shows its settings (T070).</para>
        /// <para>The ELEMENTS, not a projected row: a calibration offset is shown as <c>0,0 °C</c>, not as the
        /// stored <c>0.00</c>, and how a typed value is rendered is frontend presentation policy (ADR-002). The
        /// name and note come from <c>project.View(element)</c> as for any other element, so there is no second
        /// projection of these three attributes to drift from the rendered grid.</para>
        /// </summary>
        public IEnumerable<ProjectElement> SettingElements =>
            Element.DescendantsAndSelf().Where(Products.ProductDialogComposer.IsSetting);
    }

    /// <summary>
    /// Typed read view of a function block for its properties dialog. Beyond the editable Name/Note every block has,
    /// a block that came from the LIBRARY also carries the read-only provenance of the master it was stamped from —
    /// which library block, its number and version, when it was made and by whom (the <c>master_*</c> attributes).
    /// A block authored from scratch has none of that, and <see cref="IsLibraryBlock"/> is how the dialog tells the
    /// two apart (uxparity S-19).
    /// </summary>
    public readonly record struct FunctionBlockView(Project Project, ProjectElement Element)
    {
        private ElementView View => Project.View(Element);

        public string? Name => View.Name;
        public string? Note => Element.GetAttribute("note");
        public bool Locked => View.Locked;

        /// <summary>The library block this one was stamped from, blank for a block authored from scratch.</summary>
        public string? MasterName => Element.GetAttribute("master_name");

        /// <summary>The library number, e.g. <c>1.1.01</c>.</summary>
        public string? MasterType => Element.GetAttribute("master_type");

        /// <summary>The library version letter, e.g. <c>e</c>.</summary>
        public string? MasterVersion => Element.GetAttribute("master_version");

        /// <summary>Who developed the library block (may be multi-line, e.g. a copyright line).</summary>
        public string? MasterProgrammer => Element.GetAttribute("master_programmer");

        /// <summary>The date the library block was made, or null when it carries no usable date.</summary>
        public DateOnly? MasterDate
        {
            get
            {
                DateOnly? date = null;
                if (Part("master_date_year") is { } year && Part("master_date_month") is { } month
                    && Part("master_date_day") is { } day
                    && year <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
                    date = new DateOnly(year, month, day);
                return date;
            }
        }

        /// <summary>
        /// Whether this block is still a LIBRARY block — i.e. whether it has provenance to show at all. Keyed on
        /// <see cref="MasterType"/>, the library identity, and deliberately not on <see cref="MasterName"/>: an
        /// UNLOCKED block keeps the name it came from as its own authorship stamp while ceasing to be a library
        /// block, and the vendor stops reporting its origin at exactly that point (uxparity S-20).
        /// </summary>
        public bool IsLibraryBlock => !string.IsNullOrEmpty(MasterType);

        private int? Part(string attribute) =>
            int.TryParse(Element.GetAttribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int value) && value > 0
                ? value
                : null;
    }

    // ModemView is gone in full (T138), having lost its last reader.
    //
    // T031 removed its PhoneNumbers member; what the static review found is that the REST of it had gone the
    // same way without anyone saying so. Its consumer was the modem's own properties flow, and T030 routed the
    // modem through the one composed dialog like every other family — after which the only caller left in the
    // tree was the type's own unit test. A type exercised solely by its own test is not covered, it is
    // preserved: nothing would have failed if any of these eight properties had started returning nonsense.
    //
    // Every value it read is still reachable, and now through the surface that has readers: the composer
    // resolves `note`, `position`, `documentation_tag`, the four `cablecolour_*` and the PIN from the modem
    // preset's own bindings, so the dialog shows them and ApplyProductDialog writes them back. This deletes a
    // second way to read the same attributes, which is what made the pair able to disagree.

    /// <summary>Typed read view of a wireless dimmer's advanced settings (US-015).</summary>
    public readonly record struct DimmerView(Project Project, ProjectElement Element)
    {
        /// <summary>The positive stored value of the given <c>dimmer_setting_*</c>, or null when unset/zero — the
        /// caller applies the vendor factory default (the schema default is 0, so <c>Effective</c> reads 0 for an
        /// unset device; the dialog substitutes the factory constant, fablerefac W1-3 finding).</summary>
        public int? PositiveSetting(string settingTag)
        {
            ProjectElement? el = Element.FindDescendantOrSelf(e => e.Tag == settingTag);
            return el is not null && int.TryParse(Project.View(el).Effective("value"), out int v) && v > 0 ? v : null;
        }

        /// <summary>The dimmer's load mode (auto/rc/rl), defaulting to auto when unset.</summary>
        public string LoadMode =>
            Element.FindDescendantOrSelf(e => e.Tag == "dimmer_setting_load_mode") is { } lm
            && Project.View(lm).Effective("value") is { } mode ? mode : "auto";
    }
}
