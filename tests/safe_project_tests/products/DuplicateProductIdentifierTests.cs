using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// D22: catalog product identifiers are NOT unique — eight of them each name two or three different products
    /// under different names and categories, e.g. `LK FUGA Tryk 4 tast` and `LK OPUS Tryk 4 tast` both answer to
    /// `_0x2102`. <see cref="SharedIdentifiers"/> is the census.
    ///
    /// <para>Found on product 012's vendor comparison (T046): the vendor's `LK OPUS Tryk 4 tast` dialog is
    /// titled and named for OPUS, while OpenVisual's said `LK FUGA Tryk 4 tast`. The insert menu leaf carries
    /// only the identifier and the command factory resolved it with <c>FirstOrDefault</c>, so **choosing the
    /// OPUS entry placed the FUGA product** — a different product, with its own terminals, written into the
    /// saved project under the wrong name.</para>
    ///
    /// <para>This is not a dialog defect. The dialog rendered exactly what it was given; the wrong product was
    /// given to it. It surfaced through the dialog because the dialog is where a product finally says its own
    /// name out loud, which is the argument for comparing dialogs product by product rather than family by
    /// family.</para>
    /// </summary>
    public class DuplicateProductIdentifierTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>
        /// The collision census — every shared identifier and every product that answers to it, stated rather
        /// than left to be inferred. Eight groups, and <c>_0x4408</c> has <b>THREE</b> members, so any resolver
        /// that takes a fixed number of candidates is wrong for that one: <c>ProductCatalogLookup</c>'s
        /// <c>Take(2)</c> still cannot reach <c>WindowMaster WUC 102</c>. Fixing that lookup is separate work —
        /// this table is what makes the defect visible, not a test of it.
        /// </summary>
        private static readonly (string Identifier, string[] Members)[] SharedIdentifiers =
        [
            ("_0x2102",     ["LK FUGA Tryk 4 tast", "LK OPUS Tryk 4 tast"]),
            ("_0x2108",     ["LK FUGA Statustryk 4 tast 4 dioder", "LK OPUS Statustryk 4 tast 4 dioder"]),
            ("_0x2302",     ["Output 1-10V IHC/SA", "UniDimmer 2-tast betjent"]),
            ("_0x21000007", ["Dimmer 350LR/600CR/1000LR", "Velux KLF-100"]),
            ("_0x4304",     ["Lampeudtag dimmer", "1-10v converter - Lampeudtag dimmer"]),
            ("_0x4306",     ["Dimmer Universal", "1-10v converter - Dimmer Universal"]),
            ("_0x4406",     ["Kombi dimmer 4 tast", "1-10v converter - Kombi dimmer 4 tast"]),
            ("_0x4408",     ["Mod. kombi Wireless 4 tast", "WindowMaster WUC 101", "WindowMaster WUC 102"]),
        ];

        /// <summary>The premise, in full. If the catalog ever stops carrying duplicates, the tests below are moot
        /// and should be removed rather than left passing vacuously. Two-way set equality, so a NEW collision —
        /// a product added under an identifier already in use — fails here rather than quietly widening a group
        /// callers elsewhere believe they have enumerated.</summary>
        [Test]
        public void TheCatalogReallyDoesCarryDuplicateIdentifiers()
        {
            var duplicated = App.GetAvailableProducts()
                .GroupBy(p => p.ProductIdentifier)
                .Where(g => g.Count() > 1)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(duplicated.Select(g => g.Key), Is.EquivalentTo(SharedIdentifiers.Select(s => s.Identifier)),
                    "D22: exactly these identifiers name more than one product");
                foreach ((string identifier, string[] members) in SharedIdentifiers)
                {
                    Assert.That(duplicated.SingleOrDefault(g => g.Key == identifier)?.Select(p => p.DisplayName),
                        Is.EquivalentTo(members), identifier);
                }
            });
        }

        /// <summary>
        /// THE finding: asking for a product by identifier ALONE cannot say which of them is meant, so the
        /// insert path must be able to name one exactly.
        /// </summary>
        [Test]
        public async Task InsertingTheOpusVariant_PlacesTheOpusProduct_NotTheFuga()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            var session = new ProjectDocumentSession();
            session.Open(project);

            ProductDefinition opus = App.GetAvailableProducts()
                .Single(p => p.ProductIdentifier == "_0x2102" && p.DisplayName == "LK OPUS Tryk 4 tast");
            AddProduct? command = App.Commands.AddProduct(project, locality, opus);
            Assert.That(command, Is.Not.Null);

            ElementId placed = session.Apply(command!).Value;

            Assert.That(session.Current!.FindById(placed)!.GetAttribute("name"),
                Is.EqualTo("LK OPUS Tryk 4 tast"),
                "choosing the OPUS menu entry must not place the FUGA product");
        }

        /// <summary>And the same for the other one, so a fix cannot be "always take the last" either.</summary>
        [Test]
        public async Task InsertingTheFugaVariant_StillPlacesTheFuga()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            var session = new ProjectDocumentSession();
            session.Open(project);

            ProductDefinition fuga = App.GetAvailableProducts()
                .Single(p => p.ProductIdentifier == "_0x2102" && p.DisplayName == "LK FUGA Tryk 4 tast");

            ElementId placed = session.Apply(App.Commands.AddProduct(project, locality, fuga)!).Value;

            Assert.That(session.Current!.FindById(placed)!.GetAttribute("name"),
                Is.EqualTo("LK FUGA Tryk 4 tast"));
        }

        /// <summary>
        /// The dialog TITLE has the same problem, one layer up. It is the product TYPE, looked up in the catalog
        /// by identifier — which for a shared identifier cannot say which type. Titling the OPUS product's dialog
        /// "LK FUGA Tryk 4 tast" is what the vendor comparison caught after the insert itself was fixed.
        /// <para>The lookup already falls back to the ELEMENT's own name for an identifier the catalog does not
        /// know (the open-world case). An AMBIGUOUS identifier is the same situation — the catalog cannot answer —
        /// so it takes the same fallback. Sound here because these products insert <c>locked</c>, which fixes the
        /// stored name to the type name; a renamed product would show its own name, and the vendor's does too.</para>
        /// </summary>
        [Test]
        public async Task TheOpusVariantsDialog_IsTitledForOpus()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            var session = new ProjectDocumentSession();
            session.Open(project);

            ProductDefinition opus = App.GetAvailableProducts()
                .Single(p => p.ProductIdentifier == "_0x2102" && p.DisplayName == "LK OPUS Tryk 4 tast");
            ElementId placed = session.Apply(App.Commands.AddProduct(project, locality, opus)!).Value;

            Assert.That(App.GetProductDialog(session.Current!, placed).Title,
                Is.EqualTo("LK OPUS Tryk 4 tast"));
        }

        /// <summary>An UNambiguous identifier still takes its title from the catalog type, not from the element —
        /// so a renamed product keeps showing its type, which is the behaviour the fallback must not break.</summary>
        [Test]
        public async Task AnUnambiguousProduct_IsStillTitledByItsCatalogType_EvenWhenRenamed()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId placed = session.Apply(App.Commands.AddProduct(project, locality, "_0x2701")!).Value;

            DialogDescriptorField navn = App.GetProductDialog(session.Current!, placed)
                .Groups.SelectMany(g => g.Fields).First(f => f.Caption == "Navn");
            session.Apply(new ApplyProductDialog(placed,
                [new ProductDialogEdit(navn.Target, navn.Attribute, "Køkkentryk")]));

            Assert.That(App.GetProductDialog(session.Current!, placed).Title,
                Is.Not.EqualTo("Køkkentryk"), "the title is the TYPE, not the installer's name for it");
        }

        /// <summary>
        /// The identifier-only factory is kept for the 83 unambiguous products, but it must REFUSE rather than
        /// guess when the identifier names more than one. A silent <c>FirstOrDefault</c> is what put the wrong
        /// product in the file; returning null makes the caller say which it meant.
        /// </summary>
        [Test]
        public async Task TheIdentifierOnlyFactory_RefusesAnAmbiguousIdentifier()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(App.Commands.AddProduct(project, locality, "_0x2102"), Is.Null,
                    "_0x2102 names two products; guessing one of them is what T046 found");
                Assert.That(App.Commands.AddProduct(project, locality, "_0x2101"), Is.Not.Null,
                    "an unambiguous identifier still resolves");
            });
        }
    }
}
