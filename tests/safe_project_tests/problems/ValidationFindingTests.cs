using System;
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

                Assert.That(locationMembers, Is.EquivalentTo(new[]
                {
                    nameof(FindingLocation.Locator), nameof(FindingLocation.Element), nameof(FindingLocation.Message),
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
    }
}
