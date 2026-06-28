#nullable enable
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Ihc.Projects
{
    /// <summary>
    /// The element type-code byte: the low byte of every element <c>id</c>, where
    /// <c>id = (counter &lt;&lt; 8) | typeCode</c> (spec ch. 02 §2.2/§2.3). Constant per element type and used by
    /// id allocation (Stage-2 authoring). The five id-less elements (<c>utcs_project</c>, <c>modified</c>,
    /// <c>customer_info</c>, <c>installer_info</c>, <c>project_info</c>) have no code.
    /// </summary>
    /// <remarks>
    /// This table carries the codes for the element types currently in the schema registry (all 80 id-bearing
    /// types — the five id-less elements have none). It grows alongside the registry's canonical DTD blocks; the
    /// authoritative full map is spec ch. 02 §2.3. Two codes are deliberately shared between an internal subclass
    /// and its base (the tag, not the code, is authoritative): <c>case_action</c>/<c>actions</c> = 0x66 and
    /// <c>event_power</c>/<c>event</c> = 0xc8. The byte-fidelity round-trip path never consults these (loaded ids
    /// are preserved verbatim) — they exist for id allocation on the authoring/insert path.
    /// </remarks>
    internal static class TypeCode
    {
        /// <summary>Maps an element tag to its type-code byte, or absent for the five id-less elements.</summary>
        public static readonly FrozenDictionary<string, int> ByTag = new Dictionary<string, int>
        {
            // Resources
            ["resource_enum"] = 0x0f,
            ["resource_timer"] = 0x10,
            ["resource_input"] = 0x11,
            ["resource_output"] = 0x12,
            // Programming containers / leaves
            ["program_simple"] = 0x1e,
            ["program_sub"] = 0x1f,
            ["inputs"] = 0x23,
            ["outputs"] = 0x24,
            ["settings"] = 0x25,
            ["programs"] = 0x26,
            ["functionblock"] = 0x28,
            ["internalsettings"] = 0x29,
            ["link_to_resource"] = 0x2c,
            ["link_from_resource"] = 0x2d,
            // Structure
            ["groups"] = 0x31,
            ["group"] = 0x32,
            ["documentation_modules"] = 0x34,
            ["dataline_input_modules"] = 0x35,
            ["dataline_output_modules"] = 0x36,
            ["enum_definitions"] = 0x46,
            ["enum_definition"] = 0x47,
            ["enum_value"] = 0x48,
            ["scenes"] = 0x49,
            ["resource_scene"] = 0x4a,
            // Products / IO
            ["product_dataline"] = 0x53,
            ["dataline_input"] = 0x5a,
            ["dataline_output"] = 0x5b,
            // Programming (high codes)
            ["events"] = 0x64,
            ["conditions"] = 0x65,
            ["actions"] = 0x66,
            ["event"] = 0xc8,
            ["condition"] = 0xc9,
            ["action"] = 0xca,

            // ---- Out-of-contract types (spec ch. 02 §2.3 g10 + E1 + X1 tables). Registered for structural
            //      insert/round-trip; not yet byte-verified against a vendor .vis that uses them. Two codes are
            //      shared with an already-listed tag (the tag name is authoritative, §2.3): case_action↔actions
            //      0x66, event_power↔event 0xc8. ----
            // Extra resource kinds
            ["resource_weekday"] = 0x09,
            ["resource_flag"] = 0x0a,
            ["resource_integer"] = 0x0b,
            ["resource_counter"] = 0x0c,
            ["resource_time"] = 0x0d,
            ["resource_date"] = 0x0e,
            ["resource_light_level"] = 0x13,
            ["resource_temperature"] = 0x14,
            ["resource_light"] = 0x16,
            ["resource_timertime"] = 0x17,
            ["resource_humidity_level"] = 0x27,
            ["kWh"] = 0xcf,
            ["W"] = 0xd0,
            // program_case + its case branches; the power event; the indicator
            ["program_case"] = 0x21,
            ["light_indication"] = 0x1d,
            ["case_action"] = 0x66,
            ["event_power"] = 0xc8,
            // Airlink (wireless) products + IO/commands
            ["product_airlink"] = 0x54,
            ["airlink_shutter_lock"] = 0x59,
            ["airlink_input"] = 0x5c,
            ["airlink_dimming"] = 0x5d,
            ["airlink_relay"] = 0x5e,
            ["airlink_dimmer_increase"] = 0x5f,
            ["airlink_dimmer_decrease"] = 0x60,
            ["airlink_shutter_up"] = 0x62,
            ["airlink_shutter_down"] = 0x63,
            // Dimmer + shutter settings
            ["dimmer_settings"] = 0x6e,
            ["dimmer_setting_fade_rate_up"] = 0x6f,
            ["dimmer_setting_fade_rate_down"] = 0x70,
            ["dimmer_setting_minimum_value"] = 0x71,
            ["dimmer_setting_maximum_value"] = 0x72,
            ["dimmer_setting_dimming_rate"] = 0x73,
            ["dimmer_setting_load_mode"] = 0x74,
            ["shutter_settings"] = 0x78,
            ["shutter_setting_travel_time_up"] = 0x79,
            ["shutter_setting_travel_time_down"] = 0x7a,
            // RS485 LED dimmer, S0 meter device, SMS modem
            ["product_rs485_sms_modem"] = 0x56,
            ["s0_device"] = 0x57,
            ["product_rs485_led_dimmer"] = 0x58,
            ["rs485_led_dimmer_channel"] = 0x7b,
            ["rs485_led_dimmer_error_state_overcurrent"] = 0x7c,
            ["rs485_led_dimmer_error_state_overvoltage"] = 0x7d,
            ["rs485_led_dimmer_error_state_overheating"] = 0x7e,
            ["rs485_led_dimmer_error_state_loadfailure"] = 0x7f,
            ["sms_modem_settings"] = 0xcb,
            ["sms_modem_pincode"] = 0xcc,
            ["sms_modem_phonenumber"] = 0xcd,
        }.ToFrozenDictionary();

        /// <summary>The type-code byte for the tag, or <c>null</c> for id-less elements / unknown tags.</summary>
        public static int? ForTag(string tag) => ByTag.TryGetValue(tag, out int code) ? code : null;
    }
}
