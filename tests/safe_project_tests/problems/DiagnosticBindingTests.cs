using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-4a: a finding's ENGLISH diagnostic is bound from the same arguments as its Danish message.
    ///
    /// <para>A catalogue entry declares two texts over one set of slots — the Danish
    /// <see cref="ProblemCatalogEntry.MessageTemplate"/> a user reads and the English
    /// <see cref="ProblemCatalogEntry.Diagnostic"/> a developer reads in a log. The engine bound only the first,
    /// so any entry whose diagnostic names a slot put a literal <c>{attribute}</c> in the log: the one text
    /// written for the person debugging was the one text that lost its data.</para>
    ///
    /// <para>The corpus is the measurement rather than a hand-picked row, because which entries name a slot in
    /// their diagnostic is a property of the catalogue and changes as rows are authored. Nothing here moves an
    /// oracle: diagnostics are English log text and appear in no recorded report.</para>
    /// </summary>
    [TestFixture]
    public sealed class DiagnosticBindingTests
    {
        /// <summary>Every finding the pinned characterization corpus produces, in production order.</summary>
        private static ImmutableArray<ValidationFinding> CorpusFindings()
        {
            var findings = ImmutableArray.CreateBuilder<ValidationFinding>();
            foreach ((string _, Func<Project> build) in ValidationCharacterizationTests.Corpus)
            {
                findings.AddRange(ProjectRules.Validator.Validate(build(), ValidationProfile.Categorized));
            }

            return findings.ToImmutable();
        }

        [Test]
        public void NoFindingOverTheCorpusCarriesAnUnboundDiagnosticSlot()
        {
            ImmutableArray<ValidationFinding> findings = CorpusFindings();

            string[] unbound =
            [
                .. findings
                    .Where(f => f.Problem.Diagnostic is { } d && d.Contains('{', StringComparison.Ordinal))
                    .Select(f => $"{f.Code.Value}: {f.Problem.Diagnostic}")
                    .Distinct()
                    .OrderBy(line => line, StringComparer.Ordinal)
            ];

            Assert.Multiple(() =>
            {
                Assert.That(findings, Is.Not.Empty, "the corpus must produce findings, or this gate is vacuous");
                Assert.That(unbound, Is.Empty,
                    "these diagnostics reached a log with a slot still spelled as its own placeholder:"
                    + Environment.NewLine + string.Join(Environment.NewLine, unbound));
            });
        }

        /// <summary>
        /// The armed control. A diagnostic that names a slot the finding SUPPLIES must come out carrying the
        /// value — otherwise the assertion above could be satisfied by an engine that stripped braces, or by a
        /// catalogue that happened to declare no slot-bearing diagnostic at all.
        /// </summary>
        [Test]
        public void ADiagnosticNamingASuppliedSlotIsBoundLikeTheMessage()
        {
            ProjectElement terminal = Tree.Node("dataline_input", "_0x2a", []);
            Project project = new(Tree.Node("utcs_project", null, [], [terminal]));
            ProblemCatalogEntry entry = new(
                new ProblemCode("addr-unassigned"), ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing, CatalogDisposition.Warning, RuleKind.UserContentRule,
                RuleFaces.WholeProject, default, FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("address", ProblemArgumentType.AuthoredName)]),
                "Adressen '{address}' mangler")
            {
                Diagnostic = "The terminal at address '{address}' has no assignment.",
            };

            ProblemCatalog catalog = ProblemCatalog.From([entry]);
            RuleSet rules = RuleSet.Create(catalog,
            [
                new RuleBuilder(entry).Inspect(i =>
                    i.Report(terminal, EquatableArray.Create<ProblemArgument>(
                        [new ProblemArgument("address", "1.2.3")]))).Build()
            ]);

            ValidationFinding finding = new WholeProjectValidator(rules)
                .Validate(project, ValidationProfile.ProjectOnly).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Problem.Message, Is.EqualTo("Adressen '1.2.3' mangler"),
                    "the Danish message binds, as it always did");
                Assert.That(finding.Problem.Diagnostic, Is.EqualTo("The terminal at address '1.2.3' has no assignment."),
                    "and the English diagnostic binds from the SAME arguments");
            });
        }
    }
}
