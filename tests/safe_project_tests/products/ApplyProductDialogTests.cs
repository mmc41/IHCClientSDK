using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// <see cref="ApplyProductDialog"/> — the generic dialog write-back. One undoable commit over a flat list of
    /// pre-resolved edits, whatever family the dialog belonged to.
    /// </summary>
    public class ApplyProductDialogTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static async Task<(ProjectDocumentSession Session, ElementId Id)> Placed(string productIdentifier)
        {
            ProjectAppService app = App;
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            ProductDefinition definition = app.GetAvailableProducts()
                .First(p => p.ProductIdentifier == productIdentifier);
            var session = new ProjectDocumentSession();
            session.Open(project);
            return (session, session.Apply(new AddProduct(locality, definition)).Value);
        }

        private static ImmutableArray<ProductDialogEdit> Edits(params ProductDialogEdit[] edits) => [.. edits];

        private static DialogDescriptorField Field(ProductDialogDescriptor d, string suffix) =>
            d.AllFields.Single(f => f.AutomationId.EndsWith(suffix, System.StringComparison.Ordinal));

        // ── the happy path ──────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task ItWritesEveryEditAsOneCommit()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x2101");
            ProductDialogDescriptor dialog = App.GetProductDialog(session.Current!, id);

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(Field(dialog, "identitet.note").Target, "note", "en note"),
                new ProductDialogEdit(Field(dialog, "identitet.placering").Target, "position", "i loft"))));

            ProjectElement product = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed), "refusal reason: " + outcome.Reason);
                Assert.That(product.GetAttribute("note"), Is.EqualTo("en note"));
                Assert.That(product.GetAttribute("position"), Is.EqualTo("i loft"));
            });
        }

        /// <summary>One commit, so ONE undo restores everything the dialog changed.</summary>
        [Test]
        public async Task OneUndoRestoresTheWholeDialog()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x2101");
            ProductDialogDescriptor dialog = App.GetProductDialog(session.Current!, id);
            session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(Field(dialog, "identitet.note").Target, "note", "en note"),
                new ProductDialogEdit(Field(dialog, "identitet.placering").Target, "position", "i loft"))));

            session.Undo();

            ProjectElement product = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(product.GetAttribute("note"), Is.Not.EqualTo("en note"));
                Assert.That(product.GetAttribute("position"), Is.Not.EqualTo("i loft"));
            });
        }

        /// <summary>It reaches a DESCENDANT the same way it reaches the root — that is the point of the triples.</summary>
        [Test]
        public async Task ItWritesADescendantTarget()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x3103");
            ProductDialogDescriptor dialog = App.GetProductDialog(session.Current!, id);
            DialogDescriptorField slot = dialog.Groups.Single(g => g.Id == "telefonnumre").Fields[16];

            session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(slot.Target, "phonenumber", "+4570100017"))));

            Assert.That(session.Current!.FindById(slot.Target)!.GetAttribute("phonenumber"),
                Is.EqualTo("+4570100017"));
        }

        // ── the gate: OK with no edits ──────────────────────────────────────────────────────────────

        /// <summary>
        /// THE gate of T024. Pressing OK without touching anything produces no change — and the insert flow must
        /// treat that as a COMMIT, not a cancellation. If <c>NoChange</c> were read as "the installer declined",
        /// the insert would be rolled back and a product the installer just placed would silently vanish.
        /// </summary>
        [Test]
        public async Task OkWithNoEdits_IsNoChange_AndKeepsTheInsertedProduct()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x2101");

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, Edits()));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.NoChange), "nothing was edited, so nothing changed");
                Assert.That(outcome.Status, Is.Not.EqualTo(EditStatus.Refused), "and it is NOT a refusal");
                Assert.That(session.Current!.FindById(id), Is.Not.Null, "the just-inserted product survives");
            });
        }

        /// <summary>Re-submitting the values the dialog already showed is also a no-op, not a spurious commit.</summary>
        [Test]
        public async Task ReSubmittingUnchangedValues_IsNoChange()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x2101");
            ProductDialogDescriptor dialog = App.GetProductDialog(session.Current!, id);
            DialogDescriptorField note = Field(dialog, "identitet.note");

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(note.Target, "note", note.Value ?? string.Empty))));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.NoChange));
        }

        // ── validation ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// An edit naming an element OUTSIDE the product is refused. Not a technicality: without it a dialog handed
        /// a stale or foreign id would edit a DIFFERENT product and report success.
        /// </summary>
        [Test]
        public async Task AnEditOutsideTheProductsSubtree_IsRefused()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x2101");
            ElementId otherProduct = session.Current!.Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && e.Id is not null && e.Id.Value != id).Id!.Value;

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(otherProduct, "note", "kapret"))));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("uden for produktet"));
                Assert.That(session.Current!.FindById(otherProduct)!.GetAttribute("note"), Is.Not.EqualTo("kapret"),
                    "and the other product is untouched");
            });
        }

        [Test]
        public async Task AnEditNamingAMissingElement_IsRefused()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x2101");
            ElementId.TryParse("_0xdead01", out ElementId absent);

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(absent, "note", "x"))));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("ikke findes længere"));
            });
        }

        /// <summary>
        /// The value rule is enforced from the PRESET, not from anything the caller supplies — a caller that could
        /// pass its own rule could also omit it.
        /// <para>The phone-number rule refuses under a code OF ITS OWN. The generic <c>edit.field-value-rule</c>
        /// governs the sentence <i>"Feltet {field} har en ugyldig værdi."</i>, which is not the sentence this site
        /// shows and has no slot the offending value could bind to — so raising it here would anchor a specific
        /// guidance to a template that does not govern it.</para>
        /// </summary>
        [Test]
        public async Task AValueBreakingItsRule_IsRefused()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x3103");
            DialogDescriptorField slot = App.GetProductDialog(session.Current!, id)
                .Groups.Single(g => g.Id == "telefonnumre").Fields[0];
            ApplyProductDialog command = new(id, Edits(
                new ProductDialogEdit(slot.Target, "phonenumber", "12")));   // below the 3-character minimum

            EditVerdict verdict = session.CanApply(command);
            EditOutcome outcome = session.Apply(command);

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(verdict.Code, Is.EqualTo(EditRefusalCodes.FieldPhonenumberMalformed),
                    "the code a catalogue entry governs, not the generic field-value one");
                Assert.That(outcome.Reason, Is.EqualTo(
                    "Telefonnummeret '12' skal være på 3-20 tegn uden mellemrum og begynde med en landekode, "
                    + "f.eks. +45."),
                    "the same specific guidance as before, now naming the offending value");
            });
        }

        /// <summary>
        /// An attribute the dialog does not offer is refused — the dialog's field set is the contract.
        /// <para>The example stays <c>enduser_report</c>, and it got sharper on 2026-08-12 (T099): that
        /// attribute IS offered now, as a checkbox, on the ONE product measured to show one. So this no longer
        /// says "an attribute no dialog offers" but the stronger "an attribute THIS product's dialog does not
        /// offer" — the refusal is per-product, and it is per-product because the validator composes from the
        /// element's own <c>product_identifier</c> rather than from a flag it was handed.</para>
        /// </summary>
        [Test]
        public async Task AnAttributeTheDialogDoesNotOffer_IsRefused()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x2101");

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(id, "enduser_report", "yes"))));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                    "the vendor's dialog for THIS product shows no such field");
                Assert.That(outcome.Reason, Does.Contain("enduser_report"));
            });
        }

        /// <summary>A read-only field cannot be written through the generic channel either.</summary>
        [Test]
        public async Task AnEditToAReadOnlyField_IsRefused()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x3103");

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(id, "name", "Omdøbt"))));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("Navn"));
            });
        }

        /// <summary>A refusal writes NOTHING — the whole dialog is one commit, so a bad field aborts all of it.</summary>
        [Test]
        public async Task ARefusedDialogWritesNoneOfItsOtherEdits()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x3103");
            ProductDialogDescriptor dialog = App.GetProductDialog(session.Current!, id);
            DialogDescriptorField slot = dialog.Groups.Single(g => g.Id == "telefonnumre").Fields[0];

            session.Apply(new ApplyProductDialog(id, Edits(
                new ProductDialogEdit(Field(dialog, "identitet.note").Target, "note", "burde ikke landes"),
                new ProductDialogEdit(slot.Target, "phonenumber", "12"))));   // refused

            Assert.That(session.Current!.FindById(id)!.GetAttribute("note"), Is.Not.EqualTo("burde ikke landes"));
        }

        // ── the widget-action channel ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The widget action is CARRIED, never executed: the composite sub-dialogs keep their own commands (D05).
        /// One typed slot replaces the old pair of flags, which could contradict each other.
        /// </summary>
        [Test]
        public async Task AWidgetActionIsCarriedButNotExecuted()
        {
            (ProjectDocumentSession session, ElementId id) = await Placed("_0x2101");
            var command = new ApplyProductDialog(id, Edits(),
                new ProductDialogWidgetAction(DialogWidgetKind.TerminalGrids, id));

            EditOutcome outcome = session.Apply(command);

            Assert.Multiple(() =>
            {
                Assert.That(command.WidgetAction!.Value.Kind, Is.EqualTo(DialogWidgetKind.TerminalGrids));
                Assert.That(command.WidgetAction!.Value.Target, Is.EqualTo(id));
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.NoChange),
                    "carrying an action is not itself an edit");
            });
        }
    }
}
