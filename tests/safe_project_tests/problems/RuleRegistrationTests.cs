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
        /// A null tag with an attribute means "this attribute, on whatever element the rule reports" — not
        /// "the project as a whole", which is both members null. The attribute is still checked, against the
        /// registry at large rather than against one tag: there is no tag to look it up on, so the question
        /// becomes whether ANY declared element has it.
        /// <para>Without the check a wildcard declaration was the one shape registration accepted unread, so a
        /// typo in it would surface as a route that silently never fires.</para>
        /// </summary>
        [Test]
        public void ANullTagWildcardIsCheckedAgainstEveryDeclaredTag()
        {
            ProblemCatalogEntry good = Entry("addr-unassigned", target: new RuleTarget(null, "cable_colour"));
            ProblemCatalogEntry typo = Entry("addr-unassigned", target: new RuleTarget(null, "cable_color"));

            Assert.Multiple(() =>
            {
                Assert.That(() => RuleSet.Create(CatalogOf(good), [Traversal(good)]), Throws.Nothing,
                    "an attribute some element really declares is a legitimate wildcard target");
                Assert.That(FaultOf(CatalogOf(typo), Traversal(typo)),
                    Is.EqualTo(RuleRegistrationFault.UnknownTarget),
                    "and the American spelling — the mistake this check exists to catch — is refused");
            });
        }

        /// <summary>
        /// A wildcard declaration is reachable from a CONCRETE query. <c>ForTarget</c> is a prebuilt dictionary
        /// keyed by the declared target, so a rule declared <c>(null, attribute)</c> sat in its own bucket and
        /// was invisible to the one face that asks by <c>(tag, attribute)</c> — registered, listed by code, and
        /// unreachable, which is the silent-no-op failure registration exists to prevent.
        /// </summary>
        [Test]
        public void AConcreteTargetQueryAlsoFindsTheWildcardDeclaredForThatAttribute()
        {
            ProblemCatalogEntry wildcard = Entry("aaa-anywhere", target: new RuleTarget(null, "cable_colour"));
            ProblemCatalogEntry concrete = Entry("zzz-on-inputs",
                target: new RuleTarget("dataline_input", "cable_colour"));
            RuleSet rules = RuleSet.Create(CatalogOf(wildcard, concrete),
                [Traversal(wildcard), Traversal(concrete)]);

            Assert.Multiple(() =>
            {
                Assert.That(rules.ForTarget(new RuleTarget("dataline_input", "cable_colour"))
                        .Select(r => r.Entry.Code.Value),
                    Is.EqualTo(new[] { "aaa-anywhere", "zzz-on-inputs" }).AsCollection,
                    "both are about this field, and the union stays ordered by code like every other view here");

                Assert.That(rules.ForTarget(new RuleTarget("dataline_output", "cable_colour"))
                        .Select(r => r.Entry.Code.Value),
                    Is.EqualTo(new[] { "aaa-anywhere" }).AsCollection,
                    "a tag with no concrete rule still sees the wildcard — that is what makes it a wildcard");

                Assert.That(rules.ForTarget(new RuleTarget("dataline_input", "note")), Is.Empty,
                    "and it does not leak onto a different attribute");

                Assert.That(rules.ForTarget(new RuleTarget(null, "cable_colour"))
                        .Select(r => r.Entry.Code.Value),
                    Is.EqualTo(new[] { "aaa-anywhere" }).AsCollection,
                    "asking for the wildcard itself returns it once, not twice");
            });
        }

        /// <summary>
        /// The wildcard does not swallow the whole-project target: both members null still means the project as
        /// a whole, and still registers without an attribute to check.
        /// </summary>
        [Test]
        public void TheWholeProjectTargetIsStillBothMembersNull()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned", target: default);

            Assert.Multiple(() =>
            {
                Assert.That(new RuleTarget(null, null).IsWholeProject, Is.True);
                Assert.That(new RuleTarget(null, "cable_colour").IsWholeProject, Is.False,
                    "a wildcard names an attribute, so it is not a statement about the project");
                Assert.That(new RuleTarget("dataline_input", null).IsWholeProject, Is.False);
                Assert.That(() => RuleSet.Create(CatalogOf(entry), [Traversal(entry)]), Throws.Nothing);
            });
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
