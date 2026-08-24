using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T046 — the eight WIRING rows, per rule, over the partitions each one's predicate names.
    ///
    /// <para><b>Built as synthetic trees, and that is the only way to reach these conditions deliberately.</b> An
    /// authentic file carries whatever wiring its author happened to make; a tree built here carries exactly one
    /// condition and its neighbouring non-condition, which is what an equivalence partition needs. The authentic
    /// corpus still guards the other direction — <c>ValidationCharacterizationTests</c> records every finding these
    /// rules produce over five vendor-authored files, so a predicate that grew a false positive shows up there.</para>
    ///
    /// <para><b>Each rule is tested at the boundary its predicate states</b>: nought/one/two drivers for the
    /// multi-driven row, one linked pin versus none for the two block rows, same/different locality, and — for the
    /// pass-through row — a legal bypass versus an illegal one, which is the exclusion that makes the row true.</para>
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

        // ── link-input-unconnected ──────────────────────────────────────────────────────────────────

        [Test]
        public void AProductInputWithNoLinkIsReported_AndOneWithALinkIsNot()
        {
            Project unwired = Tree.WithRoot(Locality(Product(Input(0x52, "Tryk", wired: false))));
            Project wired = Tree.WithRoot(Locality(Product(Input(0x52, "Tryk", wired: true))));

            Assert.Multiple(() =>
            {
                Assert.That(Count(unwired, "link-input-unconnected"), Is.EqualTo(1));
                Assert.That(Messages(unwired, "link-input-unconnected").Single(), Does.Contain("Tryk"),
                    "the pin's own name is bound into the sentence");
                Assert.That(Count(wired, "link-input-unconnected"), Is.Zero, "a wired input is not reported");
            });
        }

        [Test]
        public void AWirelessProductInputIsInTheSubjectSetToo()
        {
            Project project = Tree.WithRoot(Locality(
                Tree.Node("product_airlink", Token("product_airlink", 0x60),
                    [("product_identifier", "_0x2202"), ("name", "Trådløs")],
                    Pin("airlink_input", 0x61, "Trådløst tryk", null))));

            Assert.That(Count(project, "link-input-unconnected"), Is.EqualTo(1),
                "the row says wired OR wireless, and the measured never-a-sink family is both");
        }

        // ── link-output-undriven, and the scenario exclusion ────────────────────────────────────────

        [Test]
        public void AProductOutputWithNoLinkIsReported()
        {
            Project project = Tree.WithRoot(Locality(Product(Output(0x53, "Udgang", drivers: 0))));

            Assert.That(Count(project, "link-output-undriven"), Is.EqualTo(1));
        }

        [Test]
        public void AnOutputAScenarioDrivesIsNotReported()
        {
            // The scenes container names the output resource: a scenario switches it, so "can never be switched"
            // would be a false statement about this pin.
            Project project = Tree.WithRoot(Locality(
                Tree.Node("product_dataline", Token("product_dataline", 0x51),
                    [("product_identifier", "_0x2202"), ("name", "Produkt")],
                    Output(0x53, "Udgang", drivers: 0),
                    Tree.Node("scenes", Token("scenes", 0x56),
                        [("name", "Scenarier"), ("scene_resource", Token("dataline_output", 0x53))]))));

            Assert.Multiple(() =>
            {
                Assert.That(Count(project, "link-output-undriven"), Is.Zero, "the declared exclusion");
                Assert.That(Count(project, "link-output-multidriven"), Is.Zero);
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

        // ── link-crosses-locality ───────────────────────────────────────────────────────────────────

        [Test]
        public void ALinkInsideOneLocalityIsFine_OneAcrossTwoIsReportedOnce()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(CrossLocalityTree(sameLocality: true), "link-crosses-locality"), Is.Zero);

                Project across = CrossLocalityTree(sameLocality: false);
                Assert.That(Count(across, "link-crosses-locality"), Is.EqualTo(1),
                    "ONE finding for one wire — reporting both halves would say it twice");
                Assert.That(Messages(across, "link-crosses-locality").Single(),
                    Does.Contain("Stue").And.Contain("Logik"), "both locality names are bound");
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

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

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

        /// <summary>A block with three inputs, one output and a scene pin — the shape a catalog block has.</summary>
        private static ProjectElement Block(
            int at, string name, bool wiredInput, bool wiredOutput, bool sceneWired = false) =>
            Tree.Node("functionblock", Token("functionblock", at), [("name", name)],
                Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")],
                    Pin("resource_input", at + 0x10, "Kip", wiredInput ? Half("link_to_resource", at + 0x10) : null),
                    Pin("resource_input", at + 0x11, "Kip med timer", null),
                    Pin("resource_input", at + 0x12, "Tryk", null)),
                Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")],
                    Pin("resource_output", at + 0x20, "Udgang", wiredOutput ? Half("link_from_resource", at + 0x20) : null),
                    Pin("resource_scene", at + 0x21, "Scenarie", sceneWired ? Half("scene_link", at + 0x21) : null)),
                Tree.Node("settings", Token("settings", at + 3), [("name", "S")]),
                Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "IS")]),
                Tree.Node("programs", Token("programs", at + 5), [("name", "P")],
                    Program(at + 6, at + 0x10, at + 0x20)));

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

        private static ProjectElement Program(int at, int inputAt, int outputAt, bool extraAction = false) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", "Kip")],
                Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                    Tree.Node("event", Token("event", at + 2),
                        [("name", "%P -> ON"), ("link1", Token("resource_input", inputAt)), ("method", "_0xa")])),
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

        /// <summary>Two localities, with one follow-link that either stays inside one or crosses.</summary>
        private static Project CrossLocalityTree(bool sameLocality)
        {
            ProjectElement fromHalf = Tree.Node("link_from_resource", Token("link_from_resource", 0x400),
                [("name", "Følg Link"), ("link", Token("link_to_resource", 0x401))]);
            ProjectElement toHalf = Tree.Node("link_to_resource", Token("link_to_resource", 0x401),
                [("name", "Følg Link"), ("link", Token("link_from_resource", 0x400))]);

            ProjectElement product = Tree.Node("product_dataline", Token("product_dataline", 0x51),
                [("product_identifier", "_0x2202"), ("name", "Produkt")],
                Tree.Node("dataline_input", Token("dataline_input", 0x52), [("name", "Tryk")], fromHalf));
            ProjectElement block = Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                    Tree.Node("resource_input", Token("resource_input", 0x72), [("name", "Kip")], toHalf)),
                Tree.Node("outputs", Token("outputs", 0x73), [("name", "Output")]),
                Tree.Node("settings", Token("settings", 0x74), [("name", "S")]),
                Tree.Node("internalsettings", Token("internalsettings", 0x75), [("name", "IS")]),
                Tree.Node("programs", Token("programs", 0x76), [("name", "P")]));

            return sameLocality
                ? Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")], product, block)))
                : Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")], product),
                    Tree.Node("group", Token("group", 0x22), [("name", "Logik")], block)));
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
