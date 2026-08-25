using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The finding's structured LOCATIONS, and the two things the shape deliberately does not have.
    ///
    /// <para><b>Why the id cannot be the universal anchor.</b> Four cases break it, and all four are real in this
    /// engine: a MALFORMED id cannot be parsed and the finding is about the malformation; a DUPLICATE id resolves
    /// to two elements, so it identifies no single site; a whole-project finding has several sites or none; and a
    /// pre-parse fault has no tree at all. So the raw locator is the always-available anchor and the parsed id is
    /// the convenience for when it is both well-formed and unambiguous.</para>
    ///
    /// <para><b>The related-location message slot is what makes a collision one finding.</b> Today a duplicate-id
    /// group produces N separate findings, telling the user N times that two things collide. One finding with a
    /// primary site and N−1 related sites, each able to say why it is listed, is one navigable fault.</para>
    ///
    /// <para><b>What is absent, and why the absence is asserted here.</b> There is no source POSITION, because
    /// every pre-parse fault is a refusal and a refusal is not a finding — a position could never reach this type.
    /// And there is no PRODUCER marker: it existed so a host could present a commit refusal differently from a
    /// report row, but a commit refusal is a different type off a different method, so the caller already knows
    /// which it asked for. Its only test asserted that no verdict branched on it; not having the member makes
    /// that structural rather than asserted, and the test below is what keeps it that way.</para>
    /// </summary>
    [TestFixture]
    public sealed class ValidationFindingTests
    {
        private static Problem P(string code, string message) =>
            new(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty);

        [Test]
        public void ARawLocatorAnchorsAFindingWhoseIdCannotBeParsed()
        {
            FindingLocation malformed = new("_0xNOTHEX", null, null);

            Assert.Multiple(() =>
            {
                Assert.That(malformed.Locator, Is.EqualTo("_0xNOTHEX"), "the raw token survives verbatim");
                Assert.That(malformed.Element, Is.Null, "there is no parsed id, and that is the finding");
            });
        }

        [Test]
        public void AParsedIdIsCarriedBesideTheLocatorWhenTheTokenIsWellFormed()
        {
            Assert.That(ElementId.TryParse("_0x2a", out ElementId id), Is.True);
            FindingLocation located = new("_0x2a", id, null);

            Assert.Multiple(() =>
            {
                Assert.That(located.Element, Is.EqualTo(id));
                Assert.That(located.Locator, Is.EqualTo("_0x2a"));
            });
        }

        [Test]
        public void AWholeProjectFindingHasNoPrimaryLocationAtAll()
        {
            ValidationFinding finding = new(
                P("root-children", "Uventet rækkefølge"), ValidationSeverity.Warning,
                ValidationCategory.FileIntegrity, null, EquatableArray<FindingLocation>.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(finding.Primary, Is.Null, "several sites or none — an id anchor would have to invent one");
                Assert.That(finding.Related, Is.Empty);
            });
        }

        /// <summary>
        /// The motivating case: a collision reported once, with every colliding site reachable from it, and each
        /// related site able to say why it is listed.
        /// </summary>
        [Test]
        public void ACollisionIsOneFindingCarryingEveryCollidingSite()
        {
            ValidationFinding finding = new(
                P("id-duplicate-token", "Dobbelt id"),
                ValidationSeverity.Error,
                ValidationCategory.FileIntegrity,
                new FindingLocation("_0x2a", null, null),
                EquatableArray.Create<FindingLocation>(
                [
                    new FindingLocation("_0x2a", null, "Også her"),
                    new FindingLocation("_0x2a", null, "Og her"),
                ]));

            Assert.Multiple(() =>
            {
                Assert.That(finding.Related, Has.Length.EqualTo(2), "one fault, three sites — not three faults");
                Assert.That(finding.Related.Select(r => r.Message), Is.All.Not.Null,
                    "each related site says why it is listed");
                Assert.That(finding.Primary!.Message, Is.Null,
                    "the finding's own message already covers the primary site");
            });
        }

        [Test]
        public void AFindingCarriesItsProblemRatherThanRestatingIt()
        {
            Problem problem = new(
                new ProblemCode("doc-cabletype"), "Mangler Kabeltype",
                EquatableArray.Create<ProblemArgument>([new ProblemArgument("element", "_0x2a")]),
                Diagnostic: "product_dataline has no cabletype attribute.");
            ValidationFinding finding = new(
                problem, ValidationSeverity.Warning, ValidationCategory.Documentation,
                new FindingLocation("_0x2a", null, null), EquatableArray<FindingLocation>.Empty);

            string[] members = [.. typeof(ValidationFinding)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)];

            Assert.Multiple(() =>
            {
                Assert.That(finding.Problem, Is.SameAs(problem), "one problem value travels; nothing rebuilds it");
                Assert.That(finding.Code, Is.EqualTo(problem.Code), "identity is READ from the problem, not stored");

                foreach (string restated in new[] { "Message", "Arguments", "Diagnostic", "RuleId" })
                {
                    Assert.That(members, Does.Not.Contain(restated),
                        $"'{restated}' restated here could disagree with the problem it belongs to");
                }
            });
        }

        /// <summary>
        /// The two absences, asserted. This is the replacement for the gate's "zero verdict branches on the
        /// producer marker": there is no marker to branch on, which is a stronger guarantee than counting
        /// branches, and this test is what stops one being reintroduced.
        /// </summary>
        [Test]
        public void TheFindingHasNoProducerMarkerAndNoSourcePosition()
        {
            string[] members = [.. typeof(ValidationFinding)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)];
            string[] locationMembers = [.. typeof(FindingLocation)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)];

            Assert.Multiple(() =>
            {
                Assert.That(members, Is.EquivalentTo(new[]
                {
                    nameof(ValidationFinding.Problem), nameof(ValidationFinding.Severity),
                    nameof(ValidationFinding.Category), nameof(ValidationFinding.Primary),
                    nameof(ValidationFinding.Related), nameof(ValidationFinding.Code),
                }));

                foreach (string marker in new[] { "Producer", "ExecutorKind", "Origin", "Source" })
                {
                    Assert.That(members, Does.Not.Contain(marker),
                        $"'{marker}' would be a knob for a caller who already knows which face it asked");
                }

                // Xpath is the THIRD anchor, and it belongs to the same argument the other two make rather than
                // relaxing it: the locator is what is always available, the parsed id is the convenience when it
                // resolves, and the path is what names the site in the one case neither does. It is not a source
                // position — it addresses a node in the parsed tree, which a refusal has never got as far as
                // building — so the absences asserted just below are untouched by it.
                Assert.That(locationMembers, Is.EquivalentTo(new[]
                {
                    nameof(FindingLocation.Locator), nameof(FindingLocation.Element), nameof(FindingLocation.Message),
                    nameof(FindingLocation.Xpath),
                }));
                foreach (string position in new[] { "Line", "Column", "Offset", "Position" })
                {
                    Assert.That(locationMembers, Does.Not.Contain(position),
                        $"'{position}' could never be filled: a pre-parse fault is a refusal, not a finding");
                }
            });
        }

        /// <summary>
        /// The scale grew a third value, and it is worth being precise about WHICH end it grew at.
        /// <see cref="ValidationSeverity.Info"/> is an ADVISORY tier below <see cref="ValidationSeverity.Warning"/>,
        /// added because a host that presents findings must tell "you should fix this" from "you may care about
        /// this". A refusal is still not a finding — it is a <see cref="Problem"/> off a different method — so no
        /// member here means "refused", and the blocking end of the scale is still exactly
        /// <see cref="ValidationSeverity.Error"/>. A fourth member would need that same argument made again.
        /// </summary>
        [Test]
        public void SeverityIsOneBlockingTierAndTwoAdvisoryOnesBecauseARefusalIsNotAFinding()
        {
            Assert.That(Enum.GetNames<ValidationSeverity>(), Is.EquivalentTo(new[]
            {
                nameof(ValidationSeverity.Error),
                nameof(ValidationSeverity.Warning),
                nameof(ValidationSeverity.Info),
            }));
        }

        /// <summary>
        /// Every site the corpus produces, through the SAME face the characterization oracle records: the app
        /// service's categorized run. Not <c>ProjectRules.Validator.Validate(project, ValidationProfile.Categorized)</c>,
        /// which is nine findings short — the service's own profile carries the library port two rows need (D27),
        /// and a denominator that quietly differed from the oracle's would make every count below unfalsifiable.
        /// </summary>
        private static ImmutableArray<(string Case, ValidationFinding Finding)> CorpusFindings()
        {
            var app = new ProjectAppService(TestSetup.Settings);
            var found = ImmutableArray.CreateBuilder<(string, ValidationFinding)>();
            foreach ((string name, Func<Project> build) in ValidationCharacterizationTests.Corpus)
            {
                foreach (ValidationFinding finding in app.ValidateStructured(build()))
                {
                    found.Add((name, finding));
                }
            }

            return found.ToImmutable();
        }

        /// <summary>
        /// WHICH sites carry a path, measured over the whole corpus — the property that makes
        /// <see cref="FindingLocation.Xpath"/> worth its API surface.
        ///
        /// <para><b>Why the count is asserted from both ends.</b> A path on every site would be correct and
        /// useless; a path on none would pass any test that only checked the ones that have one. So this pins the
        /// exact population: 6 primary sites out of 618, and every one of them for a reason the tree can state.</para>
        ///
        /// <para><b>Why it is not derived from <see cref="FindingLocation.Element"/>.</b> That property is null in
        /// 38 of the 618 sites, and only 2 of those 38 are ambiguous — the malformed tokens. The other 36 simply
        /// have no <c>id</c> attribute (the document root, an unrecognized element) and are identified perfectly
        /// well by their tag. In the other direction, all 4 shared-token sites have a NON-null id and are
        /// ambiguous anyway. A writer branching on <c>Element is null</c> would therefore be wrong 36 times one
        /// way and 4 the other, which is exactly why the path is carried rather than inferred.</para>
        /// </summary>
        [Test]
        public void ExactlyTheSitesWhoseLocatorSelectsNoSingleNodeCarryAPath()
        {
            ImmutableArray<(string Case, ValidationFinding Finding)> corpus = CorpusFindings();
            var primaries = corpus
                .Where(f => f.Finding.Primary is not null)
                .Select(f => (f.Case, Location: f.Finding.Primary!))
                .ToImmutableArray();

            ImmutableArray<(string Case, FindingLocation Location)> withPath =
                [.. primaries.Where(p => p.Location.Xpath is not null)];
            ImmutableArray<(string Case, FindingLocation Location)> elementNull =
                [.. primaries.Where(p => p.Location.Element is null)];

            Assert.Multiple(() =>
            {
                Assert.That(corpus, Has.Length.EqualTo(618), "the corpus population the counts below are out of");
                Assert.That(withPath, Has.Length.EqualTo(6), "under 1% of sites need one");

                // The 4 ambiguous-because-SHARED sites: the token parses and two elements answer to it.
                Assert.That(
                    withPath.Where(p => p.Location.Element is not null).Select(p => p.Location.Locator),
                    Is.EqualTo(new[] { "_0x2132", "_0x2132", "_0x2132", "_0x2132" }));

                // The 2 ambiguous-because-MALFORMED sites: the token parses to nothing, so nothing answers to it.
                Assert.That(
                    withPath.Where(p => p.Location.Element is null).Select(p => p.Location.Locator),
                    Is.EqualTo(new[] { "_0xzz", "_0xzz" }));

                // Both directions of the Element-is-null trap, as counts.
                Assert.That(elementNull, Has.Length.EqualTo(38), "36 with no id attribute, plus the 2 malformed");
                Assert.That(
                    elementNull.Count(p => p.Location.Xpath is null), Is.EqualTo(36),
                    "a tag locator selects its element, so a null Element is not ambiguity");
                Assert.That(
                    withPath.Count(p => p.Location.Element is not null), Is.EqualTo(4),
                    "and a non-null Element is not unambiguity");

                // Six sites over THREE elements — several rules fire on each — and the paths are what separate
                // them, because four of the six share one locator string and the other two share the other.
                Assert.That(
                    withPath.Select(p => p.Location.Xpath).Distinct().OrderBy(x => x, StringComparer.Ordinal),
                    Is.EqualTo(new[]
                    {
                        "/utcs_project/groups/group[1]",
                        "/utcs_project/groups/group[2]",
                        "/utcs_project/groups/group[4]",
                    }));
                Assert.That(withPath.Select(p => p.Case).Distinct(), Is.EqualTo(new[] { "synthetic/ids" }));
            });
        }

        /// <summary>
        /// A related site is anchored exactly like a primary one. The duplicate-id group is the case that proves
        /// it: its primary and its related site share one locator and differ only in path, so a related site that
        /// went unpathed would leave the group's second element unnameable.
        /// </summary>
        [Test]
        public void ARelatedSiteCarriesItsOwnPathOnTheSameRule()
        {
            ImmutableArray<FindingLocation> related =
                [.. CorpusFindings().SelectMany(f => f.Finding.Related)];

            Assert.Multiple(() =>
            {
                Assert.That(related.Count(r => r.Xpath is not null), Is.EqualTo(1));
                Assert.That(
                    related.Where(r => r.Xpath is not null).Select(r => (r.Locator, r.Xpath)),
                    Is.EqualTo(new[] { ("_0x2132", "/utcs_project/groups/group[2]") }),
                    "the second holder of the shared token, which its locator cannot distinguish from the first");
            });
        }
    }
}
