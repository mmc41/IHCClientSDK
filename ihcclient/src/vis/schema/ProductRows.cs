#nullable enable
using System;

namespace Ihc.Vis.Schema
{
    /// <summary>
    /// Which of a product body's direct children are resources, and which of those resources IHC Visual
    /// deliberately keeps out of its project tree. A vendor grammar fact — peer of <see cref="TypeCode"/>,
    /// <see cref="ReciprocalTags"/> and <see cref="ResourceMaterialization"/> — and the only one of them that is
    /// <c>public</c>, because a GUI (not just the SDK) has to read it to render the tree the vendor renders.
    /// <para>The two questions here are deliberately separate, and conflating them is the trap:
    /// <see cref="IsStructuralChild"/> is a MODEL fact — a container is not a resource, for anybody.
    /// <see cref="IsHiddenFromTree"/> is a DISPLAY fact — the row IS a genuine resource, physically present in the
    /// <c>.vis</c> and written back verbatim on save; only the vendor's tree declines to draw it. Folding the
    /// second into <c>ProductDefinition.Resources</c> would wrongly hide these rows from the product menu and
    /// every definition-model consumer, and would put a display concern in the engine.</para>
    /// </summary>
    public static class ProductRows
    {
        /// <summary>
        /// A product body's direct children are its I/O pins and family resources plus a few STRUCTURAL blocks that
        /// are not resources and must be kept out of the resource preview: the scenes container, an embedded
        /// <c>enum_definition</c> (a "med logning" product's typedef block), and any settings/config container. The
        /// last covers the generic dataline <c>settings</c> AND every family-specific variant
        /// (<c>dimmer_settings</c> on airlink dimmers, <c>sms_modem_settings</c> on rs485 modems, …); matching the
        /// <c>_settings</c> suffix keeps a new family's settings block from leaking in as a bogus resource, where a
        /// hardcoded list would silently miss it. (The function-block projections sidestep this by reading named
        /// containers, so they never meet these at the body root; <c>internalsettings</c> is a function-block-only
        /// container and never a product-body child.)
        /// <para>A nested sub-product container that is itself a family resource — the
        /// <c>rs485_led_dimmer_channel</c> of a channel-based dimmer, which nests its own increase/decrease/dimming
        /// pins — is deliberately NOT structural: it is a resource in its own right and surfaces as one entry; the
        /// shallow direct-children preview simply does not descend into it.</para>
        /// </summary>
        public static bool IsStructuralChild(string tag) =>
            tag is "scenes" or "enum_definition" or "settings"
            || tag.EndsWith("_settings", StringComparison.Ordinal);

        /// <summary>
        /// Whether IHC Visual suppresses this resource row from its project tree. Two disjoint criteria that share
        /// no signal — neither catches the other's case, so both are needed:
        /// <list type="bullet">
        /// <item><b>By tag</b> — a shutter product's <c>airlink_shutter_up</c>/<c>airlink_shutter_down</c> pins
        /// ("Op"/"Ned"). They carry no distinguishing attribute: they are structurally identical to their visible
        /// <c>airlink_input</c> siblings and even reuse the first input's <c>address_channel</c>, so ONLY the
        /// element tag identifies them.</item>
        /// <item><b>By attribute</b> — any resource carrying <c>setting="yes"</c> (a thermostat/sensor calibration
        /// row). Tag cannot decide this one: "Kalibrering af temperaturføler" shares its <c>resource_temperature</c>
        /// tag with the VISIBLE "Temperatur"/"Dugpunkt" rows of the same product.</item>
        /// </list>
        /// <paramref name="settingAttribute"/> is the raw <c>setting</c> attribute — pass what
        /// <c>ProjectElement.GetAttribute("setting")</c> returns. It does no DTD defaulting, so an absent attribute
        /// arrives as <c>null</c> and correctly does not suppress (the DTD default is <c>"no"</c>).
        /// </summary>
        public static bool IsHiddenFromTree(string tag, string? settingAttribute) =>
            tag is "airlink_shutter_up" or "airlink_shutter_down"
            || IsSetting(settingAttribute);

        /// <summary>
        /// The attribute a catalog marks a configurable SETTING resource with, and the value that marks it. Named
        /// here rather than spelled at each reader: "what counts as a setting" is one vendor grammar fact, and it is
        /// asked in three unrelated places — this class hides such a row from the tree, the product dialog gates its
        /// <i>Indstillinger</i> slot on it, and the same rule picks the rows that go in the slot. Three literals
        /// would let the grid's presence, its contents and the tree disagree about the same row.
        /// </summary>
        public const string SettingAttribute = "setting";

        /// <inheritdoc cref="SettingAttribute"/>
        public const string SettingValue = "yes";

        /// <summary>
        /// Whether a raw <see cref="SettingAttribute"/> value marks the resource as a configurable setting. Takes
        /// the raw attribute (what <c>ProjectElement.GetAttribute("setting")</c> returns) and does no DTD
        /// defaulting, so an absent attribute arrives as <c>null</c> and is correctly not a setting.
        /// </summary>
        public static bool IsSetting(string? settingAttribute) =>
            string.Equals(settingAttribute, SettingValue, StringComparison.Ordinal);
    }
}
