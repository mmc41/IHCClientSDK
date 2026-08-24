using System;
using System.Collections.Generic;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Rule registration: what a rule must declare to be runnable at all, checked once at composition rather than
    /// discovered on a run.
    ///
    /// <para><b>There is no rule descriptor, and that is the design.</b> Kind, category, disposition, shape,
    /// target and face set live on the catalogue entry, so a rule carries the entry and adds only a body. A
    /// descriptor beside the entry would have been the same five facts declared twice with nothing comparing the
    /// copies — and the mismatch check that would then have been required is unnecessary rather than merely
    /// unwritten.</para>
    ///
    /// <para><b>Why registration throws.</b> Every fault here is a programming error at composition time, not a
    /// project defect, so it is an exception and not a problem. Failing fast NAMES the offending rule; a later
    /// sweep could only report that the rule set is inconsistent.</para>
    ///
    /// <para>Note the fault the vocabulary deliberately lacks: "missing kind". Folding the descriptor into the
    /// entry made a kindless rule unconstructible, which is a better outcome than rejecting one at
    /// registration.</para>
    /// </summary>
    [TestFixture]
    public sealed class RuleRegistrationTests
    {
        private static ProblemCatalogEntry Entry(
            string code,
            RuleFaces faces = RuleFaces.WholeProject,
            RuleTarget target = default,
            ProblemCodeStatus status = ProblemCodeStatus.Active) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, ValidationCategory.Addressing,
                CatalogDisposition.Warning, RuleKind.UserContentRule, faces, target,
                FindingShape.OnePerOccurrence, default, "Label", status);

        private static RuleDefinition Traversal(ProblemCatalogEntry entry) => new(entry, null, _ => { });

        private static ProblemCatalog CatalogOf(params ProblemCatalogEntry[] entries) =>
            ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>(entries));

        private static RuleRegistrationFault FaultOf(ProblemCatalog catalog, params RuleDefinition[] rules) =>
            Assert.Throws<RuleRegistrationException>(() => RuleSet.Create(catalog, rules))!.Fault;

        [Test]
        public void AWellFormedRuleRegistersAndIsIntrospectable()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned", target: new RuleTarget("dataline_input", "address_dataline"));
            RuleSet rules = RuleSet.Create(CatalogOf(entry), [Traversal(entry)]);

            Assert.Multiple(() =>
            {
                Assert.That(rules.Rules, Has.Length.EqualTo(1));
                Assert.That(rules.TryGet(entry.Code, out RuleDefinition found), Is.True);
                Assert.That(found.Entry, Is.EqualTo(entry));

                // The catalogue IS the rule catalogue: the face set and the target come off the entry, so the two
                // introspection doors answer from the same declaration.
                Assert.That(rules.ForFace(RuleFaces.WholeProject), Has.Length.EqualTo(1));
                Assert.That(rules.ForFace(RuleFaces.DialogMetadata), Is.Empty);
                Assert.That(rules.ForTarget(new RuleTarget("dataline_input", "address_dataline")), Has.Length.EqualTo(1));
                Assert.That(rules.Codes.Select(c => c.Value), Is.EqualTo(new[] { "addr-unassigned" }).AsCollection);
            });
        }

        [Test]
        public void ADuplicateCodeIsRefused()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned");

            Assert.That(FaultOf(CatalogOf(entry), Traversal(entry), Traversal(entry)),
                Is.EqualTo(RuleRegistrationFault.DuplicateCode));
        }

        [Test]
        public void ACodeWithNoCatalogueEntryIsRefused()
        {
            ProblemCatalogEntry declared = Entry("addr-unassigned");
            ProblemCatalogEntry undeclared = Entry("invented-by-a-rule");

            Assert.That(FaultOf(CatalogOf(declared), Traversal(undeclared)),
                Is.EqualTo(RuleRegistrationFault.NoCatalogueEntry));
        }

        /// <summary>
        /// A rule declaring no face would never be run by anything, so registering it is a silent no-op — the
        /// exact failure mode a validation engine must not have.
        /// </summary>
        [Test]
        public void ARuleServingNoFaceIsRefused()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned", faces: RuleFaces.None);

            Assert.That(FaultOf(CatalogOf(entry), Traversal(entry)),
                Is.EqualTo(RuleRegistrationFault.NoFaceDeclared));
        }

        [Test]
        public void ARuleWithBothBodiesOrNeitherIsRefused()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned");
            ConstraintSequence constraints = new(EquatableArray<IValueConstraint>.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(FaultOf(CatalogOf(entry), new RuleDefinition(entry, null, null)),
                    Is.EqualTo(RuleRegistrationFault.BodyCount), "neither");
                Assert.That(FaultOf(CatalogOf(entry), new RuleDefinition(entry, constraints, _ => { })),
                    Is.EqualTo(RuleRegistrationFault.BodyCount), "both");
            });
        }

        /// <summary>
        /// A traversal has nothing a dialog could bind to, so declaring it on the dialog or command face would be
        /// a claim the rule cannot honour. This is what keeps the multi-face claim from being decorative.
        /// </summary>
        [Test]
        public void ATraversalDeclaringANonWholeProjectFaceIsRefused()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned", faces: RuleFaces.WholeProject | RuleFaces.DialogMetadata);

            Assert.That(FaultOf(CatalogOf(entry), Traversal(entry)),
                Is.EqualTo(RuleRegistrationFault.TraversalCannotServeFace));
        }

        [Test]
        public void AnUnknownTargetAttributeOnAKnownTagIsRefused()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned",
                target: new RuleTarget("dataline_input", "no_such_attribute_exists"));

            Assert.That(FaultOf(CatalogOf(entry), Traversal(entry)),
                Is.EqualTo(RuleRegistrationFault.UnknownTarget));
        }

        /// <summary>
        /// The target check is weaker than it reads, deliberately: a project's inline DTD can declare attributes
        /// the static registry does not, so a tag the registry has never heard of is ACCEPTED rather than refused.
        /// Stating that here stops it being read later as a hole.
        /// </summary>
        [Test]
        public void ATagTheRegistryDoesNotKnowIsAccepted()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned",
                target: new RuleTarget("a_tag_only_an_inline_dtd_declares", "whatever"));

            Assert.That(() => RuleSet.Create(CatalogOf(entry), [Traversal(entry)]), Throws.Nothing);
        }

        /// <summary>
        /// A retired or ruled-out entry keeps its id reserved; it must not acquire a rule, or the reservation
        /// would quietly become an implementation.
        /// </summary>
        [Test]
        public void ARuleForARetiredOrRuledOutCodeIsRefused()
        {
            Assert.Multiple(() =>
            {
                foreach (ProblemCodeStatus status in new[] { ProblemCodeStatus.Retired, ProblemCodeStatus.RuledOut })
                {
                    ProblemCatalogEntry entry = Entry("addr-unassigned", status: status);
                    Assert.That(FaultOf(CatalogOf(entry), Traversal(entry)),
                        Is.EqualTo(RuleRegistrationFault.CodeNotActive), status.ToString());
                }
            });
        }

        [Test]
        public void TheRegisteredSetIsOrderedByCodeAndSharesTheCataloguesImmutability()
        {
            ProblemCatalogEntry first = Entry("zzz-last");
            ProblemCatalogEntry second = Entry("aaa-first");
            RuleSet rules = RuleSet.Create(CatalogOf(first, second), [Traversal(first), Traversal(second)]);

            Assert.That(rules.Rules.Select(r => r.Entry.Code.Value),
                Is.EqualTo(new[] { "aaa-first", "zzz-last" }).AsCollection,
                "a deterministic set makes an execution order provable rather than incidental");
        }

        /// <summary>
        /// The fault vocabulary, pinned. Each member is exercised by a test above, so a member added later without
        /// a test fails here rather than sitting unreachable.
        /// </summary>
        [Test]
        public void EveryRegistrationFaultIsExercised()
        {
            HashSet<RuleRegistrationFault> exercised =
            [
                RuleRegistrationFault.DuplicateCode,
                RuleRegistrationFault.NoCatalogueEntry,
                RuleRegistrationFault.NoFaceDeclared,
                RuleRegistrationFault.BodyCount,
                RuleRegistrationFault.TraversalCannotServeFace,
                RuleRegistrationFault.UnknownTarget,
                RuleRegistrationFault.CodeNotActive,

                // Exercised by FindingShapeContractTests: at registration for a declarative rule declaring a
                // group, and at the emission itself for a traversal whose emissions registration cannot see.
                RuleRegistrationFault.ShapeContradictsDeclaration,
            ];

            Assert.That(exercised, Is.EquivalentTo(Enum.GetValues<RuleRegistrationFault>()));
        }
    }
}
