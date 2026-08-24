using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T047 — the seven SCENARIO rows, per rule, over the partitions each predicate names, plus boundary-value
    /// coverage of the ONE declared threshold in this set.
    ///
    /// <para><b>The scene shape these trees build.</b> A scene is a block's <c>resource_scene</c> pin holding
    /// <c>scene_link</c> halves; each half points at a member row in a product's <c>scenes</c> container; that
    /// container names the output the row drives. Every tree here is that shape with exactly one thing wrong, which
    /// is what an equivalence partition needs and what an authentic file cannot be made to carry on demand.</para>
    ///
    /// <para><b>The threshold is read from the catalogue, not retyped.</b> <c>scene-long-delay</c>'s limit is
    /// declared data on its entry; the boundary test reads it from there and probes at, just inside and just past
    /// it — so the test cannot drift from the number the rule uses, and changing the declaration moves both.</para>
    /// </summary>
    [TestFixture]
    public sealed class ScenarioRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        /// <summary>The declared maximum scene ramp, in seconds, read from the entry that owns it.</summary>
        private static double DeclaredRampLimit
        {
            get
            {
                Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("scene-long-delay"),
                    out ProblemCatalogEntry entry), Is.True);
                DeclaredThreshold threshold = entry.Thresholds.Single(t => t.Name == "MaxRampSeconds");
                Assert.That(threshold.Confidence, Is.EqualTo(ThresholdConfidence.Authored),
                    "no vendor source states a scene-ramp maximum, and the entry must say so");
                Assert.That(threshold.Evidence, Does.Contain("TODO"),
                    "an authored threshold carries its unconfirmed status where it is declared");
                return threshold.Value;
            }
        }

        // ── scene-empty ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void AnEmptyScenePinIsReported_AndAPopulatedOneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Scene(members: 0), "scene-empty"), Is.EqualTo(1));
                Assert.That(Message(Scene(members: 0), "scene-empty"), Does.Contain("Scenarie"));
                Assert.That(Count(Scene(members: 1), "scene-empty"), Is.Zero);
            });
        }

        // ── scene-duplicate-target ──────────────────────────────────────────────────────────────────

        [Test]
        public void TwoMembersOfOneSceneOnOneOutputAreReportedOnce()
        {
            Project duplicated = Scene(members: 2, sameOutput: true);
            Project distinct = Scene(members: 2, sameOutput: false);

            Assert.Multiple(() =>
            {
                Assert.That(Count(duplicated, "scene-duplicate-target"), Is.EqualTo(1),
                    "ONE finding for the collision, with both rows as related locations");
                Assert.That(Count(distinct, "scene-duplicate-target"), Is.Zero,
                    "two members driving two different outputs is an ordinary scene");
            });
        }

        // ── scene-member-unwired ────────────────────────────────────────────────────────────────────

        [Test]
        public void AMemberRowWhoseContainerBindsNoOutputIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Scene(members: 1, boundOutput: false), "scene-member-unwired"), Is.EqualTo(1));
                Assert.That(Message(Scene(members: 1, boundOutput: false), "scene-member-unwired"),
                    Does.Contain("Produkt"), "the product is named, because the row's own name identifies nothing");
                Assert.That(Count(Scene(members: 1), "scene-member-unwired"), Is.Zero);
            });
        }

        [Test]
        public void AContainerBindingAnUnknownOutputIsReportedToo()
        {
            Project dangling = Scene(members: 1, boundOutput: true, boundToken: "_0xdead49");

            Assert.That(Count(dangling, "scene-member-unwired"), Is.EqualTo(1),
                "an output token that resolves to nothing is no output at all");
        }

        // ── scene-unreferenced ──────────────────────────────────────────────────────────────────────

        [Test]
        public void ASceneNoProgramNamesIsReported_AndOneAnActionFiresIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Scene(members: 1), "scene-unreferenced"), Is.EqualTo(1),
                    "its own member halves do not make it reachable — they are what it drives");
                Assert.That(Count(Scene(members: 1, firedByProgram: true), "scene-unreferenced"), Is.Zero);
            });
        }

        /// <summary>
        /// What the ARMING of this suite found: a scene's own member halves never name the pin, so the
        /// reachability scan needs no exclusion for them. Seeding <c>scene_link</c> into the operand tag list
        /// changed nothing, which is only safe if the halves really do point elsewhere — so that is asserted here
        /// rather than left as a comment nobody can check.
        /// </summary>
        [Test]
        public void ASceneHalfNamesTheMemberRowAndNeverThePin()
        {
            Project project = Scene(members: 1);
            ProjectElement pin = project.Root.FindDescendantOrSelf(e => e.Tag == "resource_scene")!;
            ProjectElement half = pin.Children.Single(c => c.Tag == "scene_link");
            ProjectElement row = project.Root.FindDescendantOrSelf(e => e.Tag == "scene_relay")!;

            Assert.Multiple(() =>
            {
                Assert.That(half.GetAttribute("link"), Is.EqualTo(row.GetAttribute("id")),
                    "the half names the member ROW");
                Assert.That(row.GetAttribute("link"), Is.EqualTo(half.GetAttribute("id")),
                    "and the row names the half back");
                Assert.That(half.GetAttribute("link"), Is.Not.EqualTo(pin.GetAttribute("id")),
                    "neither names the PIN, which is why only a program operand can make a scene reachable");
            });
        }

        /// <summary>
        /// A scene fired from a CONDITION rather than an action is reachable too: the operand list is four
        /// attributes, and pinning only the action case would let a narrowed list pass.
        /// </summary>
        [Test]
        public void ASceneNamedByAConditionOperandIsReachable()
        {
            Assert.That(Count(Scene(members: 1, firedByCondition: true), "scene-unreferenced"), Is.Zero,
                "link2 on a condition names the scene as surely as link1 on an action does");
        }

        // ── scene-output-also-linked ────────────────────────────────────────────────────────────────

        [Test]
        public void AnOutputBothASceneAndALinkDriveIsReportedOnce()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Scene(members: 1, outputAlsoLinked: true), "scene-output-also-linked"),
                    Is.EqualTo(1));
                Assert.That(Count(Scene(members: 1), "scene-output-also-linked"), Is.Zero);
            });
        }

        // ── scene-all-off ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void ASceneEveryMemberOfWhichIsOffIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Scene(members: 2, sameOutput: false, relayValue: "off"), "scene-all-off"),
                    Is.EqualTo(1));
                Assert.That(Message(Scene(members: 2, sameOutput: false, relayValue: "off"), "scene-all-off"),
                    Does.Contain("2"), "the member count is bound as data");
                Assert.That(Count(Scene(members: 2, sameOutput: false, relayValue: "on"), "scene-all-off"), Is.Zero);
                Assert.That(Count(Scene(members: 0), "scene-all-off"), Is.Zero,
                    "an empty scene is the other row's finding, not an all-off one");
            });
        }

        [Test]
        public void OneMemberThatIsNotOffClearsTheWholeScene()
        {
            Project mixed = Scene(members: 2, sameOutput: false, relayValue: "off", secondRelayValue: "on");

            Assert.That(Count(mixed, "scene-all-off"), Is.Zero,
                "EVERY member has to be off — one that is not makes it an ordinary scene");
        }

        // ── scene-long-delay: the declared threshold, at and around it ──────────────────────────────

        [Test]
        public void TheRampThresholdIsDeclaredDataWithItsProvenance()
        {
            Assert.That(DeclaredRampLimit, Is.GreaterThan(0),
                "the row says 'unusually long' and names no figure, so the figure is declared on the entry");
        }

        [Test]
        public void ARampAtTheThresholdPasses_AndOneJustPastItIsReported()
        {
            long limitMs = (long)(DeclaredRampLimit * 1000);

            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimmer(rampMs: 0), "scene-long-delay"), Is.Zero, "no ramp at all");
                Assert.That(Count(Dimmer(rampMs: limitMs - 1), "scene-long-delay"), Is.Zero, "just inside");
                Assert.That(Count(Dimmer(rampMs: limitMs), "scene-long-delay"), Is.Zero,
                    "AT the threshold is not past it");
                Assert.That(Count(Dimmer(rampMs: limitMs + 1), "scene-long-delay"), Is.EqualTo(1),
                    "one millisecond past it is the finding");
                Assert.That(Count(Dimmer(rampMs: limitMs * 10), "scene-long-delay"), Is.EqualTo(1));
                Assert.That(Message(Dimmer(rampMs: limitMs * 10), "scene-long-delay"),
                    Does.Contain(DeclaredRampLimit.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                    "the sentence states what the ramp is measured against");
            });
        }

        [Test]
        public void ARelayMemberHasNoRampAndIsNeverReported()
        {
            Assert.That(Count(Scene(members: 1), "scene-long-delay"), Is.Zero);
        }

        // ── every SCN row is advisory ───────────────────────────────────────────────────────────────

        [Test]
        public void EveryScenarioFindingIsAdvisory()
        {
            ProjectValidationResult result = Validate(Scene(members: 0));

            Assert.Multiple(() =>
            {
                Assert.That(result.Findings.Where(f => f.Category == ValidationCategory.Scenes), Is.Not.Empty);
                Assert.That(result.Findings.Where(f => f.Category == ValidationCategory.Scenes)
                    .All(f => f.Severity == ValidationSeverity.Warning), Is.True);
                Assert.That(result.IsValid, Is.True);
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        /// <summary>
        /// A project with one block carrying one scene pin, and one product per member row. Every switch is one
        /// partition of one predicate: how many members, whether they share an output, whether the container binds
        /// one at all, whether a program fires the scene, whether the output is also link-driven, and the values
        /// the rows carry.
        /// </summary>
        private static Project Scene(
            int members,
            bool sameOutput = false,
            bool boundOutput = true,
            string? boundToken = null,
            bool firedByProgram = false,
            bool firedByCondition = false,
            bool outputAlsoLinked = false,
            string relayValue = "on",
            string? secondRelayValue = null,
            long? dimmerRampMs = null)
        {
            ImmutableArray<ProjectElement>.Builder products = ImmutableArray.CreateBuilder<ProjectElement>();
            ImmutableArray<ProjectElement>.Builder halves = ImmutableArray.CreateBuilder<ProjectElement>();

            for (int i = 0; i < members; i++)
            {
                int at = 0x100 + (i * 0x10);
                int outputAt = sameOutput ? 0x100 : at;   // one shared output, or one per member
                string memberTag = dimmerRampMs is null ? "scene_relay" : "scene_dimmer";
                (string, string)[] valueAttributes = dimmerRampMs is { } ramp
                    ? [("name", "Scenarie link"), ("link", Token("scene_link", at + 1)),
                       ("dimming_value", "50"), ("ramptime_ms", ramp.ToString(System.Globalization.CultureInfo.InvariantCulture))]
                    : [("name", "Scenarie link"), ("link", Token("scene_link", at + 1)),
                       ("relay_value", i == 1 && secondRelayValue is not null ? secondRelayValue : relayValue)];

                ProjectElement row = Tree.Node(memberTag, Token(memberTag, at + 2), valueAttributes);
                ProjectElement output = Tree.Node("dataline_output", Token("dataline_output", outputAt),
                    [("name", "Udgang " + i)],
                    outputAlsoLinked
                        ? [Tree.Node("link_to_resource", Token("link_to_resource", at + 3),
                            [("name", "Følg Link"), ("link", Token("link_from_resource", 0x900))])]
                        : []);

                (string, string)[] containerAttributes = boundOutput
                    ? [("name", "Scenarier"), ("scene_resource", boundToken ?? Token("dataline_output", outputAt))]
                    : [("name", "Scenarier")];

                // With a shared output only the FIRST product carries it; the others bind the same token.
                ProjectElement[] contents = sameOutput && i > 0
                    ? [Tree.Node("scenes", Token("scenes", at + 4), containerAttributes, row)]
                    : [output, Tree.Node("scenes", Token("scenes", at + 4), containerAttributes, row)];

                products.Add(Tree.Node("product_dataline", Token("product_dataline", at + 5),
                    [("product_identifier", "_0x2202"), ("name", "Produkt " + i)], contents));
                halves.Add(Tree.Node("scene_link", Token("scene_link", at + 1),
                    [("name", "Scenarie link"), ("link", Token(memberTag, at + 2))]));
            }

            ProjectElement scenePin = Tree.Node("resource_scene", Token("resource_scene", 0x74),
                [("name", "Scenarie Tænd")], [.. halves]);

            ProjectElement block = Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                    Tree.Node("resource_input", Token("resource_input", 0x72), [("name", "Kip")])),
                Tree.Node("outputs", Token("outputs", 0x73), [("name", "Output")], scenePin),
                Tree.Node("settings", Token("settings", 0x75), [("name", "S")]),
                Tree.Node("internalsettings", Token("internalsettings", 0x76), [("name", "IS")]),
                Tree.Node("programs", Token("programs", 0x77), [("name", "P")],
                    firedByProgram || firedByCondition ? [FiringProgram(viaCondition: firedByCondition)] : []));

            return Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], [.. products.ToImmutable(), block])));
        }

        /// <summary>
        /// A program that names the scene pin — what makes a scene reachable. Either through its action's
        /// <c>link1</c> or through a condition's <c>link2</c>, which is the same question asked of a different
        /// declared operand.
        /// </summary>
        private static ProjectElement FiringProgram(bool viaCondition = false) =>
            Tree.Node("program_simple", Token("program_simple", 0x78), [("name", "Kald scenarie")],
                Tree.Node("events", Token("events", 0x79), [("name", "Hændelser")],
                    Tree.Node("event", Token("event", 0x7a),
                        [("name", "%P -> ON"), ("link1", Token("resource_input", 0x72)), ("method", "_0xa")])),
                Tree.Node("actions", Token("actions", 0x7b), [("name", "Kommandoer"), ("type", "_0x2")],
                    viaCondition
                        ? Tree.Node("program_sub", Token("program_sub", 0x7d), [("name", "Under program")],
                            Tree.Node("conditions", Token("conditions", 0x7e), [("name", "Betingelser")],
                                Tree.Node("condition", Token("condition", 0x7f),
                                    [("name", "%P = ON"), ("link2", Token("resource_scene", 0x74)),
                                     ("method", "_0x14")])),
                            Tree.Node("actions", Token("actions", 0x80),
                                [("name", "Kommandoer"), ("type", "_0x1")]))
                        : Tree.Node("action", Token("action", 0x7c),
                            [("name", "%P = ON"), ("link1", Token("resource_scene", 0x74)), ("method", "_0x1")])));

        /// <summary>A scene with one DIMMER member carrying the given ramp — the long-delay subject.</summary>
        private static Project Dimmer(long rampMs) =>
            Scene(members: 1, firedByProgram: true, dimmerRampMs: rampMs);
    }
}
