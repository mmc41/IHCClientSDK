using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// BL-3 — id-addressed delete with reciprocal-link cascade. Deleting a wired element must also drop the
    /// <c>link_from_resource</c>/<c>link_to_resource</c> halves that live OUTSIDE the deleted subtree and point
    /// into it, or the link bijection breaks and the next save is blocked. These gates prove
    /// <see cref="ProjectEditor.DeleteById"/> cascades those external halves (validator reports zero dangling
    /// links) and that the public <c>Remove*</c> handles are backed by it. <c>Project1-SimpelWired.vis</c> wires
    /// products to function blocks within a room, so deleting one end leaves the other's half dangling unless cascaded.
    /// </summary>
    public class DeleteCascadeTests
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/" + Oracle);

        private static ProjectElement Find(Project project, string tag, string name) =>
            project.Root.Descendants().First(e => e.Tag == tag && e.GetAttribute("name") == name);

        // The partner-half ids referenced by every link half inside the subtree — the external halves a
        // correct delete must cascade.
        private static IReadOnlyList<ElementId> ExternalLinkPartners(ProjectElement subtree)
        {
            var partners = new List<ElementId>();
            foreach (ProjectElement e in new[] { subtree }.Concat(subtree.Descendants()))
            {
                if (e.Tag is "link_from_resource" or "link_to_resource"
                    && ElementId.TryParse(e.GetAttribute("link"), out ElementId partner))
                {
                    partners.Add(partner);
                }
            }
            return partners;
        }

        [Test]
        public async Task DeleteById_WiredProduct_CascadesReciprocalHalves_ValidatorClean()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            ProjectElement fuga = Find(project, "product_dataline", "LK FUGA Tryk 2 tast");
            IReadOnlyList<ElementId> partners = ExternalLinkPartners(fuga);
            Assert.That(partners, Is.Not.Empty, "FUGA is wired to the function block, so it has external partners");

            editor.DeleteById(fuga.Id!.Value);
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(after.FindById(fuga.Id!.Value), Is.Null, "the product subtree is gone");
                foreach (ElementId partnerId in partners)
                {
                    Assert.That(after.FindById(partnerId), Is.Null,
                        $"external reciprocal half {partnerId} was cascaded");
                }
                ProjectValidationResult v = app.Validate(after);
                Assert.That(v.IsValid, Is.True, "zero dangling links: " + string.Join(" | ", v.Errors));
            });
        }

        [Test]
        public async Task DeleteById_WiredFunctionBlock_ValidatorClean()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            ProjectElement kip = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name")!.Contains("Kip"));

            editor.DeleteById(kip.Id!.Value);
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(after.FindById(kip.Id!.Value), Is.Null);
                ProjectValidationResult v = app.Validate(after);
                Assert.That(v.IsValid, Is.True, "deleting the wired FB leaves no dangling links: " + string.Join(" | ", v.Errors));
            });
        }

        [Test]
        public async Task DeleteById_ReturnsEditor_AndIsNoOpForUnknownId()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();

            ProjectEditor returned = editor.DeleteById(new ElementId(0xffffff, 0x28));   // absent

            Assert.That(returned, Is.SameAs(editor), "DeleteById returns the editor for chaining");
            Assert.That(editor.ToProject(), Is.EqualTo(project), "deleting an absent id is a no-op");
        }

        [Test]
        public async Task RemoveProductHandle_IsBackedByCascade_ValidatorClean()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            GroupRef stue = editor.Group("Stue");
            stue.RemoveProduct(stue.Product("LK FUGA Tryk 2 tast"));
            Project after = editor.ToProject();

            ProjectValidationResult v = app.Validate(after);
            Assert.That(v.IsValid, Is.True, "the public RemoveProduct handle now cascades links: " + string.Join(" | ", v.Errors));
        }
    }
}
