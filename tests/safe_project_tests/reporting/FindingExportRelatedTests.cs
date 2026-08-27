using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The three trailing attributes: <c>related</c>, <c>xpath</c> and <c>related_xpath</c>.
    ///
    /// <para><b>Why they come last.</b> They are the widest and the rarest — a grouped finding can list 66
    /// related sites, and under 1% of lines carry a path at all. Putting them after the prose keeps the left
    /// edge of every line column-comparable, which is what a reader scanning hundreds of them in a terminal
    /// actually uses.</para>
    ///
    /// <para><b>Why a space-separated list and not child elements.</b> A locator is a <c>_0x</c> token or a
    /// bare tag and a path has no spaces either, so the delimiter is safe without escaping — this is XML's own
    /// IDREFS convention, which <c>.vis</c> itself uses. The alternative, one child element per related site,
    /// takes the corpus from 618 lines to about 1 100 and reintroduces child elements into a format whose
    /// whole point is that a finding is one line.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingExportRelatedTests
    {
        private static string Line(FindingLocation? primary, params FindingLocation[] related)
        {
            ValidationFinding finding = new(
                new Problem(new ProblemCode("id-duplicate-token"), "Dobbelt id", EquatableArray<ProblemArgument>.Empty),
                ValidationSeverity.Error,
                ValidationCategory.FileIntegrity,
                primary,
                EquatableArray.CreateRange(related))
            {
                // The real id-duplicate-token row refuses the edit-open, and carrying it here is what keeps
                // the order assertion below a statement about EVERY fixed attribute rather than most of them.
                RefusedOperations = ImmutableArray.Create(OperationCodes.EditOpen),
            };

            byte[] bytes = FindingExportWriter.Write(
                FindingExportProbe.Stamped(), [finding], ValidationProfile.Categorized, FindingExportOptions.Default, FindingExportProbe.Instant);
            return ProjectFile.Encoding.GetString(bytes).Split("\r\n").First(l => l.Contains("<finding "));
        }

        // ----- when nothing trails -----

        /// <summary>
        /// A resolved token with no related sites carries none of the three. The common case is 612 of the 618
        /// lines, and it stays as short as it was.
        /// </summary>
        [Test]
        public void AResolvedSingleSiteFindingCarriesNoneOfTheThree()
        {
            string line = Line(new FindingLocation("_0x2132", null, null));

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Not.Contain(" related="));
                Assert.That(line, Does.Not.Contain(" xpath="));
                Assert.That(line, Does.Not.Contain(" related_xpath="));
            });
        }

        // ----- related -----

        /// <summary>Related locators are space-separated, in the order the finding lists them.</summary>
        [Test]
        public void RelatedSitesAreASpaceSeparatedLocatorListInFindingOrder()
        {
            string line = Line(
                new FindingLocation("_0x2132", null, null),
                new FindingLocation("_0x2232", null, "<group> 'Anden'"),
                new FindingLocation("_0x2332", null, "<group> 'Tredje'"));

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.Value(line, "related"), Is.EqualTo("_0x2232 _0x2332"));
                Assert.That(line, Does.Not.Contain("Anden"), "the per-site label is not carried");
            });
        }

        /// <summary>
        /// A tag locator sits in the list exactly like a token. Neither can contain a space, which is what
        /// makes the delimiter safe without any escaping of its own.
        /// </summary>
        [Test]
        public void ATagLocatorIsListedLikeAToken()
        {
            string line = Line(
                new FindingLocation("utcs_project", null, null),
                new FindingLocation("bogus_element", null, null));

            Assert.That(FindingExportProbe.Value(line, "related"), Is.EqualTo("bogus_element"));
        }

        // ----- xpath -----

        /// <summary>An ambiguous primary carries its path; the attribute is the presence bit AND the identity.</summary>
        [Test]
        public void AnAmbiguousPrimaryCarriesItsPath()
        {
            string line = Line(new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[1]"));

            Assert.That(line, Does.Contain(" xpath=\"/utcs_project/groups/group[1]\""));
        }

        /// <summary>A finding with no primary site at all has nothing to point at and carries no path.</summary>
        [Test]
        public void AFindingWithNoPrimarySiteCarriesNoPath()
        {
            string line = Line(null);

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Not.Contain(" locator="));
                Assert.That(line, Does.Not.Contain(" xpath="));
            });
        }

        // ----- related_xpath -----

        /// <summary>
        /// The measured shape: a duplicate-id group whose two sites share one locator and are told apart only
        /// by their paths. <c>related_xpath</c> has the same count as <c>related</c> and pairs with it by
        /// position.
        /// </summary>
        [Test]
        public void RelatedPathsPairPositionallyWithRelatedLocators()
        {
            string line = Line(
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[1]"),
                new FindingLocation("_0x2132", null, "<group> 'Anden'", "/utcs_project/groups/group[2]"));

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.Value(line, "related"), Is.EqualTo("_0x2132"));
                Assert.That(FindingExportProbe.Value(line, "related_xpath"), Is.EqualTo("/utcs_project/groups/group[2]"));
                Assert.That(
                    FindingExportProbe.Value(line, "related_xpath").Split(' '), Has.Length.EqualTo(FindingExportProbe.Value(line, "related").Split(' ').Length),
                    "same count, or the positions mean nothing");
            });
        }

        /// <summary>Order is preserved across both lists, so entry N of one belongs to entry N of the other.</summary>
        [Test]
        public void BothListsKeepTheSameOrder()
        {
            string line = Line(
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[1]"),
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[2]"),
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[4]"));

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.Value(line, "related"), Is.EqualTo("_0x2132 _0x2132"));
                Assert.That(
                    FindingExportProbe.Value(line, "related_xpath"),
                    Is.EqualTo("/utcs_project/groups/group[2] /utcs_project/groups/group[4]"));
            });
        }

        /// <summary>
        /// Related sites that need no path produce no <c>related_xpath</c> at all — the 48 grouped findings
        /// whose sites all resolve stay as short as they were.
        /// </summary>
        [Test]
        public void RelatedSitesThatAllResolveProduceNoRelatedPathList()
        {
            string line = Line(
                new FindingLocation("_0x2132", null, null),
                new FindingLocation("_0x2232", null, null),
                new FindingLocation("_0x2332", null, null));

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Contain(" related=\"_0x2232 _0x2332\""));
                Assert.That(line, Does.Not.Contain(" related_xpath="));
            });
        }

        /// <summary>
        /// The shape the format cannot express: SOME related sites pathed and some not. A positional list with
        /// holes cannot be read back — entry 2 would silently belong to site 3 — and the writer holds no tree,
        /// so it cannot fill the gap either. It emits no list rather than a misaligned one.
        ///
        /// <para>The corpus never produces this: its one pathed related site belongs to a group of exactly one.
        /// The branch exists so that a future rule mixing resolved and ambiguous sites degrades to "no machine
        /// identity for the related sites" instead of to "wrong machine identity".</para>
        /// </summary>
        [Test]
        public void APartiallyPathedGroupEmitsNoRelatedPathListRatherThanAMisalignedOne()
        {
            string line = Line(
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[1]"),
                new FindingLocation("_0x2232", null, null),
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[2]"));

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.Value(line, "related"), Is.EqualTo("_0x2232 _0x2132"), "the locator list is unaffected");
                Assert.That(line, Does.Contain(" xpath="), "the primary's own path is unaffected");
                Assert.That(line, Does.Not.Contain(" related_xpath="));
            });
        }

        /// <summary>A path list without a locator list is impossible: no related sites means neither attribute.</summary>
        [Test]
        public void NoRelatedSitesMeansNeitherRelatedAttribute()
        {
            string line = Line(new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[1]"));

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Not.Contain(" related="));
                Assert.That(line, Does.Not.Contain(" related_xpath="));
                Assert.That(line, Does.Contain(" xpath="), "the primary's path does not depend on related sites");
            });
        }

        // ----- order -----

        /// <summary>
        /// A finding carrying every fixed attribute, emitted in exactly the declared order — the assertion the
        /// per-attribute tests above cannot make individually, and the one that fails if a future attribute is
        /// inserted in the middle.
        /// </summary>
        [Test]
        public void TheThreeTrailingAttributesComeLastInTheDeclaredOrder()
        {
            string line = Line(
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[1]"),
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[2]"));

            Assert.Multiple(() =>
            {
                Assert.That(
                    FindingExportProbe.AttributeNames(line),
                    Is.EqualTo(new[]
                    {
                        "severity", "code", "category", "blocks", "locator", "message",
                        "related", "xpath", "related_xpath",
                    }));
                Assert.That(
                    FindingExportProbe.AttributeNames(line), Is.EqualTo(FindingExportWriter.FixedFindingAttributes),
                    "the writer's own declaration and what it emits must not drift");
            });
        }

        /// <summary>Arguments sit BETWEEN the prose and the trailing three, never after them.</summary>
        [Test]
        public void ArgumentsSitBetweenTheMessageAndTheTrailingAttributes()
        {
            ValidationFinding finding = new(
                new Problem(
                    new ProblemCode("id-duplicate-token"), "Dobbelt id",
                    EquatableArray.CreateRange<ProblemArgument>(
                    [
                        new ProblemArgument("id", "_0x2132"),
                        new ProblemArgument("count", 2),
                    ])),
                ValidationSeverity.Error,
                ValidationCategory.FileIntegrity,
                new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[1]"),
                EquatableArray.CreateRange<FindingLocation>(
                    [new FindingLocation("_0x2132", null, null, "/utcs_project/groups/group[2]")]));

            byte[] bytes = FindingExportWriter.Write(
                FindingExportProbe.Stamped(), [finding], ValidationProfile.Categorized, FindingExportOptions.Default, FindingExportProbe.Instant);
            string line = ProjectFile.Encoding.GetString(bytes).Split("\r\n").First(l => l.Contains("<finding "));

            Assert.That(
                FindingExportProbe.AttributeNames(line),
                Is.EqualTo(new[]
                {
                    "severity", "code", "category", "locator", "message",
                    "arg_id", "arg_count",
                    "related", "xpath", "related_xpath",
                }));
        }
    }
}
