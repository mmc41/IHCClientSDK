using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>The catalog's inline-DTD grammars: 100 distinct declaration records composing 99 distinct grammars.
    /// Symbol names are content-derived (<c>D_&lt;tag&gt;[_&lt;hash8&gt;]</c> / <c>G_&lt;hash8&gt;</c>) and the tables
    /// ordinal-sorted, so regeneration diffs are independent of corpus discovery order.</summary>
    internal static class BuiltInCatalogGrammar
    {
        internal static readonly GrammarDeclaration D_W =
            GrammarDeclaration.Element("W",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", "W"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("inivalue", "0"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read"));

        internal static readonly GrammarDeclaration D_action =
            GrammarDeclaration.Element("action",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.IdRef("link1"),
                GrammarAttr.IdRef("link2"),
                GrammarAttr.Cdata("method", "_0x0"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_actions =
            GrammarDeclaration.Element("actions",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("type", "_0x0"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_airlink_dimmer_decrease =
            GrammarDeclaration.Element("airlink_dimmer_decrease",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_airlink_dimmer_increase =
            GrammarDeclaration.Element("airlink_dimmer_increase",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_airlink_dimmer_touch =
            GrammarDeclaration.Element("airlink_dimmer_touch",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_airlink_dimming =
            GrammarDeclaration.Element("airlink_dimming",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_airlink_input =
            GrammarDeclaration.Element("airlink_input",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_airlink_relay_b6b7872d =
            GrammarDeclaration.Element("airlink_relay",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "off"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "yes"));

        internal static readonly GrammarDeclaration D_airlink_relay_d62683ed =
            GrammarDeclaration.Element("airlink_relay",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_airlink_shutter_down =
            GrammarDeclaration.Element("airlink_shutter_down",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_airlink_shutter_lock =
            GrammarDeclaration.Element("airlink_shutter_lock",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_airlink_shutter_up =
            GrammarDeclaration.Element("airlink_shutter_up",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address_channel"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_case_action =
            GrammarDeclaration.Element("case_action",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.IdRefRequired("variable"),
                GrammarAttr.IdRefRequired("value"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_condition =
            GrammarDeclaration.Element("condition",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.IdRef("link1"),
                GrammarAttr.IdRef("link2"),
                GrammarAttr.Cdata("method", "_0x0"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_conditions =
            GrammarDeclaration.Element("conditions",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("type", new[] { "and", "or" }, "and"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_dataline_input_3858bc1e =
            GrammarDeclaration.Element("dataline_input",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "on"),
                GrammarAttr.Cdata("address_dataline", "_0x0"),
                GrammarAttr.Cdata("cable_colour", ""));

        internal static readonly GrammarDeclaration D_dataline_input_8e8a3ac9 =
            GrammarDeclaration.Element("dataline_input",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "off"),
                GrammarAttr.Cdata("address_dataline", "_0x0"),
                GrammarAttr.Cdata("cable_colour", ""));

        internal static readonly GrammarDeclaration D_dataline_output_769ca3c8 =
            GrammarDeclaration.Element("dataline_output",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "off"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("type", new[] { "led", "unspecified" }, "unspecified"),
                GrammarAttr.Cdata("address_dataline", "_0x0"),
                GrammarAttr.Cdata("cable_colour", ""));

        internal static readonly GrammarDeclaration D_dataline_output_a09bc482 =
            GrammarDeclaration.Element("dataline_output",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "on"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("type", new[] { "led", "unspecified" }, "unspecified"),
                GrammarAttr.Cdata("address_dataline", "_0x0"),
                GrammarAttr.Cdata("cable_colour", ""));

        internal static readonly GrammarDeclaration D_dimmer_setting_dimming_rate =
            GrammarDeclaration.Element("dimmer_setting_dimming_rate",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("value", "5000"),
                GrammarAttr.Cdata("minimum", "2000"),
                GrammarAttr.Cdata("maximum", "10000"));

        internal static readonly GrammarDeclaration D_dimmer_setting_fade_rate_down =
            GrammarDeclaration.Element("dimmer_setting_fade_rate_down",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("value", "700"),
                GrammarAttr.Cdata("minimum", "200"),
                GrammarAttr.Cdata("maximum", "60000"));

        internal static readonly GrammarDeclaration D_dimmer_setting_fade_rate_up =
            GrammarDeclaration.Element("dimmer_setting_fade_rate_up",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("value", "700"),
                GrammarAttr.Cdata("minimum", "200"),
                GrammarAttr.Cdata("maximum", "60000"));

        internal static readonly GrammarDeclaration D_dimmer_setting_load_mode_65e5fdf6 =
            GrammarDeclaration.Element("dimmer_setting_load_mode",
                GrammarAttr.Id("id"),
                GrammarAttr.Enumerated("value", new[] { "auto", "rc", "rl" }, "rl"));

        internal static readonly GrammarDeclaration D_dimmer_setting_load_mode_6ab066b0 =
            GrammarDeclaration.Element("dimmer_setting_load_mode",
                GrammarAttr.Id("id"),
                GrammarAttr.Enumerated("value", new[] { "auto", "rc", "rl_led" }, "rc"));

        internal static readonly GrammarDeclaration D_dimmer_setting_load_mode_e97ec1b0 =
            GrammarDeclaration.Element("dimmer_setting_load_mode",
                GrammarAttr.Id("id"),
                GrammarAttr.Enumerated("value", new[] { "auto", "rc", "rl" }, "auto"));

        internal static readonly GrammarDeclaration D_dimmer_setting_maximum_value =
            GrammarDeclaration.Element("dimmer_setting_maximum_value",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("value", "100"),
                GrammarAttr.Cdata("minimum", "0"),
                GrammarAttr.Cdata("maximum", "100"));

        internal static readonly GrammarDeclaration D_dimmer_setting_minimum_value =
            GrammarDeclaration.Element("dimmer_setting_minimum_value",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("value", "22"),
                GrammarAttr.Cdata("minimum", "0"),
                GrammarAttr.Cdata("maximum", "100"));

        internal static readonly GrammarDeclaration D_dimmer_settings =
            GrammarDeclaration.Element("dimmer_settings",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_enum_definition_d7b36224 =
            GrammarDeclaration.Element("enum_definition",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("typeid", "_0x0"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_enum_definition_fbd50d75 =
            GrammarDeclaration.Element("enum_definition",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("typeid", "_0x0"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_enum_value_8603ac4b =
            GrammarDeclaration.Element("enum_value",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("typeid", "_0x0"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("index", ""));

        internal static readonly GrammarDeclaration D_enum_value_ade487cc =
            GrammarDeclaration.Element("enum_value",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("typeid", "_0x0"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("index", "0"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_event =
            GrammarDeclaration.Element("event",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.IdRef("link1"),
                GrammarAttr.IdRef("link2"),
                GrammarAttr.Cdata("method", "_0x0"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_event_power =
            GrammarDeclaration.Element("event_power",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("type", new[] { "up", "down" }, "up"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_events =
            GrammarDeclaration.Element("events",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_functionblock =
            GrammarDeclaration.Element("functionblock",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("master_schneider_electric", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("master_type", ""),
                GrammarAttr.Cdata("master_version", ""),
                GrammarAttr.Cdata("master_name", ""),
                GrammarAttr.Cdata("master_programmer", ""),
                GrammarAttr.Cdata("master_date_year", "0"),
                GrammarAttr.Cdata("master_date_month", "0"),
                GrammarAttr.Cdata("master_date_day", "0"),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("helpfile", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_inputs =
            GrammarDeclaration.Element("inputs",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_internalsettings =
            GrammarDeclaration.Element("internalsettings",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_kWh =
            GrammarDeclaration.Element("kWh",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", "kWh"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("inivalue", "0"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read"));

        internal static readonly GrammarDeclaration D_light_indication =
            GrammarDeclaration.Element("light_indication",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_link_from_resource =
            GrammarDeclaration.Element("link_from_resource",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.IdRefRequired("link"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_link_to_resource =
            GrammarDeclaration.Element("link_to_resource",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.IdRefRequired("link"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_outputs =
            GrammarDeclaration.Element("outputs",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_product_airlink_336ef2a7 =
            GrammarDeclaration.Element("product_airlink",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.CdataRequired("device_type"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("serialnumber", ""));

        internal static readonly GrammarDeclaration D_product_airlink_482ce24e =
            GrammarDeclaration.Element("product_airlink",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.CdataRequired("device_type"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("serialnumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_03a62d41 =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_26803675 =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", "Window Master WUC 101"),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_3a723af4 =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "no"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_3ef2041b =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", "Mini Modul 2 tryk"),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_597d90d4 =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", "Window Master WUC 102"),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_7ef16ca1 =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "no"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", "Anvendes til indikering."),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_b9aabc24 =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", "Mini Modul 1 tryk"),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_c5c3e260 =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", "Mini Modul 3 tryk"),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_dataline_d6becd96 =
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_product_rs485_led_dimmer =
            GrammarDeclaration.Element("product_rs485_led_dimmer",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("serialnumber", ""),
                GrammarAttr.Cdata("documentation_tag", ""));

        internal static readonly GrammarDeclaration D_product_rs485_sms_modem =
            GrammarDeclaration.Element("product_rs485_sms_modem",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("helpid", "_0x0"),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("power_group", ""),
                GrammarAttr.Cdata("cabletype", ""),
                GrammarAttr.Cdata("cablenumber", ""));

        internal static readonly GrammarDeclaration D_program_case =
            GrammarDeclaration.Element("program_case",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.IdRef("link"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_program_simple =
            GrammarDeclaration.Element("program_simple",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_program_sub =
            GrammarDeclaration.Element("program_sub",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_programs =
            GrammarDeclaration.Element("programs",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_Light =
            GrammarDeclaration.ElementOnly("resource_Light");

        internal static readonly GrammarDeclaration D_resource_counter =
            GrammarDeclaration.Element("resource_counter",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("inivalue", "0"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_date_30b57fe6 =
            GrammarDeclaration.Element("resource_date",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.CdataRequired("year"),
                GrammarAttr.CdataRequired("month"),
                GrammarAttr.CdataRequired("day"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_date_3ead75b7 =
            GrammarDeclaration.Element("resource_date",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", "Date"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x29"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.CdataRequired("year"),
                GrammarAttr.CdataRequired("month"),
                GrammarAttr.CdataRequired("day"),
                GrammarAttr.Cdata("helpid", "_0x578"),
                GrammarAttr.Enumerated("access", new[] { "readonly", "readwrite", "writeonly" }, "readwrite"));

        internal static readonly GrammarDeclaration D_resource_enum_5d30a24d =
            GrammarDeclaration.Element("resource_enum",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", "Enumerator"),
                GrammarAttr.CdataImplied("typedef"),
                GrammarAttr.CdataImplied("inivalue"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x22"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("helpid", "_0x5dc"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"));

        internal static readonly GrammarDeclaration D_resource_enum_8f33b7f1 =
            GrammarDeclaration.Element("resource_enum",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.IdRefRequired("typedef"),
                GrammarAttr.IdRefRequired("inivalue"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_enum_8f9fb582 =
            GrammarDeclaration.AttlistOnly("resource_enum",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", "Enumerator"),
                GrammarAttr.IdRef("typedef"),
                GrammarAttr.IdRef("inivalue"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x22"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("helpid", "_0x5dc"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"));

        internal static readonly GrammarDeclaration D_resource_enum_916fe757 =
            GrammarDeclaration.Element("resource_enum",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", "Enumerator"),
                GrammarAttr.IdRef("typedef"),
                GrammarAttr.IdRef("inivalue"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x22"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("helpid", "_0x5dc"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read"));

        internal static readonly GrammarDeclaration D_resource_flag_c251886e =
            GrammarDeclaration.Element("resource_flag",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "off"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_flag_d20940a2 =
            GrammarDeclaration.Element("resource_flag",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", "Flag"),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x33"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("helpid", "_0x3e8"),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "off"),
                GrammarAttr.Enumerated("access", new[] { "readonly", "readwrite", "writeonly" }, "readwrite"));

        internal static readonly GrammarDeclaration D_resource_humidity_level_a2643644 =
            GrammarDeclaration.Element("resource_humidity_level",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("inivalue", "0.00"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_humidity_level_f3556781 =
            GrammarDeclaration.Element("resource_humidity_level",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("inivalue", "20.00"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"));

        internal static readonly GrammarDeclaration D_resource_input_c65c16cc =
            GrammarDeclaration.Element("resource_input",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "off"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_input_f67a90cf =
            GrammarDeclaration.Element("resource_input",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "off"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"));

        internal static readonly GrammarDeclaration D_resource_integer =
            GrammarDeclaration.Element("resource_integer",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("inivalue", "0"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_light_3b7fdc54 =
            GrammarDeclaration.Element("resource_light",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("inivalue", "0.00"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Enumerated("setting", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_light_89b35e6b =
            GrammarDeclaration.AttlistOnly("resource_light",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("inivalue", "500.00"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"));

        internal static readonly GrammarDeclaration D_resource_light_level =
            GrammarDeclaration.Element("resource_light_level",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("inivalue", "0"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_output =
            GrammarDeclaration.Element("resource_output",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "off"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_scene =
            GrammarDeclaration.Element("resource_scene",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("icon", "_0x89"),
                GrammarAttr.Enumerated("hide_dialog", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_temperature_d46e7998 =
            GrammarDeclaration.Element("resource_temperature",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("inivalue", "20.00"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"));

        internal static readonly GrammarDeclaration D_resource_temperature_f864c671 =
            GrammarDeclaration.Element("resource_temperature",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("inivalue", "0.00"),
                GrammarAttr.Enumerated("accessibility", new[] { "read", "write", "read-write" }, "read-write"),
                GrammarAttr.Enumerated("setting", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_time =
            GrammarDeclaration.Element("resource_time",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.CdataRequired("hour"),
                GrammarAttr.CdataRequired("minute"),
                GrammarAttr.CdataRequired("second"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_timer =
            GrammarDeclaration.Element("resource_timer",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.CdataRequired("hour"),
                GrammarAttr.CdataRequired("minute"),
                GrammarAttr.CdataRequired("second"),
                GrammarAttr.CdataRequired("millisecond"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_timertime =
            GrammarDeclaration.Element("resource_timertime",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.CdataRequired("hour"),
                GrammarAttr.CdataRequired("minute"),
                GrammarAttr.CdataRequired("second"),
                GrammarAttr.CdataRequired("millisecond"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_resource_weekday =
            GrammarDeclaration.Element("resource_weekday",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("backup", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Enumerated("inivalue", new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" }, "monday"),
                GrammarAttr.Cdata("note-2", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_rs485_led_dimmer_channel =
            GrammarDeclaration.Element("rs485_led_dimmer_channel",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("channel", ""),
                GrammarAttr.Cdata("channel_id", ""),
                GrammarAttr.Cdata("icon", ""));

        internal static readonly GrammarDeclaration D_s0_device =
            GrammarDeclaration.Element("s0_device",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes"),
                GrammarAttr.Enumerated("enduser_report", new[] { "yes", "no" }, "no"),
                GrammarAttr.Cdata("icon", ""),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("position", ""),
                GrammarAttr.Cdata("documentation_tag", ""),
                GrammarAttr.Cdata("cable_colour_plus", ""),
                GrammarAttr.Cdata("cable_colour_minus", ""));

        internal static readonly GrammarDeclaration D_scene_link =
            GrammarDeclaration.Element("scene_link",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.IdRefRequired("link"),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_scenes_5159b889 =
            GrammarDeclaration.Element("scenes",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.IdRefRequired("scene_resource"),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_scenes_f7066318 =
            GrammarDeclaration.Element("scenes",
                GrammarAttr.Id("id"),
                GrammarAttr.IdRefRequired("scene_resource"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_settings_4f454503 =
            GrammarDeclaration.Element("settings",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_settings_ea2dbe61 =
            GrammarDeclaration.Element("settings",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x0"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("udf", ""));

        internal static readonly GrammarDeclaration D_shutter_setting_travel_time_down =
            GrammarDeclaration.Element("shutter_setting_travel_time_down",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("value", "120"),
                GrammarAttr.Cdata("minimum", "0"),
                GrammarAttr.Cdata("maximum", "240"));

        internal static readonly GrammarDeclaration D_shutter_setting_travel_time_up =
            GrammarDeclaration.Element("shutter_setting_travel_time_up",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("value", "120"),
                GrammarAttr.Cdata("minimum", "0"),
                GrammarAttr.Cdata("maximum", "240"));

        internal static readonly GrammarDeclaration D_shutter_settings =
            GrammarDeclaration.Element("shutter_settings",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("note", ""));

        internal static readonly GrammarDeclaration D_sms_modem_phonenumber =
            GrammarDeclaration.Element("sms_modem_phonenumber",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("address"),
                GrammarAttr.Cdata("phonenumber", ""));

        internal static readonly GrammarDeclaration D_sms_modem_pincode =
            GrammarDeclaration.Element("sms_modem_pincode",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("value", ""),
                GrammarAttr.Cdata("minimum", ""),
                GrammarAttr.Cdata("maximum", ""));

        internal static readonly GrammarDeclaration D_sms_modem_settings =
            GrammarDeclaration.Element("sms_modem_settings",
                GrammarAttr.Id("id"),
                GrammarAttr.Cdata("name", ""),
                GrammarAttr.Cdata("icon", "_0x15"),
                GrammarAttr.Cdata("note", ""),
                GrammarAttr.Cdata("helpid", "_0x0"));

        /// <summary>Consumed by: 1.1.02.ifb, 1.1.02.ifb.</summary>
        internal static readonly CatalogGrammar G_00a7fc0d = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_resource_timertime,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 5.3.03.ifb.</summary>
        internal static readonly CatalogGrammar G_073d25bb = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_resource_temperature_f864c671,
                D_outputs,
                D_resource_output,
                D_resource_time,
                D_resource_integer,
                D_resource_timer,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_event,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 4.2.06.ifb.</summary>
        internal static readonly CatalogGrammar G_09b53012 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_resource_time,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_program_case,
                D_case_action,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product4201.def, product4204.def.</summary>
        internal static readonly CatalogGrammar G_0abfeba5 = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_336ef2a7,
                D_airlink_relay_b6b7872d,
                D_scenes_5159b889,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: product4406.def.</summary>
        internal static readonly CatalogGrammar G_0eb73e78 = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_336ef2a7,
                D_airlink_input,
                D_airlink_relay_b6b7872d,
                D_airlink_dimmer_increase,
                D_airlink_dimmer_decrease,
                D_airlink_dimmer_touch,
                D_airlink_dimming,
                D_light_indication,
                D_scenes_5159b889,
                D_dimmer_settings,
                D_dimmer_setting_minimum_value,
                D_dimmer_setting_maximum_value,
                D_dimmer_setting_fade_rate_up,
                D_dimmer_setting_fade_rate_down,
                D_dimmer_setting_dimming_rate,
                D_dimmer_setting_load_mode_e97ec1b0,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: 5.3.02.ifb.</summary>
        internal static readonly CatalogGrammar G_11320ded = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_temperature_f864c671,
                D_outputs,
                D_resource_time,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 8.1.01.ifb.</summary>
        internal static readonly CatalogGrammar G_11d9083a = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_outputs,
                D_resource_date_30b57fe6,
                D_resource_weekday,
                D_resource_time,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 5.2.04.ifb.</summary>
        internal static readonly CatalogGrammar G_1360e330 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_time,
                D_resource_weekday,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product4409.def.</summary>
        internal static readonly CatalogGrammar G_1567de87 = CatalogGrammar.Create(
            new[]
            {
                D_product_rs485_led_dimmer,
                D_rs485_led_dimmer_channel,
                D_dataline_output_769ca3c8,
                D_dimmer_settings,
                D_dimmer_setting_minimum_value,
                D_dimmer_setting_maximum_value,
                D_dimmer_setting_fade_rate_up,
                D_dimmer_setting_fade_rate_down,
                D_dimmer_setting_dimming_rate,
                D_dimmer_setting_load_mode_6ab066b0,
                D_resource_enum_5d30a24d,
                D_airlink_dimming,
                D_light_indication,
                D_scenes_5159b889,
                D_airlink_dimmer_increase,
                D_airlink_dimmer_decrease,
                D_resource_flag_d20940a2,
            },
            "ISO-8859-1", doctypeRoot: "product_rs485_led_dimmer");

        /// <summary>Consumed by: 3.1.01.ifb, 3.1.02.ifb.</summary>
        internal static readonly CatalogGrammar G_16374d34 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_resource_time,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.2.05.ifb.</summary>
        internal static readonly CatalogGrammar G_16f87c2a = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_resource_light_level,
                D_outputs,
                D_resource_scene,
                D_resource_enum_8f33b7f1,
                D_resource_timer,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timertime,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product4202.def, product4203.def, product4205.def.</summary>
        internal static readonly CatalogGrammar G_17f224bb = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_482ce24e,
                D_airlink_relay_b6b7872d,
                D_scenes_5159b889,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: 4.1.04.ifb.</summary>
        internal static readonly CatalogGrammar G_1aba3b40 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_counter,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_time,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 6.1.03.ifb.</summary>
        internal static readonly CatalogGrammar G_1ce30868 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_time,
                D_resource_weekday,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 4.2.01.ifb.</summary>
        internal static readonly CatalogGrammar G_1e37c462 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_resource_flag_c251886e,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2701.def.</summary>
        internal static readonly CatalogGrammar G_20207f0e = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_03a62d41,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 100H1092.def.</summary>
        internal static readonly CatalogGrammar G_26aa6061 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_7ef16ca1,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: product2707.def.</summary>
        internal static readonly CatalogGrammar G_295e9042 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
                D_resource_enum_8f9fb582,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 1.2.04.ifb, 1.2.04.ifb.</summary>
        internal static readonly CatalogGrammar G_2a4b118d = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_resource_light_level,
                D_outputs,
                D_resource_scene,
                D_resource_enum_8f33b7f1,
                D_resource_output,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 4.2.03.ifb.</summary>
        internal static readonly CatalogGrammar G_2bc127a2 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_date_30b57fe6,
                D_resource_integer,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_resource_time,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_event,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 6.4.02.ifb, 6.4.03.ifb.</summary>
        internal static readonly CatalogGrammar G_2cfa6cb4 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_resource_integer,
                D_resource_counter,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product210e.def, product210g.def, product2110.def, product2111.def, product2210.def, product2301.def, product2302.def, product2303.def, product2304.def, product2305.def, Velux KLF-100.def, product2114.def.</summary>
        internal static readonly CatalogGrammar G_314c48d2 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 5.4.02.ifb.</summary>
        internal static readonly CatalogGrammar G_3679b938 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_enum_8f33b7f1,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_resource_time,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_actions,
                D_action,
                D_event,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.4.09.ifb.</summary>
        internal static readonly CatalogGrammar G_384c6360 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2125.def.</summary>
        internal static readonly CatalogGrammar G_3bed940d = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_resource_temperature_d46e7998,
                D_resource_input_f67a90cf,
                D_settings_4f454503,
                D_resource_enum_8f9fb582,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 1.3.08.ifb.</summary>
        internal static readonly CatalogGrammar G_3c191c7f = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_temperature_f864c671,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 6.3.03.ifb.</summary>
        internal static readonly CatalogGrammar G_3e19c60b = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product4101.def, product4102.def, product4103.def, product4104.def, product4105.def, product4106.def.</summary>
        internal static readonly CatalogGrammar G_3e4f2dc2 = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_336ef2a7,
                D_airlink_input,
                D_scenes_5159b889,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: 4.1.09.ifb.</summary>
        internal static readonly CatalogGrammar G_3ea878e0 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_internalsettings,
                D_resource_counter,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by 18 files: product2201.def, product2202.def, product2203.def, product2204.def, product2206.def, product2207.def, product2208.def, product2209.def, product220a.def, product220b.def, product220c.def, product220d.def, …</summary>
        internal static readonly CatalogGrammar G_3ff12910 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 4.1.14.ifb.</summary>
        internal static readonly CatalogGrammar G_4398fa22 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_enum_8f33b7f1,
                D_resource_time,
                D_resource_date_30b57fe6,
                D_resource_integer,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_case,
                D_case_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2706.def.</summary>
        internal static readonly CatalogGrammar G_43b9a320 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_03a62d41,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
                D_resource_enum_8f9fb582,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 6.1.05.ifb.</summary>
        internal static readonly CatalogGrammar G_45a30d20 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product3103.def.</summary>
        internal static readonly CatalogGrammar G_49662bbb = CatalogGrammar.Create(
            new[]
            {
                D_product_rs485_sms_modem,
                D_sms_modem_settings,
                D_sms_modem_pincode,
                D_sms_modem_phonenumber,
            },
            "ISO-8859-1", doctypeRoot: "product_rs485_sms_modem");

        /// <summary>Consumed by: 8.1.02.ifb.</summary>
        internal static readonly CatalogGrammar G_4c32bdad = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_case,
                D_case_action,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.3.01.ifb.</summary>
        internal static readonly CatalogGrammar G_4e26a06d = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 6.3.01.ifb.</summary>
        internal static readonly CatalogGrammar G_4e4caf41 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_time,
                D_resource_date_30b57fe6,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 4.2.04.ifb.</summary>
        internal static readonly CatalogGrammar G_53e1e011 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_program_case,
                D_case_action,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 5.2.06.ifb.</summary>
        internal static readonly CatalogGrammar G_5420e304 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_temperature_f864c671,
                D_resource_input_c65c16cc,
                D_resource_enum_8f33b7f1,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timertime,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2135.def.</summary>
        internal static readonly CatalogGrammar G_55b07244 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_resource_temperature_d46e7998,
                D_resource_humidity_level_f3556781,
                D_resource_input_f67a90cf,
                D_settings_4f454503,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 5.2.03.ifb.</summary>
        internal static readonly CatalogGrammar G_57af508c = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timertime,
                D_resource_enum_8f33b7f1,
                D_resource_integer,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_resource_timer,
                D_resource_time,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 2.1.01.ifb.</summary>
        internal static readonly CatalogGrammar G_5d331e28 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_resource_time,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_event_power,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.1.03.ifb.</summary>
        internal static readonly CatalogGrammar G_5f83f695 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_scene,
                D_scene_link,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2315.def.</summary>
        internal static readonly CatalogGrammar G_624b316c = CatalogGrammar.Create(
            new[]
            {
                D_s0_device,
                D_kWh,
                D_W,
                D_resource_date_3ead75b7,
            },
            "ISO-8859-1", doctypeRoot: "s0_device");

        /// <summary>Consumed by: product4404.def, product4407.def, product4408.def.</summary>
        internal static readonly CatalogGrammar G_6437bed0 = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_336ef2a7,
                D_airlink_input,
                D_airlink_relay_b6b7872d,
                D_scenes_5159b889,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: 100H1005.def.</summary>
        internal static readonly CatalogGrammar G_64613d55 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_3ef2041b,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: WindowMaster WUC 102.def.</summary>
        internal static readonly CatalogGrammar G_69268e25 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_597d90d4,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 2.1.02.ifb.</summary>
        internal static readonly CatalogGrammar G_6a64fb7f = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_resource_enum_8f33b7f1,
                D_resource_weekday,
                D_settings_ea2dbe61,
                D_resource_time,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_event_power,
                D_program_sub,
                D_conditions,
                D_condition,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2138.def.</summary>
        internal static readonly CatalogGrammar G_6f63eee0 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_resource_temperature_d46e7998,
                D_resource_humidity_level_f3556781,
                D_resource_input_f67a90cf,
                D_settings_4f454503,
                D_resource_enum_8f9fb582,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 100H1006.def.</summary>
        internal static readonly CatalogGrammar G_6fd88895 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_c5c3e260,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 1.1.05.ifb.</summary>
        internal static readonly CatalogGrammar G_74ce51e6 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_enum_8f33b7f1,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.4.08.ifb.</summary>
        internal static readonly CatalogGrammar G_77a6e1d5 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_resource_counter,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_program_case,
                D_case_action,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: AutoProof.ifb.</summary>
        internal static readonly CatalogGrammar G_81f86af5 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_link_to_resource,
                D_outputs,
                D_resource_output,
                D_link_from_resource,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2205.def.</summary>
        internal static readonly CatalogGrammar G_83f5f1dd = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_a09bc482,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 2.1.04.ifb.</summary>
        internal static readonly CatalogGrammar G_8bcceaa0 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_date_30b57fe6,
                D_resource_time,
                D_settings_ea2dbe61,
                D_resource_flag_c251886e,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2139.def.</summary>
        internal static readonly CatalogGrammar G_8e0b434f = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_resource_temperature_d46e7998,
                D_resource_Light,
                D_resource_light_89b35e6b,
                D_resource_input_f67a90cf,
                D_settings_4f454503,
                D_resource_enum_8f9fb582,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 5.1.01.ifb.</summary>
        internal static readonly CatalogGrammar G_922b53e9 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_time,
                D_resource_timertime,
                D_resource_timer,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_resource_weekday,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 100H1004.def.</summary>
        internal static readonly CatalogGrammar G_9ad2f0dd = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_b9aabc24,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: product4501.def.</summary>
        internal static readonly CatalogGrammar G_a077a7dc = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_336ef2a7,
                D_airlink_input,
                D_airlink_shutter_up,
                D_airlink_shutter_down,
                D_airlink_shutter_lock,
                D_enum_definition_d7b36224,
                D_enum_value_8603ac4b,
                D_resource_enum_916fe757,
                D_scenes_5159b889,
                D_shutter_settings,
                D_shutter_setting_travel_time_up,
                D_shutter_setting_travel_time_down,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: Beo4.def, Beolink1000.def, Beolink5000.def, product210d.def, product211f.def.</summary>
        internal static readonly CatalogGrammar G_a42a487c = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_3a723af4,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 1.3.07.ifb.</summary>
        internal static readonly CatalogGrammar G_a434b14a = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_humidity_level_a2643644,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_event_power,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.1.13.ifb.</summary>
        internal static readonly CatalogGrammar G_a473af56 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_light_3b7fdc54,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_light_level,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.2.07.ifb.</summary>
        internal static readonly CatalogGrammar G_a4becc0f = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_flag_c251886e,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_event_power,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 5.1.02.ifb, 6.4.01.ifb.</summary>
        internal static readonly CatalogGrammar G_a8e630e0 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product4304.def, product4306.def.</summary>
        internal static readonly CatalogGrammar G_a9b7d32a = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_482ce24e,
                D_airlink_input,
                D_airlink_relay_d62683ed,
                D_airlink_dimmer_increase,
                D_airlink_dimmer_decrease,
                D_airlink_dimmer_touch,
                D_airlink_dimming,
                D_light_indication,
                D_scenes_5159b889,
                D_dimmer_settings,
                D_dimmer_setting_minimum_value,
                D_dimmer_setting_maximum_value,
                D_dimmer_setting_fade_rate_up,
                D_dimmer_setting_fade_rate_down,
                D_dimmer_setting_dimming_rate,
                D_dimmer_setting_load_mode_65e5fdf6,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: 3.1.03.ifb.</summary>
        internal static readonly CatalogGrammar G_ab5e6d02 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_resource_enum_8f33b7f1,
                D_outputs,
                D_resource_scene,
                D_resource_output,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_resource_integer,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_event,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 6.1.04.ifb.</summary>
        internal static readonly CatalogGrammar G_abd6829e = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_time,
                D_resource_weekday,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 5.2.05.ifb.</summary>
        internal static readonly CatalogGrammar G_adaa3e3b = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_temperature_f864c671,
                D_resource_input_c65c16cc,
                D_resource_enum_8f33b7f1,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timertime,
                D_resource_counter,
                D_resource_time,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_actions,
                D_event_power,
                D_event,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.3.05.ifb.</summary>
        internal static readonly CatalogGrammar G_b277e6f2 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_time,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product4303.def, product4304.def, product4306.def.</summary>
        internal static readonly CatalogGrammar G_b5ead9ca = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_482ce24e,
                D_airlink_input,
                D_airlink_relay_d62683ed,
                D_airlink_dimmer_increase,
                D_airlink_dimmer_decrease,
                D_airlink_dimmer_touch,
                D_airlink_dimming,
                D_light_indication,
                D_scenes_5159b889,
                D_dimmer_settings,
                D_dimmer_setting_minimum_value,
                D_dimmer_setting_maximum_value,
                D_dimmer_setting_fade_rate_up,
                D_dimmer_setting_fade_rate_down,
                D_dimmer_setting_dimming_rate,
                D_dimmer_setting_load_mode_e97ec1b0,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: 5.4.01.ifb.</summary>
        internal static readonly CatalogGrammar G_b6cfb2e3 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_resource_enum_8f33b7f1,
                D_resource_temperature_f864c671,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timertime,
                D_resource_time,
                D_resource_timer,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 6.1.01.ifb.</summary>
        internal static readonly CatalogGrammar G_b7d5054f = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_integer,
                D_resource_enum_8f33b7f1,
                D_resource_timer,
                D_internalsettings,
                D_resource_counter,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 4.1.05.ifb.</summary>
        internal static readonly CatalogGrammar G_bf70a093 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_counter,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.2.06.ifb.</summary>
        internal static readonly CatalogGrammar G_c53e093c = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_link_to_resource,
                D_outputs,
                D_resource_output,
                D_link_from_resource,
                D_resource_enum_8f33b7f1,
                D_resource_timer,
                D_settings_ea2dbe61,
                D_resource_timertime,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.1.01.ifb, 1.1.01.ifb, 1.4.03.ifb, 4.1.03.ifb.</summary>
        internal static readonly CatalogGrammar G_c8b0417b = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product4502.def.</summary>
        internal static readonly CatalogGrammar G_c9356ede = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_336ef2a7,
                D_airlink_input,
                D_airlink_shutter_up,
                D_airlink_shutter_down,
                D_enum_definition_d7b36224,
                D_enum_value_8603ac4b,
                D_resource_enum_916fe757,
                D_scenes_5159b889,
                D_shutter_settings,
                D_shutter_setting_travel_time_up,
                D_shutter_setting_travel_time_down,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: 5.4.03.ifb.</summary>
        internal static readonly CatalogGrammar G_cebf66ea = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_enum_8f33b7f1,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.1.12.ifb.</summary>
        internal static readonly CatalogGrammar G_d0886bda = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_light_3b7fdc54,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_event_power,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 5.2.01.ifb.</summary>
        internal static readonly CatalogGrammar G_d32e5c64 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_enum_8f33b7f1,
                D_settings_ea2dbe61,
                D_resource_time,
                D_resource_date_30b57fe6,
                D_resource_timer,
                D_resource_timertime,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2136.def.</summary>
        internal static readonly CatalogGrammar G_d46341ce = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_resource_temperature_d46e7998,
                D_resource_Light,
                D_resource_light_89b35e6b,
                D_resource_input_f67a90cf,
                D_settings_4f454503,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 6.3.05.ifb.</summary>
        internal static readonly CatalogGrammar G_d6e0ae13 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_time,
                D_resource_date_30b57fe6,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 6.3.02.ifb.</summary>
        internal static readonly CatalogGrammar G_d8c6c86f = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_resource_timer,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_event_power,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 4.1.01.ifb.</summary>
        internal static readonly CatalogGrammar G_dad15602 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_event_power,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.4.02.ifb, 1.4.02.ifb.</summary>
        internal static readonly CatalogGrammar G_df864126 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.1.04.ifb.</summary>
        internal static readonly CatalogGrammar G_e02d95c8 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_event_power,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.1.06.ifb.</summary>
        internal static readonly CatalogGrammar G_e39df072 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product4801.def.</summary>
        internal static readonly CatalogGrammar G_e8c2b169 = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_336ef2a7,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by: 4.1.08.ifb.</summary>
        internal static readonly CatalogGrammar G_eb20fc8a = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_case,
                D_case_action,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.3.06.ifb.</summary>
        internal static readonly CatalogGrammar G_ed3e7b1d = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_timer,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_resource_integer,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 1.2.02.ifb.</summary>
        internal static readonly CatalogGrammar G_f27546fc = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_enum_8f33b7f1,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_resource_flag_c251886e,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_program_sub,
                D_conditions,
                D_condition,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 6.2.01.ifb.</summary>
        internal static readonly CatalogGrammar G_f30e28eb = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_resource_enum_8f33b7f1,
                D_resource_time,
                D_resource_date_30b57fe6,
                D_settings_ea2dbe61,
                D_resource_timer,
                D_resource_weekday,
                D_internalsettings,
                D_resource_flag_c251886e,
                D_resource_integer,
                D_resource_counter,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
                D_event_power,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: 5.3.01.ifb.</summary>
        internal static readonly CatalogGrammar G_f36c1d57 = CatalogGrammar.Create(
            new[]
            {
                D_enum_definition_fbd50d75,
                D_enum_value_ade487cc,
                D_functionblock,
                D_inputs,
                D_resource_temperature_f864c671,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_time,
                D_resource_date_30b57fe6,
                D_settings_ea2dbe61,
                D_resource_enum_8f33b7f1,
                D_internalsettings,
                D_resource_timer,
                D_resource_flag_c251886e,
                D_resource_integer,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
                D_event_power,
                D_program_sub,
                D_conditions,
                D_condition,
                D_program_case,
                D_case_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2109.def, product210a.def, product210b.def, product210c.def, product210f.def, product2112.def, product2115.def.</summary>
        internal static readonly CatalogGrammar G_f4e897b5 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_3858bc1e,
                D_dataline_output_769ca3c8,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 4.1.02.ifb.</summary>
        internal static readonly CatalogGrammar G_f71da7d0 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_resource_output,
                D_resource_scene,
                D_settings_ea2dbe61,
                D_internalsettings,
                D_resource_timer,
                D_programs,
                D_program_simple,
                D_events,
                D_event_power,
                D_event,
                D_actions,
                D_program_sub,
                D_conditions,
                D_condition,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: product2124.def.</summary>
        internal static readonly CatalogGrammar G_f96c6303 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_d6becd96,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_resource_temperature_d46e7998,
                D_resource_input_f67a90cf,
                D_settings_4f454503,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: product4406.def.</summary>
        internal static readonly CatalogGrammar G_f9fc5c54 = CatalogGrammar.Create(
            new[]
            {
                D_product_airlink_336ef2a7,
                D_airlink_input,
                D_airlink_relay_b6b7872d,
                D_airlink_dimmer_increase,
                D_airlink_dimmer_decrease,
                D_airlink_dimmer_touch,
                D_airlink_dimming,
                D_light_indication,
                D_scenes_5159b889,
                D_dimmer_settings,
                D_dimmer_setting_minimum_value,
                D_dimmer_setting_maximum_value,
                D_dimmer_setting_fade_rate_up,
                D_dimmer_setting_fade_rate_down,
                D_dimmer_setting_dimming_rate,
                D_dimmer_setting_load_mode_65e5fdf6,
            },
            "ISO-8859-1", doctypeRoot: "product_airlink");

        /// <summary>Consumed by 15 files: product2101.def, product2102.def, product2103.def, product2104.def, product2105.def, product2107.def, product2108.def, product2130.def, product2132.def, product2102.def, product2106.def, product2108.def, …</summary>
        internal static readonly CatalogGrammar G_fd06aef9 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_03a62d41,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

        /// <summary>Consumed by: 8.1.03.ifb.</summary>
        internal static readonly CatalogGrammar G_fee51070 = CatalogGrammar.Create(
            new[]
            {
                D_functionblock,
                D_inputs,
                D_resource_input_c65c16cc,
                D_outputs,
                D_settings_ea2dbe61,
                D_resource_time,
                D_internalsettings,
                D_programs,
                D_program_simple,
                D_events,
                D_event,
                D_actions,
                D_action,
            },
            "ISO-8859-1", doctypeRoot: "functionblock");

        /// <summary>Consumed by: WindowMaster WUC 101.def.</summary>
        internal static readonly CatalogGrammar G_ff40db50 = CatalogGrammar.Create(
            new[]
            {
                D_product_dataline_26803675,
                D_dataline_input_8e8a3ac9,
                D_dataline_output_769ca3c8,
                D_scenes_f7066318,
            },
            "ISO-8859-1", doctypeRoot: "product_dataline");

    }
}
