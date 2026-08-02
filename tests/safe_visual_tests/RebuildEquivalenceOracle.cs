using System.Collections.Generic;
using System.Linq;
using CsCheck;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// fablerefac W3-3: the rebuild-equivalence safety net that makes in-place reconciliation (D3) tractable — it turns
/// the riskiest choice into a mechanically-verified one. It generates randomized <see cref="ProjectCommand"/>
/// sequences and, after every committed edit, asserts that a <see cref="ReconcileStep"/> produces a forest
/// <b>structurally identical</b> to one built from scratch by the shared <see cref="ProjectTreeProjector"/> (W3-1):
/// same identity/kind, text, icon, bold/unlinked/locked state, and child order.
/// <para>
/// W3-3 leaves the harness in place with two reference strategies: <see cref="RebuildFromScratch"/> (correct-but-
/// trivial — the property must hold) and <see cref="StaleNoOp"/> (deliberately divergent — proves the oracle has
/// teeth). W3-4 swaps the rebuild reference for the real incremental <c>ProjectTreeReconciler</c> and this same
/// property is what keeps it honest (red until correct).
/// </para>
/// Tree content only — selection/focus/scroll survival is covered separately by the W3-6 headless tests.
/// </summary>
internal static class RebuildEquivalenceOracle
{
    /// <summary>A reconcile strategy: given the current forest root, the change set of the edit just committed, and
    /// the updated project, produce the new forest root. The from-scratch rebuild the oracle compares against uses
    /// the same projector.</summary>
    internal delegate TreeNodeViewModel ReconcileStep(
        TreeNodeViewModel currentRoot, ProjectChangeSet changes, Project updated);

    /// <summary>Builds the installation-pane forest for a project through the shared projector (the same row path
    /// the VM and the reconciler use).</summary>
    internal static TreeNodeViewModel BuildInstallationForest(Project project) =>
        new ProjectTreeProjector(project).BuildLocalitiesRoot(functions: false);

    /// <summary>The first structural difference between two forests, or null when they are identical on the fields a
    /// re-render can change (identity, kind, text, icon, bold/unlinked/locked state) and on child order.</summary>
    internal static string? StructuralDifference(TreeNodeViewModel expected, TreeNodeViewModel actual, string path = "root")
    {
        if (expected.DisplayName != actual.DisplayName)
            return $"{path}: DisplayName '{expected.DisplayName}' != '{actual.DisplayName}'";
        if (expected.IconAsset != actual.IconAsset)
            return $"{path}: IconAsset '{expected.IconAsset}' != '{actual.IconAsset}'";
        if (expected.NodeKind != actual.NodeKind)
            return $"{path}: NodeKind '{expected.NodeKind}' != '{actual.NodeKind}'";
        if (expected.ElementId != actual.ElementId)
            return $"{path}: ElementId {expected.ElementId} != {actual.ElementId}";
        if (expected.IsBold != actual.IsBold)
            return $"{path}: IsBold {expected.IsBold} != {actual.IsBold}";
        if (expected.IsUnlinked != actual.IsUnlinked)
            return $"{path}: IsUnlinked {expected.IsUnlinked} != {actual.IsUnlinked}";
        if (expected.IsLockedFunctionBlock != actual.IsLockedFunctionBlock)
            return $"{path}: IsLockedFunctionBlock {expected.IsLockedFunctionBlock} != {actual.IsLockedFunctionBlock}";
        if (expected.Children.Count != actual.Children.Count)
            return $"{path}: child count {expected.Children.Count} != {actual.Children.Count}";
        string? childDifference = null;
        for (int i = 0; i < expected.Children.Count && childDifference is null; i++)
        {
            childDifference = StructuralDifference(expected.Children[i], actual.Children[i], $"{path}/{i}");
        }
        return childDifference;
    }

    // ---- randomized command generation over the localities (the change classes a reconciler must handle: Added,
    //      Changed label, ChildListChanged order, Removed). W3-4 extends this to products/blocks/pins. ----

    internal abstract record Op;
    internal sealed record AddLocalityOp(string Name) : Op;
    internal sealed record RenameOp(int Pick, string Name) : Op;
    internal sealed record ReorderOp(int Pick, int Position) : Op;
    internal sealed record DeleteOp(int Pick) : Op;

    private static readonly Gen<string> NameGen =
        Gen.OneOfConst("abcABC012".ToCharArray()).Array[1, 5].Select(cs => new string(cs));

    private static readonly Gen<Op> AnyOp = Gen.OneOf(
        NameGen.Select(n => (Op)new AddLocalityOp(n)),
        Gen.Select(Gen.Int[0, 20], NameGen, (pick, name) => (Op)new RenameOp(pick, name)),
        Gen.Select(Gen.Int[0, 20], Gen.Int[0, 20], (pick, pos) => (Op)new ReorderOp(pick, pos)),
        Gen.Int[0, 20].Select(pick => (Op)new DeleteOp(pick)));

    /// <summary>A random command sequence that always opens with a guaranteed-mutating <see cref="AddLocalityOp"/>,
    /// so a reconcile that drops changes diverges on step one — the teeth demonstration is seed-independent.</summary>
    internal static readonly Gen<Op[]> CommandSequence = Gen.Select(
        NameGen, AnyOp.Array[0, 7], (first, rest) => (Op[])[new AddLocalityOp(first), .. rest]);

