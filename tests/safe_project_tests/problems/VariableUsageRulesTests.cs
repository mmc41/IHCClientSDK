using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T057's SECOND half: the five variable-usage rows, as predicates over the shared read model.
    ///
    /// <para><b>The claim this suite carries</b> is the subject boundary: a block's PINS are its interface and its
    /// <c>settings</c>/<c>internalsettings</c> are its state. Measured, including pins takes <c>project3</c> from 9
    /// findings to 64 — 28 read-only inputs and 19 write-only outputs behaving exactly as pins do, all of them
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

        // ── logic-variable-unused ───────────────────────────────────────────────────────────────────

        [Test]
        public void AnUntouchedStateVariableIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(Untouched()), "logic-variable-unused"), Is.EqualTo(1));
                Assert.That(Message(Block(Untouched()), "logic-variable-unused"),
                    Is.EqualTo("Variablen 'Flag' i 'Blok' bruges ikke af noget program."));
                Assert.That(Count(Block(Written()), "logic-variable-unused"), Is.Zero, "a write is a use");
                Assert.That(Count(Block(Read()), "logic-variable-unused"), Is.Zero, "so is a read");
                Assert.That(Count(Block(Triggering()), "logic-variable-unused"), Is.Zero, "so is a trigger");
                Assert.That(Count(Block(Untouched(linked: true)), "logic-variable-unused"), Is.Zero,
                    "and so is a follow-link: the value crosses the block boundary");
            });
        }

        [Test]
        public void OneFindingPerVariableEvenWhenManyProgramsIgnoreIt()
        {
            Assert.That(Count(Block(Untouched(), programs: 3), "logic-variable-unused"), Is.EqualTo(1),
                "the catalogue's deliberate-non-findings section says this is reported once per variable, never "
                + "once per program that fails to read it");
        }

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
                Assert.That(Count(counter, "logic-variable-unused"), Is.Zero);
            });
        }

        /// <summary>
        /// The subject boundary. The tree carries an unfed input pin and an unlinked output pin — the states the
        /// wiring rows own — beside ONE untouched state variable, so the count proves both that pins are skipped
        /// and that the rules ran.
        /// </summary>
        [Test]
        public void APinIsNeverReportedByTheUsageRows()
        {
            Project project = BlockWithPins();

            Assert.Multiple(() =>
            {
                Assert.That(Count(project, "logic-variable-unused"), Is.EqualTo(1),
                    "the internal variable, and neither of the two pins beside it");
                Assert.That(Count(project, "logic-variable-write-only"), Is.Zero);
                Assert.That(Count(project, "logic-variable-read-only"), Is.Zero);
            });
        }

        /// <summary>
        /// A settings variable is configured from the dialog and is supposed to keep its configured value, so the
        /// read-only row must not name it — while the unused row still may, because a setting nothing reads is
        /// dead.
        /// </summary>
        [Test]
        public void ASettingIsNeverReportedAsReadOnly()
        {
            Project readSetting = Block(Read(), container: "settings");
            Project untouchedSetting = Block(Untouched(), container: "settings");

            Assert.Multiple(() =>
            {
                Assert.That(Count(readSetting, "logic-variable-read-only"), Is.Zero,
                    "reporting a dialog-set value for 'the logic always sees its initial value' would report the "
                    + "whole point of a setting");
                Assert.That(Count(untouchedSetting, "logic-variable-unused"), Is.EqualTo(1),
                    "but a setting no program reads at all is still a dead declaration");
            });
        }

        [Test]
        public void TheAuthenticCorpusReportsOnlyDeadDeclarations()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "logic-variable-unused"), Is.Zero,
                    "a project of library blocks whose variables its programs all use");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "logic-variable-unused"), Is.EqualTo(9),
                    "the nine internal variables of its empty blocks");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "logic-variable-read-only"), Is.Zero,
                    "and none of its 36 read-only candidates was an internal variable");
            });
        }

        // ── enum-value-unused ───────────────────────────────────────────────────────────────────────

        [Test]
        public void AnEnumValueNothingReferencesIsReported()
        {
            Project unused = EnumProject(referenceFirstValue: false);
            Project used = EnumProject(referenceFirstValue: true);

            Assert.Multiple(() =>
            {
                Assert.That(Count(unused, "enum-value-unused"), Is.EqualTo(2), "both declared values");
                Assert.That(Message(unused, "enum-value-unused"),
                    Is.EqualTo("Værdien 'Oppe' i enumerator typen 'Tilstand' bruges ikke."));
                Assert.That(Count(used, "enum-value-unused"), Is.EqualTo(1),
                    "one value is a variable's initial value, so only the other is unused");
            });
        }

        [Test]
        public void ASystemTablesValuesAreNeverReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Authentic("Project0-Tomt.vis"), "enum-value-unused"), Is.Zero,
                    "an empty project ships two system tables with 11 unreferenced values between them, and must "
                    + "stay silent about every one");
                Assert.That(Count(Authentic("Project6-Errors.vis"), "enum-value-unused"), Is.EqualTo(5),
                    "the authored types' values only");
            });
        }

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
        private sealed record Touch(bool Linked, string? RowTag, string Template, bool AsTarget);

        private static Touch Untouched(bool linked = false) => new(linked, null, string.Empty, false);

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
                null => [],
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

        /// <summary>One program row naming the flag at 0x80.</summary>
        private static ProjectElement Row(string tag, string template, int at) =>
            Tree.Node(tag, Token(tag, at),
                [("name", template), ("link1", Token("resource_flag", 0x80)), ("method", "_0xa")]);

        /// <summary>A program holding the given rows, each in the container its tag belongs to.</summary>
        private static ProjectElement Program(int at, ProjectElement[] rows) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", "Program")],
                Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                    [.. rows.Where(r => r.Tag == "event")]),
                Tree.Node("conditions", Token("conditions", at + 2), [("name", "Betingelser")],
                    [.. rows.Where(r => r.Tag == "condition")]),
                Tree.Node("actions", Token("actions", at + 3), [("name", "Kommandoer"), ("type", "_0x2")],
                    [.. rows.Where(r => r.Tag == "action")]));

        /// <summary>A block with an unfed input pin, an unlinked output pin and one untouched internal variable.</summary>
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
                                Program(0x90, []))))));

        /// <summary>An authored enum type with two values; one may be a variable's initial value.</summary>
        private static Project EnumProject(bool referenceFirstValue)
        {
            (string, string)[] variable = referenceFirstValue
                ? [("name", "Tilstand"), ("typedef", Token("enum_definition", 0x40)),
                   ("inivalue", Token("enum_value", 0x41))]
                : [("name", "Tilstand"), ("typedef", Token("enum_definition", 0x40))];

            return Tree.WithRoot(
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
                                Tree.Node("resource_enum", Token("resource_enum", 0x80), variable)),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                                Program(0x90, []))))));
        }

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
