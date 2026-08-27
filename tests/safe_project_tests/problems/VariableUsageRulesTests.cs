using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T057's SECOND half: the variable-usage rows, as predicates over the shared read model.
    ///
    /// <para><b>The claim this suite carries</b> is the subject boundary: a block's PINS are its interface and its
    /// <c>settings</c>/<c>internalsettings</c> are its state. Measured, including pins puts <c>project3</c>'s 28
    /// read-only inputs and 19 write-only outputs on these rows, behaving exactly as pins do and all of them
    /// already owned by the wiring rows. <see cref="APinIsNeverReportedByTheUsageRows"/> is that boundary, asserted
    /// with a reporting state variable in the same tree so the rules are demonstrably running.</para>
    ///
    /// <para><b>And a SETTING is dialog-configured</b>, so the read-only row is one container tighter than its
    /// siblings — <see cref="ASettingIsNeverReportedAsReadOnly"/>.</para>
    /// </summary>
    [TestFixture]
    public sealed class VariableUsageRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        // ── logic-variable-write-only and logic-variable-read-only ──────────────────────────────────

        [Test]
        public void AVariableThatIsOnlyAssignedIsReportedWriteOnly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(Written()), "logic-variable-write-only"), Is.EqualTo(1));
                Assert.That(Message(Block(Written()), "logic-variable-write-only"),
                    Is.EqualTo("Variablen 'Flag' i 'Blok' tilskrives, men læses aldrig."));
                Assert.That(Count(Block(WrittenAndRead()), "logic-variable-write-only"), Is.Zero);
                Assert.That(Count(Block(Written(linked: true)), "logic-variable-write-only"), Is.Zero,
                    "a link is a reader — the value leaves the block through it");
            });
        }

        [Test]
        public void AVariableThatIsOnlyReadIsReportedReadOnly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(Read()), "logic-variable-read-only"), Is.EqualTo(1));
                Assert.That(Message(Block(Read()), "logic-variable-read-only"),
                    Is.EqualTo("Variablen 'Flag' i 'Blok' læses, men tilskrives aldrig."));
                Assert.That(Count(Block(WrittenAndRead()), "logic-variable-read-only"), Is.Zero);
                Assert.That(Count(Block(Triggering()), "logic-variable-read-only"), Is.EqualTo(1),
                    "a trigger sees the value too, so a variable only triggered on is read-only");
            });
        }

        [Test]
        public void ASelfModifyingCommandKeepsAVariableOffBothRows()
        {
            Project counter = Block(SelfModified());

            Assert.Multiple(() =>
            {
                Assert.That(Count(counter, "logic-variable-write-only"), Is.Zero,
                    "%P = %P + 1 reads the counter as well as writing it");
                Assert.That(Count(counter, "logic-variable-read-only"), Is.Zero, "and writes it as well as reading");
            });
        }

        /// <summary>
        /// The subject boundary. One program assigns an input pin, an output pin AND one internal variable, and
        /// nothing reads any of the three — so a rule that took pins for its subject would name all three. It
        /// names one, which proves both that pins are skipped and that the rules ran.
        /// </summary>
        [Test]
        public void APinIsNeverReportedByTheUsageRows()
        {
            Project project = BlockWithPins();

            Assert.Multiple(() =>
            {
                Assert.That(Count(project, "logic-variable-write-only"), Is.EqualTo(1),
                    "the internal variable, and neither of the two pins beside it");
                Assert.That(Count(project, "logic-variable-read-only"), Is.Zero);
            });
        }

        /// <summary>
        /// A settings variable is configured from the dialog and is supposed to keep its configured value, so the
        /// read-only row must not name it — while the same variable one container over, in
        /// <c>internalsettings</c>, is exactly what the row is for.
        /// </summary>
        [Test]
        public void ASettingIsNeverReportedAsReadOnly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(Read(), container: "settings"), "logic-variable-read-only"), Is.Zero,
                    "reporting a dialog-set value for 'the logic always sees its initial value' would report the "
                    + "whole point of a setting");
                Assert.That(Count(Block(Read()), "logic-variable-read-only"), Is.EqualTo(1),
                    "and the scoping is what makes that silence a decision rather than the rule failing to run");
            });
        }

        [Test]
        public void TheAuthenticCorpusReportsNoReadOnlyInternalVariable()
        {
            Assert.That(Count(Authentic("project3-KompleksWired.vis"), "logic-variable-read-only"), Is.Zero,
                "none of its 36 read-only candidates was an internal variable");
        }

        // ── logic-holiday-schedule-firmware ─────────────────────────────────────────────────────────

        /// <summary>
        /// A29: the v3 holiday (<i>helligdag</i>) schedule did not work AT ALL below controller firmware 3.3.21.
        ///
        /// <para><b>This row is the phase's first end-to-end proof of the narrowing half</b> against a real
        /// declaration rather than the infrastructure's stand-in entry. The three cases below are the whole
        /// contract: present reports, absent is silent, and a target at or past the fix withholds.</para>
        ///
        /// <para><b>The bound is a VENDOR CLAIM, and the grade says so.</b> LK states the release fixed it; this
        /// repository has not verified that. <c>ThresholdConfidence.VendorRecommendation</c> is exactly what
        /// <c>DeclaredFirmwareBound</c>'s own doc-comment reserves for that, and it is why the row still reports
        /// with no target: an unverified claim narrows a stated target, it does not decide the default.</para>
        ///
        /// <para><b>ONE finding per project, not one per schedule.</b> The reader's decision is a firmware
        /// upgrade for the installation, which four holiday resources do not make four of.</para>
        /// </summary>
        [Test]
        public void TheHolidayScheduleIsReportedOnceAndWithheldAtTheFixedFirmware()
        {
            Project withHoliday = HolidaySchedule(count: 1);

            Assert.Multiple(() =>
            {
                Assert.That(Count(withHoliday, "logic-holiday-schedule-firmware"), Is.EqualTo(1));
                Assert.That(Count(HolidaySchedule(count: 4), "logic-holiday-schedule-firmware"), Is.EqualTo(1),
                    "OneFinding: the decision is one firmware upgrade, not one per schedule");
                Assert.That(Count(HolidaySchedule(count: 0), "logic-holiday-schedule-firmware"), Is.Zero,
                    "a project that does not use the schedule is not affected by it");
                Assert.That(Validate(withHoliday).Findings
                    .Single(f => f.RuleId == "logic-holiday-schedule-firmware").Severity,
                    Is.EqualTo(ValidationSeverity.Warning));
            });
        }

        [Test]
        public void TheHolidayRowNarrowsOnTheDeclaredFirmwareTarget()
        {
            // Through the REGISTERED rule set, not a stand-in: this is the row's end-to-end narrowing proof.
            ImmutableArray<string> Run(ValidationProfile profile) =>
                [.. new WholeProjectValidator(ProjectRules.Registered)
                    .Validate(HolidaySchedule(count: 1), profile)
                    .Select(f => f.Code.Value).Where(id => id == "logic-holiday-schedule-firmware")];

            Assert.Multiple(() =>
            {
                Assert.That(Run(ValidationProfile.ProjectOnly), Is.Not.Empty,
                    "no target declared, so the row reports — narrowing context ENABLES nothing, it withholds");
                Assert.That(
                    Run(ValidationProfile.ProjectOnly with { Firmware = new ControllerFirmwareVersion(3, 3, 20) }),
                    Is.Not.Empty, "one release below the fix");
                Assert.That(
                    Run(ValidationProfile.ProjectOnly with { Firmware = new ControllerFirmwareVersion(3, 3, 21) }),
                    Is.Empty, "the bound is inclusive: 3.3.21 itself carries the fix");
                Assert.That(
                    Run(ValidationProfile.ProjectOnly with { Firmware = new ControllerFirmwareVersion(3, 4, 0) }),
                    Is.Empty, "and anything past it");
            });
        }

        /// <summary>A project carrying <paramref name="count"/> holiday schedules.</summary>
        private static Project HolidaySchedule(int count) =>
            Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        [.. Enumerable.Range(0, count).Select(i =>
                            Tree.Node("resource_holiday", Token("resource_holiday", 0x80 + i),
                                [("name", $"Helligdag {i + 1}")]))])));

        // ── logic-case-value-foreign ────────────────────────────────────────────────────────────────

        [Test]
        public void ABranchTestingAValueOutsideItsSwitchTypeIsReported()
        {
            Project foreignValue = CaseProject(foreign: true);
            Project ownValue = CaseProject(foreign: false);

            Assert.Multiple(() =>
            {
                Assert.That(Count(foreignValue, "logic-case-value-foreign"), Is.EqualTo(1));
                Assert.That(Message(foreignValue, "logic-case-value-foreign"),
                    Is.EqualTo("Case-grenen 'Case' tester en værdi, der ikke findes i 'Tilstand'."));
                Assert.That(Count(ownValue, "logic-case-value-foreign"), Is.Zero,
                    "the chain resolves branch -> operand -> inivalue, and that value IS one of the type's");
            });
        }

        [Test]
        public void NoAuthenticProjectCarriesAForeignCaseValue()
        {
            Assert.Multiple(() =>
            {
                foreach (string file in new[]
                    { "project5-Dokumentation.vis", "project2-CustomBlock-case.vis", "Project6-Errors.vis" })
                {
                    Assert.That(Count(Authentic(file), "logic-case-value-foreign"), Is.Zero, file);
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

        /// <summary>What the one state variable under test is touched by, and how.</summary>
        private sealed record Touch(bool Linked, string RowTag, string Template, bool AsTarget);

        private static Touch Written(bool linked = false) => new(linked, "action", "%P = ON", true);

        private static Touch Read() => new(false, "condition", "%P = ON", true);

        private static Touch Triggering() => new(false, "event", "%P -> ON", true);

        private static Touch SelfModified() => new(false, "action", "%P = %P + 1", true);

        private static Touch WrittenAndRead() => new(false, "both", "%P = ON", true);

        /// <summary>
        /// A block whose <paramref name="container"/> holds one flag touched as <paramref name="touch"/> says, with
        /// <paramref name="programs"/> programs (the extra ones touch nothing).
        /// </summary>
        private static Project Block(Touch touch, string container = "internalsettings", int programs = 1)
        {
            ProjectElement variable = touch.Linked
                ? Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "Flag")],
                    Tree.Node("link_from_resource", Token("link_from_resource", 0x88),
                        [("name", "Link"), ("link", Token("link_to_resource", 0x89))]))
                : Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "Flag")]);

            ImmutableArray<ProjectElement> rows = touch.RowTag switch
            {
                "both" => [Row("action", "%P = ON", 0x95), Row("condition", "%P = ON", 0x97)],
                _ => [Row(touch.RowTag, touch.Template, 0x95)],
            };

            return Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")]),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")],
                                container == "settings" ? [variable] : []),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                                container == "internalsettings" ? [variable] : []),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                                [
                                    Program(0x90, [.. rows]),
                                    .. Enumerable.Range(1, programs - 1)
                                        .Select(i => Program(0xa0 + (i * 0x10), [])),
                                ])))));
        }

        /// <summary>One program row naming a variable — the flag at 0x80 unless another is asked for.</summary>
        private static ProjectElement Row(
            string tag, string template, int at, string targetTag = "resource_flag", int targetAt = 0x80) =>
            Tree.Node(tag, Token(tag, at),
                [("name", template), ("link1", Token(targetTag, targetAt)), ("method", "_0xa")]);

        /// <summary>A program holding the given rows, each in the container its tag belongs to.</summary>
        private static ProjectElement Program(int at, ProjectElement[] rows) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", "Program")],
                Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                    [.. rows.Where(r => r.Tag == "event")]),
                Tree.Node("conditions", Token("conditions", at + 2), [("name", "Betingelser")],
                    [.. rows.Where(r => r.Tag == "condition")]),
                Tree.Node("actions", Token("actions", at + 3), [("name", "Kommandoer"), ("type", "_0x2")],
                    [.. rows.Where(r => r.Tag == "action")]));

        /// <summary>
        /// A block whose one program assigns an input pin, an output pin and one internal variable, and reads
        /// none of them: three variables in the same state, only one of them the usage rows' subject.
        /// </summary>
        private static Project BlockWithPins() =>
            Tree.WithRoot(
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                                Tree.Node("resource_input", Token("resource_input", 0x60),
                                    [("name", "Indgang"), ("note", "N")])),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")],
                                Tree.Node("resource_output", Token("resource_output", 0x61), [("name", "Udgang")])),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                                Tree.Node("resource_flag", Token("resource_flag", 0x80), [("name", "Flag")])),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                                Program(0x90,
                                    [
                                        Row("action", "%P = ON", 0x95, "resource_input", 0x60),
                                        Row("action", "%P = ON", 0x96, "resource_output", 0x61),
                                        Row("action", "%P = ON", 0x97),
                                    ]))))));

        /// <summary>
        /// A case branch whose operand tests either one of the switch type's own values or — when
        /// <paramref name="foreign"/> — a value belonging to a second type.
        /// </summary>
        private static Project CaseProject(bool foreign)
        {
            string tested = foreign ? Token("enum_value", 0x51) : Token("enum_value", 0x41);

            return Tree.WithRoot(
                Tree.Node("enum_definitions", Token("enum_definitions", 0x30), [("name", "Enum typer")],
                    Tree.Node("enum_definition", Token("enum_definition", 0x40), [("name", "Tilstand")],
                        Tree.Node("enum_value", Token("enum_value", 0x41), [("name", "Oppe")]),
                        Tree.Node("enum_value", Token("enum_value", 0x42), [("name", "Nede"), ("index", "1")])),
                    Tree.Node("enum_definition", Token("enum_definition", 0x50), [("name", "Retning")],
                        Tree.Node("enum_value", Token("enum_value", 0x51), [("name", "Frem")]))),
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")]),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                                Tree.Node("resource_enum", Token("resource_enum", 0x80),
                                    [("name", "Tilstand"), ("typedef", Token("enum_definition", 0x40)),
                                     ("inivalue", Token("enum_value", 0x41))])),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                                Tree.Node("program_case", Token("program_case", 0xc0), [("name", "Case")],
                                    Tree.Node("case_action", Token("case_action", 0xc1),
                                        [
                                            ("name", "Case"), ("variable", Token("resource_enum", 0x80)),
                                            ("value", Token("resource_enum", 0x82)),
                                        ],
                                        Tree.Node("resource_enum", Token("resource_enum", 0x82),
                                            [("typedef", Token("enum_definition", 0x40)),
                                             ("inivalue", tested)]))))))));
        }
    }
}
