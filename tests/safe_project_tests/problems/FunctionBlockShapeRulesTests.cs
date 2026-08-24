using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T055 — four of the five FUNCTION-BLOCK SHAPE rows, and the fifth's absence.
    ///
    /// <para><b>The fifth row landed later, under D27</b>, and its tests are here beside its siblings:
    /// <c>logic-block-locked-content</c> needs the block's LIBRARY body to tell an edited value from a library
    /// default, so it declares that context and is skipped without it — which is the first thing
    /// <see cref="TheLockedContentRowIsSkippedWithoutALibrary"/> proves.</para>
    ///
    /// <para><b>The duplicate-program signature is tested from both edges</b> — a re-labelled copy still counts,
    /// and a copy with a different operand does not — because a signature that is too loose reports every pair of
    /// similar programs and one that is too tight reports nothing.</para>
    /// </summary>
    [TestFixture]
    public sealed class FunctionBlockShapeRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        /// <summary>A run under an explicit library — null means "a caller that has none".</summary>
        private static int Count(Project project, string ruleId, ILibraryBlockSource? library) =>
            ProjectVerification.Run(project, ValidationProfile.Categorized with { Library = library })
                .Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId, ILibraryBlockSource? library) =>
            ProjectVerification.Run(project, ValidationProfile.Categorized with { Library = library })
                .Findings.First(f => f.RuleId == ruleId).Message;

        /// <summary>A library holding one 1.1.01/e block whose named timer stores the given minute.</summary>
        private static ILibraryBlockSource Library(string minute, string named = "Timer") =>
            new StubLibrary(Tree.Node("functionblock", null, [("name", "1.1.01.e. Kip tænd sluk")],
                Tree.Node("inputs", null, [("name", "Input")]),
                Tree.Node("outputs", null, [("name", "Output")]),
                Tree.Node("settings", null, [("name", "Indstillinger")],
                    Tree.Node("resource_timer", null,
                        [("name", named), ("hour", "0"), ("minute", minute), ("second", "0")])),
                Tree.Node("internalsettings", null, [("name", "Interne")]),
                Tree.Node("programs", null, [("name", "Programmer")])));

        private sealed class StubLibrary(ProjectElement body) : ILibraryBlockSource
        {
            public bool TryGetBody(string masterType, string masterVersion, out ProjectElement found)
            {
                found = body;
                return masterType == "1.1.01" && masterVersion == "e";
            }
        }

        /// <summary>
        /// A library block carrying full master identity whose <c>Timer</c> setting stores the given minute — null
        /// stores nothing at all.
        /// </summary>
        private static Project LockedLibraryBlock(string? storedMinutes, bool locked = true)
        {
            (string, string)[] timer = storedMinutes is null
                ? [("name", "Timer")]
                : [("name", "Timer"), ("hour", "0"), ("minute", storedMinutes), ("second", "0")];
            (string, string)[] identity = locked
                ? [("name", "1.1.01.e. Kip tænd sluk"), ("master_type", "1.1.01"), ("master_version", "e"),
                   ("master_name", "Kip tænd sluk"), ("locked", "yes")]
                : [("name", "1.1.01.e. Kip tænd sluk"), ("master_type", "1.1.01"), ("master_version", "e"),
                   ("master_name", "Kip tænd sluk")];

            return Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70), identity,
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                        Tree.Node("resource_input", Token("resource_input", 0x80),
                            [("name", "Indgang"), ("note", "N")])),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")],
                        Tree.Node("resource_output", Token("resource_output", 0x88), [("name", "Udgang")])),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")],
                        Tree.Node("resource_timer", Token("resource_timer", 0x90), timer)),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")]),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                        Program(0x9a, "Program", 0x80)))));
        }

        // ── logic-block-empty ───────────────────────────────────────────────────────────────────────

        [Test]
        public void ABlockWithNoProgramsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(programs: 0), "logic-block-empty"), Is.EqualTo(1));
                Assert.That(Message(Block(programs: 0), "logic-block-empty"),
                    Is.EqualTo("Blokken 'Trappelys' har ingen programmer."));
                Assert.That(Count(Block(programs: 1), "logic-block-empty"), Is.Zero,
                    "every inserted block ships one default program, so one is the ordinary state");
            });
        }

        [Test]
        public void NoAuthenticProjectReportsAProgramlessBlock()
        {
            Assert.Multiple(() =>
            {
                foreach (string file in new[]
                    { "Project1-SimpelWired.vis", "project3-KompleksWired.vis", "project5-Dokumentation.vis" })
                {
                    Assert.That(Count(Authentic(file), "logic-block-empty"), Is.Zero, file);
                }
            });
        }

        // ── logic-block-no-pins ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ABlockWithNeitherInputsNorOutputsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(programs: 1, inputs: 0, outputs: 0), "logic-block-no-pins"), Is.EqualTo(1));
                Assert.That(Message(Block(programs: 1, inputs: 0, outputs: 0), "logic-block-no-pins"),
                    Is.EqualTo("Blokken 'Trappelys' har hverken ind- eller udgange."));
                Assert.That(Count(Block(programs: 1, inputs: 1, outputs: 0), "logic-block-no-pins"), Is.Zero,
                    "an input alone is a way in");
                Assert.That(Count(Block(programs: 1, inputs: 0, outputs: 1), "logic-block-no-pins"), Is.Zero,
                    "and an output alone is a way out — the row needs BOTH to be empty");
            });
        }

        // ── logic-duplicate-program ─────────────────────────────────────────────────────────────────

        [Test]
        public void TwoStructurallyIdenticalProgramsAreOneFinding()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Programs(("A", 0x80), ("B", 0x80)), "logic-duplicate-program"), Is.EqualTo(1),
                    "same operand, different label: still a copy");
                Assert.That(Message(Programs(("A", 0x80), ("B", 0x80)), "logic-duplicate-program"),
                    Is.EqualTo("Blokken 'Trappelys' har to identiske programmer."));
                Assert.That(Count(Programs(("A", 0x80), ("B", 0x81)), "logic-duplicate-program"), Is.Zero,
                    "a different operand is a different program, however similar it looks");
                Assert.That(Count(Programs(("A", 0x80)), "logic-duplicate-program"), Is.Zero);
            });
        }

        [Test]
        public void ThreeCopiesOfOneProgramAreTwoFindings()
        {
            Assert.That(Count(Programs(("A", 0x80), ("B", 0x80), ("C", 0x80)), "logic-duplicate-program"),
                Is.EqualTo(2),
                "each later copy is its own redundancy, and each one is separately deletable");
        }

        [Test]
        public void IdenticalProgramsInTwoDifferentBlocksAreNotDuplicates()
        {
            Project twoBlocks = Tree.WithRoot(Locality(
                BlockShell(0x70, "Blok A", 1, 1, [Program(0x90, "P", 0x80)]),
                BlockShell(0xa0, "Blok B", 1, 1, [Program(0xc0, "P", 0x80)])));

            Assert.That(Count(twoBlocks, "logic-duplicate-program"), Is.Zero,
                "the row is about two programs in the SAME block; two blocks may do the same thing");
        }

        // ── logic-master-block-modified ─────────────────────────────────────────────────────────────

        [Test]
        public void ALibraryBlockRenamedAwayFromItsInsertNameIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(LibraryBlock("Kip tænd sluk (lokalt tilpasset)"), "logic-master-block-modified"),
                    Is.EqualTo(1), "the error fixture's own witness, in miniature");
                Assert.That(Message(LibraryBlock("Kip tænd sluk (lokalt tilpasset)"), "logic-master-block-modified"),
                    Is.EqualTo("Blokken 'Kip tænd sluk (lokalt tilpasset)' er ændret lokalt i forhold til "
                        + "biblioteksblokken 'Kip tænd sluk'."));
                Assert.That(Count(LibraryBlock("1.1.01.e. Kip tænd sluk"), "logic-master-block-modified"), Is.Zero,
                    "a block still at its insert name is name-default's finding, not this one");
            });
        }

        [Test]
        public void AVersionlessLibraryBlockRenamedAwayFromItsInsertNameIsReported()
        {
            Project renamed = Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [
                        ("name", "Driftstimer, garage"), ("master_type", "4.1.04"),
                        ("master_name", "Driftstimetæller"), ("locked", "yes"),
                    ],
                    [.. Sections(0x70, 1, 1, [Program(0x90, "Program", 0x80)])])));
            Project untouched = Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [
                        ("name", "4.1.04. Driftstimetæller"), ("master_type", "4.1.04"),
                        ("master_name", "Driftstimetæller"), ("locked", "yes"),
                    ],
                    [.. Sections(0x70, 1, 1, [Program(0x90, "Program", 0x80)])])));

            Assert.Multiple(() =>
            {
                Assert.That(Count(renamed, "logic-master-block-modified"), Is.EqualTo(1),
                    "the versionless library form, which T055b taught the shared reader");
                Assert.That(Count(untouched, "logic-master-block-modified"), Is.Zero);
                Assert.That(Count(untouched, "name-default"), Is.EqualTo(1),
                    "and the partition holds for the versionless form as well");
            });
        }

        [Test]
        public void ABlockTheUserSavedToTheLibraryIsNeverReported()
        {
            Project saved = Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [("name", "GemOracle"), ("master_name", "GemOracle"), ("locked", "yes")],
                    [.. Sections(0x70, 1, 1, [])])));

            Assert.That(Count(saved, "logic-master-block-modified"), Is.Zero,
                "it keeps master_name but gets no master_type, so no insert name exists to differ from — and such "
                + "a block IS its own library entry");
        }

        /// <summary>
        /// The border between this row and <c>name-default</c>, asserted rather than left to a reader's memory:
        /// between them, every reconstructible library block draws exactly one advisory.
        /// </summary>
        [Test]
        public void TheTwoLibraryBlockRowsPartitionTheSamePopulation()
        {
            Project untouched = LibraryBlock("1.1.01.e. Kip tænd sluk");
            Project renamed = LibraryBlock("Kip tænd sluk (lokalt tilpasset)");

            Assert.Multiple(() =>
            {
                Assert.That(Count(untouched, "name-default"), Is.EqualTo(1));
                Assert.That(Count(untouched, "logic-master-block-modified"), Is.Zero);
                Assert.That(Count(renamed, "name-default"), Is.Zero);
                Assert.That(Count(renamed, "logic-master-block-modified"), Is.EqualTo(1));
            });
        }

        // ── logic-block-locked-content (D27) ────────────────────────────────────────────────────────

        /// <summary>
        /// The declared-context half, and the reason the row could not exist before D27: with no library supplied
        /// the rule is not evaluated at all — not evaluated against a guessed default, which is what would make the
        /// same project valid on one workstation and invalid on another.
        /// </summary>
        [Test]
        public void TheLockedContentRowIsSkippedWithoutALibrary()
        {
            ProblemCode code = new("logic-block-locked-content");
            Project edited = LockedLibraryBlock(storedMinutes: "5");

            Assert.Multiple(() =>
            {
                Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True);
                Assert.That(entry.RequiresLibrary, Is.True, "the row DECLARES the context it needs");
                Assert.That(ValidationProfile.Categorized.Includes(entry), Is.False,
                    "so a profile carrying no library does not evaluate it");
                Assert.That(Count(edited, "logic-block-locked-content", library: null), Is.Zero);
                Assert.That(Count(edited, "logic-block-locked-content", library: Library("3")), Is.EqualTo(1),
                    "and the same project reports once a library IS supplied");
            });
        }

        [Test]
        public void AValueChangedUnderALockIsReportedAgainstTheLibrarys()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(LockedLibraryBlock("5"), "logic-block-locked-content", Library("3")),
                    Is.EqualTo(1), "the error fixture's own witness: a timer moved from 3 to 5 minutes under lock");
                Assert.That(Message(LockedLibraryBlock("5"), "logic-block-locked-content", Library("3")),
                    Is.EqualTo("Den låste blok '1.1.01.e. Kip tænd sluk' har ændret 'Timer'."),
                    "the sentence names the block and the variable; a timer's four-part value would be machine "
                    + "text in Danish prose, so the comparison detail stays in the English diagnostic");
                Assert.That(Count(LockedLibraryBlock("3"), "logic-block-locked-content", Library("3")), Is.Zero,
                    "a value equal to the library's is not an edit");
            });
        }

        [Test]
        public void AnUnlockedBlockIsNotThisRowsBusiness()
        {
            Assert.That(Count(LockedLibraryBlock("5", locked: false), "logic-block-locked-content", Library("3")),
                Is.Zero,
                "an unlocked block may be edited freely; a block edited away from its library is "
                + "logic-master-block-modified's finding when its NAME moved");
        }

        [Test]
        public void ATimersValueLivesInItsTimePartsNotInAValueAttribute()
        {
            Assert.Multiple(() =>
            {
                // The reading that cost a wrong first attempt: a resource_timer stores no value/inivalue at all.
                Assert.That(Count(LockedLibraryBlock("5"), "logic-block-locked-content", Library("3")),
                    Is.EqualTo(1), "the minute part IS the value");
                Assert.That(Count(LockedLibraryBlock(null), "logic-block-locked-content", Library("3")), Is.Zero,
                    "a variable storing nothing is at its default and cannot have been edited");
            });
        }

        [Test]
        public void AVariableTheLibraryDoesNotHaveIsNotAnEditedValue()
        {
            Assert.That(Count(LockedLibraryBlock("5"), "logic-block-locked-content", Library("3", named: "Andet")),
                Is.Zero,
                "pairing is by NAME; a variable the library has no counterpart for is a structural difference and "
                + "stays logic-master-block-modified's finding");
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

        private static ImmutableArray<ProjectElement> Sections(
            int at, int inputs, int outputs, ProjectElement[] programs) =>
            [
                Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")],
                    [.. Enumerable.Range(0, inputs).Select(i => Tree.Node("resource_input",
                        Token("resource_input", 0x80 + i), [("name", $"Indgang {i}"), ("note", "N")]))]),
                Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")],
                    [.. Enumerable.Range(0, outputs).Select(i => Tree.Node("resource_output",
                        Token("resource_output", 0x88 + i), [("name", $"Udgang {i}")]))]),
                Tree.Node("settings", Token("settings", at + 3), [("name", "Indstillinger")]),
                Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "Interne")]),
                Tree.Node("programs", Token("programs", at + 5), [("name", "Programmer")], programs),
            ];

        private static ProjectElement BlockShell(
            int at, string name, int inputs, int outputs, ProjectElement[] programs) =>
            Tree.Node("functionblock", Token("functionblock", at), [("name", name)],
                [.. Sections(at, inputs, outputs, programs)]);

        /// <summary>One program whose event and action both name the operand at <paramref name="operandAt"/>.</summary>
        private static ProjectElement Program(int at, string name, int operandAt) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", name)],
                Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                    Tree.Node("event", Token("event", at + 2),
                        [("name", "%P -> ON"), ("link1", Token("resource_input", operandAt)), ("method", "_0xa")])),
                Tree.Node("actions", Token("actions", at + 3), [("name", "Kommandoer"), ("type", "_0x2")],
                    Tree.Node("action", Token("action", at + 4),
                        [("name", "%P = ON"), ("link1", Token("resource_output", operandAt)), ("method", "_0xa")])));

        /// <summary>A block with the given number of programs, inputs and outputs.</summary>
        private static Project Block(int programs, int inputs = 1, int outputs = 1) =>
            Tree.WithRoot(Locality(BlockShell(0x70, "Trappelys", inputs, outputs,
                [.. Enumerable.Range(0, programs).Select(i => Program(0x90 + (i * 0x10), $"Program {i}", 0x80))])));

        /// <summary>A block whose programs are named and operand-bound as given — the duplicate-signature cases.</summary>
        private static Project Programs(params (string Name, int OperandAt)[] programs) =>
            Tree.WithRoot(Locality(BlockShell(0x70, "Trappelys", 1, 1,
                [.. programs.Select((p, i) => Program(0x90 + (i * 0x10), p.Name, p.OperandAt))])));

        /// <summary>A library block carrying full master identity, under the given name.</summary>
        private static Project LibraryBlock(string name) =>
            Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [
                        ("name", name), ("master_type", "1.1.01"), ("master_version", "e"),
                        ("master_name", "Kip tænd sluk"), ("locked", "yes"),
                    ],
                    [.. Sections(0x70, 1, 1, [Program(0x90, "Program", 0x80)])])));
    }
}
