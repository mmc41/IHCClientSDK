using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T054 — the five ENUM-DEFINITION rows.
    ///
    /// <para><b>The claim this suite exists for</b> is that an ABSENT <c>index</c> is index ZERO. The canonicalizer
    /// omits a value equal to the DTD default, so every definition's first value in the corpus carries no
    /// <c>index</c> attribute at all — and a duplicate-index predicate reading the raw attribute would miss exactly
    /// the collision a hand-edited file produces. <see cref="AnAbsentIndexCollidesWithAnExplicitZero"/> is that
    /// claim.</para>
    ///
    /// <para><b>The three shape rows are tested against what the author does not own:</b> a <c>typeid</c>-bearing
    /// system table and the data-tables definition. Both are unreferenced in ordinary projects — 40 of the corpus's
    /// 109 definitions are system tables — so without the exclusions <c>enum-def-unused</c> would report furniture
    /// in nearly every file.</para>
    /// </summary>
    [TestFixture]
    public sealed class EnumDefinitionRulesTests
    {
        private const string UserTexts = "User-defined texts";

        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static ProjectValidationFinding Single(Project project, string ruleId) =>
            Validate(project).Findings.Single(f => f.RuleId == ruleId);

        // ── enum-def-duplicate-name ─────────────────────────────────────────────────────────────────

        [Test]
        public void TwoValuesWithOneNameAreOneFinding()
        {
            Project duplicate = Definition("Tilstand", ("Oppe", "0"), ("Oppe", "1"));
            Project distinct = Definition("Tilstand", ("Oppe", "0"), ("Nede", "1"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(duplicate, "enum-def-duplicate-name"), Is.EqualTo(1),
                    "ONE fault at two sites: the second value is the location, the first a related one");
                Assert.That(Single(duplicate, "enum-def-duplicate-name").Message,
                    Is.EqualTo("Enumerator typen 'Tilstand' har to værdier med navnet 'Oppe'."));
                Assert.That(Count(distinct, "enum-def-duplicate-name"), Is.Zero);
            });
        }

        [Test]
        public void OneNameUsedInTwoDifferentTypesIsNotACollision()
        {
            Project twoTypes = Tree.WithRoot(Definitions(
                Definition("Tilstand", 0x40, ("Oppe", "0")),
                Definition("Retning", 0x50, ("Oppe", "0"))));

            Assert.That(Count(twoTypes, "enum-def-duplicate-name"), Is.Zero,
                "the row is about one type's own values; two types may both declare an Oppe");
        }

        // ── enum-def-duplicate-index ────────────────────────────────────────────────────────────────

        /// <summary>
        /// The fact the whole row rests on: the canonicalizer elides <c>index="0"</c>, so a value carrying no
        /// index occupies index zero and collides with one that spells it out.
        /// </summary>
        [Test]
        public void AnAbsentIndexCollidesWithAnExplicitZero()
        {
            Project collision = Definition("Tilstand", ("Ukendt", null), ("Oppe", "0"));
            Project canonical = Definition("Tilstand", ("Ukendt", null), ("Oppe", "1"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(collision, "enum-def-duplicate-index"), Is.EqualTo(1),
                    "an absent index IS zero — the shape a hand-edited file produces");
                Assert.That(Single(collision, "enum-def-duplicate-index").Severity,
                    Is.EqualTo(ValidationSeverity.Error), "the catalogue rates this one an Error");
                Assert.That(Single(collision, "enum-def-duplicate-index").Message,
                    Is.EqualTo("Enumerator typen 'Tilstand' har to værdier med indeks 0."));
                Assert.That(Count(canonical, "enum-def-duplicate-index"), Is.Zero,
                    "how every authentic definition is stored: first value bare, the rest numbered");
            });
        }

        [Test]
        public void TwoValuesWithoutAnIndexCollideWithEachOther()
        {
            Assert.That(Count(Definition("Tilstand", ("Ukendt", null), ("Oppe", null)),
                "enum-def-duplicate-index"), Is.EqualTo(1),
                "both default to zero, so they are the same index");
        }

        [Test]
        public void AnIndexThatIsNotANumberIsTheSchemasBusiness()
        {
            Assert.That(Count(Definition("Tilstand", ("Ukendt", "x"), ("Oppe", "y")),
                "enum-def-duplicate-index"), Is.Zero,
                "unparseable indices are not compared here: that is attr-enum-range's row");
        }

        // ── enum-def-unused ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void AnAuthoredTypeNoVariableDeclaresIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(WithVariable(referencing: false), "enum-def-unused"), Is.EqualTo(1));
                Assert.That(Single(WithVariable(referencing: false), "enum-def-unused").Message,
                    Is.EqualTo("Enumerator typen 'Tilstand' bruges ikke af nogen variabel."));
                Assert.That(Count(WithVariable(referencing: true), "enum-def-unused"), Is.Zero,
                    "one resource_enum naming it by typedef is a reference — the only reference form in the format");
            });
        }

        /// <summary>
        /// The exclusion that keeps this row off ordinary vendor content: a system table is shipped with the
        /// format, read-only in the application, and unreferenced in most projects.
        /// </summary>
        [Test]
        public void ASystemTableIsNeverReportedThoughItIsUsuallyUnreferenced()
        {
            Project project = Tree.WithRoot(Definitions(
                SystemTable("Persienne tilstand", 0x40),
                Definition("Tilstand", 0x50, ("Oppe", "0"), ("Nede", "1"))));

            Assert.That(Count(project, "enum-def-unused"), Is.EqualTo(1),
                "the authored type, and nothing for the shipped table beside it");
        }

        [Test]
        public void TheDataTablesDefinitionIsNeverReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Definition(UserTexts), "enum-def-empty"), Is.Zero,
                    "empty until the first user-defined text is added, which is an ordinary state");
                Assert.That(Count(Definition(UserTexts, ("Kælder", "0")), "enum-def-single-value"), Is.Zero,
                    "one text is not a type stuck in one state");
                Assert.That(Count(Definition(UserTexts, ("Kælder", "0")), "enum-def-unused"), Is.Zero,
                    "no variable is ever declared of it, so 'unused' says nothing about it");
            });
        }

        [Test]
        public void TheCorpusReportsOnlyAuthoredTypesAsUnused()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Authentic("Project0-Tomt.vis"), "enum-def-unused"), Is.Zero,
                    "an empty project ships two unreferenced system tables and must stay silent");
                Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "enum-def-unused"), Is.Zero,
                    "three authored types, every one bound to a library block's variables");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "enum-def-unused"), Is.EqualTo(1),
                    "the authored TestEnum, which no variable declares");
            });
        }

        // ── enum-def-empty and enum-def-single-value ────────────────────────────────────────────────

        [Test]
        public void ATypeWithNoValuesAndATypeWithOneValueAreEachReported()
        {
            Project empty = Definition("Tilstand");
            Project single = Definition("Tilstand", ("Kun", "0"));
            Project two = Definition("Tilstand", ("Oppe", "0"), ("Nede", "1"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(empty, "enum-def-empty"), Is.EqualTo(1));
                Assert.That(Single(empty, "enum-def-empty").Message,
                    Is.EqualTo("Enumerator typen 'Tilstand' har ingen værdier."));
                Assert.That(Count(empty, "enum-def-single-value"), Is.Zero, "no values is not one value");
                Assert.That(Count(single, "enum-def-single-value"), Is.EqualTo(1));
                Assert.That(Single(single, "enum-def-single-value").Message,
                    Is.EqualTo("Enumerator typen 'Tilstand' har kun én værdi, 'Kun'."),
                    "the reader's question is which state the variable is stuck in");
                Assert.That(Count(two, "enum-def-empty"), Is.Zero);
                Assert.That(Count(two, "enum-def-single-value"), Is.Zero);
            });
        }

        [Test]
        public void AnEmptyAuthoredTypeIsWitnessedByAnAuthenticProject()
        {
            Assert.That(Count(Authentic("project3-KompleksWired.vis"), "enum-def-empty"), Is.EqualTo(1),
                "project3 carries an authored TestEnum with no values — this row is not fixture-only");
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(TestSetup.Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static ProjectElement Definitions(params ProjectElement[] definitions) =>
            Tree.Node("enum_definitions", Token("enum_definitions", 0x30), [("name", "Enum typer")], definitions);

        /// <summary>One definition and its values; a null index omits the attribute, as the canonicalizer does.</summary>
        private static ProjectElement Definition(string name, int at, params (string Name, string? Index)[] values) =>
            Tree.Node("enum_definition", Token("enum_definition", at), [("name", name)],
                [.. values.Select((v, i) => Tree.Node("enum_value", Token("enum_value", at + 0x100 + i),
                    v.Index is null ? [("name", v.Name)] : [("name", v.Name), ("index", v.Index)]))]);

        /// <summary>A project holding exactly one definition, at a fixed counter.</summary>
        private static Project Definition(string name, params (string Name, string? Index)[] values) =>
            Tree.WithRoot(Definitions(Definition(name, 0x40, values)));

        /// <summary>A <c>typeid</c>-bearing system table: shipped with the format, read-only in the application.</summary>
        private static ProjectElement SystemTable(string name, int at) =>
            Tree.Node("enum_definition", Token("enum_definition", at),
                [("typeid", "_0x10"), ("name", name)],
                Tree.Node("enum_value", Token("enum_value", at + 0x100), [("name", "Ukendt")]),
                Tree.Node("enum_value", Token("enum_value", at + 0x101), [("name", "Oppe"), ("index", "1")]));

        /// <summary>One authored definition, and a block variable that either names it by typedef or does not.</summary>
        private static Project WithVariable(bool referencing)
        {
            string definitionId = Token("enum_definition", 0x40);
            ImmutableArray<(string, string)> variable = referencing
                ? [("name", "Tilstand"), ("typedef", definitionId)]
                : [("name", "Tilstand")];

            return Tree.WithRoot(
                Definitions(Definition("Tilstand", 0x40, ("Oppe", "0"), ("Nede", "1"))),
                Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                    Tree.Node("group", Token("group", 0x21), [("name", "Stue")],
                        Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Blok")],
                            Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")]),
                            Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                            Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                            Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                                Tree.Node("resource_enum", Token("resource_enum", 0x80), [.. variable])),
                            Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")])))));
        }
    }
}
