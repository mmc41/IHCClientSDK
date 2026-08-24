using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T059 — the six CAPACITY rows, tested the only way they can be: with a DECLARED LOW LIMIT rather than a
    /// giant fixture.
    ///
    /// <para><b>Why a low limit and not a big project.</b> The address encoding itself caps a data line at 8 (input)
    /// or 16 (output), so exceeding the datasheet figures is not expressible in a <c>.vis</c> file at all; and 64
    /// wireless products or a full resource table would make the suite slow for nothing. Declaring a profile that
    /// says "this controller holds two modules" tests the same predicate at the same three points — below, at, and
    /// above — which is what the gate asks for.</para>
    ///
    /// <para><b>The property that matters more than any boundary</b> is
    /// <see cref="NoCapacityRowFiresWithoutADeclaredProfile"/>: five of these rows must be SKIPPED when no
    /// controller is named, because the same project must not be valid on one workstation and invalid on another.
    /// The sixth, the modem row, must fire anyway — its limit is one, and it is not a capability.</para>
    /// </summary>
    [TestFixture]
    public sealed class CapacityRulesTests
    {
        /// <summary>A profile naming a deliberately small controller, so the boundaries are reachable.</summary>
        private static ValidationProfile Profile(
            int inputModules = 8, int outputModules = 16, int addresses = 128, int wireless = 64,
            int resources = 2000) =>
            ValidationProfile.Categorized with
            {
                Controller = new ControllerCapabilityLimits(
                    inputModules, outputModules, addresses, wireless, resources),
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
                    "capacity-addresses"), Is.Zero, "AT the address limit");
                Assert.That(Count(Terminals(isOutput: false, lines: 1, perLine: 4), profile,
                    "capacity-addresses"), Is.EqualTo(1),
                    "four addressed terminals on one line is one module but four addresses");
                Assert.That(Single(Terminals(isOutput: false, lines: 1, perLine: 4), profile,
                    "capacity-addresses").Message,
                    Is.EqualTo("Projektet bruger 4 af 3 klemmer på én datalinjeretning."),
                    "KLEMMER, not moduler — the wrong unit is what forced the split");
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
                Assert.That(Count(over, tight, "capacity-addresses"), Is.EqualTo(1), "eight terminals over three");
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
                        "capacity-input-modules", "capacity-output-modules", "capacity-addresses",
                        "capacity-wireless-exceeded", "capacity-resources-high",
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

        /// <summary>The given number of modem products.</summary>
        private static Project Modems(int count) =>
            WithProducts(
                [
                    .. Enumerable.Range(0, count).Select(i => Tree.Node("product_rs485_sms_modem",
                        Token("product_rs485_sms_modem", 0x40 + i),
                        [("product_identifier", "_0x6101"), ("name", $"SMS Modem {i}")])),
                ]);

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
