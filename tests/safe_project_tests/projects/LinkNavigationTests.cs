using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-9 — link navigation (the F4 "jump to linked" and "Link fra…" far-end path). A follow-link is a reciprocal
    /// pair whose <c>link</c> IDREF points at the <em>partner link element</em>, and the peer resource is that
    /// element's <b>parent</b> (spec ch. 06 §6.4.1). These gates prove <see cref="ProjectEditor.GetLinks"/>
    /// enumerates a resource's link rows, <see cref="ProjectEditor.ResolveLinkOpposite"/> follows a row to the peer
    /// resource, and <see cref="ProjectEditor.GetFullPath"/> renders <c>locality / product-or-block / pin</c>.
    /// </summary>
    public class LinkNavigationTests
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/" + Oracle);

        private static ProjectElement FirstWiredResource(Project project) =>
            project.Root.Descendants().First(e =>
                !e.Children.IsDefaultOrEmpty &&
                e.Children.Any(c => c.Tag is "link_from_resource" or "link_to_resource"));

        [Test]
        public async Task GetLinks_EnumeratesAResourcesLinkRows()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ProjectElement resource = FirstWiredResource(project);

            IReadOnlyList<LinkInfo> links = editor.GetLinks(resource.Id!.Value);

            Assert.That(links, Is.Not.Empty, "a wired resource has at least one link row");
            Assert.That(links.All(l => l.Tag is "link_from_resource" or "link_to_resource"), Is.True);
            Assert.That(links.Select(l => l.LinkRowId),
                Is.EquivalentTo(resource.Children.Where(c => c.Tag is "link_from_resource" or "link_to_resource").Select(c => c.Id!.Value)),
                "the rows are exactly the resource's link children");
        }

        [Test]
        public async Task GetLinks_UnwiredResource_IsEmpty()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ProjectElement group = project.Groups.First();

            Assert.That(editor.GetLinks(group.Id!.Value), Is.Empty, "a locality owns no link rows");
        }

        [Test]
        public async Task ResolveLinkOpposite_FollowsToThePeerResource()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ProjectElement resource = FirstWiredResource(project);
            LinkInfo row = editor.GetLinks(resource.Id!.Value).First();

            ElementRef? peer = editor.ResolveLinkOpposite(row.LinkRowId);

            Assert.Multiple(() =>
            {
                Assert.That(peer, Is.Not.Null, "the opposite endpoint resolves");
                Assert.That(peer!.Id, Is.Not.EqualTo(resource.Id), "the peer is a different resource");
                Assert.That(peer.Tag, Does.StartWith("resource_").Or.StartWith("dataline_"),
                    "the opposite endpoint is a resource, not the link element");
            });
        }

        [Test]
        public async Task ResolveLinkOpposite_IsReciprocal()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ProjectElement resource = FirstWiredResource(project);
            LinkInfo rowA = editor.GetLinks(resource.Id!.Value).First();

            ElementRef peer = editor.ResolveLinkOpposite(rowA.LinkRowId)!;
            // the peer's reciprocal row points back at rowA; resolving it returns the original resource
            LinkInfo rowBack = editor.GetLinks(peer.Id).First(l => l.PartnerLinkId == rowA.LinkRowId);
            ElementRef back = editor.ResolveLinkOpposite(rowBack.LinkRowId)!;

            Assert.That(back.Id, Is.EqualTo(resource.Id), "following the link out and back returns to the start");
        }

        [Test]
        public async Task GetFullPath_RendersLocalityProductPin()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ProjectElement resource = FirstWiredResource(project);

            string path = editor.GetFullPath(resource.Id!.Value);
            string[] parts = path.Split(" / ");

            Assert.Multiple(() =>
            {
                Assert.That(parts, Has.Length.EqualTo(3), "locality / product-or-block / pin");
                Assert.That(project.Groups.Select(g => g.GetAttribute("name")), Does.Contain(parts[0]),
                    "the first segment is the owning locality");
                Assert.That(parts[^1], Is.EqualTo(resource.GetAttribute("name")), "the last segment is the pin itself");
            });
        }
    }
}
