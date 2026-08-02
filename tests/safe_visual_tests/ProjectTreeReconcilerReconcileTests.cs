using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// fablerefac W3-4: the keyed <see cref="ProjectTreeReconciler.Reconcile"/> updates the projected forest in place
/// from a <see cref="ProjectChangeSet"/> — one targeted test per change class (added / removed / changed / reorder
/// / dependent-invalidation). Each asserts BOTH rebuild-equivalence for that class AND node-identity preservation
/// (the incremental win: Avalonia keeps selection/expansion because the same <see cref="TreeNodeViewModel"/>
/// instances survive). The W3-3 CsCheck oracle covers randomized combinations; these pin the individual classes.
/// </summary>
public class ProjectTreeReconcilerReconcileTests
{
    private static Project NewBaseProject() =>
        new ProjectAppService(new IhcSettings()).CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));

    private static ProjectTreeReconciler InstallationReconciler() =>
        new(p => new ProjectTreeProjector(p).BuildLocalitiesRoot(functions: false));

    private static IEnumerable<TreeNodeViewModel> Flatten(TreeNodeViewModel node)
    {
        yield return node;
        foreach (TreeNodeViewModel child in node.Children)
        {
            foreach (TreeNodeViewModel descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    [Test]
    public void Reconcile_Changed_ReRendersLabelInPlace_PreservingIdentity()
    {
        Project project = NewBaseProject();
        ProjectTreeReconciler reconciler = InstallationReconciler();
        TreeNodeViewModel root = reconciler.Rebuild(project);
        ElementId targetId = project.Groups[0].Id!.Value;
        ElementId neighbourId = project.Groups[1].Id!.Value;
        TreeNodeViewModel targetBefore = reconciler.Find(NodeKey.ForElement(targetId))!;
        TreeNodeViewModel neighbourBefore = reconciler.Find(NodeKey.ForElement(neighbourId))!;

        var session = new ProjectDocumentSession();
        session.Open(project);
        EditOutcome outcome = session.Apply(new RenameLocality(targetId, "Renamed-XYZ", string.Empty));
        reconciler.Reconcile(session.Current!, outcome.Changes!);

        Assert.Multiple(() =>
        {
            Assert.That(reconciler.Root, Is.SameAs(root), "root identity preserved");
            Assert.That(reconciler.Find(NodeKey.ForElement(targetId)), Is.SameAs(targetBefore), "renamed node keeps identity");
            Assert.That(targetBefore.DisplayName, Is.EqualTo("Renamed-XYZ"), "label re-rendered in place");
            Assert.That(reconciler.Find(NodeKey.ForElement(neighbourId)), Is.SameAs(neighbourBefore), "untouched node keeps identity");
        });
    }

    [Test]
    public void Reconcile_Added_InsertsNewRow_PreservingExistingIdentity()
    {
        Project project = NewBaseProject();
        ProjectTreeReconciler reconciler = InstallationReconciler();
        TreeNodeViewModel root = reconciler.Rebuild(project);
        int before = root.Children.Count;
        ElementId existingId = project.Groups[0].Id!.Value;
        TreeNodeViewModel existingBefore = reconciler.Find(NodeKey.ForElement(existingId))!;

        var session = new ProjectDocumentSession();
        session.Open(project);
        EditOutcome<ElementId> outcome = session.Apply(new AddLocality("Fresh-Room"));
        reconciler.Reconcile(session.Current!, outcome.Changes!);

        Assert.Multiple(() =>
        {
            Assert.That(root.Children.Count, Is.EqualTo(before + 1), "one new row inserted");
            Assert.That(reconciler.Find(NodeKey.ForElement(outcome.Value)), Is.Not.Null, "new locality indexed");
            Assert.That(reconciler.Find(NodeKey.ForElement(outcome.Value))!.DisplayName, Is.EqualTo("Fresh-Room"));
            Assert.That(reconciler.Find(NodeKey.ForElement(existingId)), Is.SameAs(existingBefore), "existing rows keep identity");
        });
    }

    [Test]
    public void Reconcile_Removed_DropsRow_PreservingSurvivorIdentity()
    {
        Project project = NewBaseProject();
        ProjectTreeReconciler reconciler = InstallationReconciler();
        TreeNodeViewModel root = reconciler.Rebuild(project);
        int before = root.Children.Count;
        ElementId doomedId = project.Groups[2].Id!.Value;
        ElementId survivorId = project.Groups[0].Id!.Value;
        TreeNodeViewModel survivorBefore = reconciler.Find(NodeKey.ForElement(survivorId))!;

        var session = new ProjectDocumentSession();
        session.Open(project);
        EditOutcome outcome = session.Apply(new DeleteLocality(doomedId));
        reconciler.Reconcile(session.Current!, outcome.Changes!);

        Assert.Multiple(() =>
        {
            Assert.That(root.Children.Count, Is.EqualTo(before - 1), "one row dropped");
            Assert.That(reconciler.Find(NodeKey.ForElement(doomedId)), Is.Null, "deleted row dropped from the index");
            Assert.That(root.Children.Any(c => c.ElementId == doomedId), Is.False, "deleted row dropped from the forest");
            Assert.That(reconciler.Find(NodeKey.ForElement(survivorId)), Is.SameAs(survivorBefore), "survivor keeps identity");
        });
    }

    [Test]
    public void Reconcile_Reorder_ReordersChildrenToProjectOrder_PreservingIdentity()
    {
        Project project = NewBaseProject();
        ProjectTreeReconciler reconciler = InstallationReconciler();
        TreeNodeViewModel root = reconciler.Rebuild(project);
        ElementId movedId = project.Groups[0].Id!.Value;
        TreeNodeViewModel movedBefore = reconciler.Find(NodeKey.ForElement(movedId))!;

        var session = new ProjectDocumentSession();
        session.Open(project);
        EditOutcome outcome = session.Apply(new ReorderNode(movedId, 3));   // move locality 0 → position 3
        reconciler.Reconcile(session.Current!, outcome.Changes!);

        List<ElementId> projectOrder = session.Current!.Groups.Select(g => g.Id!.Value).ToList();
        List<ElementId> forestOrder = root.Children.Select(c => c.ElementId!.Value).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(forestOrder, Is.EqualTo(projectOrder), "forest child order matches project order");
            Assert.That(reconciler.Find(NodeKey.ForElement(movedId)), Is.SameAs(movedBefore), "moved node keeps identity");
            Assert.That(root.Children, Does.Contain(movedBefore), "the moved instance is still the one in the collection");
        });
    }

    // The dependency-map class: a scene member row renders its far end's path (which names the far locality). Renaming
    // that locality touches only Changed={locality}; the member's own subtree is untouched, so ONLY the dependency
    // map can re-render it. Without it the label goes stale (that is this test's RED).
    [Test]
    public async Task Reconcile_ReRendersDependentRow_WhenAReferencedLocalityIsRenamed()
    {
        var service = new ProjectAppService(new IhcSettings());
        Project project = service.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty),
            language: LocalityLanguage.English);
        var jalousi = service.GetAvailableProducts().First(p => p.DisplayName.Contains("Jalousi 4 tast"));
        var fbDef = service.GetAvailableFunctionBlocks().First(f => f.MasterType == "3.1.03");

        ProjectEditor editor = project.Edit();
        editor.Group("Living room").AddProduct(jalousi);
        editor.Group("Living room").AddFunctionBlock(fbDef);
        Project mid = editor.ToProject();
        ProjectElement room = mid.Groups.First(g => g.GetAttribute("name") == "Living room");
        ElementId scenePinId = room.ChildrenOrEmpty().First(c => c.Tag == "functionblock")
            .FindChild("outputs")!.ChildrenOrEmpty()
            .First(c => c.Tag == "resource_scene" && c.GetAttribute("name") == "Regulering").Id!.Value;
        ElementId scenesId = room.ChildrenOrEmpty().First(c => c.Tag == "product_airlink")
            .ChildrenOrEmpty().First(c => c.Tag == "scenes").Id!.Value;
        editor.LinkScene(scenePinId, scenesId, SceneValue.Shutter(up: true));
        Project linked = editor.ToProject();
        await Task.CompletedTask;   // keep the established async builder shape; no awaited IO needed

        ProjectTreeReconciler reconciler = InstallationReconciler();
        TreeNodeViewModel root = reconciler.Rebuild(linked);
        TreeNodeViewModel member = Flatten(root).First(n => n.NodeKind == "sceneMember");
        Assert.That(member.DisplayName, Does.Contain("Living room"), "precondition: the far path names the locality");

        ElementId livingRoomId = linked.Groups.First(g => g.GetAttribute("name") == "Living room").Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(linked);
        EditOutcome outcome = session.Apply(new RenameLocality(livingRoomId, "Salon", string.Empty));
        Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed),
            "RenameLocality is the behavior under test and must commit for this scenario");
        reconciler.Reconcile(session.Current!, outcome.Changes!);

        Assert.Multiple(() =>
        {
            Assert.That(Flatten(root).First(n => n.NodeKind == "sceneMember"), Is.SameAs(member), "scene member keeps identity");
            Assert.That(member.DisplayName, Does.Contain("Salon"), "dependent row re-rendered with the new name");
            Assert.That(member.DisplayName, Does.Not.Contain("Living room"), "stale name gone");
        });
    }

    // T022: the projector EMITS the cross-reference dependency edges on each derived-label row (here a scene member,
    // whose far path names the locality), so the reconciler reads them from the projection instead of hand-mirroring
    // the partner walk. A row whose label is composed only of its own attributes emits none.
    [Test]
    public void Projector_EmitsCrossReferenceEdges_OnADerivedLabelRow()
    {
        var service = new ProjectAppService(new IhcSettings());
        Project project = service.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty),
            language: LocalityLanguage.English);
        var jalousi = service.GetAvailableProducts().First(p => p.DisplayName.Contains("Jalousi 4 tast"));
        var fbDef = service.GetAvailableFunctionBlocks().First(f => f.MasterType == "3.1.03");

        ProjectEditor editor = project.Edit();
        editor.Group("Living room").AddProduct(jalousi);
        editor.Group("Living room").AddFunctionBlock(fbDef);
        Project mid = editor.ToProject();
        ProjectElement room = mid.Groups.First(g => g.GetAttribute("name") == "Living room");
        ElementId scenePinId = room.ChildrenOrEmpty().First(c => c.Tag == "functionblock")
            .FindChild("outputs")!.ChildrenOrEmpty()
            .First(c => c.Tag == "resource_scene" && c.GetAttribute("name") == "Regulering").Id!.Value;
        ElementId scenesId = room.ChildrenOrEmpty().First(c => c.Tag == "product_airlink")
            .ChildrenOrEmpty().First(c => c.Tag == "scenes").Id!.Value;
        editor.LinkScene(scenePinId, scenesId, SceneValue.Shutter(up: true));
        Project linked = editor.ToProject();

        TreeNodeViewModel root = new ProjectTreeProjector(linked).BuildLocalitiesRoot(functions: false);
        ElementId livingRoomId = linked.Groups.First(g => g.GetAttribute("name") == "Living room").Id!.Value;
        TreeNodeViewModel member = Flatten(root).First(n => n.NodeKind == "sceneMember");
        TreeNodeViewModel locality = Flatten(root).First(n => n.ElementId == livingRoomId);

        Assert.Multiple(() =>
        {
            Assert.That(member.CrossReferences, Does.Contain(livingRoomId),
                "the scene member's far path names the locality, so its id is an emitted cross-reference edge");
            Assert.That(locality.CrossReferences, Is.Empty,
                "a plain locality row's label is composed only of its own name — no cross-references");
        });
    }
}
