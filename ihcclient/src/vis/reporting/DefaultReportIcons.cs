using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// The default icon glyphs (spec R11/D5): the 1–3 character unicode Text stand-ins of
    /// <c>icon_codes.md</c> §7.1, keyed by the same semantic icon key the SVG assets use. This is the
    /// fallback for every format — a caller provider that returns null/empty for a key, and any unknown
    /// key, resolve here so generation never fails over icons. Stand-ins are plain TEXT: writers emit them
    /// through their normal escaping path (only provider fragments are trusted raw markup).
    /// </summary>
    internal static class DefaultReportIcons
    {
        /// <summary>The neutral stand-in for a key §7.1 does not map (unknown/foreign keys).</summary>
        private const string UnknownStandIn = "·";

        private static readonly FrozenDictionary<string, string> StandIns = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // §1 structure & sections
            ["locality"] = "⧉",
            ["fb-lk"] = "▢",
            ["fb-editable"] = "⊔",
            ["section-input"] = "⇥",
            ["section-output"] = "⇤",
            ["section-settings"] = "✎",
            ["section-internal-vars"] = "⚙",
            // §2 programs & logic
            ["prog-program"] = "◆",
            ["prog-subprogram"] = "↳◆",
            ["event-group"] = "!!",
            ["event"] = "!",
            ["command-group"] = "✓✓",
            ["command"] = "✓",
            ["condition"] = "?",
            ["cond-and"] = "∧",
            ["cond-or"] = "∨",
            // §3a resources with a code
            ["pin-in"] = "→",
            ["pin-out"] = "←",
            ["var-flag"] = "⚑",
            ["var-enum"] = "#",
            ["var-timer"] = "⧖",
            ["var-date"] = "▤",
            ["var-time"] = "◷",
            ["var-weekday"] = "≣7",
            ["var-timer-duration"] = "⏱",
            ["var-holiday"] = "☂",
            // §3b resources with no code
            ["var-integer"] = "ℕ",
            ["var-decimal"] = "0.0",
            ["var-counter"] = "123",
            ["var-temperature"] = "℃",
            ["var-humidity"] = "RH",
            ["var-illuminance"] = "☼",
            ["var-light-level"] = "☼%",
            ["scenario"] = "⧈",
            ["var-energy"] = "↯",
            // §4 links
            ["link-from"] = "⇠",
            ["link-to"] = "⇢",
            // §5 products
            ["product-button"] = "☟",
            ["product-lamp"] = "▽",
            ["product-socket"] = "⊓",
            ["product-sensor"] = "◉",
            ["product-s0"] = "S0",
            ["rs485-module"] = "▥",
        }.ToFrozenDictionary(StringComparer.Ordinal);

        /// <summary>The default stand-in for a key (the §7.1 Text glyph, or the neutral dot for unknown keys).</summary>
        public static string StandInFor(string iconKey) =>
            StandIns.TryGetValue(iconKey, out string? standIn) ? standIn : UnknownStandIn;

        /// <summary>
        /// Resolves the per-instance glyph for a key (spec R11 fallback rule): the caller provider's
        /// fragment when it returns a non-empty one for this format, else the default stand-in. The
        /// returned value is RAW when it came from the provider and PLAIN TEXT when it is a stand-in —
        /// <paramref name="isRawFragment"/> tells the writer which escaping path applies.
        /// </summary>
        public static string Resolve(IReportIconProvider? provider, string mimeType, string iconKey, out bool isRawFragment)
        {
            string? fragment = provider?.GetFragment(mimeType, iconKey);
            isRawFragment = !string.IsNullOrEmpty(fragment);
            return isRawFragment ? fragment! : StandInFor(iconKey);
        }
    }
}
