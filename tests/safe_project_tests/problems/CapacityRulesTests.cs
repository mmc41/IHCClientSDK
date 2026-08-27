using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T059 — the CAPACITY rows, tested the only way they can be: with a DECLARED LOW LIMIT rather than a
    /// giant fixture.
    ///
    /// <para><b>Why a low limit and not a big project.</b> The address encoding itself caps a data line at 8 (input)
    /// or 16 (output), so exceeding the datasheet figures is not expressible in a <c>.vis</c> file at all; and 64
    /// wireless products or a full resource table would make the suite slow for nothing. Declaring a profile that
    /// says "this controller holds two modules" tests the same predicate at the same three points — below, at, and
    /// above — which is what the gate asks for.</para>
    ///
    /// <para><b>The property that matters more than any boundary</b> is
    /// <see cref="NoCapacityRowFiresWithoutADeclaredProfile"/>: all but one of these rows must be SKIPPED when
    /// no controller is named, because the same project must not be valid on one workstation and invalid on
    /// another. The exception, the modem row, must fire anyway — its limit is one, and it is not a
    /// capability.</para>
    /// </summary>
    [TestFixture]
    public sealed class CapacityRulesTests
    {
        /// <summary>A profile naming a deliberately small controller, so the boundaries are reachable.</summary>
        private static ValidationProfile Profile(
            int inputModules = 8, int outputModules = 16, int addresses = 128, int wireless = 64,
            int resources = 2000, int linksPerUnit = 32, int linksPerCombi = 64,
            int scenariosPerReceiver = 32) =>
            ValidationProfile.Categorized with
            {
                Controller = new ControllerCapabilityLimits(
                    inputModules, outputModules, addresses, wireless, resources, linksPerUnit, linksPerCombi,
                    scenariosPerReceiver),
            };

        private static int Count(Project project, ValidationProfile profile, string ruleId) =>
            ProjectVerification.Run(project, profile).Findings.Count(f => f.RuleId == ruleId);

        private static ProjectValidationFinding Single(Project project, ValidationProfile profile, string ruleId) =>
            ProjectVerification.Run(project, profile).Findings.Single(f => f.RuleId == ruleId);

        // ── capacity-input-modules / capacity-output-modules / capacity-addresses ────────────────────

        /// <summary>
        /// The three quantities are three rows (D2). They were one, <c>capacity-modules-exceeded</c>, and its one
        /// Danish sentence said "moduler" for all three — so a terminals overflow reported "uses 200 of 128
        /// modules". The old row is retired rather than deleted, and its id stays reserved.
        /// </summary>
        [Test]
        public void InputModulesAreReportedOnlyAboveTheDeclaredLimit()
        {
            ValidationProfile twoModules = Profile(inputModules: 2);

            Assert.Multiple(() =>
            {
                Assert.That(Count(Terminals(isOutput: false, lines: 1), twoModules, "capacity-input-modules"),
                    Is.Zero, "below");
                Assert.That(Count(Terminals(isOutput: false, lines: 2), twoModules, "capacity-input-modules"),
                    Is.Zero, "AT the limit is not exceeding it");
                Assert.That(Count(Terminals(isOutput: false, lines: 3), twoModules, "capacity-input-modules"),
                    Is.EqualTo(1), "above");
                Assert.That(Single(Terminals(isOutput: false, lines: 3), twoModules, "capacity-input-modules")
                    .Severity, Is.EqualTo(ValidationSeverity.Error), "the catalogue rates this one an Error");
                Assert.That(Single(Terminals(isOutput: false, lines: 3), twoModules, "capacity-input-modules")
                    .Message, Is.EqualTo("Projektet bruger 3 af 2 indgangsmoduler."),
                    "and the sentence names the direction it counted");
                Assert.That(Count(Terminals(isOutput: false, lines: 3), twoModules, "capacity-output-modules"),
                    Is.Zero, "the output row is a different row and does not fire on input lines");
            });
        }

        [Test]
        public void OutputModulesHaveTheirOwnLimit()
        {
            ValidationProfile profile = Profile(inputModules: 8, outputModules: 1);

            Assert.Multiple(() =>
            {
                Assert.That(Count(Terminals(isOutput: true, lines: 1), profile, "capacity-output-modules"),
                    Is.Zero, "at the output limit");
                Assert.That(Count(Terminals(isOutput: true, lines: 2), profile, "capacity-output-modules"),
                    Is.EqualTo(1), "above it");
                Assert.That(Single(Terminals(isOutput: true, lines: 2), profile, "capacity-output-modules")
                    .Message, Is.EqualTo("Projektet bruger 2 af 1 udgangsmoduler."));
                Assert.That(Count(Terminals(isOutput: false, lines: 3), profile, "capacity-input-modules"),
                    Is.Zero, "three INPUT lines are within the input limit of eight");
            });
        }

        [Test]
        public void AddressedTerminalsPerDirectionAreTheirOwnRowWithTheirOwnUnit()
        {
            ValidationProfile profile = Profile(addresses: 3);

            Assert.Multiple(() =>
            {
                Assert.That(Count(Terminals(isOutput: false, lines: 1, perLine: 3), profile,
                    "capacity-input-addresses"), Is.Zero, "AT the address limit");
                Assert.That(Count(Terminals(isOutput: false, lines: 1, perLine: 4), profile,
                    "capacity-input-addresses"), Is.EqualTo(1),
                    "four addressed terminals on one line is one module but four addresses");
                Assert.That(Single(Terminals(isOutput: false, lines: 1, perLine: 4), profile,
                    "capacity-input-addresses").Message,
                    Is.EqualTo("Projektet bruger 4 af 3 indgangsklemmer."),
                    "KLEMMER, not moduler — the wrong unit is what forced the first split; and the DIRECTION is "
                    + "in the sentence, which is what forced the second");
                Assert.That(Count(Terminals(isOutput: true, lines: 1, perLine: 4), profile,
                    "capacity-output-addresses"), Is.EqualTo(1),
                    "the output direction is its own row, so the two can never be told apart by number alone");
            });
        }

        /// <summary>
        /// A project over BOTH its module limit and its address limit reports both. The retired row could not:
        /// its terminals check sat behind an <c>else if</c> on the module check, so the reader repaired one fault
        /// to discover the other.
        /// </summary>
        [Test]
        public void ModulesAndAddressesAreReportedIndependently()
        {
            ValidationProfile tight = Profile(inputModules: 1, addresses: 3);
            Project over = Terminals(isOutput: false, lines: 2, perLine: 4);

            Assert.Multiple(() =>
            {
                Assert.That(Count(over, tight, "capacity-input-modules"), Is.EqualTo(1), "two lines over one");
                Assert.That(Count(over, tight, "capacity-input-addresses"), Is.EqualTo(1), "eight terminals over three");
            });
        }

        /// <summary>The split id is reserved, never re-used and implemented by nothing.</summary>
        [Test]
        public void TheOldCombinedRowIsRetiredAndKeepsItsIdReserved()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("capacity-modules-exceeded"),
                out ProblemCatalogEntry retired), Is.True, "the id stays occupied");

            Assert.Multiple(() =>
            {
                Assert.That(retired.Status, Is.EqualTo(ProblemCodeStatus.Retired));
                Assert.That(retired.MessageTemplate, Is.Empty, "a retired row renders nothing");
                Assert.That(Count(Terminals(isOutput: false, lines: 3), Profile(inputModules: 1),
                    "capacity-modules-exceeded"), Is.Zero, "and nothing emits it");
            });
        }

        // ── capacity-wireless-exceeded ──────────────────────────────────────────────────────────────

        [Test]
        public void WirelessProductsAreReportedOnlyAboveTheRecommendation()
        {
            ValidationProfile two = Profile(wireless: 2);

            Assert.Multiple(() =>
            {
                Assert.That(Count(Wireless(1), two, "capacity-wireless-exceeded"), Is.Zero, "below");
                Assert.That(Count(Wireless(2), two, "capacity-wireless-exceeded"), Is.Zero, "at");
                Assert.That(Count(Wireless(3), two, "capacity-wireless-exceeded"), Is.EqualTo(1), "above");
                Assert.That(Single(Wireless(3), two, "capacity-wireless-exceeded").Severity,
                    Is.EqualTo(ValidationSeverity.Warning),
                    "the vendor states a RECOMMENDATION for response time, and the devices do bind");
                Assert.That(Single(Wireless(3), two, "capacity-wireless-exceeded").Message,
                    Is.EqualTo("Projektet har 3 trådløse produkter; anbefalingen er højst 2."));
            });
        }

        // ── capacity-wireless-links-per-unit ────────────────────────────────────────────────────────

        /// <summary>
        /// C19: more links on one wireless unit than the controller carries — 32 ordinarily, 64 on a combi unit.
        ///
        /// <para><b>The one Phase 7 row with an ENABLING posture, and section 8.4 says so outright.</b> Every
        /// other row in the phase is an erratum whose condition is in the file, so it reports with no context
        /// and a firmware target may only withhold it. This one is not an erratum at all: the ceiling is a
        /// CONTROLLER CAPABILITY, so with no controller declared there is no ceiling to be over and the row is
        /// absent rather than guessing at one.</para>
        ///
        /// <para><b>Warning, like its sibling `capacity-wireless-exceeded`.</b> The vendor states a
        /// recommendation rather than a refusal, and the field evidence is contradictory — degradation is
        /// reported at counts well below the published ceiling. An Error's consequence has to hold whatever the
        /// author intended, and a slow-but-working installation does not qualify.</para>
        /// </summary>
        [Test]
        public void LinksOnOneWirelessUnitAreReportedOnlyAboveTheCeiling()
        {
            ValidationProfile three = Profile(linksPerUnit: 3);

            Assert.Multiple(() =>
            {
                Assert.That(Count(LinkedUnit(2), three, "capacity-wireless-links-per-unit"), Is.Zero, "below");
                Assert.That(Count(LinkedUnit(3), three, "capacity-wireless-links-per-unit"), Is.Zero, "at");
                Assert.That(Count(LinkedUnit(4), three, "capacity-wireless-links-per-unit"), Is.EqualTo(1),
                    "above");
                Assert.That(Single(LinkedUnit(4), three, "capacity-wireless-links-per-unit").Severity,
                    Is.EqualTo(ValidationSeverity.Warning),
                    "a recommendation, and the field evidence is contradictory");
                Assert.That(Single(LinkedUnit(4), three, "capacity-wireless-links-per-unit").Message,
                    Does.Contain("4").And.Contain("3"));
            });
        }

        /// <summary>
        /// A COMBI unit carries its own, higher ceiling. The two numbers are declared separately rather than one
        /// derived from the other, so this asserts a combi unit at a count over the ordinary ceiling but under
        /// its own is silent — which is the whole reason the second member exists.
        /// </summary>
        [Test]
        public void ACombiUnitIsMeasuredAgainstItsOwnHigherCeiling()
        {
            ValidationProfile split = Profile(linksPerUnit: 3, linksPerCombi: 6);

            Assert.Multiple(() =>
            {
                Assert.That(Count(LinkedUnit(4, combi: true), split, "capacity-wireless-links-per-unit"),
                    Is.Zero, "over the ordinary ceiling but under the combi one");
                Assert.That(Count(LinkedUnit(6, combi: true), split, "capacity-wireless-links-per-unit"),
                    Is.Zero, "at its own ceiling");
                Assert.That(Count(LinkedUnit(7, combi: true), split, "capacity-wireless-links-per-unit"),
                    Is.EqualTo(1), "and above it");
                Assert.That(Count(LinkedUnit(4), split, "capacity-wireless-links-per-unit"), Is.EqualTo(1),
                    "while the ordinary unit at the same count IS over — the two ceilings are independent");
            });
        }

        /// <summary>
        /// PER UNIT, and absent without a controller. The first half is why the shape is
        /// <c>OnePerOccurrence</c>: two overloaded units are two units to re-plan. The second is the enabling
        /// posture — no declared controller means no ceiling, so nothing is reported rather than a default being
        /// assumed.
        /// </summary>
        [Test]
        public void EachOverloadedUnitReportsAndNoneDoWithoutAControllerProfile()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(TwoLinkedUnits(4), Profile(linksPerUnit: 3),
                    "capacity-wireless-links-per-unit"), Is.EqualTo(2),
                    "OnePerOccurrence: each unit is its own thing to re-plan");
                Assert.That(Count(LinkedUnit(400), ValidationProfile.ProjectOnly,
                    "capacity-wireless-links-per-unit"), Is.Zero,
                    "no controller declared, so there is no ceiling to be over — the row is ABSENT, not lenient");
                Assert.That(ValidationProfile.ProjectOnly.Controller, Is.Null, "non-vacuity for the case above");
            });
        }

        // ── capacity-scenarios-per-receiver ─────────────────────────────────────────────────────────

        /// <summary>
        /// C19's other half: more scenarios bound to one receiver than the controller carries.
        ///
        /// <para><b>A RECEIVER is a wireless product that owns a `scenes` container</b>, which is a definition
        /// the file decides rather than a product list to keep current. A wireless unit with no such container
        /// cannot be commanded into a scene at all, so it is not a receiver and has no ceiling to be over — the
        /// corpus carries one such product, so the distinction is measured rather than hypothetical.</para>
        ///
        /// <para><b>Counted in SCENE MEMBER ROWS, not containers.</b> A receiver with two channels has two
        /// containers and can still be in one scenario; what the controller carries is the number of scenarios,
        /// which is the number of rows across all of them.</para>
        /// </summary>
        [Test]
        public void ScenariosOnOneReceiverAreReportedOnlyAboveTheCeiling()
        {
            ValidationProfile three = Profile(scenariosPerReceiver: 3);

            Assert.Multiple(() =>
            {
                Assert.That(Count(Receiver(2), three, "capacity-scenarios-per-receiver"), Is.Zero, "below");
                Assert.That(Count(Receiver(3), three, "capacity-scenarios-per-receiver"), Is.Zero, "at");
                Assert.That(Count(Receiver(4), three, "capacity-scenarios-per-receiver"), Is.EqualTo(1), "above");
                Assert.That(Single(Receiver(4), three, "capacity-scenarios-per-receiver").Severity,
                    Is.EqualTo(ValidationSeverity.Warning));
                Assert.That(Single(Receiver(4), three, "capacity-scenarios-per-receiver").Message,
                    Does.Contain("4").And.Contain("3"));
            });
        }

        /// <summary>
        /// The two halves of the definition: rows are summed ACROSS a receiver's containers, and a wireless
        /// product with no container at all is not a receiver.
        /// </summary>
        [Test]
        public void ScenariosAreSummedAcrossAReceiversContainersAndANonReceiverIsExempt()
        {
            ValidationProfile three = Profile(scenariosPerReceiver: 3);

            Assert.Multiple(() =>
            {
                Assert.That(Count(Receiver(4, containers: 2), three, "capacity-scenarios-per-receiver"),
                    Is.EqualTo(1),
                    "two rows in each of two containers is four scenarios on one receiver, not two");
                Assert.That(Count(Receiver(0), three, "capacity-scenarios-per-receiver"), Is.Zero,
                    "an empty container is a receiver with no scenarios");
                Assert.That(Count(NonReceiver(), ValidationProfile.Categorized with
                {
                    Controller = new ControllerCapabilityLimits(8, 16, 128, 64, 2000, 32, 64, 0),
                }, "capacity-scenarios-per-receiver"), Is.Zero,
                    "a wireless product owning NO scenes container cannot be commanded into a scene, so even a "
                    + "ceiling of zero leaves it alone — it is not a receiver");
            });
        }

        [Test]
        public void TheScenarioCeilingIsAbsentWithoutAControllerProfile()
        {
            Assert.That(Count(Receiver(400), ValidationProfile.ProjectOnly,
                "capacity-scenarios-per-receiver"), Is.Zero,
                "no controller declared, so there is no ceiling to be over");
        }

        // ── capacity-modem-multiple ─────────────────────────────────────────────────────────────────

        [Test]
        public void ASecondModemIsAnErrorWithOrWithoutAProfile()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Modems(1), Profile(), "capacity-modem-multiple"), Is.Zero,
                    "one modem is what the controller binds");
                Assert.That(Count(Modems(2), Profile(), "capacity-modem-multiple"), Is.EqualTo(1));
                Assert.That(Single(Modems(2), Profile(), "capacity-modem-multiple").Severity,
                    Is.EqualTo(ValidationSeverity.Error));
                Assert.That(Single(Modems(2), Profile(), "capacity-modem-multiple").Message,
                    Is.EqualTo("Projektet indeholder 2 modemer; controlleren binder ét."));
                Assert.That(Count(Modems(2), ValidationProfile.Categorized, "capacity-modem-multiple"),
                    Is.EqualTo(1),
                    "and it needs NO profile: the limit is one, not a declared capability");
            });
        }

        // ── capacity-s0-multiple ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The controller binds ONE S0 product, so a second can never be commissioned. Like the modem row it
        /// needs no capability profile — the limit is the controller's, not a declared capability — but unlike
        /// that row it declares the number as data, because a vendor sentence states it (D08).
        /// </summary>
        [Test]
        public void ASecondS0ProductIsAnErrorWithOrWithoutAProfile()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(S0Products(1), Profile(), "capacity-s0-multiple"), Is.Zero,
                    "one S0 product is what the controller binds");
                Assert.That(Count(S0Products(2), Profile(), "capacity-s0-multiple"), Is.EqualTo(1),
                    "ONE finding for the project, not one per extra product");
                Assert.That(Count(S0Products(3), Profile(), "capacity-s0-multiple"), Is.EqualTo(1));
                Assert.That(Single(S0Products(2), Profile(), "capacity-s0-multiple").Severity,
                    Is.EqualTo(ValidationSeverity.Error));
                Assert.That(Single(S0Products(2), Profile(), "capacity-s0-multiple").Message,
                    Is.EqualTo("Projektet indeholder 2 S0-produkter; controlleren binder ét."));
                Assert.That(Count(S0Products(2), ValidationProfile.Categorized, "capacity-s0-multiple"),
                    Is.EqualTo(1),
                    "and it needs NO profile: the limit is one, not a declared capability");
            });
        }

        /// <summary>
        /// The number is DATA on the entry, and the divergence from <c>capacity-modem-multiple</c> is deliberate:
        /// that row keeps its limit of one in the predicate and declares no threshold at all. This one has a
        /// citable vendor sentence, and every compared number is declared where a source for it exists.
        ///
        /// <para><b>The evidence string states the limit of the measurement</b>, which is what keeps the
        /// <c>VendorDocumented</c> grade honest: the guard box was driven and its own wording carries the number,
        /// but no boundary run confirmed that the box appears at exactly two rather than at some higher count.</para>
        /// </summary>
        [Test]
        public void TheS0LimitIsDeclaredAsDataWithItsVendorSentence()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("capacity-s0-multiple"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == "MaximumS0Products");

            Assert.Multiple(() =>
            {
                Assert.That(declared.Value, Is.EqualTo(1));
                Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.VendorDocumented));
                Assert.That(declared.Evidence, Does.Contain("Der kan kun være et S0 produkt i Visual projektet"),
                    "the guard's own sentence, which is where the number comes from");
                Assert.That(entry.RequiresControllerLimits, Is.False);
                Assert.That(entry.RefusedOperations, Is.Empty,
                    "the file opens, edits and saves — only the second product cannot be commissioned");
            });
        }

        // ── capacity-rs485-exceeded ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// The RS-485 bus takes at most 32 components, and the SMS modem is one of them — the vendor's own guard
        /// sentence says so, which is why the modem is counted rather than excused.
        ///
        /// <para><b>Reported ABOVE the threshold, not at it.</b> The sentence states a maximum, so 32 is legal.
        /// The boundary itself was never driven, which the declared evidence records — but the reading that
        /// follows from the sentence is <c>&gt;</c>, and the test pins all three sides of it.</para>
        /// </summary>
        [Test]
        public void Rs485ComponentsAreReportedOnlyAboveTheDeclaredMaximum()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Rs485(31), Profile(), "capacity-rs485-exceeded"), Is.Zero, "below");
                Assert.That(Count(Rs485(32), Profile(), "capacity-rs485-exceeded"), Is.Zero,
                    "AT the maximum: 'maksimalt antal tilladte' makes 32 legal");
                Assert.That(Count(Rs485(33), Profile(), "capacity-rs485-exceeded"), Is.EqualTo(1), "above");
                Assert.That(Single(Rs485(33), Profile(), "capacity-rs485-exceeded").Severity,
                    Is.EqualTo(ValidationSeverity.Error),
                    "the vendor sentence states a hard maximum, unlike the wireless row's 'bør'");
                Assert.That(Single(Rs485(33), Profile(), "capacity-rs485-exceeded").Message,
                    Is.EqualTo("Projektet har 33 RS485-komponenter inkl. SMS-modem; det tilladte maksimum er 32."));
                Assert.That(Count(Rs485(33), ValidationProfile.Categorized, "capacity-rs485-exceeded"),
                    Is.EqualTo(1),
                    "and it needs NO profile: the bus limit is the bus's, not a controller capability");
            });
        }

        /// <summary>
        /// THE POPULATION, which is the whole risk of this row. All three RS-485 families count — the LED dimmer,
        /// the voice modem and the SMS modem — and nothing else does. The vendor's guard names the SMS modem
        /// explicitly ("inkl. SMS modem"), so excusing it would under-report by one on every bus that carries one.
        /// </summary>
        [Test]
        public void EveryRs485FamilyCountsAndNothingElseDoes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Rs485(30, withSmsModem: true, withVoiceModem: true), Profile(),
                    "capacity-rs485-exceeded"), Is.Zero, "30 dimmers plus two modems is exactly 32");
                Assert.That(Count(Rs485(31, withSmsModem: true, withVoiceModem: true), Profile(),
                    "capacity-rs485-exceeded"), Is.EqualTo(1),
                    "and one more is 33 — the two modems are counted, not excused");
                Assert.That(Count(WithProducts(
                    [
                        .. Enumerable.Range(0, 40).Select(i => Tree.Node("product_dataline",
                            Token("product_dataline", 0x40 + i),
                            [("product_identifier", "_0x2101"), ("name", $"Tryk {i}")])),
                    ]), Profile(), "capacity-rs485-exceeded"), Is.Zero,
                    "forty data-line products are not on the RS-485 bus at all");
            });
        }

        /// <summary>
        /// REGRESSION. The count is over PRODUCTS, and a single SMS modem's own children are enough to break a
        /// rule that forgets it.
        ///
        /// <para><b>The trap.</b> <c>ProductClassifier.Classify</c> falls back to a PATTERN for open-world tags —
        /// anything containing <c>modem</c> answers <c>Rs485Modem</c> — and, unlike <c>IsModem</c> and
        /// <c>IsWireless</c>, it does NOT guard that fallback with <c>IsProduct</c>. One modem ships a
        /// <c>sms_modem_settings</c> container and thirty <c>sms_modem_phonenumber</c> slots, so counting over
        /// every element reads 32 components from a project holding exactly one. This was caught by the
        /// characterization corpus — the error fixture went invalid — and is pinned here where it names its own
        /// cause.</para>
        /// </summary>
        [Test]
        public void OneSmsModemsOwnChildrenAreNotThirtyTwoBusComponents()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(ModemWithPhoneSlots(30), Profile(), "capacity-rs485-exceeded"), Is.Zero,
                    "one product, whatever its children are named");
                Assert.That(Count(ModemWithPhoneSlots(30), Profile(), "capacity-s0-multiple"), Is.Zero,
                    "and its sibling row walks products for the same reason");
            });
        }

        /// <summary>
        /// The number is data, and its evidence records what was NOT measured. The guard box was driven and its
        /// own sentence carries the 32; no run established that 32 commits and 33 does not.
        /// </summary>
        [Test]
        public void TheRs485MaximumIsDeclaredWithItsBoundaryCaveat()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("capacity-rs485-exceeded"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == "MaximumRs485Components");

            Assert.Multiple(() =>
            {
                Assert.That(declared.Value, Is.EqualTo(32));
                Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.VendorDocumented));
                Assert.That(declared.Evidence,
                    Does.Contain("Det maksimalt antal tilladte RS485 komponenter er 32 inkl. SMS modem"));
                Assert.That(declared.Evidence, Does.Contain("boundary"),
                    "and it says the boundary itself is uncited — a guard's wording is not a boundary probe");
                Assert.That(entry.RequiresControllerLimits, Is.False);
            });
        }

        // ── capacity-voicemodem-dimmer-conflict ─────────────────────────────────────────────────────

        /// <summary>
        /// A Voice Modem and an RS485 LED Dimmer cannot share a controller, so a project carrying both has one
        /// device that can never work. An incompatibility rather than a count — no slots, no threshold.
        /// </summary>
        [Test]
        public void AVoiceModemBesideAnLedDimmerIsAnErrorAndEitherAloneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(VoiceModem(withDimmer: true), Profile(), "capacity-voicemodem-dimmer-conflict"),
                    Is.EqualTo(1));
                Assert.That(Single(VoiceModem(withDimmer: true), Profile(),
                    "capacity-voicemodem-dimmer-conflict").Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(Single(VoiceModem(withDimmer: true), Profile(),
                    "capacity-voicemodem-dimmer-conflict").Message,
                    Is.EqualTo("Projektet indeholder både et Voice Modem og en RS485 LED-dæmper; "
                        + "de kan ikke anvendes i samme projekt."));
                Assert.That(Count(VoiceModem(withDimmer: false), Profile(), "capacity-voicemodem-dimmer-conflict"),
                    Is.Zero, "a voice modem alone is fine");
                Assert.That(Count(Rs485(1), Profile(), "capacity-voicemodem-dimmer-conflict"), Is.Zero,
                    "and a dimmer alone is fine");
            });
        }

        /// <summary>
        /// THE EXCLUSION THAT KEEPS THE CORPUS QUIET, and it is not a hypothetical: three of the committed
        /// projects carry an RS485 LED dimmer AND an SMS modem side by side. The SMS modem is a different
        /// product from the Voice Modem, so a rule that read "any modem" would report an Error on three
        /// authentic vendor files.
        ///
        /// <para>The second assertion pins the trap that broke <c>capacity-rs485-exceeded</c>'s first cut:
        /// <c>ProductClassifier.Classify</c> answers <c>Rs485Modem</c> for ANY tag containing <c>modem</c>, with
        /// no <c>IsProduct</c> guard of its own — so an SMS modem's <c>sms_modem_settings</c> and
        /// <c>sms_modem_phonenumber</c> children each classify as a voice modem when the walk is not scoped to
        /// products.</para>
        /// </summary>
        [Test]
        public void AnSmsModemIsNotAVoiceModemAndNeitherAreItsChildren()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Rs485(1, withSmsModem: true), Profile(), "capacity-voicemodem-dimmer-conflict"),
                    Is.Zero,
                    "an SMS modem beside a dimmer is the shape three committed projects actually carry");
                Assert.That(Count(ModemWithPhoneSlots(30), Profile(), "capacity-voicemodem-dimmer-conflict"),
                    Is.Zero,
                    "and a modem's own children are not products, however their tags are spelled");
            });
        }

        // ── capacity-resources-high ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// The row reports from the declared fraction UPWARDS, with no upper bound.
        ///
        /// <para><b>The upper bound was a silent gap (D1).</b> It read <c>resources &lt;= limits.Resources</c>, on
        /// the entry's claim that a project past the ceiling was "the modules row's business at upload" — but
        /// the modules rows count data lines and terminal addresses and never count
        /// <c>resource_*</c>. So 1800 of 2000 warned and 2500 of 2000 said nothing at all, which is the one reading
        /// a capacity row must not have. The ceiling itself is authored and unconfirmed, so widening the WARNING is
        /// the honest fix; an Error would rest an it-is-wrong-whatever-you-meant verdict on a guessed number.</para>
        /// </summary>
        [Test]
        public void ResourcesAreReportedFromTheDeclaredFractionUpwards()
        {
            ValidationProfile ten = Profile(resources: 10);   // 0.9 of 10 -> the warning starts at 9

            Assert.Multiple(() =>
            {
                Assert.That(Count(Resources(8), ten, "capacity-resources-high"), Is.Zero, "below the fraction");
                Assert.That(Count(Resources(9), ten, "capacity-resources-high"), Is.EqualTo(1), "AT it");
                Assert.That(Count(Resources(10), ten, "capacity-resources-high"), Is.EqualTo(1), "at the ceiling");
                Assert.That(Count(Resources(11), ten, "capacity-resources-high"), Is.EqualTo(1),
                    "and PAST it — a project over the table is the one case that must not be silent");
                Assert.That(Single(Resources(11), ten, "capacity-resources-high").Message,
                    Is.EqualTo("Projektet bruger 11 af 10 ressourcer."),
                    "the row's own sentence stays true past the limit, which is why no second row is needed");
                Assert.That(Single(Resources(9), ten, "capacity-resources-high").Message,
                    Is.EqualTo("Projektet bruger 9 af 10 ressourcer."));
            });
        }

        [Test]
        public void BothOfTheResourceRowsNumbersAreAuthoredAndSaySo()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("capacity-resources-high"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold fraction = entry.Thresholds.Single(t => t.Name == "HighWaterFraction");

            Assert.Multiple(() =>
            {
                Assert.That(fraction.Confidence, Is.EqualTo(ThresholdConfidence.Authored));
                Assert.That(fraction.Evidence, Does.Contain("TODO"), "D21(d): the marker lives in the code");
                Assert.That(ControllerCapabilityLimits.AuthoredResourceCeiling, Is.GreaterThan(0),
                    "and the ceiling it multiplies is authored in the profile, with its own TODO beside it");
            });
        }

        // ── the profile requirement ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// The property the whole capability design exists for: with no controller named, the five rows whose limit
        /// is not in the file are not evaluated at all — not evaluated against a default, which would make the same
        /// project valid on one workstation and invalid on another.
        /// </summary>
        [Test]
        public void NoCapacityRowFiresWithoutADeclaredProfile()
        {
            Project breaching = Everything();

            Assert.Multiple(() =>
            {
                foreach (string code in new[]
                    {
                        "capacity-input-modules", "capacity-output-modules",
                        "capacity-input-addresses", "capacity-output-addresses",
                        "capacity-wireless-exceeded", "capacity-wireless-links-per-unit",
                        "capacity-scenarios-per-receiver", "capacity-resources-high",
                    })
                {
                    Assert.That(Count(breaching, ValidationProfile.Categorized, code), Is.Zero, code);
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code),
                        out ProblemCatalogEntry entry), Is.True, code);
                    Assert.That(entry.RequiresControllerLimits, Is.True,
                        $"{code} must DECLARE the requirement, so the profile skips it rather than the rule "
                        + "having to handle absence");
                }

                Assert.That(Count(breaching, Profile(inputModules: 1, wireless: 1, resources: 4),
                    "capacity-input-modules"), Is.EqualTo(1),
                    "and the same project reports once a controller IS named");
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static Project WithProducts(params ProjectElement[] products) =>
            Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")], products)));

        /// <summary>
        /// One product carrying <paramref name="perLine"/> addressed terminals on each of <paramref name="lines"/>
        /// data lines, in the given direction.
        /// </summary>
        private static Project Terminals(bool isOutput, int lines, int perLine = 1)
        {
            string tag = isOutput ? "dataline_output" : "dataline_input";
            ImmutableArray<ProjectElement>.Builder terminals = ImmutableArray.CreateBuilder<ProjectElement>();
            int counter = 0x50;
            for (int line = 1; line <= lines; line++)
            {
                for (int terminal = 1; terminal <= perLine; terminal++)
                {
                    Assert.That(DatalineAddress.TryEncode(line, terminal, isOutput, out string token), Is.True,
                        "the fixture's own address has to be encodable, or the test proves nothing");
                    terminals.Add(Tree.Node(tag, Token(tag, counter++),
                        [("name", $"Klemme {line}.{terminal}"), ("address_dataline", token)]));
                }
            }

            return WithProducts(
                Tree.Node("product_dataline", Token("product_dataline", 0x40),
                    [("product_identifier", "_0x2202"), ("name", "Produkt")], [.. terminals]));
        }

        /// <summary>The given number of wireless products.</summary>
        private static Project Wireless(int count) =>
            WithProducts(
                [
                    .. Enumerable.Range(0, count).Select(i => Tree.Node("product_airlink",
                        Token("product_airlink", 0x40 + i),
                        [("product_identifier", "_0x4203"), ("name", $"Trådløs {i}"),
                         ("serialnumber", "_0xaa11")])),
                ]);

        /// <summary>
        /// One wireless unit carrying <paramref name="links"/> follow-links, ordinary or combi.
        /// </summary>
        /// <param name="links">How many follow-links hang off the unit.</param>
        /// <param name="combi">Whether the unit is one of the four combi identifiers.</param>
        private static Project LinkedUnit(int links, bool combi = false) =>
            WithProducts([Unit(0x40, links, combi)]);

        /// <summary>Two ordinary wireless units, each carrying <paramref name="links"/> follow-links.</summary>
        /// <param name="links">How many follow-links hang off each unit.</param>
        private static Project TwoLinkedUnits(int links) =>
            WithProducts([Unit(0x40, links, combi: false), Unit(0x200, links, combi: false)]);

        private static ProjectElement Unit(int at, int links, bool combi) =>
            Tree.Node("product_airlink", Token("product_airlink", at),
                [("product_identifier", combi ? "_0x4404" : "_0x4203"), ("name", "Trådløs"),
                 ("serialnumber", "_0xaa11")],
                Tree.Node("airlink_relay", Token("airlink_relay", at + 1), [("name", "Relæ")],
                    [.. Enumerable.Range(0, links).Select(i =>
                        Tree.Node("link_to_resource", Token("link_to_resource", at + 2 + i),
                            [("name", "Følg Link"), ("link", Token("link_from_resource", 0x900 + i))]))]));

        /// <summary>
        /// One wireless RECEIVER carrying <paramref name="scenarios"/> scene member rows, spread evenly across
        /// <paramref name="containers"/> scene containers.
        /// </summary>
        /// <param name="scenarios">Total scene member rows on the receiver.</param>
        /// <param name="containers">How many scene containers they are spread across.</param>
        private static Project Receiver(int scenarios, int containers = 1) =>
            WithProducts(
                [
                    Tree.Node("product_airlink", Token("product_airlink", 0x40),
                        [("product_identifier", "_0x4203"), ("name", "Modtager"), ("serialnumber", "_0xaa11")],
                        [.. Enumerable.Range(0, containers).Select(c =>
                            Tree.Node("scenes", Token("scenes", 0x50 + (c * 0x40)),
                                [("name", "Scenarier"),
                                 ("scene_resource", Token("airlink_relay", 0x300 + c))],
                                [.. Enumerable.Range(0, scenarios / containers).Select(i =>
                                    Tree.Node("scene_relay", Token("scene_relay", 0x51 + (c * 0x40) + i),
                                        [("name", "Scenarie link"), ("relay_value", "on")]))]))]),
                ]);

        /// <summary>A wireless product that owns no scene container at all — not a receiver.</summary>
        private static Project NonReceiver() =>
            WithProducts(
                [
                    Tree.Node("product_airlink", Token("product_airlink", 0x40),
                        [("product_identifier", "_0x4203"), ("name", "Trådløs"), ("serialnumber", "_0xaa11")]),
                ]);

        /// <summary>The given number of modem products.</summary>
        private static Project Modems(int count) =>
            WithProducts(
                [
                    .. Enumerable.Range(0, count).Select(i => Tree.Node("product_rs485_sms_modem",
                        Token("product_rs485_sms_modem", 0x40 + i),
                        [("product_identifier", "_0x6101"), ("name", $"SMS Modem {i}")])),
                ]);

        /// <summary>
        /// The given number of S0 metering products, each with a pulse count inside the declared range so no
        /// addressing row fires alongside the one under test.
        /// </summary>
        private static Project S0Products(int count) =>
            WithProducts(
                [
                    .. Enumerable.Range(0, count).Select(i => Tree.Node("s0_device",
                        Token("s0_device", 0x40 + i),
                        [("product_identifier", "_0x2313"), ("name", $"Måler {i}"), ("ticks", "100")])),
                ]);

        /// <summary>
        /// RS-485 bus components: <paramref name="dimmers"/> LED dimmers, plus optionally one SMS modem and one
        /// voice modem — the three families the vendor's guard sentence counts together.
        /// </summary>
        private static Project Rs485(int dimmers, bool withSmsModem = false, bool withVoiceModem = false)
        {
            ImmutableArray<ProjectElement>.Builder products = ImmutableArray.CreateBuilder<ProjectElement>();
            for (int i = 0; i < dimmers; i++)
            {
                products.Add(Tree.Node("product_rs485_led_dimmer", Token("product_rs485_led_dimmer", 0x40 + i),
                    [("product_identifier", "_0x9e10"), ("name", $"Dæmper {i}")]));
            }

            if (withSmsModem)
            {
                products.Add(Tree.Node("product_rs485_sms_modem", Token("product_rs485_sms_modem", 0x90),
                    [("product_identifier", "_0x6101"), ("name", "SMS modem")]));
            }

            if (withVoiceModem)
            {
                // `product_rs485_modem` is an OPEN-WORLD tag in this SDK: it is in neither `TypeCode.cs` nor
                // `CanonicalDtdBlocks.dtd`, because the built-in catalog ships no voice-modem product at all. The
                // id token therefore borrows the SMS modem's type code — the tree builder needs some code, and
                // which one it borrows is irrelevant to a rule that matches on the TAG.
                products.Add(Tree.Node("product_rs485_modem", Token("product_rs485_sms_modem", 0x91),
                    [("product_identifier", "_0x6001"), ("name", "Talemodem")]));
            }

            return WithProducts([.. products]);
        }

        /// <summary>
        /// ONE SMS modem carrying its settings container and the given number of phone slots — the shape the
        /// vendor ships, and the shape whose child TAGS all contain <c>modem</c>.
        /// </summary>
        private static Project ModemWithPhoneSlots(int slots) =>
            WithProducts(
            [
                Tree.Node("product_rs485_sms_modem", Token("product_rs485_sms_modem", 0x40),
                    [("product_identifier", "_0x6101"), ("name", "SMS modem")],
                    Tree.Node("sms_modem_settings", Token("sms_modem_settings", 0x41), [("name", "Indstillinger")],
                        [
                            .. Enumerable.Range(0, slots).Select(i => Tree.Node("sms_modem_phonenumber",
                                Token("sms_modem_phonenumber", 0x50 + i),
                                [("address", $"{i + 1}"), ("phonenumber", "+4512345678")])),
                        ])),
            ]);

        /// <summary>A Voice Modem, optionally beside an RS485 LED dimmer.</summary>
        private static Project VoiceModem(bool withDimmer) =>
            Rs485(withDimmer ? 1 : 0, withVoiceModem: true);

        /// <summary>A block declaring the given number of resources.</summary>
        private static Project Resources(int count) =>
            Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")]),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                                [
                                    .. Enumerable.Range(0, count).Select(i => Tree.Node("resource_flag",
                                        Token("resource_flag", 0x80 + i), [("name", $"Flag {i}")])),
                                ]),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")])))));

        /// <summary>A project that breaches every capacity limit a small profile could declare.</summary>
        private static Project Everything() =>
            Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("product_dataline", Token("product_dataline", 0x40),
                            [("product_identifier", "_0x2202"), ("name", "Produkt")],
                            Tree.Node("dataline_input", Token("dataline_input", 0x50),
                                [("name", "Klemme 1"), ("address_dataline", "_0x1")]),
                            Tree.Node("dataline_input", Token("dataline_input", 0x51),
                                [("name", "Klemme 2"), ("address_dataline", "_0x11")])),
                        Tree.Node("product_airlink", Token("product_airlink", 0x42),
                            [("product_identifier", "_0x4203"), ("name", "Trådløs 1"),
                             ("serialnumber", "_0xaa11")]),
                        Tree.Node("product_airlink", Token("product_airlink", 0x43),
                            [("product_identifier", "_0x4203"), ("name", "Trådløs 2"),
                             ("serialnumber", "_0xaa12")]),
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")]),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                                Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "Flag 0")]),
                                Tree.Node("resource_flag", Token("resource_flag", 0x81), [("name", "Flag 1")]),
                                Tree.Node("resource_flag", Token("resource_flag", 0x82), [("name", "Flag 2")]),
                                Tree.Node("resource_flag", Token("resource_flag", 0x83), [("name", "Flag 3")])),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")])))));
    }
}
