#nullable enable
using System;
using Ihc.Vis.Model;

namespace Ihc.Vis
{
    /// <summary>
    /// The SDK-side typed read surface over <see cref="ProjectElement"/> (fablerefac Wave 1, API-A/B/C): the GUI
    /// reads element classification and effective attribute values through these extension members instead of
    /// hand-parsing raw tags and attribute strings, so schema knowledge lives here (in <c>Ihc.Vis</c>) beside the
    /// write-side <c>ProductRef</c>/<c>FunctionBlockRef</c> handles. W1-2 lands the tag classification
    /// (<c>Kind</c>); W1-3/W1-4 add the effective-value readers. Seeded by the W1-1 extension-member spike.
    /// </summary>
    public static class ProjectElementRead
    {
        extension(ProjectElement element)
        {
            /// <summary>
            /// API-B: the element's coarse <see cref="ElementKind"/>, computed from its <see cref="ProjectElement.Tag"/>
            /// alone (context-free — no project or schema needed). A product's sub-family is a separate axis
            /// (<c>ProductClassifier.Classify</c>).
            /// </summary>
            public ElementKind Kind => ClassifyTag(element.Tag);
        }

        /// <summary>
        /// The tag → <see cref="ElementKind"/> map (API-B): exact tags first, then open-world prefix rules so an
        /// undocumented <c>product_x</c>/<c>airlink_x</c>/<c>resource_x</c> still classifies. It necessarily has an
        /// <see cref="ElementKind.Unknown"/> fallback (a new schema tag cannot be a compile break here) — the new-tag
        /// tripwire test guards that; compiler-checked exhaustiveness applies downstream to <c>switch</c>es over
        /// <see cref="ElementKind"/> (W3-7). Exact arms precede the prefix guards so <c>resource_enum</c>/
        /// <c>resource_scene</c>/<c>*_settings</c> beat the broader <c>resource_</c>/<c>*_setting</c> rules.
        /// </summary>
        private static ElementKind ClassifyTag(string tag) => tag switch
        {
            "utcs_project" or "modified" or "customer_info" or "installer_info" or "project_info" => ElementKind.Metadata,
            "group" or "groups" => ElementKind.Locality,
            "functionblock" => ElementKind.FunctionBlock,
            "inputs" or "outputs" or "settings" or "internalsettings" => ElementKind.VariableSection,
            "resource_enum" => ElementKind.EnumResource,
            "enum_definitions" or "enum_definition" or "enum_value" => ElementKind.EnumDefinition,
            "link_from_resource" or "link_to_resource" => ElementKind.Link,
            "scenes" or "resource_scene" => ElementKind.Scene,
            "scene_link" or "scene_dimmer" or "scene_relay" or "scene_shutter" => ElementKind.SceneMember,
            "programs" or "program_simple" or "program_sub" or "program_case"
                or "events" or "conditions" or "actions"
                or "event" or "event_power" or "condition" or "action" or "case_action" => ElementKind.ProgramNode,
            "dimmer_settings" or "shutter_settings" or "sms_modem_settings" => ElementKind.DeviceSettings,
            "documentation_modules" or "dataline_input_modules" or "dataline_output_modules" => ElementKind.ModuleMap,
            "dataline_input" or "dataline_output" => ElementKind.DatalinePin,
            "kW" or "kWh" or "W" or "Wh" or "s0_device" or "light_indication" => ElementKind.Resource,
            _ when tag.StartsWith("product_", StringComparison.Ordinal) => ElementKind.Product,
            _ when tag.StartsWith("airlink_", StringComparison.Ordinal) => ElementKind.WirelessPin,
            _ when tag.StartsWith("resource_", StringComparison.Ordinal) => ElementKind.Resource,
            _ when tag.StartsWith("dimmer_setting", StringComparison.Ordinal) => ElementKind.Resource,
            _ when tag.StartsWith("shutter_setting", StringComparison.Ordinal) => ElementKind.Resource,
            _ when tag.StartsWith("sms_modem_", StringComparison.Ordinal) => ElementKind.Resource,
            _ when tag.StartsWith("rs485_led_dimmer", StringComparison.Ordinal) => ElementKind.Resource,
            _ => ElementKind.Unknown,
        };
    }
}
