using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-8: the structure command family — MoveNode/ReorderNode/CopyNode byte-round-trip against the
    /// engine's own MoveSubtree/ReorderSubtree/CopySubtree; a cascade DeleteNode reverses as one undo step; an
    /// illegal move (self/descendant or wrong container) is Refused.
    /// </summary>
    public class StructureCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        private static (ElementId Product, ElementId SourceLoc, ElementId TargetLoc) PickProductAndTwoLocalities(Project project)
        {
            System.Collections.Generic.List<ProjectElement> groups = project.Groups.ToList();
            ProjectElement source = groups.First(g => g.ChildrenOrEmpty().Any(c => ProductClassifier.IsProduct(c.Tag)));
            ElementId product = source.ChildrenOrEmpty().First(c => ProductClassifier.IsProduct(c.Tag)).Id!.Value;
            ElementId target = groups.First(g => g.Id != source.Id).Id!.Value;
            return (product, source.Id!.Value, target);
        }

        [Test]
        public async Task MoveNode_ReparentsProduct_MatchesEngineMoveSubtree()
        {
            Project project = await Load("project3-KompleksWired.vis");
            (ElementId product, _, ElementId targetLoc) = PickProductAndTwoLocalities(project);
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new MoveNode(product, targetLoc));

            ProjectEditor editor = project.Edit();
            editor.MoveSubtree(product, targetLoc);
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindParent(product)!.Id, Is.EqualTo(targetLoc), "the product is re-parented");
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own MoveSubtree byte-for-byte");
            });
        }

        [Test]
        public async Task MoveNode_IllegalContainer_IsRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId loc = project.Groups.First().Id!.Value;
            ProjectDocumentSession session = Session(project);

            // a locality cannot be contained by a locality (and self is a descendant) — refused either way.
            EditOutcome outcome = session.Apply(new MoveNode(loc, loc));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        [Test]
        public async Task CopyNode_PastesIndependentDuplicate_MatchesEngineCopySubtree()
        {
            Project project = await Load("project3-KompleksWired.vis");
            (ElementId product, _, ElementId targetLoc) = PickProductAndTwoLocalities(project);
            ProjectDocumentSession session = Session(project);

            EditOutcome<ElementId> outcome = session.Apply(new CopyNode(product, targetLoc));

            ProjectEditor editor = project.Edit();
            editor.CopySubtree(product, targetLoc);
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(product), Is.Not.Null, "the original survives");
                Assert.That(session.Current!.FindById(outcome.Value), Is.Not.Null, "the copy resolves");
                Assert.That(session.Current!.Equals(viaEngine), Is.True,
                    "matches the engine's own CopySubtree (fresh ids, shared-enum reuse)");
            });
        }

        [Test]
        public async Task ReorderNode_MatchesEngineReorderSubtree()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId lastGroup = project.Groups.Last().Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new ReorderNode(lastGroup, SameTagIndex: 0));

            ProjectEditor editor = project.Edit();
            editor.ReorderSubtree(lastGroup, 0);
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.AnyOf(EditStatus.Committed, EditStatus.NoChange));
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own ReorderSubtree");
            });
        }

        [Test]
        public async Task DeleteNode_CascadeDelete_ReversesAsOneUndoStep()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId product = project.Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && e.Id is not null).Id!.Value;
            ProjectDocumentSession session = Session(project);

            session.Apply(new DeleteNode(product, Cascade: true));
            Assert.That(session.Current!.FindById(product), Is.Null, "the product and its subtree are gone");

            session.Undo();
            Assert.That(session.Current!.FindById(product), Is.Not.Null, "one undo restores it — the cascade reverses as a unit");
        }
    }
}
