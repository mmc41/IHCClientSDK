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
        /// The third severity, reached through the REAL pipeline rather than assigned to a hand-built finding.
        ///
        /// <para><b>Why the override is the way in.</b> No shipped row declares
        /// <see cref="CatalogDisposition.Info"/> yet, so the disposition→severity mapping is exercised on a
        /// seeded entry and the panel's Info tier on constructed rows. What neither of those covers is the path
        /// between them: a rule emitting, the profile deciding, the finding carrying the value, and the gate
        /// sorting it. The per-rule override reaches all four with a row that exists today, which is the
        /// difference between "Info is a value the type can hold" and "Info is a severity a run can produce".</para>
        ///
        /// <para><b>The gate is asserted beside the severity</b>, because filing is the half a wrong mapping
        /// breaks silently: a finding that read Info while <c>Infos</c> stayed empty and <c>Warnings</c> held it
        /// would still satisfy an assertion about the finding alone.</para>
        /// </summary>
        [Test]
        public void AnOverrideToInfoTravelsThroughTheRunAndIsFiledAsAnInfo()
        {
            ProjectElement terminal = Tree.Node("dataline_input", "_0x2a", []);
            Project project = ProjectWith(terminal);
            ProblemCatalogEntry entry = Entry("addr-unassigned");

            WholeProjectValidator validator = new(Compose([(entry, i => i.Report(terminal, default))]).Rules);
            EquatableArray<ValidationFinding> findings = validator.Validate(
                project,
                ValidationProfile.ProjectOnly with
                {
                    Overrides = EquatableArray.Create<SeverityOverride>(
                        [new SeverityOverride(entry.Code, ValidationSeverity.Info)]),
                });

            Assert.Multiple(() =>
            {
                Assert.That(findings.Single().Severity, Is.EqualTo(ValidationSeverity.Info),
                    "the profile's value reached the finding, rather than the entry's disposition");

                Assert.That(findings.Infos.Select(f => f.Code.Value),
                    Is.EqualTo(new[] { "addr-unassigned" }).AsCollection, "and the gate files it as one");
                Assert.That(findings.Warnings, Is.Empty, "not left in the tier its disposition would have given");
                Assert.That(findings.Errors, Is.Empty);
                Assert.That(findings.IsValid, Is.True, "an Info never blocks, exactly as a Warning never does");
            });
        }

        /// <summary>
        /// The lever's one limit: a row that REFUSES an operation may be overridden to Error and no lower. Its
        /// Danish sentence already says the save did not happen, so filing it as a Warning would hand the user a
        /// finding whose text and whose severity contradict each other — and the panel's Fatal tier reads exactly
        /// the (Error, refuses-something) pairing that a demotion breaks.
        ///
        /// <para><b>It throws rather than flooring.</b> Silently clamping would leave a caller believing a
        /// setting that was never applied, which is the failure a strictness lever can least afford.</para>
        ///
        /// <para><b>The guard is LAZY, and that is a design choice worth stating.</b>
        /// <see cref="ValidationProfile.SeverityFor"/> is consulted while a finding is being built, so a profile
        /// carrying an illegal override is inert until the demoted rule actually emits. Checking eagerly would
        /// mean a profile could not be constructed without a catalogue to check itself against — a profile is a
        /// value, not a validated configuration — and would reject overrides for rules the run never reaches.
        /// The cost is that an illegal override is found on the first project that triggers it; the assertion
        /// below pins that behaviour so it is a decision rather than a surprise.</para>
        /// </summary>
        [Test]
        public void ARowThatRefusesAnOperationCannotBeOverriddenBelowError()
        {
            ProjectElement terminal = Tree.Node("dataline_input", "_0x2a", []);
            Project project = ProjectWith(terminal);
            ProblemCatalogEntry refusing =
                Entry("attr-undeclared", ValidationCategory.FileIntegrity, CatalogDisposition.Error) with
                {
                    RefusedOperations = ImmutableArray.Create(OperationCodes.Save, OperationCodes.EditOpen),
                };
            ProblemCatalogEntry advisory = Entry("addr-unassigned");

            static ValidationProfile Overriding(ProblemCatalogEntry entry, ValidationSeverity to) =>
                ValidationProfile.ProjectOnly with
                {
                    Overrides = EquatableArray.Create<SeverityOverride>([new SeverityOverride(entry.Code, to)]),
                };

            WholeProjectValidator firing = new(Compose(
            [
                (refusing, i => i.Report(terminal, default)),
                (advisory, i => i.Report(terminal, default)),
            ]).Rules);
            WholeProjectValidator silent = new(Compose([(refusing, _ => { })]).Rules);

            Assert.Multiple(() =>
            {
                Assert.That(() => Overriding(refusing, ValidationSeverity.Warning).SeverityFor(refusing),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("attr-undeclared")
                        .And.Message.Contains("io.save")
                        .And.Message.Contains("edit.open"),
                    "the message names the row and every head it refuses, so it says what is wrong without a "
                    + "reader having to open the catalogue");

                Assert.That(() => firing.Validate(project, Overriding(refusing, ValidationSeverity.Info)),
                    Throws.TypeOf<InvalidOperationException>(),
                    "and it escapes Validate — the executor's rule-throws policy covers a RULE body, not the "
                    + "profile the run was handed");

                Assert.That(silent.Validate(project, Overriding(refusing, ValidationSeverity.Warning)), Is.Empty,
                    "lazy by construction: an illegal override on a rule that never emits is never consulted");

                Assert.That(Overriding(refusing, ValidationSeverity.Error).SeverityFor(refusing),
                    Is.EqualTo(ValidationSeverity.Error),
                    "naming a refusing row at Error is legal — the guard bars the demotion, not the mention");

                Assert.That(firing.Validate(project, Overriding(advisory, ValidationSeverity.Info))
                        .Single(f => f.Code.Value == "addr-unassigned").Severity,
                    Is.EqualTo(ValidationSeverity.Info),
                    "a row that refuses nothing may still be demoted: the guard is about the consequence the row "
                    + "carries, not about the lever");
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

        /// <summary>
        /// A declarative rule declared <c>RuleTarget(null, attribute)</c> — "this attribute, on whatever element
        /// the rule reports" — emits on EVERY registered element type that declares that attribute, instead of
        /// falling silent.
        ///
        /// <para>The executor used to return early on a null tag, so the wildcard registered, served the dialog
        /// face, and produced nothing at all on the whole-project face: a rule that is not there is at least
        /// visible as absent, whereas one that runs and emits nothing looks like a clean project.</para>
        ///
        /// <para><b>The witness is synthetic on purpose.</b> No shipped <c>doc-*</c> row can serve as one: every
        /// one of them is an <c>Inspection</c> body, which this face never sees, and rewriting one as a
        /// <c>Constrain</c> would move oracles and reopen work that is deliberately out of scope here.</para>
        /// </summary>
        [Test]
        public void ANullTagConstraintEmitsOnEveryElementTypeDeclaringItsAttribute()
        {
            // Two DIFFERENT tags that both declare cable_colour, plus one that does not.
            Project project = ProjectWith(
                Tree.Node("dataline_input", "_0x2a", [("cable_colour", "")]),
                Tree.Node("dataline_output", "_0x3a", [("cable_colour", "")]),
                Tree.Node("sms_modem_phonenumber", "_0x81", [("phonenumber", "12345678")]));

            // Addressing, not Documentation: ValidationProfile.ProjectOnly excludes the Documentation category
            // by AUDIENCE, so a Documentation entry would report nothing here for a reason that has nothing to
            // do with the wildcard walk under test.
            ProblemCatalogEntry entry = new(new ProblemCode("aaa-anywhere"),
                ProblemCatalogSection.ProjectFindings, ValidationCategory.Addressing,
                CatalogDisposition.Warning, RuleKind.UserContentRule, RuleFaces.WholeProject,
                new RuleTarget(null, "cable_colour"), FindingShape.OnePerOccurrence, default, "Label");

            RuleSet rules = RuleSet.Create(
                ProblemCatalog.From(ImmutableArray.Create(entry)),
                [new RuleBuilder(entry).Constrain(new AlwaysFails(entry.Code)).Build()]);

            EquatableArray<ValidationFinding> findings =
                new WholeProjectValidator(rules).Validate(project, ValidationProfile.ProjectOnly);

            Assert.That(findings.Select(f => f.Primary?.Locator),
                Is.EqualTo(new[] { "_0x2a", "_0x3a" }).AsCollection,
                "both declaring tags are walked, in document order, and the tag that does not declare the "
                + "attribute is left alone");
        }

        /// <summary>
        /// The whole-project target — both members null — still walks nothing. It is a different shape from the
        /// wildcard and must not be swept into it: there is no attribute to constrain, so a constraint body over
        /// it has nothing to check.
        /// </summary>
        [Test]
        public void AConstraintWithNoTargetAtAllStillWalksNothing()
        {
            Project project = ProjectWith(Tree.Node("dataline_input", "_0x2a", [("cable_colour", "")]));
            ProblemCatalogEntry entry = Entry("aaa-whole-project");

            RuleSet rules = RuleSet.Create(
                ProblemCatalog.From(ImmutableArray.Create(entry)),
                [new RuleBuilder(entry).Constrain(new AlwaysFails(entry.Code)).Build()]);

            Assert.That(new WholeProjectValidator(rules).Validate(project, ValidationProfile.ProjectOnly),
                Is.Empty);
        }

        /// <summary>
        /// A finding CARRIES its entry's target attribute. A host may not read the catalogue — the layer rules
        /// bar a frontend from it — so the finding is the only door the fact has, exactly as for
        /// <see cref="ValidationFinding.RefusedOperations"/>.
        /// <para>The four shapes that decide it: an entry that declares an attribute, one that declares none, a
        /// rule that THREW (an engine fault is about no field), and a GROUPED finding, where the one attribute
        /// belongs to the whole finding and not to a site.</para>
        /// </summary>
        [Test]
        public void AFindingCarriesItsEntrysTargetAttribute()
        {
            ProjectElement first = Tree.Node("dataline_input", "_0x10", []);
            ProjectElement second = Tree.Node("dataline_input", "_0x20", []);
            Project project = ProjectWith(first, second);

            ProblemCatalogEntry declared = Entry("aaa-declared") with
            {
                Target = new RuleTarget("dataline_input", "cable_colour"),
            };
            ProblemCatalogEntry undeclared = Entry("bbb-undeclared");
            ProblemCatalogEntry threw = Entry("ccc-threw") with
            {
                Target = new RuleTarget("dataline_input", "cable_colour"),
            };
            ProblemCatalogEntry grouped = Entry("ddd-grouped") with
            {
                Shape = FindingShape.PrimaryWithRelated,
                Target = new RuleTarget("dataline_input", "note"),
            };

            WholeProjectValidator validator = new(Compose(
            [
                (declared, i => i.Report(first, default)),
                (undeclared, i => i.Report(first, default)),
                (threw, _ => throw new InvalidOperationException("rule bug")),
                (grouped, i => i.ReportGroup(first, [second], default)),
            ]).Rules);

            Dictionary<string, ValidationFinding> byCode = validator
                .Validate(project, ValidationProfile.ProjectOnly)
                .ToDictionary(f => f.Code.Value);

            Assert.Multiple(() =>
            {
                Assert.That(byCode["aaa-declared"].TargetAttribute, Is.EqualTo("cable_colour"));
                Assert.That(byCode["bbb-undeclared"].TargetAttribute, Is.Null,
                    "an entry that names no attribute must not invent one");
                Assert.That(byCode["internal.unexpected"].TargetAttribute, Is.Null,
                    "a rule that threw reports an ENGINE fault, which is about no field of the user's project — "
                    + "the same reason the failure branch carries no refused operations");
                Assert.That(byCode["ddd-grouped"].TargetAttribute, Is.EqualTo("note"),
                    "the attribute belongs to the FINDING, so a grouped one carries it once rather than per site");
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
