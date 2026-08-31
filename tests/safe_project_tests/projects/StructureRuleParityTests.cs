using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The STRUCTURE rules — the root's shape and version, containment, the function-block checks, embedded
    /// constants and the program skeleton: that each is declared with both halves of the language split, that the
    /// tolerated deviations keep their advisory severity, and that the locality rule defers where it must. What
    /// these rules report over the corpus is pinned byte for byte by
    /// <c>ValidationCharacterizationTests.Corpus_ReproducesItsOracleByteForByte</c>.
    ///
    /// <para><b>The tolerated deviations are WARNINGS and must stay warnings.</b> An unusual root child order, an
    /// unmodeled containment and a deviant program skeleton all LOAD AND WORK, and vendor tooling tolerates every
    /// one of them. Promoting any of the three would be this tool asserting a rule the file format does not have,
    /// and it would block saves on files the vendor editor itself authors. The severity is therefore asserted
    /// per rule rather than left to the migration to preserve by accident.</para>
    ///
    /// <para><b>The locality rule and the dangling-reference rule deliberately stay out of each other's
    /// way.</b> A programming reference to an id NO
    /// element carries is the dangling-reference rule's business; the block-locality rule fires only when the id
    /// EXISTS but lives in another block. Reporting both would tell the user twice about one broken reference,
    /// and the split is what the shipped code does today.</para>
    /// </summary>
    [TestFixture]
    public sealed class StructureRuleParityTests
    {
        private static readonly ImmutableArray<string> MigratedIds =
        [
            "root-children", "root-version", "root-version-minor", "containment", "fb-shape", "fb-programs",
            "fb-pin-container", "fb-local-ref", "inline-constant", "program-shape",
        ];

        private static RuleSet Rules() =>
            RuleSet.Create(ProblemCatalog.Current, StructureRules.All(ProblemCatalog.Current));

        [Test]
        public void EveryMigratedRuleIsDeclaredWithADanishLabelAndAnEnglishDiagnostic() =>
            MigrationParity.AssertDeclaredWithBothLanguages(MigratedIds, RuleKind.UserContentRule);

        /// <summary>
        /// The three advisory rules keep their severity. This is not bookkeeping: each names a state the vendor
        /// tooling loads without complaint, so an Error here would block a save on a file the vendor editor wrote.
        /// </summary>
        [Test]
        public void TheThreeToleratedDeviationsStayWarnings()
        {
            Assert.Multiple(() =>
            {
                foreach (string advisory in new[] { "root-children", "containment", "program-shape" })
                {
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(advisory), out ProblemCatalogEntry entry),
                        Is.True, advisory);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Warning), advisory);
                }

                foreach (string error in new[] { "fb-shape", "fb-programs", "fb-pin-container", "fb-local-ref", "inline-constant" })
                {
                    ProblemCatalog.Current.TryGet(new ProblemCode(error), out ProblemCatalogEntry entry);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Error), error);
                }
            });
        }

        /// <summary>
        /// A programming reference to an id NOTHING carries is not a locality violation. The locality rule's whole
        /// question is "the id exists — is it in this block?", which a nonexistent id cannot answer, and reporting
        /// both would tell the user twice about one broken reference.
        ///
        /// <para>Built as its own fixture rather than read from the corpus: the corpus's locality case carries no
        /// DANGLING programming reference, so a corpus-only check passes whether or not the rule defers — which
        /// was verified by removing the deference and watching nothing fail.</para>
        /// </summary>
        [Test]
        public void AProgrammingReferenceToANonexistentIdIsNotALocalityViolation()
        {
            Project project = MigrationParity.CorpusCase("synthetic/fb-locality");
            ProjectElement block = project.Root.DescendantsAndSelf().First(e => e.Tag == "functionblock");
            string[] localityFindings = [.. new WholeProjectValidator(Rules())
                .Validate(project, ValidationProfile.Categorized).Findings
                .Where(f => f.Code.Value == "fb-local-ref")
                .Select(f => f.Primary!.Locator!)];

            // The same project, with every block-local reference re-pointed at an id no element carries.
            Project dangling = new(Repoint(project.Root));

            string[] afterRepointing = [.. new WholeProjectValidator(Rules())
                .Validate(dangling, ValidationProfile.Categorized).Findings
                .Where(f => f.Code.Value == "fb-local-ref")
                .Select(f => f.Primary!.Locator!)];

            Assert.Multiple(() =>
            {
                Assert.That(block, Is.Not.Null, "precondition: the case carries a function block");
                Assert.That(localityFindings, Is.Not.Empty,
                    "precondition: the case carries a reference that exists but lives outside its block");
                Assert.That(afterRepointing, Is.Empty,
                    "re-pointed at an id nothing carries, the locality rule falls silent and the dangling rule owns it");
            });
        }

        /// <summary>Re-points every block-local programming reference at an id no element in the project carries.</summary>
        private static ProjectElement Repoint(ProjectElement element)
        {
            // Read from the rule's own declaration rather than restating it. A second literal here would keep
            // this test passing after a pair is added to StructureRules, which is precisely the drift the
            // parity test exists to catch.
            ImmutableHashSet<(string Tag, string Attribute)> local = StructureRules.BlockLocalReferences;

            ImmutableArray<(string, string)> attrs = [.. element.Attrs.Select(a =>
                local.Contains((element.Tag, a.Item1)) && a.Item2 != "_0x0"
                    ? (a.Item1, "_0xdead99")
                    : (a.Item1, a.Item2))];

            return new ProjectElement(element.Tag, element.Id, attrs, [.. element.Children.Select(Repoint)]);
        }
    }
}
