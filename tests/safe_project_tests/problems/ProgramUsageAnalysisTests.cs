using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T057's FIRST half: the shared program read model itself, tested where the rules cannot test it — the
    /// read/write/trigger classification, one row kind at a time.
    ///
    /// <para><b>Why the model gets its own suite.</b> Eleven catalogue rows are dataflow questions over it, and a
    /// misclassified operand is invisible in a rule test: <c>logic-variable-write-only</c> would simply report one
    /// variable too many, and the count would look as plausible as the right one. Asserting the classification
    /// directly is what makes those eleven rows' answers checkable.</para>
    ///
    /// <para><b>The one judgement the model makes</b> is that a self-modifying command reads its own target, read
    /// off the vendor template the row stores (<c>%P = %P + %S</c> names it twice). That is asserted here from both
    /// sides, because keying on the method token instead would have missed the four arithmetic tokens the SDK's
    /// method catalog does not model.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProgramUsageAnalysisTests
    {
        private const string TriggerVariable = "resource_flag";

        private static IProgramUsageAnalysis Usage(Project project) =>
            new ProjectAnalyses(project).Usage;

        private static ProjectElement Find(Project project, string tag, int counter) =>
            project.Root.DescendantsAndSelf()
                .Single(e => e.Tag == tag && e.GetAttribute("id") == Token(tag, counter));

        // ── the classification, one row kind at a time ──────────────────────────────────────────────

        [Test]
        public void AnEventsFirstOperandIsATriggerAndItsSecondIsARead()
        {
            Project project = OneRow("event", "%P -> %S", link2: true);
            IProgramUsageAnalysis usage = Usage(project);

            Assert.Multiple(() =>
            {
                Assert.That(usage.IsTriggeredOn(Find(project, TriggerVariable, 0x80)), Is.True);
                Assert.That(usage.IsRead(Find(project, TriggerVariable, 0x80)), Is.False,
                    "a trigger is not a read: the program starts on the change, it does not consume the value");
                Assert.That(usage.IsRead(Find(project, TriggerVariable, 0x81)), Is.True, "the compared operand");
                Assert.That(usage.IsWritten(Find(project, TriggerVariable, 0x80)), Is.False);
            });
        }

        [Test]
        public void BothSidesOfAConditionAreReads()
        {
            Project project = OneRow("condition", "%P = %S", link2: true);
            IProgramUsageAnalysis usage = Usage(project);

            Assert.Multiple(() =>
            {
                Assert.That(usage.IsRead(Find(project, TriggerVariable, 0x80)), Is.True);
                Assert.That(usage.IsRead(Find(project, TriggerVariable, 0x81)), Is.True);
                Assert.That(usage.IsWritten(Find(project, TriggerVariable, 0x80)), Is.False);
                Assert.That(usage.IsTriggeredOn(Find(project, TriggerVariable, 0x80)), Is.False);
            });
        }

        [Test]
        public void AnActionWritesItsTargetAndReadsItsSource()
        {
            Project project = OneRow("action", "%P = %S", link2: true);
            IProgramUsageAnalysis usage = Usage(project);

            Assert.Multiple(() =>
            {
                Assert.That(usage.IsWritten(Find(project, TriggerVariable, 0x80)), Is.True);
                Assert.That(usage.IsRead(Find(project, TriggerVariable, 0x80)), Is.False,
                    "a plain assignment does not read what it overwrites");
                Assert.That(usage.IsRead(Find(project, TriggerVariable, 0x81)), Is.True);
            });
        }

        /// <summary>
        /// The one judgement the model makes, from both sides: the row's persisted template says whether the
        /// command reads its own target.
        /// </summary>
        [Test]
        public void ASelfModifyingCommandAlsoReadsItsTarget()
        {
            Project compound = OneRow("action", "%P = %P + %S", link2: true);
            Project plain = OneRow("action", "%P = %S", link2: true);

            Assert.Multiple(() =>
            {
                Assert.That(Usage(compound).IsRead(Find(compound, TriggerVariable, 0x80)), Is.True,
                    "%P = %P + %S names its target twice, which is the file saying it reads it");
                Assert.That(Usage(compound).IsWritten(Find(compound, TriggerVariable, 0x80)), Is.True,
                    "and it still writes it");
                Assert.That(Usage(plain).IsRead(Find(plain, TriggerVariable, 0x80)), Is.False);
            });
        }

        [Test]
        public void AStepCommandWithoutASecondOperandStillReadsItsTarget()
        {
            Project step = OneRow("action", "%P = %P + 1", link2: false);

            Assert.That(Usage(step).IsRead(Find(step, TriggerVariable, 0x80)), Is.True,
                "the corpus carries this template with no %S at all, and it still reads the counter");
        }

        [Test]
        public void ACaseBranchReadsItsSwitchAndItsTestResolvesThroughTheOperand()
        {
            Project project = CaseProject();
            IProgramUsageAnalysis usage = Usage(project);
            CaseTest test = usage.CaseTests.Single();

            Assert.Multiple(() =>
            {
                Assert.That(usage.IsRead(Find(project, "resource_enum", 0x80)), Is.True, "the switch variable");
                Assert.That(test.Switch, Is.Not.Null);
                Assert.That(test.Operand, Is.Not.Null, "the branch's value names an inline operand element");
                Assert.That(test.ValueToken, Is.EqualTo(Token("enum_value", 0x41)),
                    "and the value actually tested is that operand's inivalue, one hop further");
            });
        }

        [Test]
        public void EveryInivalueCountsAsAReferenceToItsValue()
        {
            Project project = CaseProject();

            Assert.That(Usage(project).ReferencedValueTokens, Does.Contain(Token("enum_value", 0x41)),
                "the ONE reference form: a variable's initial value and a case operand's tested value are stored "
                + "the same way");
        }

        [Test]
        public void ALinkedVariableIsReportedAsLinked()
        {
            Project project = OneRow("action", "%P = %S", link2: true, linkTarget: true);

            Assert.Multiple(() =>
            {
                Assert.That(Usage(project).IsLinked(Find(project, TriggerVariable, 0x80)), Is.True);
                Assert.That(Usage(project).IsLinked(Find(project, TriggerVariable, 0x81)), Is.False);
            });
        }

        [Test]
        public void UsagesAreAttributedToTheProgramTheySitIn()
        {
            Project project = OneRow("action", "%P = %S", link2: true);
            ProjectElement program = Find(project, "program_simple", 0x90);

            Assert.Multiple(() =>
            {
                Assert.That(Usage(project).Of(program), Has.Length.EqualTo(2), "the write and the read");
                Assert.That(Usage(project).Of(program).All(u => ReferenceEquals(u.Program, program)), Is.True);
                Assert.That(Usage(project).Usages, Has.Length.EqualTo(2));
            });
        }

        [Test]
        public void TheModelSeesEveryProgramRowInTheAuthenticCorpus()
        {
            IProgramUsageAnalysis usage = Usage(Authentic("project3-KompleksWired.vis"));

            Assert.Multiple(() =>
            {
                Assert.That(usage.Usages.Length, Is.GreaterThan(500),
                    "project3 carries hundreds of program rows; a model that resolved none of them would still "
                    + "make every dataflow rule 'pass' by reporting nothing");
                Assert.That(usage.Usages.Any(u => u.Kind == VariableUsageKind.Trigger), Is.True);
                Assert.That(usage.Usages.Any(u => u.Kind == VariableUsageKind.Read), Is.True);
                Assert.That(usage.Usages.Any(u => u.Kind == VariableUsageKind.Write), Is.True);
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

        /// <summary>
        /// A block with two flags and ONE program row of the given kind: <c>link1</c> names the first flag,
        /// <c>link2</c> the second when asked for.
        /// </summary>
        private static Project OneRow(string rowTag, string template, bool link2, bool linkTarget = false)
        {
            (string, string)[] attrs = link2
                ? [("name", template), ("link1", Token(TriggerVariable, 0x80)),
                   ("link2", Token(TriggerVariable, 0x81)), ("method", "_0xa")]
                : [("name", template), ("link1", Token(TriggerVariable, 0x80)), ("method", "_0xa")];

            ProjectElement row = Tree.Node(rowTag, Token(rowTag, 0x95), attrs);
            string container = rowTag switch
            {
                "event" => "events",
                "condition" => "conditions",
                _ => "actions",
            };
            (string, string)[] containerAttrs = container == "actions"
                ? [("name", "Kommandoer"), ("type", "_0x2")]
                : [("name", container)];

            ProjectElement target = linkTarget
                ? Tree.Node(TriggerVariable, Token(TriggerVariable, 0x80), [("name", "Flag A")],
                    Tree.Node("link_from_resource", Token("link_from_resource", 0x88),
                        [("name", "Link"), ("link", Token("link_to_resource", 0x89))]))
                : Tree.Node(TriggerVariable, Token(TriggerVariable, 0x80), [("name", "Flag A")]);

            return Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")]),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                                target,
                                Tree.Node(TriggerVariable, Token(TriggerVariable, 0x81), [("name", "Flag B")])),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                                Tree.Node("program_simple", Token("program_simple", 0x90), [("name", "Program")],
                                    Tree.Node(container, Token(container, 0x91), containerAttrs, row)))))));
        }

        /// <summary>An enum switch, one case branch, and the inline operand carrying the value it tests.</summary>
        private static Project CaseProject() =>
            Tree.WithRoot(
                Tree.Node("enum_definitions", Token("enum_definitions", 0x30), [("name", "Enum typer")],
                    Tree.Node("enum_definition", Token("enum_definition", 0x40), [("name", "Tilstand")],
                        Tree.Node("enum_value", Token("enum_value", 0x41), [("name", "Oppe")]),
                        Tree.Node("enum_value", Token("enum_value", 0x42), [("name", "Nede"), ("index", "1")]))),
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")]),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                                Tree.Node("resource_enum", Token("resource_enum", 0x80),
                                    [("name", "Tilstand"), ("typedef", Token("enum_definition", 0x40))])),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                                Tree.Node("program_case", Token("program_case", 0xc0), [("name", "Case")],
                                    Tree.Node("case_action", Token("case_action", 0xc1),
                                        [
                                            ("name", "Case"), ("variable", Token("resource_enum", 0x80)),
                                            ("value", Token("resource_enum", 0x82)),
                                        ],
                                        Tree.Node("resource_enum", Token("resource_enum", 0x82),
                                            [("typedef", Token("enum_definition", 0x40)),
                                             ("inivalue", Token("enum_value", 0x41))]))))))));
    }
}
