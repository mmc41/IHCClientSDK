using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// T031 (S1): the two-pane tree-sync engine extracted from <see cref="MainWindowViewModel"/> — owns the two keyed
/// <see cref="ProjectTreeReconciler"/>s and drives, per edit, either an in-place reconcile (node identity preserved,
/// so Avalonia keeps selection/expansion) or a full rebuild that carries expansion across (US-070), plus the
/// programming-mode pane build. Mirrors the other coordinators' delegate-ctor shape: it holds the two pane
/// collections and reaches the view-model only through delegates (current project / last change / name / headers),
/// so it owns no Avalonia types beyond the bound collections and is headlessly testable. Selection capture/restore
/// stays in the view-model (it is view-state the VM owns and drives around these calls).
/// </summary>
internal sealed class TreePaneCoordinator(
    ObservableCollection<TreeNodeViewModel> installationNodes,
    ObservableCollection<TreeNodeViewModel> functionNodes,
    Func<Project?> currentProject,
    Func<ProjectChangeSet?> lastChange,
    Action<string, string> setHeaders)
{
    // One reconciler per pane. It reconciles in place from a ProjectChangeSet, preserving node identity; a fallback
    // (or a reconcile that falls back) rebuilds through the same reconciler, which re-seeds it.
    private readonly ProjectTreeReconciler _installationReconciler =
        new(p => new ProjectTreeProjector(p).BuildLocalitiesRoot(functions: false));
    private readonly ProjectTreeReconciler _functionsReconciler =
        new(p => new ProjectTreeProjector(p).BuildLocalitiesRoot(functions: true));

    private string? _lastBuiltViewKey;

    /// <summary>Records the view about to be built and reports whether it is the SAME as the last build — i.e. whether
    /// this is an in-place refresh whose expansion should be carried across, rather than a mode switch that opens
    /// fresh.</summary>
    public bool SameViewAsLastBuild(string key)
    {
        bool same = _lastBuiltViewKey == key;
        _lastBuiltViewKey = key;
        return same;
    }

    /// <summary>Config mode: reconcile BOTH panes in place when this is an incremental edit whose panes still hold the
    /// reconcilers' roots. Returns false (doing nothing) when an in-place reconcile is not possible, so the caller
    /// falls back to <see cref="RebuildConfig"/> (with its own selection capture/restore).</summary>
    public bool TryReconcileConfig()
    {
        if (currentProject() is { } current && lastChange() is { } changes
            && PaneHoldsRoot(installationNodes, _installationReconciler)
            && PaneHoldsRoot(functionNodes, _functionsReconciler))
        {
            ReconcilePane(installationNodes, _installationReconciler, current, changes);
            ReconcilePane(functionNodes, _functionsReconciler, current, changes);
            return true;
        }
        return false;
    }

    /// <summary>Config mode full-rebuild fallback (load/save/close/mode switch/first build, or panes out of sync —
    /// undo/redo reconcile in place since crudarch T007): rebuild BOTH panes through the reconciler, carrying
    /// expansion across unless <paramref name="preserve"/> is false (a mode switch, whose fresh defaults ARE the
    /// wanted state).</summary>
    public void RebuildConfig(bool preserve)
    {
        RebuildPaneFallback(installationNodes, _installationReconciler, preserve);
        RebuildPaneFallback(functionNodes, _functionsReconciler, preserve);
    }

    /// <summary>Programming mode (US-026): the left pane shows the block's variable sections, the right pane its
    /// program subtree (Programs > Program > { Events, Commands }); both headers carry the block's name.</summary>
    public void BuildProgrammingTrees(ProjectElement block, bool preserveExpansion)
    {
        string name = currentProject()!.NameOr(block, "block");
        setHeaders(name, name);

        RebuildPreservingExpansion(installationNodes, preserveExpansion, () =>
        {
            installationNodes.Clear();
            // block → Input/Output/Settings/Internal variables (row projection extracted to ProjectTreeProjector, W3-1)
            installationNodes.Add(new ProjectTreeProjector(currentProject()!).BuildFunctionBlockNode(block, name, programmingMode: true));
        });
        RebuildPreservingExpansion(functionNodes, preserveExpansion, () =>
        {
            functionNodes.Clear();
            // block → Programs → Program → Events/Commands (row projection extracted to ProjectTreeProjector, W3-1)
            functionNodes.Add(new ProjectTreeProjector(currentProject()!).BuildBlockProgramsNode(block, name));
        });
    }

    // Whether the pane currently holds exactly the reconciler's root instance — the precondition for an in-place
    // reconcile (a fallback rebuild or a mode switch leaves them out of sync until the next re-seed).
    private static bool PaneHoldsRoot(ObservableCollection<TreeNodeViewModel> pane, ProjectTreeReconciler reconciler) =>
        reconciler.Root is { } root && pane.Count == 1 && ReferenceEquals(pane[0], root);

    // In-place reconcile: the root instance is preserved, so selection/expansion survive by identity. If the
    // reconciler had to fall back internally (a new root), re-point the pane at it.
    private static void ReconcilePane(ObservableCollection<TreeNodeViewModel> pane, ProjectTreeReconciler reconciler,
        Project current, ProjectChangeSet changes)
    {
        TreeNodeViewModel root = reconciler.Reconcile(current, changes);
        if (pane.Count != 1 || !ReferenceEquals(pane[0], root))
        {
            pane.Clear();
            pane.Add(root);
        }
    }

    // Full-rebuild fallback (US-070): rebuild the pane through the reconciler (which re-seeds it with the new root)
    // and carry each surviving node's expand/collapse state across (via the shared RebuildPreservingExpansion), unless
    // this is a deliberate mode switch (preserve=false), where the fresh defaults ARE the wanted state.
    private void RebuildPaneFallback(ObservableCollection<TreeNodeViewModel> pane, ProjectTreeReconciler reconciler,
        bool preserve) =>
        RebuildPreservingExpansion(pane, preserve, () =>
        {
            TreeNodeViewModel root = currentProject() is { } project
                ? reconciler.Rebuild(project)
                : new TreeNodeViewModel("Localities", NodeIcons.Locality, isExpanded: true) { Kind = TreeNodeKind.LocalitiesRoot };
            pane.Clear();
            pane.Add(root);
        });

    // Carries each surviving node's expand/collapse state across a full pane rebuild (US-070): every edit clears and
    // repopulates the pane, so without this the fresh nodes snap back to their build-time defaults and the whole tree
    // collapses on every change. Snapshot is taken BEFORE <paramref name="populate"/> clears the pane, and restored
    // after; skipped (preserve=false) on a mode switch, where the fresh defaults ARE the wanted state.
    private static void RebuildPreservingExpansion(ObservableCollection<TreeNodeViewModel> target, bool preserve, Action populate)
    {
        Dictionary<ElementId, bool>? previous = preserve ? SnapshotExpansion(target) : null;
        populate();
        if (previous is not null)
            RestoreExpansion(target, previous);
    }

    private static Dictionary<ElementId, bool> SnapshotExpansion(IEnumerable<TreeNodeViewModel> nodes)
    {
        var map = new Dictionary<ElementId, bool>();
        CollectExpansion(nodes, map);
        return map;
    }

    // Records the expand/collapse state of every node that CURRENTLY HAS CHILDREN, keyed by element id. The
    // "has children" gate is what lets a node revealing its FIRST child (an empty locality gaining a product,
    // US-006) keep its open-by-default state rather than inherit a stale collapsed one, while a node that was
    // already a parent carries the installer's expansion across the rebuild (US-070).
    private static void CollectExpansion(IEnumerable<TreeNodeViewModel> nodes, Dictionary<ElementId, bool> into)
    {
        foreach (TreeNodeViewModel node in nodes)
        {
            if (node.ElementId is { } id && node.Children.Count > 0)
                into[id] = node.IsExpanded;
            CollectExpansion(node.Children, into);
        }
    }

    private static void RestoreExpansion(IEnumerable<TreeNodeViewModel> nodes, IReadOnlyDictionary<ElementId, bool> previous)
    {
        foreach (TreeNodeViewModel node in nodes)
        {
            if (node.ElementId is { } id && previous.TryGetValue(id, out bool wasExpanded))
                node.IsExpanded = wasExpanded;
            RestoreExpansion(node.Children, previous);
        }
    }
}
