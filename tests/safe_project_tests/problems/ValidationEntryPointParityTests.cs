using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The two shipped entry points differ on ONE axis and no other.
    ///
    /// <para><b>The axis they are documented to differ on</b> is the AUDIENCE:
    /// <see cref="ProjectAppService.ValidateCategorized"/> adds the
    /// <see cref="ValidationCategory.Documentation"/> warnings that feed the report appendix, and
    /// <see cref="ProjectAppService.Validate"/> is the pre-serialize structural checklist without them. That is the
    /// whole documented difference, and both docs say <c>IsValid</c> means the same thing in both.</para>
    ///
    /// <para><b>The axis they must NOT differ on</b> is EVALUABILITY. A row that declares a context is skipped when
    /// the caller has none — but the service HAS the library, it is the same field both methods can reach, and a
    /// row whose category is <see cref="ValidationCategory.Logic"/> disappearing from the structural checklist is
    /// not an audience difference. It made the save gate blind to a class of finding the same service reports one
    /// method over.</para>
    /// </summary>
    [TestFixture]
    public sealed class ValidationEntryPointParityTests
    {
        /// <summary>The row that exposed the gap: it declares a library, and its category is not Documentation.</summary>
        private const string LibraryRow = "logic-block-locked-content";

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(TestSetup.Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static IReadOnlyList<string> Rows(ProjectValidationResult result, string ruleId) =>
            [.. result.Findings.Where(f => f.RuleId == ruleId).Select(f => $"{f.Severity}\t{f.Locator}\t{f.Message}")];

        [TestCase("Project1-SimpelWired.vis", 3)]
        [TestCase("project3-KompleksWired.vis", 5)]
        [TestCase("Project6-Errors.vis", 1)]
        public void BothEntryPointsSeeTheLibraryTheServiceHolds(string file, int expected)
        {
            var app = new ProjectAppService(TestSetup.Settings);
            Project project = Authentic(file);

            IReadOnlyList<string> structural = Rows(app.Validate(project), LibraryRow);
            IReadOnlyList<string> categorized = Rows(app.ValidateCategorized(project), LibraryRow);

            Assert.Multiple(() =>
            {
                Assert.That(categorized, Has.Count.EqualTo(expected),
                    "the corpus recording is the non-vacuity guard — this file really does carry the row");
                Assert.That(structural, Is.EqualTo(categorized).AsCollection,
                    $"'{LibraryRow}' is a Logic row, not a Documentation one, so the pre-serialize checklist must "
                    + "see exactly what the categorized run sees; the service holds the library for both");
            });
        }

        /// <summary>
        /// The general form of the same property: the two runs may differ only by
        /// <see cref="ValidationCategory.Documentation"/> rows.
        /// </summary>
        [TestCase("Project1-SimpelWired.vis")]
        [TestCase("project3-KompleksWired.vis")]
        [TestCase("project5-Dokumentation.vis")]
        [TestCase("Project6-Errors.vis")]
        public void TheOnlyDifferenceBetweenTheEntryPointsIsTheDocumentationCategory(string file)
        {
            var app = new ProjectAppService(TestSetup.Settings);
            Project project = Authentic(file);

            string[] structural = [.. app.Validate(project).Findings.Select(Render)];
            string[] categorizedNonDoc =
            [
                .. app.ValidateCategorized(project).Findings
                    .Where(f => f.Category != ValidationCategory.Documentation)
                    .Select(Render),
            ];

            Assert.That(structural, Is.EqualTo(categorizedNonDoc).AsCollection,
                "audience is the ONLY declared difference between the two entry points");
        }

        private static string Render(ProjectValidationFinding finding) =>
            $"{finding.Severity}\t{finding.RuleId}\t{finding.Category}\t{finding.Locator}\t{finding.Message}";
    }
}
