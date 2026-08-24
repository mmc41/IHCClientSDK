using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// THE FINDINGS FACE — the collect-all whole-project executor.
    ///
    /// <para><b>The property this face is gated on is DETERMINISM.</b> Output order must be the same for the same
    /// project and independent of the order rules were registered in. That is not a nicety: an engine whose output
    /// order depends on composition produces a diff on every run, makes a recorded oracle worthless, and hides a
    /// dropped finding behind a reshuffle. Order is document-scan order, decided by the executor — which is why
    /// the rule-facing interface offers a rule no way to influence it.</para>
    ///
    /// <para><b>A rule that throws does not abort the pass.</b> It contributes one <c>internal.unexpected</c>
    /// finding carrying the exception as its English diagnostic, and the run continues. The alternative is worse
    /// than it looks: a project with a novel shape would stop being validated at all, and the user would get a
    /// clean bill of health produced by a crash. The rethrow policy exists for diagnostic runs, where a swallowed
    /// bug is the worse outcome.</para>
    /// </summary>
    [TestFixture]
    public sealed class WholeProjectValidatorTests
    {
        private static ProblemCatalogEntry Entry(
            string code,
            ValidationCategory category = ValidationCategory.Addressing,
            CatalogDisposition disposition = CatalogDisposition.Warning,
            string template = "Label",
            bool needsController = false) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, category, disposition,
                RuleKind.UserContentRule, RuleFaces.WholeProject, default, FindingShape.OnePerOccurrence,
                default, template)
            {
                RequiresControllerLimits = needsController,
            };

        /// <summary>A declarative entry over a real DTD target, so registration accepts it, at the given faces.</summary>
        private static ProblemCatalogEntry ConstraintEntry(string code, RuleFaces faces) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, ValidationCategory.Addressing,
                CatalogDisposition.Warning, RuleKind.UserContentRule, faces,
                new RuleTarget("sms_modem_phonenumber", "phonenumber"), FindingShape.OnePerOccurrence,
                default, "Label");

        /// <summary>A constraint that refuses every value, so the only thing deciding whether it reports is the face filter.</summary>
        private sealed class AlwaysFails(ProblemCode code) : IValueConstraint
        {
            public ProblemCode Code => code;

            public ValueConstraintVerdict Check(string? rawValue) =>
                ValueConstraintVerdict.Failed(EquatableArray<ProblemArgument>.Empty);

            public FieldConstraintMetadata Describe() => FieldConstraintMetadata.Unconstrained;
        }

        private static Project ProjectWith(params ProjectElement[] children) =>
            new(Tree.Node("utcs_project", null, [], children));

        private static (ProblemCatalog Catalog, RuleSet Rules) Compose(
            IEnumerable<(ProblemCatalogEntry Entry, ProjectInspection Body)> rules)
        {
            (ProblemCatalogEntry Entry, ProjectInspection Body)[] materialized = [.. rules];
            ProblemCatalog catalog = ProblemCatalog.From(
                materialized.Select(r => r.Entry).ToImmutableArray());
            RuleSet set = RuleSet.Create(catalog,
                materialized.Select(r => new RuleBuilder(r.Entry).Inspect(r.Body).Build()));
            return (catalog, set);
        }

        [Test]
        public void AFindingCarriesItsProblemItsSeverityItsCategoryAndItsLocation()
        {
            ProjectElement terminal = Tree.Node("dataline_input", "_0x2a", []);
            Project project = ProjectWith(terminal);
            ProblemCatalogEntry entry = Entry("addr-unassigned", template: "Adresse mangler");

            WholeProjectValidator validator = new(Compose([(entry, i => i.Report(terminal, default))]).Rules);
            ValidationFinding finding = validator.Validate(project, ValidationProfile.ProjectOnly).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Code.Value, Is.EqualTo("addr-unassigned"));
                Assert.That(finding.Problem.Message, Is.EqualTo("Adresse mangler"), "bound from the entry's template");
                Assert.That(finding.Severity, Is.EqualTo(ValidationSeverity.Warning), "the entry's disposition");
                Assert.That(finding.Category, Is.EqualTo(ValidationCategory.Addressing));
                Assert.That(finding.Primary!.Locator, Is.EqualTo("_0x2a"));
                Assert.That(finding.Primary.Element, Is.Not.Null, "the token parses, so the id is carried too");
            });
        }

        /// <summary>
        /// THE GATE. The same rules registered in every possible order must produce byte-identical output.
        /// </summary>
        [Test]
        public void OutputIsIdenticalHoweverTheRulesWereRegistered()
        {
            ProjectElement first = Tree.Node("dataline_input", "_0x10", []);
            ProjectElement second = Tree.Node("dataline_input", "_0x20", []);
            ProjectElement third = Tree.Node("dataline_output", "_0x30", []);
            Project project = ProjectWith(first, second, third);

            (ProblemCatalogEntry Entry, ProjectInspection Body)[] rules =
            [
                (Entry("zzz-late"), i => { i.Report(third, default); i.Report(first, default); }),
                (Entry("aaa-early"), i => i.Report(second, default)),
                (Entry("mmm-middle"), i => { i.Report(first, default); i.Report(third, default); }),
            ];

            List<string[]> runs = [];
            foreach (IEnumerable<(ProblemCatalogEntry, ProjectInspection)> order in Permutations(rules))
            {
                WholeProjectValidator validator = new(Compose(order).Rules);
                runs.Add([.. validator.Validate(project, ValidationProfile.ProjectOnly)
                    .Select(f => $"{f.Code.Value}@{f.Primary?.Locator}")]);
            }

            Assert.Multiple(() =>
            {
                Assert.That(runs, Has.Count.EqualTo(6), "all six registration orders were tried");
                foreach (string[] run in runs)
                {
                    Assert.That(run, Is.EqualTo(runs[0]).AsCollection, string.Join(", ", run));
                }

                // And the order is DOCUMENT order, not code order or emission order.
                Assert.That(runs[0], Is.EqualTo(new[]
                {
                    "mmm-middle@_0x10", "zzz-late@_0x10", "aaa-early@_0x20", "mmm-middle@_0x30", "zzz-late@_0x30",
                }).AsCollection);
            });
        }

        [Test]
        public void ARuleThatThrowsCostsItsOwnResultAndNotTheRun()
        {
            ProjectElement terminal = Tree.Node("dataline_input", "_0x2a", []);
            Project project = ProjectWith(terminal);

            WholeProjectValidator validator = new(Compose(
            [
                (Entry("aaa-broken"), _ => throw new InvalidOperationException("rule bug")),
                (Entry("bbb-healthy"), i => i.Report(terminal, default)),
            ]).Rules);

            ValidationFinding[] findings = [.. validator.Validate(project, ValidationProfile.ProjectOnly)];

            Assert.Multiple(() =>
            {
                Assert.That(findings, Has.Length.EqualTo(2), "the healthy rule still reported");
                Assert.That(findings.Select(f => f.Code.Value), Does.Contain("bbb-healthy"));

                ValidationFinding failure = findings.Single(f => f.Code.Value == "internal.unexpected");
                Assert.That(failure.Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(failure.Problem.Diagnostic, Does.Contain("aaa-broken").And.Contain("rule bug"),
                    "the English diagnostic names the rule and the exception; the user sees the fixed Danish label");
                Assert.That(failure.Problem.Message, Is.EqualTo("Uventet fejl"));
                Assert.That(failure.Problem.Cause, Is.TypeOf<InvalidOperationException>());
            });
        }

        [Test]
        public void TheRethrowPolicyAbortsThePassForDiagnosticRuns()
        {
            Project project = ProjectWith(Tree.Node("dataline_input", "_0x2a", []));
            WholeProjectValidator validator = new(Compose(
                [(Entry("aaa-broken"), _ => throw new InvalidOperationException("rule bug"))]).Rules);

            ValidationProfile diagnostic = ValidationProfile.ProjectOnly with { FailurePolicy = RuleFailurePolicy.Rethrow };

            Assert.That(() => validator.Validate(project, diagnostic),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("rule bug"));
        }

        /// <summary>
        /// The profile's two axes, exercised. AUDIENCE decides whether documentation findings are in scope;
        /// EVALUABILITY keeps a rule that depends on the target machine out of a project-only run entirely — it
        /// does not run and does not report, rather than reporting against a guess.
        /// </summary>
        [Test]
        public void TheProfileSelectsOnAudienceAndOnEvaluability()
        {
            ProjectElement terminal = Tree.Node("dataline_input", "_0x2a", []);
            Project project = ProjectWith(terminal);

            WholeProjectValidator validator = new(Compose(
            [
                (Entry("addr-unassigned"), i => i.Report(terminal, default)),
                (Entry("doc-cabletype", ValidationCategory.Documentation), i => i.Report(terminal, default)),
                (Entry("capacity-wireless-exceeded", ValidationCategory.ProjectStructure, needsController: true),
                    i => i.Report(terminal, default)),
            ]).Rules);

            string[] structural = [.. validator.Validate(project, ValidationProfile.ProjectOnly).Select(f => f.Code.Value)];
            string[] categorized = [.. validator.Validate(project, ValidationProfile.Categorized).Select(f => f.Code.Value)];
            string[] withController = [.. validator
                .Validate(project, ValidationProfile.Categorized with { Controller = ControllerCapabilityLimits.VendorDocumented })
                .Select(f => f.Code.Value)];

            Assert.Multiple(() =>
            {
                Assert.That(structural, Is.EqualTo(new[] { "addr-unassigned" }).AsCollection);
                Assert.That(categorized, Is.EqualTo(new[] { "addr-unassigned", "doc-cabletype" }).AsCollection);
                Assert.That(withController, Does.Contain("capacity-wireless-exceeded"),
                    "supplying the limits is what brings the rule into scope");
                Assert.That(categorized, Does.Not.Contain("capacity-wireless-exceeded"),
                    "absent, not evaluated-against-a-default: the same project must not be valid on one "
                    + "workstation and invalid on another");
            });
        }

        [Test]
        public void APerRuleSeverityOverrideIsTheOneStrictnessLever()
        {
            ProjectElement terminal = Tree.Node("dataline_input", "_0x2a", []);
            Project project = ProjectWith(terminal);
            ProblemCatalogEntry entry = Entry("addr-unassigned");

            WholeProjectValidator validator = new(Compose([(entry, i => i.Report(terminal, default))]).Rules);
            ValidationProfile strict = ValidationProfile.ProjectOnly with
            {
                Overrides = EquatableArray.Create<SeverityOverride>(
                    [new SeverityOverride(entry.Code, ValidationSeverity.Error)]),
            };

            Assert.Multiple(() =>
            {
                Assert.That(validator.Validate(project, ValidationProfile.ProjectOnly).Single().Severity,
                    Is.EqualTo(ValidationSeverity.Warning));
                Assert.That(validator.Validate(project, strict).Single().Severity,
                    Is.EqualTo(ValidationSeverity.Error));
            });
        }

        /// <summary>
        /// A collision reports once with its related sites, rather than once per site — the shape decision made
        /// operational.
        /// </summary>
        [Test]
        public void AGroupedReportBecomesOneFindingCarryingItsRelatedSites()
        {
            ProjectElement first = Tree.Node("dataline_input", "_0x2a", []);
            ProjectElement second = Tree.Node("dataline_output", "_0x2a", []);
            Project project = ProjectWith(first, second);
            // PrimaryWithRelated, because this rule reports a GROUP: the engine now refuses an emission that
            // contradicts the declared shape, so a fixture may not declare one shape and emit the other either.
            ProblemCatalogEntry entry = Entry("id-duplicate-token", ValidationCategory.FileIntegrity,
                CatalogDisposition.Error, "Dobbelt id") with { Shape = FindingShape.PrimaryWithRelated };

            WholeProjectValidator validator = new(Compose(
                [(entry, i => i.ReportGroup(first, EquatableArray.Create<ProjectElement>([second]), default))]).Rules);

            ValidationFinding finding = validator.Validate(project, ValidationProfile.ProjectOnly).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Primary!.Locator, Is.EqualTo("_0x2a"));
                Assert.That(finding.Related, Has.Length.EqualTo(1));
                Assert.That(finding.Related[0].Locator, Is.EqualTo("_0x2a"));
            });
        }

        /// <summary>
        /// Two structurally identical siblings are EQUAL as records but are different sites. Document order has to
        /// tell them apart, or a finding on the second silently sorts to the first's position.
        /// </summary>
        [Test]
        public void IdenticalSiblingsAreDistinctSitesInDocumentOrder()
        {
            ProjectElement first = Tree.Node("dataline_input", null, [("name", "same")]);
            ProjectElement second = Tree.Node("dataline_input", null, [("name", "same")]);
            Project project = ProjectWith(first, Tree.Node("marker", "_0xff", []), second);

            WholeProjectValidator validator = new(Compose(
            [
                (Entry("aaa-first"), i => i.Report(first, default)),
                (Entry("bbb-second"), i => i.Report(second, default)),
                (Entry("mmm-marker"), i => i.Report(project.Root.Children[1], default)),
            ]).Rules);

            Assert.That(validator.Validate(project, ValidationProfile.ProjectOnly).Select(f => f.Code.Value),
                Is.EqualTo(new[] { "aaa-first", "mmm-marker", "bbb-second" }).AsCollection,
                "the second sibling sorts AFTER the marker between them, so identity decided the order");
        }

        /// <summary>
        /// A face declaration is not decoration: a rule that declares only <see cref="RuleFaces.DialogMetadata"/>
        /// answers a dialog's "what would be acceptable?" and must NOT put a finding in the project report.
        /// <para>Registration cannot be what enforces this. It accepts the entry — a constraint serving one face
        /// is legal, and only a TRAVERSAL is required to declare the whole-project face
        /// (<c>TraversalCannotServeFace</c>) — so the executor is the single place that can honour the
        /// declaration. Before it did, a dialog-only row emitted project findings and its face declaration meant
        /// nothing.</para>
        /// <para>The control is the second rule: same element, same attribute, same always-failing constraint,
        /// differing ONLY in the declared faces. Without it, a filter that dropped every constraint rule would
        /// pass this test just as well.</para>
        /// </summary>
        [Test]
        public void AConstraintDeclaringOnlyTheDialogFaceEmitsNoProjectFinding()
        {
            Project project = ProjectWith(
                Tree.Node("sms_modem_phonenumber", "_0x81", [("phonenumber", "ikke et nummer")]));
            ProblemCatalogEntry dialogOnly = ConstraintEntry("aaa-dialog-only", RuleFaces.DialogMetadata);
            ProblemCatalogEntry bothFaces = ConstraintEntry(
                "zzz-both-faces", RuleFaces.WholeProject | RuleFaces.DialogMetadata);

            RuleSet rules = RuleSet.Create(
                ProblemCatalog.From(ImmutableArray.Create(dialogOnly, bothFaces)),
                [
                    new RuleBuilder(dialogOnly).Constrain(new AlwaysFails(dialogOnly.Code)).Build(),
                    new RuleBuilder(bothFaces).Constrain(new AlwaysFails(bothFaces.Code)).Build(),
                ]);

            EquatableArray<ValidationFinding> findings =
                new WholeProjectValidator(rules).Validate(project, ValidationProfile.ProjectOnly);

            Assert.Multiple(() =>
            {
                Assert.That(findings.Select(f => f.Code.Value), Is.EqualTo(new[] { "zzz-both-faces" }).AsCollection,
                    "the dialog-only rule would have failed this very value; the face declaration is what "
                    + "keeps it out of the report");
                Assert.That(rules.ForFace(RuleFaces.DialogMetadata), Has.Length.EqualTo(2),
                    "and the dialog face still sees both — the rule is filtered from this face, not unregistered");
            });
        }

        private static IEnumerable<IEnumerable<T>> Permutations<T>(IReadOnlyList<T> items)
        {
            if (items.Count <= 1)
            {
                yield return items;
                yield break;
            }

            for (int i = 0; i < items.Count; i++)
            {
                T head = items[i];
                List<T> rest = [.. items.Where((_, index) => index != i)];
                foreach (IEnumerable<T> tail in Permutations(rest))
                {
                    yield return new[] { head }.Concat(tail);
                }
            }
        }
    }
}
