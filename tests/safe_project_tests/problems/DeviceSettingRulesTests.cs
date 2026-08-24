using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T050 — the five dimmer and shutter device-setting rows, per rule, over the partitions each predicate names.
    ///
    /// <para><b>The partition that matters most in this set is ABSENT versus ZERO.</b> The catalog ships every one of
    /// these setting elements with an id and no <c>value</c>, and a project's own DTD defaults that value to 0 —
    /// while the vendor's dialog shows its factory default there instead. So "absent" and "stored as zero" are two
    /// different states of the device, and every rule here is tested against both.</para>
    ///
    /// <para><b>One test uses the shipped catalog rather than a tree</b>, because the whole absent-value question is
    /// about what a real placed product looks like: placing a real dimmer must produce none of these five findings.</para>
    /// </summary>
    [TestFixture]
    public sealed class DeviceSettingRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        // ── dev-dimmer-fade-zero ────────────────────────────────────────────────────────────────────

        [Test]
        public void BothFadeRatesAtZeroAreReported_AndOneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmer(fadeUp: "0", fadeDown: "0"), "dev-dimmer-fade-zero"), Is.EqualTo(1));
                Assert.That(Count(Dimmer(fadeUp: "0", fadeDown: "700"), "dev-dimmer-fade-zero"), Is.Zero,
                    "one hard direction is an asymmetry a dimmer can be set to — the row says BOTH");
                Assert.That(Count(Dimmer(fadeUp: "700", fadeDown: "700"), "dev-dimmer-fade-zero"), Is.Zero);
            });
        }

        [Test]
        public void AnAbsentFadeRateIsNotAStoredZero()
        {
            Assert.That(Count(Dimmer(fadeUp: null, fadeDown: null), "dev-dimmer-fade-zero"), Is.Zero,
                "the catalog stores no value and the dialog shows 700 ms there — an absent setting is "
                + "uncommissioned, not a dimmer that switches hard");
        }

        // ── dev-dimmer-range-inverted, and its boundary ─────────────────────────────────────────────

        [Test]
        public void AnInvertedOrEmptyRangeIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmer(minimum: "80", maximum: "40"), "dev-dimmer-range-inverted"), Is.EqualTo(1),
                    "inverted — and the exact shape the vendor-authored error fixture carries");
                Assert.That(Count(Dimmer(minimum: "40", maximum: "40"), "dev-dimmer-range-inverted"), Is.EqualTo(1),
                    "EQUAL counts: a range from 40 to 40 has no room to dim in");
                Assert.That(Count(Dimmer(minimum: "22", maximum: "100"), "dev-dimmer-range-inverted"), Is.Zero,
                    "the catalog's own values are a proper range");
                Assert.That(Message(Dimmer(minimum: "80", maximum: "40"), "dev-dimmer-range-inverted"),
                    Does.Contain("80").And.Contain("40"), "both levels are bound, so the reader sees which way round");
            });
        }

        [Test]
        public void OneAbsentBoundLeavesTheRangeUnjudged()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmer(minimum: "80", maximum: null), "dev-dimmer-range-inverted"), Is.Zero);
                Assert.That(Count(Dimmer(minimum: null, maximum: "40"), "dev-dimmer-range-inverted"), Is.Zero);
            });
        }

        // ── dev-dimmer-max-zero ─────────────────────────────────────────────────────────────────────

        [Test]
        public void AMaximumOfZeroIsReportedBesideTheEmptyRange()
        {
            Project both = Dimmer(minimum: "0", maximum: "0");

            Assert.Multiple(() =>
            {
                Assert.That(Count(both, "dev-dimmer-max-zero"), Is.EqualTo(1));
                Assert.That(Count(both, "dev-dimmer-range-inverted"), Is.EqualTo(1),
                    "TWO findings on purpose: 'the range is empty' and 'the load can never be lit' are two facts "
                    + "a reader acts on differently");
                Assert.That(Count(Dimmer(maximum: null), "dev-dimmer-max-zero"), Is.Zero, "absent is not zero");
            });
        }

        // ── dev-dimmer-load-mode-auto ───────────────────────────────────────────────────────────────

        [Test]
        public void AnLedDimmerOnAutomaticIsReported_IncludingWhenNoModeIsStored()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(LedDimmer(loadMode: "auto"), "dev-dimmer-load-mode-auto"), Is.EqualTo(1));
                Assert.That(Count(LedDimmer(loadMode: null), "dev-dimmer-load-mode-auto"), Is.EqualTo(1),
                    "the exception to this set's stored-value reading: an absent mode takes the project's own "
                    + "declared default, and with no inline DTD that is the SDK registry's 'auto' — which every "
                    + "authentic vendor file also declares");
                Assert.That(Count(LedDimmer(loadMode: "rl"), "dev-dimmer-load-mode-auto"), Is.Zero);
                Assert.That(Count(LedDimmer(loadMode: "rc"), "dev-dimmer-load-mode-auto"), Is.Zero);
            });
        }

        /// <summary>
        /// The row is about the EFFECTIVE mode, so an absent value has to be read through the project's own schema
        /// view — the same read <c>DialogReadViews.DimmerView.LoadMode</c> makes.
        ///
        /// <para><b>What the file actually determines.</b> Every authentic vendor <c>.vis</c> declares this element
        /// once, as <c>value (auto | rc | rl) "auto"</c>, and materializes the LED family's catalog default onto
        /// the instance instead (<c>project3-KompleksWired.vis</c> stores <c>value="rc"</c> on both channels of its
        /// <c>product_rs485_led_dimmer</c>, while the unstored one belongs to a <c>product_airlink</c> this row
        /// excludes). So on the corpus the two readings agree, and the bug is invisible there. It is not invisible
        /// in general: the format is open-world, a project may declare any default it likes for the tag, and a
        /// hard-coded <c>?? "auto"</c> then reports a dimmer as automatic while the dialog beside it shows
        /// Capacitive. Reading through the view makes the rule and the dialog agree by construction rather than by
        /// coincidence.</para>
        /// </summary>
        [Test]
        public void AnAbsentLoadModeTakesTheProjectsDeclaredDefault_NotAHardCodedAuto()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(LedDimmerDeclaring("rc", loadMode: null), "dev-dimmer-load-mode-auto"), Is.Zero,
                    "the project declares 'rc' as the default, so an absent mode is capacitive — which is what "
                    + "the dimmer dialog shows for it");
                Assert.That(Count(LedDimmerDeclaring("rc", loadMode: "auto"), "dev-dimmer-load-mode-auto"),
                    Is.EqualTo(1), "a STORED auto still fires whatever the declared default is");
                Assert.That(Count(LedDimmerDeclaring("auto", loadMode: null), "dev-dimmer-load-mode-auto"),
                    Is.EqualTo(1), "and the vendor's own declaration keeps absence reporting");
            });
        }

        [Test]
        public void AWirelessDimmerOnAutomaticIsNotReported()
        {
            Assert.That(Count(Dimmer(loadMode: "auto"), "dev-dimmer-load-mode-auto"), Is.Zero,
                "automatic is the vendor's own default outside the LED family, and the row's consequence names "
                + "LED loads");
        }

        // ── dev-shutter-traveltime-zero ─────────────────────────────────────────────────────────────

        [Test]
        public void EitherTravelTimeAtZeroIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Shutter(up: "0", down: "120"), "dev-shutter-traveltime-zero"), Is.EqualTo(1),
                    "EITHER direction, unlike the dimmer's fade pair: a shutter that cannot time one direction "
                    + "cannot position itself at all");
                Assert.That(Count(Shutter(up: "120", down: "0"), "dev-shutter-traveltime-zero"), Is.EqualTo(1));
                Assert.That(Count(Shutter(up: "120", down: "120"), "dev-shutter-traveltime-zero"), Is.Zero);
                Assert.That(Count(Shutter(up: null, down: null), "dev-shutter-traveltime-zero"), Is.Zero,
                    "absent is the 120 s factory default the dialog shows — the row's own 'measured during "
                    + "commissioning' reading");
            });
        }

        [Test]
        public void AProductWithNoShutterSettingsIsNotConsidered()
        {
            Assert.That(Count(Dimmer(fadeUp: "700"), "dev-shutter-traveltime-zero"), Is.Zero);
        }

        // ── the shipped catalog: a placed product is not a defect ────────────────────────────────────

        [Test]
        public void PlacingTheShippedDimmerProducesNoneOfTheseFindings()
        {
            ProjectAppService app = new(TestSetup.Settings);
            Ihc.Vis.Products.ProductDefinition dimmer = app.GetAvailableProducts()
                .First(p => p.Body.FindDescendantOrSelf(e => e.Tag == "dimmer_settings") is not null);
            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ElementId locality = project.Groups.First().Id!.Value;
            project = app.Apply(project, app.Commands.AddProduct(project, locality, dimmer)).Project!;

            string[] rows =
            [
                "dev-dimmer-fade-zero", "dev-dimmer-range-inverted", "dev-dimmer-max-zero",
                "dev-shutter-traveltime-zero",
            ];

            Assert.Multiple(() =>
            {
                foreach (string row in rows)
                {
                    Assert.That(Count(project, row), Is.Zero,
                        $"{row}: the catalog stores no value on these settings, and an effective read would have "
                        + "called every placed dimmer's setting zero");
                }

                Assert.That(Validate(project).IsValid, Is.True);
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static Project InLocality(params ProjectElement[] products) =>
            Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], products)));

        /// <summary>A setting element, carrying a stored value only when one is given.</summary>
        private static ProjectElement Setting(string tag, int at, string? value) =>
            Tree.Node(tag, Token(tag, at),
                value is null ? [] : [("value", value)]);

        private static ProjectElement DimmerSettings(
            int at, string? fadeUp, string? fadeDown, string? minimum, string? maximum, string? loadMode) =>
            Tree.Node("dimmer_settings", Token("dimmer_settings", at), [("name", "Indstillinger")],
                Setting("dimmer_setting_minimum_value", at + 1, minimum),
                Setting("dimmer_setting_maximum_value", at + 2, maximum),
                Setting("dimmer_setting_fade_rate_up", at + 3, fadeUp),
                Setting("dimmer_setting_fade_rate_down", at + 4, fadeDown),
                Setting("dimmer_setting_load_mode", at + 5, loadMode));

        /// <summary>A WIRELESS dimmer product (the family whose automatic load mode is the vendor's own choice).</summary>
        private static Project Dimmer(
            string? fadeUp = null, string? fadeDown = null, string? minimum = null, string? maximum = null,
            string? loadMode = null) =>
            InLocality(Tree.Node("product_airlink", Token("product_airlink", 0x60),
                [("product_identifier", "_0x4304"), ("name", "Trådløs dæmper"), ("serialnumber", "_0xaa11")],
                Tree.Node("airlink_dimming", Token("airlink_dimming", 0x61),
                    [("name", "Dæmp"), ("address_channel", "_0x1")]),
                DimmerSettings(0x70, fadeUp, fadeDown, minimum, maximum, loadMode)));

        /// <summary>
        /// The same LED dimmer, in a project that DECLARES the load-mode default itself — the open-world state
        /// where the tag's default is the file's own rather than the SDK registry's fallback.
        /// </summary>
        /// <param name="declaredDefault">The default the project's inline DTD declares for <c>value</c>.</param>
        /// <param name="loadMode">The stored mode, or null to leave the attribute absent.</param>
        private static Project LedDimmerDeclaring(string declaredDefault, string? loadMode) =>
            LedDimmer(loadMode) with
            {
                InlineDtdBlocks = ImmutableDictionary<string, string>.Empty.Add("dimmer_setting_load_mode",
                    "<!ELEMENT dimmer_setting_load_mode ANY>\n"
                    + "<!ATTLIST dimmer_setting_load_mode id ID #REQUIRED\n"
                    + $"               value (auto | rc | rl_led) \"{declaredDefault}\"\n"
                    + "               udf CDATA \"\">"),
            };

        /// <summary>An RS485 LED dimmer — the family whose load type is known.</summary>
        private static Project LedDimmer(string? loadMode) =>
            InLocality(Tree.Node("product_rs485_led_dimmer", Token("product_rs485_led_dimmer", 0x60),
                [("product_identifier", "_0x4410"), ("name", "LED dæmper")],
                Tree.Node("rs485_led_dimmer_channel", Token("rs485_led_dimmer_channel", 0x61),
                    [("name", "Kanal 1"), ("channel", "_0x0"), ("channel_id", "_0x11")]),
                DimmerSettings(0x70, "700", "700", "22", "100", loadMode)));

        private static Project Shutter(string? up, string? down) =>
            InLocality(Tree.Node("product_airlink", Token("product_airlink", 0x60),
                [("product_identifier", "_0x4305"), ("name", "Trådløs gardin"), ("serialnumber", "_0xbb22")],
                Tree.Node("airlink_shutter_up", Token("airlink_shutter_up", 0x61),
                    [("name", "Op"), ("address_channel", "_0x1")]),
                Tree.Node("shutter_settings", Token("shutter_settings", 0x70), [("name", "Indstillinger")],
                    Setting("shutter_setting_travel_time_up", 0x71, up),
                    Setting("shutter_setting_travel_time_down", 0x72, down))));
    }
}
