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
    /// <para><b>And neither "empty program" row names the shipped empty default:</b> a block inserted from the
    /// library brings a program with no trigger and no command, and reporting it says only that the author has not
    /// finished. <c>logic-program-no-events</c> asks for a program that HAS work, and <c>logic-program-no-actions</c>
    /// requires events to be present — so the untouched default is deliberately nobody's finding here. A block that
    /// is empty ALL THE WAY DOWN is still <c>logic-block-empty</c>'s.</para>
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
        public void AProgramWithCommandsButNoEventsIsReportedAndOneWithEventsIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Simple(events: 0, actions: 1), "logic-program-no-events"), Is.EqualTo(1));
                Assert.That(Single(Simple(events: 0, actions: 1), "logic-program-no-events").Message,
                    Is.EqualTo("Programmet 'Program' har kommandoer, men ingen hændelser."));
                Assert.That(Count(Simple(events: 1, actions: 1), "logic-program-no-events"), Is.Zero);
            });
        }

        /// <summary>
        /// A sub-program is work too. The commands may all sit inside a conditional branch, and a program built that
        /// way is as stranded as one whose commands sit at the top level — so the exclusion asks for work of either
        /// kind, not for a non-empty <c>actions</c> container.
        /// </summary>
        [Test]
        public void AProgramWhoseOnlyWorkIsASubProgramIsReportedToo()
        {
            Project branchOnly = Block([Program("Program", events: 0, actions: 0,
                subPrograms: [SubProgram("Under program", conditions: 1)])]);

            Assert.That(Count(branchOnly, "logic-program-no-events"), Is.EqualTo(1),
                "the branch holds the commands, and nothing can ever reach it");
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
        /// THE SHIPPED EMPTY DEFAULT IS NOBODY'S FINDING, and that is a decision rather than a gap. Every block
        /// inserted from the library brings a program with neither trigger nor command; saying "it never starts" of
        /// one is saying the author has not finished, which they can see. Both rows therefore stay silent — the
        /// events row because the program has no work to strand, the commands row because it declares no events.
        /// </summary>
        [Test]
        public void TheEmptyDefaultProgramIsNeitherRowsFinding()
        {
            Project empty = Simple(events: 0, actions: 0);

            Assert.Multiple(() =>
            {
                Assert.That(Count(empty, "logic-program-no-events"), Is.Zero,
                    "the row asks for a program that HAS work and cannot be reached");
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
        public void TheAuthenticCorpusReportsOnlyProgramsThatCarryWork()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "logic-program-no-events"), Is.Zero,
                    "a project whose blocks are all library blocks with real programs");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "logic-program-no-events"), Is.Zero,
                    "its three Tom blok programs carry no commands either — the shipped default, not a stranding");
                Assert.That(Count(Authentic("Project6-Errors.vis"), "logic-program-no-events"), Is.EqualTo(1),
                    "the fixture's own designed witness: commands, and no trigger that could ever run them");
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

        // ── logic-statement-unlinked ────────────────────────────────────────────────────────────────

        /// <summary>
        /// The finding: a statement carrying no <c>link1</c> references nothing. Asserted on an
        /// <c>&lt;action&gt;</c>, which is the tag whose consequence was actually measured.
        /// </summary>
        [Test]
        public void AStatementCarryingNoLinkIsAnError()
        {
            Project unlinked = ProgramOf([Statement("event", 0x92, linked: true)], [Statement("action", 0x96, linked: false)]);
            Project linked = ProgramOf([Statement("event", 0x92, linked: true)], [Statement("action", 0x96, linked: true)]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(unlinked, "logic-statement-unlinked"), Is.EqualTo(1));
                Assert.That(Single(unlinked, "logic-statement-unlinked").Severity,
                    Is.EqualTo(ValidationSeverity.Error), "the catalogue rates this one an Error");
                Assert.That(Single(unlinked, "logic-statement-unlinked").Message,
                    Is.EqualTo("Programlinjen <action> i blokken 'Trappelys' peger ikke på nogen ressource."));
                Assert.That(Count(linked, "logic-statement-unlinked"), Is.Zero);
            });
        }

        /// <summary>
        /// THE EXCLUSION, and the whole risk of this row. <c>event_power</c> carries no <c>link1</c>,
        /// <c>link2</c> or <c>method</c> BY DESIGN — its element name is the discriminator, because its behaviour
        /// is hard-wired rather than selected by a method number.
        ///
        /// <para><b>It is not distinguishable from <c>event</c> by anything but the tag.</b> It shares
        /// <c>event</c>'s id type code <c>c8</c> and its constant <c>icon="_0xc"</c>, so a rule recognising
        /// statements by the id suffix — the shortcut the format otherwise invites, since the P data type IS read
        /// off that suffix — or by the icon fires on every Powerup event in the corpus. This tree carries the two
        /// side by side so that mistake produces a second finding here rather than an oracle diff later.</para>
        /// </summary>
        [Test]
        public void APowerUpEventIsNeverReportedForCarryingNoLink()
        {
            Project mixed = ProgramOf(
                [Statement("event", 0x92, linked: true), PowerUp(0x94)],
                [Statement("action", 0x96, linked: false)]);
            Project powerUpAlone = ProgramOf([PowerUp(0x94)], [Statement("action", 0x96, linked: true)]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(mixed, "logic-statement-unlinked"), Is.EqualTo(1),
                    "the linkless action alone — the event_power beside it carries no link1 by design");
                Assert.That(Single(mixed, "logic-statement-unlinked").Message, Does.Contain("<action>"),
                    "and the one finding names the action, not the Powerup event");
                Assert.That(Count(powerUpAlone, "logic-statement-unlinked"), Is.Zero,
                    "a program whose only linkless element is an event_power reports nothing at all");
            });
        }

        /// <summary>
        /// All three statement tags are matched, and matched BY TAG. <c>condition</c> lives in a sub-program, so
        /// the walk cannot be scoped to <c>program_simple</c> the way the events rows above are.
        /// </summary>
        [Test]
        public void EachOfTheThreeStatementTagsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    Single(ProgramOf([Statement("event", 0x92, linked: false)], [Statement("action", 0x96, linked: true)]),
                        "logic-statement-unlinked").Message, Does.Contain("<event>"));
                Assert.That(
                    Single(ProgramOf([Statement("event", 0x92, linked: true)], [Statement("action", 0x96, linked: false)]),
                        "logic-statement-unlinked").Message, Does.Contain("<action>"));
                Assert.That(
                    Single(SubProgramOf([Statement("condition", 0xa2, linked: false)]),
                        "logic-statement-unlinked").Message, Does.Contain("<condition>"));
            });
        }

        /// <summary>
        /// The measurement the row rests on: the vendor editor always writes <c>link1</c>, so no authentic file
        /// carries this state — while every one of them carries <c>event_power</c> elements that a
        /// type-code-matching rule would report. The second assertion is the one that would catch that mistake:
        /// without it, a rule reporting nothing at all would pass the first.
        /// </summary>
        [Test]
        public void NoAuthenticProjectCarriesAnUnlinkedStatementThoughAllCarryPowerUpEvents()
        {
            string[] files = ["project2-CustomBlock.vis", "project3-KompleksWired.vis", "project5-Dokumentation.vis"];

            Assert.Multiple(() =>
            {
                foreach (string file in files)
                {
                    Project project = Authentic(file);
                    Assert.That(Count(project, "logic-statement-unlinked"), Is.Zero, file);
                    Assert.That(
                        project.Root.Descendants().Count(e => e.Tag == "event_power"), Is.GreaterThan(0),
                        $"{file} carries Powerup events, so the zero above is a real exclusion and not an empty walk");
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

        /// <summary>A simple program carrying the given number of events and commands, and any branches beside them.</summary>
        private static ProjectElement Program(
            string name, int events, int actions, int at = 0x90, ProjectElement[]? subPrograms = null) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", name)],
                [
                    Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                        [.. Enumerable.Range(0, events).Select(i => Tree.Node("event", Token("event", at + 2 + i),
                            [("name", "%P -> ON"), ("link1", Token("resource_input", 0x80)), ("method", "_0xa")]))]),
                    Tree.Node("actions", Token("actions", at + 5), [("name", "Kommandoer"), ("type", "_0x2")],
                        [.. Enumerable.Range(0, actions).Select(i => Tree.Node("action", Token("action", at + 6 + i),
                            [("name", "%P = ON"), ("link1", Token("resource_output", 0x81)), ("method", "_0xa")]))]),
                    .. subPrograms ?? [],
                ]);

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

        /// <summary>
        /// One statement, linked or not. The other builders here always write a <c>link1</c>, which is exactly
        /// what the unlinked-statement row is about — so it needs a builder that can leave it out.
        /// </summary>
        /// <param name="tag">The statement tag: <c>event</c>, <c>condition</c> or <c>action</c>.</param>
        /// <param name="at">The id counter.</param>
        /// <param name="linked">Whether the statement carries a <c>link1</c> at all.</param>
        private static ProjectElement Statement(string tag, int at, bool linked) =>
            Tree.Node(tag, Token(tag, at),
                linked
                    ? [("name", "%P = ON"), ("link1", Token("resource_output", 0x81)), ("method", "_0xa")]
                    : [("name", "%P = ON"), ("method", "_0xa")]);

        /// <summary>
        /// A Powerup event, exactly as the vendor writes one: no <c>link1</c>, no <c>link2</c>, no
        /// <c>method</c> — and an id whose type code is <c>event</c>'s.
        /// </summary>
        /// <param name="at">The id counter.</param>
        private static ProjectElement PowerUp(int at) =>
            Tree.Node("event_power", Token("event_power", at), [("name", "Powerup")]);

        /// <summary>A block whose simple program carries exactly these events and these commands.</summary>
        private static Project ProgramOf(ProjectElement[] events, ProjectElement[] actions) =>
            Block(
            [
                Tree.Node("program_simple", Token("program_simple", 0x90), [("name", "Program")],
                    Tree.Node("events", Token("events", 0x91), [("name", "Hændelser")], events),
                    Tree.Node("actions", Token("actions", 0x95), [("name", "Kommandoer"), ("type", "_0x2")], actions)),
            ]);

        /// <summary>A block whose SUB-program carries exactly these conditions, and one linked command.</summary>
        private static Project SubProgramOf(ProjectElement[] conditions) =>
            Block(
            [
                Tree.Node("program_sub", Token("program_sub", 0xa0), [("name", "Betingelse")],
                    Tree.Node("conditions", Token("conditions", 0xa1), [("name", "Betingelser")], conditions),
                    Tree.Node("actions", Token("actions", 0xa5), [("name", "Kommandoer"), ("type", "_0x2")],
                        Statement("action", 0xa6, linked: true))),
            ]);
    }
}
