using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T046 — the WIRING rows, per rule, over the partitions each one's predicate names.
    ///
    /// <para><b>Built as synthetic trees, and that is the only way to reach these conditions deliberately.</b> An
    /// authentic file carries whatever wiring its author happened to make; a tree built here carries exactly one
    /// condition and its neighbouring non-condition, which is what an equivalence partition needs. The authentic
    /// corpus still guards the other direction — <c>ValidationCharacterizationTests</c> records every finding these
    /// rules produce over five vendor-authored files, so a predicate that grew a false positive shows up there.</para>
    ///
    /// <para><b>Each rule is tested at the boundary its predicate states</b>: nought/one/two drivers for the
    /// multi-driven row, one linked pin versus none for the two block rows, and — for the pass-through row — a
    /// legal bypass versus an illegal one, which is the exclusion that makes the row true.</para>
    ///
    /// <para><b>The per-pin rows are gone, and the boundary tests here are what replaced them.</b> A spare terminal
    /// on an installed product is what a plate of buttons and indicators looks like when the author wires the two
    /// they need, so a per-pin reading stated its own consequence falsely once per terminal they declined. The
    /// subject is the PRODUCT — no pin of it wired in either direction — and the tests below pin both halves of
    /// that: the untouched product fires, and one wire anywhere on it is enough to silence the row.</para>
    /// </summary>
    [TestFixture]
    public sealed class WiringRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string[] Messages(Project project, string ruleId) =>
            [.. Validate(project).Findings.Where(f => f.RuleId == ruleId).Select(f => f.Message)];

        /// <summary>The structured finding, which is where a row's related sites are visible at all.</summary>
        private static ValidationFinding Structured(Project project, string code) =>
            new ProjectAppService(TestSetup.Settings).ValidateStructured(project).Findings
                .Single(f => f.Code.Value == code);

        // ── link-product-unwired ────────────────────────────────────────────────────────────────────

        [Test]
        public void AProductWithNoWiredPinAtAllIsReportedOnce_WithItsPinsAsRelatedSites()
        {
            Project unwired = Tree.WithRoot(Locality(Product(
                Input(0x52, "Tryk 1", wired: false),
                Input(0x54, "Tryk 2", wired: false),
                Output(0x53, "LED", drivers: 0))));

            Assert.Multiple(() =>
            {
                Assert.That(Count(unwired, "link-product-unwired"), Is.EqualTo(1),
                    "ONE finding for the product, not one per terminal");
                Assert.That(Messages(unwired, "link-product-unwired").Single(),
                    Is.EqualTo("Produktet 'Produkt' har ingen forbundne ind- eller udgange."),
                    "the product's own name is bound into the sentence");
                Assert.That(Structured(unwired, "link-product-unwired").Related, Has.Length.EqualTo(3),
                    "every unwired pin is a related site — the reader has to see what the product offers");
            });
        }

        /// <summary>
        /// ONE WIRE ANYWHERE IS ENOUGH, in either direction, and this is the whole reason the row is per product.
        /// A plate whose two buttons are wired and whose indicator LEDs are not is an installed, working product;
        /// the per-pin rows this replaced reported the LEDs, once each, on every such plate in a real installation.
        /// </summary>
        [Test]
        public void OneWiredPinInEitherDirectionSilencesTheRow()
        {
            Project inputWired = Tree.WithRoot(Locality(Product(
                Input(0x52, "Tryk", wired: true), Output(0x53, "LED", drivers: 0))));
            Project outputWired = Tree.WithRoot(Locality(Product(
                Input(0x52, "Tryk", wired: false), Output(0x53, "LED", drivers: 1))));

            Assert.Multiple(() =>
            {
                Assert.That(Count(inputWired, "link-product-unwired"), Is.Zero,
                    "the buttons are wired; the indicator the author left spare is not this row's business");
                Assert.That(Count(outputWired, "link-product-unwired"), Is.Zero,
                    "and a product driven but never read is installed just as much");
            });
        }

        [Test]
        public void AProductOfOnlyInputsOrOnlyOutputsIsJudgedTheSameWay()
        {
            Project inputsOnly = Tree.WithRoot(Locality(Product(Input(0x52, "Tryk", wired: false))));
            Project outputsOnly = Tree.WithRoot(Locality(Product(Output(0x53, "Udgang", drivers: 0))));
            Project wirelessOnly = Tree.WithRoot(Locality(
                Tree.Node("product_airlink", Token("product_airlink", 0x60),
                    [("product_identifier", "_0x2202"), ("name", "Trådløs")],
                    Pin("airlink_input", 0x61, "Trådløst tryk", null))));

            Assert.Multiple(() =>
            {
                Assert.That(Count(inputsOnly, "link-product-unwired"), Is.EqualTo(1));
                Assert.That(Count(outputsOnly, "link-product-unwired"), Is.EqualTo(1));
                Assert.That(Count(wirelessOnly, "link-product-unwired"), Is.EqualTo(1),
                    "the subject is wired OR wireless, as the measured pin families are");
            });
        }

        /// <summary>
        /// THE SCENARIO EXCLUSION SURVIVES THE RESHAPE, and it has to: a lamp module a scenario recalls carries no
        /// follow-link at all, so without this the row would report every scene-driven product in the corpus.
        /// </summary>
        [Test]
        public void AProductWhoseOnlyConsumerIsAScenarioIsNotReported()
        {
            Project project = Tree.WithRoot(Locality(
                Tree.Node("product_dataline", Token("product_dataline", 0x51),
                    [("product_identifier", "_0x2202"), ("name", "Produkt")],
                    Output(0x53, "Udgang", drivers: 0),
                    Tree.Node("scenes", Token("scenes", 0x56),
                        [("name", "Scenarier"), ("scene_resource", Token("dataline_output", 0x53))]))));

            Assert.Multiple(() =>
            {
                Assert.That(Count(project, "link-product-unwired"), Is.Zero, "the declared exclusion");
                Assert.That(Count(project, "link-output-multidriven"), Is.Zero);
            });
        }

        /// <summary>
        /// A product with nothing wirable on it is <c>struct-product-no-terminals</c>' finding, not this one — the
        /// two subjects do not overlap, and a modem must not draw both.
        /// </summary>
        [Test]
        public void AProductWithNoPinsAtAllIsTheStructureRowsFinding()
        {
            Project modem = Tree.WithRoot(Locality(
                Tree.Node("product_dataline", Token("product_dataline", 0x51),
                    [("product_identifier", "_0x2202"), ("name", "Modem")])));

            Assert.Multiple(() =>
            {
                Assert.That(Count(modem, "link-product-unwired"), Is.Zero,
                    "no pin means no unwired pin — there is nothing this row could name");
                Assert.That(Count(modem, "struct-product-no-terminals"), Is.EqualTo(1));
            });
        }

        // ── link-output-multidriven: the boundary is one driver ─────────────────────────────────────

        [Test]
        public void OneDriverIsFine_TwoIsReported()
        {
            Project one = Tree.WithRoot(Locality(Product(Output(0x53, "Udgang", drivers: 1))));
            Project two = Tree.WithRoot(Locality(Product(Output(0x53, "Udgang", drivers: 2))));
            Project three = Tree.WithRoot(Locality(Product(Output(0x53, "Udgang", drivers: 3))));

            Assert.Multiple(() =>
            {
                Assert.That(Count(one, "link-output-multidriven"), Is.Zero, "one source is the normal case");
                Assert.That(Count(two, "link-output-multidriven"), Is.EqualTo(1), "two is the finding");
                Assert.That(Messages(two, "link-output-multidriven").Single(), Does.Contain("2"),
                    "and the count is bound into the sentence as data");
                Assert.That(Count(three, "link-output-multidriven"), Is.EqualTo(1),
                    "three sources are still ONE finding — the pin is the thing to repair");
                Assert.That(Messages(three, "link-output-multidriven").Single(), Does.Contain("3"));
            });
        }

        // ── the two block rows: per block, and one wired pin is enough ──────────────────────────────

        [Test]
        public void ABlockWithNoFedInputIsReportedOnce_HoweverManyInputsItHas()
        {
            Project none = Tree.WithRoot(Locality(Block(0x70, "Blok", wiredInput: false, wiredOutput: true)));
            Project one = Tree.WithRoot(Locality(Block(0x70, "Blok", wiredInput: true, wiredOutput: true)));

            Assert.Multiple(() =>
            {
                Assert.That(Count(none, "link-fb-input-unfed"), Is.EqualTo(1),
                    "ONE finding for the block, not one per unfed pin — the block has three inputs");
                Assert.That(Messages(none, "link-fb-input-unfed").Single(), Does.Contain("Blok"));
                Assert.That(Count(one, "link-fb-input-unfed"), Is.Zero,
                    "a single wired input feeds the block; the other two are alternatives the author declined");
            });
        }

        /// <summary>
        /// A BLOCK THAT STARTS ITSELF IS NOT WAITING FOR A WIRE. The row's sentence is "the trigger never arrives",
        /// and it is simply false of a clock block, of one triggered at power-up, and of one woken by a resource
        /// another block owns. Each of those is decidable from the file: an <c>event_power</c>, or an
        /// <c>&lt;event&gt;</c> whose <c>link1</c> resolves outside this block's own <c>inputs</c> container.
        /// </summary>
        [Test]
        public void ABlockWithAnAutonomousStartIsNotWaitingForAnInput()
        {
            Project powerUp = Tree.WithRoot(Locality(
                Block(0x70, "Powerup - Altid tændt", wiredInput: false, wiredOutput: true, start: Start.PowerUp)));
            Project foreignTrigger = Tree.WithRoot(Locality(
                Block(0x70, "Ur", wiredInput: false, wiredOutput: true, start: Start.InternalTimer)));

            Assert.Multiple(() =>
            {
                Assert.That(Count(powerUp, "link-fb-input-unfed"), Is.Zero,
                    "the controller starts it; no wire ever had to");
                Assert.That(Count(foreignTrigger, "link-fb-input-unfed"), Is.Zero,
                    "its trigger arrives from a resource outside its own inputs container");
            });
        }

        [Test]
        public void ABlockWithNoConsumedOutputIsReportedOnce_AndAScenePinCounts()
        {
            Project none = Tree.WithRoot(Locality(Block(0x70, "Blok", wiredInput: true, wiredOutput: false)));
            Project consumed = Tree.WithRoot(Locality(Block(0x70, "Blok", wiredInput: true, wiredOutput: true)));
            Project sceneOnly = Tree.WithRoot(Locality(
                Block(0x70, "Blok", wiredInput: true, wiredOutput: false, sceneWired: true)));

            Assert.Multiple(() =>
            {
                Assert.That(Count(none, "link-fb-output-unused"), Is.EqualTo(1));
                Assert.That(Count(consumed, "link-fb-output-unused"), Is.Zero);
                Assert.That(Count(sceneOnly, "link-fb-output-unused"), Is.Zero,
                    "a scenario reads the block's scene pin, so its result IS consumed");
            });
        }

        [Test]
        public void ABlockWithNoInputPinsAtAllIsNotReported()
        {
            Project project = Tree.WithRoot(Locality(BlockWithoutPins(0x80, "Tom blok")));

            Assert.Multiple(() =>
            {
                Assert.That(Count(project, "link-fb-input-unfed"), Is.Zero,
                    "a block that declares no input cannot be missing a link on one");
                Assert.That(Count(project, "link-fb-output-unused"), Is.Zero);
            });
        }

        // ── link-through-empty-block ────────────────────────────────────────────────────────────────

        [Test]
        public void AnEmptyBlockAWireRunsIntoIsReported_AndAnEmptyBlockNothingEntersIsNot()
        {
            Project entered = Tree.WithRoot(Locality(EmptyBlock(0x90, "Kobling", incoming: true)));
            Project untouched = Tree.WithRoot(Locality(EmptyBlock(0x90, "Kobling", incoming: false)));

            Assert.Multiple(() =>
            {
                Assert.That(Count(entered, "link-through-empty-block"), Is.EqualTo(1));
                Assert.That(Count(untouched, "link-through-empty-block"), Is.Zero,
                    "an empty block nothing links into is unused, which is the other row's finding");
            });
        }

        // ── link-pass-through, and the exclusion that makes it true ─────────────────────────────────

        [Test]
        public void APassThroughBlockIsReportedOnlyWhenTheBypassWouldBeLegal()
        {
            Project bypassable = PassThroughTree(upstreamIsBlockOutput: true);
            Project required = PassThroughTree(upstreamIsBlockOutput: false);

            Assert.Multiple(() =>
            {
                Assert.That(LinkRoles.CanLink("resource_output", "dataline_output"), Is.True,
                    "sanity: block-to-product IS a legal direct link, so this pass-through adds nothing");
                Assert.That(Count(bypassable, "link-pass-through"), Is.EqualTo(1));

                Assert.That(LinkRoles.CanLink("dataline_input", "dataline_output"), Is.False,
                    "sanity: IHC routes product-to-product through a block, so THAT block cannot be removed");
                Assert.That(Count(required, "link-pass-through"), Is.Zero,
                    "the row's stated consequence — a simpler path exists — would be false here");
            });
        }

        [Test]
        public void ABlockWithRealLogicIsNotAPassThrough()
        {
            Project withCondition = PassThroughTree(upstreamIsBlockOutput: true, extraAction: true);

            Assert.That(Count(withCondition, "link-pass-through"), Is.Zero,
                "a second command is logic — the block does more than copy");
        }

        // ── every WIR row is a Warning, and none of them blocks a save ──────────────────────────────

        [Test]
        public void EveryWiringFindingIsAdvisory()
        {
            Project project = Tree.WithRoot(Locality(Product(Input(0x52, "Tryk", wired: false))));
            ProjectValidationResult result = Validate(project);

            Assert.Multiple(() =>
            {
                Assert.That(result.Findings.Where(f => f.Category == ValidationCategory.Wiring), Is.Not.Empty);
                Assert.That(result.Findings.Where(f => f.Category == ValidationCategory.Wiring)
                    .All(f => f.Severity == ValidationSeverity.Warning), Is.True);
                Assert.That(result.IsValid, Is.True, "a wiring advisory never blocks a save");
            });
        }

        // ── rs485-dimmer-fault-unwired ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The product can report its own faults and this project throws that capability away: a load fault will
        /// never surface to the user.
        ///
        /// <para><b>The condition is "none of them", across every channel.</b> The four fault resources sit under
        /// each <c>rs485_led_dimmer_channel</c>, not under the product, so a two-channel dimmer exposes EIGHT and
        /// one linked flag anywhere is enough to make the row silent — partial wiring is a design choice, not
        /// this condition.</para>
        /// </summary>
        [Test]
        public void ADimmerWithNoLinkedFaultResourceIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmer(channels: 2, linkedFaults: 0), "rs485-dimmer-fault-unwired"),
                    Is.EqualTo(1), "eight fault resources, none of them linked");
                Assert.That(Count(Dimmer(channels: 2, linkedFaults: 1), "rs485-dimmer-fault-unwired"),
                    Is.Zero, "ONE linked flag is enough — partial wiring is a design choice");
                Assert.That(Count(Dimmer(channels: 1, linkedFaults: 0), "rs485-dimmer-fault-unwired"),
                    Is.EqualTo(1), "a one-channel dimmer exposes four, and the condition is the same");
                Assert.That(Count(Dimmer(channels: 2, linkedFaults: 8), "rs485-dimmer-fault-unwired"),
                    Is.Zero, "and a fully wired dimmer is what the row wants to see");
            });
        }

        /// <summary>
        /// KEYED ON THE ELEMENT TAGS, NOT ON THE DANISH NAMES. The format gives four dedicated tags, which are
        /// language-independent and not user-editable; the Danish <c>Fejl - Overstrøm</c> strings are ordinary
        /// <c>name</c> values an author can change.
        ///
        /// <para>A resource RENAMED away from the vendor's Danish still counts, and an ordinary resource NAMED
        /// like a fault flag does not. Both directions matter: an earlier draft keyed on the name.</para>
        /// </summary>
        [Test]
        public void TheFaultResourcesAreRecognisedByTagRatherThanByName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmer(channels: 1, linkedFaults: 1, faultName: "Renamed"),
                    "rs485-dimmer-fault-unwired"), Is.Zero,
                    "a renamed fault resource is still a fault resource, and this one is linked");
                Assert.That(Count(DimmerWithImpostor(), "rs485-dimmer-fault-unwired"), Is.EqualTo(1),
                    "and an ordinary linked resource NAMED like a fault flag does not satisfy the row");
            });
        }

        /// <summary>
        /// Nothing but an RS-485 LED dimmer is asked this question — the fault resources belong to that product,
        /// and no other family exposes them.
        /// </summary>
        [Test]
        public void OnlyTheLedDimmerIsAskedAboutFaultResources()
        {
            Assert.That(Count(Tree.WithRoot(Locality(Product(Input(0x52, "Tryk", wired: false)))),
                "rs485-dimmer-fault-unwired"), Is.Zero);
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        /// <summary>The four per-channel fault resources the format gives dedicated tags.</summary>
        private static readonly string[] FaultTags =
        [
            "rs485_led_dimmer_error_state_overcurrent",
            "rs485_led_dimmer_error_state_overvoltage",
            "rs485_led_dimmer_error_state_overheating",
            "rs485_led_dimmer_error_state_loadfailure",
        ];

        /// <summary>
        /// An RS-485 LED dimmer with the given number of channels, each exposing the four fault resources — the
        /// first <paramref name="linkedFaults"/> of them linked, counted across channels.
        /// </summary>
        /// <param name="channels">How many channels the dimmer carries.</param>
        /// <param name="linkedFaults">How many of its fault resources own a link half.</param>
        /// <param name="faultName">The Danish name to write on them, which the rule must not read.</param>
        private static Project Dimmer(int channels, int linkedFaults, string faultName = "Fejl - Overstrøm")
        {
            int linked = 0;
            ImmutableArray<ProjectElement>.Builder channelNodes = ImmutableArray.CreateBuilder<ProjectElement>();
            for (int c = 0; c < channels; c++)
            {
                ImmutableArray<ProjectElement>.Builder faults = ImmutableArray.CreateBuilder<ProjectElement>();
                for (int f = 0; f < FaultTags.Length; f++)
                {
                    int at = 0x300 + (c * 0x10) + f;
                    bool wire = linked++ < linkedFaults;
                    faults.Add(Tree.Node(FaultTags[f], Token(FaultTags[f], at), [("name", faultName)],
                        wire
                            ? [Tree.Node("link_from_resource", Token("link_from_resource", at + 0x100),
                                [("name", "Følg Link"), ("link", Token("resource_input", 0x72))])]
                            : []));
                }

                channelNodes.Add(Tree.Node("rs485_led_dimmer_channel",
                    Token("rs485_led_dimmer_channel", 0x200 + c),
                    [("name", $"Kanal {c}"), ("channel_id", $"_0x{c + 1}")], [.. faults]));
            }

            return Tree.WithRoot(Locality(
                Tree.Node("product_rs485_led_dimmer", Token("product_rs485_led_dimmer", 0x51),
                    [("product_identifier", "_0x9e10"), ("name", "Dæmper")], [.. channelNodes])));
        }

        /// <summary>
        /// A dimmer whose fault resources are all unlinked, beside an ORDINARY resource named like a fault flag
        /// and linked — the shape that would fool a name-keyed predicate.
        /// </summary>
        private static Project DimmerWithImpostor() =>
            Tree.WithRoot(Locality(
                Tree.Node("product_rs485_led_dimmer", Token("product_rs485_led_dimmer", 0x51),
                    [("product_identifier", "_0x9e10"), ("name", "Dæmper")],
                    Tree.Node("rs485_led_dimmer_channel", Token("rs485_led_dimmer_channel", 0x200),
                        [("name", "Kanal 0"), ("channel_id", "_0x1")],
                        [
                            .. FaultTags.Select((tag, f) => Tree.Node(tag, Token(tag, 0x300 + f),
                                [("name", "Fejl - Overstrøm")])),
                            Tree.Node("resource_flag", Token("resource_flag", 0x320),
                                [("name", "Fejl - Overstrøm")],
                                Tree.Node("link_from_resource", Token("link_from_resource", 0x420),
                                    [("name", "Følg Link"), ("link", Token("resource_input", 0x72))])),
                        ]))));

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static ProjectElement Locality(params ProjectElement[] contents) =>
            Tree.Node("groups", Token("groups", 0x20), [("name", "Lokaliteter")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], contents));

        private static ProjectElement Product(params ProjectElement[] pins) =>
            Tree.Node("product_dataline", Token("product_dataline", 0x51),
                [("product_identifier", "_0x2202"), ("name", "Produkt")], pins);

        private static ProjectElement Pin(string tag, int at, string name, ProjectElement[]? halves) =>
            Tree.Node(tag, Token(tag, at), [("name", name)], halves ?? []);

        private static ProjectElement Input(int at, string name, bool wired) =>
            Pin("dataline_input", at, name,
                wired
                    ? [Tree.Node("link_from_resource", Token("link_from_resource", at + 0x100),
                        [("name", "Følg Link"), ("link", Token("resource_input", 0x72))])]
                    : null);

        private static ProjectElement Output(int at, string name, int drivers) =>
            Pin("dataline_output", at, name,
                [.. Enumerable.Range(0, drivers).Select(i =>
                    Tree.Node("link_to_resource", Token("link_to_resource", at + 0x200 + i),
                        [("name", "Følg Link"), ("link", Token("resource_output", 0x73))]))]);

        /// <summary>
        /// What makes a block's program run. The three are mutually exclusive, which is why they are one parameter
        /// and not a boolean each — <c>link-fb-input-unfed</c> discriminates on exactly this.
        /// </summary>
        private enum Start
        {
            /// <summary>An event on one of the block's own input pins: it waits for a wire.</summary>
            InputPin,

            /// <summary>An <c>event_power</c>: it runs at power-up and waits for nothing.</summary>
            PowerUp,

            /// <summary>An event on a timer the block owns: it starts itself.</summary>
            InternalTimer,
        }

        /// <summary>A block with three inputs, one output and a scene pin — the shape a catalog block has.</summary>
        /// <param name="start">What makes its program run; an input pin unless stated.</param>
        private static ProjectElement Block(
            int at, string name, bool wiredInput, bool wiredOutput, bool sceneWired = false,
            Start start = Start.InputPin) =>
            Tree.Node("functionblock", Token("functionblock", at), [("name", name)],
                Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")],
                    Pin("resource_input", at + 0x10, "Kip", wiredInput ? Half("link_to_resource", at + 0x10) : null),
                    Pin("resource_input", at + 0x11, "Kip med timer", null),
                    Pin("resource_input", at + 0x12, "Tryk", null)),
                Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")],
                    Pin("resource_output", at + 0x20, "Udgang", wiredOutput ? Half("link_from_resource", at + 0x20) : null),
                    Pin("resource_scene", at + 0x21, "Scenarie", sceneWired ? Half("scene_link", at + 0x21) : null)),
                Tree.Node("settings", Token("settings", at + 3), [("name", "S")]),
                Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "IS")],
                    start == Start.InternalTimer ? [Pin("resource_timer", at + 0x30, "Ur", null)] : []),
                Tree.Node("programs", Token("programs", at + 5), [("name", "P")],
                    Program(
                        at + 6, start == Start.InternalTimer ? at + 0x30 : at + 0x10, at + 0x20, start: start)));

        private static ProjectElement[] Half(string tag, int at) =>
            [Tree.Node(tag, Token(tag, at + 0x300), [("name", "Følg Link"), ("link", Token("resource_input", 0x300))])];

        private static ProjectElement BlockWithoutPins(int at, string name) =>
            Tree.Node("functionblock", Token("functionblock", at), [("name", name)],
                Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")]),
                Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")]),
                Tree.Node("settings", Token("settings", at + 3), [("name", "S")]),
                Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "IS")]),
                Tree.Node("programs", Token("programs", at + 5), [("name", "P")]));

        /// <summary>A block with NO programs, optionally with a wire running into it.</summary>
        private static ProjectElement EmptyBlock(int at, string name, bool incoming) =>
            Tree.Node("functionblock", Token("functionblock", at), [("name", name)],
                Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")],
                    Pin("resource_input", at + 0x10, "Kip", incoming ? Half("link_to_resource", at + 0x10) : null)),
                Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")],
                    Pin("resource_output", at + 0x20, "Udgang", Half("link_from_resource", at + 0x20))),
                Tree.Node("settings", Token("settings", at + 3), [("name", "S")]),
                Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "IS")]),
                Tree.Node("programs", Token("programs", at + 5), [("name", "P")]));

        /// <param name="triggerAt">The element the program's event names — an input pin, or the block's own timer.</param>
        private static ProjectElement Program(
            int at, int triggerAt, int outputAt, bool extraAction = false, Start start = Start.InputPin) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", "Kip")],
                Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                    start == Start.PowerUp
                        ? Tree.Node("event_power", Token("event_power", at + 2), [("name", "Powerup")])
                        : Tree.Node("event", Token("event", at + 2),
                            [("name", "%P -> ON"),
                                ("link1", Token(
                                    start == Start.InternalTimer ? "resource_timer" : "resource_input", triggerAt)),
                                ("method", "_0xa")])),
                Tree.Node("actions", Token("actions", at + 3), [("name", "Kommandoer"), ("type", "_0x2")],
                    [.. Actions(at, outputAt, extraAction)]));

        private static ProjectElement[] Actions(int at, int outputAt, bool extraAction)
        {
            ImmutableArray<ProjectElement>.Builder actions = ImmutableArray.CreateBuilder<ProjectElement>();
            actions.Add(Tree.Node("action", Token("action", at + 4),
                [("name", "%P = ON"), ("link1", Token("resource_output", outputAt)), ("method", "_0x1")]));
            if (extraAction)
            {
                actions.Add(Tree.Node("action", Token("action", at + 5),
                    [("name", "%P = OFF"), ("link1", Token("resource_output", outputAt)), ("method", "_0x2")]));
            }

            return [.. actions];
        }

        /// <summary>
        /// A one-in-one-out block, wired either between two blocks (bypassable — a block output may link straight
        /// to a product output) or between a product input and a product output (NOT bypassable: IHC routes every
        /// product-to-product path through a block).
        /// </summary>
        private static Project PassThroughTree(bool upstreamIsBlockOutput, bool extraAction = false)
        {
            string upstreamTag = upstreamIsBlockOutput ? "resource_output" : "dataline_input";
            int upstreamAt = upstreamIsBlockOutput ? 0x62 : 0x52;

            ProjectElement upstreamHalf = Tree.Node("link_from_resource", Token("link_from_resource", 0x500),
                [("name", "Følg Link"), ("link", Token("link_to_resource", 0x501))]);
            ProjectElement inputHalf = Tree.Node("link_to_resource", Token("link_to_resource", 0x501),
                [("name", "Følg Link"), ("link", Token("link_from_resource", 0x500))]);
            ProjectElement outputHalf = Tree.Node("link_from_resource", Token("link_from_resource", 0x502),
                [("name", "Følg Link"), ("link", Token("link_to_resource", 0x503))]);
            ProjectElement sinkHalf = Tree.Node("link_to_resource", Token("link_to_resource", 0x503),
                [("name", "Følg Link"), ("link", Token("link_from_resource", 0x502))]);

            // The upstream source: either another block's output pin, or a product input terminal.
            ProjectElement upstream = upstreamIsBlockOutput
                ? Tree.Node("functionblock", Token("functionblock", 0x60), [("name", "Kilde")],
                    Tree.Node("inputs", Token("inputs", 0x61), [("name", "Input")]),
                    Tree.Node("outputs", Token("outputs", 0x63), [("name", "Output")],
                        Tree.Node("resource_output", Token("resource_output", upstreamAt), [("name", "Ud")], upstreamHalf)),
                    Tree.Node("settings", Token("settings", 0x64), [("name", "S")]),
                    Tree.Node("internalsettings", Token("internalsettings", 0x65), [("name", "IS")]),
                    Tree.Node("programs", Token("programs", 0x66), [("name", "P")]))
                : Tree.Node("product_dataline", Token("product_dataline", 0x51),
                    [("product_identifier", "_0x2202"), ("name", "Tryk-produkt")],
                    Tree.Node(upstreamTag, Token(upstreamTag, upstreamAt), [("name", "Tryk")], upstreamHalf));

            ProjectElement copier = Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Kopi")],
                Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                    Tree.Node("resource_input", Token("resource_input", 0x72), [("name", "Ind")], inputHalf)),
                Tree.Node("outputs", Token("outputs", 0x73), [("name", "Output")],
                    Tree.Node("resource_output", Token("resource_output", 0x74), [("name", "Ud")], outputHalf)),
                Tree.Node("settings", Token("settings", 0x75), [("name", "S")]),
                Tree.Node("internalsettings", Token("internalsettings", 0x76), [("name", "IS")]),
                Tree.Node("programs", Token("programs", 0x77), [("name", "P")],
                    Program(0x78, 0x72, 0x74, extraAction)));

            ProjectElement sink = Tree.Node("product_dataline", Token("product_dataline", 0x81),
                [("product_identifier", "_0x2203"), ("name", "Lampe-produkt")],
                Tree.Node("dataline_output", Token("dataline_output", 0x82), [("name", "Udgang")], sinkHalf));

            return Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], upstream, copier, sink)));
        }
    }
}
