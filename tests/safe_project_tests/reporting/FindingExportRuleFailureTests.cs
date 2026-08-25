using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// What an exported file says when a RULE broke rather than the project.
    ///
    /// <para><b>The engine's choice, carried through.</b> A rule that throws does not abort the pass — it
    /// contributes one <c>internal.unexpected</c> finding and the run continues, because otherwise a project
    /// with a novel shape stops being validated at all and the user gets a clean bill of health produced by a
    /// crash. So the failure has to be representable in the export, and this is what that looks like.</para>
    ///
    /// <para><b>And what it must NOT look like.</b> The English engine sentence — which names the rule and
    /// quotes the exception — lives in <c>Problem.Diagnostic</c>, and the export carries no diagnostic at all.
    /// That is deliberate rather than an omission: an exported file is a user-facing artifact that gets
    /// forwarded to a support case, and an exception message, a stack frame or a build path in it is a leak,
    /// not a courtesy. The Danish label is the whole user-visible text.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingExportRuleFailureTests
    {
        private const string BrokenRuleCode = "aaa-broken-rule";

        private const string ExceptionText = "rule bug at C:\\build\\agent\\_work\\src\\SomeRule.cs line 42";

        private static ProblemCatalogEntry Entry(string code) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning, RuleKind.UserContentRule, RuleFaces.WholeProject, default,
                FindingShape.OnePerOccurrence, default, "Label");

        /// <summary>
        /// Runs a rule set in which one rule throws, and exports whatever the engine produced. Nothing here
        /// hand-builds the failure finding: the point is that the shape the ENGINE makes survives the writer.
        /// </summary>
        private static byte[] ExportAfterAThrowingRule()
        {
            ProjectElement terminal = new("dataline_input", ElementId.ParseOrNull("_0x2a"),
                ImmutableArray.Create((Name: "id", Value: "_0x2a")), EquatableArray<ProjectElement>.Empty);
            Project project = new(new ProjectElement(
                "utcs_project", null,
                ImmutableArray.Create((Name: "id2", Value: "_0x2")),
                EquatableArray.CreateRange<ProjectElement>([terminal])));

            (ProblemCatalogEntry Entry, ProjectInspection Body)[] rules =
            [
                (Entry(BrokenRuleCode), _ => throw new InvalidOperationException(ExceptionText)),
                (Entry("bbb-healthy-rule"), i => i.Report(terminal, default)),
            ];
            ProblemCatalog catalog = ProblemCatalog.From([.. rules.Select(r => r.Entry)]);
            RuleSet set = RuleSet.Create(
                catalog, rules.Select(r => new RuleBuilder(r.Entry).Inspect(r.Body).Build()));

            EquatableArray<ValidationFinding> findings =
                new WholeProjectValidator(set).Validate(project, ValidationProfile.ProjectOnly);

            return FindingExportWriter.Write(
                project, [.. findings], ValidationProfile.ProjectOnly,
                FindingExportOptions.Default with { SourceName = "Fixture.vis" }, FindingExportProbe.Instant);
        }

        private static string FailureLine(byte[] bytes) =>
            FindingExportProbe.Text(bytes).Split("\r\n").Single(l => l.Contains("internal.unexpected"));

        /// <summary>The failure is in the file, as its code and its fixed Danish label.</summary>
        [Test]
        public void AThrownRuleIsExportedAsItsCodeAndItsDanishLabel()
        {
            string line = FailureLine(ExportAfterAThrowingRule());

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Contain(" code=\"internal.unexpected\""));
                Assert.That(line, Does.Contain(" message=\"Uventet fejl\""));
                Assert.That(line, Does.Contain(" severity=\"Error\""), "a broken rule is not an advisory");
            });
        }

        /// <summary>
        /// Nothing of the engine's English detail reaches the bytes: not the exception message, not the id of
        /// the rule that threw, not a stack frame, not a filesystem path. Asserted over the WHOLE document
        /// rather than over the failure line, because a leak into any attribute of any line is the same leak.
        /// </summary>
        [Test]
        public void NoEngineTextReachesTheExportedBytes()
        {
            string text = FindingExportProbe.Text(ExportAfterAThrowingRule());

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Not.Contain("rule bug"), "the exception message");
                Assert.That(text, Does.Not.Contain(BrokenRuleCode), "the id of the rule that threw");
                Assert.That(text, Does.Not.Contain("InvalidOperationException"), "the exception type");
                Assert.That(text, Does.Not.Contain("C:\\"), "a filesystem path");
                Assert.That(text, Does.Not.Contain(".cs"), "a source file name");
                Assert.That(text, Does.Not.Contain("   at "), "a stack frame");
                Assert.That(text, Does.Not.Contain("threw"), "the engine's own sentence");
            });
        }

        /// <summary>
        /// The failure carries no site. It is a fact about the RUN, not about an element — the engine cannot
        /// know which element the rule was looking at when it threw — so there is no locator and no path to
        /// invent one from.
        /// </summary>
        [Test]
        public void AThrownRuleCarriesNoSiteAtAll()
        {
            string line = FailureLine(ExportAfterAThrowingRule());

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Not.Contain(" locator="));
                Assert.That(line, Does.Not.Contain(" xpath="));
                Assert.That(line, Does.Not.Contain(" related="));
            });
        }

        /// <summary>
        /// <c>internal.unexpected</c> has no catalogue row, so it declares no slots and carries no arguments.
        /// This is the branch the argument writer's no-row case exists for, reached through the engine rather
        /// than through a hand-built code.
        /// </summary>
        [Test]
        public void AThrownRuleCarriesNoArgumentAttributes()
        {
            Assert.That(FailureLine(ExportAfterAThrowingRule()), Does.Not.Contain(" arg_"));
        }

        /// <summary>
        /// The non-vacuity guard, and the engine's own contract restated at this boundary: the healthy rule's
        /// finding is in the same file. A test that only checked for absent text would pass just as well on an
        /// export that had aborted entirely.
        /// </summary>
        [Test]
        public void TheRunContinuesSoTheHealthyRulesFindingIsInTheSameFile()
        {
            string text = FindingExportProbe.Text(ExportAfterAThrowingRule());

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain(" code=\"bbb-healthy-rule\""));
                Assert.That(
                    text.Split("\r\n").Count(l => l.Contains("<finding ")), Is.EqualTo(2),
                    "the failure and the healthy finding, and nothing else");
            });
        }
    }
}
