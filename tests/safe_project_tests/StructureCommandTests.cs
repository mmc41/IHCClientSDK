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
    public class StructureCommandTests : SessionCommandFixture
    {
        private static (ElementId Product, ElementId SourceLoc, ElementId TargetLoc) PickProductAndTwoLocalities(Project project)
        {
            System.Collections.Generic.List<ProjectElement> groups = project.Groups.ToList();
            ProjectElement source = groups.First(g => g.Children.Any(c => ProductClassifier.IsProduct(c.Tag)));
            ElementId product = source.Children.First(c => ProductClassifier.IsProduct(c.Tag)).Id!.Value;
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

        [Test]   // W2-16: from MoveTests.Move_IntoSameParent_IsRefusedAsNoOp (moved down)
        public async Task MoveNode_IntoCurrentParent_IsRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            (ElementId product, ElementId sourceLoc, _) = PickProductAndTwoLocalities(project);
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new MoveNode(product, sourceLoc));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                "moving a node into the container it already lives in is a no-op refusal");
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

        [Test]   // W2-16: from CopyPasteTests.Copy_IntoIllegalContainer_IsRefused (moved down)
        public async Task CopyNode_IntoIllegalContainer_IsRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            (ElementId product, _, _) = PickProductAndTwoLocalities(project);
            ProjectDocumentSession session = Session(project);

            // a product is not a legal paste target — only a locality can hold a product/block.
            EditOutcome<ElementId> outcome = session.Apply(new CopyNode(product, product));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        [Test]   // W2-16: from CopyPasteTests.Copy_DropsLinksWhoseOtherEndIsOutsideTheCopy (moved down)
        public async Task CopyNode_DropsExternalLinkHalves()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement linked = project.Groups
                .SelectMany(g => g.Children.Where(c => ProductClassifier.IsProduct(c.Tag)))
                .First(p => p.DescendantsAndSelf().Any(d => d.Tag is "link_to_resource" or "link_from_resource"));
            ElementId product = linked.Id!.Value;
            ElementId currentParent = project.FindParent(product)!.Id!.Value;
            ElementId target = project.Groups.First(g => g.Id!.Value != currentParent).Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome<ElementId> outcome = session.Apply(new CopyNode(product, target));

            ProjectElement copy = session.Current!.FindById(outcome.Value)!;
            Assert.Multiple(() =>
            {
                Assert.That(copy.DescendantsAndSelf().Any(d => d.Tag is "link_from_resource" or "link_to_resource"), Is.False,
                    "the copy drops link halves whose other end lies outside the copy");
                Assert.That(session.Current!.FindById(product)!.DescendantsAndSelf()
                    .Any(d => d.Tag is "link_from_resource" or "link_to_resource"), Is.True, "the original keeps its link");
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
        public async Task DeleteNode_Evaluate_RefusesExactlyWhatCanDeleteForbids()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");

            // One representative per element tag sweeps deletable nodes AND the structural containers
            // (events/commands/conditions/sections/programs/...) the CanDelete gate forbids (G7).
            var representatives = project.Root.DescendantsAndSelf()
                .Where(e => e.Id is not null)
                .GroupBy(e => e.Tag)
                .Select(g => g.First())
                .ToList();
            bool[] gate = representatives.Select(e => app.Commands.CanDelete(project, e.Id!.Value)).ToArray();
            Assert.That(gate, Does.Contain(true).And.Contain(false),
                "the sweep must include both deletable and not-deletable representatives");

            Assert.Multiple(() =>
            {
                for (int i = 0; i < representatives.Count; i++)
                {
                    var element = representatives[i];
                    EditVerdict evaluated = app.CanApply(project,
                        app.Commands.DeleteNode(project, element.Id!.Value, cascade: false));
                    Assert.That(evaluated.Ok, Is.EqualTo(gate[i]),
                        $"G7 parity: DeleteNode.Evaluate vs Commands.CanDelete diverge on <{element.Tag}> (id {element.Id})");
                }
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
