#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The DIMMER and SHUTTER device-setting rows: whether a dimmer can dim, whether it drives its load the
    /// right way, and whether a shutter can position itself.
    ///
    /// <para><b>STORED versus EFFECTIVE is the whole difficulty of this set, and it is decided per row.</b> The
    /// catalog ships these setting elements carrying an id and NOTHING else — no <c>value</c> at all — while a
    /// project's own inline DTD defaults that value to <c>0</c>. So an effective read calls every freshly placed
    /// dimmer's fade rate, minimum and maximum zero, and three of these five rules would fire on every placed
    /// product. But the vendor's own dialog shows 700 ms, 100 % and 120 s for those same absent values (its factory
    /// defaults, measured and reproduced in this app's advanced-dimmer read), so the device behaves as the factory
    /// default and NOT as zero.</para>
    ///
    /// <para>Therefore: the four numeric rows read the STORED value and skip an absent one — an unset setting is
    /// uncommissioned, which is <c>dev-setting-default</c>'s row, not a zero. The load-mode row is the exception
    /// and reads the EFFECTIVE value, because there the default IS what the device does and what the dialog shows:
    /// a dimmer with no stored mode runs on automatic detection.</para>
    /// </summary>
    public static class DeviceSettingRules
    {
        private const string FadeUpTag = "dimmer_setting_fade_rate_up";
        private const string FadeDownTag = "dimmer_setting_fade_rate_down";
        private const string MinimumTag = "dimmer_setting_minimum_value";
        private const string MaximumTag = "dimmer_setting_maximum_value";
        private const string LoadModeTag = "dimmer_setting_load_mode";
        private const string TravelUpTag = "shutter_setting_travel_time_up";
        private const string TravelDownTag = "shutter_setting_travel_time_down";

        /// <summary>The automatic load-detection mode — the value this set is about.</summary>
        private const string AutomaticLoadMode = "auto";

        /// <summary>
        /// The product family whose load type is KNOWN to be LED, which is the family this row's consequence names
        /// ("automatic detection can mis-drive LED loads"). Every other dimmer family ships on automatic as the
        /// vendor's own choice, and reporting those would contradict the catalogue's own "why it may be fine".
        /// </summary>
        private const string LedDimmerProductTag = "product_rs485_led_dimmer";

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "dev-dimmer-fade-zero", FadeZero),
                Rule(catalog, "dev-dimmer-range-inverted", RangeInverted),
                Rule(catalog, "dev-dimmer-max-zero", MaximumZero),
                Rule(catalog, "dev-dimmer-load-mode-auto", LoadModeAutomatic),
                Rule(catalog, "dev-shutter-traveltime-zero", TravelTimeZero));
        }

        /// <summary>
        /// A dimmer whose stored fade rates are BOTH zero: it switches hard instead of fading.
        /// <para>SUBJECT: every product holding a dimmer-settings group. CONDITION: both fade rates stored as zero.
        /// One of the two at zero is not this row — the row says both, and a single hard direction is a
        /// deliberate asymmetry a dimmer can be set to. EXCLUSION: an absent value, which is the factory default
        /// the dialog shows and not a stored zero.</para>
        /// </summary>
        private static void FadeZero(IProjectInspection inspection)
        {
            foreach (ProjectElement product in Dimmers(inspection))
            {
                ProjectElement? fadeUp = Setting(product, FadeUpTag);
                if (Stored(fadeUp) == 0 && Stored(product, FadeDownTag) == 0)
                {
                    // BOTH rates are the finding, so neither is "the" one. The route starts at the up rate,
                    // which is the first of the pair in the dialog: a reader who lands there has the other in
                    // view beside it, and there is no reading under which the down rate is the better start.
                    inspection.Report(product, Arguments(("product", Name(product))), Fix(fadeUp));
                }
            }
        }

        /// <summary>
        /// A dimmer whose minimum level is at or above its maximum: the dimming range is empty or inverted.
        /// <para>SUBJECT: dimmers storing BOTH bounds. BOUNDARY: equal counts — a range from 40 to 40 has no room
        /// to dim in. EXCLUSION: either bound absent, for the reason the class comment states.</para>
        /// </summary>
        private static void RangeInverted(IProjectInspection inspection)
        {
            foreach (ProjectElement product in Dimmers(inspection))
            {
                ProjectElement? min = Setting(product, MinimumTag);
                if (Stored(min) is { } minimum && Stored(product, MaximumTag) is { } maximum
                    && minimum >= maximum)
                {
                    // The MINIMUM: an inverted range is repaired by moving one bound, and the minimum is the
                    // one that was raised past the other. The maximum sits beside it either way.
                    inspection.Report(
                        product,
                        Arguments(("product", Name(product)), ("minimum", minimum), ("maximum", maximum)),
                        Fix(min));
                }
            }
        }

        /// <summary>
        /// A dimmer whose stored maximum level is zero: the load can never be lit.
        /// <para>SUBJECT: dimmers storing a maximum. This is a SEPARATE row from the inverted range and fires
        /// beside it when the minimum is zero too, which is correct: "the range is empty" and "the load can never
        /// be lit" are two facts a reader acts on differently.</para>
        /// </summary>
        private static void MaximumZero(IProjectInspection inspection)
        {
            foreach (ProjectElement product in Dimmers(inspection))
            {
                ProjectElement? maximum = Setting(product, MaximumTag);
                if (Stored(maximum) == 0)
                {
                    inspection.Report(product, Arguments(("product", Name(product))), Fix(maximum));
                }
            }
        }

        /// <summary>
        /// An LED dimmer left on automatic load detection: automatic can mis-drive an LED load.
        /// <para>SUBJECT: <c>product_rs485_led_dimmer</c> products only — the family whose load type is known, and
        /// the one this row's consequence names. EFFECTIVE, not stored, and effective means read through the
        /// PROJECT'S OWN schema view: an absent mode takes whatever default the file declares for the tag, which is
        /// the value the dimmer dialog displays beside it. Every authentic vendor file declares <c>"auto"</c>, so
        /// absence is the condition there — but the format is open-world and a hard-coded <c>?? "auto"</c> would
        /// report a capacitive dimmer as automatic on a file that says otherwise. EXCLUSION: every other dimmer
        /// family, where automatic is the vendor's own default and the catalogue's "why it may be fine"
        /// applies.</para>
        /// </summary>
        private static void LoadModeAutomatic(IProjectInspection inspection)
        {
            foreach (ProjectElement product in Dimmers(inspection)
                .Where(p => p.Tag == LedDimmerProductTag))
            {
                if (Setting(product, LoadModeTag) is { } mode
                    && inspection.Project.View(mode).Effective("value") == AutomaticLoadMode)
                {
                    inspection.Report(product, Arguments(("product", Name(product))), Fix(mode));
                }
            }
        }

        /// <summary>
        /// A shutter with a stored travel time of zero in either direction: position control cannot work.
        /// <para>SUBJECT: every product holding shutter settings. EITHER direction, unlike the dimmer's fade pair:
        /// a shutter that cannot time one direction cannot position itself at all. EXCLUSION: an absent time,
        /// which is the 120 s factory default the dialog shows — "times measured and entered during commissioning"
        /// is the row's own legitimate reading of that state.</para>
        /// </summary>
        private static void TravelTimeZero(IProjectInspection inspection)
        {
            foreach (ProjectElement product in AllProducts(inspection.Analyses))
            {
                ProjectElement? up = Setting(product, TravelUpTag);
                ProjectElement? down = Setting(product, TravelDownTag);
                if (up is null && down is null)
                {
                    continue;
                }

                if (Stored(up) == 0 || Stored(down) == 0)
                {
                    // WHICHEVER direction is the zero one — unlike the fade pair, this row fires on either, so
                    // the offending direction is known and naming the other would send the reader to a value
                    // that is not the finding.
                    inspection.Report(
                        product,
                        Arguments(("product", Name(product))),
                        Fix(Stored(up) == 0 ? up : down));
                }
            }
        }

        /// <summary>
        /// WHERE one of these findings is repaired: the SETTING element behind the value, and the attribute it
        /// stores the value in.
        ///
        /// <para>Every rule in this file reports the PRODUCT, and that is right for the reader — a dimmer whose
        /// range is inverted is a fact about that dimmer, and the product is the row the tree draws. But the
        /// repair is one field of one <c>dimmer_setting_*</c> child, which the tree does not draw at all, so
        /// anchoring and repairing part company. Before the per-occurrence fix location the family had no way to
        /// say the second half, and every one of its findings stopped at the product's dialog.</para>
        ///
        /// <para>The value lives in <c>value</c> on the setting element, which is the same pair the composed
        /// dialog binds its field to — so the route ends on the control the installer edits.</para>
        /// </summary>
        /// <remarks>
        /// Takes the setting ELEMENT rather than its tag: every caller has already resolved it to read the value
        /// it stores, and re-finding it by tag would walk the product's subtree a second time to reach the
        /// element in hand.
        /// </remarks>
        private static FixLocation? Fix(ProjectElement? setting) =>
            setting?.Id is { } id ? new FixLocation(id, "value") : null;

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>Every product carrying a dimmer-settings group.</summary>
        private static IEnumerable<ProjectElement> Dimmers(IProjectInspection inspection) =>
            AllProducts(inspection.Analyses).Where(p => p.FindDescendantOrSelf(e => e.Tag == "dimmer_settings") is not null);

        /// <summary>The product's setting element of that tag, or null when it carries none.</summary>
        private static ProjectElement? Setting(ProjectElement product, string tag) =>
            product.FindDescendantOrSelf(e => e.Tag == tag);

        /// <summary>
        /// The value a setting element STORES, or null when it stores none. Raw on purpose: the schema would
        /// default an absent value to zero, and the vendor's dialog shows its factory default there instead — so an
        /// absent value is an uncommissioned setting, not a stored zero.
        /// </summary>
        private static long? Stored(ProjectElement product, string tag) => Stored(Setting(product, tag));

        /// <summary>The same read over an already-resolved setting element — see <see cref="Fix"/>.</summary>
        private static long? Stored(ProjectElement? setting) =>
            setting?.GetAttribute("value") is { } raw
            && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                ? value
                : null;
    }
}
