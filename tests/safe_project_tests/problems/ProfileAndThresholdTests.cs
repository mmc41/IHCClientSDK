using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Profiles and blocking, kept apart — the distinction the whole two-axis design rests on.
    ///
    /// <para><b>A profile changes what is LOOKED FOR.</b> Two axes and no more: AUDIENCE (structural versus
    /// categorized, the distinction the two shipped entry points already draw) and EVALUABILITY (a rule needing
    /// facts about a target controller is not in a project-only profile at all).</para>
    ///
    /// <para><b>Blocking changes what is TOLERATED</b>, and it is one read over the findings of ONE pass: errors
    /// block, warnings do not. Conflating the two would let a stricter gate silently run rules the user never saw
    /// findings from — a save failing for a reason nothing ever reported.</para>
    ///
    /// <para><b>Why there is no threshold type.</b> It would have had exactly one legal value. A gate that must be
    /// stricter selects a profile that PROMOTES the rule it cares about, so the finding the user sees and the
    /// finding that blocks are the same finding — which a separate threshold would have broken apart.</para>
    ///
    /// <para><b>Required context, after the evidence check.</b> Exactly ONE survives: a target controller's
    /// capability limits, needed by three capacity rows. There is no implicit fallback — a capacity rule with no
    /// limits supplied is not evaluated, rather than evaluated against a guess, because the same project must not
    /// be valid on one workstation and invalid on another.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProfileAndThresholdTests
    {
        private static ProblemCatalogEntry Entry(
            string code,
            CatalogDisposition disposition,
            ValidationCategory category = ValidationCategory.Addressing,
            bool needsController = false) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, category, disposition,
                RuleKind.UserContentRule, RuleFaces.WholeProject, default, FindingShape.OnePerOccurrence,
                default, "Label")
            {
                RequiresControllerLimits = needsController,
            };

        private static (Project Project, WholeProjectValidator Validator) Fixture(
            params ProblemCatalogEntry[] entries)
        {
            ProjectElement subject = Tree.Node("dataline_input", "_0x2a", []);
            Project project = new(Tree.Node("utcs_project", null, [], subject));
            ProblemCatalog catalog = ProblemCatalog.From(entries.ToImmutableArray());
            RuleSet rules = RuleSet.Create(catalog,
                entries.Select(e => new RuleBuilder(e).Inspect(i => i.Report(subject, default)).Build()));
            return (project, new WholeProjectValidator(rules));
        }

        [Test]
        public void AGateBlocksOnErrorsAndToleratesWarnings()
        {
            (Project project, WholeProjectValidator validator) = Fixture(
                Entry("addr-unassigned", CatalogDisposition.Warning),
                Entry("dataline-address-range", CatalogDisposition.Error));

            EquatableArray<ValidationFinding> findings = validator.Validate(project, ValidationProfile.ProjectOnly);

            Assert.Multiple(() =>
            {
                Assert.That(findings, Has.Length.EqualTo(2));
                Assert.That(findings.IsValid, Is.False, "one Error is enough to block");
                Assert.That(findings.Errors.Select(f => f.Code.Value),
                    Is.EqualTo(new[] { "dataline-address-range" }).AsCollection);
                Assert.That(findings.Warnings.Select(f => f.Code.Value),
                    Is.EqualTo(new[] { "addr-unassigned" }).AsCollection);
            });
        }

        [Test]
        public void WarningsAloneLeaveAProjectValid()
        {
            (Project project, WholeProjectValidator validator) = Fixture(
                Entry("addr-unassigned", CatalogDisposition.Warning));

            EquatableArray<ValidationFinding> findings = validator.Validate(project, ValidationProfile.ProjectOnly);

            Assert.Multiple(() =>
            {
                Assert.That(findings, Has.Length.EqualTo(1), "the finding is still reported");
                Assert.That(findings.IsValid, Is.True, "and still does not block — only the author can judge it");
            });
        }

        /// <summary>
        /// The gate's own requirement, in one test: an override promotes a NAMED Warning at ONE gate, the other
        /// gate is unaffected, and both read the findings of the same engine rather than a second pipeline.
        /// </summary>
        [Test]
        public void AnOverridePromotesANamedWarningAtOneGateOnly()
        {
            ProblemCatalogEntry advisory = Entry("addr-unassigned", CatalogDisposition.Warning);
            ProblemCatalogEntry other = Entry("dev-setting-default", CatalogDisposition.Warning,
                ValidationCategory.DeviceSettings);
            (Project project, WholeProjectValidator validator) = Fixture(advisory, other);

            ValidationProfile lenient = ValidationProfile.ProjectOnly;
            ValidationProfile beforeUpload = ValidationProfile.ProjectOnly with
            {
                Name = "BeforeUpload",
                Overrides = EquatableArray.Create<SeverityOverride>(
                    [new SeverityOverride(advisory.Code, ValidationSeverity.Error)]),
            };

            EquatableArray<ValidationFinding> lenientRun = validator.Validate(project, lenient);
            EquatableArray<ValidationFinding> strictRun = validator.Validate(project, beforeUpload);

            Assert.Multiple(() =>
            {
                Assert.That(lenientRun.IsValid, Is.True, "the ordinary gate tolerates it");
                Assert.That(strictRun.IsValid, Is.False, "the upload gate does not");

                Assert.That(strictRun.Errors.Select(f => f.Code.Value),
                    Is.EqualTo(new[] { "addr-unassigned" }).AsCollection,
                    "exactly the NAMED rule was promoted");
                Assert.That(strictRun.Warnings.Select(f => f.Code.Value),
                    Is.EqualTo(new[] { "dev-setting-default" }).AsCollection,
                    "the other warning is untouched — this is a per-rule lever, not a global one");

                // The same rules ran either way: what changed is what is TOLERATED, not what is looked for.
                Assert.That(strictRun.Select(f => f.Code.Value), Is.EqualTo(lenientRun.Select(f => f.Code.Value)).AsCollection,
                    "one pass, two readings — never a second pipeline with its own rules");
            });
        }

        /// <summary>
        /// The other axis, moving independently: a profile changes WHICH rules run, and that is visible as a
        /// different finding set rather than as a different severity.
        /// </summary>
        [Test]
        public void AProfileChangesWhichRulesRunRatherThanHowSevereTheyAre()
        {
            (Project project, WholeProjectValidator validator) = Fixture(
                Entry("addr-unassigned", CatalogDisposition.Warning),
                Entry("doc-cabletype", CatalogDisposition.Warning, ValidationCategory.Documentation));

            string[] structural = [.. validator.Validate(project, ValidationProfile.ProjectOnly).Select(f => f.Code.Value)];
            string[] categorized = [.. validator.Validate(project, ValidationProfile.Categorized).Select(f => f.Code.Value)];

            Assert.Multiple(() =>
            {
                Assert.That(structural, Is.EqualTo(new[] { "addr-unassigned" }).AsCollection);
                Assert.That(categorized, Is.EqualTo(new[] { "addr-unassigned", "doc-cabletype" }).AsCollection);
                Assert.That(structural.Length, Is.LessThan(categorized.Length),
                    "a narrower profile is a SUBSET — it withholds nothing it looked for");
            });
        }

        /// <summary>
        /// The evaluability axis, and the no-implicit-fallback rule that makes it honest.
        /// </summary>
        [Test]
        public void ARuleNeedingControllerLimitsIsAbsentUntilTheyAreSupplied()
        {
            ProblemCatalogEntry capacity = Entry("capacity-wireless-exceeded", CatalogDisposition.Warning,
                ValidationCategory.ProjectStructure, needsController: true);
            (Project project, WholeProjectValidator validator) = Fixture(capacity);

            EquatableArray<ValidationFinding> withoutLimits = validator.Validate(project, ValidationProfile.ProjectOnly);
            EquatableArray<ValidationFinding> withLimits = validator.Validate(project,
                ValidationProfile.ProjectOnly with { Controller = ControllerCapabilityLimits.VendorDocumented });

            Assert.Multiple(() =>
            {
                Assert.That(withoutLimits, Is.Empty, "not evaluated — and therefore not reported as passing either");
                Assert.That(withLimits.Select(f => f.Code.Value),
                    Is.EqualTo(new[] { "capacity-wireless-exceeded" }).AsCollection);

                Assert.That(ValidationProfile.ProjectOnly.Includes(capacity), Is.False);
                Assert.That(ValidationProfile.ProjectOnly.Controller, Is.Null,
                    "no implicit fallback: validating against a default is indistinguishable from a guess");
            });
        }

        /// <summary>
        /// The documented limits, as declared data. They are the vendor's, not invented, and a rule reads them
        /// from here rather than writing a number inline where review cannot see it.
        /// </summary>
        [Test]
        public void TheDocumentedControllerLimitsAreTheVendorsOwn()
        {
            ControllerCapabilityLimits limits = ControllerCapabilityLimits.VendorDocumented;

            Assert.Multiple(() =>
            {
                Assert.That(limits.InputModules, Is.EqualTo(8));
                Assert.That(limits.OutputModules, Is.EqualTo(16));
                Assert.That(limits.AddressesPerDirection, Is.EqualTo(128),
                    "8 x 16 and 16 x 8 both land here, which is what corroborates the datasheet");
                Assert.That(limits.WirelessDevices, Is.EqualTo(64));
            });
        }

        /// <summary>
        /// There is no threshold TYPE, and its absence is asserted so one is not reintroduced: a second strictness
        /// mechanism beside the per-rule override is how the finding a user sees and the finding that blocks stop
        /// being the same finding.
        /// </summary>
        [Test]
        public void ThereIsNoSeverityThresholdTypeBesideThePerRuleOverride()
        {
            string[] validationTypes = [.. typeof(ValidationProfile).Assembly.GetExportedTypes()
                .Where(t => t.Namespace == typeof(ValidationProfile).Namespace)
                .Select(t => t.Name)];

            Assert.Multiple(() =>
            {
                foreach (string threshold in new[] { "SeverityThreshold", "BlockingPolicy", "ValidationContextKind", "IValidationContext" })
                {
                    Assert.That(validationTypes, Does.Not.Contain(threshold),
                        $"'{threshold}' would be a second strictness or context mechanism beside the one that works");
                }

                Assert.That(validationTypes, Does.Contain(nameof(SeverityOverride)));
                Assert.That(validationTypes, Does.Contain(nameof(ControllerCapabilityLimits)));
            });
        }
    }
}
