#nullable enable
using System;
using System.Collections.Generic;

namespace Ihc.Vis.Schema
{
    /// <summary>
    /// The per-resource-type presentation attributes IHC Visual stamps on a resource the moment it is created: the
    /// canonical GUI <c>icon</c> for its type, plus — for the value types whose value attributes are <c>#REQUIRED</c>
    /// (a date's <c>year/month/day</c>, a timer's <c>hour/minute/second/millisecond</c>) — the vendor's initial
    /// values. A vendor grammar fact (peer of <see cref="TypeCode"/>), held once here so every creation path
    /// reproduces a freshly-authored resource byte-for-byte:
    /// <list type="bullet">
    /// <item><c>Ihc.Vis.Editing.InsertTransform</c> (catalog insert) stamps only the <see cref="Icon"/> — a catalog
    /// element's value attributes already arrive from the component <c>.def</c>'s DTD defaults.</item>
    /// <item>the hand-authoring paths (<c>Ihc.Vis.Editing.ProjectEditor.UpsertResourceChild</c> and the
    /// code-authored function-block builders) have no catalog template, so they stamp both the icon and the
    /// <c>#REQUIRED</c> value initials via <see cref="NewResourceDefaults"/>.</item>
    /// </list>
    /// Every entry is mined conflict-free from the authentic oracles (each listed type maps to exactly one icon). A
    /// type absent from a table carries no canonical value there — its <c>icon</c> stays the DTD default <c>_0x0</c>,
    /// elided on save; only <c>#REQUIRED</c> attributes (never a DTD default) must be listed here.
    /// </summary>
    internal static class ResourceMaterialization
    {
        private static readonly Dictionary<string, string> Icons = new(StringComparer.Ordinal)
        {
            // Creation icons for the structural/link elements the authoring paths stamp (ProjectEditor.Link/SeedGroup
            // and the File→New template): non-default overrides (their DTD default is _0x0), so they belong here, not
            // in KnownDefaultIconTags. Catalog bodies never contain these tags, so the insert-stamp path is unaffected.
            ["group"] = "_0x15",
            ["link_from_resource"] = "_0x47",
            ["link_to_resource"] = "_0x4a",
            // The FB-side scene-membership half carries the same "Link fra…" icon as link_from_resource on every
            // vendor instance (spec §8.5; -scenelinks oracle) — the product-side members declare no icon at all.
            ["scene_link"] = "_0x47",
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

        /// <summary>
        /// Icon-bearing element types (their schema declares an <c>icon</c> attribute) confirmed to use their
        /// DTD-default icon as-is, so they need no <see cref="Icons"/> override: the structural/container nodes whose
        /// default is <c>_0x0</c>, plus <c>resource_scene</c> whose DTD default <c>_0x89</c> is itself the vendor icon.
        /// Together with <see cref="Icons"/> this partitions every icon-bearing registry type; the
        /// <c>ResourceIconCoverageTests</c> guard fails when a newly declared icon-bearing type is in neither set,
        /// forcing an explicit decision instead of a silent <c>_0x0</c> fall-through (review suggestion #2).
        /// </summary>
        internal static readonly IReadOnlySet<string> KnownDefaultIconTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "utcs_project", "groups", "product_dataline",
            "functionblock", "inputs", "outputs", "resource_scene", "settings", "internalsettings", "programs",
            "program_simple", "events", "event", "actions", "program_sub", "conditions", "condition", "action",
            "documentation_modules", "dataline_input_modules", "dataline_output_modules", "case_action",
            "dimmer_settings", "event_power", "product_airlink", "product_rs485_led_dimmer", "product_rs485_sms_modem",
            "program_case", "rs485_led_dimmer_channel", "s0_device", "shutter_settings", "sms_modem_settings",
        };

        /// <summary>The element tags carrying a non-default GUI icon override (the keys of the <see cref="Icons"/> table).</summary>
        internal static IReadOnlyCollection<string> IconOverrideTags => Icons.Keys;

        /// <summary>The canonical GUI icon for a resource type, or null when the type has none (effective <c>_0x0</c>).</summary>
        public static string? Icon(string tag) => Icons.TryGetValue(tag, out string? icon) ? icon : null;

        /// <summary>
        /// <see cref="Icon"/> for a type the authoring paths stamp unconditionally: throws when the type has no
        /// registered override, so a removed or renamed <see cref="Icons"/> entry fails here at the source instead
        /// of emitting a null attribute value far from the cause.
        /// </summary>
        internal static string RequireIcon(string tag) =>
            Icon(tag) ?? throw new InvalidOperationException(
                $"No creation icon is registered for <{tag}> in {nameof(ResourceMaterialization)}.");

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