    // Resolves an abstract op against the live project, targeting localities by index (mod count) so it stays valid
    // as the project shrinks/grows; null when there is no locality to target.
    private static ProjectCommand? Interpret(Op op, Project project)
    {
        IReadOnlyList<ProjectElement> groups = project.Groups;
        return op switch
        {
            AddLocalityOp add => new AddLocality(add.Name),
            RenameOp r when groups.Count > 0 =>
                new RenameLocality(groups[r.Pick % groups.Count].Id!.Value, r.Name, string.Empty),
            ReorderOp ro when groups.Count > 0 =>
                new ReorderNode(groups[ro.Pick % groups.Count].Id!.Value, ro.Position % groups.Count),
            DeleteOp d when groups.Count > 0 =>
                new DeleteLocality(groups[d.Pick % groups.Count].Id!.Value),
            _ => null,
        };
    }

    /// <summary>Drives one command sequence: reconciles after each committed edit and compares to a from-scratch
    /// rebuild. Returns the first divergence, or null when the reconcile stayed rebuild-equivalent throughout.</summary>
    internal static string? FirstDivergence(Project baseProject, Op[] ops, ReconcileStep reconcile)
    {
        // The session is lock-serialized (D04) but driven synchronously within this one call (no awaits), so the
        // whole sequence is a single-mutator run on the calling thread. CsCheck sampling runs single-threaded (see Check).
        var session = new ProjectDocumentSession();
        session.Open(baseProject);
        TreeNodeViewModel forest = BuildInstallationForest(session.Current!);
        string? divergence = null;
        foreach (Op op in ops)
        {
            if (Interpret(op, session.Current!) is not { } command)
            {
                continue;
            }
            EditOutcome outcome = session.Apply(command);
            if (outcome.Status != EditStatus.Committed)
            {
                continue;
            }
            forest = reconcile(forest, outcome.Changes!, session.Current!);
            divergence = StructuralDifference(BuildInstallationForest(session.Current!), forest);
            if (divergence is not null)
            {
                break;
            }
        }
        return divergence;
    }

    /// <summary>The correct-but-trivial reference reconcile: rebuild the whole forest each edit. Rebuild-equivalent
    /// by construction. W3-4 replaces it with the real incremental reconciler, which must satisfy the same property.</summary>
    internal static readonly ReconcileStep RebuildFromScratch = (_, _, updated) => BuildInstallationForest(updated);

    // ---- W3-4: the real incremental reconciler driven through the same oracle. A fresh ProjectTreeReconciler per
    //      sequence is seeded from the base project and then reconciled after each committed edit; its in-place
    //      forest must stay structurally identical to a from-scratch rebuild throughout. ----

    /// <summary>Drives one command sequence through the real <see cref="ProjectTreeReconciler"/>: seeds it from the
    /// base project, reconciles after each committed edit, and returns the first divergence from a from-scratch
    /// rebuild (or null when it stayed rebuild-equivalent).</summary>
    internal static string? FirstDivergenceIncremental(Project baseProject, Op[] ops)
    {
        var session = new ProjectDocumentSession();
        session.Open(baseProject);
        var reconciler = new ProjectTreeReconciler(p => new ProjectTreeProjector(p).BuildLocalitiesRoot(functions: false));
        reconciler.Rebuild(session.Current!);
        string? divergence = null;
        foreach (Op op in ops)
        {
            if (Interpret(op, session.Current!) is not { } command)
            {
                continue;
            }
            EditOutcome outcome = session.Apply(command);
            if (outcome.Status != EditStatus.Committed)
            {
                continue;
            }
            TreeNodeViewModel forest = reconciler.Reconcile(session.Current!, outcome.Changes!);
            divergence = StructuralDifference(BuildInstallationForest(session.Current!), forest);
            if (divergence is not null)
            {
                break;
            }
        }
        return divergence;
    }

    /// <summary>Samples randomized command sequences against the real incremental reconciler (fixed iteration count,
    /// single-threaded for determinism); throws (CsCheck) on the first sequence whose in-place reconcile diverges
    /// from a from-scratch rebuild.</summary>
    internal static void CheckIncremental(Project baseProject, long iter = 100) =>
        CommandSequence.Sample(
            ops => FirstDivergenceIncremental(baseProject, ops) is null,
            iter: iter,
            threads: 1,
            print: ops => string.Join(" ; ", ops.Select(op => op.ToString())));

    /// <summary>A deliberately divergent reconcile that ignores the change set (keeps the stale forest). Used only to
    /// prove the oracle catches a wrong reconcile.</summary>
    internal static readonly ReconcileStep StaleNoOp = (current, _, _) => current;

    /// <summary>Samples randomized command sequences against <paramref name="reconcile"/> with a fixed iteration
    /// count, single-threaded for determinism; throws (CsCheck) on the first sequence that diverges from a
    /// from-scratch rebuild. CsCheck logs the seed of any counterexample so a failure replays deterministically.</summary>
    internal static void Check(Project baseProject, ReconcileStep reconcile, long iter = 100) =>
        CommandSequence.Sample(
            ops => FirstDivergence(baseProject, ops, reconcile) is null,
            iter: iter,
            threads: 1,
            print: ops => string.Join(" ; ", ops.Select(op => op.ToString())));
}
