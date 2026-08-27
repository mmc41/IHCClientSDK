using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// <c>rules_not_run</c>: which rules could not be evaluated, and — the part that matters — which
    /// legitimately-absent rules must NOT be listed there.
    ///
    /// <para><b>Why the attribute exists.</b> Without it, "no capacity findings" reads as <i>the project fits
    /// the controller</i>. It does not: nobody looked. Every export a user produces omits every capacity rule
    /// whose limit comes from the controller, because the service supplies a library port but never controller
    /// limits, so this attribute is
    /// the only thing standing between a partial list and a reader who takes it as exhaustive.</para>
    ///
    /// <para><b>Why the EVALUABILITY axis only.</b> <see cref="ValidationProfile.Includes"/> returns false for
    /// a third reason beyond the two ports — a Structural audience excludes the whole Documentation family.
    /// Deriving the attribute from <c>Includes</c> wholesale would let it silently change meaning from
    /// <i>nobody could look</i> to <i>this audience does not look</i>, which is a different and much weaker
    /// claim. The negative test below is the one that catches that substitution.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingExportRulesNotRunTests
    {
        private static readonly string[] CapacityCodes =
        [
            "capacity-input-addresses",
            "capacity-input-modules",
            "capacity-output-addresses",
            "capacity-output-modules",
            "capacity-resources-high",
            "capacity-scenarios-per-receiver",
            "capacity-wireless-exceeded",
            "capacity-wireless-links-per-unit",
        ];

        /// <summary>
        /// The rows that declare a LIBRARY, in ordinal order — the order the attribute is written in.
        /// <para>
        /// THREE NOW, ASKING THREE DIFFERENT QUESTIONS OF THE SAME PORT: whether the library holds a block's
        /// TYPE at all, at which VERSIONS it holds it, and how its BODY compares with the entry the block
        /// claims. None is answerable without a library, which is what the declaration means.
        /// </para>
        /// </summary>
        private static readonly string[] LibraryCodes =
            ["fb-master-missing-from-library", "fb-master-version-differs", "logic-block-locked-content"];

        /// <summary>A library that holds nothing. The rule's evaluability turns on the PORT, not on its answers.</summary>
        private sealed class EmptyLibrary : ILibraryBlockSource
        {
            public bool TryGetBody(string masterType, string masterVersion, out ProjectElement body)
            {
                body = null!;
                return false;
            }

            public bool TryGetVersions(string masterType, out EquatableArray<string> versions)
            {
                versions = EquatableArray<string>.Empty;
                return false;
            }
        }

        /// <summary>The codes the export names as not run, in emitted order.</summary>
        private static string[] NotRun(ValidationProfile profile)
        {
            byte[] bytes = FindingExportWriter.Write(
                FindingExportProbe.Stamped(), [], profile, FindingExportOptions.Default, FindingExportProbe.Instant);
            string value = ProjectFile.Encoding.GetString(bytes)
                .Split(" rules_not_run=\"")[1].Split('"')[0];
            return value.Length == 0 ? [] : value.Split(' ');
        }

        // ----- the two ports, each on its own -----

        /// <summary>
        /// The production shape: a library supplied, controller limits not. Exactly the six capacity codes,
        /// ordinal-sorted so a catalogue reordering cannot move the bytes.
        /// </summary>
        [Test]
        public void WithALibraryButNoControllerLimitsExactlyTheSixCapacityCodesAreNamed()
        {
            string[] notRun = NotRun(ValidationProfile.Categorized with { Library = new EmptyLibrary() });

            Assert.Multiple(() =>
            {
                Assert.That(notRun, Is.EqualTo(CapacityCodes));
                Assert.That(
                    notRun, Is.Ordered.Using<string>(StringComparer.Ordinal),
                    "ordinal order, so reordering the catalogue does not move the bytes");
            });
        }

        /// <summary>The mirror case: controller limits supplied, no library. Only the library-gated rules.</summary>
        [Test]
        public void WithControllerLimitsButNoLibraryOnlyTheLibraryGatedCodesAreNamed()
        {
            string[] notRun = NotRun(
                ValidationProfile.Categorized with { Controller = ControllerCapabilityLimits.VendorDocumented });

            Assert.That(notRun, Is.EqualTo(LibraryCodes));
        }

        /// <summary>Neither port: every context-declaring row, the capacity ones plus the library ones.</summary>
        [Test]
        public void WithNeitherPortAllContextDeclaringCodesAreNamed()
        {
            string[] notRun = NotRun(ValidationProfile.Categorized);

            Assert.That(
                notRun, Is.EqualTo(CapacityCodes.Concat(LibraryCodes).OrderBy(c => c, StringComparer.Ordinal)),
                "both sets, merged into one ordinal sequence — the attribute is sorted, not concatenated");
        }

        /// <summary>
        /// Both ports supplied: every rule could be evaluated, and the attribute is EMPTY rather than absent.
        /// A missing attribute would read as "this file predates the caveat"; an empty one says "nothing was
        /// skipped", which is a fact worth stating.
        /// </summary>
        [Test]
        public void WithBothPortsTheAttributeIsPresentAndEmpty()
        {
            ValidationProfile complete = ValidationProfile.Categorized with
            {
                Library = new EmptyLibrary(),
                Controller = ControllerCapabilityLimits.VendorDocumented,
            };

            byte[] bytes = FindingExportWriter.Write(FindingExportProbe.Stamped(), [], complete, FindingExportOptions.Default, FindingExportProbe.Instant);

            Assert.Multiple(() =>
            {
                Assert.That(NotRun(complete), Is.Empty);
                Assert.That(ProjectFile.Encoding.GetString(bytes), Does.Contain(" rules_not_run=\"\""));
            });
        }

        // ----- the audience axis must not leak in -----

        /// <summary>
        /// THE test this task exists for. A Structural-audience profile excludes the entire Documentation
        /// family — deliberately, because that audience does not want it — and those rules were perfectly
        /// evaluable. If <c>rules_not_run</c> were derived from <see cref="ValidationProfile.Includes"/>, the
        /// attribute would swell from six codes to dozens and quietly change what it claims.
        ///
        /// <para>The non-vacuity guard matters here: the assertion is only meaningful if the corpus actually
        /// HAS Documentation rows to leak, so their count is asserted to be substantial first.</para>
        /// </summary>
        [Test]
        public void AStructuralAudienceDoesNotPutTheDocumentationFamilyIntoTheAttribute()
        {
            ImmutableArray<string> documentationCodes =
            [
                .. ProblemCatalog.Current.Entries
                    .Where(e => e.Category == ValidationCategory.Documentation)
                    .Select(e => e.Code.Value),
            ];
            ValidationProfile structural = ValidationProfile.ProjectOnly with { Library = new EmptyLibrary() };

            Assert.Multiple(() =>
            {
                Assert.That(
                    documentationCodes, Has.Length.GreaterThan(10),
                    "precondition: there is a Documentation family large enough for its leak to be visible");
                Assert.That(
                    ValidationProfile.ProjectOnly.Audience, Is.EqualTo(ProfileAudience.Structural),
                    "precondition: this profile really is the one that excludes them");
                Assert.That(
                    documentationCodes.Where(c => ValidationProfile.ProjectOnly.Includes(
                        ProblemCatalog.Current.Entries.First(e => e.Code.Value == c))),
                    Is.Empty,
                    "precondition: Includes() really does drop every one of them");

                Assert.That(
                    NotRun(structural), Is.EqualTo(CapacityCodes),
                    "not one Documentation code, because none of them was UNEVALUABLE");
            });
        }

        // ----- the structural half -----

        /// <summary>
        /// The same fact stated about the catalogue rather than about a file: exactly these rows declare
        /// controller limits and exactly these declare a library, and the controller ones are precisely the
        /// capacity codes. This is what makes the attribute's contents a consequence of the catalogue rather
        /// than a list someone maintains beside it.
        /// </summary>
        [Test]
        public void ExactlyTheseCatalogueRowsDeclareAContextAndTheControllerOnesAreTheCapacityCodes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    ProblemCatalog.Current.Entries
                        .Where(e => e.RequiresControllerLimits)
                        .Select(e => e.Code.Value)
                        .OrderBy(c => c, StringComparer.Ordinal),
                    Is.EqualTo(CapacityCodes));
                Assert.That(
                    ProblemCatalog.Current.Entries
                        .Where(e => e.RequiresLibrary)
                        .Select(e => e.Code.Value)
                        .OrderBy(c => c, StringComparer.Ordinal),
                    Is.EqualTo(LibraryCodes));
            });
        }

        /// <summary>
        /// And the consequence for the profile a real export runs under: the service's categorized profile
        /// supplies a library and never controller limits, so it excludes exactly the six — which is why every
        /// file a user produces names those six and nothing else.
        /// </summary>
        [Test]
        public void TheProfileARealExportRunsUnderExcludesExactlyTheSixCapacityRows()
        {
            ValidationProfile asTheServiceBuildsIt =
                ValidationProfile.Categorized with { Library = new EmptyLibrary() };

            Assert.That(
                ProblemCatalog.Current.Entries
                    .Where(e => e.Section == ProblemCatalogSection.ProjectFindings)
                    .Where(e => !asTheServiceBuildsIt.Includes(e))
                    .Select(e => e.Code.Value)
                    .OrderBy(c => c, StringComparer.Ordinal),
                Is.EqualTo(CapacityCodes));
        }
    }
}
