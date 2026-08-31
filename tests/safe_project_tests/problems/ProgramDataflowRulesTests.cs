using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;
using static Ihc.Vis.Tests.RuleProbe;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T058 — the remaining LOGIC rows, all predicates over T057's shared read model.
    ///
    /// <para><b>Each row is tested at the boundary its predicate states</b>, over synthetic trees carrying exactly
    /// one condition and its neighbouring non-condition: a linked output nothing assigns against an unlinked one,
    /// a flag only ever set against one a toggle touches, a counter only ever stepped against one a plain
    /// assignment resets.</para>
    ///
    /// <para><b>And a sub-program is not a program here:</b> two branches of one program are mutually exclusive,
    /// which is why writes are attributed to the top-level program —
    /// <see cref="ASubProgramAssigningItsParentsTriggerIsTheSameLoop"/>.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProgramDataflowRulesTests
    {
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
                    "a flag no program writes at all is not a latch: the row is about a write that only ever sets");
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

        /// <summary>
        /// THE TWO KINDS WHOSE SELF-WRITE IS THE IDIOM, not the fault. A program that re-arms the timer that woke
        /// it is a delay; one that steps the counter that counts its own pulses is a tally. Both are written that
        /// way on purpose, and neither oscillates — the timer has to elapse again and the counter's step is not an
        /// edge the count can re-fire. Every witnessed hit of these two kinds in the measured corpus was one of the
        /// two idioms, so the row's sentence was false of all of them.
        ///
        /// <para>The exclusion is by ELEMENT KIND and nothing else: a flag or a variable written by the program it
        /// triggers is still the loop this row exists for, and stays reported.</para>
        /// </summary>
        [Test]
        public void ATimerOrCounterRearmingItselfIsTheIdiomAndNotTheFault()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(SelfTriggerOn("resource_timer"), "logic-self-trigger"), Is.Zero,
                    "a delay re-arms the timer that woke it");
                Assert.That(Count(SelfTriggerOn("resource_counter"), "logic-self-trigger"), Is.Zero,
                    "a tally steps the counter it counts on");
                Assert.That(Count(SelfTriggerOn("resource_flag"), "logic-self-trigger"), Is.EqualTo(1),
                    "and a flag feeding itself back is the loop the row is for");
            });
        }

        /// <summary>
        /// AND AN EXCLUDED SELF-EDGE IS NOBODY'S FINDING, deliberately. <c>logic-block-recursive</c> excludes every
        /// direct self-edge — one node writing to itself — so narrowing this row does not hand the timer and
        /// counter idioms to that one instead. Widening the recursion row to catch them would report the same
        /// deliberate pattern under a code whose consequence ("the call silently never runs") is false of it.
        /// </summary>
        [Test]
        public void AnExcludedSelfTriggerIsNotHandedToTheRecursionRow()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(SelfTriggerOn("resource_timer"), "logic-block-recursive"), Is.Zero);
                Assert.That(Count(SelfTriggerOn("resource_counter"), "logic-block-recursive"), Is.Zero);
            });
        }

        [Test]
        public void NoAuthenticProjectLatchesAFlag()
        {
            Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "logic-flag-never-cleared"), Is.Zero);
        }

        // ── logic-block-recursive ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// A block whose program path reaches ITSELF works perfectly in the simulator and does nothing at all on
        /// the controller — which is the worst shape a defect can take: it tests clean and fails silently in the
        /// field.
        ///
        /// <para><b>The cycle runs through TWO blocks here</b>, which is the shape the row is named for: block A
        /// writes a variable that triggers block B, and B writes a variable that triggers A.</para>
        /// </summary>
        [Test]
        public void ABlockThatReachesItselfThroughAnotherBlockIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(TwoBlockCycle(closed: true), "logic-block-recursive"), Is.EqualTo(2),
                    "both blocks are on the cycle, and each is separately the thing to break");
                Assert.That(Count(TwoBlockCycle(closed: false), "logic-block-recursive"), Is.Zero,
                    "a one-way chain A -> B is ordinary composition, not recursion");
                Assert.That(Validate(TwoBlockCycle(closed: true)).Findings
                    .Where(f => f.RuleId == "logic-block-recursive").Select(f => f.Severity),
                    Has.All.EqualTo(ValidationSeverity.Error),
                    "unfixed on every firmware the source knows, which is section 8.1's first row");
            });
        }

        /// <summary>
        /// THE DIRECT SELF-EDGE IS NOT REPORTED TWICE. A single program triggered by a variable it also assigns
        /// is <c>logic-self-trigger</c>'s finding, so this one excludes every direct self-edge — otherwise each
        /// self-trigger in the corpus would gain a second finding.
        ///
        /// <para>The two describe different runtime consequences: the ring <c>logic-self-trigger</c> finds RUNS
        /// and is aborted, while a block reaching itself silently never executes. Reporting one situation under
        /// both codes would make that distinction meaningless.</para>
        /// </summary>
        [Test]
        public void ADirectSelfTriggerIsTheOtherRowsFindingAndNotThisOne()
        {
            Project selfTrigger = SelfTriggerOn("resource_flag");

            Assert.Multiple(() =>
            {
                Assert.That(Count(selfTrigger, "logic-self-trigger"), Is.EqualTo(1),
                    "a flag feeding itself back is that row's finding");
                Assert.That(Count(selfTrigger, "logic-block-recursive"), Is.Zero,
                    "and the new row does not report it a second time");
            });
        }

        /// <summary>
        /// TWO PROGRAMS IN ONE BLOCK TRADING AN INTERNAL FLAG ARE NOT A RECURSIVE CALL. A31's subject is a block
        /// that reaches itself through a CALL — the path has to leave the block and come back. A block's own
        /// programs signalling each other over its internal settings never leaves it, and is how the vendor's
        /// shipped library blocks are built.
        ///
        /// <para>Measured, not reasoned: the first cut of this rule projected the program graph onto blocks
        /// AFTER cycle detection, so intra-block traffic collapsed into a block self-loop. It reported
        /// <c>1.2.04.e. Trådløs / Bus lysdæmper</c> in <c>project3-KompleksWired</c> — a
        /// <c>master_schneider_electric="yes"</c> library block, i.e. the vendor accusing its own shipped
        /// product of a defect that silently never runs. Contracting each block to ONE node before the search
        /// is what makes the distinction structural.</para>
        /// </summary>
        [Test]
        public void ProgramsSignallingEachOtherInsideOneBlockAreNotARecursiveCall()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(BlockTalkingToItself(), "logic-block-recursive"), Is.Zero,
                    "the path never leaves the block, so nothing calls the block");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "logic-block-recursive"), Is.Zero,
                    "and no authentic file's library block is accused of it");
            });
        }

        /// <summary>
        /// THE SEARCH IS BOUNDED BY THE HEAP, NOT BY THE CALL STACK. Depth-first descends once per node on the
        /// path, so a project chaining blocks deeply enough drives the search as deep as the file is long — and
        /// a blown call stack is the ONE failure a caller cannot catch, in a component whose whole contract is to
        /// report on a file rather than fall over on one.
        ///
        /// <para>The chain is closed into a ring, so the whole of it is on the cycle: that pins the ANSWER at
        /// full depth as well as the traversal, since a search that quietly gave up early would report fewer.</para>
        ///
        /// <para>THE LENGTH IS MEASURED, not picked for roundness: the recursive search this replaced survives
        /// 4 000 and dies at 10 000, taking the whole test host with it — a <see cref="StackOverflowException"/>
        /// is not catchable, so a shorter ring would be a guard that cannot fail.</para>
        /// </summary>
        [Test]
        public void ADeeplyChainedCallGraphIsTraversedWithoutRecursion()
        {
            const int length = 10000;

            Assert.That(Count(DeepBlockChain(length), "logic-block-recursive"), Is.EqualTo(length),
                "every block on the ring is reported, and the run completes");
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One block, two programs: the first writes a flag that triggers the second, which writes a flag that
        /// triggers the first. A ring wholly inside one block's internal settings.
        /// </summary>
        private static Project BlockTalkingToItself() =>
            BlockOf(
                [],
                [],
                [
                    Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "Flag A")]),
                    Tree.Node("resource_flag", Token("resource_flag", 0x81), [("name", "Flag B")]),
                ],
                [
                    Program(0x90, "Første",
                        [Event(0x95, "resource_flag", 0x80)],
                        [Action(0x96, "resource_flag", 0x81, "_0xa", "%P = ON")]),
                    Program(0x98, "Anden",
                        [Event(0x9d, "resource_flag", 0x81)],
                        [Action(0x9e, "resource_flag", 0x80, "_0xa", "%P = ON")]),
                ]);

        /// <summary>
        /// Two blocks wired into a call graph: A's program writes a flag that triggers B, and — when
        /// <paramref name="closed"/> — B's program writes a flag that triggers A, closing the cycle.
        /// </summary>
        /// <param name="closed">Whether B writes back to A's trigger, making the path recursive.</param>
        private static Project TwoBlockCycle(bool closed)
        {
            ProjectElement Block(int at, string name, int triggerFlag, int writeFlag, bool writes) =>
                Tree.Node("functionblock", Token("functionblock", at), [("name", name)],
                    Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")]),
                    Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")]),
                    Tree.Node("settings", Token("settings", at + 3), [("name", "Indstillinger")]),
                    Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "Interne")],
                        Tree.Node("resource_flag", Token("resource_flag", triggerFlag),
                            [("name", $"Flag {triggerFlag:x}")])),
                    Tree.Node("programs", Token("programs", at + 5), [("name", "Programmer")],
                        Program(at + 6, name + " program",
                            [Event(at + 9, "resource_flag", triggerFlag)],
                            writes ? [Action(at + 10, "resource_flag", writeFlag, "_0xa", "%P = ON")] : [])));

            return Tree.WithRoot(Locality(
                Block(0x70, "Blok A", 0x80, 0x81, writes: true),
                Block(0xa0, "Blok B", 0x81, 0x80, writes: closed)));
        }

        /// <summary>
        /// A RING of blocks, each triggered by its own internal flag and writing the next block's — so the call
        /// graph is one cycle whose depth-first path is as long as the ring itself.
        /// </summary>
        /// <param name="length">How many blocks the ring holds.</param>
        private static Project DeepBlockChain(int length)
        {
            int FlagOf(int index) => 0x1000 + (index * 0x20) + 0xb;

            ProjectElement Block(int index)
            {
                int at = 0x1000 + (index * 0x20);
                return Tree.Node("functionblock", Token("functionblock", at), [("name", $"Blok {index}")],
                    Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")]),
                    Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")]),
                    Tree.Node("settings", Token("settings", at + 3), [("name", "Indstillinger")]),
                    Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "Interne")],
                        Tree.Node("resource_flag", Token("resource_flag", FlagOf(index)),
                            [("name", $"Flag {index}")])),
                    Tree.Node("programs", Token("programs", at + 5), [("name", "Programmer")],
                        Program(at + 6, $"Program {index}",
                            [Event(at + 9, "resource_flag", FlagOf(index))],
                            [Action(at + 10, "resource_flag", FlagOf((index + 1) % length), "_0xa", "%P = ON")])));
            }

            return Tree.WithRoot(Locality([.. Enumerable.Range(0, length).Select(Block)]));
        }

        /// <summary>
        /// One block, one program, triggered by the very variable it assigns — the self-trigger shape, over any
        /// variable kind, which is what the row now discriminates on.
        /// </summary>
        /// <param name="tag">The element tag of the variable the program both triggers on and assigns.</param>
        private static Project SelfTriggerOn(string tag) =>
            BlockOf(
                [],
                [],
                [Tree.Node(tag, Token(tag, 0x80), [("name", "Ressource")])],
                [Program(0x90, "Program",
                    [Event(0x95, tag, 0x80)],
                    [Action(0x96, tag, 0x80, "_0xa", "%P = ON")])]);

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

    }
}
