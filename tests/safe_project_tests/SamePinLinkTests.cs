using System.Linq;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T019 / D06 — a follow-link from a pin to ITSELF (id == id) is refused everywhere: the engine <c>CanLink</c>
    /// returns false and <c>Link</c> throws, matching the session <c>LinkPins</c> refusal — so a direct engine caller
    /// cannot author the self-link the vendor never produces either. A <c>resource_flag</c> is a tag-linkable value
    /// pin, so id == id is the ONLY reason it is refused (isolating the rule from the data-flow rule).
    /// </summary>
    public class SamePinLinkTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static (Project Project, ElementId PinId) FlagPin()
        {
            Project project = ProjectReader.Read(TestData.ReadBytes("projects/project2-CustomBlock.vis"));
            ElementId pinId = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok")
                .Descendants().First(e => e.Tag == "resource_flag").Id!.Value;
            return (project, pinId);
        }

        [Test]
        public void SamePinLink_Engine_CanLinkFalse_AndLinkThrows()
        {
            (Project project, ElementId pinId) = FlagPin();
            ProjectEditor editor = project.Edit();

            Assert.Multiple(() =>
            {
                Assert.That(editor.CanLink(pinId, pinId), Is.False, "the engine refuses a pin-to-itself link (D06)");
                Assert.That(() => editor.Link(pinId, pinId), Throws.InvalidOperationException, "Link throws on a self-link");
            });
        }

        [Test]
        public void SamePinLink_Session_LinkPins_IsRefused()
        {
            (Project project, ElementId pinId) = FlagPin();
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(App.Commands.LinkPins(project, pinId, pinId));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }
    }
}
