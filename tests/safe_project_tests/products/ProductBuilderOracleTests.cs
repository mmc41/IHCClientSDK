#nullable enable
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Component tests for <see cref="ProductDefinitionBuilder"/>: author each product <b>entirely from code</b>
    /// (grammar + body + ids — no file read feeds the authoring) and assert both that the result reduces to the same
    /// canonical component the matching synthetic <c>Products\*.def</c> oracle yields
    /// (<see cref="SyntheticOracle.AssertMatchesOracle"/>) and that <see cref="CatalogFileWriter"/>-serializing it
    /// reproduces the oracle's <b>bytes</b> under the fidelity relation
    /// (<see cref="SyntheticOracle.AssertWritesOracleBytes(ProductDefinition, string, string[])"/> — the writer's
    /// own gate covers the well-formedness half). The tests that use a bare family factory byte-pin that family's
    /// grammar preset; the rest carry their oracle's exact grammar from <see cref="OracleGrammars"/>.
    /// </summary>
    public class ProductBuilderOracleTests
    {
        private const string Dir = "products/synthetic/";

        [Test]
        public void Author_9f01_Input_MatchesOracle()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f01", "01#Synthetic Input Product")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRoot("yes"), OracleGrammars.DatalineInput,
                }))
                .Note("Synthetic oracle - not a real product (æøå)")
                .AddInput("Input A", i => i.Note("Synthetic pin A").Address("_0x1").CableColour("ColourA"))
                .AddInput("Input B", i => i.Note("Synthetic pin B").Address("_0x2").CableColour("ColourB").Attribute("inivalue", "on"))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f01_input.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f01_input.def",
                "_0x01", "_0x02", "_0x03");
            Assert.Multiple(() =>
            {
                Assert.That(product.ProductIdentifier, Is.EqualTo("_0x9f01"));
                Assert.That(product.Body.Tag, Is.EqualTo("product_dataline"));
                // The oracle body omits locked/enduser_report — they ride this file's DTD defaults (both "yes"),
                // which the grammar carries as data; the lean authored body must omit them identically.
                Assert.That(product.Body.GetAttribute("locked"), Is.Null);
                Assert.That(product.Grammar.TryGetDeclaration("product_dataline")!
                    .FindAttr("enduser_report")!.RawLiteral, Is.EqualTo("yes"));
            });
        }

        [Test]
        public void Author_9f02_Output_MatchesOracle_AndPinsDatalinePreset()
        {
            // Bare Dataline factory — no grammar call — so the written header IS the preset's rendering.
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
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f02_output.def",
                "_0x01", "_0x02", "_0x03", "_0x04");
        }

        [Test]
        public void Author_9f03_Resources_MatchesOracle()
        {
            // Embedded typeid-keyed enum (no name) — a "med logning" product's typedef block, spliced via RawChild;
            // the resource_enum wires typedef/inivalue at its ids (remapped on comparison).
            ProjectElement enumDefinition = El("enum_definition", 0x80, new[] { ("typeid", "_0x16") },
                El("enum_value", 0x81, new[] { ("typeid", "_0x17") }));
            string enumId = enumDefinition.GetAttribute("id")!;
            string valueId = enumDefinition.Children[0].GetAttribute("id")!;

            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f03", "03#Synthetic Sensor Product")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRootLean7, OracleGrammars.DatalineInput,
                    OracleGrammars.ResourceTemperatureProduct, OracleGrammars.ResourceInputProduct,
                    OracleGrammars.ResourceEnumProduct, OracleGrammars.SettingsProduct,
                }))
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
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f03_resources.def",
                "_0x01", "_0x50", "_0x51", "_0x02", "_0x03", "_0x04", "_0x05", "_0x06", "_0x07");
        }

        [Test]
        public void Author_9f04_Wireless_MatchesOracle_AndPinsAirlinkPreset()
        {
            // Lean authoring, file order: device_type precedes name; locked/enduser_report ride the DTD defaults
            // (the oracle body omits them — baking them would change the written bytes).
            ProductDefinition product = ProductDefinitionBuilder
                .Airlink("_0x9f04", "02#Synthetic Airlink Product")
                .Attribute("device_type", "_0x0804")
                .Name("02#Synthetic Airlink Product")
                .Note("Synthetic oracle - wireless (trådløs æøå)")
                .AddResource("airlink_relay", "Relay 1", r => r.Address("_0x01"))
                .AddScenes("Scene Set")
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f04_wireless.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f04_wireless.def",
                "_0x01", "_0x02", "_0x03");
            Assert.That(product.Body.GetAttribute("locked"), Is.Null,
                "locked rides the .def DTD default — the raw body omits it exactly as the oracle does");
        }

        [Test]
        public void Author_9f05_Dimmer_MatchesOracle_AndPinsLedDimmerPreset()
        {
            ProjectElement dimming = El("airlink_dimming", 0x93, new[] { ("name", "Level"), ("address_channel", "_0x01") });
            // The oracle writes the channel's icon BEFORE its id (authentic vendor attribute order) — author it
            // with the id at its file position rather than through the id-first El helper.
            ProjectElement channel = ElOrdered("rs485_led_dimmer_channel",
                new[] { ("icon", "_0x86"), ("id", new ElementId(0x90, TypeCode.RequireForTag("rs485_led_dimmer_channel")).ToToken()), ("product_identifier", "_0x9f15"), ("name", "Channel 1"), ("channel", "_0x00"), ("channel_id", "") },
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
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f05_dimmer.def",
                "_0x01", "_0x02", "_0x10", "_0x11", "_0x12", "_0x13", "_0x14", "_0x15", "_0x16", "_0x17",
                "_0x18", "_0x19", "_0x20", "_0x21", "_0x22", "_0x23", "_0x24", "_0x25", "_0x26");
        }

        [Test]
        public void Author_9f06_Modem_MatchesOracle_AndPinsSmsModemPreset()
        {
            // Lean authoring, file order: note before icon; locked rides the DTD default (the oracle omits it).
            ProductDefinition product = ProductDefinitionBuilder
                .Rs485SmsModem("_0x9f06", "05#Synthetic SMS Modem Product")
                .Note("Synthetic oracle - SMS modem")
                .Attribute("icon", "_0x11")
                .RawChild(El("sms_modem_settings", 0x80, new[] { ("icon", "_0xd") },
                    El("sms_modem_pincode", 0x81, new[] { ("value", "1234"), ("minimum", "0"), ("maximum", "9999") })))
                .RawChild(El("sms_modem_settings", 0x82, new[] { ("name", "Numbers 1-3 (æ)"), ("icon", "_0xd") },
                    El("sms_modem_phonenumber", 0x83, new[] { ("address", "1") }),
                    El("sms_modem_phonenumber", 0x84, new[] { ("address", "2") }),
                    El("sms_modem_phonenumber", 0x85, new[] { ("address", "3") })))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f06_modem.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f06_modem.def",
                "_0x01", "_0x10", "_0x11", "_0x20", "_0x21", "_0x22", "_0x23");
        }

        [Test]
        public void Author_9f07_Meter_MatchesOracle_AndPinsS0Preset()
        {
            // Lean authoring, file order: note, icon, ticks; locked rides the DTD default. The undeclared root
            // attribute "ticks" is authentic vendor style (its own DTD omits it) — a grammar-undeclared-attribute
            // advisory, never a write blocker.
            ProductDefinition product = ProductDefinitionBuilder
                .S0Device("_0x9f07", "Synthetic S0 Meter Product")
                .Note("Synthetic oracle - energy meter (måler)")
                .Attribute("icon", "_0x99")
                .Attribute("ticks", "100")
                .AddResource("W", "Power now")
                .AddResource("kWh", "Energy total", r => r.Attribute("accessibility", "read-write"))
                .AddResource("resource_date", "Billing date",
                    r => r.Attribute("year", "2000").Attribute("month", "1").Attribute("day", "1"))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f07_meter.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f07_meter.def",
                "_0x01", "_0x02", "_0x03", "_0x04");
        }

        [Test]
        public void Author_9f08_OpenWorld_MatchesOracle()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f08", "Synthetic Open-World Product (åben)")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRootTiny6, OracleGrammars.SoilMoisture,
                }))
                .Note("Synthetic oracle - non-registry element type")
                .RawChild(ElRaw("resource_soil_moisture", "_0x02",
                    new[] { ("name", "Custom reading"), ("unit", "%"), ("inivalue", "42.00") }))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f08_openworld.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f08_openworld.def",
                "_0x01", "_0x02");
        }

        [Test]
        public void Author_9f09_Logging_WithOrphanAttlists_MatchesOracle()
        {
            // The "med logning" class: orphan ATTLIST declarations (a registry tag AND an invented non-registry
            // tag) whose defaults drive insert materialization; embedded typeid-keyed enum wired to a resource_enum.
            ProjectElement enumDefinition = El("enum_definition", 0x80, new[] { ("typeid", "_0x16") },
                El("enum_value", 0x81, new[] { ("typeid", "_0x17") }));
            string enumId = enumDefinition.GetAttribute("id")!;
            string valueId = enumDefinition.Children[0].GetAttribute("id")!;

            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f09", "11#Synthetic sensor med logning")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRoot("no"), OracleGrammars.ResourceTemperatureProduct,
                    OracleGrammars.ResourceEnumOrphan, OracleGrammars.SampleLogOrphan,
                }))
                .Note("Synthetic oracle - orphan ATTLIST defaults drive insert materialization (æøå)")
                .Attribute("icon", "_0x83")
                .RawChild(enumDefinition)
                .AddResource("resource_temperature", "Rumtemperatur",
                    r => r.Note("").Attribute("accessibility", "read"))
                .AddResource("resource_enum", "Log Rumtemperatur",
                    r => r.Attribute("typedef", enumId).Attribute("inivalue", valueId))
                .RawChild(ElRaw("resource_sample_log", "_0x904",
                    new[] { ("name", "Prøvekanal"), ("note", "Synthetic non-registry orphan type") }))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f09_logging.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f09_logging.def",
                "_0x01", "_0x50", "_0x51", "_0x02", "_0x03", "_0x04");
        }

        [Test]
        public void Author_9f10_Superset_WithDigitLeadingEnumTokens_MatchesOracle()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f10", "Synthetic superset produkt")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRootLean7, OracleGrammars.DatalineInputPulse,
                    OracleGrammars.DatalineOutput, OracleGrammars.ResourceTemperatureProduct,
                }))
                .Note("Synthetic oracle - superset DTD, undeclared body type, digit-leading enum tokens")
                .AddInput("Indgang", i => i.Note("Bruger pulse_width 48").Attribute("pulse_width", "48").Address("_0x1"))
                .AddResource("resource_input", "Alarm",
                    r => r.Note("Synthetic - type undeclared in own DTD").Attribute("accessibility", "read"))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f10_superset.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f10_superset.def",
                "_0x01", "_0x02", "_0x03");
        }

        [Test]
        public void Author_9f11_Quirks_ApostropheValues_MatchesOracle()
        {
            // The logical values carry literal apostrophes; the oracle file escapes them as &apos; (the 1.2.05
            // vendor class) — the D3 comparer forgiveness is what lets the byte assertion hold.
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f11", "Synthetic quirks produkt")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRootLean7, OracleGrammars.DatalineInput,
                }))
                .Note("Synthetic oracle - DOCTYPE space, tabs, trailing spaces, apostrophe entity")
                .AddInput("Føler'ens indgang", i => i.Note("PIR'en styrer lyset").Address("_0x1"))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f11_quirks.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f11_quirks.def",
                "_0x01", "_0x02");
        }

        [Test]
        public void Author_9f12_CaseSkew_MatchesOracle()
        {
            // The corpus's only case-insensitive tag-collision class: a mis-cased ELEMENT-only declaration beside
            // the orphan ATTLIST the body uses — ordinal tags throughout, or this grammar is unrepresentable.
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f12", "Synthetic caseskew produkt")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRootLean7, OracleGrammars.SkewElementOnly, OracleGrammars.SkewOrphan,
                }))
                .Note("Synthetic oracle - mis-cased ELEMENT beside orphan ATTLIST (ordinal tags)")
                .Attribute("icon", "_0x83")
                .RawChild(ElRaw("resource_skew", "_0x902",
                    new[] { ("name", "Skæv kanal"), ("note", "Små bogstaver i kroppen"), ("accessibility", "read") }))
                .Build();

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f12_caseskew.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f12_caseskew.def",
                "_0x01", "_0x02");
        }

        [Test]
        public void Author_9f13_Utf8NoBom_MatchesOracle()
        {
            // UTF-8 without BOM with a truthful encoding="UTF-8" prolog: the declared encoding is grammar DATA,
            // the physical encoding a definition datum — both must survive code authoring.
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9f13", "Synthetic UTF-8 uden BOM æøå")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRootLean7, OracleGrammars.DatalineInput,
                }, declaredEncoding: "UTF-8"))
                .Note("Synthetic oracle - UTF-8 without BOM, declared UTF-8 (ÆØÅ)")
                .AddInput("Indgang æble", i => i.Note("Grøn ledning").Address("_0x1").CableColour("Grøn"))
                .Build() with
            { SourceEncoding = CatalogTextEncoding.Utf8 };

            SyntheticOracle.AssertMatchesOracle(product.Body, Dir + "synthetic_9f13_utf8nobom.def");
            SyntheticOracle.AssertWritesOracleBytes(product, Dir + "synthetic_9f13_utf8nobom.def",
                "_0x01", "_0x02");
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

        // An element whose attribute list — INCLUDING the id's position — is given verbatim, for oracles whose
        // authentic attribute order puts another attribute before the id (e.g. the dimmer channel's icon).
        private static ProjectElement ElOrdered(string tag, (string Name, string Value)[] attrs,
            params ProjectElement[] children)
        {
            string? idToken = attrs.FirstOrDefault(a => a.Name == "id").Value;
            ElementId? id = ElementId.TryParse(idToken, out ElementId parsed) ? parsed : null;
            return new ProjectElement(tag, id, attrs.ToImmutableArray(), children.ToImmutableArray());
        }
    }
}
