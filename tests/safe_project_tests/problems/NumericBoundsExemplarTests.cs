using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// NUMERIC BOUNDS — the proving case for one definition serving both engine faces, and the fix for the defect
    /// that motivated the whole engine.
    ///
    /// <para><b>The defect, precisely.</b> The product-dialog composer derives each numeric field's minimum and
    /// maximum from the placed element (the catalog grammar declares them, so a preset cannot go stale), the
    /// view-model re-exposes them, and then nothing reads them: the number template binds only the value, and the
    /// commit check consults the field's string rule and stops one line short of its bounds. A SIM PIN of 99999
    /// therefore commits. That is closable by ONE added condition on the commit path, and this fixture lands that
    /// first and separately, because it is a real user-facing fix and it should be reviewable without an engine
    /// attached to it.</para>
    ///
    /// <para><b>What the engine then adds that the commit check never could.</b> A commit check only sees values
    /// arriving through a dialog. A value that arrived by IMPORT or by hand-editing the file is already in the
    /// project and no commit-time check will ever look at it — which is precisely the population the whole-project
    /// face exists for. Both faces read the SAME constraint object here, so a bound cannot be enforced in one
    /// place and advertised differently in another.</para>
    ///
    /// <para>Boundary values are tested at, inside and outside each bound, on every face.</para>
    /// </summary>
    [TestFixture]
    public sealed class NumericBoundsExemplarTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>A fresh project with the SMS modem placed — the one shipped product with a bounded number field.</summary>
        private static Task<(ProjectDocumentSession Session, ElementId ProductId, DialogDescriptorField Pin)> WithModem() =>
            PlacedModem.Open();

        private static ProductDialogEdit Edit(DialogDescriptorField field, string value) =>
            new(field.Target, field.Attribute, value);

        // ── the FIELD-METADATA face: the dialog already knows the bounds ────────────────────────────

        [Test]
        public async Task TheDialogAdvertisesTheBoundsTheCatalogDeclares()
        {
            (_, _, DialogDescriptorField pin) = await WithModem();

            Assert.Multiple(() =>
            {
                Assert.That(pin.Control, Is.EqualTo(DialogControlKind.Number));
                Assert.That(pin.Minimum, Is.EqualTo(0));
                Assert.That(pin.Maximum, Is.EqualTo(9999),
                    "derived from the placed element, so a catalog change cannot leave a preset stale");
            });
        }

        // ── the SESSION command face: the commit path now reads them ────────────────────────────────

        [Test]
        public async Task AValueAboveTheMaximumIsRefusedAtCommit()
        {
            (ProjectDocumentSession session, ElementId id, DialogDescriptorField pin) = await WithModem();

            EditVerdict verdict = session.CanApply(
                new ApplyProductDialog(id, EquatableArray.Create<ProductDialogEdit>([Edit(pin, "10000")])));

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Ok, Is.False, "9999 is the declared maximum; 10000 must not commit");
                Assert.That(verdict.Reason, Does.Contain(pin.Caption), "the refusal names the field the user was editing");
                Assert.That(verdict.Reason, Does.Contain("9999"), "the refusal names the bound it broke");
            });
        }

        [Test]
        public async Task AValueBelowTheMinimumIsRefusedAtCommit()
        {
            (ProjectDocumentSession session, ElementId id, DialogDescriptorField pin) = await WithModem();

            EditVerdict verdict = session.CanApply(
                new ApplyProductDialog(id, EquatableArray.Create<ProductDialogEdit>([Edit(pin, "-1")])));

            Assert.That(verdict.Ok, Is.False, "0 is the declared minimum");
        }

        /// <summary>Boundary values: at each bound, and one inside each, must all commit.</summary>
        [Test]
        public async Task TheBoundsThemselvesAndEveryValueBetweenThemCommit()
        {
            Assert.Multiple(async () =>
            {
                foreach (string acceptable in new[] { "0", "1", "5000", "9998", "9999" })
                {
                    (ProjectDocumentSession session, ElementId id, DialogDescriptorField pin) = await WithModem();
                    EditOutcome outcome = session.Apply(
                        new ApplyProductDialog(id, EquatableArray.Create<ProductDialogEdit>([Edit(pin, acceptable)])));

                    Assert.That(outcome.Status, Is.Not.EqualTo(EditStatus.Refused), acceptable);
                }
            });
        }

        /// <summary>
        /// A BLANK numeric field means "at the declared default" — the composer shows blank there and committing
        /// blank writes the default back. Refusing it would break the round trip the dialog is built on.
        /// </summary>
        [Test]
        public async Task ABlankNumericFieldIsStillAccepted()
        {
            (ProjectDocumentSession session, ElementId id, DialogDescriptorField pin) = await WithModem();

            EditOutcome outcome = session.Apply(
                new ApplyProductDialog(id, EquatableArray.Create<ProductDialogEdit>([Edit(pin, string.Empty)])));

            Assert.That(outcome.Status, Is.Not.EqualTo(EditStatus.Refused),
                "blank is the declared default, not an out-of-range value");
        }

        /// <summary>
        /// The hole beside the bounds check: <c>int.TryParse</c> failing used to mean "no bounds violation", so a
        /// value that is not a number at all fell through every gate and was written into the project verbatim.
        /// OpenVisual binds a <c>NumericUpDown</c> so its own dialog cannot produce one — but the SDK command is
        /// a public door, and a field whose catalog element DECLARES bounds is a field the catalog says holds a
        /// number.
        /// </summary>
        [TestCase("abc")]
        [TestCase("12abc")]
        [TestCase("1.5")]
        [TestCase("1,5")]
        [TestCase("0x10")]
        [TestCase("١٢٣")]
        public async Task ANonNumericValueInABoundedFieldIsRefusedAtCommit(string submitted)
        {
            (ProjectDocumentSession session, ElementId id, DialogDescriptorField pin) = await WithModem();

            EditVerdict verdict = session.CanApply(
                new ApplyProductDialog(id, EquatableArray.Create<ProductDialogEdit>([Edit(pin, submitted)])));

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Ok, Is.False, $"'{submitted}' is not a number the field can hold");
                Assert.That(verdict.Code, Is.EqualTo(EditRefusalCodes.FieldNotANumber));
                Assert.That(verdict.Reason, Does.Contain(pin.Caption), "the refusal names the field");
                Assert.That(verdict.Reason, Does.Contain(submitted), "the refusal quotes what was submitted");
            });
        }

        /// <summary>
        /// The second, independent check: the refusal happens BEFORE the write, so the project the session holds
        /// still serializes to the bytes it had. A refusal that fired after the edit was applied would leave the
        /// non-numeric value in the file whatever verdict it returned.
        /// </summary>
        [Test]
        public async Task ARefusedNonNumericValueLeavesTheProjectBytesUnchanged()
        {
            (ProjectDocumentSession session, ElementId id, DialogDescriptorField pin) = await WithModem();
            byte[] before = ProjectSerializer.Serialize(session.Current!);

            EditOutcome outcome = session.Apply(
                new ApplyProductDialog(id, EquatableArray.Create<ProductDialogEdit>([Edit(pin, "abc")])));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(ProjectSerializer.Serialize(session.Current!), Is.EqualTo(before));
            });
        }

        /// <summary>
        /// The refusal is scoped to fields the catalog says are numeric. A free-text field still takes free text,
        /// which is what keeps the guard from becoming "every dialog field must be a number".
        /// </summary>
        [Test]
        public async Task AnUnboundedFieldStillAcceptsFreeText()
        {
            (ProjectDocumentSession session, ElementId id, _) = await WithModem();

            DialogDescriptorField free = App.GetProductDialog(session.Current!, id).AllFields
                .First(f => !f.ReadOnly && f.Rule is null && f.Minimum is null && f.Maximum is null);

            EditVerdict verdict = session.CanApply(
                new ApplyProductDialog(id, EquatableArray.Create<ProductDialogEdit>([Edit(free, "ikke et tal")])));

            Assert.That(verdict.Ok, Is.True, "a field the catalog declares no bounds for is not a numeric field");
        }

        /// <summary>
        /// A field with no declared bounds is unbounded, not implicitly zero-based. The S0 pulse count declares
        /// neither a minimum nor a maximum, and inventing one here would be the engine making up a limit.
        /// </summary>
        [Test]
        public async Task AFieldWithNoDeclaredBoundsIsNotConstrained()
        {
            ProjectAppService app = App;
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            ProductDefinition s0 = app.GetAvailableProducts().FirstOrDefault(p => p.ProductIdentifier == "_0x2601")
                ?? app.GetAvailableProducts().First(p => p.ProductIdentifier == "_0x3103");

            ProjectDocumentSession session = new();
            session.Open(project);
            ElementId id = session.Apply(new AddProduct(locality, s0)).Value;

            DialogDescriptorField[] unbounded = [.. app.GetProductDialog(session.Current!, id).AllFields
                .Where(f => f.Control == DialogControlKind.Number && f.Minimum is null && f.Maximum is null)];

            Assert.That(unbounded.All(f => f.Minimum is null && f.Maximum is null), Is.True,
                "an undeclared bound stays undeclared — the engine does not invent a limit");
        }

        // ── the FINDINGS face: the same bounds, over values no commit check ever sees ───────────────

        /// <summary>
        /// The capability the engine genuinely adds. A commit check only ever sees a value passing through a
        /// dialog; this one is already in the project, as it would be after an import or a hand edit.
        /// </summary>
        [Test]
        public void TheWholeProjectFaceCatchesAValueThatArrivedWithoutADialog()
        {
            ProjectElement pin = Tree.Node("sms_modem_pincode", "_0x81",
                [("value", "99999"), ("minimum", "0"), ("maximum", "9999")]);
            Project project = new(Tree.Node("utcs_project", null, [], pin));

            ProblemCatalogEntry entry = new(
                new ProblemCode("dev-setting-out-of-range"), ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings, CatalogDisposition.Error, RuleKind.UserContentRule,
                RuleFaces.WholeProject | RuleFaces.DialogMetadata,
                new RuleTarget("sms_modem_pincode", "value"), FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("maximum", ProblemArgumentType.Integer),
                ]),
                "Værdien {value} er over grænsen {maximum}");

            RuleSet rules = RuleSet.Create(
                ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>([entry])),
                [new RuleBuilder(entry).Constrain(new PinBounds()).Build()]);

            ValidationFinding finding = new WholeProjectValidator(rules)
                .Validate(project, ValidationProfile.ProjectOnly).Findings.Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Code.Value, Is.EqualTo("dev-setting-out-of-range"));
                Assert.That(finding.Problem.Message, Is.EqualTo("Værdien 99999 er over grænsen 9999"),
                    "the bound travels as DATA and the Danish template binds it");
                Assert.That(finding.Primary!.Locator, Is.EqualTo("_0x81"));
            });
        }

        /// <summary>
        /// ONE definition, both faces. The same object answers "is this acceptable?" for the validator and
        /// "what would be acceptable?" for the dialog, so the two cannot be given different bounds.
        /// </summary>
        [Test]
        public void OneConstraintAnswersBothTheValidatorAndTheDialog()
        {
            IValueConstraint bounds = new PinBounds();
            FieldConstraintMetadata advertised = bounds.Describe();

            Assert.Multiple(() =>
            {
                Assert.That(advertised.Minimum, Is.EqualTo(0));
                Assert.That(advertised.Maximum, Is.EqualTo(9999));

                Assert.That(bounds.Check("0").Satisfied, Is.True, "at the minimum");
                Assert.That(bounds.Check("9999").Satisfied, Is.True, "at the maximum");
                Assert.That(bounds.Check("5000").Satisfied, Is.True, "inside");
                Assert.That(bounds.Check("-1").Satisfied, Is.False, "one below");
                Assert.That(bounds.Check("10000").Satisfied, Is.False, "one above");
                Assert.That(bounds.Check(null).Satisfied, Is.True, "absent is the declared default, not a violation");
            });
        }

        /// <summary>The exemplar every later range rule follows: bounds as data, verdict and description off one object.</summary>
        private sealed class PinBounds : IValueConstraint
        {
            public ProblemCode Code => new("dev-setting-out-of-range");

            public ValueConstraintVerdict Check(string? rawValue)
            {
                if (string.IsNullOrEmpty(rawValue) || !int.TryParse(rawValue, out int value))
                {
                    return ValueConstraintVerdict.Ok;
                }

                return value is >= 0 and <= 9999
                    ? ValueConstraintVerdict.Ok
                    : ValueConstraintVerdict.Failed(EquatableArray.Create<ProblemArgument>(
                    [
                        new ProblemArgument("value", rawValue),
                        new ProblemArgument("maximum", 9999),
                    ]));
            }

            public FieldConstraintMetadata Describe() =>
                FieldConstraintMetadata.Unconstrained with { Minimum = 0, Maximum = 9999 };
        }
    }
}
