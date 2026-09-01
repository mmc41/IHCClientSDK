using System;
using System.Collections.Frozen;
using System.Collections.Generic;

using Ihc.Vis.Model;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// The SDK-side element → semantic icon-key resolution for the report pipeline (R11: the "Our key" set
    /// of <c>icon_codes.md</c> stays SDK logic; only key → glyph is caller territory). Covers what the FB
    /// report renders; the conditions group fans out by its <c>type</c> (A12: <c>or</c> → the OR group,
    /// anything else including unknown values → AND, per U6).
    /// </summary>
    internal static class ReportIconKeys
    {
        private static readonly FrozenDictionary<string, string> ByTag = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["inputs"] = "section-input",
            ["outputs"] = "section-output",
            ["settings"] = "section-settings",
            ["internalsettings"] = "section-internal-vars",
            ["programs"] = "prog-subprogram",
            ["program_simple"] = "prog-program",
            ["program_sub"] = "prog-subprogram",
            ["program_case"] = "prog-subprogram",
            ["events"] = "event-group",
            ["event"] = "event",
            ["event_power"] = "event",
            ["actions"] = "command-group",
            ["case_action"] = "command-group",
            ["action"] = "command",
            ["condition"] = "condition",
            ["resource_input"] = "pin-in",
            ["resource_output"] = "pin-out",
            ["resource_scene"] = "scenario",
            ["resource_flag"] = "var-flag",
            ["resource_enum"] = "var-enum",
            ["resource_timer"] = "var-timer",
            ["resource_date"] = "var-date",
            ["resource_time"] = "var-time",
            ["resource_weekday"] = "var-weekday",
            ["resource_timertime"] = "var-timer-duration",
            ["resource_counter"] = "var-counter",
            ["resource_integer"] = "var-integer",
            ["resource_floating_point"] = "var-decimal",
            ["resource_temperature"] = "var-temperature",
            ["resource_light_level"] = "var-light-level",
            // The register-C1 types (Full mode only). Their keys are the ones icon_codes.md §3a/§3b already
            // assigns, and every one already has a default stand-in and an app SVG — the four energy types
            // deliberately SHARE var-energy, because the value's unit column is what distinguishes them.
            ["resource_holiday"] = "var-holiday",
            ["resource_humidity_level"] = "var-humidity",
            ["resource_light"] = "var-illuminance",
            ["kW"] = "var-energy",
            ["kWh"] = "var-energy",
            ["W"] = "var-energy",
            ["Wh"] = "var-energy",
        }.ToFrozenDictionary(StringComparer.Ordinal);

        /// <summary>The icon key for a report-rendered element, or null when the type has none.</summary>
        public static string? ForElement(ProjectElement element) =>
            element.Tag == "conditions"
                ? (element.GetAttribute("type") == "or" ? "cond-or" : "cond-and")
                : ByTag.TryGetValue(element.Tag, out string? key) ? key : null;
    }
}
