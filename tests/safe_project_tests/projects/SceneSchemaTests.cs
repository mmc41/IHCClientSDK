namespace Ihc.Vis.Tests
{
    /// <summary>
    /// G1a — the scene-link schema enabler for US-024: the four scene membership types
    /// (<c>scene_link</c>/<c>scene_dimmer</c>/<c>scene_relay</c>/<c>scene_shutter</c>) carry their spec ch. 08 §8.6
    /// type codes and project-form canonical DTD blocks, so the authoring layer can allocate their ids and the
    /// emitter can declare them in a project whose own DTD lacks them (the vendor added exactly these blocks when
    /// the first scene link was wired into project3). The three types the committed <c>-scenelinks</c> oracle
    /// carries are pinned VERBATIM to the vendor's inline-DTD delta (the oracle wins over spec text);
    /// <c>scene_shutter</c> has no committed oracle (no jalousi product in project3) and is pinned to the spec
    /// §8.4.1 declaration — provisional until a capture uses it.
    /// </summary>
    public class SceneSchemaTests
    {
        private const string ScenelinksOracle = "project3-KompleksWired-scenelinks.vis";

        // ---- Type codes (spec §8.6; member ids in the oracle end 4d/4c, partner scene_link 4b) ----

        [TestCase("scene_link", 0x4b)]
        [TestCase("scene_dimmer", 0x4c)]
        [TestCase("scene_relay", 0x4d)]
        [TestCase("scene_shutter", 0x4e)]
        public void TypeCode_SceneTypes_MapPerSpec(string tag, int code) =>
            Assert.That(TypeCode.ForTag(tag), Is.EqualTo(code), tag);

        // ---- The §18 "G1 DTD delta" gate, kept as a regression: registry text == the vendor's generated block ----

        [TestCase("scene_relay")]
        [TestCase("scene_dimmer")]
        [TestCase("scene_link")]
        public async Task RegistryBlock_MatchesScenelinksOracleInlineDtd_Verbatim(string tag)
        {
            Project oracle = await new ProjectAppService(TestSetup.Settings)
                .Load("testdata/projects/" + ScenelinksOracle);

            Assert.That(oracle.InlineDtdBlocks.TryGetValue(tag, out string? vendorBlock), Is.True,
                $"the -scenelinks oracle declares '{tag}' in its inline DTD");
            Assert.That(ProjectSchemaRegistry.Get(tag).CanonicalDtdBlock, Is.EqualTo(vendorBlock),
                $"the registry's canonical '{tag}' block must be byte-what the vendor generated (oracle wins)");
        }

        // ---- scene_shutter: spec-derived project form (no oracle instance yet) ----

        [Test]
        public void SceneShutter_SpecDerivedBlock_DeclaresProjectFormAttrs()
        {
            ElementSchema schema = ProjectSchemaRegistry.Get("scene_shutter");

            Assert.Multiple(() =>
            {
                Assert.That(schema.Attrs.Select(a => a.Name),
                    Is.EqualTo(new[] { "id", "name", "shutter_position", "delay_ms", "note", "link", "udf" }),
                    "attribute order = spec §8.4.1 project DTD order");
                AttrSchema position = schema.FindAttr("shutter_position")!;
                Assert.That(position.EnumValues, Is.EqualTo(new[] { "up", "down" }), "enumerated, no percentage");
                Assert.That(position.Default, Is.EqualTo("up"));
                Assert.That(position.Kind, Is.EqualTo(AttrKind.Defaulted));
            });
        }

        // ---- The halves' back-reference is IDREF #REQUIRED on all four (drives cascade + idref-dangling) ----

        [TestCase("scene_link")]
        [TestCase("scene_dimmer")]
        [TestCase("scene_relay")]
        [TestCase("scene_shutter")]
        public void SceneTypes_LinkAttr_IsRequiredIdRef(string tag)
        {
            AttrSchema link = ProjectSchemaRegistry.Get(tag).FindAttr("link")!;

            Assert.Multiple(() =>
            {
                Assert.That(link.Render, Is.EqualTo(AttrRender.IdRef), $"{tag}@link participates in IDREF remap/cascade");
                Assert.That(link.Kind, Is.EqualTo(AttrKind.Required), $"{tag}@link is #REQUIRED");
            });
        }

        // ---- relay_value: project default is "off" (≠ the editor template's "on") → "on" is always written ----

        [Test]
        public void SceneRelay_RelayValue_IsEnumeratedDefaultOff()
        {
            AttrSchema relayValue = ProjectSchemaRegistry.Get("scene_relay").FindAttr("relay_value")!;

            Assert.Multiple(() =>
            {
                Assert.That(relayValue.EnumValues, Is.EqualTo(new[] { "on", "off" }));
                Assert.That(relayValue.Default, Is.EqualTo("off"),
                    "project-form default is off, so the oracle's relay_value=\"on\" is non-default and written");
                Assert.That(relayValue.OmitsOnWrite("off"), Is.True, "an off member row omits the attribute");
            });
        }
    }
}
