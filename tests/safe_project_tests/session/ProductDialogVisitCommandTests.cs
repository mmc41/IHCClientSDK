using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// A product-dialog VISIT is one command.
    ///
    /// <para>The installer opens the dialog, steps into a terminal, addresses it, and comes back out through OK.
    /// That is one act, so it is one undo entry: <i>Fortryd</i> afterwards takes back the documentation AND the
    /// addressing. Committing the two halves separately would leave the addressing behind — an undo that half
    /// works is worse than one that does not exist, because the user has no way to tell.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProductDialogVisitCommandTests
    {
        private static (ProjectDocumentSession Session, ElementId Product, ElementId Pin) Placed()
        {
            var app = new ProjectAppService(new IhcSettings());
            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ElementId locality = project.Groups.First().Id!.Value;
            ProductDefinition definition = app.GetAvailableProducts()
                .First(p => p.ProductIdentifier == "_0x2101");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId product = session.Apply(new AddProduct(locality, definition)).Value;
            ElementId pin = session.Current!.FindById(product)!.Children
                .First(c => c.Kind == ElementKind.DatalinePin).Id!.Value;
            return (session, product, pin);
        }

        private static ProductDialogEdit NoteEdit(ProjectAppService app, Project project, ElementId product)
        {
            DialogDescriptorField field = app.GetProductDialog(project, product).AllFields
                .First(f => f.Attribute == "note" && f.Target == product && !f.ReadOnly);
            return new ProductDialogEdit(field.Target, field.Attribute, "besøgt");
        }

        [Test]
        public void OneCommandCommitsBothTheFieldEditsAndTheTerminalEdits()
        {
            (ProjectDocumentSession session, ElementId product, ElementId pin) = Placed();
            var app = new ProjectAppService(new IhcSettings());

            int before = UndoDepth(session);
            EditOutcome outcome = session.Apply(app.Commands.ApplyProductDialogVisit(
                session.Current!, product,
                [NoteEdit(app, session.Current!, product)],
                [new ProductDialogTerminalEdit(pin,
                    new PinPropertiesResult(DataLine: 2, Terminal: 3, CableColour: "Grøn", Note: "klemme",
                        InitialValueOn: false))]));

            Project after = session.Current!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(after.FindById(product)!.GetAttribute("note"), Is.EqualTo("besøgt"),
                    "the product half");
                Assert.That(after.FindById(pin)!.GetAttribute("cable_colour"), Is.EqualTo("Grøn"),
                    "and the terminal half");
                Assert.That(after.FindById(pin)!.GetAttribute("address_dataline"), Is.Not.Null);
                Assert.That(UndoDepth(session) - before, Is.EqualTo(1),
                    "ONE entry, because the installer performed one act");
            });
        }

        /// <summary>The whole visit comes back together — not the documentation with the addressing left behind.</summary>
        [Test]
        public void UndoingTheVisitTakesBackBothHalves()
        {
            (ProjectDocumentSession session, ElementId product, ElementId pin) = Placed();
            var app = new ProjectAppService(new IhcSettings());
            string? colourBefore = session.Current!.FindById(pin)!.GetAttribute("cable_colour");

            session.Apply(app.Commands.ApplyProductDialogVisit(
                session.Current!, product,
                [NoteEdit(app, session.Current!, product)],
                [new ProductDialogTerminalEdit(pin,
                    new PinPropertiesResult(2, 3, "Grøn", "klemme", InitialValueOn: false))]));
            session.Undo();

            Project after = session.Current!;
            Assert.Multiple(() =>
            {
                Assert.That(after.FindById(product)!.GetAttribute("note"), Is.Not.EqualTo("besøgt"));
                Assert.That(after.FindById(pin)!.GetAttribute("cable_colour"), Is.EqualTo(colourBefore),
                    "one Fortryd, both halves — an undo that took back only the documentation would leave the "
                    + "terminal addressed by an edit the user has just reversed");
            });
        }

        /// <summary>
        /// The terminal editor's own address rule still holds. A visit must not become a side door for writing an
        /// address the editor would have refused.
        /// </summary>
        [Test]
        public void AnInvalidAddressIsStillRefused()
        {
            (ProjectDocumentSession session, ElementId product, ElementId pin) = Placed();
            var app = new ProjectAppService(new IhcSettings());
            // A field edit that WOULD have been accepted, so the refusal is shown to discard the whole visit
            // rather than merely to skip the bad terminal.
            ProductDialogEdit note = NoteEdit(app, session.Current!, product);
            string? noteBefore = session.Current!.FindById(product)!.GetAttribute("note");

            EditOutcome outcome = session.Apply(app.Commands.ApplyProductDialogVisit(
                session.Current!, product, [note],
                [new ProductDialogTerminalEdit(pin,
                    new PinPropertiesResult(DataLine: 99, Terminal: 99, CableColour: "Grøn", Note: "",
                        InitialValueOn: false))]));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(session.Current!.FindById(product)!.GetAttribute("note"), Is.EqualTo(noteBefore),
                    "the VALID half was not written either — a refusal is all-or-nothing, or a rejected visit "
                    + "would leave the document half-edited with nothing saying so");
                Assert.That(session.Current!.FindById(pin)!.GetAttribute("cable_colour"),
                    Is.Not.EqualTo("Grøn"));
            });
        }

        /// <summary>
        /// A terminal belonging to a DIFFERENT product is refused, exactly as a stray field edit is. Without it a
        /// visit could address someone else's terminal and report success.
        /// </summary>
        [Test]
        public void ATerminalOutsideTheProductIsRefused()
        {
            (ProjectDocumentSession session, ElementId product, _) = Placed();
            var app = new ProjectAppService(new IhcSettings());
            ProductDefinition definition = app.GetAvailableProducts()
                .First(p => p.ProductIdentifier == "_0x2101");
            ElementId locality = session.Current!.Groups.First().Id!.Value;
            ElementId other = session.Apply(new AddProduct(locality, definition)).Value;
            ElementId foreignPin = session.Current!.FindById(other)!.Children
                .First(c => c.Kind == ElementKind.DatalinePin).Id!.Value;

            EditOutcome outcome = session.Apply(app.Commands.ApplyProductDialogVisit(
                session.Current!, product, [],
                [new ProductDialogTerminalEdit(foreignPin,
                    new PinPropertiesResult(1, 1, "Grøn", "", InitialValueOn: false))]));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        /// <summary>A visit with no terminal edits behaves exactly as the plain apply always did.</summary>
        [Test]
        public void AVisitThatSteppedIntoNoTerminalIsTheOrdinaryApply()
        {
            (ProjectDocumentSession session, ElementId product, _) = Placed();
            var app = new ProjectAppService(new IhcSettings());

            EditOutcome outcome = session.Apply(app.Commands.ApplyProductDialogVisit(
                session.Current!, product, [NoteEdit(app, session.Current!, product)], []));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(product)!.GetAttribute("note"), Is.EqualTo("besøgt"));
            });
        }

        private static int UndoDepth(ProjectDocumentSession session)
        {
            int depth = 0;
            // Counted by exhausting a CLONE of the history would need a second session; instead count how many
            // undos the session will accept, then redo back to where it was. The redo stack restores exactly what
            // the undos took, so the session is left as it was found.
            while (session.CanUndo)
            {
                session.Undo();
                depth++;
            }
            for (int i = 0; i < depth; i++)
            {
                session.Redo();
            }
            return depth;
        }
    }
}
