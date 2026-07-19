#nullable enable
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
    /// Only the flags (<c>Locked</c>/<c>EnduserReport</c>/initial value) and the dimmer settings read effective.</para>
    /// </summary>
    public readonly record struct PinView(Project Project, ProjectElement Element)
    {
        private ElementView View => Project.View(Element);

        /// <summary>The pin's name.</summary>
        public string? Name => Element.GetAttribute("name");

        /// <summary>The pin's documentation note.</summary>
        public string? Note => Element.GetAttribute("note");

        /// <summary>The pin's cable colour.</summary>
        public string? CableColour => Element.GetAttribute("cable_colour");

        /// <summary>Whether this is an output pin (a <c>dataline_output</c>).</summary>
        public bool IsOutput => Element.Tag == "dataline_output";

        /// <summary>Whether the pin's power-up initial value is on (US-012).</summary>
        public bool InitialValueOn => View.InitialValue == "on";

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

        public string? Name => Element.GetAttribute("name");
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
        public bool IsWirelessDimmer => IsWireless && Element.DescendantsAndSelf().Any(e => e.Tag == "dimmer_settings");

        /// <summary>The product's input/output terminals (the addressing grids, US-012) as typed pin views.</summary>
        public IEnumerable<PinView> Terminals
        {
            get
            {
                Project project = Project;   // struct 'this' can't be captured by the projection lambda
                return Element.DescendantsAndSelf()
                    .Where(e => e.Tag is "dataline_input" or "dataline_output")
                    .Select(e => new PinView(project, e));
            }
        }
    }

    /// <summary>Typed read view of an SMS modem for its properties dialog.</summary>
    public readonly record struct ModemView(Project Project, ProjectElement Element)
    {
        public string? Name => Element.GetAttribute("name");
        public string? Note => Element.GetAttribute("note");
        public string? DocumentationTag => Element.GetAttribute("documentation_tag");
        public string? CableColour0V => Element.GetAttribute("cablecolour_0V");
        public string? CableColour24V => Element.GetAttribute("cablecolour_24V");
        public string? CableColourRS485Minus => Element.GetAttribute("cablecolour_RS485Minus");
        public string? CableColourRS485Plus => Element.GetAttribute("cablecolour_RS485Plus");

        /// <summary>The modem's stored PIN code, or null when none (the DTD default "0" is preserved verbatim; the
        /// dialog blanks it for presentation).</summary>
        public string? PinCode =>
            Element.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "sms_modem_pincode")?.GetAttribute("value");

        /// <summary>The four phone-number slots (1..4), blank where unset — the order the dialog shows them.</summary>
        public IReadOnlyList<string> PhoneNumbers
        {
            get
            {
                var phones = new List<string>();
                for (int slot = 1; slot <= 4; slot++)
                {
                    string s = slot.ToString(CultureInfo.InvariantCulture);
                    ProjectElement? pn = Element.DescendantsAndSelf()
                        .FirstOrDefault(e => e.Tag == "sms_modem_phonenumber" && e.GetAttribute("address") == s);
                    phones.Add(pn?.GetAttribute("phonenumber") ?? string.Empty);
                }
                return phones;
            }
        }
    }

    /// <summary>Typed read view of a wireless dimmer's advanced settings (US-015).</summary>
    public readonly record struct DimmerView(Project Project, ProjectElement Element)
    {
        /// <summary>The positive stored value of the given <c>dimmer_setting_*</c>, or null when unset/zero — the
        /// caller applies the vendor factory default (the schema default is 0, so <c>Effective</c> reads 0 for an
        /// unset device; the dialog substitutes the factory constant, fablerefac W1-3 finding).</summary>
        public int? PositiveSetting(string settingTag)
        {
            ProjectElement? el = Element.DescendantsAndSelf().FirstOrDefault(e => e.Tag == settingTag);
            return el is not null && int.TryParse(Project.View(el).Effective("value"), out int v) && v > 0 ? v : null;
        }

        /// <summary>The dimmer's load mode (auto/rc/rl), defaulting to auto when unset.</summary>
        public string LoadMode =>
            Element.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "dimmer_setting_load_mode") is { } lm
            && Project.View(lm).Effective("value") is { } mode ? mode : "auto";
    }
}
