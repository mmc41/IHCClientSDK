namespace safe_project_tests;

/// <summary>
/// fablerefac W1-4 (API-A): the universal read properties on <c>project.View(element)</c> each return the effective
/// value of one attribute — text as strings, <c>(yes | no)</c> flags decoded to bool, absent falling back to the
/// DTD default. The attribute-name literals live SDK-side, so the GUI stops hand-parsing them.
/// </summary>
public class ElementReadPropertiesTests
{
    private static readonly Project Ctx = new(ProjectElement.Create("utcs_project", null, [], []));

    private static ElementView View(string tag, params (string Name, string Value)[] attrs) =>
        Ctx.View(ProjectElement.Create(tag, null, attrs, []));

    [Test]
    public void TextProperties_ReturnThePresentValue()
    {
        ElementView v = View("product_dataline",
            ("name", "Kitchen light"), ("note", "north wall"), ("position", "_0x2"), ("icon", "_0x7"));

        Assert.Multiple(() =>
        {
            Assert.That(v.Name, Is.EqualTo("Kitchen light"));
            Assert.That(v.Note, Is.EqualTo("north wall"));
            Assert.That(v.Position, Is.EqualTo("_0x2"));
            Assert.That(v.Icon, Is.EqualTo("_0x7"));
        });
    }

    [Test]
    public void TextProperties_FallBackToTheDtdDefaultWhenAbsent()
    {
        ElementView v = View("product_dataline");   // name/note/position default "", icon defaults "_0x0"

        Assert.Multiple(() =>
        {
            Assert.That(v.Name, Is.EqualTo(""));
            Assert.That(v.Note, Is.EqualTo(""));
            Assert.That(v.Position, Is.EqualTo(""));
            Assert.That(v.Icon, Is.EqualTo("_0x0"));
        });
    }

    // Boolean round-trip: "yes" -> true, "no" -> false, absent -> the DTD default ("no" -> false).
    [TestCase("yes", true)]
    [TestCase("no", false)]
    public void Locked_DecodesTheYesNoFlag(string raw, bool expected)
    {
        Assert.That(View("functionblock", ("locked", raw)).Locked, Is.EqualTo(expected));
    }

    [Test]
    public void BooleanFlags_AbsentDecodeToTheirDefaultNo()
    {
        Assert.Multiple(() =>
        {
            Assert.That(View("functionblock").Locked, Is.False, "locked defaults to no");
            Assert.That(View("dataline_output").Backup, Is.False, "backup defaults to no");
            Assert.That(View("product_dataline").EnduserReport, Is.False, "enduser_report defaults to no");
            Assert.That(View("dataline_output", ("backup", "yes")).Backup, Is.True);
            Assert.That(View("product_dataline", ("enduser_report", "yes")).EnduserReport, Is.True);
        });
    }

    // Boundary on a dimmer numeric: present value wins; absent falls back to the DTD default (0 / auto).
    [Test]
    public void Value_And_InitialValue_PresentThenDefault()
    {
        Assert.Multiple(() =>
        {
            Assert.That(View("dimmer_setting_maximum_value", ("value", "100")).Value, Is.EqualTo("100"));
            Assert.That(View("dimmer_setting_maximum_value").Value, Is.EqualTo("0"), "dimmer numeric default is 0");
            Assert.That(View("dimmer_setting_load_mode").Value, Is.EqualTo("auto"));
            Assert.That(View("dataline_output", ("inivalue", "on")).InitialValue, Is.EqualTo("on"));
            Assert.That(View("dataline_output").InitialValue, Is.EqualTo("off"), "inivalue defaults to off");
        });
    }

    // IsUnlinkedWireless reuses ProductClassifier over the tag + effective serialnumber.
    [Test]
    public void IsUnlinkedWireless_ReflectsWirelessAndSerial()
    {
        Assert.Multiple(() =>
        {
            Assert.That(View("product_airlink").IsUnlinkedWireless, Is.True, "an airlink product with no serial is unlinked");
            Assert.That(View("product_airlink", ("serialnumber", "12345")).IsUnlinkedWireless, Is.False, "a commissioned one is linked");
            Assert.That(View("product_dataline").IsUnlinkedWireless, Is.False, "a wired product is never 'unlinked wireless'");
        });
    }
}
