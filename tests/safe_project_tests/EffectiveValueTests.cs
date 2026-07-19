namespace safe_project_tests;

/// <summary>
/// fablerefac W1-3 (API-C): <c>project.View(element).Effective(attr)</c> returns an attribute's value when present
/// (empty stays empty), the element type's DTD default when absent-but-defaulted, and null when the type declares
/// no default — resolved through the project's schema view, so the GUI never re-encodes <c>?? "no"</c>/<c>?? "auto"</c>.
/// </summary>
public class EffectiveValueTests
{
    // A project supplies the schema-view context; its InlineDtdBlocks is empty, so defaults resolve through the SDK
    // registry — the same project.SchemaView.TryGet path a per-project inline-DTD default would take.
    private static readonly Project SchemaContext = new(ProjectElement.Create("utcs_project", null, [], []));

    private static string? Effective(string tag, string attr, params (string Name, string Value)[] attrs) =>
        SchemaContext.View(ProjectElement.Create(tag, null, attrs, [])).Effective(attr);

    // Absent attribute → the type's DTD default (the ?? "no"/?? "auto" encodings the GUI should not own), or null
    // when the type declares no default for it.
    [TestCase("functionblock", "locked", "no")]                 // US-020 lock flag (?? "no")
    [TestCase("product_dataline", "locked", "no")]
    [TestCase("product_dataline", "enduser_report", "no")]      // end-user report flag (?? "no")
    [TestCase("dataline_output", "backup", "no")]               // power-loss persistence (?? "no")
    [TestCase("dimmer_setting_load_mode", "value", "auto")]     // ?? "auto" load mode
    [TestCase("dimmer_setting_fade_rate_up", "value", "0")]     // dimmer numeric — DTD default is 0 (see NB below)
    [TestCase("dimmer_setting_minimum_value", "value", "0")]
    [TestCase("dataline_output", "not_a_declared_attr", null)]  // absent + no declared default → null
    public void Effective_AbsentAttr_ReturnsDtdDefaultOrNull(string tag, string attr, string? expected)
    {
        Assert.That(Effective(tag, attr), Is.EqualTo(expected));
    }

    [Test]
    public void Effective_PresentAttr_WinsOverTheDefault()
    {
        Assert.That(Effective("functionblock", "locked", ("locked", "yes")), Is.EqualTo("yes"));
    }

    // Boundary: an explicitly empty value is a present value, not an absent one — it must not fall through to the default.
    [Test]
    public void Effective_PresentButEmptyAttr_StaysEmpty()
    {
        Assert.That(Effective("dataline_output", "backup", ("backup", "")), Is.EqualTo(string.Empty));
    }
}
