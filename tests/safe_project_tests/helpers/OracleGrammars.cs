#nullable enable
namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The per-oracle structured grammars for the code-authored byte tests: each synthetic oracle whose header is
    /// not exactly a family preset gets its declarations transcribed here (from the oracle's own DTD — invented
    /// content, no vendor text), so a test authors the definition entirely from code and byte-compares the written
    /// file against the oracle. The declarations double as the test-side pin of the grammar model's expressiveness
    /// over the corpus envelope (orphan ATTLISTs, ELEMENT-only records, per-file variants, digit-leading tokens).
    /// </summary>
    internal static class OracleGrammars
    {
        // ---- shared attr shorthands ----
        private static GrammarAttr Id => GrammarAttr.Id("id");
        private static GrammarAttr Name(string d = "") => GrammarAttr.Cdata("name", d);
        private static GrammarAttr Note => GrammarAttr.Cdata("note", "");
        private static GrammarAttr Udf => GrammarAttr.Cdata("udf", "");
        private static GrammarAttr Note2 => GrammarAttr.Cdata("note-2", "");
        private static GrammarAttr Icon(string d) => GrammarAttr.Cdata("icon", d);
        private static GrammarAttr YesNo(string n, string d) => GrammarAttr.Enumerated(n, new[] { "yes", "no" }, d);
        private static GrammarAttr OnOff(string n, string d) => GrammarAttr.Enumerated(n, new[] { "on", "off" }, d);
        private static GrammarAttr Accessibility(string d = "read-write") =>
            GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, d);

        // ---- product-side declarations ----

        /// <summary>The full 12-attribute dataline root (<c>enduser_report</c> default varies per file).</summary>
        internal static GrammarDeclaration DatalineRoot(string enduserReportDefault) =>
            GrammarDeclaration.Element("product_dataline",
                Id, GrammarAttr.CdataRequired("product_identifier"), Name(),
                YesNo("locked", "yes"), YesNo("enduser_report", enduserReportDefault), Icon(""), Note,
                GrammarAttr.Cdata("position", ""), GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""), GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        /// <summary>The lean 7-attribute dataline root several grammar-envelope oracles use.</summary>
        internal static GrammarDeclaration DatalineRootLean7 { get; } = GrammarDeclaration.Element("product_dataline",
            Id, GrammarAttr.CdataRequired("product_identifier"), Name(),
            YesNo("locked", "yes"), YesNo("enduser_report", "no"), Icon(""), Note);

        /// <summary>The 6-attribute dataline root of the open-world oracle (no <c>enduser_report</c>).</summary>
        internal static GrammarDeclaration DatalineRootTiny6 { get; } = GrammarDeclaration.Element("product_dataline",
            Id, GrammarAttr.CdataRequired("product_identifier"), Name(), YesNo("locked", "yes"), Icon(""), Note);

        internal static GrammarDeclaration DatalineInput { get; } = GrammarDeclaration.Element("dataline_input",
            Id, Name(), Note, OnOff("inivalue", "off"),
            GrammarAttr.Cdata("address_dataline", "_0x0"), GrammarAttr.Cdata("cable_colour", ""));

        /// <summary>The superset oracle's input variant with the digit-leading NMTOKEN enumeration.</summary>
        internal static GrammarDeclaration DatalineInputPulse { get; } = GrammarDeclaration.Element("dataline_input",
            Id, Name(), Note, OnOff("inivalue", "off"),
            GrammarAttr.Enumerated("pulse_width", new[] { "24", "48", "none" }, "24"),
            GrammarAttr.Cdata("address_dataline", "_0x0"), GrammarAttr.Cdata("cable_colour", ""));

        internal static GrammarDeclaration DatalineOutput { get; } = GrammarDeclaration.Element("dataline_output",
            Id, Name(), Note, OnOff("inivalue", "off"), YesNo("backup", "yes"),
            GrammarAttr.Enumerated("type", new[] { "led", "unspecified" }, "unspecified"),
            GrammarAttr.Cdata("address_dataline", "_0x0"), GrammarAttr.Cdata("cable_colour", ""));

        internal static GrammarDeclaration ResourceTemperatureProduct { get; } =
            GrammarDeclaration.Element("resource_temperature",
                Id, Name(), Note, YesNo("backup", "no"), GrammarAttr.Cdata("inivalue", "20.00"), Accessibility());

        internal static GrammarDeclaration ResourceInputProduct { get; } =
            GrammarDeclaration.Element("resource_input",
                Id, Name(), YesNo("backup", "no"), Icon("_0x0"), Note, OnOff("inivalue", "off"), Accessibility());

        internal static GrammarDeclaration ResourceEnumProduct { get; } =
            GrammarDeclaration.Element("resource_enum",
                Id, Name("Enumerator"), GrammarAttr.IdRef("typedef"), GrammarAttr.IdRef("inivalue"),
                YesNo("backup", "no"), Icon("_0x22"), Note, Accessibility());

        /// <summary>The "med logning" orphan ATTLIST (vendor <c>product2125</c> shape, incl. <c>helpid</c>).</summary>
        internal static GrammarDeclaration ResourceEnumOrphan { get; } =
            GrammarDeclaration.AttlistOnly("resource_enum",
                Id, Name("Enumerator"), GrammarAttr.IdRef("typedef"), GrammarAttr.IdRef("inivalue"),
                YesNo("backup", "no"), Icon("_0x22"), Note, GrammarAttr.Cdata("helpid", "_0x5dc"), Accessibility());

        /// <summary>The invented non-registry orphan whose defaults drive insert materialization.</summary>
        internal static GrammarDeclaration SampleLogOrphan { get; } =
            GrammarDeclaration.AttlistOnly("resource_sample_log",
                Id, Name(), Note, YesNo("backup", "no"), Icon("_0x2c"),
                GrammarAttr.Cdata("inivalue", "500.00"), GrammarAttr.Cdata("interval", "300"),
                Accessibility("read"));

        internal static GrammarDeclaration SettingsProduct { get; } =
            GrammarDeclaration.Element("settings", Id, Name(), Note);

        internal static GrammarDeclaration SoilMoisture { get; } =
            GrammarDeclaration.Element("resource_soil_moisture",
                Id, Name(), Note, YesNo("backup", "no"), Icon("_0x0"),
                GrammarAttr.Cdata("inivalue", "0.00"), GrammarAttr.Cdata("unit", "%"), Accessibility("read"));

        /// <summary>The case-skew pair: a mis-cased ELEMENT-only declaration beside the orphan the body uses.</summary>
        internal static GrammarDeclaration SkewElementOnly { get; } = GrammarDeclaration.ElementOnly("resource_Skew");

        internal static GrammarDeclaration SkewOrphan { get; } = GrammarDeclaration.AttlistOnly("resource_skew",
            Id, Name(), Note, YesNo("backup", "no"), GrammarAttr.Cdata("inivalue", "500.00"), Accessibility());

        // ---- function-block-side declarations ----

        internal static GrammarDeclaration FbRoot { get; } = GrammarDeclaration.Element("functionblock",
            Id, Name(), YesNo("master_schneider_electric", "no"),
            GrammarAttr.Cdata("master_type", ""), GrammarAttr.Cdata("master_version", ""),
            GrammarAttr.Cdata("master_name", ""), GrammarAttr.Cdata("master_programmer", ""),
            GrammarAttr.Cdata("master_date_year", "0"), GrammarAttr.Cdata("master_date_month", "0"),
            GrammarAttr.Cdata("master_date_day", "0"), YesNo("locked", "no"), Icon("_0x0"), Note,
            GrammarAttr.Cdata("helpfile", ""), Udf);

        internal static GrammarDeclaration Container(string tag) =>
            GrammarDeclaration.Element(tag, Id, Name(), Icon("_0x0"), Note, Udf);

        internal static GrammarDeclaration FbActions { get; } = GrammarDeclaration.Element("actions",
            Id, Name(), Icon("_0x0"), Note, GrammarAttr.Cdata("type", "_0x0"), Udf);

        internal static GrammarDeclaration FbConditions { get; } = GrammarDeclaration.Element("conditions",
            Id, Name(), Icon("_0x0"), Note, GrammarAttr.Enumerated("type", new[] { "and", "or" }, "and"), Udf);

        internal static GrammarDeclaration ProgramLeaf(string tag) => GrammarDeclaration.Element(tag,
            Id, Name(), Icon("_0x0"), Note, GrammarAttr.IdRef("link1"), GrammarAttr.IdRef("link2"),
            GrammarAttr.Cdata("method", "_0x0"), Udf);

        internal static GrammarDeclaration EventPower { get; } = GrammarDeclaration.Element("event_power",
            Id, Name(), Icon("_0x0"), Note, GrammarAttr.Enumerated("type", new[] { "up", "down" }, "up"), Udf);

        internal static GrammarDeclaration FbPin(string tag, string inivalueDefault = "off") =>
            GrammarDeclaration.Element(tag,
                Id, Name(), YesNo("backup", "no"), Icon("_0x0"), Note, OnOff("inivalue", inivalueDefault),
                Accessibility(), Note2, Udf);

        internal static GrammarDeclaration ResourceTimerFb { get; } = GrammarDeclaration.Element("resource_timer",
            Id, Name(), YesNo("backup", "no"), Icon("_0x0"), Note,
            GrammarAttr.CdataRequired("hour"), GrammarAttr.CdataRequired("minute"),
            GrammarAttr.CdataRequired("second"), GrammarAttr.CdataRequired("millisecond"), Note2, Udf);

        internal static GrammarDeclaration ResourceSceneFb { get; } = GrammarDeclaration.Element("resource_scene",
            Id, Name(), Note, Icon("_0x89"), YesNo("hide_dialog", "no"), Note2, Udf);

        internal static GrammarDeclaration EnumDefinitionFb { get; } = GrammarDeclaration.Element("enum_definition",
            Id, GrammarAttr.Cdata("typeid", "_0x0"), Name(), Note, Udf);

        internal static GrammarDeclaration EnumValueFb { get; } = GrammarDeclaration.Element("enum_value",
            Id, GrammarAttr.Cdata("typeid", "_0x0"), Name(), Note, GrammarAttr.Cdata("index", "0"), Udf);

        internal static GrammarDeclaration ResourceEnumFb { get; } = GrammarDeclaration.Element("resource_enum",
            Id, Name(), GrammarAttr.IdRefRequired("typedef"), GrammarAttr.IdRefRequired("inivalue"),
            YesNo("backup", "no"), Icon("_0x0"), Note, Accessibility(), Note2, Udf);

        internal static GrammarDeclaration ResourceDateFb { get; } = GrammarDeclaration.Element("resource_date",
            Id, Name(), YesNo("backup", "no"), Icon("_0x0"), Note,
            GrammarAttr.CdataRequired("year"), GrammarAttr.CdataRequired("month"),
            GrammarAttr.CdataRequired("day"), Note2, Udf);

        internal static GrammarDeclaration ResourceFlagFb(string inivalueDefault, string iconDefault = "_0x0") =>
            GrammarDeclaration.Element("resource_flag",
                Id, Name(), YesNo("backup", "no"), Icon(iconDefault), Note, OnOff("inivalue", inivalueDefault),
                Accessibility(), Note2, Udf);

        internal static GrammarDeclaration ResourceTemperatureFb { get; } =
            GrammarDeclaration.Element("resource_temperature",
                Id, Name(), YesNo("backup", "no"), Note, GrammarAttr.Cdata("inivalue", "0.00"),
                Accessibility(), YesNo("setting", "no"), Note2, Udf);

        internal static GrammarDeclaration ResourceCounterFb { get; } = GrammarDeclaration.Element("resource_counter",
            Id, Name(), YesNo("backup", "no"), Note, GrammarAttr.Cdata("inivalue", "0"), Note2, Udf);

        internal static GrammarDeclaration ResourceWeekdayFb { get; } = GrammarDeclaration.Element("resource_weekday",
            Id, Name(), YesNo("backup", "no"), Icon("_0x0"), Note,
            GrammarAttr.Enumerated("inivalue",
                new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" }, "monday"),
            Note2, Udf);

        internal static GrammarDeclaration ResourceIntegerFb { get; } = GrammarDeclaration.Element("resource_integer",
            Id, Name(), YesNo("backup", "no"), Note, GrammarAttr.Cdata("inivalue", "0"), Note2, Udf);

        internal static GrammarDeclaration ResourceTimeFb { get; } = GrammarDeclaration.Element("resource_time",
            Id, Name(), YesNo("backup", "no"), Icon("_0x0"), Note,
            GrammarAttr.CdataRequired("hour"), GrammarAttr.CdataRequired("minute"),
            GrammarAttr.CdataRequired("second"), Note2, Udf);
    }
}
