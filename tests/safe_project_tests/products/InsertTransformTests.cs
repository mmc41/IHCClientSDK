using System.Collections.Immutable;
using System.Globalization;
using CsCheck;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Deterministic (no install dir) tests for the catalog→project insert transform: fresh sequential ids with
    /// preserved type-code suffixes, intra-subtree IDREF remapping, <c>NN#</c> menu-prefix stripping, cross-DTD
    /// default materialization, and <c>enum_definition</c> hoisting with reference rewriting (spec ch. 09).
    /// </summary>
    public class InsertTransformTests
    {
        private static ProjectElement Node(string tag, string id, (string, string)[] attrs, ProjectElement[] children)
        {
            ElementId.TryParse(id, out ElementId parsed);
            var bag = ImmutableArray.CreateBuilder<(string, string)>();
            bag.Add(("id", id));
            bag.AddRange(attrs);
            return new ProjectElement(tag, parsed, bag.ToImmutable(), children.ToImmutableArray());
        }

        private static ProjectElement EmptyEnumDefinitions() =>
            new("enum_definitions", new ElementId(0x30, 0x46),
                ImmutableArray.Create(("id", "_0x3046")), ImmutableArray<ProjectElement>.Empty);

        [Test]
        public void Insert_ReallocatesIds_RemapsSceneRef_StripsPrefix_MaterializesDefaults()
        {
            // A Lampeudtag-like catalog body (effective values, as CatalogReader would yield with DTD defaults).
            ProjectElement body = Node("product_dataline", "_0x01",
                new[] { ("product_identifier", "_0x2202"), ("name", "01#Lampeudtag"), ("locked", "yes"), ("icon", "_0x86") },
                new[]
                {
                    Node("dataline_output", "_0x02", new[] { ("name", "Udgang"), ("backup", "yes") }, System.Array.Empty<ProjectElement>()),
                    Node("scenes", "_0x03", new[] { ("name", "Scenarier"), ("scene_resource", "_0x02") }, System.Array.Empty<ProjectElement>()),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);
            ProjectElement root = result.InsertedRoot;
            ProjectElement output = root.FindChild("dataline_output")!;
            ProjectElement scenes = root.FindChild("scenes")!;

            Assert.Multiple(() =>
            {
                Assert.That(root.GetAttribute("id"), Is.EqualTo("_0x5153"), "product_dataline suffix 0x53");
                Assert.That(output.GetAttribute("id"), Is.EqualTo("_0x525b"), "dataline_output suffix 0x5b");
                Assert.That(scenes.GetAttribute("id"), Is.EqualTo("_0x5349"), "scenes suffix 0x49");
                Assert.That(scenes.GetAttribute("scene_resource"), Is.EqualTo("_0x525b"), "scene_resource remapped to new output id");
                Assert.That(root.GetAttribute("name"), Is.EqualTo("Lampeudtag"), "NN# prefix stripped");
                Assert.That(root.GetAttribute("locked"), Is.EqualTo("yes"), "materialized vs project default 'no'");
                Assert.That(output.GetAttribute("backup"), Is.EqualTo("yes"), "materialized vs project default 'no'");
                Assert.That(allocator.LastUniqueIdToken, Is.EqualTo("_0x53"));
            });
        }

        [Test]
        public void Insert_StripsLeadingZerosFromOpaqueTokens()
        {
            // Airlink/exotic .def templates author some opaque _0x tokens with a leading zero (e.g.
            // device_type="_0x080a"); IHC Visual re-emits every _0x token in canonical minimal-width hex, so the
            // insert transform strips the zeros (here on product_identifier, the same NormalizeTokens path).
            ProjectElement body = Node("product_dataline", "_0x01",
                new[] { ("product_identifier", "_0x02202"), ("name", "Lampeudtag"), ("icon", "_0x86") },
                System.Array.Empty<ProjectElement>());

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);

            Assert.That(result.InsertedRoot.GetAttribute("product_identifier"), Is.EqualTo("_0x2202"),
                "opaque _0x tokens are canonicalized to minimal-width hex (leading zeros stripped) on insert");
        }

        [Test]
        public void Insert_CanonicalizesEnumTokenPunctuation_ThenElidesAtDefault()
        {
            // product2315.def authors an s0 kWh's accessibility as the typo "readwrite"; the canonical DTD token is
            // "read-write" (also the kWh project default), so the insert normalizes it and Canonicalize then elides it.
            ProjectElement body = Node("kWh", "_0x06",
                new[] { ("name", "Consumption"), ("accessibility", "readwrite") },
                System.Array.Empty<ProjectElement>());

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);

            Assert.That(result.InsertedRoot.GetAttribute("accessibility"), Is.Null,
                "the typo'd 'readwrite' is canonicalized to the default token 'read-write' and elided on save");
        }

        [Test]
        public void Insert_StampsNullSerialNumberOnAirlinkProduct()
        {
            // An airlink .def leaves serialnumber at its own DTD default ""; the attribute is #REQUIRED in the project
            // DTD, so IHC Visual stamps the null token "_0x0" on every fresh (unpaired) airlink insert.
            ProjectElement body = Node("product_airlink", "_0x01",
                new[] { ("product_identifier", "_0x4306"), ("device_type", "_0x80a"), ("name", "Dimmer Universal"),
                        ("serialnumber", ""), ("icon", "_0x86") },
                System.Array.Empty<ProjectElement>());

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);

            Assert.That(result.InsertedRoot.GetAttribute("serialnumber"), Is.EqualTo("_0x0"),
                "airlink insert stamps the null serialnumber token");
        }

        [Test]
        public void Insert_StampsEmptyRequiredTokens_ButPreservesFilledSiblings()
        {
            // The schema-derived null-token rule (no per-type table): an rs485 LED-dimmer channel's channel_id is
            // #REQUIRED in the project DTD but left empty by the .def, so it is stamped "_0x0"; the sibling channel is
            // #REQUIRED AND filled by the .def ("_0x1"), so it is preserved verbatim. The discriminator is emptiness,
            // not the attribute name — so any future #REQUIRED-yet-empty token attribute is handled the same way.
            ProjectElement body = Node("rs485_led_dimmer_channel", "_0x01",
                new[] { ("product_identifier", "_0x4409"), ("name", "Kanal 1"), ("channel", "_0x1"), ("channel_id", "") },
                System.Array.Empty<ProjectElement>());

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);

            Assert.Multiple(() =>
            {
                Assert.That(result.InsertedRoot.GetAttribute("channel_id"), Is.EqualTo("_0x0"), "empty #REQUIRED channel_id stamped with the null token");
                Assert.That(result.InsertedRoot.GetAttribute("channel"), Is.EqualTo("_0x1"), "filled #REQUIRED channel preserved verbatim");
            });
        }

        [Test]
        public void Insert_HoistsUserEnum_AndRewritesResourceEnumReferences()
        {
            // A function-block-like body carrying a user enum (no typeid) referenced by a resource_enum.
            ProjectElement body = Node("functionblock", "_0x01",
                new[] { ("name", "Block"), ("master_type", "9.9.99") },
                new[]
                {
                    Node("enum_definition", "_0x10", new[] { ("name", "Mode") }, new[]
                    {
                        Node("enum_value", "_0x11", new[] { ("name", "A") }, System.Array.Empty<ProjectElement>()),
                    }),
                    Node("settings", "_0x20", new[] { ("name", "Settings") }, new[]
                    {
                        Node("resource_enum", "_0x21", new[] { ("name", "Tilstand"), ("typedef", "_0x10"), ("inivalue", "_0x11") }, System.Array.Empty<ProjectElement>()),
                    }),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);

            ProjectElement hoistedDef = result.EnumDefinitions.Children[0];
            ProjectElement resourceEnum = result.InsertedRoot.FindChild("settings")!.FindChild("resource_enum")!;

            Assert.Multiple(() =>
            {
                Assert.That(result.EnumDefinitions.Children, Has.Length.EqualTo(1), "user enum hoisted to project container");
                Assert.That(result.InsertedRoot.FindChild("enum_definition"), Is.Null, "enum removed from the inserted subtree");
                Assert.That(resourceEnum.GetAttribute("typedef"), Is.EqualTo(hoistedDef.GetAttribute("id")), "typedef rewired to hoisted def");
                Assert.That(resourceEnum.GetAttribute("inivalue"), Is.EqualTo(hoistedDef.Children[0].GetAttribute("id")), "inivalue rewired to hoisted value");
            });
        }

        // R-enum (B3): a catalog component may embed an enum_definition that DUPLICATES one already in the project
        // (a seed global or a prior insert's hoist). IHC Visual allocates the duplicate's def+value ids in document
        // order but immediately DISCARDS them — advancing the counter (a permanent hole) — and rewires every
        // referencing resource_enum to the pre-existing definition. Match key: typeid when present (& non-zero),
        // else name. Value mapping inside a matched def: by typeid when present, else by name. The three tests below
        // pin the two dedup branches (typeid / name) plus the no-match guard; case (d) — value mapping by typeid vs
        // name — is asserted within (a) and (b) respectively.

        // (a) product2125 shape: a NAMELESS enum carrying typeid _0x16 dedups against the seed "Logning" (typeid
        // _0x16). Burns def+value ids, rewires the resource_enum to the seed def and (by value typeid) the seed value.
        [Test]
        public void Insert_ProductEnumMatchingSeedByTypeid_BurnsIdsAndRewiresToSeed()
        {
            ProjectElement seed = SeededEnumDefinitions(
                Def("_0x4747", typeid: "_0x16", name: "Logning",
                    Val("_0x4848", typeid: "_0x41", name: "Off"),
                    Val("_0x4948", typeid: "_0x42", name: "Kun ændringer")));

            ProjectElement body = Node("product_dataline", "_0x01",
                new[] { ("product_identifier", "_0x2125"), ("name", "Temperatur sensor med logning") },
                new[]
                {
                    // nameless, typeid'd embedded enum with a single value (the product2125.def shape)
                    Node("enum_definition", "_0x50", new[] { ("typeid", "_0x16") }, new[]
                    {
                        Node("enum_value", "_0x51", new[] { ("typeid", "_0x42"), ("name", "Kun ændringer") }, System.Array.Empty<ProjectElement>()),
                    }),
                    Node("settings", "_0x60", new[] { ("name", "Indstillinger") }, new[]
                    {
                        Node("resource_enum", "_0x61", new[] { ("name", "Log"), ("typedef", "_0x50"), ("inivalue", "_0x51") }, System.Array.Empty<ProjectElement>()),
                    }),
                });

            var allocator = new IdAllocator(0x80);
            InsertResult result = InsertTransform.Insert(body, allocator, seed, ProjectSchemaView.RegistryOnly);
            ProjectElement resourceEnum = result.InsertedRoot.FindChild("settings")!.FindChild("resource_enum")!;

            Assert.Multiple(() =>
            {
                Assert.That(result.EnumDefinitions.Children, Has.Length.EqualTo(1), "no new def — embedded enum dedup'd to the seed");
                // counter: product_dataline 0x81, [burn def 0x82, burn value 0x83], settings 0x84, resource_enum 0x85
                Assert.That(allocator.Counter, Is.EqualTo(0x85), "def+value ids burned (0x82,0x83) leaving a permanent hole");
                Assert.That(resourceEnum.GetAttribute("typedef"), Is.EqualTo("_0x4747"), "typedef rewired to the seed def");
                Assert.That(resourceEnum.GetAttribute("inivalue"), Is.EqualTo("_0x4948"), "inivalue rewired to seed value by typeid _0x42");
            });
        }

        // (b) PIR shape: a NAMED enum with no typeid dedups by name against an existing project def. Burns def+values,
        // rewires the resource_enum to the existing def and (by value name) the existing value.
        [Test]
        public void Insert_FunctionBlockEnumMatchingExistingByName_BurnsIdsAndRewiresToExisting()
        {
            ProjectElement seed = SeededEnumDefinitions(
                Def("_0x4047", typeid: null, name: "PIR funktion",
                    Val("_0x4148", typeid: null, name: "Fra"),
                    Val("_0x4248", typeid: null, name: "Til")));

            ProjectElement body = Node("functionblock", "_0x01",
                new[] { ("name", "Block"), ("master_type", "1.4.02") },
                new[]
                {
                    Node("enum_definition", "_0x10", new[] { ("name", "PIR funktion") }, new[]
                    {
                        Node("enum_value", "_0x11", new[] { ("name", "Fra") }, System.Array.Empty<ProjectElement>()),
                        Node("enum_value", "_0x12", new[] { ("name", "Til") }, System.Array.Empty<ProjectElement>()),
                    }),
                    Node("settings", "_0x20", new[] { ("name", "S") }, new[]
                    {
                        Node("resource_enum", "_0x21", new[] { ("name", "T"), ("typedef", "_0x10"), ("inivalue", "_0x12") }, System.Array.Empty<ProjectElement>()),
                    }),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, seed, ProjectSchemaView.RegistryOnly);
            ProjectElement resourceEnum = result.InsertedRoot.FindChild("settings")!.FindChild("resource_enum")!;

            Assert.Multiple(() =>
            {
                Assert.That(result.EnumDefinitions.Children, Has.Length.EqualTo(1), "no new def — user enum dedup'd by name");
                // counter: functionblock 0x51, [burn def 0x52, values 0x53,0x54], settings 0x55, resource_enum 0x56
                Assert.That(allocator.Counter, Is.EqualTo(0x56), "def+2 values burned (0x52,0x53,0x54) leaving a permanent hole");
                Assert.That(resourceEnum.GetAttribute("typedef"), Is.EqualTo("_0x4047"), "typedef rewired to the existing def by name");
                Assert.That(resourceEnum.GetAttribute("inivalue"), Is.EqualTo("_0x4248"), "inivalue rewired to existing value 'Til' by name");
            });
        }

        // (c) no-match guard: a user enum whose name matches nothing in a NON-empty container must still hoist fresh
        // (no over-dedup). Ids identical to today's hoist-fresh path. Stays green pre- and post-implementation.
        [Test]
        public void Insert_EnumWithNoNameOrTypeidMatch_HoistsFreshAlongsideExisting()
        {
            ProjectElement seed = SeededEnumDefinitions(
                Def("_0x4047", typeid: null, name: "Persienne tilstand",
                    Val("_0x4148", typeid: null, name: "Oppe")));

            ProjectElement body = Node("functionblock", "_0x01",
                new[] { ("name", "Block") },
                new[]
                {
                    Node("enum_definition", "_0x10", new[] { ("name", "Helt andet") }, new[]
                    {
                        Node("enum_value", "_0x11", new[] { ("name", "X") }, System.Array.Empty<ProjectElement>()),
                    }),
                    Node("settings", "_0x20", new[] { ("name", "S") }, new[]
                    {
                        Node("resource_enum", "_0x21", new[] { ("typedef", "_0x10"), ("inivalue", "_0x11") }, System.Array.Empty<ProjectElement>()),
                    }),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, seed, ProjectSchemaView.RegistryOnly);
            ProjectElement hoisted = result.EnumDefinitions.Children[1];
            ProjectElement resourceEnum = result.InsertedRoot.FindChild("settings")!.FindChild("resource_enum")!;

            Assert.Multiple(() =>
            {
                Assert.That(result.EnumDefinitions.Children, Has.Length.EqualTo(2), "unmatched enum hoisted alongside the existing one");
                Assert.That(resourceEnum.GetAttribute("typedef"), Is.EqualTo(hoisted.GetAttribute("id")), "typedef rewired to the freshly hoisted def");
                Assert.That(hoisted.GetAttribute("name"), Is.EqualTo("Helt andet"));
            });
        }

        // On catalog insert IHC Visual stamps a per-resource-type GUI icon on every resource (resource_input -> _0x36,
        // resource_output -> _0x39, ...). Function-block .ifb templates bake these in, but product .def templates omit
        // them (icon defaults to _0x0), so the insert path must stamp them for byte-fidelity. Types with no canonical
        // icon (e.g. resource_temperature) keep the default _0x0 (elided). Table verified conflict-free across every oracle.
        [Test]
        public void Insert_StampsCanonicalResourceIcons_ForProductTemplateResources()
        {
            ProjectElement body = Node("product_dataline", "_0x01",
                new[] { ("product_identifier", "_0x2125"), ("name", "Temp") },
                new[]
                {
                    Node("resource_temperature", "_0x02", new[] { ("name", "Rumtemperatur") }, System.Array.Empty<ProjectElement>()),
                    Node("resource_input", "_0x03", new[] { ("name", "Alarm") }, System.Array.Empty<ProjectElement>()),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);
            ProjectElement input = result.InsertedRoot.FindChild("resource_input")!;
            ProjectElement temp = result.InsertedRoot.FindChild("resource_temperature")!;

            Assert.Multiple(() =>
            {
                Assert.That(input.GetAttribute("icon"), Is.EqualTo("_0x36"), "resource_input gets its canonical icon stamped on insert");
                Assert.That(temp.GetAttribute("icon"), Is.Null.Or.EqualTo("_0x0"), "resource_temperature has no canonical icon (default _0x0, elided)");
            });
        }

        // On insert IHC Visual reconciles a catalog template's numeric precision with the PROJECT's: resource_light's
        // project inivalue default is "0" (integer) so a catalog value "500.00" is re-emitted as "500", while
        // resource_temperature's default "0.00" keeps 2 decimals so "20.00" is preserved. (B3 step06 / product2139.)
        [Test]
        public void Insert_NormalizesNumericAttr_ToProjectDefaultPrecision()
        {
            ProjectElement body = Node("product_dataline", "_0x01",
                new[] { ("product_identifier", "_0x2139"), ("name", "Lux") },
                new[]
                {
                    Node("resource_light", "_0x02", new[] { ("name", "Lys"), ("inivalue", "500.00") }, System.Array.Empty<ProjectElement>()),
                    Node("resource_temperature", "_0x03", new[] { ("name", "Temp"), ("inivalue", "20.00") }, System.Array.Empty<ProjectElement>()),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);
            ProjectElement light = result.InsertedRoot.FindChild("resource_light")!;
            ProjectElement temp = result.InsertedRoot.FindChild("resource_temperature")!;

            Assert.Multiple(() =>
            {
                Assert.That(light.GetAttribute("inivalue"), Is.EqualTo("500"), "resource_light inivalue reformatted to project precision (0 decimals)");
                Assert.That(temp.GetAttribute("inivalue"), Is.EqualTo("20.00"), "resource_temperature inivalue keeps project precision (2 decimals)");
            });
        }

        [Test]
        public void Insert_DoesNotRoundAuthoredNumericValue()
        {
            // Finding 3: resource_light's project inivalue default is "0" (0 decimals). Reformatting "12.5" to 0
            // places ROUNDS it ("13"), silently mutating authored data. Only value-preserving zero-trim/pad may be
            // applied — a value that would round must be left verbatim.
            ProjectElement body = Node("product_dataline", "_0x01",
                new[] { ("product_identifier", "_0x2139"), ("name", "Lux") },
                new[]
                {
                    Node("resource_light", "_0x02", new[] { ("name", "Lys"), ("inivalue", "12.5") }, System.Array.Empty<ProjectElement>()),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);
            ProjectElement light = result.InsertedRoot.FindChild("resource_light")!;

            Assert.That(light.GetAttribute("inivalue"), Is.EqualTo("12.5"),
                "a value that would round is preserved verbatim, never mutated to 13");
        }

        /// <summary>The two authored-value cases above are single points on one law: reconciling precision may
        /// change the TEXT but never the NUMBER. Either the value is re-emitted at the project's places (trailing
        /// zeros padded or trimmed, which preserves it) or it is left exactly as authored (which also preserves
        /// it); there is no third outcome, and in particular no rounding.
        /// <para>The place counts are the registry's own: every fixed-point default in the project schema is
        /// either <c>"0"</c> (resource_light and the rest of the integer family) or <c>"0.00"</c> (the seven
        /// decimal-family attributes), so those two are what a generator can vary over. The authored text varies
        /// far more widely — sign, leading zeros, and zero to six fraction digits, which is what reaches both the
        /// reformat branch and the leave-verbatim branch.</para></summary>
        private static readonly Gen<(string Tag, string Text)> AuthoredNumericAttr = Gen.Select(
            Gen.OneOfConst("resource_light", "resource_temperature"),
            Gen.Bool, Gen.Int[0, 3], Gen.Long[0, 99_999_999], Gen.Int[0, 6], Gen.Long[0, 999_999],
            (tag, negative, pad, whole, places, fraction) =>
                (tag, ComposeNumber(negative, pad, whole, places, fraction)));

        private static readonly long[] PowersOfTen = { 1, 10, 100, 1_000, 10_000, 100_000, 1_000_000 };

        private static string ComposeNumber(bool negative, int pad, long whole, int places, long fraction)
        {
            string digits = places == 0
                ? string.Empty
                : "." + (fraction % PowersOfTen[places]).ToString(CultureInfo.InvariantCulture).PadLeft(places, '0');
            return (negative ? "-" : string.Empty) + new string('0', pad)
                + whole.ToString(CultureInfo.InvariantCulture) + digits;
        }

        [Test]
        public void Insert_PrecisionReconciliation_NeverChangesTheNumericValue()
        {
            const NumberStyles Style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            int rewrites = 0;

            AuthoredNumericAttr.Sample(sample =>
            {
                ProjectElement body = Node("product_dataline", "_0x01",
                    new[] { ("product_identifier", "_0x2139"), ("name", "Lux") },
                    new[]
                    {
                        Node(sample.Tag, "_0x02", new[] { ("name", "R"), ("inivalue", sample.Text) },
                            System.Array.Empty<ProjectElement>()),
                    });

                InsertResult result = InsertTransform.Insert(
                    body, new IdAllocator(0x50), EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);
                string? written = result.InsertedRoot.FindChild(sample.Tag)!.GetAttribute("inivalue");
                if (written != sample.Text)
                {
                    rewrites++;
                }

                decimal authored = decimal.Parse(sample.Text, Style, CultureInfo.InvariantCulture);
                return written is not null
                       && decimal.TryParse(written, Style, CultureInfo.InvariantCulture, out decimal stored)
                       && stored == authored;
            }, iter: 500, threads: 1);

            Assert.That(rewrites, Is.GreaterThan(0),
                "negative control: the law must hold because reformatting preserves the value, not because the "
                + "transform never reformats anything");
        }

        private static ProjectElement SeededEnumDefinitions(params ProjectElement[] defs) =>
            new("enum_definitions", new ElementId(0x30, 0x46),
                ImmutableArray.Create(("id", "_0x3046")), defs.ToImmutableArray());

        private static ProjectElement Def(string id, string? typeid, string name, params ProjectElement[] values) =>
            Node("enum_definition", id,
                typeid is null ? new[] { ("name", name) } : new[] { ("name", name), ("typeid", typeid) },
                values);

        private static ProjectElement Val(string id, string? typeid, string name) =>
            Node("enum_value", id,
                typeid is null ? new[] { ("name", name) } : new[] { ("name", name), ("typeid", typeid) },
                System.Array.Empty<ProjectElement>());
    }
}
