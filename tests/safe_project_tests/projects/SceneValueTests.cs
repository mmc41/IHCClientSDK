using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// US-024/US-058: the scene-value round-trip on the SDK <see cref="SceneValue"/> (typed read via
    /// <see cref="SceneValue.TryParse"/>) and the in-place edit via <see cref="ProjectEditor.SetSceneValue"/>.
    /// The read is tolerant (previously-viewable projects still render); the edit preserves the member row's
    /// identity/link and is guarded against a kind mismatch.
    /// </summary>
    public class SceneValueTests
    {
        private const string Original = "project3-KompleksWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        // ----- encode → TryParse round-trip per kind -----

        [Test]
        public void TryParse_RelayRoundTrip()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TryParse(SceneValue.Relay(true)).On, Is.True);
                Assert.That(TryParse(SceneValue.Relay(false)).On, Is.False);
            });
        }

        [Test]
        public void TryParse_DimmerRoundTrip_IncludingBoundaries()
        {
            Assert.Multiple(() =>
            {
                SceneValue mid = TryParse(SceneValue.Dimmer(42, TimeSpan.FromMilliseconds(2000)));
                Assert.That(mid.Kind, Is.EqualTo(SceneValueKind.Dimmer));
                Assert.That(mid.LevelPercent, Is.EqualTo(42));
                Assert.That(mid.RampTime, Is.EqualTo(TimeSpan.FromMilliseconds(2000)));
                // boundaries 0 % / 0 ms and 100 %
                Assert.That(TryParse(SceneValue.Dimmer(0, TimeSpan.Zero)).LevelPercent, Is.EqualTo(0));
                Assert.That(TryParse(SceneValue.Dimmer(0, TimeSpan.Zero)).RampTime, Is.EqualTo(TimeSpan.Zero));
                Assert.That(TryParse(SceneValue.Dimmer(100, TimeSpan.Zero)).LevelPercent, Is.EqualTo(100));
            });
        }

        [Test]
        public void TryParse_ShutterRoundTrip()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TryParse(SceneValue.Shutter(true)).ShutterUp, Is.True);
                Assert.That(TryParse(SceneValue.Shutter(false)).ShutterUp, Is.False);
            });
        }

        // ----- malformed tolerance: a non-numeric value parses to 0, never throws -----

        [Test]
        public void TryParse_MalformedDimmer_DefaultsToZero_DoesNotThrow()
        {
            ProjectElement member = new("scene_dimmer", null,
                ImmutableArray.Create(("dimming_value", "abc"), ("ramptime_ms", "not-a-number")),
                ImmutableArray<ProjectElement>.Empty);
            Assert.That(SceneValue.TryParse(member, out SceneValue sv), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(sv.LevelPercent, Is.EqualTo(0));
                Assert.That(sv.RampTime, Is.EqualTo(TimeSpan.Zero));
            });
        }

        [Test]
        public void TryParse_NonSceneMember_IsFalse()
        {
            ProjectElement notAMember = new("resource_output", null,
                ImmutableArray<(string, string)>.Empty, ImmutableArray<ProjectElement>.Empty);
            Assert.That(SceneValue.TryParse(notAMember, out _), Is.False);
        }

        // ----- SetSceneValue edits in place, preserving identity/link -----

        [Test]
        public async Task SetSceneValue_EditsValueInPlace_PreservingIdNameAndLink()
        {
            ProjectEditor editor = (await ReplayOracle.LoadProject(Original)).Edit();
            (ResourceRef pin, ScenesRef target) = ResolveDimmerEndpoints(editor);
            editor.LinkScene(pin, target, SceneValue.Dimmer(100, TimeSpan.FromMilliseconds(1000)));
            ProjectElement before = editor.ToProject().Root.Descendants().Single(e => e.Tag == "scene_dimmer");
            ElementId memberId = before.Id!.Value;
            string beforeName = before.GetAttribute("name")!;
            string beforeLink = before.GetAttribute("link")!;

            editor.SetSceneValue(memberId, SceneValue.Dimmer(42, TimeSpan.FromMilliseconds(2000)));
            ProjectElement after = editor.ToProject().FindById(memberId)!;

            Assert.Multiple(() =>
            {
                Assert.That(after.GetAttribute("dimming_value"), Is.EqualTo("42"), "the value is rewritten");
                Assert.That(after.GetAttribute("ramptime_ms"), Is.EqualTo("2000"));
                Assert.That(after.GetAttribute("name"), Is.EqualTo(beforeName), "name preserved");
                Assert.That(after.GetAttribute("link"), Is.EqualTo(beforeLink), "the IDREF back to the scene_link is preserved");
                Assert.That(after.Id!.Value, Is.EqualTo(memberId), "identity preserved");
            });
        }

        // ----- SetSceneValue rejects a kind mismatch -----

        [Test]
        public async Task SetSceneValue_KindMismatch_Throws()
        {
            ProjectEditor editor = (await ReplayOracle.LoadProject(Original)).Edit();
            (ResourceRef pin, ScenesRef target) = ResolveDimmerEndpoints(editor);
            editor.LinkScene(pin, target, SceneValue.Dimmer(100, TimeSpan.FromMilliseconds(1000)));
            ElementId dimmerId = editor.ToProject().Root.Descendants().Single(e => e.Tag == "scene_dimmer").Id!.Value;

            Assert.That(() => editor.SetSceneValue(dimmerId, SceneValue.Relay(true)),
                Throws.TypeOf<InvalidOperationException>());
        }

        // ----- helpers -----

        private static SceneValue TryParse(SceneValue built)
        {
            // Round-trip through a member element built from the value's own written attributes.
            ProjectElement member = new(MemberTagOf(built), null,
                built.Kind switch
                {
                    SceneValueKind.Relay => ImmutableArray.Create(("relay_value", built.On ? "on" : "off")),
                    SceneValueKind.Dimmer => ImmutableArray.Create(
                        ("dimming_value", built.LevelPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        ("ramptime_ms", ((long)built.RampTime.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    _ => ImmutableArray.Create(("shutter_position", built.ShutterUp ? "up" : "down")),
                },
                ImmutableArray<ProjectElement>.Empty);
            Assert.That(SceneValue.TryParse(member, out SceneValue parsed), Is.True);
            return parsed;
        }

        private static string MemberTagOf(SceneValue v) => v.Kind switch
        {
            SceneValueKind.Relay => "scene_relay",
            SceneValueKind.Dimmer => "scene_dimmer",
            _ => "scene_shutter",
        };

        private static (ResourceRef Pin, ScenesRef Target) ResolveDimmerEndpoints(ProjectEditor editor)
        {
            ResourceRef pin = editor.Group("Værelse").FunctionBlock("4.1.01. AND (\"Og\"- blok)")
                .SceneOutput("Scenarie Sluk");
            return (pin, editor.Group("Soveværelse").Product("Dimmer Universal").Scenes());
        }
    }
}
