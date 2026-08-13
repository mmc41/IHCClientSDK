using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// End-to-end over the synthetic catalog fixtures: a component IMPORTED from a hand-authored file behaves like
    /// a built-in one all the way through — it can be inserted into a project, its dialog composes, and an edit
    /// made through the generic write-back lands in the tree.
    /// <para>Worth doing over the whole chain rather than per layer: import, insert, compose and apply are four
    /// separately-tested steps, and the interesting failures live in the joins between them (an imported definition
    /// that carries no dialog, an inserted element whose descendants were re-stamped away from what the composer
    /// resolved against).</para>
    /// <para>Every fixture used here is clearly marked synthetic — <c>synthetic_</c> filename, a <c>_0x9fNN</c>
    /// identifier no real catalog uses, and a display name that says so — so nothing here can be mistaken for a
    /// real LK/Schneider product or for IHC Visual output.</para>
    /// </summary>
    public class SyntheticCatalogEndToEndTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static string Fixture(string kind, string file) => TestData.PathOf(kind, "synthetic", file);

        // ── an imported synthetic PRODUCT, end to end ───────────────────────────────────────────────

        /// <summary>
        /// The full chain for a product whose family the SDK DOES know (its root is <c>product_dataline</c>, so it
        /// has a TypeCode and can be placed): import → insert → compose → edit → read back.
        /// </summary>
        [Test]
        public async Task AnImportedSyntheticProduct_CanBeInsertedAndEdited()
        {
            ProjectAppService app = App;
            app.ImportCatalogFile(Fixture("products", "synthetic_9f02_output.def"));
            ProductDefinition imported = app.GetAvailableProducts().Single(p => p.ProductIdentifier == "_0x9f02");

            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId locality = project.Groups.First().Id!.Value;

            // insert
            EditOutcome<ElementId> inserted = session.Apply(new AddProduct(locality, imported));
            ElementId id = inserted.Value;

            // compose
            ProductDialogDescriptor dialog = app.GetProductDialog(session.Current!, id);
            DialogDescriptorField note = dialog.AllFields.Single(f => f.AutomationId == "dlg.identitet.note");

            // edit through the GENERIC write-back
            EditOutcome applied = session.Apply(new ApplyProductDialog(id,
                ImmutableArray.Create(new ProductDialogEdit(note.Target, "note", "redigeret e2e"))));

            Assert.Multiple(() =>
            {
                Assert.That(inserted.Status, Is.EqualTo(EditStatus.Committed), "the imported product inserts");
                Assert.That(ProductDialogPresets.ForRootTag(imported.Body.Tag), Is.SameAs(ProductDialogPresets.Dataline),
                    "an imported definition reaches its family preset, exactly as a built-in one does");
                Assert.That(dialog.Title, Is.EqualTo(imported.DisplayName), "titled with its own catalog name");
                Assert.That(applied.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(id)!.GetAttribute("note"), Is.EqualTo("redigeret e2e"),
                    "the edit reached the placed element");
            });
        }

        /// <summary>
        /// THE open-world chain, end to end and through the PUBLIC door: a product family the SDK has never seen
        /// is imported, inserted into a real project, and opens the minimal fallback dialog — Navn, Placering,
        /// Note, Identifikationskode, in Danish — which is then edited through the generic write-back.
        /// <para>Insert is never blocked by an unrecognised product, which is the promise the fallback exists to
        /// keep. (This was expected to be unreachable, on the assumption that a root tag without a
        /// <c>TypeCode</c> could not be placed. Measured: it can.)</para>
        /// </summary>
        [Test]
        public async Task AnImportedUnknownFamily_IsInsertedAndOpensTheMinimalFallback()
        {
            ProjectAppService app = App;
            app.ImportCatalogFile(Fixture("products", "synthetic_9f14_unknownfamily.def"));
            ProductDefinition unknown = app.GetAvailableProducts().Single(p => p.ProductIdentifier == "_0x9f14");

            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId locality = project.Groups.First().Id!.Value;

            EditOutcome<ElementId> inserted = session.Apply(new AddProduct(locality, unknown));
            ElementId id = inserted.Value;
            ProductDialogDescriptor dialog = app.GetProductDialog(session.Current!, id);
            DialogDescriptorField note = dialog.AllFields.Single(f => f.AutomationId == "dlg.identitet.note");
            EditOutcome applied = session.Apply(new ApplyProductDialog(id,
                ImmutableArray.Create(new ProductDialogEdit(note.Target, "note", "ukendt familie redigeret"))));

            Assert.Multiple(() =>
            {
                Assert.That(ProductDialogPresets.ForRootTag(unknown.Body.Tag).IsEmpty, Is.True,
                    "it resolves to the EMPTY preset — the fallback's trigger");
                Assert.That(inserted.Status, Is.EqualTo(EditStatus.Committed),
                    "insert is never blocked by an unrecognised product");
                Assert.That(dialog.AllFields.Select(f => f.Caption), Is.EqualTo(
                    new[] { "Navn", "Placering", "Note", "Identifikationskode" }).AsCollection,
                    "the minimal fallback, with Danish captions — never raw attribute names");
                Assert.That(applied.Status, Is.EqualTo(EditStatus.Committed),
                    "and the generic write-back edits it like any other product");
                Assert.That(session.Current!.FindById(id)!.GetAttribute("note"),
                    Is.EqualTo("ukendt familie redigeret"));
            });
        }

        // ── an imported synthetic FUNCTION BLOCK, end to end ────────────────────────────────────────

        /// <summary>
        /// The same chain for a function block: import → insert → edit. Function blocks carry no dialog metadata
        /// (they have no properties dialog of this kind), so the edit here is the ordinary rename — the point is
        /// that an imported synthetic block is a first-class component in a project, not that it has a dialog.
        /// </summary>
        [Test]
        public async Task AnImportedSyntheticFunctionBlock_CanBeInsertedAndEdited()
        {
            ProjectAppService app = App;
            app.ImportCatalogFile(Fixture("functionblocks", "synthetic_fb01_toggle.ifb"));
            FunctionBlockDefinition imported = app.GetAvailableFunctionBlocks()
                .OrderByDescending(fb => fb.MasterType == "9f01").First();

            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId locality = project.Groups.First().Id!.Value;

            EditOutcome<ElementId> inserted = session.Apply(new AddFunctionBlock(locality, imported));
            EditOutcome renamed = session.Apply(new RenameLocality(inserted.Value, "Omdøbt blok", "en note"));

            Assert.Multiple(() =>
            {
                Assert.That(inserted.Status, Is.EqualTo(EditStatus.Committed), "the imported block inserts");
                Assert.That(renamed.Status, Is.EqualTo(EditStatus.Committed), "and is editable in the project");
                Assert.That(session.Current!.FindById(inserted.Value)!.GetAttribute("name"),
                    Is.EqualTo("Omdøbt blok"));
            });
        }
    }
}
