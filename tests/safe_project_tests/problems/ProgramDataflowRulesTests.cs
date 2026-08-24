using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T058 — the six remaining LOGIC rows, all predicates over T057's shared read model.
    ///
    /// <para><b>The claim this suite exists for</b> is the one that took two wrong readings to find:
    /// <c>logic-contending-writers</c>' notion of "unrelated triggers" is a DATAFLOW question. A library block's
    /// standard shape is one program setting an output ON and another setting it OFF, each from its own pulse flag,
    /// with both pulse flags written by programs triggered by the SAME button. Comparing trigger variables reports
    /// that on every library block; comparing transitive ancestries does not.
    /// <see cref="TheStandardOnOffBlockShapeIsNotAContention"/> builds exactly that shape and requires silence, and
    /// <see cref="TwoUnrelatedSourcesAreAContention"/> requires the fault next to it.</para>
    ///
    /// <para><b>And a sub-program is not a program here:</b> two branches of one program are mutually exclusive,
    /// which is why writes are attributed to the top-level program.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProgramDataflowRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        // ── logic-output-never-assigned ─────────────────────────────────────────────────────────────

        [Test]
        public void ALinkedOutputNoProgramAssignsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Output(linked: true, assigned: false), "logic-output-never-assigned"),
                    Is.EqualTo(1));
                Assert.That(Message(Output(linked: true, assigned: false), "logic-output-never-assigned"),
                    Is.EqualTo("Udgangen 'Udgang' tilskrives ikke af noget program."));
                Assert.That(Count(Output(linked: true, assigned: true), "logic-output-never-assigned"), Is.Zero);
                Assert.That(Count(Output(linked: false, assigned: false), "logic-output-never-assigned"), Is.Zero,
                    "an unlinked output is link-fb-output-unused's finding: nothing consumes it");
            });
        }

        // ── logic-flag-never-cleared ────────────────────────────────────────────────────────────────

        [Test]
        public void AFlagOnlyEverSetIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Flag("_0xa", "%P = ON"), "logic-flag-never-cleared"), Is.EqualTo(1));
                Assert.That(Message(Flag("_0xa", "%P = ON"), "logic-flag-never-cleared"),
                    Is.EqualTo("Flaget 'Flag' sættes, men nulstilles aldrig."));
                Assert.That(Count(Flag("_0xa", "%P = ON", "_0x14", "%P = OFF"), "logic-flag-never-cleared"), Is.Zero,
                    "a clear command anywhere clears the latch");
                Assert.That(Count(Flag("_0x23", "Kip %P"), "logic-flag-never-cleared"), Is.Zero,
                    "a toggle clears half the time, so it is not 'cleared by none'");
                Assert.That(Count(Flag(null, null), "logic-flag-never-cleared"), Is.Zero,
                    "a flag no program writes is logic-variable-unused's finding, not a latch");
            });
        }

        // ── logic-counter-never-reset ───────────────────────────────────────────────────────────────

        [Test]
        public void ACounterOnlyEverSteppedIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Counter("_0x54", "%P = %P + 1"), "logic-counter-never-reset"), Is.EqualTo(1));
                Assert.That(Message(Counter("_0x54", "%P = %P + 1"), "logic-counter-never-reset"),
                    Is.EqualTo("Tælleren 'Tæller' tælles op, men nulstilles aldrig."));
                Assert.That(Count(Counter("_0x54", "%P = %P + 1", "_0xa", "%P = 0"), "logic-counter-never-reset"),
                    Is.Zero, "a plain assignment is a reset");
                Assert.That(Count(Counter("_0x57", "%P = %P - 1"), "logic-counter-never-reset"), Is.EqualTo(1),
                    "a decrement-only counter never returns to a known state either");
            });
        }

        // ── logic-timer-unused ──────────────────────────────────────────────────────────────────────

        [Test]
        public void ATimerNoProgramStartsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Timer(null, null), "logic-timer-unused"), Is.EqualTo(1));
                Assert.That(Message(Timer(null, null), "logic-timer-unused"),
                    Is.EqualTo("Timeren 'Timer' startes ikke af noget program."));
                Assert.That(Count(Timer("_0xbe", "Aktiver nedtælling på %P med initial værdi"),
                    "logic-timer-unused"), Is.Zero, "the activation command starts it");
                Assert.That(Count(Timer("_0xc8", "Aktiver optælling på %P"), "logic-timer-unused"), Is.Zero);
            });
        }

        [Test]
        public void AssigningATimerIsNotStartingIt()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Timer("_0xa", "%P = 0"), "logic-timer-unused"), Is.EqualTo(1),
                    "setting a timer to zero does not start it");
                Assert.That(Count(Timer("_0x19", "%P = Initialværdi"), "logic-timer-unused"), Is.EqualTo(1));
                Assert.That(Count(Timer("_0xdc", "Stands tælling på %P"), "logic-timer-unused"), Is.EqualTo(1),
                    "and stopping it certainly does not");
            });
        }

        // ── logic-self-trigger ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AProgramTriggeredByWhatItAssignsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(SelfTrigger(sameVariable: true), "logic-self-trigger"), Is.EqualTo(1));
                Assert.That(Message(SelfTrigger(sameVariable: true), "logic-self-trigger"),
                    Is.EqualTo("Programmet 'Program' udløses af 'Flag', som det selv tilskriver."));
                Assert.That(Count(SelfTrigger(sameVariable: false), "logic-self-trigger"), Is.Zero);
            });
        }

        [Test]
        public void ASubProgramAssigningItsParentsTriggerIsTheSameLoop()
        {
            Assert.That(Count(SelfTriggerThroughSubProgram(), "logic-self-trigger"), Is.EqualTo(1),
                "the parent is what starts again, so the write counts as the parent's");
        }

        // ── logic-contending-writers ────────────────────────────────────────────────────────────────

        /// <summary>
        /// The shape every library block has: two programs driving one output from two pulse flags, both flags
        /// written by programs triggered by the SAME button. Comparing trigger variables calls this a contention;
        /// comparing ancestries does not.
        /// </summary>
        [Test]
        public void TheStandardOnOffBlockShapeIsNotAContention()
        {
            Assert.That(Count(OnOffBlock(sharedButton: true), "logic-contending-writers"), Is.Zero,
                "the two triggers descend from one button, so the order is the user's, not the event queue's");
        }

        [Test]
        public void TwoUnrelatedSourcesAreAContention()
        {
            Project contended = OnOffBlock(sharedButton: false);

            Assert.Multiple(() =>
            {
                Assert.That(Count(contended, "logic-contending-writers"), Is.EqualTo(1),
                    "a manual button and an independent sensor, the row's own example");
                Assert.That(Message(contended, "logic-contending-writers"),
                    Is.EqualTo("Variablen 'Udgang' tilskrives af 2 programmer med uafhængige udløsere."));
            });
        }

        [Test]
        public void TwoProgramsIssuingTheSameCommandDoNotContend()
        {
            Assert.That(Count(OnOffBlock(sharedButton: false, sameCommand: true), "logic-contending-writers"),
                Is.Zero,
                "both set the output ON, so which one runs first cannot change the outcome");
        }

        [Test]
        public void AProgramWithNoTriggerCannotContend()
        {
            Assert.That(Count(OnOffBlock(sharedButton: false, secondProgramUntriggered: true),
                "logic-contending-writers"), Is.Zero,
                "a program that never starts is logic-program-no-events' finding, not a contender");
        }

        [Test]
        public void TheAuthenticCorpusReportsOnlyRealContentions()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "logic-contending-writers"), Is.EqualTo(2),
                    "two, not the nine a trigger-variable comparison finds on this two-block project");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "logic-contending-writers"),
                    Is.EqualTo(4), "four, not twenty-four");
                Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "logic-flag-never-cleared"), Is.Zero,
                    "and no authentic project latches a flag");
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

        private static ProjectElement Locality(params ProjectElement[] contents) =>
            Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], contents));

        /// <summary>A block shell whose four containers hold the given members, plus the given programs.</summary>
        private static Project BlockOf(
            ProjectElement[] inputs, ProjectElement[] outputs, ProjectElement[] internals,
            ProjectElement[] programs) =>
            Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")], inputs),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")], outputs),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")], internals),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")], programs))));

        /// <summary>One program: the given events and commands, each already built.</summary>
        private static ProjectElement Program(
            int at, string name, ProjectElement[] events, ProjectElement[] actions,
            ProjectElement[]? subPrograms = null) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", name)],
                [
                    Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")], events),
                    Tree.Node("actions", Token("actions", at + 2), [("name", "Kommandoer"), ("type", "_0x2")],
                        actions),
                    .. subPrograms ?? [],
                ]);

        private static ProjectElement Event(int at, string variable, int counter) =>
            Tree.Node("event", Token("event", at),
                [("name", "%P -> ON"), ("link1", Token(variable, counter)), ("method", "_0xa")]);

        private static ProjectElement Action(int at, string variable, int counter, string method, string template) =>
            Tree.Node("action", Token("action", at),
                [("name", template), ("link1", Token(variable, counter)), ("method", method)]);

        /// <summary>An output pin, optionally linked and optionally assigned by a program.</summary>
        private static Project Output(bool linked, bool assigned)
        {
            ProjectElement pin = linked
                ? Tree.Node("resource_output", Token("resource_output", 0x81), [("name", "Udgang")],
                    Tree.Node("link_from_resource", Token("link_from_resource", 0x88),
                        [("name", "Link"), ("link", Token("link_to_resource", 0x89))]))
                : Tree.Node("resource_output", Token("resource_output", 0x81), [("name", "Udgang")]);

            ProjectElement[] actions = assigned
                ? [Action(0x96, "resource_output", 0x81, "_0xa", "%P = ON")]
                : [];

            return BlockOf(
                [Tree.Node("resource_input", Token("resource_input", 0x60), [("name", "Indgang"), ("note", "N")])],
                [pin],
                [],
                [Program(0x90, "Program", [Event(0x95, "resource_input", 0x60)], actions)]);
        }

        /// <summary>A flag written by the given command(s); a null method writes nothing.</summary>
        private static Project Flag(string? method, string? template, string? second = null, string? secondTemplate = null)
        {
            ImmutableArray<ProjectElement> actions =
            [
                .. method is null ? [] : new[] { Action(0x96, "resource_flag", 0x80, method, template!) },
                .. second is null ? [] : new[] { Action(0x97, "resource_flag", 0x80, second, secondTemplate!) },
            ];

            return BlockOf(
                [Tree.Node("resource_input", Token("resource_input", 0x60), [("name", "Indgang"), ("note", "N")])],
                [],
                [Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "Flag")])],
                [Program(0x90, "Program", [Event(0x95, "resource_input", 0x60)], [.. actions])]);
        }

        /// <summary>A counter written by the given command(s).</summary>
        private static Project Counter(string method, string template, string? second = null, string? secondTemplate = null)
        {
            ImmutableArray<ProjectElement> actions =
            [
                Action(0x96, "resource_counter", 0x80, method, template),
                .. second is null ? [] : new[] { Action(0x97, "resource_counter", 0x80, second, secondTemplate!) },
            ];

            return BlockOf(
                [Tree.Node("resource_input", Token("resource_input", 0x60), [("name", "Indgang"), ("note", "N")])],
                [],
                [Tree.Node("resource_counter", Token("resource_counter", 0x80), [("name", "Tæller")])],
                [Program(0x90, "Program", [Event(0x95, "resource_input", 0x60)], [.. actions])]);
        }

        /// <summary>A timer written by the given command, or by nothing at all.</summary>
        private static Project Timer(string? method, string? template) =>
            BlockOf(
                [Tree.Node("resource_input", Token("resource_input", 0x60), [("name", "Indgang"), ("note", "N")])],
                [],
                [Tree.Node("resource_timer", Token("resource_timer", 0x80), [("name", "Timer")])],
                [
                    Program(0x90, "Program", [Event(0x95, "resource_input", 0x60)],
                        method is null ? [] : [Action(0x96, "resource_timer", 0x80, method, template!)]),
                ]);

        /// <summary>A program triggered by the flag it assigns, or by an unrelated input.</summary>
        private static Project SelfTrigger(bool sameVariable) =>
            BlockOf(
                [Tree.Node("resource_input", Token("resource_input", 0x60), [("name", "Indgang"), ("note", "N")])],
                [],
                [Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "Flag")])],
                [
                    Program(0x90, "Program",
                        [sameVariable
                            ? Event(0x95, "resource_flag", 0x80)
                            : Event(0x95, "resource_input", 0x60)],
                        [Action(0x96, "resource_flag", 0x80, "_0xa", "%P = ON")]),
                ]);

        /// <summary>The same loop, with the write inside a sub-program of the triggered program.</summary>
        private static Project SelfTriggerThroughSubProgram() =>
            BlockOf(
                [],
                [],
                [Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "Flag")])],
                [
                    Program(0x90, "Program", [Event(0x95, "resource_flag", 0x80)], [],
                        [
                            Tree.Node("program_sub", Token("program_sub", 0xa0), [("name", "Under program")],
                                Tree.Node("conditions", Token("conditions", 0xa1), [("name", "Betingelser")],
                                    Tree.Node("condition", Token("condition", 0xa2),
                                        [("name", "%P = ON"), ("link1", Token("resource_flag", 0x80)),
                                         ("method", "_0xa")])),
                                Tree.Node("actions", Token("actions", 0xa3),
                                    [("name", "Kommandoer"), ("type", "_0x2")],
                                    Action(0xa4, "resource_flag", 0x80, "_0xa", "%P = ON"))),
                        ]),
                ]);

        /// <summary>
        /// The ON/OFF shape: two programs driving one output from two pulse flags. With
        /// <paramref name="sharedButton"/> both pulse flags are written by programs triggered by the same input —
        /// the library-block shape; without it the second pulse comes from an independent sensor.
        /// </summary>
        private static Project OnOffBlock(
            bool sharedButton, bool sameCommand = false, bool secondProgramUntriggered = false)
        {
            ProjectElement button = Tree.Node("resource_input", Token("resource_input", 0x60),
                [("name", "Tryk"), ("note", "N")]);
            ProjectElement sensor = Tree.Node("resource_input", Token("resource_input", 0x61),
                [("name", "Sensor"), ("note", "N")]);
            ProjectElement onPulse = Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "ON puls")]);
            ProjectElement offPulse = Tree.Node("resource_flag", Token("resource_flag", 0x81), [("name", "OFF puls")]);
            ProjectElement output = Tree.Node("resource_output", Token("resource_output", 0x82), [("name", "Udgang")]);

            return BlockOf(
                [button, sensor],
                [output],
                [onPulse, offPulse],
                [
                    // the two pulse producers: both from the button, or one from the sensor
                    Program(0x90, "Puls ON", [Event(0x95, "resource_input", 0x60)],
                        [Action(0x96, "resource_flag", 0x80, "_0xa", "%P = ON")]),
                    Program(0xb0, "Puls OFF",
                        [Event(0xb5, "resource_input", sharedButton ? 0x60 : 0x61)],
                        [Action(0xb6, "resource_flag", 0x81, "_0xa", "%P = ON")]),

                    // the two writers of the output
                    Program(0xc0, "Tænd", [Event(0xc5, "resource_flag", 0x80)],
                        [Action(0xc6, "resource_output", 0x82, "_0xa", "%P = ON")]),
                    Program(0xd0, "Sluk",
                        secondProgramUntriggered ? [] : [Event(0xd5, "resource_flag", 0x81)],
                        [
                            sameCommand
                                ? Action(0xd6, "resource_output", 0x82, "_0xa", "%P = ON")
                                : Action(0xd6, "resource_output", 0x82, "_0x14", "%P = OFF"),
                        ]),
                ]);
        }
    }
}
