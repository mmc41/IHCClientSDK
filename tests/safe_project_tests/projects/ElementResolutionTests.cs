using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-1 — public id-addressable resolution, the foundation of a GUI selection model. A selection carries an
    /// <see cref="ElementId"/>; these gates prove the read model resolves any id to its node
    /// (<see cref="Project.FindById"/>), enumerates a subtree in document order
    /// (<see cref="ProjectElement.Descendants"/>), and finds a node's parent (<see cref="Project.FindParent"/>),
    /// and that the edit session hands back a generic <see cref="ElementRef"/> for any id
    /// (<see cref="ProjectEditor.TryResolve"/>) — disambiguating same-named blocks by id — all without mutating.
    /// The deepest authentic oracle (<c>project3-KompleksWired.vis</c>) is the fixture.
    /// </summary>
    public class ElementResolutionTests
    {
        private const string Oracle = "project3-KompleksWired.vis";

        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        // Ground-truth element-id order read straight from the file bytes: a real element id is written as
        // " id=\"_0x...\"" (space-prefixed), which typeid/channel_id/last_unique_id never match.
        private static IReadOnlyList<string> FileIdOrder()
        {
            string text = Encoding.Latin1.GetString(TestData.ReadBytes("projects/" + Oracle));
            return Regex.Matches(text, " id=\"(_0x[0-9a-f]+)\"")
                .Select(m => m.Groups[1].Value)
                .ToList();
        }

        [Test]
        public async Task FindById_ResolvesEveryIdInDeepestTree()
        {
            Project project = await LoadOracle();
            IReadOnlyList<ProjectElement> withId = project.Root.Descendants().Where(e => e.Id is not null).ToList();

            Assert.That(withId, Is.Not.Empty, "the oracle has id-bearing elements");
            foreach (ProjectElement e in withId)
            {
                ProjectElement? found = project.FindById(e.Id!.Value);
                Assert.That(found?.Id, Is.EqualTo(e.Id), $"id {e.Id} resolves to the element carrying it");
            }
        }

        [Test]
        public async Task FindById_UnknownId_ReturnsNull()
        {
            Project project = await LoadOracle();
            Assert.That(project.FindById(new ElementId(0xffffff, 0x28)), Is.Null);
        }

        [Test]
        public async Task Descendants_YieldsEveryElementIdInDocumentOrder()
        {
            Project project = await LoadOracle();

            List<string> walkOrder = project.Root.Descendants()
                .Where(e => e.Id is not null)
                .Select(e => e.Id!.Value.ToToken())
                .ToList();

            Assert.That(walkOrder, Is.EqualTo(FileIdOrder()),
                "Descendants enumerates depth-first pre-order, matching the file's top-to-bottom id order");
        }

        [Test]
        public async Task Descendants_ExcludesSelf()
        {
            Project project = await LoadOracle();
            Assert.That(project.Root.Descendants(), Has.None.EqualTo(project.Root));
        }

        [Test]
        public async Task TryResolve_DisambiguatesSameNamedBlocksById()
        {
            Project project = await LoadOracle();
            List<ElementId> tomBlokIds = project.Root.Descendants()
                .Where(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Tom blok")
                .Select(e => e.Id!.Value)
                .ToList();

            Assert.That(tomBlokIds, Has.Count.EqualTo(3), "three same-named 'Tom blok' blocks");
            Assert.That(tomBlokIds.Distinct().Count(), Is.EqualTo(3), "each carries a distinct id");

            ProjectEditor editor = project.Edit();
            foreach (ElementId id in tomBlokIds)
            {
                Assert.That(editor.TryResolve(id, out ElementRef? handle), Is.True, $"{id} resolves");
                Assert.That(handle!.Id, Is.EqualTo(id));
                Assert.That(handle.Element.GetAttribute("id"), Is.EqualTo(id.ToToken()),
                    "the handle addresses exactly the block with that id, not another same-named one");
            }
        }

        [Test]
        public async Task TryResolve_UnknownId_ReturnsFalseAndNullHandle()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();

            Assert.That(editor.TryResolve(new ElementId(0xffffff, 0x28), out ElementRef? handle), Is.False);
            Assert.That(handle, Is.Null);
        }

        [Test]
        public async Task FindParent_ReturnsImmediateContainer()
        {
            Project project = await LoadOracle();
            ProjectElement tomBlok = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Tom blok");

            ProjectElement? parent = project.FindParent(tomBlok.Id!.Value);

            Assert.Multiple(() =>
            {
                Assert.That(parent?.Tag, Is.EqualTo("group"), "a function block sits directly under its locality group");
                Assert.That(parent!.Children, Does.Contain(tomBlok), "the parent actually contains the child");
            });
        }

        [Test]
        public async Task FindParent_UnknownId_ReturnsNull()
        {
            Project project = await LoadOracle();
            Assert.That(project.FindParent(new ElementId(0xffffff, 0x28)), Is.Null);
        }

        [Test]
        public async Task Resolve_IsReadOnly_CommitRoundTripsUnchanged()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();

            foreach (ProjectElement e in project.Root.Descendants().Where(e => e.Id is not null))
            {
                editor.TryResolve(e.Id!.Value, out _);
            }

            Assert.That(editor.ToProject(), Is.EqualTo(project), "resolution is side-effect free");
        }
    }
}
