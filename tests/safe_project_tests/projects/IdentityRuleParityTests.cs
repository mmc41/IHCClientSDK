using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// PARITY for the eight IDENTITY rules — the id vocabulary and the project's high-water mark — against the
    /// recording made before any of them moved.
    ///
    /// <para><b>This is the migration that justified a shared analysis.</b> The shipped validator establishes a
    /// token-to-element map, a counter set and a maximum in ONE walk, then threads the map into the per-attribute
    /// pass for the dangling-reference rule and the maximum into the three high-water-mark rules. Eight rules, one
    /// walk. Migrating them as eight independent traversals would have meant eight walks — and, worse, two rules
    /// each re-deriving "which of these two elements is the duplicate" with a chance to disagree.</para>
    ///
    /// <para><b>The three high-water-mark rules are mutually exclusive, and the exclusivity moved into the
    /// analysis rather than being copied into three rules.</b> Each asks which fault holds and reports only if it
    /// is its own. That preserves a product decision the shipped code records in a comment: a token that never
    /// parsed must not ALSO report "0x0 is below the highest counter", because that second sentence is derived
    /// from a phantom zero and reads as a distinct fault when it is noise.</para>
    /// </summary>
    [TestFixture]
    public sealed class IdentityRuleParityTests
    {
        private static readonly ImmutableArray<string> MigratedIds =
        [
            "id-wellformed", "id-duplicate-token", "id-duplicate-counter", "id-typecode",
            "idref-dangling", "luid-malformed", "luid-ceiling", "luid-low",
        ];

        private static RuleSet Rules() =>
            RuleSet.Create(ProblemCatalog.Current, IdentityRules.All(ProblemCatalog.Current));

        [Test]
        public void EveryMigratedRuleIsDeclaredWithADanishLabelAndAnEnglishDiagnostic() =>
            MigrationParity.AssertDeclaredWithBothLanguages(MigratedIds, RuleKind.UserContentRule);

        [Test]
        public void TheEngineReproducesTheRecordedTuplesForTheMigratedIds() =>
            MigrationParity.AssertReproducesRecording(MigratedIds, Rules());

        [Test]
        public void TheMigratedRulesAreSilentOnAnAuthenticVendorProject() =>
            Assert.That(
                new WholeProjectValidator(Rules())
                    .Validate(MigrationParity.CorpusCase("authentic/Project1-SimpelWired"), ValidationProfile.Categorized),
                Is.Empty,
                "a vendor-authored file has a consistent id space; anything here is a false positive");

        /// <summary>
        /// The exclusivity, exercised directly rather than inferred from the corpus. A malformed high-water mark
        /// must produce exactly ONE finding, not a malformed one plus a below-the-mark one derived from the zero
        /// its failed parse left behind.
        /// </summary>
        [Test]
        public void AMalformedHighWaterMarkReportsOnceAndNotAlsoAsTooLow()
        {
            Project project = MigrationParity.CorpusCase("synthetic/luid-ceiling");
            string[] recordedForCase = [.. MigrationParity.Recorded(MigratedIds)["synthetic/luid-ceiling"]
                .Select(cells => cells[2])];

            string[] produced = [.. new WholeProjectValidator(Rules())
                .Validate(project, ValidationProfile.Categorized)
                .Select(f => f.Code.Value)
                .Where(code => code.StartsWith("luid-", StringComparison.Ordinal))];

            Assert.Multiple(() =>
            {
                Assert.That(produced, Has.Length.EqualTo(1), "exactly one high-water-mark fault is reported");
                Assert.That(produced, Is.EqualTo(recordedForCase.Where(c => c.StartsWith("luid-", StringComparison.Ordinal)))
                    .AsCollection, "and it is the one the recording names");
            });
        }

        /// <summary>
        /// A duplicate token is not examined further, matching the shipped behaviour: reporting its counter and
        /// type code as well would say the same collision three times, and the FIRST holder is the one whose
        /// counter and type code are the project's.
        /// </summary>
        [Test]
        public void ADuplicateTokenIsNotAlsoReportedAsADuplicateCounter()
        {
            Project project = new(Tree.Node("utcs_project", null, [("version_major", "4"), ("last_unique_id", "_0xffff")],
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", []),
                    Tree.Node("group", "_0x2121", []))));

            string[] produced = [.. new WholeProjectValidator(Rules())
                .Validate(project, ValidationProfile.Categorized)
                .Select(f => f.Code.Value)];

            Assert.Multiple(() =>
            {
                Assert.That(produced.Count(c => c == "id-duplicate-token"), Is.EqualTo(1));
                Assert.That(produced, Does.Not.Contain("id-duplicate-counter"),
                    "the second holder is one collision, reported once");
            });
        }

        /// <summary>
        /// The shared analysis is read, not re-derived: every rule here sees the SAME first-holder-wins answer,
        /// which is what makes "which of these is the duplicate" stable rather than a function of rule order.
        /// </summary>
        [Test]
        public void AllEightRulesReadOneWalk()
        {
            RuleSet rules = Rules();

            Assert.Multiple(() =>
            {
                Assert.That(rules.Rules, Has.Length.EqualTo(MigratedIds.Length));
                Assert.That(rules.Codes.Select(c => c.Value), Is.EquivalentTo(MigratedIds));

                // Every one is a traversal: none of these is expressible as a per-field predicate, because each
                // needs cross-element state the field itself cannot supply.
                Assert.That(rules.Rules.All(r => r.Inspection is not null), Is.True);
                Assert.That(rules.Rules.All(r => r.Constraints is null), Is.True);
            });
        }
    }
}
