using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The PRODUCT ADVISORY rows: what a placed product's own datasheet or lifecycle status says about the
    /// hardware, as opposed to what the file says about itself.
    ///
    /// <para><b>Why these are a module of their own.</b> Every other rule module is named for a QUESTION about the
    /// project — is it addressed, is it wired, is its program shaped like a program. These rows ask nothing of the
    /// project: the file is correct, and the fact worth reporting is a property of the device the author placed.
    /// Their subject is one thing — a placed product — so they share a module, exactly as the modules organised by
    /// subject elsewhere do.</para>
    ///
    /// <para><b>The first INFORMATION row in the catalogue lands here.</b> Information is the tier below Warning:
    /// a Warning asks the author for a judgement, and an Information finding asks for nothing at all. So the
    /// assertions below check the tier as deliberately as they check the count — a row that reported this as a
    /// Warning would be telling an author to consider repairing a correctly placed meter.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProductAdvisoryRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        // ── product-s0-instrument-only ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The finding, and its shape: ONE per meter, because each is a separate terminal a designer could try to
        /// build automation on.
        /// </summary>
        [Test]
        public void EveryS0MeterIsReportedAsAnInstrumentationInput()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Meters(1), "product-s0-instrument-only"), Is.EqualTo(1));
                Assert.That(Count(Meters(3), "product-s0-instrument-only"), Is.EqualTo(3),
                    "OnePerOccurrence — two meters are two separate read-out terminals");
                Assert.That(Message(Meters(1), "product-s0-instrument-only"), Does.Contain("Måler 1"),
                    "the authored name, so a reader can find the terminal in the tree");
            });
        }

        /// <summary>
        /// The EXCLUSION, and the one that matters: the row is about the S0 device root alone. A project of
        /// ordinary products — including a data-line input, which is the very terminal the message says the pulse
        /// wire may not share — reports nothing.
        /// </summary>
        [Test]
        public void AProjectWithoutAnS0MeterReportsNothing()
        {
            Assert.That(Count(OrdinaryInput(), "product-s0-instrument-only"), Is.Zero);
        }

        /// <summary>
        /// The tier. This is the catalogue's FIRST <see cref="ValidationSeverity.Info"/> finding, so the assertion
        /// is on the declaration and on the finding alike: nothing about a correctly placed meter is a mistake,
        /// and an Info finding never blocks a save.
        /// </summary>
        [Test]
        public void TheMeterFindingIsInformationAndBlocksNothing()
        {
            ProjectValidationResult result = Validate(Meters(1));
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("product-s0-instrument-only"),
                out ProblemCatalogEntry entry), Is.True, "the row is declared");

            Assert.Multiple(() =>
            {
                Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Info));
                Assert.That(entry.Category, Is.EqualTo(ValidationCategory.Addressing));
                Assert.That(result.Findings.Single(f => f.RuleId == "product-s0-instrument-only").Severity,
                    Is.EqualTo(ValidationSeverity.Info));
                Assert.That(result.Infos, Is.Not.Empty, "and it reaches the result's own Info accessor");
                Assert.That(result.IsValid, Is.True, "Information never blocks — only Error does");
            });
        }

        // ── product-wireless-phaseout ───────────────────────────────────────────────────────────────

        /// <summary>
        /// A project standing on IHC Wireless hardware owns a procurement decision: the vendor has announced a
        /// sales stop for the whole family during 2026. ONE finding for the project, carrying the exposure size.
        /// </summary>
        [Test]
        public void AProjectHoldingWirelessProductsIsToldAboutThePhaseOut()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Wireless(1), "product-wireless-phaseout"), Is.EqualTo(1));
                Assert.That(Count(Wireless(4), "product-wireless-phaseout"), Is.EqualTo(1),
                    "OneFinding: the project owns the decision, not each device");
                Assert.That(Message(Wireless(4), "product-wireless-phaseout"), Does.Contain("4"),
                    "and the count is what makes the exposure legible");
                Assert.That(Count(OrdinaryInput(), "product-wireless-phaseout"), Is.Zero,
                    "a project with no wireless product owns no such decision");
            });
        }

        /// <summary>
        /// THE SENTENCE IS THE ROW, and two things about it are load-bearing enough to assert.
        ///
        /// <para>It must NOT claim installed devices stop working: the vendor page states a sales stop only, and
        /// the harm is that replacement units become unorderable. And the execution date is explicitly still to
        /// be announced — an earlier draft of this text wrote <i>"udgår af salg fra 2026"</i>, which reads as a
        /// start date the vendor has not given. Both are the kind of overclaim a later edit could reintroduce
        /// without noticing, so the wording is pinned here rather than only in the declaration.</para>
        /// </summary>
        [Test]
        public void ThePhaseOutSentenceStatesASalesStopWithAnUnannouncedDate()
        {
            string message = Message(Wireless(1), "product-wireless-phaseout");

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("2026"));
                Assert.That(message, Does.Contain("endnu ikke er meldt ud"),
                    "the execution date hedge the vendor's own page carries");
                Assert.That(message, Does.Contain("erstatningsenheder"),
                    "the harm is that spares stop being orderable");
                Assert.That(message, Does.Not.Contain("holder op med at virke")
                    .And.Not.Contain("stopper med at virke"),
                    "installed devices keep working — the vendor states a SALES stop only");
                Assert.That(Validate(Wireless(1)).Findings
                    .Single(f => f.RuleId == "product-wireless-phaseout").Severity,
                    Is.EqualTo(ValidationSeverity.Info), "nothing is wrong; this is worth knowing");
            });
        }

        // ── product-discontinued ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A replacement for a discontinued device has to be planned, not assumed. ONE finding per instance,
        /// because each device is separately replaceable — unlike the fleet-wide phase-out above, which is one
        /// decision for the project.
        /// </summary>
        [Test]
        public void EachDiscontinuedProductInstanceIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Placed(("product_airlink", "_0x4104")), "product-discontinued"), Is.EqualTo(1));
                Assert.That(Count(Placed(("product_dataline", "_0x210c")), "product-discontinued"), Is.EqualTo(1),
                    "the set spans two root elements, which is why the row anchors per instance");
                Assert.That(
                    Count(Placed(("product_airlink", "_0x4104"), ("product_airlink", "_0x4105")),
                        "product-discontinued"),
                    Is.EqualTo(2), "OnePerOccurrence: two devices are two replacements to plan");
                Assert.That(Message(Placed(("product_dataline", "_0x210c")), "product-discontinued"),
                    Does.Contain("Produkt 0"), "the instance's name, so the reader can find the device");
            });
        }

        /// <summary>
        /// THE SET IS NINE IDS, NOT TEN, and the tenth is the interesting one. `_0x210d` was folded in by an
        /// earlier draft on the strength of a source that says something else entirely: that the RECEIVER
        /// `507N0034` is sold as a spare part only — and the receiver is not a product and never appears in a
        /// project file. The remote itself is recorded as an old IR system, not as discontinued.
        ///
        /// <para>It is not uncovered: `product-ir-generations-mixed` and `migration-untested-product` both speak
        /// for it. What it must not do is claim a vendor status no page states.</para>
        /// </summary>
        [Test]
        public void TheSixteenKeyRemoteIsNotInTheDiscontinuedSet()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Placed(("product_dataline", "_0x210d")), "product-discontinued"), Is.Zero,
                    "no source records _0x210d as discontinued — only its receiver as a spare part");
                Assert.That(Count(Placed(("product_dataline", "_0x2101")), "product-discontinued"), Is.Zero,
                    "and an ordinary current product is not in the set either");
                Assert.That(Count(Placed(("product_airlink", "_0x4304")), "product-discontinued"), Is.Zero,
                    "nor is a wireless product that is merely inside the 2026 family phase-out — that is a "
                    + "different condition, reported by a different row");
            });
        }

        /// <summary>
        /// THE KEY IS THE PAIR, not the identifier alone. Product identifiers are not unique across root elements
        /// in this catalogue, so a predicate keyed on the identifier by itself would report a data-line product
        /// that happens to share a wireless product's number.
        /// </summary>
        [Test]
        public void TheDiscontinuedKeyIsTheRootElementAndTheIdentifierTogether()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Placed(("product_dataline", "_0x4104")), "product-discontinued"), Is.Zero,
                    "_0x4104 is discontinued as a WIRELESS product; the same number on a data-line root is a "
                    + "different device and says nothing");
                Assert.That(Count(Placed(("product_airlink", "_0x210c")), "product-discontinued"), Is.Zero,
                    "and the converse");
            });
        }

        // ── product-ir-generations-mixed ────────────────────────────────────────────────────────────

        /// <summary>
        /// CO-OCCURRENCE IS THE WHOLE CONDITION, and the two negatives are what make it one. The installation
        /// cannot serve both transmitter generations from one receiver, but neither transmitter is a fault on its
        /// own — one alone is an ordinary installation with an ordinary receiver behind it.
        ///
        /// <para>The receiver never appears in a project file, so co-occurrence is the only mechanisable form of
        /// the question. A rule that reported either id by itself would be asserting a hardware conflict from
        /// half the evidence.</para>
        /// </summary>
        [Test]
        public void BothIrGenerationsTogetherAreReportedAndEitherAloneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    Count(Placed(("product_dataline", "_0x210d"), ("product_dataline", "_0x211f")),
                        "product-ir-generations-mixed"),
                    Is.EqualTo(1), "the pair IS the condition");
                Assert.That(Count(Placed(("product_dataline", "_0x210d")), "product-ir-generations-mixed"),
                    Is.Zero, "the 16-key remote alone is an ordinary old installation");
                Assert.That(Count(Placed(("product_dataline", "_0x211f")), "product-ir-generations-mixed"),
                    Is.Zero, "and the B&O-compatible one alone is a hardware question, not a finding");
                Assert.That(
                    Count(Placed(("product_dataline", "_0x210d"), ("product_dataline", "_0x211f"),
                            ("product_dataline", "_0x211f")),
                        "product-ir-generations-mixed"),
                    Is.EqualTo(1), "OneFinding: the conflict is the project's, however many of each it holds");
            });
        }

        /// <summary>
        /// The sentence is complete without arguments — there is exactly one condition it can state — so the row
        /// declares no slots at all. Asserted because a later edit adding a count would have to change the
        /// sentence too, and this is where that would be noticed.
        /// </summary>
        [Test]
        public void TheIrGenerationsRowBindsNoArguments()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("product-ir-generations-mixed"),
                out ProblemCatalogEntry entry), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entry.Slots, Is.Empty);
                Assert.That(entry.MessageTemplate, Does.Not.Contain("{"),
                    "no placeholder, because nothing varies between two projects that carry the pair");
            });
        }

        // ── product-sounder-not-alarm-approved ──────────────────────────────────────────────────────

        /// <summary>
        /// The vendor records these two sounders as not approved for statutory warning systems. If programs drive
        /// one as life-safety signalling, that is a compliance question the reader owns.
        ///
        /// <para><b>The rule does not try to decide whether the sounder IS used for life safety.</b> The file
        /// cannot answer that — a sounder driven by a program is a sounder driven by a program, whatever the
        /// installer meant it to signal. So it informs, per instance, and leaves the judgement where it belongs.
        /// That is also why the row is Information rather than a Warning: a Warning would be asking the author to
        /// decide something this row is explicitly not asking about.</para>
        /// </summary>
        [Test]
        public void EachUnapprovedSounderIsReportedWithoutJudgingItsUse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Placed(("product_dataline", "_0x2203")), "product-sounder-not-alarm-approved"),
                    Is.EqualTo(1));
                Assert.That(Count(Placed(("product_dataline", "_0x2204")), "product-sounder-not-alarm-approved"),
                    Is.EqualTo(1), "both recorded identifiers");
                Assert.That(
                    Count(Placed(("product_dataline", "_0x2203"), ("product_dataline", "_0x2204")),
                        "product-sounder-not-alarm-approved"),
                    Is.EqualTo(2), "OnePerOccurrence: each device is its own compliance question");
                Assert.That(Count(Placed(("product_dataline", "_0x2202")), "product-sounder-not-alarm-approved"),
                    Is.Zero, "a neighbouring identifier the vendor records nothing about");
                Assert.That(Message(Placed(("product_dataline", "_0x2203")), "product-sounder-not-alarm-approved"),
                    Does.Contain("Produkt 0"), "the instance's name, so the reader can find the device");
                Assert.That(Validate(Placed(("product_dataline", "_0x2203"))).Findings
                    .Single(f => f.RuleId == "product-sounder-not-alarm-approved").Severity,
                    Is.EqualTo(ValidationSeverity.Info));
            });
        }

        // ── migration-untested-product ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The conversion cost of a project is decided by exactly these products: the vendor states they cannot
        /// currently be reused in a KNX conversion, and that it is still investigating a replacement.
        /// </summary>
        [Test]
        public void EachNotCurrentlyConvertibleProductIsReported()
        {
            Assert.Multiple(() =>
            {
                foreach (string identifier in new[]
                {
                    "_0x2124", "_0x2125", "_0x2135", "_0x2138", "_0x2136", "_0x2139",   // smart sensors
                    "_0x210a", "_0x210f", "_0x2111",                                     // alarm
                    "_0x210d", "_0x211f",                                                // IR
                })
                {
                    Assert.That(Count(Placed(("product_dataline", identifier)), "migration-untested-product"),
                        Is.EqualTo(1), identifier);
                }

                Assert.That(Count(Placed(("product_dataline", "_0x2101")), "migration-untested-product"),
                    Is.Zero, "the reusable half — pushbuttons, cabling, PIR on/off — is not a finding");
                Assert.That(
                    Count(Placed(("product_dataline", "_0x2125"), ("product_dataline", "_0x2139")),
                        "migration-untested-product"),
                    Is.EqualTo(2), "OnePerOccurrence: each product is its own conversion cost");
            });
        }

        /// <summary>
        /// THE VENDOR STATEMENT IS PROVISIONAL, and the sentence must not harden it. The source prefaces the whole
        /// list <i>"Nedenstående er foreløbige konklusioner, som vi arbejder videre med at forbedre og
        /// validere"</i>, and the three clauses read "cannot currently be replaced/used, still being investigated".
        ///
        /// <para>An earlier draft of the source rendered all eleven as <i>"må derfor redesignes"</i> — a verdict
        /// the vendor never gave — and only ONE of the three groups carries even a recommendation. The sentence
        /// here is the common denominator the vendor actually states, and this test guards it: a later edit that
        /// reintroduced "must be redesigned" would fail on the words rather than pass quietly.</para>
        /// </summary>
        [Test]
        public void TheMigrationSentenceStaysProvisionalAsTheVendorWroteIt()
        {
            string message = Message(Placed(("product_dataline", "_0x2125")), "migration-untested-product");

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("for nuværende"),
                    "\"cannot CURRENTLY be reused\" — the vendor's own hedge");
                Assert.That(message, Does.Contain("undersøger fortsat"),
                    "and \"still investigating\", which is the other half of it");
                Assert.That(message, Does.Not.Contain("redesignes").And.Not.Contain("skal erstattes"),
                    "the vendor gave no such verdict, and an earlier draft asserted one");
            });
        }

        /// <summary>
        /// The two IR remotes are in THIS set as well as in their own co-occurrence row. That is not duplication:
        /// one says the pair needs incompatible receivers, the other says neither converts to KNX today. A single
        /// IR remote therefore reports here and stays silent there.
        /// </summary>
        [Test]
        public void AnIrRemoteReportsItsMigrationStatusWithoutTheGenerationsRow()
        {
            Project single = Placed(("product_dataline", "_0x210d"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(single, "migration-untested-product"), Is.EqualTo(1));
                Assert.That(Count(single, "product-ir-generations-mixed"), Is.Zero,
                    "one remote is no generation conflict");
                Assert.That(Count(single, "product-discontinued"), Is.Zero,
                    "and _0x210d is not recorded discontinued — only its receiver is a spare part");
            });
        }

        // ── product-keypad-codes-local ──────────────────────────────────────────────────────────────

        /// <summary>
        /// A15: the RS-485 LED dimmer, which the vendor reports as suffering persistent link and upload errors
        /// below controller firmware 03.03.33.
        ///
        /// <para><b>Three rows can fire on one dimmer, and that is intended rather than duplication.</b>
        /// `rs485-bus-installation` is one statement about the BUS the project puts something on;
        /// `rs485-dimmer-powerfail-level` is about how THIS dimmer is configured; this row is about the
        /// CONTROLLER FIRMWARE the installation runs. Three independent facts, and a reader who fixes one has
        /// not addressed the others. The test asserts all three together so a later reader meeting them on one
        /// device sees the intent recorded rather than filing it as noise.</para>
        ///
        /// <para><b>It narrows.</b> The bound is a vendor claim at 03.03.33, inclusive.</para>
        /// </summary>
        [Test]
        public void TheAffectedLedDimmerIsReportedPerInstanceAndNarrowsOnFirmware()
        {
            Project dimmer = Placed(("product_rs485_led_dimmer", "_0x4409"));

            ImmutableArray<string> Run(ValidationProfile profile) =>
                [.. new WholeProjectValidator(ProjectRules.Registered).Validate(dimmer, profile).Findings
                    .Select(f => f.Code.Value).Where(id => id == "rs485-dimmer-firmware-link-errors")];

            Assert.Multiple(() =>
            {
                Assert.That(Count(dimmer, "rs485-dimmer-firmware-link-errors"), Is.EqualTo(1));
                Assert.That(
                    Count(Placed(("product_rs485_led_dimmer", "_0x4409"),
                        ("product_rs485_led_dimmer", "_0x4409")), "rs485-dimmer-firmware-link-errors"),
                    Is.EqualTo(2), "OnePerOccurrence: each dimmer is its own device on the bus");
                Assert.That(Count(Placed(("product_dataline", "_0x2202")), "rs485-dimmer-firmware-link-errors"),
                    Is.Zero, "a product that is not the affected dimmer");
                Assert.That(Validate(dimmer).Findings
                    .Single(f => f.RuleId == "rs485-dimmer-firmware-link-errors").Severity,
                    Is.EqualTo(ValidationSeverity.Warning));

                // The deliberate neighbourliness, asserted rather than described.
                Assert.That(Validate(dimmer).Findings.Select(f => f.RuleId),
                    Does.Contain("rs485-bus-installation").And.Contain("rs485-dimmer-firmware-link-errors"),
                    "the bus row and the firmware row are independent statements about one device");

                Assert.That(Run(ValidationProfile.ProjectOnly), Is.Not.Empty, "no target declared");
                Assert.That(
                    Run(ValidationProfile.ProjectOnly with { Firmware = new ControllerFirmwareVersion(3, 3, 32) }),
                    Is.Not.Empty, "one release below the fix");
                Assert.That(
                    Run(ValidationProfile.ProjectOnly with { Firmware = new ControllerFirmwareVersion(3, 3, 33) }),
                    Is.Empty, "inclusive: 03.03.33 itself carries the claimed fix");
            });
        }

        /// <summary>
        /// A03: the catalogue's 3-key push button, on ONE field report of an upload that aborts partway and
        /// leaves the controller in <i>fejltilstand</i>.
        ///
        /// <para><b>Warning, and D25 settled that — this test does not re-argue it.</b> Section 8.1's first row
        /// would read Error (no fixed release is known, so no upgrade helps) and its third row Warning (a single
        /// reporter). The checkpoint took the third, because suppression is foreclosed: an Error would be
        /// permanent and undismissable for everyone whose installation demonstrably works, with not even a
        /// narrowing firmware context to escape through.</para>
        ///
        /// <para><b>The product is `_0x106` "Mini Modul 3 tryk", by measurement rather than by naming.</b> The
        /// catalogue holds two 3-key products, and the other — `_0x2132`, the FUGA <i>Betjeningstryk</i> — is
        /// the one the English name superficially suggests. The source topic decides it: the reporter fixed the
        /// installation by substituting <i>three separate 1-key push buttons</i>, and the FUGA family has no
        /// 1-key member at all (it runs 2/4/6), while `_0x104` "Mini Modul 1 tryk" is the only one in the
        /// catalogue. The swap is only possible inside the Mini Modul family. This test pins the neighbour so a
        /// later edit cannot quietly drift onto it.</para>
        ///
        /// <para><b>The recovery procedure stays out of the sentence.</b> The reporter needed factory-default
        /// firmware to clear the controller; that is installation advice, not a fact about this project.</para>
        /// </summary>
        [Test]
        public void TheThreeKeyPushButtonIsReportedPerInstanceAsAWarning()
        {
            Project button = Placed(("product_dataline", "_0x106"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(button, "product-3key-upload-abort"), Is.EqualTo(1));
                Assert.That(
                    Count(Placed(("product_dataline", "_0x106"), ("product_dataline", "_0x106")),
                        "product-3key-upload-abort"),
                    Is.EqualTo(2), "OnePerOccurrence: each placement is its own instance");
                Assert.That(Count(Placed(("product_dataline", "_0x2132")), "product-3key-upload-abort"),
                    Is.Zero, "the OTHER 3-key product is not the subject, and naming alone would have picked it");
                Assert.That(Count(Placed(("product_dataline", "_0x104")), "product-3key-upload-abort"),
                    Is.Zero, "nor is the 1-key member the reporter replaced it WITH");
                Assert.That(Validate(button).Findings
                    .Single(f => f.RuleId == "product-3key-upload-abort").Severity,
                    Is.EqualTo(ValidationSeverity.Warning), "D25, on section 8.1's third row");
                Assert.That(Message(button, "product-3key-upload-abort"),
                    Does.Not.Contain("fabriks").And.Not.Contain("gendan"),
                    "the factory-default recovery is installation advice and is not in the sentence");
            });
        }

        /// <summary>
        /// A handover or disaster-recovery plan that assumes the keypad's codes are backed up with the project is
        /// wrong: they live in the keypad itself, and neither the project file nor the controller holds them.
        ///
        /// <para><b>The source's recovery folklore stays out of the message</b> — a second keypad further along
        /// the daisy chain is installation advice, not a fact about this project, and the row's job is to correct
        /// an assumption rather than to tell an installer how to work.</para>
        /// </summary>
        [Test]
        public void EachCodeKeypadIsReportedAsHoldingItsOwnCodes()
        {
            Project keypad = Placed(("product_dataline", "_0x2111"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(keypad, "product-keypad-codes-local"), Is.EqualTo(1));
                Assert.That(
                    Count(Placed(("product_dataline", "_0x2111"), ("product_dataline", "_0x2111")),
                        "product-keypad-codes-local"),
                    Is.EqualTo(2), "OnePerOccurrence: two keypads hold two sets of codes");
                Assert.That(Count(Placed(("product_dataline", "_0x2110")), "product-keypad-codes-local"),
                    Is.Zero, "a neighbouring identifier is a different product");
                Assert.That(Message(keypad, "product-keypad-codes-local"), Does.Contain("Produkt 0"));
                Assert.That(Validate(keypad).Findings
                    .Single(f => f.RuleId == "product-keypad-codes-local").Severity,
                    Is.EqualTo(ValidationSeverity.Info));
                Assert.That(Message(keypad, "product-keypad-codes-local"), Does.Not.Contain("kæde")
                    .And.Not.Contain("næste tastatur"),
                    "the recovery folklore is installation advice and is not in the sentence");
            });
        }

        /// <summary>
        /// The same device is `migration-untested-product`'s subject too — it is one of the three alarm products
        /// that letter — so a placed keypad reports BOTH. Two independent statements about one device: where its
        /// codes live, and what becomes of it in a KNX conversion.
        /// </summary>
        [Test]
        public void AKeypadAlsoCarriesItsMigrationStatus()
        {
            Project keypad = Placed(("product_dataline", "_0x2111"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(keypad, "product-keypad-codes-local"), Is.EqualTo(1));
                Assert.That(Count(keypad, "migration-untested-product"), Is.EqualTo(1),
                    "_0x2111 is in the alarm group of the not-currently-convertible set");
            });
        }

        // ── product-pir-alarm-polarity ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Reusing an alarm-grade PIR for lighting silently inverts the trigger sense: it BREAKS its output on
        /// motion — normally-closed by design — which is the opposite of what a lighting block expects.
        ///
        /// <para><b>The ordinary PIR is the load-bearing exclusion, and it is not hypothetical:</b> `_0x210e` is
        /// in the committed corpus (Project1 and project3). A rule that read "any PIR" would report authentic
        /// vendor-authored files, so the negative is asserted here as deliberately as the positive.</para>
        /// </summary>
        [Test]
        public void TheAlarmPirIsReportedAndTheOrdinaryPirIsNot()
        {
            Project alarmPir = Placed(("product_dataline", "_0x210f"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(alarmPir, "product-pir-alarm-polarity"), Is.EqualTo(1));
                Assert.That(Count(Placed(("product_dataline", "_0x210e")), "product-pir-alarm-polarity"),
                    Is.Zero, "normally-open is the EXPECTED case, and this identifier is in the corpus");
                Assert.That(
                    Count(Placed(("product_dataline", "_0x210f"), ("product_dataline", "_0x210e")),
                        "product-pir-alarm-polarity"),
                    Is.EqualTo(1), "one of each reports exactly the alarm one");
                Assert.That(Message(alarmPir, "product-pir-alarm-polarity"), Does.Contain("Produkt 0"));
                Assert.That(Validate(alarmPir).Findings
                    .Single(f => f.RuleId == "product-pir-alarm-polarity").Severity,
                    Is.EqualTo(ValidationSeverity.Info));
            });
        }

        /// <summary>
        /// The sentence states the polarity and the consequence, and nothing else. The source's lag and
        /// daisy-chain clauses are installation advice — true of the hardware, but not a fact this project can be
        /// read for — and carrying them would turn a one-line correction into a commissioning note.
        /// </summary>
        [Test]
        public void ThePirSentenceCarriesThePolarityAndNotTheInstallationAdvice()
        {
            string message = Message(Placed(("product_dataline", "_0x210f")), "product-pir-alarm-polarity");

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("bryder sit signal"),
                    "the polarity: it BREAKS the signal on motion");
                Assert.That(message, Does.Contain("inverteres"),
                    "and the consequence the reader acts on, in the program");
                Assert.That(message, Does.Not.Contain("kæde").And.Not.Contain("forsinkelse"),
                    "the daisy-chain and lag clauses are installation advice and stay out");
            });
        }

        // ── product-sensor-pulse-input ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The smart sensor is not an analog input: it encodes its reading as a timed pulse train on a plain 24 V
        /// line, and pairing it with the older 24/24 module silently fails because that module does not speak the
        /// pulse protocol.
        ///
        /// <para><b>The row informs about the REQUIREMENT and does not check compliance</b>, because the file
        /// cannot see which physical input module a sensor lands on — the documentation modules are optional and
        /// carry no such binding. A rule that pretended otherwise would be asserting a wiring fact from a file
        /// that does not contain it.</para>
        /// </summary>
        [Test]
        public void EachSmartSensorIsReportedAsNeedingThePulseCapableModule()
        {
            Assert.Multiple(() =>
            {
                foreach (string identifier in new[]
                    { "_0x2124", "_0x2125", "_0x2135", "_0x2138", "_0x2136", "_0x2139" })
                {
                    Assert.That(Count(Placed(("product_dataline", identifier)), "product-sensor-pulse-input"),
                        Is.EqualTo(1), identifier);
                }

                Assert.That(Count(Placed(("product_dataline", "_0x2101")), "product-sensor-pulse-input"),
                    Is.Zero, "an ordinary pushbutton is not a smart sensor");
                Assert.That(Count(Placed(("product_dataline", "_0x210e")), "product-sensor-pulse-input"),
                    Is.Zero, "and neither is a PIR, which signals on/off rather than a reading");
            });
        }

        /// <summary>
        /// TWO ROWS, ONE DEVICE, AND THAT IS THE POINT. These six identifiers are also
        /// `migration-untested-product`'s sensor group, so a placed smart sensor reports both — what it needs in
        /// order to work at all, and what becomes of it in a KNX conversion. Neither statement contains the other,
        /// and a reader planning a conversion needs a different one from a reader wiring a module.
        /// </summary>
        [Test]
        public void ASmartSensorReportsBothItsWiringRequirementAndItsMigrationStatus()
        {
            Project sensor = Placed(("product_dataline", "_0x2139"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(sensor, "product-sensor-pulse-input"), Is.EqualTo(1));
                Assert.That(Count(sensor, "migration-untested-product"), Is.EqualTo(1));
                Assert.That(Validate(sensor).Findings
                    .Where(f => f.RuleId is "product-sensor-pulse-input" or "migration-untested-product")
                    .Select(f => f.Category).Distinct(),
                    Is.EquivalentTo(new[] { ValidationCategory.Addressing, ValidationCategory.ProjectStructure }),
                    "and they file under different categories, which is what keeps them legible as two facts");
            });
        }

        // ── controller-link-budget ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// A design needing a MEASUREMENT on the other controller needs a different mechanism entirely, and the
        /// terminal cost of the Controller Link is paid whether the signals are used or not.
        /// </summary>
        [Test]
        public void AControllerLinkProductIsReportedWithItsSignalBudget()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Placed(("product_dataline", "_0x2704")), "controller-link-budget"),
                    Is.EqualTo(1), "the OUT half");
                Assert.That(Count(Placed(("product_dataline", "_0x2705")), "controller-link-budget"),
                    Is.EqualTo(1), "and the IN half");
                Assert.That(
                    Count(Placed(("product_dataline", "_0x2704"), ("product_dataline", "_0x2705"),
                            ("product_dataline", "_0x2704")),
                        "controller-link-budget"),
                    Is.EqualTo(1),
                    "OneFinding: the budget is the LINK's, and a full direction is two OUT products against one IN");
                Assert.That(Count(Placed(("product_dataline", "_0x2101")), "controller-link-budget"), Is.Zero);
                Assert.That(Message(Placed(("product_dataline", "_0x2704")), "controller-link-budget"),
                    Does.Contain("16"), "the budget, bound from the declared threshold");
            });
        }

        /// <summary>
        /// THE SENTENCE STOPS SHORT OF THE TERMINAL FIGURE, deliberately. "16 inputs and 16 outputs on each
        /// controller" holds only once a direction is FULLY POPULATED — two OUT products against one IN, since an
        /// input module has 16 inputs and an output module 8 — and the file cannot tell the reader whether it is.
        ///
        /// <para>An earlier draft also wrote <i>"optager tilsvarende faste ind- og udgange"</i>, asserting a
        /// symmetry the two products do not have: the OUT def declares 8 outputs and the IN def 16 inputs. The
        /// sentence says the terminals are occupied WITHOUT quantifying them, and this test holds it there.</para>
        /// </summary>
        [Test]
        public void TheLinkSentenceOccupiesTerminalsWithoutCountingThem()
        {
            string message = Message(Placed(("product_dataline", "_0x2705")), "controller-link-budget");

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("faste ind- og udgange"),
                    "the terminals are occupied, which is the cost the reader pays");
                Assert.That(message, Does.Not.Contain("tilsvarende"),
                    "but not SYMMETRICALLY — the two products declare 8 and 16 respectively");
                Assert.That(message, Does.Contain("analoge"),
                    "and the link cannot carry an analog value at all, which is the design consequence");
                Assert.That(System.Text.RegularExpressions.Regex.Matches(message, @"\d+"), Has.Count.EqualTo(1),
                    "exactly ONE number in the sentence: the signal budget. A terminal count would be the "
                    + "figure that only holds on a fully populated direction");
            });
        }

        /// <summary>
        /// The number is declared data and graded <c>Authored</c> — community topics corroborated by module
        /// arithmetic, not a vendor publication — and its evidence carries that arithmetic so a reader can check
        /// the reasoning rather than take the 16 on trust.
        /// </summary>
        [Test]
        public void TheLinkBudgetIsDeclaredWithItsModuleArithmetic()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("controller-link-budget"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == "LinkSignalsPerDirection");

            Assert.Multiple(() =>
            {
                Assert.That(declared.Value, Is.EqualTo(16));
                Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.Authored));
                Assert.That(declared.Evidence, Does.Contain("TODO"),
                    "an authored threshold carries its unconfirmed status where it is declared");
                Assert.That(declared.Evidence, Does.Contain("8"),
                    "and the module arithmetic behind it: 16 inputs per input module, 8 outputs per output one");
                Assert.That(entry.RequiresControllerLimits, Is.False,
                    "the budget belongs to the LINK PRODUCT, not to a declared controller capability");
            });
        }

        // ── rs485-dimmer-powerfail-level ────────────────────────────────────────────────────────────

        /// <summary>
        /// A program that assumes "off after an outage" has to assert it explicitly here — the reverse of what
        /// every other output does. The LED dimmer does not retain on/off across a longer power failure; its
        /// channels come back at the configured level.
        ///
        /// <para><b>No exclusion, and none is possible:</b> the behaviour is a property of the PRODUCT, not of a
        /// setting the file could inspect, so every placed dimmer reports.</para>
        /// </summary>
        [Test]
        public void EveryLedDimmerIsReportedAsNotRetainingItsOnOffState()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmers(1), "rs485-dimmer-powerfail-level"), Is.EqualTo(1));
                Assert.That(Count(Dimmers(3), "rs485-dimmer-powerfail-level"), Is.EqualTo(3),
                    "OnePerOccurrence: each dimmer is its own surprise");
                Assert.That(Count(OrdinaryInput(), "rs485-dimmer-powerfail-level"), Is.Zero);
                Assert.That(Validate(Dimmers(1)).Findings
                    .Single(f => f.RuleId == "rs485-dimmer-powerfail-level").Severity,
                    Is.EqualTo(ValidationSeverity.Info));
            });
        }

        /// <summary>
        /// THE FACTORY LEVEL IS BOUND FROM THE THRESHOLD, never written into the template. It is a vendor number,
        /// and every other numeric row in this set binds one — an earlier draft hard-coded the 100 into the
        /// Danish sentence, which puts the same fact in two places that can then disagree.
        ///
        /// <para>Note what the grade does NOT claim: the threshold is <c>VendorDocumented</c> because the vendor
        /// publishes the factory default, not because anything here compares against it. The predicate compares
        /// nothing at all — every dimmer reports.</para>
        /// </summary>
        [Test]
        public void TheFactoryLevelIsBoundFromTheDeclaredThreshold()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("rs485-dimmer-powerfail-level"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == "Rs485DimmerPowerfailLevel");

            Assert.Multiple(() =>
            {
                Assert.That(declared.Value, Is.EqualTo(100));
                Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.VendorDocumented));
                Assert.That(entry.MessageTemplate, Does.Contain("{level}").And.Not.Contain("100"),
                    "the template carries the PLACEHOLDER, not the number");
                Assert.That(Message(Dimmers(1), "rs485-dimmer-powerfail-level"), Does.Contain("100"),
                    "and the rendered sentence carries the number, bound from the declaration");
            });
        }

        // ── rs485-bus-installation ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// The RS-485 bus has installation rules no part of the file records, and sporadic dimmer log entries
        /// usually mean cabling rather than a failing module. ONE finding for the project: there is one bus.
        /// </summary>
        [Test]
        public void AnyRs485BusProductBringsTheInstallationRulesWithIt()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmers(1), "rs485-bus-installation"), Is.EqualTo(1));
                Assert.That(Count(Dimmers(4), "rs485-bus-installation"), Is.EqualTo(1),
                    "OneFinding: four dimmers sit on one bus");
                Assert.That(Count(Placed(("product_rs485_sms_modem", "_0x6101")), "rs485-bus-installation"),
                    Is.EqualTo(1), "the SMS modem is on the bus too, and brings the same rules");
                Assert.That(Count(OrdinaryInput(), "rs485-bus-installation"), Is.Zero,
                    "a project with nothing on the bus has no bus to install");
            });
        }

        /// <summary>
        /// THE VOICE MODEM IS ON THE SAME BUS, and this row and <c>capacity-rs485-exceeded</c> have to agree
        /// about that: the breach row counts a voice modem towards the ceiling, so a project whose only bus
        /// device is one is a project measured against a rule it was never told.
        ///
        /// <para>Naming the two families this row was first written for left exactly that project silent about
        /// termination, shielding and the ceiling itself. Both rows now read one population, so the pair cannot
        /// drift again.</para>
        /// </summary>
        [Test]
        public void TheVoiceModemBringsTheSameBusRulesAsTheOtherTwoFamilies()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(VoiceModem(), "rs485-bus-installation"), Is.EqualTo(1),
                    "one bus device is one bus, whichever family it belongs to");
                Assert.That(Message(VoiceModem(), "rs485-bus-installation"), Does.Contain("modstand"),
                    "and it is the same statement, not a reduced one");
            });
        }

        /// <summary>
        /// TERMINATION IS A DISJUNCTION, and dropping half of it is the mistake this test exists to prevent. The
        /// vendor writes: place the SMS modem last IF ONE EXISTS, <b>or</b> fit a resistor.
        ///
        /// <para>An earlier draft dropped the SMS branch "to keep one sentence" while keeping the SMS modem in
        /// this row's own trigger — so an SMS-modem project was told to fit a resistor the vendor says it does
        /// not need. Both branches are in the sentence, and both are asserted here.</para>
        /// </summary>
        [Test]
        public void TheTerminationSentenceKeepsBothBranches()
        {
            string message = Message(Dimmers(1), "rs485-bus-installation");

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("SMS-modulets indbyggede terminering"),
                    "the branch an SMS-modem project takes");
                Assert.That(message, Does.Contain("modstand"),
                    "and the branch every other project takes");
                Assert.That(message, Does.Contain("eller"), "stated as a choice, which is what the vendor wrote");
            });
        }

        /// <summary>
        /// THE 10 m GOVERNS BONDING THE SHIELD, NOT WHETHER THE CABLE IS SHIELDED. The vendor requires shielded
        /// cable for this bus unconditionally; what the length threshold decides is whether the shield is
        /// connected to the supply's 0 V.
        ///
        /// <para>An earlier draft inverted that — "over about 10 metres requires shielded cable" — and added an
        /// earth-at-one-end rule the vendor document does not state. The sentence must not make shielding
        /// conditional, and this test says so on the words.</para>
        /// </summary>
        [Test]
        public void TheShieldClauseBondsTheShieldRatherThanRequiringOne()
        {
            string message = Message(Dimmers(1), "rs485-bus-installation");

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("kabelskærmen").And.Contain("0 V"),
                    "what the length threshold governs is BONDING the shield to the supply's 0 V");
                Assert.That(message, Does.Not.Contain("skærmet kabel"),
                    "shielded cable is required unconditionally, so the sentence must not make it conditional");
                Assert.That(message, Does.Not.Contain("kun én ende"),
                    "and the earth-at-one-end rule is the community's wording, not the vendor's");
            });
        }

        /// <summary>
        /// All three numbers are declared data, all three are the vendor's own, and the slots run in the order
        /// the sentence first uses them.
        /// </summary>
        [Test]
        public void TheThreeBusNumbersAreDeclaredAndOrderedByFirstAppearance()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("rs485-bus-installation"),
                out ProblemCatalogEntry entry), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entry.Thresholds.Single(t => t.Name == "Rs485MaxComponents").Value, Is.EqualTo(32));
                Assert.That(entry.Thresholds.Single(t => t.Name == "Rs485TerminationOhm").Value, Is.EqualTo(120));
                Assert.That(entry.Thresholds.Single(t => t.Name == "Rs485ShieldBondFromMeters").Value,
                    Is.EqualTo(10));
                Assert.That(entry.Thresholds.Select(t => t.Confidence),
                    Has.All.EqualTo(ThresholdConfidence.VendorDocumented),
                    "all three come from the product's own vendor documentation");
                Assert.That(entry.Slots.Select(s => s.Name),
                    Is.EqualTo(new[] { "maxdevices", "termination", "shieldlength" }).AsCollection,
                    "declared order is the template's first-appearance order");
            });
        }

        /// <summary>
        /// THE DELIBERATE OVERLAP WITH `capacity-rs485-exceeded`. This row publishes the 32 as a FACT whenever an
        /// RS-485 product exists; that row reports only when the count passes it. Both fire on an over-limit
        /// project, and that is intended — one states the rule, the other states the breach.
        /// </summary>
        [Test]
        public void TheBusRulesAndTheCapacityBreachAreTwoDifferentStatements()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmers(1), "rs485-bus-installation"), Is.EqualTo(1));
                Assert.That(Count(Dimmers(1), "capacity-rs485-exceeded"), Is.Zero,
                    "one dimmer is nowhere near the limit, but the rules still apply");
                Assert.That(Count(Dimmers(33), "rs485-bus-installation"), Is.EqualTo(1));
                Assert.That(Count(Dimmers(33), "capacity-rs485-exceeded"), Is.EqualTo(1),
                    "and over the limit both fire — the rule and the breach");
            });
        }

        /// <summary>
        /// ONE CEILING BEHIND TWO ROWS. The two thresholds are separate declarations on purpose — each row is
        /// read on its own and each rule reads its OWN entry — but they are two names for ONE physical fact, and
        /// the declarations used to hold the figure twice while the evidence claimed they agreed "by
        /// construction". They now bind one constant, which is what makes that claim true.
        ///
        /// <para><b>What would break without this.</b> Raising one alone is not a smaller change than raising
        /// both: it makes the row that PUBLISHES the ceiling publish a number the row that ENFORCES it does not
        /// use, so a project is told one limit and measured against another. The last assertion is the one that
        /// catches it — the first two would still pass with the pair split apart.</para>
        /// </summary>
        [Test]
        public void TheBusCeilingIsOneNumberUnderTwoDeclaredNames()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("rs485-bus-installation"),
                out ProblemCatalogEntry published), Is.True);
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("capacity-rs485-exceeded"),
                out ProblemCatalogEntry enforced), Is.True);

            double publishedMax = published.Thresholds.Single(t => t.Name == "Rs485MaxComponents").Value;
            double enforcedMax = enforced.Thresholds.Single(t => t.Name == "MaximumRs485Components").Value;

            Assert.Multiple(() =>
            {
                Assert.That(publishedMax, Is.EqualTo(32), "the vendor's figure for the bus");
                Assert.That(enforcedMax, Is.EqualTo(32));
                Assert.That(publishedMax, Is.EqualTo(enforcedMax),
                    "the ceiling this project is TOLD about and the ceiling it is MEASURED against are the same "
                    + "bus; raising one without the other publishes a limit nothing enforces");
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static Project InLocality(params ProjectElement[] contents) =>
            Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], contents)));

        /// <summary>
        /// The given number of S0 meters, each shaped as the vendor writes one: the catalog product identifier and
        /// a pulse count inside the declared range, so no addressing row fires alongside the one under test.
        /// </summary>
        private static Project Meters(int count) =>
            InLocality(
            [
                .. Enumerable.Range(0, count).Select(i => Tree.Node("s0_device", Token("s0_device", 0x60 + i),
                    [("product_identifier", "_0x2313"), ("name", $"Måler {i + 1}"), ("ticks", "100")])),
            ]);

        /// <summary>
        /// The given number of IHC Wireless products, each commissioned — a serial number that is neither blank
        /// nor the null token — so no addressing row fires alongside the one under test.
        /// </summary>
        private static Project Wireless(int count) =>
            InLocality(
            [
                .. Enumerable.Range(0, count).Select(i => Tree.Node("product_airlink",
                    Token("product_airlink", 0x60 + i),
                    [("product_identifier", "_0x4304"), ("name", $"Trådløs {i}"),
                     ("serialnumber", $"_0xaa{i:00}")])),
            ]);

        /// <summary>
        /// The given products, each as its (root element tag, <c>product_identifier</c>) pair — the key D11
        /// makes every product predicate use, because an identifier alone is not unique in this catalogue.
        /// </summary>
        /// <param name="products">The (tag, identifier) pairs to place, in order.</param>
        private static Project Placed(params (string Tag, string Identifier)[] products) =>
            InLocality(
            [
                .. products.Select((p, i) => Tree.Node(p.Tag, Token(p.Tag, 0x60 + i),
                    p.Tag == "product_airlink"
                        ? [("product_identifier", p.Identifier), ("name", $"Produkt {i}"),
                           ("serialnumber", $"_0xaa{i:00}")]
                        : [("product_identifier", p.Identifier), ("name", $"Produkt {i}")])),
            ]);

        /// <summary>The given number of RS-485 LED dimmers, each a bare product root.</summary>
        private static Project Dimmers(int count) =>
            InLocality(
            [
                .. Enumerable.Range(0, count).Select(i => Tree.Node("product_rs485_led_dimmer",
                    Token("product_rs485_led_dimmer", 0x60 + i),
                    [("product_identifier", "_0x9e10"), ("name", $"Dæmper {i}")])),
            ]);

        /// <summary>
        /// A voice modem, tagged as the committed corpus writes one. <c>product_rs485_modem</c> has no
        /// <see cref="TypeCode"/> of its own — the classifier recognises the family, the catalog ships no such
        /// product — so its id is minted from the SMS modem's, exactly as the synthetic corpus case does.
        /// </summary>
        private static Project VoiceModem() =>
            InLocality(Tree.Node("product_rs485_modem", Token("product_rs485_sms_modem", 0x60),
                [("product_identifier", "_0x6001"), ("name", "Talemodem")]));

        /// <summary>A data-line product carrying an ordinary input terminal — no S0 device anywhere.</summary>
        private static Project OrdinaryInput() =>
            InLocality(Tree.Node("product_dataline", Token("product_dataline", 0x51),
                [("product_identifier", "_0x2101"), ("name", "Tryk")],
                Tree.Node("dataline_input", Token("dataline_input", 0x52),
                    [("name", "Tryk"), ("address_dataline", "_0x101")])));
    }
}
