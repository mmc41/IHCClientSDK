using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-6: a rule whose emission contradicts its declared <see cref="FindingShape"/> is REJECTED, instead of
    /// drifting silently.
    ///
    /// <para>The shape is not decoration. It tells a consumer whether N findings are N repairs or one, and
    /// whether a finding has other sites worth navigating to. Nothing checked it, and the corpus proved the cost:
    /// TWO rows were wrong, in opposite directions — <c>dataline-address-duplicate</c> declared a group and
    /// emitted singletons, <c>logic-duplicate-program</c> emitted a group under a single-site declaration.</para>
    ///
    /// <para>The contract is enforced in two places because only one of its halves is decidable statically. A
    /// DECLARATIVE rule reports through <c>Report</c> and has no way to name a related site, so declaring a group
    /// is a contradiction registration can refuse. A TRAVERSAL's emissions are invisible behind a delegate, so
    /// that half is enforced at the emission itself.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingShapeContractTests
    {
        private static ProblemCatalogEntry Entry(FindingShape shape, RuleTarget target = default) =>
            new(new ProblemCode("addr-unassigned"), ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing, CatalogDisposition.Warning, RuleKind.UserContentRule,
                RuleFaces.WholeProject, target, shape, default, "Label");

        private static Project OneElement(out ProjectElement element)
        {
            element = Node("dataline_input", "_0x2a", []);
            return new Project(Node("utcs_project", null, [], [element]));
        }

        private static RuleSet Compose(ProblemCatalogEntry entry, ProjectInspection body) =>
            RuleSet.Create(
                ProblemCatalog.From([entry]), [new RuleBuilder(entry).Inspect(body).Build()]);

        [Test]
        public void APrimaryWithRelatedRuleEmittingASingleSiteIsRejected()
        {
            Project project = OneElement(out ProjectElement element);
            ProblemCatalogEntry entry = Entry(FindingShape.PrimaryWithRelated);
            RuleSet rules = Compose(entry, i => i.Report(element, default));

            RuleRegistrationException thrown = Assert.Throws<RuleRegistrationException>(
                () => new WholeProjectValidator(rules).Validate(project, ValidationProfile.ProjectOnly))!;

            Assert.Multiple(() =>
            {
                Assert.That(thrown.Fault, Is.EqualTo(RuleRegistrationFault.ShapeContradictsDeclaration));
                Assert.That(thrown.Code.Value, Is.EqualTo("addr-unassigned"));
            });
        }

        /// <summary>The other direction: a single-site row may not report a group either.</summary>
        [Test]
        public void ASingleSiteRuleEmittingAGroupIsRejected()
        {
            Project project = OneElement(out ProjectElement element);
            ProblemCatalogEntry entry = Entry(FindingShape.OnePerOccurrence);
            RuleSet rules = Compose(entry, i => i.ReportGroup(element, [element], default));

            RuleRegistrationException thrown = Assert.Throws<RuleRegistrationException>(
                () => new WholeProjectValidator(rules).Validate(project, ValidationProfile.ProjectOnly))!;

            Assert.That(thrown.Fault, Is.EqualTo(RuleRegistrationFault.ShapeContradictsDeclaration));
        }

        /// <summary>
        /// A shape violation is a COMPOSITION error, so it must escape the report-and-continue net. Swallowed, it
        /// would become one more <c>internal.unexpected</c> finding ABOUT THE USER'S FILE — which would hide the
        /// very drift this check exists to surface, and hide it under the default policy.
        /// </summary>
        [Test]
        public void AShapeViolationIsNotSwallowedByTheReportAndContinuePolicy()
        {
            Project project = OneElement(out ProjectElement element);
            RuleSet rules = Compose(Entry(FindingShape.PrimaryWithRelated), i => i.Report(element, default));

            Assert.That(ValidationProfile.ProjectOnly.FailurePolicy,
                Is.EqualTo(RuleFailurePolicy.ReportAndContinue),
                "the profile under test must be the forgiving one, or this proves nothing");
            Assert.That(
                () => new WholeProjectValidator(rules).Validate(project, ValidationProfile.ProjectOnly).Findings,
                Throws.InstanceOf<RuleRegistrationException>());
        }

        /// <summary>
        /// The statically decidable half: a DECLARATIVE rule cannot name a related site, so a group declaration is
        /// refused at registration — before anything runs.
        /// </summary>
        [Test]
        public void ADeclarativeRuleDeclaringAGroupIsRejectedAtRegistration()
        {
            ProblemCatalogEntry entry = Entry(
                FindingShape.PrimaryWithRelated, new RuleTarget("sms_modem_phonenumber", "phonenumber"));

            RuleRegistrationException thrown = Assert.Throws<RuleRegistrationException>(
                () => RuleSet.Create(
                    ProblemCatalog.From([entry]),
                    [new RuleBuilder(entry).Constrain(RequiredFieldConstraint.For(entry.Code)).Build()]))!;

            Assert.That(thrown.Fault, Is.EqualTo(RuleRegistrationFault.ShapeContradictsDeclaration));
        }

        /// <summary>
        /// THE ARMED CONTROL, and the reason the checks above are not decoration: every SHIPPED rule agrees with
        /// its declaration. This is the assertion that caught the two rows that did not.
        /// </summary>
        [Test]
        public void EveryShippedRuleAgreesWithItsDeclaredShape()
        {
            Assert.Multiple(() =>
            {
                foreach ((string name, Func<Project> build) in ValidationCharacterizationTests.Corpus)
                {
                    Assert.That(
                        () => ProjectRules.Validator.Validate(build(), ValidationProfile.Categorized).Findings,
                        Throws.Nothing, name);
                }
            });
        }
    }
}
