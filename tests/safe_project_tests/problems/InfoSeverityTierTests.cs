using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The third severity tier. <see cref="ValidationSeverity"/> used to be two-level because the only question
    /// a finding had to answer was "does this block a save?" — and the answer was yes for
    /// <see cref="ValidationSeverity.Error"/> and no for everything else.
    ///
    /// <para>A host that PRESENTS findings asks a second question the two tiers cannot answer: which of the
    /// non-blocking findings does a user have to act on, and which are merely worth knowing? So
    /// <see cref="ValidationSeverity.Info"/> is an advisory tier BELOW Warning — appended after it, never
    /// reordered, because the enum's ordinals are public API.</para>
    ///
    /// <para>It is emphatically NOT a refusal tier. A refusal is still not a finding; it is a
    /// <see cref="Problem"/> off a different method. The third value widens the advisory end of the scale, not
    /// the blocking end, which is why <see cref="ProjectValidationResult.IsValid"/> keeps its meaning exactly:
    /// only Error blocks.</para>
    ///
    /// <para>The tier ships EMPTY — no rule emits Info yet, so every committed oracle stays byte-identical and
    /// the coverage below is built from findings constructed here.</para>
    /// </summary>
    [TestFixture]
    public sealed class InfoSeverityTierTests
    {
        private static ProjectValidationFinding Finding(ValidationSeverity severity, string message) =>
            new(severity, "synthetic-" + severity.ToString().ToLowerInvariant(), "_0x1", message);

        [Test]
        public void InfoIsAppendedAfterWarningSoTheExistingOrdinalsDoNotMove()
        {
            Assert.Multiple(() =>
            {
                Assert.That((int)ValidationSeverity.Error, Is.EqualTo(0), "shipped ordinal");
                Assert.That((int)ValidationSeverity.Warning, Is.EqualTo(1), "shipped ordinal");
                Assert.That((int)ValidationSeverity.Info, Is.EqualTo(2),
                    "the new tier is APPENDED; inserting it between Error and Warning would silently " +
                    "renumber a public enum");
            });
        }

        [Test]
        public void AnInfoFindingNeverBlocksBecauseOnlyErrorBlocks()
        {
            ProjectValidationResult result = ProjectValidationResult.FromFindings(
            [
                Finding(ValidationSeverity.Info, "En oplysning."),
                Finding(ValidationSeverity.Warning, "En advarsel."),
            ]);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.True, "neither advisory tier blocks a save or an upload");
                Assert.That(result.Errors, Is.Empty, "Errors carries error messages only");
            });
        }

        [Test]
        public void AnErrorStillBlocksWhenInfoFindingsAreAlsoPresent()
        {
            ProjectValidationResult result = ProjectValidationResult.FromFindings(
            [
                Finding(ValidationSeverity.Info, "En oplysning."),
                Finding(ValidationSeverity.Error, "En fejl."),
            ]);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Errors, Is.EqualTo(new[] { "En fejl." }));
            });
        }

        [Test]
        public void TheWarningsAccessorExcludesInfoBecauseTheTiersAreDistinct()
        {
            ProjectValidationResult result = ProjectValidationResult.FromFindings(
            [
                Finding(ValidationSeverity.Warning, "En advarsel."),
                Finding(ValidationSeverity.Info, "En oplysning."),
            ]);

            Assert.That(result.Warnings, Is.EqualTo(new[] { "En advarsel." }),
                "an Info message must not leak into the warning list a caller already reads");
        }

        [Test]
        public void TheInfosAccessorReturnsTheInfoMessagesInFindingOrder()
        {
            ProjectValidationResult result = ProjectValidationResult.FromFindings(
            [
                Finding(ValidationSeverity.Info, "Foerste oplysning."),
                Finding(ValidationSeverity.Warning, "En advarsel."),
                Finding(ValidationSeverity.Info, "Anden oplysning."),
            ]);

            Assert.That(result.Infos, Is.EqualTo(new[] { "Foerste oplysning.", "Anden oplysning." }),
                "the Warnings pattern, one tier down: computed, message-only, in finding order");
        }

        [Test]
        public void TheInfosAccessorIsEmptyOnACleanResult()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProjectValidationResult.Success.Infos, Is.Empty);
                Assert.That(ProjectValidationResult.Success.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TheStructuredFindingCarriesInfoJustAsItCarriesTheOtherTwo()
        {
            ValidationFinding finding = new(
                new Problem(new ProblemCode("synthetic-info"), "En oplysning.", EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Info,
                ValidationCategory.Documentation,
                new FindingLocation("_0x1", null, null),
                EquatableArray<FindingLocation>.Empty);

            Assert.That(finding.Severity, Is.EqualTo(ValidationSeverity.Info));
        }
    }
}
