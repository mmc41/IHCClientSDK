using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using static Ihc.Vis.Tests.RuleProbe;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T049 — the four remaining ADDRESSING rows: wireless identity, wireless channel collisions, a modem that can
    /// reach nobody, and the S0 pulse range (va-ana G6).
    ///
    /// <para><b>The pulse range is read from the entry, never retyped</b> — T049's instruction to reuse T023's
    /// bounds pattern rather than hardcode a range. Its boundary test probes below the minimum, AT it, inside, AT
    /// the maximum and past it, all against the declared numbers.</para>
    ///
    /// <para><b>Three tests exist because a default nearly made a rule wrong.</b> A wireless product ships
    /// uncommissioned, its first pin ships on the catalog's channel 1, and a modem ships with thirty blank slots;
    /// each of those states is asserted NOT to be the respective collision or blank-path finding, over the shipped
    /// catalog where possible rather than over a hand-typed tree.</para>
    /// </summary>
    [TestFixture]
    public sealed class DeviceAddressRulesTests
    {
        private static double Threshold(string name)
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("addr-s0-ticks-missing"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == name);
            Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.VendorDocumented),
                "the range is the vendor tool's own bound, measured from its refusal message — not authored");
            return declared.Value;
        }

        // ── addr-wireless-not-commissioned ──────────────────────────────────────────────────────────

        [Test]
        public void AProductWithNoSerialOrThePlaceholderIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Wireless(serial: ""), "addr-wireless-not-commissioned"), Is.EqualTo(1));
                Assert.That(Count(Wireless(serial: ElementId.NullToken), "addr-wireless-not-commissioned"),
                    Is.EqualTo(1), "the placeholder token is what a planned-but-uncommissioned product carries");
                Assert.That(Count(Wireless(serial: "_0x1234ab"), "addr-wireless-not-commissioned"), Is.Zero);
                Assert.That(Message(Wireless(serial: ""), "addr-wireless-not-commissioned"),
                    Does.Contain("Trådløs"), "the product is named");
            });
        }

        // ── addr-wireless-channel-shared ────────────────────────────────────────────────────────────

        [Test]
        public void TwoCommissionedProductsSharingASerialAndChannelAreReported()
        {
            Project colliding = TwoWireless("_0xaa11", "_0xaa11", "_0x1", "_0x1");
            Project differentChannel = TwoWireless("_0xaa11", "_0xaa11", "_0x1", "_0x2");
            Project differentDevice = TwoWireless("_0xaa11", "_0xbb22", "_0x1", "_0x1");

            Assert.Multiple(() =>
            {
                Assert.That(Count(colliding, "addr-wireless-channel-shared"), Is.EqualTo(1),
                    "one serial, one channel, two products — both react to the same command");
                Assert.That(Message(colliding, "addr-wireless-channel-shared"), Does.Contain("_0x1"));
                Assert.That(Count(differentChannel, "addr-wireless-channel-shared"), Is.Zero);
                Assert.That(Count(differentDevice, "addr-wireless-channel-shared"), Is.Zero,
                    "a channel index means nothing across two different devices");
            });
        }

        [Test]
        public void TwoSpellingsOfOneChannelAreOneChannel()
        {
            Project project = TwoWireless("_0xaa11", "_0xaa11", "_0x01", "_0x1");

            Assert.That(Count(project, "addr-wireless-channel-shared"), Is.EqualTo(1),
                "the catalog writes _0x01 where a saved file writes _0x1 — comparing the raw strings would miss it");
        }

        [Test]
        public void UncommissionedProductsSharingTheCatalogsChannelAreNotACollision()
        {
            Project planned = TwoWireless(ElementId.NullToken, ElementId.NullToken, "_0x1", "_0x1");

            Assert.Multiple(() =>
            {
                Assert.That(Count(planned, "addr-wireless-channel-shared"), Is.Zero,
                    "measured: channel 1 is shared by three or four products in every vendor fixture, because "
                    + "none of them is commissioned — reporting it would fire on files IHC Visual authored");
                Assert.That(Count(planned, "addr-wireless-not-commissioned"), Is.EqualTo(2),
                    "the state IS reported, by the row whose condition it is");
            });
        }

        [Test]
        public void TwoPinsOfOneProductSharingAChannelAreNotACollision()
        {
            Project shutter = ShutterProduct("_0xaa11", "_0x1");

            Assert.That(Count(shutter, "addr-wireless-channel-shared"), Is.Zero,
                "a shutter product's up/down pins deliberately reuse their first input's channel (measured)");
        }

        /// <summary>The same claim over the SHIPPED catalog: placing two real wireless products is not a collision.</summary>
        [Test]
        public void PlacingTwoShippedWirelessProductsProducesNoCollision()
        {
            ProjectAppService app = new(TestSetup.Settings);
            Ihc.Vis.Products.ProductDefinition wireless = app.GetAvailableProducts()
                .First(p => p.Body.FindDescendantOrSelf(e => e.GetAttribute("address_channel") is not null) is not null);
            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ElementId locality = project.Groups.First().Id!.Value;
            project = app.Apply(project, app.Commands.AddProduct(project, locality, wireless)).Project!;
            project = app.Apply(project, app.Commands.AddProduct(project, locality, wireless)).Project!;

            Assert.Multiple(() =>
            {
                Assert.That(Count(project, "addr-wireless-channel-shared"), Is.Zero,
                    "two freshly placed products share the catalog's channel 1 and are both uncommissioned");
                Assert.That(Validate(project).IsValid, Is.True);
            });
        }

        // ── addr-modem-phonenumber-blank ────────────────────────────────────────────────────────────

        [Test]
        public void AModemWithNoNumberInAnySlotIsReportedOnce()
        {
            Project empty = Modem(numbersFilled: 0, slots: 30);
            Project oneFilled = Modem(numbersFilled: 1, slots: 30);

            Assert.Multiple(() =>
            {
                Assert.That(Count(empty, "addr-modem-phonenumber-blank"), Is.EqualTo(1),
                    "ONE finding for the modem — not twenty-seven for its blank slots");
                Assert.That(Message(empty, "addr-modem-phonenumber-blank"), Does.Contain("30"),
                    "the slot count is bound, so the reader knows where to put the number");
                Assert.That(Count(oneFilled, "addr-modem-phonenumber-blank"), Is.Zero,
                    "one number is a working notification path — the row's consequence is about the path, not "
                    + "about paperwork");
            });
        }

        // ── addr-modem-phonenumber-malformed ────────────────────────────────────────────────────────

        private const string Malformed = "addr-modem-phonenumber-malformed";

        /// <summary>
        /// The 3/20 pair has ONE owner. <c>DialogValueRule.PhoneNumber</c> is the operative predicate the dialog
        /// and the commit path consult; the entry's thresholds are the governance mirror. Without this assertion
        /// <c>Check</c> would enforce Products' pair while <c>Describe</c> and the rendered finding advertise the
        /// entry's, and nothing would notice.
        /// </summary>
        [Test]
        public void TheTelephoneLengthsAreDeclaredOnTheEntryAndMirrorTheDialogRule()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(Malformed),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold minimum = entry.Thresholds.Single(t => t.Name == "MinPhoneNumberLength");
            DeclaredThreshold maximum = entry.Thresholds.Single(t => t.Name == "MaxPhoneNumberLength");

            Assert.Multiple(() =>
            {
                Assert.That(minimum.Value, Is.EqualTo((double)DialogValueRule.PhoneNumber.MinLength!.Value),
                    "the entry mirrors the operative predicate's minimum rather than declaring a second one");
                Assert.That(maximum.Value, Is.EqualTo((double)DialogValueRule.PhoneNumber.MaxLength!.Value),
                    "the entry mirrors the operative predicate's maximum rather than declaring a second one");
                Assert.That(minimum.Confidence, Is.EqualTo(ThresholdConfidence.VendorDocumented),
                    "measured: the vendor refuses a 2-character number and accepts a 3-character one");
                Assert.That(maximum.Confidence, Is.EqualTo(ThresholdConfidence.Authored),
                    "an OpenVisual strictness — the vendor accepts a 60-digit number");
                Assert.That(entry.Faces, Is.EqualTo(RuleFaces.WholeProject | RuleFaces.DialogMetadata),
                    "one definition, two faces: the project finding and the dialog's bounds");
                Assert.That(entry.Target, Is.EqualTo(new RuleTarget("sms_modem_phonenumber", "phonenumber")),
                    "RunConstraints returns immediately for a target-less rule");
            });
        }

        [Test]
        public void AMalformedTelephoneNumberIsReportedPerSlotAgainstTheDeclaredLengths()
        {
            string atMinimum = "+45";
            string belowMinimum = "+4";
            string atMaximum = "+" + new string('4', 19);
            string pastMaximum = "+" + new string('4', 20);

            Assert.Multiple(() =>
            {
                Assert.That(belowMinimum, Has.Length.EqualTo(2));
                Assert.That(atMinimum, Has.Length.EqualTo(3));
                Assert.That(atMaximum, Has.Length.EqualTo(20));
                Assert.That(pastMaximum, Has.Length.EqualTo(21));

                Assert.That(Count(ModemWith(belowMinimum), Malformed), Is.EqualTo(1), "two characters");
                Assert.That(Count(ModemWith(atMinimum), Malformed), Is.Zero,
                    "three — AT the minimum, and the one bound the vendor itself enforces");
                Assert.That(Count(ModemWith(atMaximum), Malformed), Is.Zero, "twenty — AT the maximum");
                Assert.That(Count(ModemWith(pastMaximum), Malformed), Is.EqualTo(1), "twenty-one — past it");
                Assert.That(Count(ModemWith("+45 12 34 56"), Malformed), Is.EqualTo(1), "embedded whitespace");
                Assert.That(Count(ModemWith("4512345678"), Malformed), Is.EqualTo(1), "no country code");
                Assert.That(Count(ModemWith(""), Malformed), Is.Zero,
                    "an empty slot is addr-modem-phonenumber-blank's question about a different subject");
                Assert.That(Count(ModemWith("+4512345678", belowMinimum, "4512345678"), Malformed), Is.EqualTo(2),
                    "one finding per offending SLOT — the shape is OnePerOccurrence");
                Assert.That(Message(ModemWith(belowMinimum), Malformed), Does.Contain(belowMinimum),
                    "the offending value is bound into the sentence");
            });
        }

        // ── addr-s0-ticks-missing: the declared range ───────────────────────────────────────────────

        [Test]
        public void ThePulseRangeIsDeclaredOnTheEntryAsVendorDocumented()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Threshold("MinimumTicks"), Is.EqualTo(1));
                Assert.That(Threshold("MaximumTicks"), Is.EqualTo(10000));
            });
        }

        [Test]
        public void TheMeterIsReportedOutsideTheDeclaredRangeAndAcceptedInsideIt()
        {
            long minimum = (long)Threshold("MinimumTicks");
            long maximum = (long)Threshold("MaximumTicks");

            Assert.Multiple(() =>
            {
                Assert.That(Count(Meter(null), "addr-s0-ticks-missing"), Is.EqualTo(1), "absent");
                Assert.That(Count(Meter(""), "addr-s0-ticks-missing"), Is.EqualTo(1), "blank");
                Assert.That(Count(Meter("0"), "addr-s0-ticks-missing"), Is.EqualTo(1), "below the minimum");
                Assert.That(Count(Meter(Text(minimum)), "addr-s0-ticks-missing"), Is.Zero, "AT the minimum");
                Assert.That(Count(Meter("100"), "addr-s0-ticks-missing"), Is.Zero,
                    "inside — and the value every vendor fixture's meter carries");
                Assert.That(Count(Meter(Text(maximum)), "addr-s0-ticks-missing"), Is.Zero, "AT the maximum");
                Assert.That(Count(Meter(Text(maximum + 1)), "addr-s0-ticks-missing"), Is.EqualTo(1), "just past it");
                Assert.That(Count(Meter("mange"), "addr-s0-ticks-missing"), Is.EqualTo(1), "not a number at all");
                Assert.That(Message(Meter(null), "addr-s0-ticks-missing"),
                    Does.Contain(Text(minimum)).And.Contain(Text(maximum)),
                    "both declared bounds are bound into the sentence");
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static Project InLocality(params ProjectElement[] products) =>
            Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], products)));

        private static ProjectElement WirelessProduct(int at, string name, string serial, params (string Tag, string Channel)[] pins) =>
            Tree.Node("product_airlink", Token("product_airlink", at),
                [("product_identifier", "_0x4304"), ("name", name), ("serialnumber", serial)],
                [.. pins.Select((pin, i) => Tree.Node(pin.Tag, Token(pin.Tag, at + 1 + i),
                    [("name", $"Kanal {i + 1}"), ("address_channel", pin.Channel)]))]);

        private static Project Wireless(string serial) =>
            InLocality(WirelessProduct(0x60, "Trådløs afbryder", serial, ("airlink_input", "_0x1")));

        private static Project TwoWireless(string firstSerial, string secondSerial, string firstChannel, string secondChannel) =>
            InLocality(
                WirelessProduct(0x60, "Trådløs 1", firstSerial, ("airlink_input", firstChannel)),
                WirelessProduct(0x70, "Trådløs 2", secondSerial, ("airlink_input", secondChannel)));

        /// <summary>One product whose up/down pins reuse the first input's channel — the vendor's own encoding.</summary>
        private static Project ShutterProduct(string serial, string channel) =>
            InLocality(WirelessProduct(0x60, "Trådløs gardin", serial,
                ("airlink_input", channel), ("airlink_shutter_up", channel), ("airlink_shutter_down", channel)));

        private static Project Modem(int numbersFilled, int slots) =>
            InLocality(Tree.Node("product_rs485_sms_modem", Token("product_rs485_sms_modem", 0x60),
                [("product_identifier", "_0x9f10"), ("name", "SMS modem")],
                Tree.Node("sms_modem_settings", Token("sms_modem_settings", 0x61), [("name", "Indstillinger")],
                    [.. Enumerable.Range(0, slots).Select(i => Tree.Node("sms_modem_phonenumber",
                        Token("sms_modem_phonenumber", 0x62 + i),
                        [("address", Text(i + 1)), ("phonenumber", i < numbersFilled ? "+4512345678" : "")]))])));

        /// <summary>One modem whose slots carry exactly the given numbers, in order.</summary>
        private static Project ModemWith(params string[] numbers) =>
            InLocality(Tree.Node("product_rs485_sms_modem", Token("product_rs485_sms_modem", 0x60),
                [("product_identifier", "_0x9f10"), ("name", "SMS modem")],
                Tree.Node("sms_modem_settings", Token("sms_modem_settings", 0x61), [("name", "Indstillinger")],
                    [.. numbers.Select((number, i) => Tree.Node("sms_modem_phonenumber",
                        Token("sms_modem_phonenumber", 0x62 + i),
                        [("address", Text(i + 1)), ("phonenumber", number)]))])));

        private static Project Meter(string? ticks) =>
            InLocality(Tree.Node("s0_device", Token("s0_device", 0x60),
                ticks is null
                    ? [("product_identifier", "_0x9f20"), ("name", "Elmåler")]
                    : [("product_identifier", "_0x9f20"), ("name", "Elmåler"), ("ticks", ticks)]));
    }
}
