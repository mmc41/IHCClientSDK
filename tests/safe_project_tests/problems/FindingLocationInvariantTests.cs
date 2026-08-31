using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Cross-rule invariants on WHERE a finding points, checked over the whole characterization corpus rather than
    /// per rule.
    ///
    /// <para><b>Why over the corpus, and why not in each rule's own suite.</b> The defect these catch is a slip in
    /// how one <c>ReportGroup</c> call is written, and a rule's own suite counts findings and reads messages — the
    /// shapes it asserts are exactly the ones that survive the flattening to
    /// <see cref="ProjectValidationFinding"/>, where <see cref="ValidationFinding.Related"/> does not appear. So a
    /// primary listed a second time among its own related locations is invisible to every rule suite AND to the
    /// characterization oracle, which records the primary locator only. One corpus-wide invariant covers the
    /// sixteen <c>ReportGroup</c> sites that ship and every one added later, which is the durable form.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingLocationInvariantTests
    {
        /// <summary>
        /// The engine's own findings — <see cref="IWholeProjectValidator.Validate"/> rather than
        /// <c>ProjectAppService</c>, because the structured locations are exactly what the app-service boundary
        /// flattens away.
        /// </summary>
        private static ImmutableArray<(string Case, ValidationFinding Finding)> CorpusFindings()
        {
            var found = ImmutableArray.CreateBuilder<(string, ValidationFinding)>();
            foreach ((string name, Func<Project> build) in ValidationCharacterizationTests.Corpus)
            {
                foreach (ValidationFinding finding in
                    ProjectRules.Validator.Validate(build(), ValidationProfile.Categorized).Findings)
                {
                    found.Add((name, finding));
                }
            }

            return found.ToImmutable();
        }

        /// <summary>
        /// The ONE row whose sites legitimately share a locator: a duplicate id token is N DIFFERENT elements
        /// carrying the SAME id, so every site's locator is that id and the check below cannot tell "the whole
        /// group was passed" from "this is the collision". The locator is a proxy for element identity everywhere
        /// else, and this is the row where the proxy is the subject.
        /// <para>
        /// The row is not left unchecked: <c>DuplicateIdGroupingTests</c> pins its group size, its anchor and its
        /// sentence directly.
        /// </para>
        /// </summary>
        private const string LocatorIsTheDuplicatedValue = "id-duplicate-token";

        /// <summary>
        /// A finding's related locations are the OTHER sites. Listing the primary among them makes the anchor
        /// element appear twice in one finding, so a reader is sent to the place they are already standing and a
        /// count taken over the group is one too high.
        /// </summary>
        [Test]
        public void NoFindingListsItsPrimaryAmongItsRelatedLocations()
        {
            List<string> offenders =
            [
                .. CorpusFindings()
                    .Where(pair => pair.Finding.Code.Value != LocatorIsTheDuplicatedValue)
                    .Where(pair => pair.Finding.Primary?.Locator is { Length: > 0 } primary
                        && pair.Finding.Related.Any(r =>
                            string.Equals(r.Locator, primary, StringComparison.Ordinal)))
                    .Select(pair =>
                        $"{pair.Case}\t{pair.Finding.Code.Value}\t{pair.Finding.Primary!.Locator}")
                    .Distinct(),
            ];

            Assert.That(offenders, Is.Empty,
                "a related location repeating the primary is a ReportGroup call passing the whole group where it "
                + "should pass the tail: " + Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }

        /// <summary>
        /// The armed control: the invariant must be able to fail. A finding built with its primary repeated in the
        /// related set is what the corpus check looks for, so the predicate has to catch this one.
        /// </summary>
        [Test]
        public void TheInvariantCatchesARepeatedPrimary()
        {
            FindingLocation primary = new("_0x1234", null, null);
            EquatableArray<FindingLocation> related = EquatableArray.Create([primary, new("_0x5678", null, null)]);

            Assert.That(
                related.Any(r => string.Equals(r.Locator, primary.Locator, StringComparison.Ordinal)),
                Is.True,
                "the predicate the corpus check applies must come out true for a repeated primary");
        }

        /// <summary>
        /// Non-vacuity: the corpus must actually produce grouped findings, or the invariant above passes because
        /// nothing has a related location at all.
        /// </summary>
        [Test]
        public void TheCorpusProducesFindingsWithRelatedLocations()
        {
            ImmutableArray<(string Case, ValidationFinding Finding)> findings = CorpusFindings();

            Assert.Multiple(() =>
            {
                Assert.That(findings, Is.Not.Empty, "the corpus must produce findings");
                Assert.That(findings.Count(pair => !pair.Finding.Related.IsEmpty), Is.GreaterThan(0),
                    "at least one corpus finding must carry related locations, or the invariant is vacuous");
            });
        }
    }
}
