using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-10 — context-sensitive placement legality for right-click menus / gray-out
    /// (<see cref="ProjectEditor.CanInsert"/> predicate + <see cref="ProjectEditor.GetInsertableAt"/> option list).
    /// The containment model is authored from the spec (ch. 03/04, §6.3.1, §8.2) and validated against the
    /// authentic oracles: every parent→child pair the vendor files actually contain must be admitted, and the
    /// named illegal placements rejected. Unmodeled parents fall back to permissive (never block a legal insert).
    /// </summary>
    public class InsertLegalityTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> Load(string file) => new ProjectAppService(Settings).Load("testdata/projects/" + file);

        // The structural parents the model constrains (others are permissive).
        private static readonly HashSet<string> ModeledParents = new()
        {
            "groups", "group", "inputs", "outputs", "settings", "internalsettings", "programs",
        };

        [TestCase("project2-CustomBlock.vis")]
        [TestCase("project3-KompleksWired.vis")]
        public async Task CanInsert_AdmitsEveryModeledParentChildPairPresentInTheOracle(string file)
        {
            Project project = await Load(file);
            ProjectEditor editor = project.Edit();

            foreach (ProjectElement parent in project.Root.Descendants().Where(e => e.Id is not null && ModeledParents.Contains(e.Tag)))
            {
                foreach (ProjectElement child in parent.Children.Where(c => c.Id is not null))
                {
                    Assert.That(editor.CanInsert(parent.Id!.Value, child.Tag), Is.True,
                        $"vendor file places <{child.Tag}> under <{parent.Tag}>, so it must be legal");
                }
            }
        }

        [Test]
        public async Task CanInsert_HonorsNamedLegalAndIllegalPlacements()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();
            ElementId group = project.Groups.First().Id!.Value;
            ElementId inputs = project.Root.Descendants().First(e => e.Tag == "inputs").Id!.Value;
            ElementId outputs = project.Root.Descendants().First(e => e.Tag == "outputs").Id!.Value;
            ElementId settings = project.Root.Descendants().First(e => e.Tag == "settings").Id!.Value;
            ElementId programs = project.Root.Descendants().First(e => e.Tag == "programs").Id!.Value;

            Assert.Multiple(() =>
            {
                // locality → products + function blocks
                Assert.That(editor.CanInsert(group, "product_dataline"), Is.True);
                Assert.That(editor.CanInsert(group, "product_airlink"), Is.True);
                Assert.That(editor.CanInsert(group, "functionblock"), Is.True);
                Assert.That(editor.CanInsert(group, "group"), Is.False, "localities do not nest");
                Assert.That(editor.CanInsert(group, "resource_scene"), Is.False);

                // scene pins are output-bound
                Assert.That(editor.CanInsert(outputs, "resource_scene"), Is.True);
                Assert.That(editor.CanInsert(inputs, "resource_scene"), Is.False, "a scene under inputs is illegal");
                Assert.That(editor.CanInsert(outputs, "group"), Is.False, "a group under outputs is illegal");

                // §6.3.1 value/pin split
                Assert.That(editor.CanInsert(settings, "resource_flag"), Is.True);
                Assert.That(editor.CanInsert(settings, "resource_input"), Is.False, "settings excludes pins");

                // programs holds only program_simple
                Assert.That(editor.CanInsert(programs, "program_simple"), Is.True);
                Assert.That(editor.CanInsert(programs, "program_sub"), Is.False);
            });
        }

        [Test]
        public async Task GetInsertableAt_Group_OffersProductsAndFunctionBlocks()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();

            IReadOnlyList<string> tags = editor.GetInsertableAt(project.Groups.First().Id!.Value).Select(o => o.ChildTag).ToList();

            Assert.That(tags, Is.EquivalentTo(new[] { "product_dataline", "product_airlink", "functionblock" }));
        }

        [Test]
        public async Task GetInsertableAt_Outputs_IncludesResourceSceneAndPin()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();
            ElementId outputs = project.Root.Descendants().First(e => e.Tag == "outputs").Id!.Value;

            IReadOnlyList<InsertOption> options = editor.GetInsertableAt(outputs);

            Assert.Multiple(() =>
            {
                Assert.That(options.Select(o => o.ChildTag), Does.Contain("resource_scene"));
                Assert.That(options.Select(o => o.ChildTag), Does.Contain("resource_output"));
                Assert.That(options.Any(o => o.ChildTag == "resource_flag" && o.Category == "Variable"), Is.True);
            });
        }

        [Test]
        public async Task GetInsertableAt_Programs_IsProgramSimpleOnly()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();
            ElementId programs = project.Root.Descendants().First(e => e.Tag == "programs").Id!.Value;

            IReadOnlyList<InsertOption> options = editor.GetInsertableAt(programs);

            Assert.That(options.Select(o => o.ChildTag), Is.EqualTo(new[] { "program_simple" }));
        }

        [Test]
        public async Task CanInsert_UnmodeledParent_IsPermissive()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();
            ElementId product = project.Root.Descendants().First(e => e.Tag == "product_dataline").Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(editor.CanInsert(product, "dataline_input"), Is.True);
                Assert.That(editor.CanInsert(product, "anything_unmodeled"), Is.True,
                    "an unmodeled parent is permissive — the model never blocks a legal insert it does not know about");
                Assert.That(editor.GetInsertableAt(product), Is.Empty, "no modeled options offered for an unmodeled parent");
            });
        }
    }
}
