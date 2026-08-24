using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T056 — the five PROGRAM-SHAPE rows.
    ///
    /// <para><b>The claim this suite exists for</b> is that only <c>program_simple</c> has events. All 746
    /// <c>program_sub</c> elements in the corpus carry <c>conditions</c> and <c>actions</c> and no <c>events</c>
    /// container at all, so an events rule walking every <c>program_*</c> element would report 746 of them in every
    /// authentic file. <see cref="ASubProgramIsNeverReportedForHavingNoEvents"/> is that claim, asserted from both
    /// sides in one tree.</para>
    ///
    /// <para><b>And the two "empty program" rows must not overlap:</b> the empty default program every inserted
    /// block ships is <c>logic-program-no-events</c>'s finding alone, because <c>logic-program-no-actions</c>
    /// requires events to be present — the row's own wording.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProgramShapeRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static ProjectValidationFinding Single(Project project, string ruleId) =>
            Validate(project).Findings.Single(f => f.RuleId == ruleId);

        // ── logic-program-no-events and logic-program-no-actions ────────────────────────────────────

        [Test]
        public void AProgramWithNoEventsIsReportedAndOneWithEventsIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Simple(events: 0, actions: 1), "logic-program-no-events"), Is.EqualTo(1));
                Assert.That(Single(Simple(events: 0, actions: 1), "logic-program-no-events").Message,
                    Is.EqualTo("Programmet 'Program' har ingen hændelser."));
                Assert.That(Count(Simple(events: 1, actions: 1), "logic-program-no-events"), Is.Zero);
            });
        }

        [Test]
        public void AProgramWithEventsButNoCommandsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Simple(events: 1, actions: 0), "logic-program-no-actions"), Is.EqualTo(1));
                Assert.That(Single(Simple(events: 1, actions: 0), "logic-program-no-actions").Message,
                    Is.EqualTo("Programmet 'Program' har hændelser, men ingen kommandoer."));
                Assert.That(Count(Simple(events: 1, actions: 1), "logic-program-no-actions"), Is.Zero);
            });
        }

        /// <summary>
        /// The two rows partition the broken cases instead of both firing: the empty default program every inserted
        /// block ships has neither events nor commands, and only the events row names it.
        /// </summary>
        [Test]
        public void TheEmptyDefaultProgramIsOneFindingAndNotTwo()
        {
            Project empty = Simple(events: 0, actions: 0);

            Assert.Multiple(() =>
            {
                Assert.That(Count(empty, "logic-program-no-events"), Is.EqualTo(1));
                Assert.That(Count(empty, "logic-program-no-actions"), Is.Zero,
                    "the row says 'declares events but no commands', and this one declares no events");
            });
        }

        /// <summary>
        /// The grammar fact, from both sides in one tree: the sub-program has no events container and must stay
        /// silent, while the simple program beside it — which does have one, empty — is reported.
        /// </summary>
        [Test]
        public void ASubProgramIsNeverReportedForHavingNoEvents()
        {
            Project mixed = Block([
                Program("Program", events: 0, actions: 1),
                SubProgram("Under program", conditions: 1),
            ]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(mixed, "logic-program-no-events"), Is.EqualTo(1),
                    "the simple program only — a sub-program is a conditional BRANCH, not a program missing its "
                    + "trigger, and 746 of the corpus's sub-programs carry no events container at all");
                Assert.That(Count(mixed, "logic-program-no-actions"), Is.Zero);
            });
        }

        [Test]
        public void TheAuthenticCorpusReportsOnlyLeftoverPrograms()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "logic-program-no-events"), Is.Zero,
                    "a project whose blocks are all library blocks with real programs");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "logic-program-no-events"), Is.EqualTo(3),
                    "one per empty Tom blok, each carrying its untouched default program");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "logic-program-no-actions"), Is.Zero,
                    "and no authentic project has a program that starts and does nothing");
            });
        }

        // ── logic-subprogram-no-conditions ──────────────────────────────────────────────────────────

        [Test]
        public void ASubProgramWithNoConditionsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block([SubProgram("Under program", conditions: 0)]),
                    "logic-subprogram-no-conditions"), Is.EqualTo(1));
                Assert.That(Single(Block([SubProgram("Under program", conditions: 0)]),
                    "logic-subprogram-no-conditions").Message,
                    Is.EqualTo("Underprogrammet 'Under program' har ingen betingelser."));
                Assert.That(Count(Block([SubProgram("Under program", conditions: 1)]),
                    "logic-subprogram-no-conditions"), Is.Zero);
            });
        }

        // ── logic-case-no-branches ──────────────────────────────────────────────────────────────────

        [Test]
        public void ACaseNodeWithNoBranchesIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block([CaseNode([])]), "logic-case-no-branches"), Is.EqualTo(1));
                Assert.That(Single(Block([CaseNode([])]), "logic-case-no-branches").Message,
                    Is.EqualTo("Case-noden 'Case' har ingen case-værdier."));
                Assert.That(Count(Block([CaseNode(["_0x11"])]), "logic-case-no-branches"), Is.Zero);
            });
        }

        /// <summary>
        /// Branches are counted wherever the format puts them: the corpus stores <c>case_action</c> both directly
        /// under the case node and inside its <c>actions</c> container, so a predicate reading one container would
        /// report half the switches in the corpus as empty.
        /// </summary>
        [Test]
        public void ABranchInsideTheActionsContainerCountsToo()
        {
            Assert.That(Count(Block([CaseNode(["_0x11"], branchesInsideActions: true)]),
                "logic-case-no-branches"), Is.Zero);
        }

        // ── logic-case-duplicate-value ──────────────────────────────────────────────────────────────

        [Test]
        public void TwoBranchesTestingOneValueAreAnError()
        {
            Project duplicate = Block([CaseNode(["_0x11", "_0x11"])]);
            Project distinct = Block([CaseNode(["_0x11", "_0x12"])]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(duplicate, "logic-case-duplicate-value"), Is.EqualTo(1),
                    "ONE fault at two sites: the second branch is the location, the first a related one");
                Assert.That(Single(duplicate, "logic-case-duplicate-value").Severity,
                    Is.EqualTo(ValidationSeverity.Error), "the catalogue rates this one an Error");
                Assert.That(Single(duplicate, "logic-case-duplicate-value").Message,
                    Is.EqualTo("Case-noden 'Case' tester den samme værdi i to grene."));
                Assert.That(Count(distinct, "logic-case-duplicate-value"), Is.Zero);
            });
        }

        [Test]
        public void ABranchTestingNothingIsNotACollision()
        {
            Assert.That(Count(Block([CaseNode([null, null])]), "logic-case-duplicate-value"), Is.Zero,
                "a branch with no value tests nothing, so two of them are not the same test");
        }

        [Test]
        public void NoAuthenticProjectCarriesADuplicateCaseValue()
        {
            Assert.Multiple(() =>
            {
                foreach (string file in new[]
                    { "project5-Dokumentation.vis", "project2-CustomBlock-case.vis", "Project6-Errors.vis" })
                {
                    Assert.That(Count(Authentic(file), "logic-case-duplicate-value"), Is.Zero, file);
                }
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(TestSetup.Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        /// <summary>A block whose <c>programs</c> container holds the given programs.</summary>
        private static Project Block(ProjectElement[] programs) =>
            Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Trappelys")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                                Tree.Node("resource_input", Token("resource_input", 0x80),
                                    [("name", "Indgang"), ("note", "N")])),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")],
                                Tree.Node("resource_output", Token("resource_output", 0x81), [("name", "Udgang")])),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")]),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")], programs)))));

        /// <summary>A simple program carrying the given number of events and commands.</summary>
        private static ProjectElement Program(string name, int events, int actions, int at = 0x90) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", name)],
                Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                    [.. Enumerable.Range(0, events).Select(i => Tree.Node("event", Token("event", at + 2 + i),
                        [("name", "%P -> ON"), ("link1", Token("resource_input", 0x80)), ("method", "_0xa")]))]),
                Tree.Node("actions", Token("actions", at + 5), [("name", "Kommandoer"), ("type", "_0x2")],
                    [.. Enumerable.Range(0, actions).Select(i => Tree.Node("action", Token("action", at + 6 + i),
                        [("name", "%P = ON"), ("link1", Token("resource_output", 0x81)), ("method", "_0xa")]))]));

        /// <summary>A sub-program — the format gives it conditions and commands, never events.</summary>
        private static ProjectElement SubProgram(string name, int conditions, int at = 0xa0) =>
            Tree.Node("program_sub", Token("program_sub", at), [("name", name)],
                Tree.Node("conditions", Token("conditions", at + 1), [("name", "Betingelser")],
                    [.. Enumerable.Range(0, conditions).Select(i => Tree.Node("condition",
                        Token("condition", at + 2 + i),
                        [("name", "%P = ON"), ("link1", Token("resource_input", 0x80)), ("method", "_0xa")]))]),
                Tree.Node("actions", Token("actions", at + 5), [("name", "Kommandoer"), ("type", "_0x2")],
                    Tree.Node("action", Token("action", at + 6),
                        [("name", "%P = ON"), ("link1", Token("resource_output", 0x81)), ("method", "_0xa")])));

        /// <summary>
        /// A case node whose branches test the given values — one per entry, a null entry testing nothing. The
        /// corpus stores branches both ways, so <paramref name="branchesInsideActions"/> selects which.
        /// </summary>
        private static ProjectElement CaseNode(
            string?[] values, bool branchesInsideActions = false, int at = 0xc0)
        {
            ImmutableArray<ProjectElement> branches =
            [
                .. values.Select((v, i) => Tree.Node("case_action", Token("case_action", at + 2 + i),
                    v is null ? [("name", "Case")] : [("name", "Case"), ("value", v)])),
            ];

            return Tree.Node("program_case", Token("program_case", at), [("name", "Case")],
                branchesInsideActions
                    ? [Tree.Node("actions", Token("actions", at + 1), [("name", "Kommandoer"), ("type", "_0x2")],
                        [.. branches])]
                    : [.. branches]);
        }

        /// <summary>A block holding one simple program with the given event and command counts.</summary>
        private static Project Simple(int events, int actions) =>
            Block([Program("Program", events, actions)]);
    }
}
