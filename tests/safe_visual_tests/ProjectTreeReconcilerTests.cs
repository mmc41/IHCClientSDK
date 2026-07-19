using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>
/// fablerefac W3-2: the reconciler's index + dependency-map data structures — the incremental-mutation substrate
/// W3-4's <c>Reconcile</c> builds on. The index resolves a change to its row in O(1); the dependency map answers
/// "which rows must re-render when this other element changes".
/// </summary>
public class ProjectTreeReconcilerTests
{
    private static ElementId Id(int counter) => new(counter, 1);
    private static TreeNodeViewModel Row(string name) => new(name, "/Assets/x.svg");

    [Test]
    public void Register_IndexesRowByKey_AndCollectsDependents()
    {
        var reconciler = new ProjectTreeReconciler();
        ElementId operand = Id(50);
        NodeKey eventRow = NodeKey.ForElement(Id(10));
        NodeKey commandRow = NodeKey.ForElement(Id(11));
        TreeNodeViewModel eventNode = Row("Kip -> ON");

        // Both rows render an operand's live name into their label, so both depend on that other element.
        reconciler.Register(eventRow, eventNode, operand);
        reconciler.Register(commandRow, Row("Kip Udgang"), operand);

        Assert.Multiple(() =>
        {
            Assert.That(reconciler.Count, Is.EqualTo(2));
            Assert.That(reconciler.Find(eventRow), Is.SameAs(eventNode));
            // a change to `operand` must re-render exactly the two dependent rows
            Assert.That(reconciler.DependentsOf(operand), Is.EquivalentTo(new[] { eventRow, commandRow }));
            // an element nothing derives from has no dependents
            Assert.That(reconciler.DependentsOf(Id(999)), Is.Empty);
        });
    }

    [Test]
    public void Register_WithNoDependencies_IndexesRow_WithNoDependents()
    {
        var reconciler = new ProjectTreeReconciler();
        NodeKey plainRow = NodeKey.ForElement(Id(20));

        reconciler.Register(plainRow, Row("Stue"));

        Assert.Multiple(() =>
        {
            Assert.That(reconciler.Find(plainRow), Is.Not.Null);
            Assert.That(reconciler.DependentsOf(Id(20)), Is.Empty);
        });
    }
}
