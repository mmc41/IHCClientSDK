using System;
using System.Linq;

using Ihc.Vis.Model;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-6 / D03: the duplicate-id rows emit ONE finding with related sites, as their declared
    /// <see cref="FindingShape.PrimaryWithRelated"/> promises.
    ///
    /// <para>Both rows declared that shape and neither honoured it: each reported one finding per DUPLICATE
    /// HOLDER through <c>Report</c>. Three consequences, and the third is the one a user feels — N-1 findings for
    /// a single repair, no relation tying them together, and the FIRST holder never named at all, so the reader
    /// was shown the copies and not the element they collide with.</para>
    ///
    /// <para>The grouping is derived in the id ANALYSIS, in the same pass that decides which holder is first.
    /// "First holder wins, in document order" is stated once there, and a rule re-grouping the holders itself
    /// would be a second answer to which element is the duplicate.</para>
    /// </summary>
    [TestFixture]
    public sealed class DuplicateIdGroupingTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>Three elements on one id token, plus an unrelated well-formed one.</summary>
        private static Project SharedToken() =>
            new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0xff")],
                [
                    Node("groups", null, [],
                    [
                        Node("group", "_0x2131", []),
                        Node("group", "_0x2131", []),
                        Node("group", "_0x2131", []),
                        Node("group", "_0x2231", []),
                    ]),
                ]));

        private static ValidationFinding DuplicateToken(Project project) =>
            App.ValidateStructured(project).Findings.Single(f => f.Code.Value == "id-duplicate-token");

        [Test]
        public void ThreeElementsOnOneIdProduceOneFindingWithTwoRelatedSites()
        {
            EquatableArray<ValidationFinding> findings = App.ValidateStructured(SharedToken()).Findings;

            ValidationFinding[] duplicates = [.. findings.Where(f => f.Code.Value == "id-duplicate-token")];

            Assert.Multiple(() =>
            {
                Assert.That(duplicates, Has.Length.EqualTo(1),
                    "one collision is ONE finding — it used to be one per duplicate holder");
                Assert.That(duplicates[0].Related, Has.Length.EqualTo(2),
                    "the other two holders travel with it as related sites");
                Assert.That(duplicates[0].Primary!.Locator, Is.EqualTo("_0x2131"));
            });
        }

        /// <summary>
        /// The FIRST holder is the anchor. That is the element a reader compares the copies against, and the old
        /// shape — which reported only the non-first holders — never named it.
        /// </summary>
        [Test]
        public void TheFindingIsAnchoredAtTheFirstHolderAndNamesEveryOtherOne()
        {
            ValidationFinding finding = DuplicateToken(SharedToken());

            string?[] sites = [finding.Primary!.Locator, .. finding.Related.Select(r => r.Locator)];

            Assert.Multiple(() =>
            {
                Assert.That(sites, Has.Length.EqualTo(3), "every element carrying the id is a site of the finding");
                Assert.That(finding.Related.Select(r => r.Locator), Is.All.EqualTo("_0x2131"));
            });
        }

        /// <summary>The row's arguments reach the Danish sentence, count included (D04).</summary>
        [Test]
        public void TheSentenceNamesTheIdItsElementAndHowManyShareIt()
        {
            ValidationFinding finding = DuplicateToken(SharedToken());

            Assert.Multiple(() =>
            {
                Assert.That(finding.Problem.Message,
                    Is.EqualTo("Dobbelt id '_0x2131' på <group>: 3 elementer deler dette id."));
                Assert.That(finding.Problem.Message, Does.Not.Contain("{"), "every declared slot binds");
            });
        }

        /// <summary>
        /// A COUNTER collision groups the same way. Its members share a counter and differ in token, which is why
        /// the sentence counts ids rather than elements.
        /// </summary>
        [Test]
        public void ACounterCollisionAlsoProducesOneGroupedFinding()
        {
            // An id packs as counter<<8 | typeCode, so _0x2131 and _0x2132 share counter 0x21 and differ
            // only in type code — a counter collision with no token collision.
            Project project = new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0xff")],
                [Node("groups", null, [], [Node("group", "_0x2131", []), Node("group", "_0x2132", [])])]));

            ValidationFinding[] counters =
                [.. App.ValidateStructured(project).Findings.Where(f => f.Code.Value == "id-duplicate-counter")];

            Assert.Multiple(() =>
            {
                Assert.That(counters, Has.Length.EqualTo(1), "one counter collision is one finding");
                Assert.That(counters[0].Related, Has.Length.EqualTo(1), "with the second id as its related site");
                Assert.That(counters[0].Problem.Message, Does.Contain("2 id'er"));
            });
        }

        /// <summary>
        /// T018: every site of a GROUP carries its own text, so a duplicate-id group is one navigable finding
        /// rather than N anonymous anchors. The authored NAME leads, because a duplicate id makes every site's
        /// locator identical — a label built from the id would read the same N times.
        /// </summary>
        [Test]
        public void EverySiteOfAGroupCarriesItsOwnText()
        {
            Project project = new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0xff")],
                [
                    Node("groups", null, [],
                    [
                        Node("group", "_0x2131", [("name", "Stue")]),
                        Node("group", "_0x2131", [("name", "Køkken")]),
                    ]),
                ]));

            ValidationFinding finding = DuplicateToken(project);
            string?[] texts = [finding.Primary!.Message, .. finding.Related.Select(r => r.Message)];

            Assert.Multiple(() =>
            {
                Assert.That(texts, Is.All.Not.Null, "every site of the group says what it is");
                Assert.That(texts, Is.EqualTo(new[] { "<group> 'Stue'", "<group> 'Køkken'" }).AsCollection,
                    "and the two sites are told apart by their authored names, not by their shared id");
            });
        }

        /// <summary>
        /// A single-site finding leaves the slot null: its own message already says everything, and filling it
        /// would put the same text in two places.
        /// </summary>
        [Test]
        public void AnUngroupedFindingLeavesTheSiteTextNull()
        {
            Project project = new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0xff")],
                [Node("groups", null, [], [Node("group", "_0xzz", [])])]));

            ValidationFinding finding =
                App.ValidateStructured(project).Findings.First(f => f.Code.Value == "id-wellformed");

            Assert.Multiple(() =>
            {
                Assert.That(finding.Related, Is.Empty, "the fixture's finding is a single-site one");
                Assert.That(finding.Primary!.Message, Is.Null);
            });
        }

        /// <summary>The control: a project whose ids are all distinct reports neither row.</summary>
        [Test]
        public void DistinctIdsReportNeitherCollision()
        {
            Project project = new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0xff")],
                [Node("groups", null, [], [Node("group", "_0x2131", []), Node("group", "_0x2231", [])])]));

            Assert.That(
                App.ValidateStructured(project).Findings.Where(f => f.Code.Value.StartsWith("id-duplicate", StringComparison.Ordinal)),
                Is.Empty);
        }
    }
}
