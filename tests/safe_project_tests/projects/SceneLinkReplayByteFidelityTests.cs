namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The scene-link authoring byte-fidelity gate for <see cref="ProjectEditor.LinkScene"/> against the authentic
    /// vendor oracle <c>project3-KompleksWired-scenelinks.vis</c> (IHC Visual 03.04.72.03 after two recorded
    /// scenario-link drags on <c>project3-KompleksWired.vis</c>, single save). The SDK loads the original,
    /// reproduces the vendor's one-time load-time enum re-hoist (<see cref="ProjectEditor.NormalizeCatalogEnums"/> —
    /// Action 0), replays the two links in allocation order — R: FB scene pin "Scenarie Sluk" (<c>_0x974a</c>) onto
    /// Lampeudtag's scenes (<c>_0x5649</c>), relay ON; D: FB scene pin "Scenarie Sluk" (<c>_0x4814a</c>) onto the
    /// airlink dimmer's scenes (<c>_0x8349</c>), dimmer 100 % / 1000 ms (the scenario dialog's defaults) — then
    /// restamps to the oracle's clock and asserts byte-identity. Pinned vendor semantics (ENG-A2): allocation is
    /// member-first in gesture order (+4 ids, no burn); the member row carries the values inside the product's
    /// scenes container; the <c>scene_link</c> carries <c>icon="_0x47"</c> inside the FB's <c>resource_scene</c>;
    /// the halves cross-reference via <c>link</c>. Both verbs are catalog-free, so these run unconditionally.
    /// </summary>
    public class SceneLinkReplayByteFidelityTests
    {
        private const string Original = "project3-KompleksWired.vis";
        private const string SceneLinksOracle = "project3-KompleksWired-scenelinks.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- Full replay: Action 0 → R (relay link) → D (dimmer link) → byte-identity ----

        [Test]
        public async Task LinkScenes_ReplaysProject3SceneOracle_ByteIdentical()
        {
            byte[] expected = TestData.ReadBytes("projects/" + SceneLinksOracle);
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();                                              // Action 0: _0x56c -> _0x579
            (ResourceRef relayPin, ScenesRef relayTarget) = ResolveRelayEndpoints(editor);
            editor.LinkScene(relayPin, relayTarget, SceneValue.Relay(on: true));         // R: _0x57a4d + _0x57b4b
            (ResourceRef dimmerPin, ScenesRef dimmerTarget) = ResolveDimmerEndpoints(editor);
            editor.LinkScene(dimmerPin, dimmerTarget,
                SceneValue.Dimmer(100, TimeSpan.FromMilliseconds(1000)));                // D: _0x57c4c + _0x57d4b

            // id2=_0xb0d1a0b decodes to day 11 / hour 13 / min 26 / sec 11; <modified> is minute-precision (13:26),
            // so the second (11) lives only in id2 and must be supplied to the restamp clock.
            Project stamped = MetadataStamper.Restamp(editor.ToProject(),
                new DateTimeOffset(2026, 7, 11, 13, 26, 11, TimeSpan.Zero));
            using var ms = new MemoryStream();
            await app.Save(stamped, ms, ProjectSaveOptions.PreserveExistingMetadata);

            TestData.AssertBytesIdentical(expected, ms.ToArray(), "scene-link replay → " + SceneLinksOracle);
        }

        // ---- Composition isolation: member-first allocation in gesture order, pinned tokens, no burn ----

        [Test]
        public async Task LinkScene_AllocatesMemberFirstContiguously_NoBurn()
        {
            Project original = await new ProjectAppService(Settings).Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();
            (ResourceRef relayPin, ScenesRef relayTarget) = ResolveRelayEndpoints(editor);
            (ResourceRef dimmerPin, ScenesRef dimmerTarget) = ResolveDimmerEndpoints(editor);
            Assert.Multiple(() =>
            {
                Assert.That(relayPin.Id!.Value.ToToken(), Is.EqualTo("_0x974a"), "relay pin = pinned oracle endpoint");
                Assert.That(relayTarget.Id.ToToken(), Is.EqualTo("_0x5649"), "relay target = pinned oracle endpoint");
                Assert.That(dimmerPin.Id!.Value.ToToken(), Is.EqualTo("_0x4814a"), "dimmer pin = pinned oracle endpoint");
                Assert.That(dimmerTarget.Id.ToToken(), Is.EqualTo("_0x8349"), "dimmer target = pinned oracle endpoint");
            });

            editor.LinkScene(relayPin, relayTarget, SceneValue.Relay(on: true));
            editor.LinkScene(dimmerPin, dimmerTarget, SceneValue.Dimmer(100, TimeSpan.FromMilliseconds(1000)));
            Project after = editor.ToProject();

            ProjectElement relayMember = after.Root.Descendants().Single(e => e.Tag == "scene_relay");
            ProjectElement dimmerMember = after.Root.Descendants().Single(e => e.Tag == "scene_dimmer");
            Assert.Multiple(() =>
            {
                Assert.That(relayMember.Id!.Value.ToToken(), Is.EqualTo("_0x57a4d"), "member allocated first (R)");
                Assert.That(relayMember.GetAttribute("link"), Is.EqualTo("_0x57b4b"), "member -> scene_link");
                Assert.That(relayMember.GetAttribute("relay_value"), Is.EqualTo("on"), "relay value");
                Assert.That(after.FindParent(relayMember.Id!.Value)!.Id!.Value.ToToken(), Is.EqualTo("_0x5649"),
                    "relay member lives inside the product's scenes container");

                Assert.That(dimmerMember.Id!.Value.ToToken(), Is.EqualTo("_0x57c4c"), "member allocated first (D)");
                Assert.That(dimmerMember.GetAttribute("link"), Is.EqualTo("_0x57d4b"), "member -> scene_link");
                Assert.That(dimmerMember.GetAttribute("dimming_value"), Is.EqualTo("100"), "dimmer level");
                Assert.That(dimmerMember.GetAttribute("ramptime_ms"), Is.EqualTo("1000"), "dimmer ramp time");
                Assert.That(after.FindParent(dimmerMember.Id!.Value)!.Id!.Value.ToToken(), Is.EqualTo("_0x8349"),
                    "dimmer member lives inside the product's scenes container");

                Assert.That(after.LastUniqueId, Is.EqualTo("_0x57d"), "+4 ids member-first, no burn");
            });
            foreach ((string linkToken, string memberToken, string pinToken) in new[]
                     { ("_0x57b4b", "_0x57a4d", "_0x974a"), ("_0x57d4b", "_0x57c4c", "_0x4814a") })
            {
                ProjectElement sceneLink = after.Root.Descendants().Single(e => e.GetAttribute("id") == linkToken);
                Assert.Multiple(() =>
                {
                    Assert.That(sceneLink.Tag, Is.EqualTo("scene_link"), $"{linkToken} partner row type");
                    Assert.That(sceneLink.GetAttribute("name"), Is.EqualTo("Scenarie link"), $"{linkToken} name");
                    Assert.That(sceneLink.GetAttribute("icon"), Is.EqualTo("_0x47"), $"{linkToken} icon");
                    Assert.That(sceneLink.GetAttribute("link"), Is.EqualTo(memberToken), $"{linkToken} -> member");
                    Assert.That(after.FindParent(sceneLink.Id!.Value)!.Id!.Value.ToToken(), Is.EqualTo(pinToken),
                        $"{linkToken} lives inside the FB's resource_scene pin");
                });
            }
        }

        // ---- Validator interplay: both memberships wired → clean (the scene-bijection positive) ----

        [Test]
        public async Task LinkedScenes_ValidateClean()
        {
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            (ResourceRef relayPin, ScenesRef relayTarget) = ResolveRelayEndpoints(editor);
            editor.LinkScene(relayPin, relayTarget, SceneValue.Relay(on: true));
            (ResourceRef dimmerPin, ScenesRef dimmerTarget) = ResolveDimmerEndpoints(editor);
            editor.LinkScene(dimmerPin, dimmerTarget, SceneValue.Dimmer(100, TimeSpan.FromMilliseconds(1000)));

            ProjectValidationResult validation = app.Validate(editor.ToProject());

            Assert.That(validation.IsValid, Is.True,
                "freshly wired scene memberships are reciprocal and validate clean; errors: "
                + string.Join(" | ", validation.Errors));
        }

        // ---- Validator interplay: hand-broken reciprocity → scene-bijection error ----

        [Test]
        public async Task SceneMemberRetargeted_FailsSceneBijectionValidation()
        {
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            (ResourceRef relayPin, ScenesRef relayTarget) = ResolveRelayEndpoints(editor);
            editor.LinkScene(relayPin, relayTarget, SceneValue.Relay(on: true));
            (ResourceRef dimmerPin, ScenesRef dimmerTarget) = ResolveDimmerEndpoints(editor);
            editor.LinkScene(dimmerPin, dimmerTarget, SceneValue.Dimmer(100, TimeSpan.FromMilliseconds(1000)));

            // Break: repoint the relay member at the dimmer pair's scene_link — both rows exist, reciprocity broken.
            Project linked = editor.ToProject();
            ProjectElement relayMember = linked.Root.Descendants().Single(e => e.Tag == "scene_relay");
            string dimmerLinkToken = linked.Root.Descendants().Single(e => e.Tag == "scene_dimmer").GetAttribute("link")!;
            Assert.That(editor.TryResolve(relayMember.Id!.Value, out ElementRef? handle), Is.True);
            handle!.SetAttribute("link", dimmerLinkToken);

            ProjectValidationResult validation = app.Validate(editor.ToProject());

            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False, "broken scene reciprocity must not validate");
                Assert.That(validation.Findings.Any(f => f.RuleId == "scene-bijection"), Is.True,
                    "the break is reported by the scene-bijection rule; findings: "
                    + string.Join(" | ", validation.Findings));
            });
        }

        // ---- Delete cascade, FB direction: deleting the scene pin cascades the member row ----

        // Every stock scene pin in project3 is fired by its own block's program ("Fremkald %P" actions), and the
        // Strict delete policy refuses to orphan those references (program-row cascade is the deferred M-B item) —
        // so the pin under deletion is a freshly authored one on the custom "Tom blok" (no program references it),
        // which also exercises LinkScene over an in-session allocated pin.
        [Test]
        public async Task DeleteScenePin_CascadesMemberRowFromProductScenes()
        {
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);
            string room = original.FindParent(original.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Tom blok").Id!.Value)!
                .GetAttribute("name")!;

            ProjectEditor editor = original.Edit();
            ResourceRef pin = editor.Group(room).FunctionBlock("Tom blok").AddOutput("resource_scene", "Cascade probe");
            (ResourceRef _, ScenesRef relayTarget) = ResolveRelayEndpoints(editor);
            editor.LinkScene(pin, relayTarget, SceneValue.Relay(on: true));
            editor.DeleteById(pin.Id!.Value);                          // the scene_link half goes with the pin subtree
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(after.Root.Descendants().Any(e => e.Tag == "scene_relay"), Is.False,
                    "the member row inside the product's scenes container is cascaded");
                Assert.That(after.Root.Descendants().Any(e => e.Tag == "scene_link"), Is.False,
                    "the scene_link went with the deleted pin");
                Assert.That(after.Root.Descendants().Any(e => e.GetAttribute("id") == "_0x5649"), Is.True,
                    "the scenes container itself survives");
                Assert.That(app.Validate(after).IsValid, Is.True, "no dangling reference remains");
            });
        }

        // ---- Delete cascade, product direction: deleting the product cascades the scene_link off the pin ----

        [Test]
        public async Task DeleteProduct_CascadesSceneLinkFromFbPin()
        {
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            (ResourceRef relayPin, ScenesRef relayTarget) = ResolveRelayEndpoints(editor);
            editor.LinkScene(relayPin, relayTarget, SceneValue.Relay(on: true));
            editor.DeleteById(Id("_0x5453"));                          // Lampeudtag; member + scenes go with it
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(after.Root.Descendants().Any(e => e.Tag == "scene_link"), Is.False,
                    "the scene_link inside the FB pin is cascaded");
                Assert.That(after.Root.Descendants().Any(e => e.GetAttribute("id") == "_0x974a"), Is.True,
                    "the FB scene pin itself survives");
                Assert.That(app.Validate(after).IsValid, Is.True, "no dangling reference remains");
            });
        }

        // ----- helpers -----

        // R endpoints: FB "1.1.01.e. Kip tænd sluk" pin "Scenarie Sluk" (_0x974a) → dataline Lampeudtag's
        // "Scenarier" (_0x5649), both in the room 'Stue & Køkken "åben"'.
        private static (ResourceRef Pin, ScenesRef Target) ResolveRelayEndpoints(ProjectEditor editor)
        {
            GroupRef room = editor.Group("Stue & Køkken \"åben\"");
            ResourceRef pin = room.FunctionBlock("1.1.01.e. Kip tænd sluk").SceneOutput("Scenarie Sluk");
            return (pin, room.Product("Lampeudtag").Scenes());
        }

        // D endpoints: FB '4.1.01. AND ("Og"- blok)' (room "Værelse") pin "Scenarie Sluk" (_0x4814a) → airlink
        // "Dimmer Universal" (room "Soveværelse") "Scenarier/regulering" (_0x8349).
        private static (ResourceRef Pin, ScenesRef Target) ResolveDimmerEndpoints(ProjectEditor editor)
        {
            ResourceRef pin = editor.Group("Værelse").FunctionBlock("4.1.01. AND (\"Og\"- blok)")
                .SceneOutput("Scenarie Sluk");
            return (pin, editor.Group("Soveværelse").Product("Dimmer Universal").Scenes());
        }

        private static ElementId Id(string token) =>
            ElementId.TryParse(token, out ElementId id) ? id : throw new ArgumentException($"Bad id token: {token}");
    }
}
