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

        // The two gates below pin what the lookup maps behind FindById/FindParent must reproduce. Both describe
        // situations the oracle corpus cannot produce — a duplicated id is a validation error, and a re-rooted
        // project is built rather than loaded — so they are the only cover the maps' ordering and invalidation have.

        // A shallower candidate and a deeper one, arranged so the two lookups CAN disagree: the first element
        // carrying the id sits deep, while a later twin hangs directly off a shallower parent. A parent map filled
        // independently of the element map answers with that shallower parent — which is not the parent of the
        // element the id resolves to, and is the pair a delete or locked-block gate would then classify from.
        private static Project WithDuplicatedId(out ElementId duplicated)
        {
            var twinId = new ElementId(0x4242, 0x28);
            duplicated = twinId;
            ProjectElement Leaf(string tag) => ProjectElement.Create(tag, twinId, [], []);
            ProjectElement deepBranch = ProjectElement.Create("deep_parent", new ElementId(0x11, 0x28), [],
                [Leaf("deep_twin")]);
            ProjectElement shallowParent = ProjectElement.Create("shallow_parent", new ElementId(0x10, 0x28), [],
                [deepBranch, Leaf("shallow_twin")]);
            return new Project(ProjectElement.Create("utcs_project", null, [], [shallowParent]));
        }

        /// <summary>
        /// The two lookups describe ONE position in the tree, even where an id names more than one element: the id
        /// resolves to the first element carrying it, and the parent is THAT element's parent.
        /// </summary>
        /// <remarks>
        /// It matters beyond tidiness because the session's ownership gate reads both — the target for the wiring
        /// and pin clauses, its parent for the catalog clause, its ancestors for the locked one — and classifies a
        /// delete from the three together. Answered independently, the pair can name a branch the target does not
        /// sit in, and the gate then allows what <c>ProjectEditor.ClassifyDeletionRefusal</c>'s single chain
        /// refuses. A duplicated id is itself a validation finding, but a project carrying one still has to be
        /// safe to open and edit.
        /// </remarks>
        [Test]
        public void DuplicatedId_ResolvesToTheFirstMatchAndToThatMatchsParent()
        {
            Project project = WithDuplicatedId(out ElementId duplicated);

            Assert.Multiple(() =>
            {
                Assert.That(project.FindById(duplicated)?.Tag, Is.EqualTo("deep_twin"),
                    "FindById answers with the first element carrying the id in document order");
                Assert.That(project.FindParent(duplicated)?.Tag, Is.EqualTo("deep_parent"),
                    "and FindParent with the parent of THAT element, not with the shallower element whose "
                    + "children also happen to carry the id");
            });
        }

        [Test]
        public void ReRootedProject_ResolvesAgainstTheNewTree()
        {
            Project project = WithDuplicatedId(out ElementId duplicated);
            _ = project.FindById(duplicated);   // resolve first, so a stale answer is available to be given

            var movedId = new ElementId(0x99, 0x28);
            Project moved = project with
            {
                Root = ProjectElement.Create("utcs_project", null, [],
                    [ProjectElement.Create("relocated", movedId, [], [])]),
            };

            Assert.Multiple(() =>
            {
                Assert.That(moved.FindById(movedId)?.Tag, Is.EqualTo("relocated"), "the new tree's element resolves");
                Assert.That(moved.FindById(duplicated), Is.Null, "the replaced tree's element no longer does");
                Assert.That(project.FindById(duplicated)?.Tag, Is.EqualTo("deep_twin"), "the original is unaffected");
            });
        }
    }
}
