using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Mutating through a stale handle (an id no longer in the session) must throw — never silently no-op.
    /// A silent no-op turns an ordinary delete-then-edit GUI flow into corruption: <see cref="ProjectEditor.Link"/>
    /// with one deleted endpoint used to append a one-sided half with a dangling IDREF and burn two counter ids.
    /// </summary>
    public class MutationFailFastTests
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        [Test]
        public async Task SetAttributeById_AbsentId_Throws()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            var stale = new ElementId(0xeeeee, 0x28);   // no such counter in Project1

            Assert.That(() => editor.SetAttributeById(stale, "name", "x"),
                Throws.InvalidOperationException.With.Message.Contains(stale.ToToken()));
        }

        [Test]
        public async Task Link_AfterSinkDeleted_Throws_AndLeavesSessionUnchanged()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            GroupRef stue = editor.Group("Stue");
            ResourceRef a = stue.Product("LK FUGA Tryk 2 tast").Input("Tryk (venstre)");
            ResourceRef b = stue.Product("Lampeudtag").Output("Udgang");

            stue.RemoveProduct(stue.Product("Lampeudtag"));   // b's id is now gone from the session
            Project before = editor.ToProject();

            Assert.That(() => editor.Link(a, b), Throws.InvalidOperationException,
                "linking to a deleted resource is an error, not a half-written link");
            Assert.That(editor.ToProject(), Is.EqualTo(before),
                "the failed link neither appended a half nor advanced the id counter");
        }

        [Test]
        public async Task GroupHandle_AfterRemoveGroup_Throws()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            GroupRef garage = editor.Group("Garage");

            editor.RemoveGroup(garage);

            Assert.That(() => garage.Name("Nyt navn"), Throws.InvalidOperationException,
                "renaming through a stale handle must not silently do nothing");
        }

        [Test]
        public async Task AddProduct_AfterGroupDeleted_Throws_AndLeavesSessionUnchanged()
        {
            // Finding 5: InsertComponent used to adopt DTD blocks, burn allocator ids and hoist enums BEFORE the
            // target group's liveness was ever tested, so an insert into a deleted group left a half-mutated session.
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProductDefinition product = app.GetAvailableProducts().First();
            ProjectEditor editor = project.Edit();
            GroupRef garage = editor.Group("Garage");

            editor.RemoveGroup(garage);            // garage's id is now gone from the session
            Project before = editor.ToProject();

            Assert.That(() => garage.AddProduct(product), Throws.InvalidOperationException,
                "inserting into a deleted group is an error, not a half-mutated session");
            Assert.That(editor.ToProject(), Is.EqualTo(before),
                "a failed insert adopts no DTD block, hoists no enum and burns no id");
        }
    }
}
