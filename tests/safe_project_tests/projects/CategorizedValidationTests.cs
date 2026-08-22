using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// R10/T002: the unified categorized verification API. The eight US-072 documentation-completeness
    /// checks live in <c>Ihc.Vis.Validation</c> as Category=Documentation / Severity=Warning findings behind
    /// the facade's <see cref="ProjectAppService.ValidateCategorized"/> entry, while the existing
    /// <see cref="ProjectAppService.Validate"/> keeps returning exactly the structural checklist. Ordering
    /// (document-scan order of the subject element, then the fixed per-element check order) and
    /// all-eight-checks-fire are pinned over the <c>project5-Dokumentation.vis</c> fixture — the order the
    /// vendor-witnessed "Fejl i dokumentation" appendix oracle shows.
    /// </summary>
    public class CategorizedValidationTests
    {
        private static ProjectAppService App() => new(TestSetup.Settings);

        private static Project Load(string name) =>
            App().Load(new MemoryStream(TestData.ReadBytes(Path.Combine("projects", name)))).GetAwaiter().GetResult();

        [Test]
        public void ValidateCategorized_DocumentationFindingsAreWarnings_AndNeverAffectValidity()
        {
            ProjectAppService app = App();
            Project project = Load("project5-Dokumentation.vis");

            ProjectValidationResult structural = app.Validate(project);
            ProjectValidationResult categorized = app.ValidateCategorized(project);

            ImmutableArray<ProjectValidationFinding> documentation = categorized.Findings
                .Where(f => f.Category == ValidationCategory.Documentation)
                .ToImmutableArray();
            Assert.Multiple(() =>
            {
                Assert.That(documentation, Is.Not.Empty, "project5 carries known documentation gaps");
                Assert.That(documentation.All(f => f.Severity == ValidationSeverity.Warning), Is.True,
                    "documentation findings are advisory warnings by contract");
                Assert.That(categorized.IsValid, Is.EqualTo(structural.IsValid),
                    "documentation findings never affect IsValid");
                Assert.That(categorized.Errors, Is.EqualTo(structural.Errors),
                    "documentation findings never appear in Errors");
                Assert.That(categorized.Warnings, Is.SupersetOf(documentation.Select(f => f.Message)),
                    "documentation labels surface as Warnings");
            });
        }

        [Test]
        public void Validate_KeepsReturningOnlyStructuralFindings()
        {
            ProjectValidationResult structural = App().Validate(Load("project5-Dokumentation.vis"));

            Assert.That(structural.Findings.All(f => f.Category == ValidationCategory.Structural), Is.True,
                "the existing Validate surface returns exactly today's structural checklist — no documentation findings");
        }

        [Test]
        public void ValidateCategorized_OrderAndRuleIds_PinnedOverProject5_AllEightChecksFire()
        {
            ProjectValidationResult categorized = App().ValidateCategorized(Load("project5-Dokumentation.vis"));

            ImmutableArray<(string RuleId, string? Locator, string Message)> documentation = categorized.Findings
                .Where(f => f.Category == ValidationCategory.Documentation)
                .Select(f => (f.RuleId, f.Locator, f.Message))
                .ToImmutableArray();

            var expected = new (string RuleId, string? Locator, string Message)[]
            {
                ("doc-documentation-tag", "_0x5a53", "Mangler Id-kode"),
                ("doc-cable-colour", "_0x5d5a", "Mangler Ledningsfarve"),
                ("doc-not-linked", "_0x5e5a", "Ikke forbundet"),
                ("doc-cable-colour", "_0x5e5a", "Mangler Ledningsfarve"),
                ("doc-address", "_0x5e5a", "Mangler Adresse"),
                ("doc-cabletype", "_0x6453", "Mangler Kabeltype"),
                ("doc-cablenumber", "_0x6453", "Mangler Kabelnummer"),
                ("doc-power-group", "_0x6653", "Mangler Lysgruppe"),
                ("doc-position", "_0x6653", "Mangler Placering"),
                // The sensor's own terminal, which sits inside its product's `settings` container: it is
                // self-closed, hence unlinked. Reached only since the checks were widened to the report
                // body's descent scope (RL-1/G5) — before that, a whole product family's terminals were
                // silently exempt from every terminal-level check.
                ("doc-not-linked", "_0x705a", "Ikke forbundet"),
            };
            Assert.Multiple(() =>
            {
                Assert.That(documentation, Is.EqualTo(expected),
                    "document-scan order of the subject element, then the fixed per-element check order (the vendor-witnessed appendix order)");
                Assert.That(documentation.Select(f => f.RuleId).Distinct().Count(), Is.EqualTo(8),
                    "all eight US-072 checks fire over project5");
            });
        }
    }
}
