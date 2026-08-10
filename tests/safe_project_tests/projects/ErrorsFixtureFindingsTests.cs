using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The finding-catalogue gate for <c>Project6-Errors.vis</c> — the vendor-authored fixture that carries a
    /// deliberate instance of every <em>non-fatal</em> condition in
    /// <c>applications/ihc_openvisual/docs/error-list.md</c> that IHC Visual will let a user author, plus the
    /// deliberate non-findings that must stay silent.
    ///
    /// Two things are pinned here, and they are different in kind:
    ///
    /// <list type="bullet">
    /// <item><description><b>Structural silence.</b> Every condition in the fixture is user-sourced and non-fatal
    /// by construction, so the structural checklist (ids, IDREFs, bijections, FB shape, schema) must report
    /// <em>nothing</em>. A structural finding here means the fixture drifted into file-level damage — the one
    /// thing a vendor-authored oracle can never legitimately contain.</description></item>
    /// <item><description><b>Documentation completeness.</b> The eight implemented US-072 checks must fire exactly
    /// where the fixture was authored to provoke them, and — the part that actually catches over-reporting — must
    /// stay off the issue-free control product.</description></item>
    /// </list>
    ///
    /// The remaining ~70 catalogue rows are not asserted: the SDK does not implement them yet. This fixture is the
    /// oracle they will be built against, so a row moving from "unimplemented" to "implemented" should add its
    /// assertion here rather than a new fixture.
    /// </summary>
    public class ErrorsFixtureFindingsTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private const string Fixture = "projects/Project6-Errors.vis";

        /// <summary>The issue-free control: every documentation field filled, addressed, coloured, named.</summary>
        private const string CleanProduct = "Lampeudtag";

        private static Project Load()
        {
            using var ms = new MemoryStream(TestData.ReadBytes(Fixture));
            return new ProjectAppService(Settings).Load(ms).GetAwaiter().GetResult();
        }

        private static ProjectValidationResult Validated() =>
            new ProjectAppService(Settings).ValidateCategorized(Load());

        [Test]
        public void Fixture_HasNoStructuralFindings()
        {
            var structural = Validated().Findings
                .Where(f => f.Category == ValidationCategory.Structural)
                .ToArray();

            Assert.That(structural, Is.Empty,
                "Project6-Errors.vis carries only user-sourced, non-fatal conditions, so the structural checklist "
                + "must stay silent. Reported: "
                + string.Join(" | ", structural.Select(f => f.ToString())));
        }

        [Test]
        public void Fixture_IsValid_BecauseEveryConditionIsNonFatal()
        {
            Assert.That(Validated().IsValid, Is.True,
                "No condition in this fixture may block a save — every row it witnesses is an advisory or a "
                + "user-sourced error that still serializes.");
        }

        /// <summary>The eight documentation checks implemented today, and how often each fires on this fixture.</summary>
        private static readonly string[] ImplementedDocumentationRules =
        [
            "doc-documentation-tag", "doc-power-group", "doc-cabletype", "doc-cablenumber",
            "doc-position", "doc-not-linked", "doc-cable-colour", "doc-address",
        ];

        /// <summary>
        /// The eight implemented documentation checks all fire, and fire exactly as often as the fixture was
        /// authored to provoke them. Authored gaps: the five product-level fields are blank on
        /// <c>LK FUGA Tryk 4 tast 2 dioder</c>; one of its terminals is unaddressed, one carries no wire
        /// colour, and one owns no link.
        ///
        /// <para>The counts are pinned rather than merely "not empty", because "not empty" is satisfied by both
        /// failure modes that matter: a check that collapsed onto a single element, and one that fanned out over
        /// every element in the project. Note the two different units — the five product-level rules count
        /// <em>products</em> (a six-button product carries one missing Id-kode, not six), while the three
        /// terminal-level rules count <em>terminals</em>. 44 findings in total.</para>
        ///
        /// <para>Independently corroborated: the third-party <c>jemi.dk/ihc/docs</c> reporter, run over this same
        /// fixture, names the same elements for all eight kinds and reports no ninth kind. Its own totals are
        /// larger on the five product-level rules only because it repeats them under each terminal — see the
        /// appendix of <c>error-list.md</c>. That tool is unofficial and has no severity model, so it corroborates
        /// <em>detection</em> only; the numbers below are this implementation's and remain the thing to defend.</para>
        /// </summary>
        [TestCase("doc-documentation-tag", 4)]
        [TestCase("doc-power-group", 4)]
        [TestCase("doc-cabletype", 4)]
        [TestCase("doc-cablenumber", 5)]
        [TestCase("doc-position", 4)]
        [TestCase("doc-not-linked", 10)]
        [TestCase("doc-cable-colour", 8)]
        [TestCase("doc-address", 5)]
        public void Fixture_ReportsDocumentationRule(string ruleId, int expectedCount)
        {
            var findings = Validated().Findings.Where(f => f.RuleId == ruleId).ToArray();

            Assert.That(findings, Is.Not.Empty, $"'{ruleId}' has a deliberate witness in the fixture but did not fire.");
            Assert.That(findings, Has.Length.EqualTo(expectedCount),
                $"'{ruleId}' fires on a fixed, authored set of elements. Reported: "
                + string.Join(" | ", findings.Select(f => f.Locator)));
            Assert.That(findings.All(f => f.Category == ValidationCategory.Documentation), Is.True,
                $"'{ruleId}' is a documentation check, not a structural one.");
            Assert.That(findings.All(f => f.Severity == ValidationSeverity.Warning), Is.True,
                $"'{ruleId}' is advisory — the user judges it, so it must never be an Error.");
        }

        /// <summary>
        /// No documentation check beyond the implemented eight says anything about this fixture. This is the scope
        /// guard: when a catalogue row moves from unimplemented to implemented, it surfaces here first as a failure
        /// naming the new id — which is the prompt to give it a counted assertion above and a witness in the
        /// authoring record, rather than letting it appear unnoticed and un-mapped.
        /// </summary>
        [Test]
        public void Fixture_ReportsNoDocumentationRuleBeyondTheImplementedEight()
        {
            string[] unexpected = Validated().Findings
                .Where(f => f.Category == ValidationCategory.Documentation)
                .Select(f => f.RuleId)
                .Distinct()
                .Where(id => !ImplementedDocumentationRules.Contains(id))
                .OrderBy(id => id)
                .ToArray();

            Assert.That(unexpected, Is.Empty,
                "A documentation rule fired that this fixture does not yet account for: "
                + string.Join(", ", unexpected)
                + ". Add its expected count to Fixture_ReportsDocumentationRule and record its witness in "
                + "Project6-Errors.md.");
        }

        /// <summary>
        /// The over-reporting guard. The control product has every documentation field filled and its terminal
        /// addressed, coloured and linked, so no documentation finding may name it. Without this, a check that
        /// fires on everything would pass every test above.
        /// </summary>
        [Test]
        public void Fixture_CleanControlProduct_ProducesNoDocumentationFinding()
        {
            Project project = Load();
            var result = new ProjectAppService(Settings).ValidateCategorized(project);

            // A finding names its subject by that element's raw `id` attribute (FindingCollector.Locate),
            // so the control's own id plus every id beneath it is the set that must never appear.
            string[] cleanIds = project.Root.DescendantsAndSelf()
                .Where(e => e.GetAttribute("name") == CleanProduct)
                .SelectMany(e => e.DescendantsAndSelf())
                .Select(e => e.GetAttribute("id"))
                .Where(id => id is not null)
                .Distinct()
                .ToArray()!;

            Assert.That(cleanIds, Is.Not.Empty, $"The control product '{CleanProduct}' is missing from the fixture.");

            var onClean = result.Findings
                .Where(f => f.Locator is not null && cleanIds.Contains(f.Locator))
                .ToArray();

            Assert.That(onClean, Is.Empty,
                $"'{CleanProduct}' is the issue-free control and must appear in no finding. Reported: "
                + string.Join(" | ", onClean.Select(f => f.ToString())));
        }
    }
}
