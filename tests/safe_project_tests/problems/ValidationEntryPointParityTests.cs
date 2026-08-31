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

        /// <summary>
        /// The third parity, on a different axis: the FLAT face and the STRUCTURED face are one run seen twice,
        /// so every field they share has to agree.
        ///
        /// <para><b>The field that did not.</b> The flattening dropped
        /// <see cref="ProjectValidationFinding.Diagnostic"/>, so the English sentence read null for everything
        /// the engine produced — and that loss was invisible from the flat side alone, because a null field looks
        /// exactly like a row that has no English text to carry. What it cost is one call further on:
        /// <see cref="ProjectValidationException"/> builds its aggregate items from these findings, so a refused
        /// upload listed every item in Danish alone, with nothing naming which attribute or which tag, on the one
        /// path a developer reads.</para>
        ///
        /// <para><b>Compared by ZIP, not as sets.</b> The flat face is a projection of the structured one, so a
        /// reordering is a defect of the same kind and deserves the same failure. LOCATION is the one thing that
        /// legitimately does not survive — the flat type has a locator where the structured one has sites — so
        /// the locator is compared and the related sites are not.</para>
        /// </summary>
        [TestCase("Project1-SimpelWired.vis")]
        [TestCase("project3-KompleksWired.vis")]
        [TestCase("project5-Dokumentation.vis")]
        [TestCase("Project6-Errors.vis")]
        public void TheFlatFaceKeepsEveryFieldItSharesWithTheStructuredOne(string file)
        {
            Project project = Authentic(file);
            ValidationProfile profile = ValidationProfile.Categorized;

            ProjectValidationFinding[] flat = [.. ProjectVerification.Run(project, profile).Findings];
            ValidationFinding[] structured = [.. ProjectVerification.RunStructured(project, profile).Findings];

            Assert.Multiple(() =>
            {
                Assert.That(flat, Has.Length.EqualTo(structured.Length), "the flattening drops no finding");

                // Without this the zip below would be satisfied by null == null on every row, and the field this
                // test exists for would go untested on a corpus that happens to carry no English text.
                Assert.That(structured.Any(f => f.Problem.Diagnostic is { Length: > 0 }), Is.True,
                    "this file really does produce diagnostic-bearing findings");

                foreach ((ProjectValidationFinding one, ValidationFinding other) in flat.Zip(structured))
                {
                    Assert.That(one.RuleId, Is.EqualTo(other.Code.Value), "the rows are paired in order");
                    Assert.That(one.Severity, Is.EqualTo(other.Severity), one.RuleId);
                    Assert.That(one.Category, Is.EqualTo(other.Category), one.RuleId);
                    Assert.That(one.Locator, Is.EqualTo(other.Primary?.Locator), one.RuleId);
                    Assert.That(one.Message, Is.EqualTo(other.Problem.Message), one.RuleId);
                    Assert.That(one.Diagnostic, Is.EqualTo(other.Problem.Diagnostic), one.RuleId);
                }
            });
        }

        private static string Render(ProjectValidationFinding finding) =>
            $"{finding.Severity}\t{finding.RuleId}\t{finding.Category}\t{finding.Locator}\t{finding.Message}";
    }
}
