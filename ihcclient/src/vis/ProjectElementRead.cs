#nullable enable
using System;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

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
        /// <summary>The vendor enum <c>typeid</c> of the "Logning" state type behind the "Log …" rows — the schema
        /// signal that identifies a log-mark row (A-22/US-068), read by the <c>IsLogRow</c> predicate below and by the
        /// editing layer's log-mark toggle.</summary>
        public const string LogEnumTypeId = "_0x16";

        extension(ProjectElement element)
        {
            /// <summary>
            /// API-B: the element's coarse <see cref="ElementKind"/>, computed from its <see cref="ProjectElement.Tag"/>
            /// alone (context-free — no project or schema needed). A product's sub-family is a separate axis
            /// (<c>ProductClassifier.Classify</c>).
            /// </summary>
            public ElementKind Kind => ClassifyTag(element.Tag);

            // ── Fine model classification (fablerefac W3-10): the tree projection distinguishes rows below the grain
            // of the 17-member coarse <see cref="ElementKind"/> (every program-tree node is one ElementKind.ProgramNode;
            // every link half one ElementKind.Link). These predicates carry that finer, schema-derived classification
            // here — SDK-side, beside the coarse Kind — so the GUI projector reads them instead of hand-matching raw
            // element tags. They are MODEL facts (which tag is a command vs an event vs a condition), not GUI concepts.

            /// <summary>A locality (room) — the <c>group</c> element specifically, NOT the <c>groups</c> container
            /// (both are the coarse <see cref="ElementKind.Locality"/>, so that can't tell them apart).</summary>
            public bool IsLocalityGroup => element.Tag == "group";

            /// <summary>A single program under a block's <c>Programs</c> (a simple or conditional program).</summary>
            public bool IsProgram => element.Tag is "program_simple" or "program_sub";

            /// <summary>A program event row — a resource-triggered or power-up event (US-028/US-033).</summary>
            public bool IsProgramEvent => element.Tag is "event" or "event_power";

            /// <summary>A program command leaf (US-028).</summary>
            public bool IsProgramCommand => element.Tag == "action";

            /// <summary>A conditional sub-program (US-029).</summary>
            public bool IsSubProgram => element.Tag == "program_sub";

            /// <summary>A <c>program_case</c> switch (US-031).</summary>
            public bool IsProgramCase => element.Tag == "program_case";

            /// <summary>A case value branch (US-031) — a command container whose label is user data.</summary>
            public bool IsCaseValue => element.Tag == "case_action";

            /// <summary>A single condition row (US-029).</summary>
            public bool IsCondition => element.Tag == "condition";

            /// <summary>A <c>conditions</c> group (US-029).</summary>
            public bool IsConditionsGroup => element.Tag == "conditions";

            /// <summary>An <c>actions</c> ("Commands") container (US-028/US-029).</summary>
            public bool IsActionsContainer => element.Tag == "actions";

            /// <summary>An <c>events</c> container (US-028).</summary>
            public bool IsEventsContainer => element.Tag == "events";

            /// <summary>A product's <c>scenes</c> container — a scenario-link target (US-024).</summary>
            public bool IsScenesContainer => element.Tag == "scenes";

            /// <summary>A scene membership row inside a <c>scenes</c> container (US-024).</summary>
            public bool IsSceneMember => element.Tag is "scene_relay" or "scene_dimmer" or "scene_shutter";

            /// <summary>A shutter scene membership (renders its direction, F-051/A-19).</summary>
            public bool IsSceneShutter => element.Tag == "scene_shutter";

            /// <summary>A link half under a pin — a follow-link end or a scene link (US-022/US-025).</summary>
            public bool IsLinkHalf => element.Tag is "link_from_resource" or "link_to_resource" or "scene_link";

            /// <summary>The source ("from") end of a follow-link (F-020 direction).</summary>
            public bool IsLinkFromEnd => element.Tag == "link_from_resource";

            /// <summary>A scene link row (US-025).</summary>
            public bool IsSceneLink => element.Tag == "scene_link";

            /// <summary>An output pin — a function-block or physical output, or a wireless relay (US-033).</summary>
            public bool IsOutputPin => element.Tag is "resource_output" or "dataline_output" or "airlink_relay";

            /// <summary>A node that maps to an IHC controller resource id (shown in the hover tooltip, US-048).</summary>
            public bool HasResourceId =>
                element.Tag is "resource_input" or "resource_output" or "dataline_input" or "dataline_output" or "functionblock";

            /// <summary>A function-block setting that carries a literal time value (hour/minute/second, A-21/F-062).</summary>
            public bool IsTimeSetting => element.Tag is "resource_timer" or "resource_timertime" or "resource_time";

            /// <summary>An enum type definition (<c>enum_definition</c>) — distinct from the <c>enum_value</c> rows and
            /// the <c>enum_definitions</c> container it groups (all the coarse <see cref="ElementKind.EnumDefinition"/>).</summary>
            public bool IsEnumDefinition => element.Tag == "enum_definition";

            /// <summary>A single enum state (<c>enum_value</c>).</summary>
            public bool IsEnumValue => element.Tag == "enum_value";

            /// <summary>A function block's scene output resource (<c>resource_scene</c>) — a scenario-link source.</summary>
            public bool IsSceneResource => element.Tag == "resource_scene";

            /// <summary>A wireless dimmer's dimming resource (<c>airlink_dimming</c>) — the Advanced dimmer target (US-015).</summary>
            public bool IsWirelessDimming => element.Tag == "airlink_dimming";

            /// <summary>Whether this element is a "Log …" row — a <c>resource_enum</c> whose enum type is the Logning
            /// type (<see cref="LogEnumTypeId"/>), resolved against <paramref name="project"/>. The signal a GUI uses
            /// to offer the log-mark toggle only where the vendor does (A-22/US-068). Unlike the context-free
            /// predicates above, this one needs the project to resolve the row's enum-type reference.</summary>
            public bool IsLogRow(Project project) =>
                element.Tag == "resource_enum"
                && ElementId.TryParse(element.GetAttribute("typedef"), out ElementId defId)
                && project.FindById(defId)?.GetAttribute("typeid") == LogEnumTypeId;
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
