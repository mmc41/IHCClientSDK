using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// THE FIELD-METADATA FACE — the dialog-metadata read, answered from the SAME rule definitions the
    /// whole-project run executes.
    ///
    /// <para><b>The duplication this closes.</b> Today a field's numeric bounds are derived by the product-dialog
    /// composer and then thrown away: the commit check reads the field's rule and never its bounds, so an
    /// out-of-range value commits, and the GUI carries its own hard-coded clamps as a second copy that can
    /// disagree with the catalog. One constraint answering both "is this acceptable?" and "what would be
    /// acceptable?" makes that pair impossible to get out of step.</para>
    ///
    /// <para><b>Most-restrictive wins.</b> Where two rules constrain one field, the merged answer takes the
    /// tighter bound, the longer minimum, the shorter maximum, the narrower allowed set, and required if either
    /// requires. A dialog advertising the LOOSER of two bounds would invite a value the commit path then refuses,
    /// which is a worse experience than a bound the user never had the chance to break.</para>
    ///
    /// <para><b>A traversal contributes nothing here, and that is the honest answer.</b> Graph reachability and
    /// dataflow rules have nothing a dialog could bind to — which is exactly why a rule declares one body kind,
    /// and why the multi-face claim is stated as "about one row in five" rather than as a property of every
    /// rule.</para>
    /// </summary>
    [TestFixture]
    public sealed class FieldMetadataFaceTests
    {
        private sealed class Constraint : IValueConstraint
        {
            public Constraint(string code, FieldConstraintMetadata metadata)
            {
                Code = new ProblemCode(code);
                Metadata = metadata;
            }

            public ProblemCode Code { get; }

            public FieldConstraintMetadata Metadata { get; }

            public ValueConstraintVerdict Check(string? rawValue) => ValueConstraintVerdict.Ok;

            public FieldConstraintMetadata Describe() => Metadata;
        }

        private static readonly RuleTarget Pin = new("dataline_input", "address_dataline");

        private static ProblemCatalogEntry Entry(
            string code, RuleTarget target, RuleFaces faces = RuleFaces.WholeProject | RuleFaces.DialogMetadata) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, ValidationCategory.Addressing,
                CatalogDisposition.Warning, RuleKind.UserContentRule, faces,
                target, FindingShape.OnePerOccurrence, default, "Label");

        private static RuleSet SetOf(params (ProblemCatalogEntry Entry, IValueConstraint? Constraint)[] rules)
        {
            ProblemCatalog catalog = ProblemCatalog.From(rules.Select(r => r.Entry).ToImmutableArray());
            return RuleSet.Create(catalog, rules.Select(r =>
            {
                RuleBuilder builder = new(r.Entry);
                return r.Constraint is { } constraint
                    ? builder.Constrain(constraint).Build()
                    : builder.Inspect(_ => { }).Build();
            }));
        }

        [Test]
        public void AFieldNoRuleTargetsIsUnconstrained()
        {
            RuleSet rules = SetOf((Entry("addr-unassigned", Pin), new Constraint("addr-unassigned",
                FieldConstraintMetadata.Unconstrained with { Required = true })));

            FieldConstraintMetadata metadata = rules.DescribeField(new RuleTarget("product", "name"));

            Assert.Multiple(() =>
            {
                Assert.That(metadata, Is.EqualTo(FieldConstraintMetadata.Unconstrained));
                Assert.That(metadata.Required, Is.False, "an honest 'no constraint' rather than a guessed default");
                Assert.That(rules.ConstraintsOn(new RuleTarget("product", "name")), Is.Empty);
            });
        }

        [Test]
        public void TheMetadataComesOffTheSameConstraintTheValidatorRuns()
        {
            IValueConstraint bounds = new Constraint("addr-out-of-range",
                FieldConstraintMetadata.Unconstrained with { Minimum = 1, Maximum = 128 });
            RuleSet rules = SetOf((Entry("addr-out-of-range", Pin), bounds));

            FieldConstraintMetadata metadata = rules.DescribeField(Pin);

            Assert.Multiple(() =>
            {
                Assert.That(metadata.Minimum, Is.EqualTo(1));
                Assert.That(metadata.Maximum, Is.EqualTo(128));
                Assert.That(rules.ConstraintsOn(Pin).Select(c => c.Value),
                    Is.EqualTo(new[] { "addr-out-of-range" }).AsCollection,
                    "and the identity behind the bound is reachable, for a tooltip that names the rule");
            });
        }

        [Test]
        public void TwoRulesOnOneFieldMergeToTheMostRestrictiveAnswer()
        {
            RuleSet rules = SetOf(
                (Entry("addr-out-of-range", Pin), new Constraint("addr-out-of-range",
                    new FieldConstraintMetadata(false, 1, 128, 1, 40, true, EquatableArray<string>.Empty))),
                (Entry("addr-unassigned", Pin), new Constraint("addr-unassigned",
                    new FieldConstraintMetadata(true, 8, 64, 3, 20, false, EquatableArray<string>.Empty))));

            FieldConstraintMetadata metadata = rules.DescribeField(Pin);

            Assert.Multiple(() =>
            {
                Assert.That(metadata.Required, Is.True, "required if EITHER requires");
                Assert.That(metadata.Minimum, Is.EqualTo(8), "the higher lower bound");
                Assert.That(metadata.Maximum, Is.EqualTo(64), "the lower upper bound");
                Assert.That(metadata.MinimumLength, Is.EqualTo(3));
                Assert.That(metadata.MaximumLength, Is.EqualTo(20));
                Assert.That(metadata.WhitespaceAllowed, Is.False, "allowed only if BOTH allow");
            });
        }

        [Test]
        public void AllowedValueSetsIntersectRatherThanConcatenate()
        {
            RuleSet rules = SetOf(
                (Entry("attr-enum-range", Pin), new Constraint("attr-enum-range",
                    FieldConstraintMetadata.Unconstrained with
                    {
                        AllowedValues = EquatableArray.Create<string>(["on", "off", "toggle"]),
                    })),
                (Entry("addr-unassigned", Pin), new Constraint("addr-unassigned",
                    FieldConstraintMetadata.Unconstrained with
                    {
                        AllowedValues = EquatableArray.Create<string>(["off", "toggle", "pulse"]),
                    })));

            Assert.That(rules.DescribeField(Pin).AllowedValues,
                Is.EqualTo(new[] { "off", "toggle" }).AsCollection,
                "a value only one rule permits is not permitted");
        }

        [Test]
        public void ATraversalRuleContributesNothingToADialog()
        {
            RuleSet rules = SetOf((Entry("link-output-undriven", Pin, RuleFaces.WholeProject), null));

            Assert.Multiple(() =>
            {
                Assert.That(rules.DescribeField(Pin), Is.EqualTo(FieldConstraintMetadata.Unconstrained),
                    "a graph traversal has nothing a dialog could bind to");
                Assert.That(rules.ConstraintsOn(Pin), Is.Empty);
                Assert.That(rules.ForTarget(Pin), Has.Length.EqualTo(1),
                    "the rule IS about the field — it simply has nothing declarative to say about it");
            });
        }

        /// <summary>
        /// The facade is the door. Composing a <see cref="RuleSet"/> with a target is business logic, and
        /// <c>ProjectAppService</c> is where the SDK composes its faces — no other type may hold the rule set, so
        /// a frontend that wanted a field's declared bounds had nowhere to ask before this member existed.
        /// <para>The answer is asserted to be a REAL one rather than merely present: the telephone target is the
        /// repository's one registered constraint, so a door that returned <c>Unconstrained</c> for it would be a
        /// door onto nothing.</para>
        /// </summary>
        [Test]
        public void TheFacadeAnswersTheFieldMetadataReadFromTheRegisteredRules()
        {
            RuleTarget phone = new("sms_modem_phonenumber", "phonenumber");
            FieldConstraintMetadata throughTheDoor = new ProjectAppService(TestSetup.Settings).DescribeField(phone);

            Assert.Multiple(() =>
            {
                Assert.That(throughTheDoor, Is.EqualTo(ProjectRules.Registered.DescribeField(phone)),
                    "the door composes the SHIPPED rule set, not a set of its own");
                Assert.That(throughTheDoor, Is.Not.EqualTo(FieldConstraintMetadata.Unconstrained),
                    "and it has something to say — otherwise the door is onto nothing");
                Assert.That(throughTheDoor.MinimumLength, Is.EqualTo(3));
                Assert.That(throughTheDoor.MaximumLength, Is.EqualTo(20));
                Assert.That(throughTheDoor.WhitespaceAllowed, Is.False);
                Assert.That(new ProjectAppService(TestSetup.Settings)
                        .DescribeField(new RuleTarget("dataline_input", "name")),
                    Is.EqualTo(FieldConstraintMetadata.Unconstrained),
                    "and it answers honestly for a field no constraint targets");
            });
        }

        /// <summary>
        /// A CONSTRAINT that does not declare the dialog face is not this face's business either.
        /// <para>The traversal case above is decided by the BODY — a graph walk has nothing to describe. This one
        /// is decided by the DECLARATION and by nothing else: same body kind, same target, a perfectly good
        /// <see cref="FieldConstraintMetadata"/> to offer, and it is still excluded because its entry never
        /// claimed the face. Registration cannot enforce that (a constraint serving one face is legal), so the
        /// face read is the only place it can be honoured.</para>
        /// <para>The second rule is the control, and the bounds are chosen so a missing filter cannot hide: the
        /// project-only rule is the STRICTER of the two, so if it were still merged in it would WIN the maximum
        /// length and the assertion would read 4 instead of 9.</para>
        /// </summary>
        [Test]
        public void AConstraintNotDeclaringTheDialogFaceContributesNothing()
        {
            RuleSet rules = SetOf(
                (Entry("aaa-project-only", Pin, RuleFaces.WholeProject),
                    new Constraint("aaa-project-only",
                        FieldConstraintMetadata.Unconstrained with { Required = true, MaximumLength = 4 })),
                (Entry("zzz-dialog-too", Pin),
                    new Constraint("zzz-dialog-too",
                        FieldConstraintMetadata.Unconstrained with { MaximumLength = 9 })));

            Assert.Multiple(() =>
            {
                Assert.That(rules.DescribeField(Pin).MaximumLength, Is.EqualTo(9),
                    "the project-only rule's stricter bound would have won the merge if it were still read here");
                Assert.That(rules.DescribeField(Pin).Required, Is.False,
                    "and its required-ness would have been advertised on a field no dialog rule requires");
                Assert.That(rules.ConstraintsOn(Pin).Select(c => c.Value),
                    Is.EqualTo(new[] { "zzz-dialog-too" }).AsCollection);
                Assert.That(rules.ForTarget(Pin), Has.Length.EqualTo(2),
                    "both rules ARE about the field — one of them simply does not answer to this face");
            });
        }

        /// <summary>
        /// The bound a dialog advertises and the bound the commit path enforces come off ONE object, so they
        /// cannot disagree. This is the property the whole face exists for, so it is asserted directly rather
        /// than inferred from the two halves being tested separately.
        /// </summary>
        [Test]
        public void WhatTheDialogAdvertisesIsWhatTheValidatorEnforces()
        {
            IValueConstraint bounded = new BoundedTo128();
            RuleSet rules = SetOf((Entry("dataline-address-range", Pin), bounded));
            FieldConstraintMetadata advertised = rules.DescribeField(Pin);

            Assert.Multiple(() =>
            {
                Assert.That(advertised.Maximum, Is.EqualTo(128));
                Assert.That(bounded.Check("128").Satisfied, Is.True, "the advertised maximum is accepted");
                Assert.That(bounded.Check("129").Satisfied, Is.False, "and one past it is not");
                Assert.That(bounded.Check("129").Arguments.Select(a => a.Name),
                    Does.Contain("maximum"), "the refusal carries the bound as DATA, ready for the template");
            });
        }

        private sealed class BoundedTo128 : IValueConstraint
        {
            public ProblemCode Code => new("dataline-address-range");

            public ValueConstraintVerdict Check(string? rawValue) =>
                int.TryParse(rawValue, out int value) && value is >= 1 and <= 128
                    ? ValueConstraintVerdict.Ok
                    : ValueConstraintVerdict.Failed(EquatableArray.Create<ProblemArgument>(
                    [
                        new ProblemArgument("value", rawValue ?? string.Empty),
                        new ProblemArgument("maximum", 128),
                    ]));

            public FieldConstraintMetadata Describe() =>
                FieldConstraintMetadata.Unconstrained with { Minimum = 1, Maximum = 128 };
        }
    }
}
