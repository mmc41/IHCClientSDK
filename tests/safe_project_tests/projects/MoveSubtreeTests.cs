using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-5 — id-preserving reparent (<see cref="ProjectEditor.MoveSubtree"/>): relocate an existing subtree to a
    /// new parent while <b>keeping every id</b>, so reciprocal links (which address elements by id) stay intact —
    /// unlike remove+re-add, which re-ids and strands links. Spec ch. 02 §6.6: "blocks can be moved between groups;
    /// their ids never change." <c>project3-KompleksWired.vis</c> (11 rooms, wired components) is the oracle.
    /// </summary>
    public class MoveSubtreeTests
    {
        private const string Oracle = "project3-KompleksWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/" + Oracle);

        private static HashSet<ElementId> Ids(ProjectElement subtree)
        {
            var ids = new HashSet<ElementId>();
            foreach (ProjectElement e in new[] { subtree }.Concat(subtree.Descendants()))
            {
                if (e.Id is { } id)
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        private static bool HasLinks(ProjectElement subtree) =>
            new[] { subtree }.Concat(subtree.Descendants())
                .Any(e => e.Tag is "link_from_resource" or "link_to_resource");

        // A wired component that is a direct child of a room, plus a different target room.
        private static (ProjectElement moved, ElementId sourceGroupId, ElementId targetGroupId) WiredComponentAndOtherGroup(Project project)
        {
            (ProjectElement group, ProjectElement child) = project.Groups
                .SelectMany(g => g.Children.Select(c => (g, c)))
                .First(t => HasLinks(t.c));
            ElementId targetGroupId = project.Groups.First(g => g.Id!.Value != group.Id!.Value).Id!.Value;
            return (child, group.Id!.Value, targetGroupId);
        }

        [Test]
        public async Task MoveSubtree_WiredComponentBetweenRooms_PreservesEveryId_LinksIntact()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            (ProjectElement moved, ElementId sourceGroupId, ElementId targetGroupId) = WiredComponentAndOtherGroup(project);
            HashSet<ElementId> idsBefore = Ids(moved);

            editor.MoveSubtree(moved.Id!.Value, targetGroupId);
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(Ids(after.FindById(moved.Id!.Value)!), Is.EquivalentTo(idsBefore),
                    "every id in the moved subtree is unchanged");
                Assert.That(after.FindParent(moved.Id!.Value)!.Id, Is.EqualTo(targetGroupId), "reparented to the target room");
                Assert.That(after.FindById(sourceGroupId)!.Children.Any(c => c.Id == moved.Id), Is.False,
                    "no longer under the source room");
                Assert.That(after.LastUniqueId, Is.EqualTo(project.LastUniqueId), "move allocates no ids");
                ProjectValidationResult v = app.Validate(after);
                Assert.That(v.IsValid, Is.True, "reciprocal links survive the move: " + string.Join(" | ", v.Errors));
            });
        }

        [Test]
        public async Task MoveSubtree_ReturnsEditor_ForChaining()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            (ProjectElement moved, _, ElementId targetGroupId) = WiredComponentAndOtherGroup(project);

            Assert.That(editor.MoveSubtree(moved.Id!.Value, targetGroupId), Is.SameAs(editor));
        }

        [Test]
        public async Task MoveSubtree_AtIndex_InsertsAtThatPosition()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            (ProjectElement moved, _, ElementId targetGroupId) = WiredComponentAndOtherGroup(project);

            editor.MoveSubtree(moved.Id!.Value, targetGroupId, index: 0);
            Project after = editor.ToProject();

            Assert.That(after.FindById(targetGroupId)!.Children.First().Id, Is.EqualTo(moved.Id),
                "the moved subtree becomes the first child at index 0");
        }

        [Test]
        public async Task MoveSubtree_IntoItsOwnSubtree_Throws()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ProjectElement group = project.Groups.First(g => !g.Children.IsDefaultOrEmpty);
            ElementId descendantId = group.Descendants().First(e => e.Id is not null).Id!.Value;

            Assert.Throws<InvalidOperationException>(() => editor.MoveSubtree(group.Id!.Value, descendantId),
                "a subtree cannot be moved inside itself");
        }
    }
}
