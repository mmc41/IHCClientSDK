using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The one lookup substrate behind <see cref="Project.FindById"/>/<see cref="Project.FindParent"/> answers
    /// every id in an oracle project the way the whole-tree searches it replaced did, and reports absent for an
    /// unknown id.
    /// <para>The reference side is written out here rather than taken from <see cref="Project"/>: those doors ARE
    /// the index now, so comparing the two would assert the index against itself. The walks below share no code
    /// with it, which is what makes them a second opinion.</para>
    /// </summary>
    public class ProjectIndexTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        /// <summary>The depth-first search <see cref="Project.FindById"/> ran before the index existed.</summary>
        private static ProjectElement? WalkToElement(Project project, ElementId id) =>
            project.Root.FindDescendantOrSelf(e => e.Id == id);

        /// <summary>The parent of whatever <see cref="WalkToElement"/> resolved, found by identity so a project
        /// carrying two elements of one id still names the branch the id resolved into.</summary>
        private static ProjectElement? WalkToParent(Project project, ElementId id) =>
            WalkToElement(project, id) is { } target
                ? project.Root.FindDescendantOrSelf(e => e.Children.Any(c => ReferenceEquals(c, target)))
                : null;

        [Test]
        public async Task Index_AgreesWithAnIndependentWalk_ForEveryId()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectIndex index = ProjectIndex.Build(project.Root);

            var ids = project.Root.DescendantsAndSelf()
                .Where(e => e.Id is not null).Select(e => e.Id!.Value).Distinct().ToList();
            Assert.That(ids, Is.Not.Empty, "the oracle has id-bearing elements to check");

            Assert.Multiple(() =>
            {
                foreach (ElementId id in ids)
                {
                    Assert.That(index.FindById(id), Is.SameAs(WalkToElement(project, id)), $"FindById {id.ToToken()}");
                    Assert.That(index.FindParent(id), Is.SameAs(WalkToParent(project, id)), $"FindParent {id.ToToken()}");
                }
            });
        }

        [Test]
        public async Task Index_MissingId_IsAbsent()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectIndex index = ProjectIndex.Build(project.Root);
            var missing = new ElementId(0x7FFFFF, 0x99);

            Assert.Multiple(() =>
            {
                Assert.That(index.FindById(missing), Is.Null);
                Assert.That(index.FindParent(missing), Is.Null);
            });
        }

        /// <summary>
        /// The index carries the tree it was built from, which is the whole of its identity: an index is a pure
        /// function of an immutable root, so a consumer handed one BESIDE a project can tell in O(1) whether the
        /// two belong together. Pinned at the build, because everything that checks a pairing (the diff, the
        /// session's witness) reads what this line sets.
        /// </summary>
        [Test]
        public async Task Build_StampsTheTreeItIndexed()
        {
            Project project = await Load("project3-KompleksWired.vis");

            Assert.That(ProjectIndex.Build(project.Root).Root, Is.SameAs(project.Root),
                "the maps say which tree they answer for");
        }

        /// <summary>
        /// The lifetime contract from the caller's side: a project holds its index WEAKLY, so the instance stays
        /// the same only for as long as somebody holds it. Whoever wants the lookups to stay O(1) between calls
        /// holds what it was handed — this test is the "held" half; the released half is
        /// <c>SessionHistoryTests.HistorySnapshots_DoNotRetainTheirIndex</c>.
        /// </summary>
        [Test]
        public async Task ElementIndex_ReturnsTheSameInstance_WhileHeld()
        {
            Project project = await Load("project3-KompleksWired.vis");

            ProjectIndex held = project.ElementIndex;

            Assert.That(project.ElementIndex, Is.SameAs(held),
                "a second read while the first is held reuses it rather than re-walking the tree");
        }

        /// <summary>
        /// Parentage is keyed by ELEMENT, so the upward chain reaches every ancestor rather than stopping at the
        /// first one that carries no id.
        /// </summary>
        /// <remarks>
        /// An id-less element inside a project is an ordinary open-world shape — the engine admits element types a
        /// project's own DTD declares, the edit-open guard skips id-less elements rather than refusing them, and
        /// <c>ProjectChangeSet</c> rolls an id-less descendant's change up into its nearest id-bearing ancestor. A
        /// chain keyed on ids ends at one, which is why this is asserted on the index rather than left to the one
        /// rule that walks it: the locked-block gate is the caller today, and a second caller would inherit the
        /// same hole.
        /// </remarks>
        [Test]
        public void ParentChain_SpansAnIdlessAncestor()
        {
            var leafId = new ElementId(0x77, 0x28);
            ProjectElement leaf = Tree.Node("resource_flag", "_0x7728", []);
            ProjectElement idless = Tree.Node("imported_container", null, [], leaf);
            ProjectElement top = Tree.Node("functionblock", "_0x7628", [], idless);
            Project project = Tree.WithRoot(top);
            ProjectIndex index = ProjectIndex.Build(project.Root);

            Assert.Multiple(() =>
            {
                Assert.That(index.FindParent(leafId), Is.SameAs(idless),
                    "the element's parent is the id-less container it actually hangs off");
                Assert.That(index.ParentOf(idless), Is.SameAs(top),
                    "and the chain continues past it to the ancestor that does carry an id");
                Assert.That(index.ParentOf(project.Root), Is.Null, "the root has no parent");
            });
        }
    }
}
