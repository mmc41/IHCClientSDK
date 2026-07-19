using System.Linq;

namespace safe_project_tests;

/// <summary>
/// fablerefac W1-2 (API-B): <c>element.Kind</c> classifies a <see cref="ProjectElement"/> into a coarse
/// <see cref="ElementKind"/> from its tag alone. Equivalence-partition one representative tag per kind, confirm the
/// product-family sub-axis stays with <see cref="ProductClassifier"/>, and a tripwire that fails when a schema tag
/// is added to <c>TypeCode.ByTag</c> without a kind mapping.
/// </summary>
public class ElementKindTests
{
    // One representative element per kind (equivalence partitioning), plus an unknown tag (the fallback partition).
    [TestCase("group", ElementKind.Locality)]
    [TestCase("groups", ElementKind.Locality)]
    [TestCase("product_dataline", ElementKind.Product)]
    [TestCase("dataline_input", ElementKind.DatalinePin)]
    [TestCase("dataline_output", ElementKind.DatalinePin)]
    [TestCase("airlink_input", ElementKind.WirelessPin)]
    [TestCase("airlink_shutter_up", ElementKind.WirelessPin)]
    [TestCase("functionblock", ElementKind.FunctionBlock)]
    [TestCase("inputs", ElementKind.VariableSection)]
    [TestCase("internalsettings", ElementKind.VariableSection)]
    [TestCase("resource_flag", ElementKind.Resource)]
    [TestCase("s0_device", ElementKind.Resource)]
    [TestCase("kWh", ElementKind.Resource)]
    [TestCase("dimmer_setting_load_mode", ElementKind.Resource)]
    [TestCase("resource_enum", ElementKind.EnumResource)]
    [TestCase("enum_definition", ElementKind.EnumDefinition)]
    [TestCase("enum_value", ElementKind.EnumDefinition)]
    [TestCase("link_from_resource", ElementKind.Link)]
    [TestCase("link_to_resource", ElementKind.Link)]
    [TestCase("scenes", ElementKind.Scene)]
    [TestCase("resource_scene", ElementKind.Scene)]
    [TestCase("scene_link", ElementKind.SceneMember)]
    [TestCase("scene_dimmer", ElementKind.SceneMember)]
    [TestCase("program_simple", ElementKind.ProgramNode)]
    [TestCase("event", ElementKind.ProgramNode)]
    [TestCase("condition", ElementKind.ProgramNode)]
    [TestCase("action", ElementKind.ProgramNode)]
    [TestCase("events", ElementKind.ProgramNode)]
    [TestCase("program_case", ElementKind.ProgramNode)]
    [TestCase("dimmer_settings", ElementKind.DeviceSettings)]
    [TestCase("sms_modem_settings", ElementKind.DeviceSettings)]
    [TestCase("documentation_modules", ElementKind.ModuleMap)]
    [TestCase("dataline_input_modules", ElementKind.ModuleMap)]
    [TestCase("utcs_project", ElementKind.Metadata)]
    [TestCase("project_info", ElementKind.Metadata)]
    [TestCase("this_is_not_a_real_tag", ElementKind.Unknown)]
    public void Kind_ClassifiesTagIntoExpectedElementKind(string tag, ElementKind expected)
    {
        ProjectElement element = ProjectElement.Create(tag, null, [], []);

        Assert.That(element.Kind, Is.EqualTo(expected));
    }

    // A product of each ProductFamily is the coarse Product kind; the family sub-kind is the ProductClassifier axis.
    [TestCase("product_dataline", ProductFamily.Dataline)]
    [TestCase("product_airlink", ProductFamily.Airlink)]
    [TestCase("product_rs485_led_dimmer", ProductFamily.Rs485LedDimmer)]
    [TestCase("product_rs485_modem", ProductFamily.Rs485Modem)]
    [TestCase("product_rs485_sms_modem", ProductFamily.Rs485SmsModem)]
    public void Kind_ForAnyProductFamily_IsProduct_WithFamilyOnTheSeparateAxis(string tag, ProductFamily family)
    {
        ProjectElement element = ProjectElement.Create(tag, null, [], []);

        Assert.Multiple(() =>
        {
            Assert.That(element.Kind, Is.EqualTo(ElementKind.Product), "Kind is the coarse Product axis");
            Assert.That(ProductClassifier.Classify(tag), Is.EqualTo(family), "the family is the ProductClassifier axis");
        });
    }

    // New-tag tripwire: every registered schema tag must map to a non-Unknown kind. Fails when a tag is added to
    // TypeCode.ByTag without extending ProjectElementRead.ClassifyTag.
    [Test]
    public void EveryRegisteredSchemaTag_MapsToAKnownKind()
    {
        var unmapped = TypeCode.ByTag.Keys
            .Where(tag => ProjectElement.Create(tag, null, [], []).Kind == ElementKind.Unknown)
            .OrderBy(tag => tag)
            .ToList();

        Assert.That(unmapped, Is.Empty,
            "these TypeCode.ByTag tags have no ElementKind mapping — add them to ProjectElementRead.ClassifyTag");
    }
}
