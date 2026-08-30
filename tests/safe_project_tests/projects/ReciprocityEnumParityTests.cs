using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The BIJECTION and ENUM rules: that each is declared with both halves of the language split, and the
    /// behaviours no corpus case witnesses. What these rules report over the corpus is pinned byte for byte by
    /// <c>ValidationCharacterizationTests.Corpus_ReproducesItsOracleByteForByte</c>.
    ///
    /// <para><b>The bijection ids needed checking specifically.</b> In the shipped code both come out of ONE
    /// helper with the rule id passed in as a parameter — so a scan of call sites cannot see either id, and
    /// swapping them, or losing one, would be invisible to a reader. Here each id is bound to its own registered
    /// rule, and the tests below check that BOTH still emit and that each emits under its own id.</para>
    ///
    /// <para><b>The configurations differ in exactly one behaviour worth pinning.</b> A scene row may be
    /// authored UNWIRED and is legitimately skipped; a follow-link half is never unwired, so an unwired one is
    /// corruption. Getting that backwards would either flood every project that has a half-built scene, or go
    /// silent on genuinely broken wiring.</para>
    /// </summary>
    [TestFixture]
    public sealed class ReciprocityEnumParityTests
    {
        private static readonly ImmutableArray<string> MigratedIds =
            ["link-bijection", "scene-bijection", "enum-typedef", "enum-inivalue"];

        private static RuleSet Rules() =>
            RuleSet.Create(ProblemCatalog.Current, ReciprocityAndEnumRules.All(ProblemCatalog.Current));

        [Test]
        public void EveryMigratedRuleIsDeclaredWithADanishLabelAndAnEnglishDiagnostic() =>
            MigrationParity.AssertDeclaredWithBothLanguages(MigratedIds, RuleKind.UserContentRule);

        /// <summary>
        /// Both bijection ids emit, and each under its own. The shipped code passes the id in as a parameter, so
        /// this is the check a call-site scan could never make.
        /// </summary>
        [Test]
        public void BothBijectionIdsStillEmitUnderTheirOwnId()
        {
            string[] produced = [.. new WholeProjectValidator(Rules())
                .Validate(MigrationParity.CorpusCase("synthetic/bijection"), ValidationProfile.Categorized)
                .Select(f => f.Code.Value)];

            Assert.Multiple(() =>
            {
                Assert.That(produced, Does.Contain("link-bijection"));
                Assert.That(produced, Does.Contain("scene-bijection"));
                Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("link-bijection"), out ProblemCatalogEntry link), Is.True);
                Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("scene-bijection"), out ProblemCatalogEntry scene), Is.True);
                Assert.That(link.Category, Is.EqualTo(ValidationCategory.Wiring));
                Assert.That(scene.Category, Is.EqualTo(ValidationCategory.Scenes),
                    "two rules, two categories — which is also why one shared id would have been wrong");
            });
        }

        /// <summary>
        /// The one behavioural difference between the two configurations: an unwired SCENE row is a legitimate
        /// authored state and an unwired follow-link half is not. Built as its own fixture, because the corpus
        /// case exercises broken partners rather than absent ones.
        /// </summary>
        [Test]
        public void AnUnwiredSceneRowIsLegitimateAndAnUnwiredFollowLinkHalfIsNot()
        {
            Project sceneOnly = new(Tree.Node("utcs_project", null, [],
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", [],
                        Tree.Node("scene_member_dataline", "_0x5a01", [("link", ElementId.NullToken)])))));

            Project linkOnly = new(Tree.Node("utcs_project", null, [],
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", [],
                        Tree.Node("link_from_resource", "_0x5b01", [("link", ElementId.NullToken)])))));

            WholeProjectValidator engine = new(Rules());

            Assert.Multiple(() =>
            {
                Assert.That(engine.Validate(sceneOnly, ValidationProfile.Categorized).Select(f => f.Code.Value),
                    Does.Not.Contain("scene-bijection"),
                    "an unwired scene row is a half-built scene, not corruption");
                Assert.That(engine.Validate(linkOnly, ValidationProfile.Categorized).Select(f => f.Code.Value),
                    Does.Contain("link-bijection"),
                    "an unwired follow-link half is never authored, so it is corruption");
            });
        }

        /// <summary>
        /// The two enum rules stay out of each other's way and out of the reference rule's way. A type reference
        /// that resolves to a non-definition is ONE fault, reported by the typedef rule; the initial-value rule
        /// has no set of values to check against and stays silent.
        /// </summary>
        [Test]
        public void AWrongTypeReferenceIsReportedOnceAndNotAlsoAsAWrongInitialValue()
        {
            Project project = new(Tree.Node("utcs_project", null, [],
                Tree.Node("enum_definitions", "_0x4040", [],
                    Tree.Node("group", "_0x2141", [("name", "not an enum definition")])),
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", [],
                        Tree.Node("resource_enum", "_0x7001",
                            [("name", "V"), ("typedef", "_0x2141"), ("inivalue", "_0xdead01")])))));

            string[] produced = [.. new WholeProjectValidator(Rules())
                .Validate(project, ValidationProfile.Categorized).Select(f => f.Code.Value)];

            Assert.Multiple(() =>
            {
                Assert.That(produced.Count(c => c == "enum-typedef"), Is.EqualTo(1));
                Assert.That(produced, Does.Not.Contain("enum-inivalue"),
                    "without a definition there is no value set, so a second finding would be derived from nothing");
            });
        }
    }
}
