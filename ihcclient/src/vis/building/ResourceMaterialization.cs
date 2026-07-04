#nullable enable
using System;
using System.Collections.Generic;

namespace Ihc.Projects
{
    /// <summary>
    /// The per-resource-type presentation attributes IHC Visual stamps on a resource the moment it is created: the
    /// canonical GUI <c>icon</c> for its type, plus — for the value types whose value attributes are <c>#REQUIRED</c>
    /// (a date's <c>year/month/day</c>, a timer's <c>hour/minute/second/millisecond</c>) — the vendor's initial
    /// values. Shared by the two creation paths so both reproduce a freshly-authored resource byte-for-byte:
    /// <list type="bullet">
    /// <item><see cref="InsertTransform"/> (catalog insert) stamps only the <see cref="Icon"/> — a catalog element's
    /// value attributes already arrive from the component <c>.def</c>'s DTD defaults.</item>
    /// <item>the hand-authoring path (<see cref="ProjectEditor.UpsertResourceChild"/>) has no catalog template, so it
    /// stamps both the icon and the <c>#REQUIRED</c> value initials via <see cref="NewResourceDefaults"/>.</item>
    /// </list>
    /// Every entry is mined conflict-free from the authentic oracles (each listed type maps to exactly one icon). A
    /// type absent from a table carries no canonical value there — its <c>icon</c> stays the DTD default <c>_0x0</c>,
    /// elided on save; only <c>#REQUIRED</c> attributes (never a DTD default) must be listed here.
    /// </summary>
    internal static class ResourceMaterialization
    {
        private static readonly Dictionary<string, string> Icons = new(StringComparer.Ordinal)
        {
            ["resource_enum"] = "_0x22",
            ["resource_input"] = "_0x36",
            ["resource_output"] = "_0x39",
            ["resource_timer"] = "_0x43",
            ["resource_flag"] = "_0x33",
            ["resource_time"] = "_0x2f",
            ["resource_date"] = "_0x29",
            ["resource_weekday"] = "_0x2c",
            ["resource_timertime"] = "_0x4d",
            ["resource_holiday"] = "_0x9b",
            // RS-485 LED dimmer channel error-state indicators — read-only status resources IHC Visual stamps with
            // the resource-input icon on insert (the product .def omits it), just like resource_input.
            ["rs485_led_dimmer_error_state_overcurrent"] = "_0x36",
            ["rs485_led_dimmer_error_state_overvoltage"] = "_0x36",
            ["rs485_led_dimmer_error_state_overheating"] = "_0x36",
            ["rs485_led_dimmer_error_state_loadfailure"] = "_0x36",
        };

        // The #REQUIRED value attributes (never omittable, so always physically present) with the vendor's initial
        // values for a freshly-authored resource. Authoring path only; a catalog insert gets these from the .def DTD.
        private static readonly Dictionary<string, (string Name, string Value)[]> RequiredValues = new(StringComparer.Ordinal)
        {
            ["resource_date"] = new[] { ("year", "2000"), ("month", "1"), ("day", "1") },
            ["resource_time"] = new[] { ("hour", "0"), ("minute", "0"), ("second", "0") },
            ["resource_timer"] = new[] { ("hour", "0"), ("minute", "0"), ("second", "0"), ("millisecond", "0") },
            ["resource_timertime"] = new[] { ("hour", "0"), ("minute", "0"), ("second", "0"), ("millisecond", "0") },
        };

        /// <summary>The canonical GUI icon for a resource type, or null when the type has none (effective <c>_0x0</c>).</summary>
        public static string? Icon(string tag) => Icons.TryGetValue(tag, out string? icon) ? icon : null;

        /// <summary>
        /// The presentation attributes a hand-authored resource of this type must carry to match the vendor: the
        /// canonical icon (when any) followed by the type's <c>#REQUIRED</c> value initials. Empty for a type that
        /// needs neither. Caller-supplied attributes (e.g. a user override, or an enum's typedef/inivalue) are applied
        /// <em>after</em> these and win on any name collision.
        /// </summary>
        public static IReadOnlyList<(string Name, string Value)> NewResourceDefaults(string tag)
        {
            string? icon = Icon(tag);
            (string Name, string Value)[] values = RequiredValues.TryGetValue(tag, out (string Name, string Value)[]? v)
                ? v
                : Array.Empty<(string Name, string Value)>();
            if (icon is null)
            {
                return values;
            }
            var defaults = new List<(string Name, string Value)>(values.Length + 1) { ("icon", icon) };
            defaults.AddRange(values);
            return defaults;
        }
    }
}
