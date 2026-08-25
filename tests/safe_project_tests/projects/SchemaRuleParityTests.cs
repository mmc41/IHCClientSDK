using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// PARITY for the five schema-conformance rules: what the engine reports for
    /// <c>attr-required</c>, <c>attr-enum-range</c>, <c>attr-undeclared</c>, <c>attr-latin1</c> and
    /// <c>element-undeclared</c>, checked against the recording made before any of them moved.
    ///
    /// <para><b>What is asserted unchanged, and what is asserted CHANGED.</b> Severity, rule id and locator must
    /// reproduce the recording exactly — those are the things a migration must not disturb. Two things change on
    /// purpose and are therefore asserted to their new values rather than to the old: the MESSAGE becomes the
    /// entry's short Danish label (the English sentence moves to the diagnostic, losing nothing), and the CATEGORY
    /// moves off the transitional <c>Structural</c> value onto the catalogue's real one. Declaring a change is
    /// what makes it intended; an undeclared one fails.</para>
    ///
    /// <para><b>Why this runs beside the old validator rather than replacing it.</b> The shipped pipeline still
    /// orchestrates: it emits findings in PASS order — every id rule, then every element, then the root — while
    /// the engine emits in DOCUMENT order. Both are deterministic and the engine's is the better contract, but
    /// they are not the same sequence, so switching one rule at a time would reorder the output around every rule
    /// that had not moved yet. The rules move first and the ORCHESTRATION moves once, at the end, where the
    /// reordering can be declared and re-recorded as a single reviewed change. Until then this fixture is what
    /// proves each migrated rule still says exactly what it said.</para>
    /// </summary>
    [TestFixture]
    public sealed class SchemaRuleParityTests
    {
        private static readonly ImmutableArray<string> MigratedIds =
            ["attr-required", "attr-enum-range", "attr-undeclared", "attr-latin1", "element-undeclared"];

        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        /// <summary>One corpus case by name, built through the same fixtures the recording was made from.</summary>
        private static Project CorpusCase(string name) =>
            ValidationCharacterizationTests.Corpus.Single(c => c.Case == name).Build();

        private static WholeProjectValidator Engine() =>
            new(RuleSet.Create(Catalog, SchemaConformanceRules.All(Catalog)));

        /// <summary>
        /// The recorded findings for the migrated ids, per corpus case — through the shared machinery, so this
        /// fixture and every other migration fixture select and group the recording the same way.
        /// </summary>
        private static ILookup<string, RecordedFinding> Recorded() => MigrationParity.Recorded(MigratedIds);

        [Test]
        public void EveryMigratedRuleIsDeclaredInTheCatalogueWithADanishLabelAndAnEnglishDiagnostic()
        {
            Assert.Multiple(() =>
            {
                foreach (string code in MigratedIds)
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True, code);
                    Assert.That(entry.MessageTemplate, Is.Not.Empty, $"{code}: the user-facing Danish label");
                    Assert.That(entry.Diagnostic, Is.Not.Null.And.Not.Empty, $"{code}: the English engine sentence");
                    Assert.That(entry.Kind, Is.EqualTo(RuleKind.SchemaSerializationGuard), code);
                    Assert.That(entry.Category, Is.EqualTo(ValidationCategory.FileIntegrity), code);
                }
            });
        }

        /// <summary>
        /// The migrated rules reproduce the recording for their own ids: same set, same count, same severity and
        /// same locator per case — with the message and category at their declared new values.
        /// </summary>
        [Test]
        public void TheEngineReproducesTheRecordedTuplesForTheMigratedIds()
        {
            WholeProjectValidator engine = Engine();
            ILookup<string, RecordedFinding> recorded = Recorded();

            Assert.That(recorded, Is.Not.Empty, "the corpus must witness these rules, or this gate is vacuous");

            Assert.Multiple(() =>
            {
                foreach (IGrouping<string, RecordedFinding> forCase in recorded)
                {
                    Project project = CorpusCase(forCase.Key);

                    // Both rows come from the SHARED builders, so the produced and recorded sides cannot spell a
                    // cell differently — an absent locator above all, which the two artifacts represent
                    // differently in their own terms.
                    string[] produced = [.. engine.Validate(project, ValidationProfile.Categorized)
                        .Select(MigrationParity.Row)
                        .OrderBy(row => row, StringComparer.Ordinal)];
                    string[] expected = [.. forCase
                        .Select(MigrationParity.Expected)
                        .OrderBy(row => row, StringComparer.Ordinal)];

                    Assert.That(produced, Is.EqualTo(expected).AsCollection, forCase.Key);
                }
            });
        }

        /// <summary>
        /// The English sentence is not lost, only relocated. Each migrated entry's diagnostic still says what the
        /// recorded message said — same subject, same fault — so a developer reading a log learns what they used
        /// to learn from the finding itself.
        /// </summary>
        [Test]
        public void TheRecordedEnglishSentenceSurvivesAsTheDiagnostic()
        {
            (string Code, string Fragment)[] expected =
            [
                ("attr-required", "required attribute"),
                ("attr-enum-range", "is not one of"),
                ("attr-undeclared", "is not declared in the element"),
                ("attr-latin1", "non-ISO-8859-1"),
                ("element-undeclared", "is not declared in the project"),
            ];

            Assert.Multiple(() =>
            {
                foreach ((string code, string fragment) in expected)
                {
                    Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry);
                    Assert.That(entry.Diagnostic, Does.Contain(fragment), code);
                }
            });
        }

        /// <summary>
        /// Three of the five are ALSO save refusals in the catalogue's own terms, and the migration must not have
        /// quietly demoted them to findings alone. The refusal still lives at the serializer's throw site; what
        /// this pins is that the catalogue still says so.
        /// </summary>
        [Test]
        public void TheThreeSaveBlockingRulesKeepBothFaces()
        {
            Assert.Multiple(() =>
            {
                foreach (string code in new[] { "attr-undeclared", "attr-latin1", "element-undeclared" })
                {
                    Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Error),
                        $"{code}: it reports a finding at validate");
                    Assert.That(entry.Faces & RuleFaces.WholeProject, Is.Not.EqualTo(RuleFaces.None),
                        $"{code}: and the whole-project face is what reports it");
                }
            });
        }

        /// <summary>
        /// A rule that reports nothing on a clean project is as important as one that reports on a dirty one: a
        /// migration that widened a predicate would show up here and nowhere else.
        /// </summary>
        [Test]
        public void TheMigratedRulesAreSilentOnAnAuthenticVendorProject()
        {
            Project authentic = CorpusCase("authentic/Project1-SimpelWired");

            Assert.That(Engine().Validate(authentic, ValidationProfile.Categorized), Is.Empty,
                "a vendor-authored file conforms to its own schema; anything here is a false positive");
        }
    }
}
