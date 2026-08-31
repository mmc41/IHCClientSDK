using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-6: the product/pin/function-block command family — AddProduct returns a resolvable id;
    /// ApplyProductDialog/UpdatePin apply their edits; UnlockFunctionBlock then Undo re-locks (E14 / W0-3 #5).
    /// </summary>
    public class ProductCommandTests : SessionCommandFixture
    {
        [Test]
        public async Task UnlockFunctionBlock_WrongTagId_IsRefused_NotFailed()
        {
            // review A5: UnlockFunctionBlock on a non-functionblock id is a clean Refused (RequireTag), not the
            // Failed the engine's FunctionBlock(id) throw would produce — matching the sibling SaveFunctionBlockToLibrary.
            Project project = await Load("project3-KompleksWired.vis");
            ElementId localityId = project.Groups.First().Id!.Value;   // a locality, not a function block
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new UnlockFunctionBlock(localityId, "me", new DateOnly(2026, 1, 1)));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        [Test]
        public async Task AddProduct_Commits_ReturnsResolvableId()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId loc = project.Groups.First().Id!.Value;
            ProductDefinition def = App.GetAvailableProducts().First(p => p.Body.Tag == "product_dataline");
            ProjectDocumentSession session = Session(project);

            EditOutcome<ElementId> outcome = session.Apply(new AddProduct(loc, def));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(outcome.Value), Is.Not.Null, "the returned id resolves to the new product");
            });
        }

        // Alignment (2026-08-11): the original IHC Visual stores a product's name = its catalog type name at insert —
        // a real vendor .vis carries e.g. <product_dataline ... name="Lampeudtag">. OpenVisual left the name empty, so
        // an un-renamed product fell back to its raw element tag ("product_airlink") in the tree. A newly inserted
        // product must carry its DisplayName as the stored name, both for the tree label and for vendor byte parity.
        [Test]
        public async Task AddProduct_StoresCatalogNameAsProductName()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId loc = project.Groups.First().Id!.Value;
            ProductDefinition def = App.GetAvailableProducts().First(p => p.Body.Tag == "product_dataline");
            ProjectDocumentSession session = Session(project);

            EditOutcome<ElementId> outcome = session.Apply(new AddProduct(loc, def));

            string? name = session.Current!.FindById(outcome.Value)!.GetAttribute("name");
            Assert.That(name, Is.EqualTo(def.DisplayName));
        }

        /// <summary>
        /// Applies a dialog edit through the one write-back (T031 — was <c>UpdateProduct_AppliesTheDto</c>).
        /// <para><c>enduser_report</c> is no longer among the assertions: the composed dialog does not offer it
        /// (the vendor shows no such control), and an unoffered field is never written. That is checked where it
        /// belongs, by <c>ProductProperties_LeavesTheEndUserReportFlagAlone</c>.</para>
        /// </summary>
        [Test]
        public async Task ApplyProductDialog_AppliesTheEdits()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement product = project.Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && !ProductClassifier.IsWireless(e.Tag) && e.Id is not null);
            ElementId id = product.Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, DialogEdits(session, id,
                ("Note", "the note"), ("Placering", "pos"), ("Kabeltype", "CT"), ("Kabelnummer", "CN"),
                ("Identifikationskode", "IDC"), ("Lysgruppe", "LG"))));

            ProjectElement updated = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed), outcome.Reason);
                Assert.That(updated.GetAttribute("note"), Is.EqualTo("the note"));
                Assert.That(updated.GetAttribute("position"), Is.EqualTo("pos"));
                Assert.That(updated.GetAttribute("documentation_tag"), Is.EqualTo("IDC"));
                Assert.That(updated.GetAttribute("cabletype"), Is.EqualTo("CT"), "a wired product carries cabling");
            });
        }

        /// <summary>Resolves dialog edits BY CAPTION against the product's own composed dialog — the same
        /// resolution the GUI does, so a caption no family offers cannot be smuggled into a write.</summary>
        private static ImmutableArray<ProductDialogEdit> DialogEdits(
            ProjectDocumentSession session, ElementId productId, params (string Caption, string Value)[] edits)
        {
            var byCaption = App.GetProductDialog(session.Current!, productId)
                .Groups.SelectMany(g => g.Fields).ToDictionary(f => f.Caption);
            return
            [
                .. edits.Select(e => byCaption.TryGetValue(e.Caption, out DialogDescriptorField? field)
                    ? new ProductDialogEdit(field.Target, field.Attribute, e.Value)
                    : throw new InvalidOperationException(
                        $"no field captioned '{e.Caption}'; offered: {string.Join(", ", byCaption.Keys)}"))
            ];
        }

        // T014 replaces the former C3 test. C3 pinned that a BAD re-parent target was refused rather than
        // silently dropped — a guard on a capability the dialog no longer has: neither properties dialog
        // re-parents now, matching the original (A-13 for the product, T014 for the modem), and moving a
        // device between localities is a tree operation (US-054).
        //
        // The guarantee worth keeping from C3 is the same one, stated positively: committing a properties
        // edit must never move the element. Deleting the test outright would have left "the dialog silently
        // moved my product" untested rather than impossible.
        [Test]
        public async Task TheProductDialog_NeverReParentsTheProduct()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement product = project.Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && e.Id is not null);
            ElementId productId = product.Id!.Value;
            ElementId currentGroup = project.FindParent(productId)!.Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new ApplyProductDialog(productId,
                DialogEdits(session, productId, ("Placering", "flyttet?"))));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindParent(productId)!.Id, Is.EqualTo(currentGroup),
                    "the product stays in the locality it was in");
                Assert.That(session.Current!.FindById(productId)!.GetAttribute("position"), Is.EqualTo("flyttet?"),
                    "Placering is free text about where it sits, not a locality that moves it");
            });
        }

        // The same guarantee for the modem, whose dialog DID re-parent until T014.
        [Test]
        public async Task TheModemDialog_NeverReParentsTheModem()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId loc = project.Groups.First().Id!.Value;
            ProductDefinition modemDef = App.GetAvailableProducts()
                .First(p => ProductClassifier.IsModem(p.Body.Tag));
            ProjectDocumentSession session = Session(project);
            ElementId modemId = session.Apply(new AddProduct(loc, modemDef)).Value;

            EditOutcome outcome = session.Apply(new ApplyProductDialog(modemId,
                DialogEdits(session, modemId, ("Placering", "i teknikskab"), ("Note", "note"))));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindParent(modemId)!.Id, Is.EqualTo(loc));
                Assert.That(session.Current!.FindById(modemId)!.GetAttribute("position"), Is.EqualTo("i teknikskab"),
                    "the modem's Placering writes the position attribute, as the product dialog's already did");
            });
        }

        // Proposal 2.1.1: UpdateProduct wrote power_group unconditionally and cabletype/cablenumber for every
        // non-wireless product, but the attribute set is per FAMILY, not per wired/wireless. The RS485 LED
        // dimmer (_0x4409) declares none of the three and is not wireless, so committing its dialog threw
        // ArgumentException from SetAttribute — a crash on the OK button of a dialog that had opened fine.
        //
        // T031 removed the class of bug rather than the instance: there is no fixed attribute list left to
        // over-write, because the dialog only OFFERS what the family declares. The regression is now stated
        // structurally — the dimmer's dialog cannot even name the three attributes, so the crash has no route.
        [Test]
        public async Task TheDialogOfAFamilyWithoutCablingAttributes_CannotEvenNameThem()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId loc = project.Groups.First().Id!.Value;
            ProductDefinition dimmer = App.GetAvailableProducts()
                .First(p => p.Body.Tag == "product_rs485_led_dimmer");
            ProjectDocumentSession session = Session(project);
            ElementId id = session.Apply(new AddProduct(loc, dimmer)).Value;

            // No Navn: this product inserts locked, and the dialog offers its name read-only.
            EditOutcome outcome = session.Apply(new ApplyProductDialog(id, DialogEdits(session, id,
                ("Placering", "i tavle"), ("Note", "note"))));

            ProjectElement updated = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed),
                    "committing the dialog of a family without cabling attributes must not throw");
                Assert.That(updated.GetAttribute("position"), Is.EqualTo("i tavle"), "the offered fields still land");
                Assert.That(updated.GetAttribute("note"), Is.EqualTo("note"));
                Assert.That(updated.GetAttribute("power_group"), Is.Null, "undeclared attributes are not invented");
                Assert.That(updated.GetAttribute("cabletype"), Is.Null);
                Assert.That(updated.GetAttribute("cablenumber"), Is.Null);
                // The crash route is closed at the source: the dialog has no such field to write through.
                Assert.That(() => DialogEdits(session, id, ("Kabeltype", "CT")),
                    Throws.InvalidOperationException, "this family's dialog offers no Kabeltype at all");
            });
        }

        // The narrowing must be driven by the SCHEMA, not by a family allow-list: a wired dataline product
        // declares all three and must still receive them. Without this, "stop throwing" could be implemented by
        // simply never writing the cabling fields, and every wired product would silently lose its cabling.
        [Test]
        public async Task TheDialogOfAFamilyThatDeclaresCabling_StillWritesIt()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId loc = project.Groups.First().Id!.Value;
            ProductDefinition wired = App.GetAvailableProducts().First(p => p.Body.Tag == "product_dataline");
            ProjectDocumentSession session = Session(project);
            ElementId id = session.Apply(new AddProduct(loc, wired)).Value;

            session.Apply(new ApplyProductDialog(id, DialogEdits(session, id,
                ("Kabeltype", "3G1,5"), ("Kabelnummer", "7"), ("Lysgruppe", "gruppe2"))));

            ProjectElement updated = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(updated.GetAttribute("cabletype"), Is.EqualTo("3G1,5"));
                Assert.That(updated.GetAttribute("cablenumber"), Is.EqualTo("7"));
                Assert.That(updated.GetAttribute("power_group"), Is.EqualTo("gruppe2"));
            });
        }

        // T012: the session Evaluate existence guards now route through EditContext.RequireExists; a stale-id command
        // must still Refuse with its command-specific noun.
        [Test]
        public async Task StaleId_Command_StillRefusesWithItsNoun()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            ElementId.TryParse("_0xdead01", out ElementId absent);

            EditOutcome outcome = session.Apply(new RenameLocality(absent, "X", ""));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused), "a stale-id command is refused, not committed");
                Assert.That(outcome.Reason, Does.Contain("Elementet").And.Contain("findes ikke længere"),
                    "the refusal keeps the command's per-noun message — in Danish since T015");
            });
        }

        [Test]
        public async Task UpdatePin_AppliesTheAddress()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement pin = project.Root.Descendants().First(e => e.Tag == "dataline_output");
            ElementId id = pin.Id!.Value;
            ProjectDocumentSession session = Session(project);
            var r = new PinPropertiesResult(DataLine: 1, Terminal: 1, CableColour: "red", Note: "n", InitialValueOn: true);

            EditOutcome outcome = session.Apply(new UpdatePin(id, r));

            ProjectElement updated = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(updated.GetAttribute("cable_colour"), Is.EqualTo("red"));
                Assert.That(updated.GetAttribute("inivalue"), Is.EqualTo("on"), "an output carries the initial value");
                Assert.That(DatalineAddress.TryParse(updated.GetAttribute("address_dataline"), isOutput: true, out _), Is.True);
            });
        }

        [Test]
        public async Task UnlockFunctionBlock_ThenUndo_ReLocks()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement fb = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes");
            ElementId id = fb.Id!.Value;
            ProjectDocumentSession session = Session(project);

            session.Apply(new UnlockFunctionBlock(id, "Test Installer", new DateOnly(2026, 1, 1)));
            Assert.That(session.Current!.FindById(id)!.GetAttribute("locked"), Is.Not.EqualTo("yes"), "unlocked");

            session.Undo();
            Assert.That(session.Current!.FindById(id)!.GetAttribute("locked"), Is.EqualTo("yes"),
                "undo re-locks the block (E14 standing regression / W0-3 #5)");
        }
    }
}
