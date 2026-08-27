using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// <c>severities</c>: which tiers the export was allowed to contain.
    ///
    /// <para><b>What it is for.</b> A file with no <c>&lt;finding&gt;</c> has two possible meanings, and the
    /// two files are otherwise byte-identical: the project is CLEAN and every tier was looked at, or NOTHING
    /// was included and the project may be full of errors. This attribute is the only thing in the file that
    /// tells them apart, which is why it is emitted always — including when all three tiers are on, and
    /// including when none is.</para>
    ///
    /// <para><b>Why omitting it when complete would be wrong.</b> Absence would then mean "all", which is an
    /// inference. The whole argument for the attribute is that a completeness caveat must be STATED, not
    /// inferred from what is missing.</para>
    ///
    /// <para><b>It is a different fact from <c>rules_not_run</c>.</b> That one says <i>nobody looked</i>; this
    /// one says <i>the author chose not to include</i>. A reader seeing neither a capacity finding nor an info
    /// finding needs both sentences to know which happened to which.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingExportSeveritiesTests
    {
        private static ValidationFinding Finding(ValidationSeverity severity) =>
            new(
                new Problem(new ProblemCode("struct-locality-empty"), "Tom", EquatableArray<ProblemArgument>.Empty),
                severity,
                ValidationCategory.ProjectStructure,
                new FindingLocation("_0x2132", null, null),
                EquatableArray<FindingLocation>.Empty);

        private static byte[] Export(FindingExportOptions options, params ValidationFinding[] findings) =>
            FindingExportWriter.Write(
                FindingExportProbe.Stamped(), findings, ValidationProfile.Categorized, options, FindingExportProbe.Instant);

        private static string Severities(byte[] bytes) =>
            FindingExportProbe.Text(bytes).Split(" severities=\"")[1].Split('"')[0];

        private static FindingExportOptions Showing(params ValidationSeverity[] tiers) =>
            FindingExportOptions.Default with { SourceName = "s", Severities = [.. tiers] };

        // ----- the three shapes -----

        /// <summary>An unfiltered export names all three tiers, even though it is the complete case.</summary>
        [Test]
        public void AllTiersShownNamesAllThree()
        {
            byte[] bytes = Export(
                FindingExportOptions.Default with { SourceName = "s" },
                Finding(ValidationSeverity.Error),
                Finding(ValidationSeverity.Warning));

            Assert.That(Severities(bytes), Is.EqualTo("Error Warning Info"));
        }

        /// <summary>
        /// Errors only: the attribute names one tier and the file holds no finding of any other. The two
        /// halves are asserted together because the attribute is a claim ABOUT the list — a file naming one
        /// tier while carrying rows of another would be lying, not merely inconsistent.
        /// </summary>
        [Test]
        public void ErrorsOnlyNamesOneTierAndCarriesNoOtherTiersFindings()
        {
            byte[] bytes = Export(
                Showing(ValidationSeverity.Error),
                Finding(ValidationSeverity.Error),
                Finding(ValidationSeverity.Error));

            Assert.Multiple(() =>
            {
                Assert.That(Severities(bytes), Is.EqualTo("Error"));
                Assert.That(FindingExportProbe.FindingLines(bytes), Has.Length.EqualTo(2));
                Assert.That(FindingExportProbe.FindingLines(bytes), Is.All.Contain("severity=\"Error\""));
                Assert.That(FindingExportProbe.Text(bytes), Does.Not.Contain("severity=\"Warning\""));
                Assert.That(FindingExportProbe.Text(bytes), Does.Not.Contain("severity=\"Info\""));
            });
        }

        /// <summary>
        /// No tiers at all: an empty attribute and zero findings. This is a reachable state, not a theoretical
        /// one — the export command is enabled whenever no background update is running, which admits a panel
        /// with every tier switched off.
        /// </summary>
        [Test]
        public void NoTiersShownGivesAnEmptyAttributeAndNoFindings()
        {
            byte[] bytes = Export(Showing());

            Assert.Multiple(() =>
            {
                Assert.That(Severities(bytes), Is.Empty);
                Assert.That(FindingExportProbe.Text(bytes), Does.Contain(" severities=\"\""));
                Assert.That(FindingExportProbe.FindingLines(bytes), Is.Empty);
            });
        }

        // ----- the two empty files -----

        /// <summary>
        /// THE reason the attribute is emitted always. A clean project and a project whose every tier was
        /// hidden both export zero findings; this asserts the two files differ in the severities attribute AND
        /// NOWHERE ELSE, which is what makes that attribute the only thing standing between an empty file and
        /// a reader who reads it as a clean bill of health.
        /// </summary>
        [Test]
        public void ACleanProjectAndAnAllTiersOffProjectDifferOnlyInThatAttribute()
        {
            byte[] clean = Export(FindingExportOptions.Default with { SourceName = "s" });
            byte[] hidden = Export(Showing());

            string cleanText = FindingExportProbe.Text(clean);
            string hiddenText = FindingExportProbe.Text(hidden);

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.FindingLines(clean), Is.Empty);
                Assert.That(FindingExportProbe.FindingLines(hidden), Is.Empty);
                Assert.That(cleanText, Is.Not.EqualTo(hiddenText), "the two files must be distinguishable at all");
                // TWO attributes now, and they move together by construction: with no explicit error-tier
                // split the writer derives @error_tiers from @severities, so a file that excluded Error says
                // so in both places rather than in one and by implication in the other.
                Assert.That(
                    cleanText
                        .Replace(" severities=\"Error Warning Info\"", " severities=\"\"")
                        .Replace(" error_tiers=\"refusing ordinary\"", " error_tiers=\"\""),
                    Is.EqualTo(hiddenText),
                    "and they must differ in NOTHING else");
            });
        }

        // ----- order -----

        /// <summary>
        /// Enum order, never the order the tiers were listed in. A user clicking Info then Error must not
        /// produce a different file from one clicking Error then Info: the attribute states a SET, and a set
        /// rendered in click order would make two identical filters yield two different bytes.
        /// </summary>
        [Test]
        public void ClickOrderDoesNotReorderTheAttribute()
        {
            byte[] clicked = Export(
                Showing(ValidationSeverity.Info, ValidationSeverity.Error, ValidationSeverity.Warning));
            byte[] declared = Export(
                Showing(ValidationSeverity.Error, ValidationSeverity.Warning, ValidationSeverity.Info));

            Assert.Multiple(() =>
            {
                Assert.That(Severities(clicked), Is.EqualTo("Error Warning Info"));
                Assert.That(clicked, Is.EqualTo(declared), "byte-identical, not merely equivalent");
            });
        }

        /// <summary>A two-tier subset keeps enum order too, whichever way round it was supplied.</summary>
        [Test]
        public void ASubsetIsAlsoEmittedInEnumOrder()
        {
            Assert.That(
                Severities(Export(Showing(ValidationSeverity.Info, ValidationSeverity.Error))),
                Is.EqualTo("Error Info"));
        }

        /// <summary>
        /// The order the attribute uses is the ENUM's declaration order, read from the enum rather than
        /// restated — so a fourth tier added tomorrow lands in the right place without this test needing to
        /// know about it.
        /// </summary>
        [Test]
        public void TheOrderIsTheEnumsOwnDeclarationOrder()
        {
            Assert.That(
                Severities(Export(FindingExportOptions.Default with { SourceName = "s" })).Split(' '),
                Is.EqualTo(Enum.GetValues<ValidationSeverity>().Select(s => s.ToString())));
        }

        /// <summary>
        /// The default is every tier: an export nobody filtered claims nothing about exclusion.
        /// <c>AllSeverities</c> is derived from the enum, not typed out, so it cannot fall behind it.
        /// </summary>
        [Test]
        public void TheDefaultOptionsIncludeEveryTierTheEnumDeclares()
        {
            Assert.That(
                FindingExportOptions.Default.Severities,
                Is.EqualTo(Enum.GetValues<ValidationSeverity>()));
        }

        /// <summary>
        /// A tier with no findings is still named. Info is the live case: no catalogue row can declare it yet,
        /// so all 618 corpus findings are Error or Warning — and every oracle nonetheless records that Info
        /// was looked for and found nothing, which is a different statement from not looking.
        /// </summary>
        [Test]
        public void ATierWithNoFindingsIsStillNamed()
        {
            byte[] bytes = Export(
                FindingExportOptions.Default with { SourceName = "s" },
                Finding(ValidationSeverity.Error));

            Assert.Multiple(() =>
            {
                Assert.That(Severities(bytes), Does.Contain("Info"));
                Assert.That(FindingExportProbe.Text(bytes), Does.Not.Contain("severity=\"Info\""));
            });
        }
    }
}
