using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The hand-authored, spec-derived standard grammar each named builder factory seeds, so the common authoring
    /// path needs no explicit grammar work at all: every element type the family's <b>closed-emitter</b> builder
    /// verbs can produce (family root, I/O pins, scenes — and on the function-block side every container,
    /// program-graph node and enum-stub type the program verbs emit) is declared with the family's standard
    /// attribute set. Open-world <c>Create(rootTag, …)</c> deliberately seeds <b>no</b> preset.
    /// </summary>
    /// <remarks>
    /// Two invariants keep these from rotting (both committed tests): the reflection classification test fails on
    /// any UNCLASSIFIED new builder verb and requires every closed-emitter tag ⊆ its family preset; and each
    /// preset's CONTENT (attribute types, defaults, IDREF classification) is byte-pinned by a designated synthetic
    /// oracle whose header is exactly the preset's rendering (<c>synthetic_9f02</c> dataline, <c>9f04</c> airlink,
    /// <c>9f05</c> RS485 LED dimmer, <c>9f06</c> RS485 SMS modem, <c>9f07</c> S0, <c>fb08</c> function block) —
    /// authored from the bare factory in the oracle byte tests. Body verbs never mutate the grammar; a superset
    /// preset over a lean body is the authentic vendor shape (81 corpus files declare types they never use).
    /// </remarks>
    internal static class CatalogGrammarPresets
    {
        // ---- shared attribute shorthands (vendor-standard shapes) ----

        // Enumeration token sets shared by the shorthands below. Hoisted because each shorthand is called once per
        // declared attribute across every preset, and GrammarAttr.Enumerated copies the tokens into an
        // ImmutableArray rather than retaining what it is handed.
        private static readonly string[] YesNoTokens = { "yes", "no" };
        private static readonly string[] OnOffTokens = { "on", "off" };
        private static readonly string[] AccessibilityTokens = { "read", "write", "read-write" };

        private static GrammarAttr Id() => GrammarAttr.Id("id");
        private static GrammarAttr Name(string @default = "") => GrammarAttr.Cdata("name", @default);
        private static GrammarAttr Note() => GrammarAttr.Cdata("note", "");
        private static GrammarAttr Udf() => GrammarAttr.Cdata("udf", "");
        private static GrammarAttr Icon(string @default) => GrammarAttr.Cdata("icon", @default);
        private static GrammarAttr YesNo(string name, string @default) =>
            GrammarAttr.Enumerated(name, YesNoTokens, @default);
        private static GrammarAttr OnOff(string name, string @default) =>
            GrammarAttr.Enumerated(name, OnOffTokens, @default);

        private static GrammarDeclaration Scenes() => GrammarDeclaration.Element("scenes",
            Id(), Name(), GrammarAttr.IdRefRequired("scene_resource"), Note());

        // ---- product families ----

        /// <summary>Dataline family (root + I/O pins + scenes) — pinned by <c>synthetic_9f02_output.def</c>.</summary>
        public static CatalogGrammar Dataline { get; } = CatalogGrammar.Create(new[]
        {
            GrammarDeclaration.Element("product_dataline",
                Id(), GrammarAttr.CdataRequired("product_identifier"), Name(),
                YesNo("locked", "yes"), YesNo("enduser_report", "no"),
                GrammarAttr.Cdata("icon", ""), Note(),
                GrammarAttr.Cdata("position", ""), GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""), GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", "")),
            GrammarDeclaration.Element("dataline_input",
                Id(), Name(), Note(), OnOff("inivalue", "off"),
                GrammarAttr.Cdata("address_dataline", "_0x0"), GrammarAttr.Cdata("cable_colour", "")),
            GrammarDeclaration.Element("dataline_output",
                Id(), Name(), Note(), OnOff("inivalue", "off"), YesNo("backup", "yes"),
                GrammarAttr.Enumerated("type", new[] { "led", "unspecified" }, "unspecified"),
                GrammarAttr.Cdata("address_dataline", "_0x0"), GrammarAttr.Cdata("cable_colour", "")),
            // scenes is declared LAST in the Dataline preset because that is the child order the vendor
            // synthetic_<...>_dataline.def oracle pins — the byte-fidelity tests compare against it, so the
            // declaration sequence here is not free to reorder.
            GrammarDeclaration.Element("scenes",
                Id(), GrammarAttr.IdRefRequired("scene_resource"), Name(), Note()),
        });

        /// <summary>Airlink (wireless) family — pinned by <c>synthetic_9f04_wireless.def</c>.</summary>
        public static CatalogGrammar Airlink { get; } = CatalogGrammar.Create(new[]
        {
            GrammarDeclaration.Element("product_airlink",
                Id(), GrammarAttr.CdataRequired("product_identifier"), GrammarAttr.CdataRequired("device_type"),
                Name(), YesNo("locked", "yes"), YesNo("enduser_report", "yes"),
                GrammarAttr.Cdata("icon", ""), Note(),
                GrammarAttr.Cdata("position", ""), GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""), GrammarAttr.Cdata("serialnumber", "")),
            GrammarDeclaration.Element("airlink_relay",
                Id(), GrammarAttr.CdataRequired("address_channel"), Name(), Note(),
                OnOff("inivalue", "off"), YesNo("backup", "yes")),
            Scenes(),
        });

        /// <summary>RS485 LED-dimmer family — pinned by <c>synthetic_9f05_dimmer.def</c>.</summary>
        public static CatalogGrammar Rs485LedDimmer { get; } = CatalogGrammar.Create(new[]
        {
            GrammarDeclaration.Element("product_rs485_led_dimmer",
                Id(), GrammarAttr.CdataRequired("product_identifier"), Name(),
                YesNo("locked", "yes"), YesNo("enduser_report", "no"),
                GrammarAttr.Cdata("icon", ""), Note(),
                GrammarAttr.Cdata("position", ""), GrammarAttr.Cdata("serialnumber", ""),
                GrammarAttr.Cdata("documentation_tag", "")),
            GrammarDeclaration.Element("rs485_led_dimmer_channel",
                Id(), Name(), Note(), GrammarAttr.Cdata("channel", ""), GrammarAttr.Cdata("channel_id", ""),
                GrammarAttr.Cdata("icon", "")),
            GrammarDeclaration.Element("resource_flag",
                Id(), Name("Flag"), YesNo("backup", "no"), Icon("_0x33"), Note(), OnOff("inivalue", "off"),
                GrammarAttr.Enumerated("access", new[] { "readonly", "readwrite", "writeonly" }, "readwrite")),
            GrammarDeclaration.Element("airlink_dimmer_increase",
                Id(), GrammarAttr.CdataRequired("address_channel"), Name(), Note()),
            GrammarDeclaration.Element("airlink_dimmer_decrease",
                Id(), GrammarAttr.CdataRequired("address_channel"), Name(), Note()),
            GrammarDeclaration.Element("airlink_dimming",
                Id(), GrammarAttr.CdataRequired("address_channel"), Name(), Note()),
            GrammarDeclaration.Element("light_indication", Id(), Name(), Note()),
            Scenes(),
            GrammarDeclaration.Element("dimmer_settings", Id(), Name(), Note()),
            DimmerSetting("dimmer_setting_minimum_value", "22", "0", "100"),
            DimmerSetting("dimmer_setting_maximum_value", "100", "0", "100"),
            DimmerSetting("dimmer_setting_fade_rate_up", "700", "200", "60000"),
            DimmerSetting("dimmer_setting_fade_rate_down", "700", "200", "60000"),
            DimmerSetting("dimmer_setting_dimming_rate", "5000", "2000", "10000"),
            GrammarDeclaration.Element("dimmer_setting_load_mode",
                Id(), GrammarAttr.Enumerated("value", new[] { "auto", "rc", "rl_led" }, "rc")),
        });

        private static GrammarDeclaration DimmerSetting(string tag, string value, string minimum, string maximum) =>
            GrammarDeclaration.Element(tag,
                Id(), GrammarAttr.Cdata("value", value), GrammarAttr.Cdata("minimum", minimum),
                GrammarAttr.Cdata("maximum", maximum));

        /// <summary>RS485 SMS-modem family — pinned by <c>synthetic_9f06_modem.def</c>.</summary>
        public static CatalogGrammar Rs485SmsModem { get; } = CatalogGrammar.Create(new[]
        {
            GrammarDeclaration.Element("product_rs485_sms_modem",
                Id(), GrammarAttr.CdataRequired("product_identifier"), Name(),
                YesNo("locked", "yes"), Icon("_0x0"), Note(), GrammarAttr.Cdata("helpid", "_0x0"),
                GrammarAttr.Cdata("position", ""), GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""), GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", "")),
            GrammarDeclaration.Element("sms_modem_settings",
                Id(), Name(), Icon("_0x15"), Note(), GrammarAttr.Cdata("helpid", "_0x0")),
            GrammarDeclaration.Element("sms_modem_pincode",
                Id(), GrammarAttr.Cdata("value", ""), GrammarAttr.Cdata("minimum", ""),
                GrammarAttr.Cdata("maximum", "")),
            GrammarDeclaration.Element("sms_modem_phonenumber",
                Id(), GrammarAttr.CdataRequired("address"), GrammarAttr.Cdata("phonenumber", "")),
            Scenes(),
        });

        /// <summary>S0 metering family — pinned by <c>synthetic_9f07_meter.def</c>.</summary>
        public static CatalogGrammar S0Device { get; } = CatalogGrammar.Create(new[]
        {
            GrammarDeclaration.Element("s0_device",
                Id(), GrammarAttr.CdataRequired("product_identifier"), Name(),
                YesNo("locked", "yes"), YesNo("enduser_report", "no"),
                GrammarAttr.Cdata("icon", ""), Note(),
                GrammarAttr.Cdata("position", ""), GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("cable_colour_plus", ""), GrammarAttr.Cdata("cable_colour_minus", "")),
            GrammarDeclaration.Element("kWh",
                Id(), Name("kWh"), Note(), YesNo("backup", "no"), GrammarAttr.Cdata("inivalue", "0"),
                GrammarAttr.Enumerated("accessibility", AccessibilityTokens, "read")),
            GrammarDeclaration.Element("W",
                Id(), Name("W"), Note(), YesNo("backup", "no"), GrammarAttr.Cdata("inivalue", "0"),
                GrammarAttr.Enumerated("accessibility", AccessibilityTokens, "read")),
            GrammarDeclaration.Element("resource_date",
                Id(), Name("Date"), YesNo("backup", "no"), Icon("_0x29"), Note(),
                GrammarAttr.CdataRequired("year"), GrammarAttr.CdataRequired("month"),
                GrammarAttr.CdataRequired("day"),
                GrammarAttr.Enumerated("access", new[] { "readonly", "readwrite", "writeonly" }, "readwrite")),
            Scenes(),
        });

        // ---- function blocks ----

        private static GrammarDeclaration Container(string tag) =>
            GrammarDeclaration.Element(tag, Id(), Name(), Icon("_0x0"), Note(), Udf());

        private static GrammarDeclaration ProgramLeaf(string tag) =>
            GrammarDeclaration.Element(tag,
                Id(), Name(), Icon("_0x0"), Note(),
                GrammarAttr.IdRef("link1"), GrammarAttr.IdRef("link2"),
                GrammarAttr.Cdata("method", "_0x0"), Udf());

        private static GrammarDeclaration Pin(string tag) =>
            GrammarDeclaration.Element(tag,
                Id(), Name(), YesNo("backup", "no"), Icon("_0x0"), Note(), OnOff("inivalue", "off"),
                GrammarAttr.Enumerated("accessibility", AccessibilityTokens, "read-write"),
                GrammarAttr.Cdata("note-2", ""), Udf());

        /// <summary>The function-block standard grammar — every container, pin, program-graph node and enum-stub
        /// type the block/program verbs emit — pinned by <c>synthetic_fb08_full.ifb</c>.</summary>
        public static CatalogGrammar FunctionBlock { get; } = CatalogGrammar.Create(new[]
        {
            GrammarDeclaration.Element("functionblock",
                Id(), Name(), YesNo("master_schneider_electric", "no"),
                GrammarAttr.Cdata("master_type", ""), GrammarAttr.Cdata("master_version", ""),
                GrammarAttr.Cdata("master_name", ""), GrammarAttr.Cdata("master_programmer", ""),
                GrammarAttr.Cdata("master_date_year", "0"), GrammarAttr.Cdata("master_date_month", "0"),
                GrammarAttr.Cdata("master_date_day", "0"), YesNo("locked", "no"), Icon("_0x0"), Note(),
                GrammarAttr.Cdata("helpfile", ""), Udf()),
            Container("inputs"),
            Pin("resource_input"),
            Container("outputs"),
            Pin("resource_output"),
            Container("settings"),
            Container("internalsettings"),
            Container("programs"),
            Container("program_simple"),
            Container("events"),
            ProgramLeaf("event"),
            GrammarDeclaration.Element("event_power",
                Id(), Name(), Icon("_0x0"), Note(),
                GrammarAttr.Enumerated("type", new[] { "up", "down" }, "up"), Udf()),
            GrammarDeclaration.Element("actions",
                Id(), Name(), Icon("_0x0"), Note(), GrammarAttr.Cdata("type", "_0x0"), Udf()),
            Container("program_sub"),
            GrammarDeclaration.Element("conditions",
                Id(), Name(), Icon("_0x0"), Note(),
                GrammarAttr.Enumerated("type", new[] { "and", "or" }, "and"), Udf()),
            ProgramLeaf("condition"),
            ProgramLeaf("action"),
            GrammarDeclaration.Element("program_case",
                Id(), Name(), Icon("_0x0"), Note(), GrammarAttr.IdRef("link"), Udf()),
            GrammarDeclaration.Element("case_action",
                Id(), Name(), Icon("_0x0"), Note(),
                GrammarAttr.IdRefRequired("variable"), GrammarAttr.IdRefRequired("value"), Udf()),
            GrammarDeclaration.Element("enum_definition",
                Id(), GrammarAttr.Cdata("typeid", "_0x0"), Name(), Note(), Udf()),
            GrammarDeclaration.Element("enum_value",
                Id(), GrammarAttr.Cdata("typeid", "_0x0"), Name(), Note(),
                GrammarAttr.Cdata("index", "0"), Udf()),
            GrammarDeclaration.Element("resource_enum",
                Id(), Name(), GrammarAttr.IdRefRequired("typedef"), GrammarAttr.IdRefRequired("inivalue"),
                YesNo("backup", "no"), Icon("_0x0"), Note(),
                GrammarAttr.Enumerated("accessibility", AccessibilityTokens, "read-write"),
                GrammarAttr.Cdata("note-2", ""), Udf()),
        });
    }
}
