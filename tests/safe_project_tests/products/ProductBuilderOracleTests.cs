#nullable enable
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Component tests for <see cref="ProductDefinitionBuilder"/>: author each product from code and assert the result
    /// reduces to the same canonical component the matching synthetic <c>Products\*.def</c> oracle yields
    /// (<see cref="SyntheticOracle.AssertMatchesOracle"/>). Catalog-free / install-dir-free — the design contract
    /// "a builder is a code-authored CatalogReader", proven directly against the oracle rather than through a project.
    /// </summary>
    public class ProductBuilderOracleTests
    {
        private const string Dir = "products/synthetic/";

        [Test]
        public void Author_9f01_Input_MatchesOracle()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f01", "01#Synthetic Input Product")
                .Locked().EnduserReport()
                .Note("Synthetic oracle - not a real product (æøå)")
                .AddInput("Input A", i => i.Note("Synthetic pin A").Address("_0x1").CableColour("ColourA"))
                .AddInput("Input B", i => i.Note("Synthetic pin B").Address("_0x2").CableColour("ColourB").Attribute("inivalue", "on"))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f01_input.def");
            Assert.Multiple(() =>
            {
                Assert.That(product.ProductIdentifier, Is.EqualTo("_0x9f01"));
                Assert.That(product.Body.Tag, Is.EqualTo("product_dataline"));
                // Locked/EnduserReport ride the .def DTD default (yes) so they drop in the oracle-grammar comparison;
                // pin them directly on the registry-canonicalized Build() body, where they survive.
                Assert.That(product.Body.GetAttribute("locked"), Is.EqualTo("yes"));
                Assert.That(product.Body.GetAttribute("enduser_report"), Is.EqualTo("yes"));
            });
        }

        [Test]
        public void Author_9f02_Output_MatchesOracle()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f02", "Synthetic Output Product")
                .Locked(false)
                .Note("Synthetic oracle - install attrs (æ)")
                .Position("Pos-A").DocumentationTag("DOC-9F02").PowerGroup("PowerGroup-1")
                .CableType("CableType-X").CableNumber("K-42")
                .AddInput("Control", i => i.Address("_0x1"))
                .AddOutput("Output 1", o => o.Backup(false).Attribute("type", "led").Address("_0x2").CableColour("ColourC"))
                .AddScenes("Scene Set")
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f02_output.def");
        }

        [Test]
        public void Author_9f03_Resources_MatchesOracle()
        {
            // Embedded typeid-keyed enum (no name) — a "med logning" product's typedef block, spliced via RawChild;
            // the resource_enum wires typedef/inivalue at its ids (remapped on comparison).
            ProjectElement enumDefinition = El("enum_definition", 0x80, new[] { ("typeid", "_0x16") },
                El("enum_value", 0x81, new[] { ("typeid", "_0x17") }));
            string enumId = enumDefinition.GetAttribute("id")!;
            string valueId = enumDefinition.ChildrenOrEmpty()[0].GetAttribute("id")!;

            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f03", "03#Synthetic Sensor Product")
                .Note("Synthetic oracle - embedded enum + settings")
                .Attribute("icon", "_0x83")
                .RawChild(enumDefinition)
                .AddResource("resource_temperature", "Measure 1", r => r.Attribute("accessibility", "read"))
                .AddResource("resource_temperature", "Measure 2",
                    r => r.Note("Synthetic reading (å)").Attribute("accessibility", "read"))
                .AddResource("resource_input", "Alarm flag",
                    r => r.Note("Synthetic alarm").Attribute("accessibility", "read"))
                .AddResource("resource_enum", "Log 1",
                    r => r.Attribute("typedef", enumId).Attribute("inivalue", valueId))
                .RawChild(El("settings", 0x82, new[] { ("name", "Settings"), ("note", "Synthetic settings") },
                    El("dataline_input", 0x83, new[] { ("name", "Sensor pin") })))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f03_resources.def");
        }

        [Test]
        public void Author_9f04_Wireless_MatchesOracle()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Airlink("_0x9f04", "02#Synthetic Airlink Product")
                .Locked().EnduserReport()
                .Attribute("device_type", "_0x0804")
                .Note("Synthetic oracle - wireless (trådløs æøå)")
                .AddResource("airlink_relay", "Relay 1", r => r.Address("_0x01"))
                .AddScenes("Scene Set")
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f04_wireless.def");
            Assert.That(product.Body.GetAttribute("locked"), Is.EqualTo("yes"));
        }

        [Test]
        public void Author_9f05_Dimmer_MatchesOracle()
        {
            ProjectElement dimming = El("airlink_dimming", 0x93, new[] { ("name", "Level"), ("address_channel", "_0x01") });
            ProjectElement channel = El("rs485_led_dimmer_channel", 0x90,
                new[] { ("icon", "_0x86"), ("product_identifier", "_0x9f15"), ("name", "Channel 1"), ("channel", "_0x00"), ("channel_id", "") },
                El("airlink_dimmer_increase", 0x91, new[] { ("name", "Up"), ("address_channel", "_0x01") }),
                El("airlink_dimmer_decrease", 0x92, new[] { ("name", "Down"), ("address_channel", "_0x01") }),
                dimming,
                El("light_indication", 0x94, new[] { ("name", "Indicator"), ("note", "Synthetic indicator") }),
                El("scenes", 0x95, new[] { ("name", "Scene Set"), ("scene_resource", dimming.GetAttribute("id")!) }),
                El("rs485_led_dimmer_error_state_overcurrent", 0x96, new[] { ("name", "Error 1"), ("note", "Synthetic error state"), ("locked", "yes") }),
                El("rs485_led_dimmer_error_state_overvoltage", 0x97, new[] { ("name", "Error 2"), ("note", "Synthetic error state"), ("locked", "yes") }),
                El("rs485_led_dimmer_error_state_overheating", 0x98, new[] { ("name", "Error 3"), ("note", "Synthetic error state"), ("locked", "yes") }),
                El("rs485_led_dimmer_error_state_loadfailure", 0x99, new[] { ("name", "Error 4"), ("note", "Synthetic error state"), ("locked", "yes") }),
                El("dimmer_settings", 0x9a, System.Array.Empty<(string, string)>(),
                    El("dimmer_setting_minimum_value", 0xa1, System.Array.Empty<(string, string)>()),
                    El("dimmer_setting_maximum_value", 0xa2, System.Array.Empty<(string, string)>()),
                    El("dimmer_setting_fade_rate_up", 0xa3, System.Array.Empty<(string, string)>()),
                    El("dimmer_setting_fade_rate_down", 0xa4, System.Array.Empty<(string, string)>()),
                    El("dimmer_setting_dimming_rate", 0xa5, System.Array.Empty<(string, string)>()),
                    El("dimmer_setting_load_mode", 0xa6, System.Array.Empty<(string, string)>())));

            ProductDefinition product = ProductDefinitionBuilder
                .Rs485LedDimmer("_0x9f05", "Synthetic RS485 Dimmer Product")
                .Attribute("serialnumber", "")
                .Note("Synthetic oracle - dimmer (æøå)")
                .Attribute("icon", "_0x86")
                .AddResource("resource_flag", "Sync flag", r => r.Attribute("inivalue", "off"))
                .RawChild(channel)
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f05_dimmer.def");
        }

        [Test]
        public void Author_9f06_Modem_MatchesOracle()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Rs485SmsModem("_0x9f06", "05#Synthetic SMS Modem Product")
                .Locked()
                .Attribute("icon", "_0x11")
                .Note("Synthetic oracle - SMS modem")
                .RawChild(El("sms_modem_settings", 0x80, new[] { ("icon", "_0xd") },
                    El("sms_modem_pincode", 0x81, new[] { ("value", "1234"), ("minimum", "0"), ("maximum", "9999") })))
                .RawChild(El("sms_modem_settings", 0x82, new[] { ("name", "Numbers 1-3 (æ)"), ("icon", "_0xd") },
                    El("sms_modem_phonenumber", 0x83, new[] { ("address", "1") }),
                    El("sms_modem_phonenumber", 0x84, new[] { ("address", "2") }),
                    El("sms_modem_phonenumber", 0x85, new[] { ("address", "3") })))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f06_modem.def");
        }

        [Test]
        public void Author_9f07_Meter_MatchesOracle()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .S0Device("_0x9f07", "Synthetic S0 Meter Product")
                .Locked()
                .Attribute("ticks", "100")
                .Attribute("icon", "_0x99")
                .Note("Synthetic oracle - energy meter (måler)")
                .AddResource("W", "Power now")
                .AddResource("kWh", "Energy total", r => r.Attribute("accessibility", "read-write"))
                .AddResource("resource_date", "Billing date",
                    r => r.Attribute("year", "2000").Attribute("month", "1").Attribute("day", "1"))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f07_meter.def");
        }

        [Test]
        public void Author_9f08_OpenWorld_MatchesOracle()
        {
            (ProjectElement _, ImmutableDictionary<string, string> blocks) =
                SyntheticOracle.Read(Dir + "synthetic_9f08_openworld.def");

            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f08", "Synthetic Open-World Product (åben)")
                .Locked()
                .Note("Synthetic oracle - non-registry element type")
                .InlineDtdBlock("resource_soil_moisture", blocks["resource_soil_moisture"])
                .RawChild(ElRaw("resource_soil_moisture", "_0x02",
                    new[] { ("name", "Custom reading"), ("unit", "%"), ("inivalue", "42.00") }))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f08_openworld.def");
        }

        [Test]
        public void From_ReopenAndRebuild_PreservesBody()
        {
            ProductDefinition original = ProductDefinitionBuilder
                .Dataline("_0x1234", "Round Trip")
                .Locked(false).EnduserReport().Note("Round-trip note (æ)").Position("Loft")
                .AddInput("Tryk", i => i.Address("_0x1").CableColour("Rød"))
                .AddOutput("Udgang", o => o.Address("_0x2").Backup())
                .AddScenes()
                .Build();

            ProductDefinition reopened = ProductDefinitionBuilder.From(original).Build();

            ImmutableDictionary<string, string> registry = ImmutableDictionary<string, string>.Empty;
            Assert.That(DefinitionNormalizer.Normalize(reopened.Body, registry),
                Is.EqualTo(DefinitionNormalizer.Normalize(original.Body, registry)));
        }

        // A registry-typed element for a RawChild subtree, id = (counter << 8) | typeCode for the tag.
        private static ProjectElement El(string tag, int counter, (string Name, string Value)[] attrs,
            params ProjectElement[] children) =>
            ElRaw(tag, new ElementId(counter, TypeCode.RequireForTag(tag)).ToToken(), attrs, children);

        // An element with a caller-supplied id token (for open-world tags with no registered type code).
        private static ProjectElement ElRaw(string tag, string idToken, (string Name, string Value)[] attrs,
            params ProjectElement[] children)
        {
            ElementId? id = ElementId.TryParse(idToken, out ElementId parsed) ? parsed : null;
            ImmutableArray<(string, string)> bag = new[] { ("id", idToken) }.Concat(attrs).ToImmutableArray();
            return new ProjectElement(tag, id, bag, children.ToImmutableArray());
        }
    }
}
