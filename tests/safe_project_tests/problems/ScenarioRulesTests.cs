using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Schema;
using static Ihc.Vis.Tests.RuleProbe;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T047 — the SCENARIO rows, per rule, over the partitions each predicate names, plus boundary-value
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

        // ── scene-dimming-out-of-range ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Both ends of the range, and both boundaries. A light level past 100 % means nothing to any dimmer, and
        /// the vendor dialog silently zeroes it the first time the member is committed — so the value the author
        /// wrote quietly becomes 0.
        ///
        /// <para><b>The bounds are read from the entry, not retyped</b>, exactly as the ramp boundary test above
        /// reads its own. Changing either declaration moves the rule and this test together.</para>
        /// </summary>
        [Test]
        public void ADimmingValueOutsideTheDeclaredRangeIsReportedAtBothEnds()
        {
            int minimum = (int)DeclaredDimming("DimmingMinimum");
            int maximum = (int)DeclaredDimming("DimmingMaximum");

            Assert.Multiple(() =>
            {
                Assert.That(Count(Dimming($"{maximum + 1}"), "scene-dimming-out-of-range"), Is.EqualTo(1),
                    "one past the maximum");
                Assert.That(Count(Dimming($"{minimum - 1}"), "scene-dimming-out-of-range"), Is.EqualTo(1),
                    "one below the minimum");
                Assert.That(Count(Dimming($"{maximum}"), "scene-dimming-out-of-range"), Is.Zero,
                    "AT the maximum: 100 % is a legal light level");
                Assert.That(Count(Dimming($"{minimum}"), "scene-dimming-out-of-range"), Is.Zero,
                    "AT the minimum: 0 % is the off state, and legal");
                Assert.That(Count(Dimming("60"), "scene-dimming-out-of-range"), Is.Zero, "well inside");
            });
        }

        [Test]
        public void TheDimmingRangeFindingNamesTheRowTheValueAndTheLegalRange()
        {
            Assert.That(Message(Dimming("150"), "scene-dimming-out-of-range"),
                Is.EqualTo("Scenemedlemmet 'Scenarie link' har lysniveauet 150 %; det gyldige område er 0-100 %."));
        }

        /// <summary>
        /// A Warning rather than an Error, and the reason is the axis §2 draws. The file layer carries the value
        /// and renders it; the demonstrated harm is the vendor dialog's silent zeroing on commit, and controller
        /// behaviour is untested. An Error's consequence has to hold whatever the author intended — this one
        /// depends on which tool touches the row next.
        /// </summary>
        [Test]
        public void TheDimmingRangeFindingIsAdvisoryBecauseTheHarmDependsOnTheNextTool()
        {
            Assert.That(Validate(Dimming("150")).Findings
                .Single(f => f.RuleId == "scene-dimming-out-of-range").Severity,
                Is.EqualTo(ValidationSeverity.Warning));
        }

        /// <summary>
        /// THE EXCLUSIONS. A relay row carries no <c>dimming_value</c> at all, and a dimmer row that carries none
        /// is unset rather than out of range — a different state, owned by other rows. Neither is reported, and
        /// neither can be: a rule that read a missing attribute as 0 would report every relay in the corpus.
        /// </summary>
        [Test]
        public void ARowCarryingNoDimmingValueIsNotReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Scene(members: 1), "scene-dimming-out-of-range"), Is.Zero,
                    "a relay member row has no light level to be out of range");
                Assert.That(Count(Dimming(null), "scene-dimming-out-of-range"), Is.Zero,
                    "and an unparseable or absent value is not a range violation");
            });
        }

        /// <summary>The declared dimming bound of that name, with the confidence grade the entry must carry.</summary>
        /// <param name="name">The threshold's declared name.</param>
        private static double DeclaredDimming(string name)
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("scene-dimming-out-of-range"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold threshold = entry.Thresholds.Single(t => t.Name == name);

            if (name == "DimmingMinimum")
            {
                Assert.That(threshold.Confidence, Is.EqualTo(ThresholdConfidence.Authored),
                    "no source probed the lower bound — the spinner was driven upward only");
                Assert.That(threshold.Evidence, Does.Contain("TODO"),
                    "an authored threshold carries its unconfirmed status where it is declared");
            }
            else
            {
                Assert.That(threshold.Confidence, Is.EqualTo(ThresholdConfidence.VendorDocumented),
                    "the vendor's own Lysniveau spinner stops at 100 and does not wrap");
            }

            return threshold.Value;
        }

        /// <summary>One scene whose single dimmer member carries the given light level, and no ramp.</summary>
        /// <param name="value">The <c>dimming_value</c> to store, or null for a row that carries none.</param>
        private static Project Dimming(string? value) =>
            Scene(members: 1, dimmingValue: value ?? "");

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        /// <summary>
        /// A16: the affected RS-485 LED dimmer driven through SCENARIO RECALL.
        ///
        /// <para><b>Why this row earns its place, and why the fix cannot be narrowed on.</b> The defect is fixed
        /// by <i>dimmer</i> firmware 01.01.40, which itself needs controller CTR.R.03.03.44 — and an upload from
        /// Visual never applies dimmer firmware. So the user cannot fix it from the application at all, which is
        /// what makes it worth reporting. Crucially the entry declares NO <c>FixedIn</c>: the narrowing context
        /// compares a CONTROLLER version, and a controller at CTR.R.03.03.44 still has an unpatched dimmer, so
        /// narrowing on that release would withhold a finding that still holds. Both versions live in the Danish
        /// sentence instead, per D21.</para>
        ///
        /// <para><b>The corpus supplies both halves of the partition</b>, which is unusual and worth naming:
        /// <c>project3-KompleksWired</c>'s dimmer has NO scene member rows under either channel and is not
        /// reported, while <c>project5-Dokumentation</c> and <c>Project6-Errors</c> each have one and are. The
        /// negative control is an authentic vendor file rather than a built tree.</para>
        /// </summary>
        [Test]
        public void AnAffectedDimmerDrivenFromASceneIsReportedAndAnUnusedOneIsNot()
        {
            Project driven = ScenarioDrivenDimmer(memberRows: 1);

            Assert.Multiple(() =>
            {
                Assert.That(Count(driven, "rs485-dimmer-scenario-recall"), Is.EqualTo(1));
                Assert.That(Count(ScenarioDrivenDimmer(memberRows: 0), "rs485-dimmer-scenario-recall"), Is.Zero,
                    "a dimmer no scene drives is not driven through scenario recall");
                Assert.That(Count(ScenarioDrivenDimmer(memberRows: 2), "rs485-dimmer-scenario-recall"),
                    Is.EqualTo(1),
                    "OnePerOccurrence is per DIMMER: two scene rows on one device are one device to re-flash");
                Assert.That(
                    Count(ScenarioDrivenDimmer(memberRows: 1, identifier: "_0x4408"), "rs485-dimmer-scenario-recall"),
                    Is.Zero, "and a different product is not the subject");
                Assert.That(Validate(driven).Findings
                    .Single(f => f.RuleId == "rs485-dimmer-scenario-recall").Severity,
                    Is.EqualTo(ValidationSeverity.Warning));

                // D21: the sentence carries both versions, because no declared bound can express them.
                Assert.That(Message(driven, "rs485-dimmer-scenario-recall"),
                    Does.Contain("01.01.40").And.Contain("CTR.R.03.03.44"));
                Assert.That(
                    ProblemCatalog.Current.TryGet(new ProblemCode("rs485-dimmer-scenario-recall"), out var entry),
                    Is.True);
                Assert.That(entry!.FirmwareBound?.FixedIn, Is.Null,
                    "a controller at CTR.R.03.03.44 still has an unpatched dimmer, so no target may withhold it");
            });
        }

        /// <summary>
        /// A17: one scene commanding SEVERAL affected RS-485 LED dimmers off at once. Only one may respond, and
        /// the quick successive channel commands cross-talk.
        ///
        /// <para><b>"Off" is decided from the value, not from a word</b>, and that is the exclusion worth
        /// stating. A <c>scene_dimmer</c> row carries a <c>dimming_value</c>, never an on/off token, so off means
        /// the value is zero — the same reading <c>scene-all-off</c> uses. Zero is also the legal floor
        /// <c>scene-dimming-out-of-range</c> accepts, so a row at zero is a perfectly valid row: this predicate
        /// is about how MANY valid rows fire together, not about any one of them being wrong.</para>
        ///
        /// <para><b>Several means two DIMMERS, not two rows.</b> A dimmer has two channels and each can hold its
        /// own member row, so counting rows would report a single device commanded off on both channels — which
        /// is one device responding, exactly the case that works.</para>
        /// </summary>
        [Test]
        public void ASceneCommandingTwoAffectedDimmersOffIsReportedAndOneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(SceneOverDimmers(dimmers: 2, off: true), "rs485-dimmer-scene-multi-off"),
                    Is.EqualTo(1));
                Assert.That(Count(SceneOverDimmers(dimmers: 1, off: true), "rs485-dimmer-scene-multi-off"),
                    Is.Zero, "one dimmer responds, which is the case that works");
                Assert.That(Count(SceneOverDimmers(dimmers: 2, off: false), "rs485-dimmer-scene-multi-off"),
                    Is.Zero, "two dimmers commanded to a LEVEL are not commanded off");
                Assert.That(
                    Count(SceneOverDimmers(dimmers: 1, off: true, bothChannels: true),
                        "rs485-dimmer-scene-multi-off"),
                    Is.Zero,
                    "two rows on ONE dimmer are one device: the count is over dimmers, not over member rows");
                Assert.That(Count(SceneOverDimmers(dimmers: 3, off: true), "rs485-dimmer-scene-multi-off"),
                    Is.EqualTo(1), "OnePerOccurrence is per SCENE, however many dimmers it commands");
                Assert.That(Message(SceneOverDimmers(dimmers: 2, off: true), "rs485-dimmer-scene-multi-off"),
                    Does.Contain("2"), "the count is what tells the reader the scale of the problem");
                Assert.That(Validate(SceneOverDimmers(dimmers: 2, off: true)).Findings
                    .Single(f => f.RuleId == "rs485-dimmer-scene-multi-off").Severity,
                    Is.EqualTo(ValidationSeverity.Warning));
            });
        }

        /// <summary>
        /// One scene whose halves drive <paramref name="dimmers"/> affected LED dimmers, each to zero when
        /// <paramref name="off"/> and to a level otherwise.
        /// </summary>
        /// <param name="dimmers">How many distinct dimmers the scene commands.</param>
        /// <param name="off">Whether the member rows carry a zero dimming value.</param>
        /// <param name="bothChannels">Whether ONE dimmer contributes two member rows instead of one.</param>
        private static Project SceneOverDimmers(int dimmers, bool off, bool bothChannels = false)
        {
            ImmutableArray<ProjectElement>.Builder products = ImmutableArray.CreateBuilder<ProjectElement>();
            ImmutableArray<ProjectElement>.Builder halves = ImmutableArray.CreateBuilder<ProjectElement>();
            string value = off ? "0" : "60";

            for (int d = 0; d < dimmers; d++)
            {
                int at = 0x100 + (d * 0x40);
                int rows = bothChannels && d == 0 ? 2 : 1;
                ImmutableArray<ProjectElement>.Builder channels = ImmutableArray.CreateBuilder<ProjectElement>();

                for (int c = 0; c < 2; c++)
                {
                    int cat = at + (c * 0x10);
                    bool carries = c < rows;
                    channels.Add(Tree.Node("rs485_led_dimmer_channel", Token("rs485_led_dimmer_channel", cat),
                        [("name", "Kanal")],
                        Tree.Node("rs485_led_dimmer_output", Token("rs485_led_dimmer_output", cat + 1),
                            [("name", "Udgang")]),
                        Tree.Node("scenes", Token("scenes", cat + 2),
                            [("name", "Scenarier/regulering"),
                             ("scene_resource", Token("rs485_led_dimmer_output", cat + 1))],
                            carries
                                ? [Tree.Node("scene_dimmer", Token("scene_dimmer", cat + 3),
                                    [("name", "Scenarie link"), ("link", Token("scene_link", cat + 4)),
                                     ("dimming_value", value)])]
                                : [])));

                    if (carries)
                    {
                        halves.Add(Tree.Node("scene_link", Token("scene_link", cat + 4),
                            [("name", "Scenarie link"), ("link", Token("scene_dimmer", cat + 3))]));
                    }
                }

                products.Add(Tree.Node("product_rs485_led_dimmer", Token("product_rs485_led_dimmer", at + 0x30),
                    [("product_identifier", "_0x4409"), ("name", "LED dæmper " + d)], [.. channels]));
            }

            ProjectElement block = Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                    Tree.Node("resource_scene", Token("resource_scene", 0x74), [("name", "Scenarie Sluk")],
                        [.. halves])));

            return Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")], [block, .. products])));
        }

        /// <summary>
        /// An RS-485 LED dimmer with two channels, the first of which carries a <c>scenes</c> container holding
        /// <paramref name="memberRows"/> member rows — the shape the authentic files carry.
        /// </summary>
        private static Project ScenarioDrivenDimmer(int memberRows, string identifier = "_0x4409")
        {
            ProjectElement Channel(int at, bool driven) =>
                Tree.Node("rs485_led_dimmer_channel", Token("rs485_led_dimmer_channel", at), [("name", "Kanal")],
                    Tree.Node("rs485_led_dimmer_output", Token("rs485_led_dimmer_output", at + 1),
                        [("name", "Udgang")]),
                    Tree.Node("scenes", Token("scenes", at + 2),
                        [("name", "Scenarier/regulering"),
                         ("scene_resource", Token("rs485_led_dimmer_output", at + 1))],
                        [.. Enumerable.Range(0, driven ? memberRows : 0).Select(i =>
                            Tree.Node("scene_dimmer", Token("scene_dimmer", at + 3 + i),
                                [("name", "Scenarie link"), ("link", Token("scene_link", 0x200 + i)),
                                 ("dimming_value", "50")]))]));

            return Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("product_rs485_led_dimmer", Token("product_rs485_led_dimmer", 0x50),
                            [("product_identifier", identifier), ("name", "LED dæmper")],
                            Channel(0x60, driven: true),
                            Channel(0x80, driven: false)))));
        }

        /// <summary>
        /// A project with one block carrying one scene pin, and one product per member row. Every switch is one
        /// partition of one predicate: how many members, whether they share an output, whether the container binds
        /// one at all, whether a program fires the scene, and the values the rows carry.
        /// </summary>
        private static Project Scene(
            int members,
            bool sameOutput = false,
            bool boundOutput = true,
            string? boundToken = null,
            bool firedByProgram = false,
            bool firedByCondition = false,
            string relayValue = "on",
            string? secondRelayValue = null,
            long? dimmerRampMs = null,
            string? dimmingValue = null)
        {
            ImmutableArray<ProjectElement>.Builder products = ImmutableArray.CreateBuilder<ProjectElement>();
            ImmutableArray<ProjectElement>.Builder halves = ImmutableArray.CreateBuilder<ProjectElement>();

            for (int i = 0; i < members; i++)
            {
                int at = 0x100 + (i * 0x10);
                int outputAt = sameOutput ? 0x100 : at;   // one shared output, or one per member
                bool dimmer = dimmerRampMs is not null || dimmingValue is not null;
                string memberTag = dimmer ? "scene_dimmer" : "scene_relay";
                (string, string)[] valueAttributes;
                if (dimmer)
                {
                    // A dimmer row with no ramp is legal and is the shape the range row needs: ramptime_ms is
                    // written only when a ramp was asked for, so scene-long-delay stays out of the way.
                    List<(string, string)> attributes =
                    [
                        ("name", "Scenarie link"),
                        ("link", Token("scene_link", at + 1)),
                        ("dimming_value", dimmingValue ?? "50"),
                    ];
                    if (dimmerRampMs is { } ramp)
                    {
                        attributes.Add(
                            ("ramptime_ms", ramp.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    }

                    valueAttributes = [.. attributes];
                }
                else
                {
                    valueAttributes =
                    [
                        ("name", "Scenarie link"),
                        ("link", Token("scene_link", at + 1)),
                        ("relay_value", i == 1 && secondRelayValue is not null ? secondRelayValue : relayValue),
                    ];
                }

                ProjectElement row = Tree.Node(memberTag, Token(memberTag, at + 2), valueAttributes);
                ProjectElement output = Tree.Node("dataline_output", Token("dataline_output", outputAt),
                    [("name", "Udgang " + i)]);

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
