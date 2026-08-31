using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// REQUIRED FIELDS — the second exemplar, and the one that shows the constraint shape is a vocabulary rather
    /// than a special case.
    ///
    /// <para><b>What it replaces.</b> There is no required-field concept anywhere today, in the SDK or the app:
    /// the shipped dialog rule states outright that an empty value always satisfies it, and no other member says
    /// a value is needed. What exists instead is three imperative blank gates, written independently, which
    /// disagree about the middle case and of which only ONE tells the user anything — the other two return
    /// silently, so the user presses OK and nothing happens.</para>
    ///
    /// <para><b>The middle case is the whole point.</b> Blank, whitespace-only, present: three equivalence
    /// partitions, and the second is where the ad-hoc gates part company. Naming the policy makes the answer a
    /// declaration rather than a consequence of whichever helper a site reached for — and it travels to the
    /// dialog, so a dialog that trims and a validator that does not cannot disagree about a name of three
    /// spaces.</para>
    ///
    /// <para><b>Both faces, one object.</b> The same constraint answers the commit verdict and describes the
    /// field, exactly as the numeric-bounds exemplar does. That is what makes this a pattern.</para>
    /// </summary>
    [TestFixture]
    public sealed class RequiredFieldExemplarTests
    {
        private static readonly ProblemCode Name = new("name-empty");

        // ── equivalence partitions: blank, whitespace-only, present ─────────────────────────────────

        [Test]
        public void ABlankValueIsRefusedUnderEitherPolicy()
        {
            Assert.Multiple(() =>
            {
                foreach (BlankPolicy policy in Enum.GetValues<BlankPolicy>())
                {
                    IValueConstraint required = RequiredFieldConstraint.For(Name, policy);
                    Assert.That(required.Check(null).Satisfied, Is.False, $"{policy}: null");
                    Assert.That(required.Check(string.Empty).Satisfied, Is.False, $"{policy}: empty");
                }
            });
        }

        [Test]
        public void APresentValueIsAcceptedUnderEitherPolicy()
        {
            Assert.Multiple(() =>
            {
                foreach (BlankPolicy policy in Enum.GetValues<BlankPolicy>())
                {
                    IValueConstraint required = RequiredFieldConstraint.For(Name, policy);
                    Assert.That(required.Check("Stue").Satisfied, Is.True, $"{policy}: a name");
                    Assert.That(required.Check(" Stue ").Satisfied, Is.True, $"{policy}: padded, but present");
                    Assert.That(required.Check("0").Satisfied, Is.True, $"{policy}: zero is a value, not a blank");
                }
            });
        }

        /// <summary>
        /// The partition the three ad-hoc gates disagree about, decided once and named. A name of three spaces is
        /// not a name — treating it as one is how a project ends up with invisible labels — but a padded field the
        /// file must keep verbatim is a different question, which is why the policy exists at all.
        /// </summary>
        [Test]
        public void WhitespaceOnlyIsWhereThePolicyActuallyDecides()
        {
            IValueConstraint strict = RequiredFieldConstraint.For(Name, BlankPolicy.WhitespaceIsBlank);
            IValueConstraint lenient = RequiredFieldConstraint.For(Name, BlankPolicy.EmptyOnly);

            Assert.Multiple(() =>
            {
                foreach (string whitespace in new[] { " ", "   ", "\t", "\n", " \t " })
                {
                    Assert.That(strict.Check(whitespace).Satisfied, Is.False,
                        $"WhitespaceIsBlank: '{whitespace.Replace('\t', 't').Replace('\n', 'n')}'");
                    Assert.That(lenient.Check(whitespace).Satisfied, Is.True,
                        $"EmptyOnly: '{whitespace.Replace('\t', 't').Replace('\n', 'n')}'");
                }
            });
        }

        [Test]
        public void WhitespaceIsBlankIsTheDefault()
        {
            RequiredFieldConstraint fallback = RequiredFieldConstraint.For(Name);

            Assert.Multiple(() =>
            {
                Assert.That(fallback.Policy, Is.EqualTo(BlankPolicy.WhitespaceIsBlank));
                Assert.That(fallback.Check("   ").Satisfied, Is.False,
                    "the default is the one that protects a reader, not the one that is easiest to implement");
            });
        }

        // ── one object, both faces ──────────────────────────────────────────────────────────────────

        [Test]
        public void TheDialogLearnsTheFieldIsRequiredFromTheSameObjectThatRefusesIt()
        {
            IValueConstraint required = RequiredFieldConstraint.For(Name);
            FieldConstraintMetadata described = required.Describe();

            Assert.Multiple(() =>
            {
                Assert.That(described.Required, Is.True);
                Assert.That(described.MinimumLength, Is.EqualTo(1));
                Assert.That(required.Check(string.Empty).Satisfied, Is.False,
                    "advertised and enforced by the same object, so they cannot disagree");
            });
        }

        /// <summary>
        /// The blank POLICY reaches the dialog too. Without it a dialog could trim before testing while the
        /// validator does not, and a field of three spaces would pass one and fail the other.
        ///
        /// <para><b>On its OWN member, not on <c>WhitespaceAllowed</c> (D4).</b> That property means "no
        /// whitespace character anywhere" — which is how the shipped <c>DialogValueRule.Matches</c> enforces it
        /// (<c>!WhitespaceAllowed &amp;&amp; value.Any(char.IsWhiteSpace)</c>). Carrying the blank policy on it
        /// therefore told a dialog that a required NAME may contain no space at all, so "Stue loft" was
        /// advertised as invalid; and because <c>Tighten</c> merges that flag with <c>&amp;&amp;</c>, ONE such
        /// constraint poisoned every other rule on the same field. Two different facts needed two members.</para>
        /// </summary>
        [Test]
        public void TheBlankPolicyTravelsToTheDialogRatherThanStayingBehind()
        {
            FieldConstraintMetadata strict = RequiredFieldConstraint.For(Name, BlankPolicy.WhitespaceIsBlank).Describe();
            FieldConstraintMetadata lenient = RequiredFieldConstraint.For(Name, BlankPolicy.EmptyOnly).Describe();

            Assert.Multiple(() =>
            {
                Assert.That(strict.Blank, Is.EqualTo(BlankPolicy.WhitespaceIsBlank),
                    "whitespace-only is blank here, so the dialog must not offer it as content");
                Assert.That(lenient.Blank, Is.EqualTo(BlankPolicy.EmptyOnly));

                Assert.That(strict.WhitespaceAllowed, Is.True,
                    "and a name with a space in it is still a name — interior whitespace is a DIFFERENT question");
                Assert.That(lenient.WhitespaceAllowed, Is.True);
            });
        }

        /// <summary>
        /// Merging keeps the STRICTER blank policy, for the reason every other merge in
        /// <c>FieldMetadataFace.Tighten</c> keeps the stricter bound: a dialog that advertised the looser of two
        /// policies would invite a value the commit path then refuses.
        /// </summary>
        [Test]
        public void MergingTwoBlankPoliciesKeepsTheStricter()
        {
            FieldConstraintMetadata strict = RequiredFieldConstraint.For(Name, BlankPolicy.WhitespaceIsBlank).Describe();
            FieldConstraintMetadata lenient = RequiredFieldConstraint.For(Name, BlankPolicy.EmptyOnly).Describe();

            Assert.Multiple(() =>
            {
                Assert.That(FieldConstraintMetadata.Unconstrained.Blank, Is.EqualTo(BlankPolicy.EmptyOnly),
                    "an unconstrained field takes the LOOSER policy, so a merge can only tighten");
                Assert.That(FieldMetadataFace.Stricter(lenient, strict).Blank,
                    Is.EqualTo(BlankPolicy.WhitespaceIsBlank), "either order");
                Assert.That(FieldMetadataFace.Stricter(strict, lenient).Blank,
                    Is.EqualTo(BlankPolicy.WhitespaceIsBlank));
            });
        }

        /// <summary>
        /// The refusal carries NO arguments, and that is right: "this field is empty" needs no datum to say. The
        /// rule's own Danish message is the whole sentence, which is what stops a host authoring a second one for
        /// the same condition.
        /// </summary>
        [Test]
        public void TheRefusalNeedsNoArgumentsBecauseTheMessageSaysEverything()
        {
            ValueConstraintVerdict verdict = RequiredFieldConstraint.For(Name).Check(null);

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Satisfied, Is.False);
                Assert.That(verdict.Arguments, Is.Empty);
            });
        }

        // ── through the engine, on both faces ───────────────────────────────────────────────────────

        [Test]
        public void AWholeProjectRunReportsEveryBlankOccurrenceThroughTheSameConstraint()
        {
            ProblemCatalogEntry entry = new(
                Name, ProblemCatalogSection.ProjectFindings, ValidationCategory.Documentation,
                CatalogDisposition.Warning, RuleKind.UserContentRule,
                RuleFaces.WholeProject | RuleFaces.DialogMetadata,
                new RuleTarget("product_dataline", "name"), FindingShape.OnePerOccurrence,
                default, "Mangler navn");

            Project project = new(Tree.Node("utcs_project", null, [],
                Tree.Node("product_dataline", "_0x10", [("name", "Stue")]),
                Tree.Node("product_dataline", "_0x20", [("name", "   ")]),
                Tree.Node("product_dataline", "_0x30", [])));

            RuleSet rules = RuleSet.Create(
                ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>([entry])),
                [new RuleBuilder(entry).Constrain(RequiredFieldConstraint.For(Name)).Build()]);

            ValidationFinding[] findings = [.. new WholeProjectValidator(rules)
                .Validate(project, ValidationProfile.Categorized).Findings];

            Assert.Multiple(() =>
            {
                Assert.That(findings.Select(f => f.Primary!.Locator),
                    Is.EqualTo(new[] { "_0x20", "_0x30" }).AsCollection,
                    "the whitespace-only name and the absent one; the real name is left alone");
                Assert.That(findings[0].Problem.Message, Is.EqualTo("Mangler navn"),
                    "the rule's own Danish label, not a sentence assembled at the site");
                Assert.That(rules.DescribeField(new RuleTarget("product_dataline", "name")).Required, Is.True,
                    "and the dialog face reads required-ness off the same rule");
            });
        }

        /// <summary>
        /// The blank decision has a DOOR, and it is the facade's. Composing the required-field constraint with a
        /// refusal code and turning the verdict into a coded problem is business logic; it lived in the OpenVisual
        /// shell, where four surfaces reached for it and nothing outside that one application could.
        /// <para>The whitespace case is the one that matters: it is where the ad-hoc gates this replaced parted
        /// company, and it is why the policy travels with the decision instead of being re-chosen per surface.</para>
        /// </summary>
        [Test]
        public void TheFacadeAnswersTheBlankFieldDecisionWithACodedProblem()
        {
            var app = new ProjectAppService(TestSetup.Settings);

            Assert.Multiple(() =>
            {
                Assert.That(app.MissingRequiredField("Stue"), Is.Null, "a real value is not missing");
                Assert.That(app.MissingRequiredField("  Stue  "), Is.Null, "nor is one with room around it");
                Assert.That(app.MissingRequiredField(null), Is.Not.Null, "absent");
                Assert.That(app.MissingRequiredField(string.Empty), Is.Not.Null, "empty");
                Assert.That(app.MissingRequiredField("   "), Is.Not.Null,
                    "whitespace-only — the middle partition, and the one the replaced gates disagreed about");
                Assert.That(app.MissingRequiredField("   ")!.Code, Is.EqualTo(EditRefusalCodes.ValueRequired),
                    "it is the SDK's coded problem, so the sentence an installer reads is authored once");
                Assert.That(app.MissingRequiredField("   ")!.Message, Is.Not.Empty);
            });
        }
    }
}
