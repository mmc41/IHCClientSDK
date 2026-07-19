#nullable enable
namespace Ihc.Vis
{
    /// <summary>
    /// API-B (fablerefac Wave 1): the coarse classification of a <c>ProjectElement</c> by its tag — <i>what kind</i>
    /// of thing a node is, independent of what it is named. Computed tag-only (context-free) by the <c>Kind</c>
    /// extension property (see <see cref="ProjectElementRead"/>); a product's sub-family is a separate axis via
    /// <c>ProductClassifier.Classify</c>.
    /// </summary>
    /// <remarks>
    /// The GUI's <c>TreeNodeViewModel</c> string <c>NodeKind</c> and kind-flags derive from this in W3-7, where
    /// <c>switch</c>es <i>over</i> <see cref="ElementKind"/> gain compiler-checked exhaustiveness. The tag→kind
    /// mapper itself necessarily has an <see cref="Unknown"/> fallback, so a newly-added schema tag is <b>not</b> a
    /// compile break there — the new-tag tripwire test covers that instead. <see cref="Unknown"/> is the zero value
    /// so an unclassified default reads as "nobody classified this", never as a kind in its own right.
    /// </remarks>
    public enum ElementKind
    {
        /// <summary>No tag classification applies — the safe default (<c>default(ElementKind)</c>).</summary>
        Unknown,

        /// <summary>A room/locality (<c>group</c>) or the locality container (<c>groups</c>).</summary>
        Locality,

        /// <summary>A product device root (<c>product_*</c>); its family is a separate axis (<c>ProductClassifier</c>).</summary>
        Product,

        /// <summary>A wired product IO pin (<c>dataline_input</c>/<c>dataline_output</c>).</summary>
        DatalinePin,

        /// <summary>A wireless (IHC Wireless / airlink) product IO or command pin (<c>airlink_*</c>).</summary>
        WirelessPin,

        /// <summary>A function block (<c>functionblock</c>).</summary>
        FunctionBlock,

        /// <summary>A function-block variable section (<c>inputs</c>/<c>outputs</c>/<c>settings</c>/<c>internalsettings</c>).</summary>
        VariableSection,

        /// <summary>A value-bearing resource pin or leaf (<c>resource_*</c> except enum/scene, plus device setting
        /// and IO leaves, energy units and the S0 device).</summary>
        Resource,

        /// <summary>An enum-typed resource pin (<c>resource_enum</c>).</summary>
        EnumResource,

        /// <summary>The enum type model (<c>enum_definitions</c>/<c>enum_definition</c>/<c>enum_value</c>).</summary>
        EnumDefinition,

        /// <summary>A link half (<c>link_from_resource</c>/<c>link_to_resource</c>).</summary>
        Link,

        /// <summary>A scene container (<c>scenes</c>) or scene resource (<c>resource_scene</c>).</summary>
        Scene,

        /// <summary>A scene membership half (<c>scene_link</c>/<c>scene_dimmer</c>/<c>scene_relay</c>/<c>scene_shutter</c>).</summary>
        SceneMember,

        /// <summary>Any programming-tree element — <c>programs</c>, <c>program_*</c>, the events/conditions/actions
        /// containers and their event/condition/action leaves.</summary>
        ProgramNode,

        /// <summary>A device configuration group (<c>dimmer_settings</c>/<c>shutter_settings</c>/<c>sms_modem_settings</c>).</summary>
        DeviceSettings,

        /// <summary>A module address-map container (<c>documentation_modules</c>/<c>dataline_input_modules</c>/<c>dataline_output_modules</c>).</summary>
        ModuleMap,

        /// <summary>An id-less project metadata root (<c>utcs_project</c>/<c>modified</c>/<c>customer_info</c>/<c>installer_info</c>/<c>project_info</c>).</summary>
        Metadata,
    }
}
