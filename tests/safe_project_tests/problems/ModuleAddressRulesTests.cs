using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T048 — module and channel addressing: the two module rows over their declared thresholds, the two dimmer
    /// channel rows over their partitions, and the MEASUREMENT that rules the fifth row out.
    ///
    /// <para><b>The thresholds are read from the catalogue.</b> Both module rows describe a matter of degree, so
    /// both carry a declared number; every boundary test below reads it from the entry that owns it, so the test
    /// and the rule move together and neither can be changed silently.</para>
    ///
    /// <para><b>The ruled-out row is tested too, and that is the point of ruling it out rather than deleting it.</b>
    /// <c>addr-unassigned</c> would have reported exactly what <c>doc-address</c> reports; the test asserts the
    /// measurement — one unaddressed terminal, one finding — so a later task cannot quietly implement a second
    /// sentence for one observation.</para>
    /// </summary>
    [TestFixture]
    public sealed class ModuleAddressRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        private static double Threshold(string code, string name)
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == name);
            Assert.Multiple(() =>
            {
                Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.Authored),
                    "no vendor source states this number, and the entry must say so");
                Assert.That(declared.Evidence, Does.Contain("TODO"), "an authored threshold carries its status");
            });
            return declared.Value;
        }

        // ── addr-module-partial ─────────────────────────────────────────────────────────────────────

        [Test]
        public void AModuleBelowTheDeclaredMinimumIsReported_AndOneAtItIsNot()
        {
            int minimum = (int)Threshold("addr-module-partial", "MinimumUsedTerminals");

            // Two modules in use: the first well filled, the second holding one terminal short of the minimum.
            Project sparse = Modules(firstLineTerminals: 4, secondLineTerminals: minimum - 1);
            Project filled = Modules(firstLineTerminals: 4, secondLineTerminals: minimum);

            Assert.Multiple(() =>
            {
                Assert.That(Count(sparse, "addr-module-partial"), Is.EqualTo(1),
                    "one finding for the nearly-empty module, with its terminals as related locations");
                Assert.That(Message(sparse, "addr-module-partial"), Does.Contain("2").Or.Contain("1"),
                    "the line, the used count and the capacity are bound as data");
                Assert.That(Count(filled, "addr-module-partial"), Is.Zero, "AT the minimum is not below it");
            });
        }

        [Test]
        public void ASingleModuleInADirectionIsNeverReported()
        {
            Project alone = Modules(firstLineTerminals: 1, secondLineTerminals: 0);

            Assert.That(Count(alone, "addr-module-partial"), Is.Zero,
                "a project wired onto one module is a small installation, not a mis-address");
        }

        [Test]
        public void AnUndecodableAddressIsNotCountedIntoAModule()
        {
            Project project = Modules(firstLineTerminals: 4, secondLineTerminals: 0, strayUnaddressed: true);

            Assert.Multiple(() =>
            {
                Assert.That(Count(project, "addr-module-partial"), Is.Zero,
                    "an unaddressed terminal belongs to no module — counting it would invent a line");
                Assert.That(Count(project, "doc-address"), Is.EqualTo(1),
                    "and it IS reported, once, by the row whose condition that is");
            });
        }

        // ── addr-module-mixed-locality ──────────────────────────────────────────────────────────────

        [Test]
        public void AModuleSpanningMoreLocalitiesThanDeclaredIsReported()
        {
            int maximum = (int)Threshold("addr-module-mixed-locality", "MaxLocalitiesPerModule");

            Project atLimit = SpreadAcrossLocalities(maximum);
            Project past = SpreadAcrossLocalities(maximum + 1);

            Assert.Multiple(() =>
            {
                Assert.That(Count(atLimit, "addr-module-mixed-locality"), Is.Zero,
                    "two localities on one module is ordinary — adjacent rooms fed from one module");
                Assert.That(Count(past, "addr-module-mixed-locality"), Is.EqualTo(1));
                Assert.That(Message(past, "addr-module-mixed-locality"),
                    Does.Contain((maximum + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    "the locality count is bound as data");
            });
        }

        // ── addr-dimmer-channel-unassigned / -duplicate ─────────────────────────────────────────────

        [Test]
        public void AChannelWithNoIdIsReported_AndOneWithAnIdIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmer("", ""), "addr-dimmer-channel-unassigned"), Is.EqualTo(2),
                    "one finding per unassigned channel — each is separately repairable");
                Assert.That(Count(Dimmer("1", "2"), "addr-dimmer-channel-unassigned"), Is.Zero);
                Assert.That(Count(Dimmer("   ", "2"), "addr-dimmer-channel-unassigned"), Is.EqualTo(1),
                    "whitespace is not an id");
            });
        }

        [Test]
        public void TwoChannelsSharingAnIdAreReportedOnceAsAnError()
        {
            Project colliding = Dimmer("7", "7");
            Project distinct = Dimmer("7", "8");

            Assert.Multiple(() =>
            {
                Assert.That(Count(colliding, "addr-dimmer-channel-duplicate"), Is.EqualTo(1),
                    "ONE finding for the collision, anchored on the second holder");
                Assert.That(Validate(colliding).Findings.Single(f => f.RuleId == "addr-dimmer-channel-duplicate")
                    .Severity, Is.EqualTo(ValidationSeverity.Error),
                    "the catalogue rates this one an Error, and the rating is implemented as stated");
                Assert.That(Message(colliding, "addr-dimmer-channel-duplicate"), Does.Contain("7"));
                Assert.That(Count(distinct, "addr-dimmer-channel-duplicate"), Is.Zero);
            });
        }

        [Test]
        public void TwoUnassignedChannelsDoNotCountAsACollision()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmer("", ""), "addr-dimmer-channel-duplicate"), Is.Zero,
                    "a blank id is the other row's finding — two unassigned channels do not collide");
                Assert.That(Count(Dimmer(ElementId.NullToken, ElementId.NullToken), "addr-dimmer-channel-duplicate"),
                    Is.Zero,
                    "and neither does the NULL TOKEN, which is what a freshly placed catalog dimmer carries — "
                    + "treating it as an id made every inserted dimmer an Error, which is how the gate found it");
                Assert.That(Count(Dimmer(ElementId.NullToken, ElementId.NullToken),
                    "addr-dimmer-channel-unassigned"), Is.EqualTo(2),
                    "it IS reported, as unassigned, which is the condition it actually is");
            });
        }

        /// <summary>
        /// The measurement behind that exclusion, over the SHIPPED catalog rather than a hand-built tree: placing
        /// the real LED dimmer must not produce an Error. Six unrelated suites failed on this before the null token
        /// was recognised, which is the strongest argument for keeping the check here.
        /// </summary>
        [Test]
        public void PlacingTheShippedLedDimmerProducesNoError()
        {
            ProjectAppService app = new(TestSetup.Settings);
            Ihc.Vis.Products.ProductDefinition dimmer = app.GetAvailableProducts()
                .First(p => p.Body.FindDescendantOrSelf(e => e.Tag == "rs485_led_dimmer_channel") is not null);
            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ElementId locality = project.Groups.First().Id!.Value;
            project = app.Apply(project, app.Commands.AddProduct(project, locality, dimmer)).Project!;

            ProjectValidationResult result = Validate(project);

            Assert.Multiple(() =>
            {
                Assert.That(result.Findings.Where(f => f.RuleId == "addr-dimmer-channel-duplicate"), Is.Empty,
                    "the catalog's own channels are unassigned, not colliding");
                Assert.That(result.Findings.Count(f => f.RuleId == "addr-dimmer-channel-unassigned"),
                    Is.GreaterThan(0), "they ARE unassigned, which the other row says");
                Assert.That(result.IsValid, Is.True, "and nothing about a freshly placed product blocks a save");
            });
        }

        // ── addr-unassigned: the ruled-out row ──────────────────────────────────────────────────────

        [Test]
        public void TheUnassignedRowIsRuledOutBecauseAnotherRowReportsItsCondition()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("addr-unassigned"),
                out ProblemCatalogEntry entry), Is.True, "the id stays occupied and can never be re-pointed");

            Project project = Modules(firstLineTerminals: 4, secondLineTerminals: 0, strayUnaddressed: true);

            Assert.Multiple(() =>
            {
                Assert.That(entry.Status, Is.EqualTo(ProblemCodeStatus.RuledOut));
                Assert.That(Count(project, "addr-unassigned"), Is.Zero, "no rule emits it");
                Assert.That(Count(project, "doc-address"), Is.EqualTo(1),
                    "ONE finding for the one observation — which is why the second id would have been noise");
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static string Address(int line, int terminal, bool isOutput)
        {
            Assert.That(DatalineAddress.TryEncode(line, terminal, isOutput, out string token), Is.True);
            return token;
        }

        /// <summary>
        /// One locality, one product, INPUT terminals spread over data lines 1 and 2 in the given numbers, and
        /// optionally one terminal left unaddressed.
        /// </summary>
        private static Project Modules(int firstLineTerminals, int secondLineTerminals, bool strayUnaddressed = false)
        {
            ImmutableArray<ProjectElement>.Builder terminals = ImmutableArray.CreateBuilder<ProjectElement>();
            int at = 0x100;
            foreach ((int line, int count) in new[] { (1, firstLineTerminals), (2, secondLineTerminals) })
            {
                for (int terminal = 1; terminal <= count; terminal++)
                {
                    terminals.Add(Tree.Node("dataline_input", Token("dataline_input", at++),
                        [("name", $"Tryk {line}.{terminal}"), ("address_dataline", Address(line, terminal, false)),
                         ("cable_colour", "Rød")]));
                }
            }

            if (strayUnaddressed)
            {
                terminals.Add(Tree.Node("dataline_input", Token("dataline_input", at++),
                    [("name", "Uadresseret"), ("cable_colour", "Rød")]));
            }

            return Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                    Product(0x51, "Produkt", [.. terminals]))));
        }

        /// <summary>ONE module (input line 1) whose terminals sit in the given number of distinct localities.</summary>
        private static Project SpreadAcrossLocalities(int localities)
        {
            ImmutableArray<ProjectElement>.Builder groups = ImmutableArray.CreateBuilder<ProjectElement>();
            for (int i = 0; i < localities; i++)
            {
                ProjectElement terminal = Tree.Node("dataline_input", Token("dataline_input", 0x100 + i),
                    [("name", $"Tryk {i}"), ("address_dataline", Address(1, i + 1, false)), ("cable_colour", "Rød")]);
                groups.Add(Tree.Node("group", Token("group", 0x21 + i), [("name", "Rum " + i)],
                    Product(0x51 + (i * 0x10), "Produkt " + i, [terminal])));
            }

            return Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")], [.. groups]));
        }

        private static ProjectElement Product(int at, string name, ProjectElement[] terminals) =>
            Tree.Node("product_dataline", Token("product_dataline", at),
                [("product_identifier", "_0x2202"), ("name", name), ("documentation_tag", "T"),
                 ("power_group", "G"), ("cabletype", "C"), ("cablenumber", "1"), ("position", "P")],
                terminals);

        /// <summary>An RS485 LED dimmer with two channels carrying the given channel ids.</summary>
        private static Project Dimmer(string firstChannelId, string secondChannelId) =>
            Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                    Tree.Node("product_rs485_led_dimmer", Token("product_rs485_led_dimmer", 0x60),
                        [("product_identifier", "_0x9f05"), ("name", "LED dæmper")],
                        Tree.Node("rs485_led_dimmer_channel", Token("rs485_led_dimmer_channel", 0x61),
                            [("name", "Kanal 1"), ("channel", "1"), ("channel_id", firstChannelId)]),
                        Tree.Node("rs485_led_dimmer_channel", Token("rs485_led_dimmer_channel", 0x62),
                            [("name", "Kanal 2"), ("channel", "2"), ("channel_id", secondChannelId)])))));
    }
}
