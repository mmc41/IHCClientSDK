using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests;

/// <summary>
/// fablerefac W3-3: proves the rebuild-equivalence oracle (the W3-4 safety net) has teeth and is sound. The RED for
/// a safety-harness task is demonstrating the oracle detects a known-wrong reconcile — here framed as green
/// meta-assertions (the oracle DOES report the divergence).
/// </summary>
public class RebuildEquivalenceOracleTests
{
    private static Project NewBaseProject() =>
        new ProjectAppService(new IhcSettings())
            .CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));

    // Teeth + soundness of the structural comparator itself (the spec's ChildListChanged-reorder example), without
    // CsCheck: two builds of the same project are identical, but a reordered child list is detected.
    [Test]
    public void StructuralDifference_IsNullForSameProject_ButDetectsAReorder()
    {
        Project project = NewBaseProject();
        TreeNodeViewModel before = RebuildEquivalenceOracle.BuildInstallationForest(project);

        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId firstLocality = project.Groups.First().Id!.Value;
        EditOutcome outcome = session.Apply(new ReorderNode(firstLocality, 3));   // move room 0 → position 3
        TreeNodeViewModel afterReorder = RebuildEquivalenceOracle.BuildInstallationForest(session.Current!);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
            // sound: two independent builds of the same project are structurally identical
            Assert.That(RebuildEquivalenceOracle.StructuralDifference(
                before, RebuildEquivalenceOracle.BuildInstallationForest(project)), Is.Null);
            // teeth: a reordered child list is caught as a divergence
            Assert.That(RebuildEquivalenceOracle.StructuralDifference(before, afterReorder), Is.Not.Null);
        });
    }

    // Teeth through the full CsCheck harness: a reconcile that drops the change set diverges from a from-scratch
    // rebuild, and the oracle catches it (Sample throws on the counterexample) over randomized command sequences.
    [Test]
    public void Oracle_CatchesAStaleReconcile()
    {
        Project project = NewBaseProject();
        Assert.That(
            () => RebuildEquivalenceOracle.Check(project, RebuildEquivalenceOracle.StaleNoOp, iter: 100),
            Throws.Exception, "the oracle must detect a reconcile that ignores the change set");
    }

    // Soundness end-to-end (the harness W3-4 inherits): a correct rebuild-from-scratch reconcile stays
    // rebuild-equivalent over every randomized sequence, so the oracle reports no divergence. W3-4 swaps this
    // reference reconcile for the real incremental reconciler and this property must still hold.
    [Test]
    public void Oracle_AcceptsARebuildEquivalentReconcile()
    {
        Project project = NewBaseProject();
        Assert.DoesNotThrow(
            () => RebuildEquivalenceOracle.Check(project, RebuildEquivalenceOracle.RebuildFromScratch, iter: 100));
    }

    // W3-4: the safety net now drives the REAL incremental reconciler. Over randomized Add/Rename/Reorder/Delete
    // sequences its in-place forest must stay structurally identical to a from-scratch rebuild — the property that
    // keeps keyed reconciliation honest.
    [Test]
    public void Oracle_AcceptsTheRealIncrementalReconciler()
    {
        Project project = NewBaseProject();
        Assert.DoesNotThrow(() => RebuildEquivalenceOracle.CheckIncremental(project, iter: 200));
    }
}
