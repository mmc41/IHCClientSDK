using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-6: the product/pin/function-block command family — AddProduct returns a resolvable id;
    /// UpdateProduct/UpdatePin apply their DTOs; UnlockFunctionBlock then Undo re-locks (E14 / W0-3 #5).
    /// </summary>
    public class ProductCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
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

        [Test]
        public async Task UpdateProduct_AppliesTheDto()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement product = project.Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && !ProductClassifier.IsWireless(e.Tag) && e.Id is not null);
            ElementId id = product.Id!.Value;
            ProjectDocumentSession session = Session(project);
            var r = new ProductPropertiesResult(
                "NewName", "", "the note", "CT", "CN", "IDC", "LG", Position: "pos", EndUserReport: true);

            EditOutcome outcome = session.Apply(new UpdateProduct(id, r, CurrentLocalityId: null));

            ProjectElement updated = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(updated.GetAttribute("name"), Is.EqualTo("NewName"));
                Assert.That(updated.GetAttribute("note"), Is.EqualTo("the note"));
                Assert.That(updated.GetAttribute("position"), Is.EqualTo("pos"));
                Assert.That(updated.GetAttribute("enduser_report"), Is.EqualTo("yes"));
                Assert.That(updated.GetAttribute("cabletype"), Is.EqualTo("CT"), "a wired product carries cabling");
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

            session.Apply(new UnlockFunctionBlock(id));
            Assert.That(session.Current!.FindById(id)!.GetAttribute("locked"), Is.Not.EqualTo("yes"), "unlocked");

            session.Undo();
            Assert.That(session.Current!.FindById(id)!.GetAttribute("locked"), Is.EqualTo("yes"),
                "undo re-locks the block (E14 standing regression / W0-3 #5)");
        }
    }
}
