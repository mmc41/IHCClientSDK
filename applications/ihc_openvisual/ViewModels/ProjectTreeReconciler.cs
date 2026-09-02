using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-2/W3-4: the keyed tree reconciler. Instead of clearing and rebuilding both panes on every edit,
/// it updates the projected forest <b>in place</b> from a <see cref="ProjectChangeSet"/>, preserving node identity
/// (so Avalonia keeps container/selection/expansion state) and re-rendering only what changed. Two plain
/// dictionaries back it (a Frozen index is for wholesale rebuilds, not incremental mutation):
/// <list type="bullet">
/// <item>the <b>index</b> — every projected row by its stable <see cref="NodeKey"/>, so a change resolves to the
/// exact row in O(1) rather than a linear <c>FindNode</c> scan (P4);</item>
/// <item>the <b>dependency map</b> — element id → the keys of rows whose text/icon are DERIVED from that OTHER
/// element (link opposite paths, scene rows, program operands). When such an element changes or is removed,
/// exactly these rows must be re-rendered even though their own subtree is untouched by the change set.</item>
/// </list>
/// <para>
/// W3-4 sources fresh row values from a full re-projection through the shared <see cref="ProjectTreeProjector"/>
/// (W3-1) — the same row path the from-scratch fallback uses — and applies only the delta the change set names, so
/// unchanged subtrees are never touched. The W3-3 CsCheck oracle keeps it rebuild-equivalent. Any inconsistency
/// (<see cref="ProjectChangeSet.FullReload"/>, an unkeyable synthetic row, an unresolvable parent) falls back to a
/// full rebuild — always correct, just not incremental.
/// </para>
/// </summary>
public sealed class ProjectTreeReconciler
{
    // Builds a whole pane's forest from a project (installation vs functions) through the shared projector. Null in
    // the W3-2 manual mode (Register/Find only); Reconcile/Rebuild require it.
    private readonly Func<Project, TreeNodeViewModel>? _buildForest;

    // Every projected row, keyed by its stable identity — the O(1) lookup Reconcile resolves a change against.
    private readonly Dictionary<NodeKey, TreeNodeViewModel> _index = new();

    // Reverse map: an element id → the keys of rows whose rendering is derived from it. A row appears here once
    // per OTHER element it reads (not for the element it stands for).
    private readonly Dictionary<ElementId, HashSet<NodeKey>> _dependents = new();

    private TreeNodeViewModel? _root;

    // The synthetic Localities root stands for the id-bearing `groups` container, so a change set that names the
    // groups id maps to the root. Captured per (re)build/reconcile from the current project.
    private ElementId? _rootElementId;

    /// <summary>Creates a reconciler. <paramref name="buildForest"/> projects a whole pane (e.g.
    /// <c>p =&gt; new ProjectTreeProjector(p).BuildLocalitiesRoot(functions: false)</c>); omit it for the W3-2
    /// manual index surface only.</summary>
    public ProjectTreeReconciler(Func<Project, TreeNodeViewModel>? buildForest = null) => _buildForest = buildForest;

    /// <summary>The current forest root, or null before the first <see cref="Rebuild"/>.</summary>
    public TreeNodeViewModel? Root => _root;

    /// <summary>The number of indexed rows. Intentional test-only seam (D02): read only by the reconciler-index
    /// tests to assert the index population; kept as an observability handle, not dead.</summary>
    public int Count => _index.Count;

    /// <summary>The row indexed under <paramref name="key"/>, or null if none. Intentional test-only seam (D02):
    /// the reconcile is driven internally; this lookup is exposed for the tests to inspect a specific row by key.</summary>
    public TreeNodeViewModel? Find(NodeKey key) => _index.GetValueOrDefault(key);

    /// <summary>
    /// Indexes <paramref name="node"/> under <paramref name="key"/> and records that its rendering is derived
    /// from <paramref name="dependsOn"/> — the OTHER elements whose name/value bleeds into this row's label or
    /// icon — so a later change to any of them re-renders this row.
    /// </summary>
    public void Register(NodeKey key, TreeNodeViewModel node, params ReadOnlySpan<ElementId> dependsOn)
    {
        _index[key] = node;
        foreach (ElementId element in dependsOn)
        {
            if (!_dependents.TryGetValue(element, out HashSet<NodeKey>? keys))
            {
                keys = new HashSet<NodeKey>();
                _dependents[element] = keys;
            }
            keys.Add(key);
        }
    }

    /// <summary>The keys of rows whose rendering is derived from <paramref name="element"/> — the set to
    /// re-render when it changes or is removed. Empty when nothing depends on it.</summary>
    public IReadOnlyCollection<NodeKey> DependentsOf(ElementId element) =>
        _dependents.TryGetValue(element, out HashSet<NodeKey>? keys)
            ? keys
            : (IReadOnlyCollection<NodeKey>)Array.Empty<NodeKey>();

    /// <summary>Projects the whole pane from scratch, seeding the forest, index and dependency map. This is both the
    /// initial build and the fallback for any change the incremental path can't confidently apply in place.</summary>
    public TreeNodeViewModel Rebuild(Project project)
    {
        Func<Project, TreeNodeViewModel> build = RequireBuilder();
        _root = build(project);
        RebuildIndex(project);
        return _root;
    }

    /// <summary>Updates the forest in place for the committed <paramref name="changes"/> against the now-current
    /// <paramref name="project"/>, preserving node identity, and returns the (same) root. Falls back to a full
    /// <see cref="Rebuild"/> on <see cref="ProjectChangeSet.FullReload"/> or any inconsistency.</summary>
    public TreeNodeViewModel Reconcile(Project project, ProjectChangeSet changes)
    {
        if (_root is null || changes.FullReload)
        {
            return Rebuild(project);
        }

        // Fresh full projection = the value source for every re-rendered field and every added subtree — the same
        // row path the from-scratch fallback uses (projector reuse). We then apply only the named delta.
        TreeNodeViewModel fresh = RequireBuilder()(project);
        if (!TryIndexForest(fresh, out Dictionary<NodeKey, TreeNodeViewModel> freshByKey))
        {
            return Rebuild(project);   // an unkeyable synthetic row in this pane → safe full rebuild
        }

        // Dependents to re-render — captured from the OLD dependency map BEFORE any structural mutation removes it
        // (invalidation-order caution).
        var dependentsToReRender = new HashSet<NodeKey>();
        foreach (ElementId id in changes.Changed)
        {
            dependentsToReRender.UnionWith(DependentsOf(id));
        }
        foreach (ElementId id in changes.Removed)
        {
            dependentsToReRender.UnionWith(DependentsOf(id));
        }

        // (1) Structural: every id-bearing parent whose child list changed (covers Added/Removed/reorder at that
        // level, and the groups→root mapping). A miss means an inconsistency we can't reconcile in place.
        foreach (ElementId parentId in changes.ChildListChanged)
        {
            if (!ReconcileChildrenOf(parentId, freshByKey))
            {
                return Rebuild(project);
            }
        }

        // (2) Content: re-render each changed node's own fields (label/icon/state) in place.
        foreach (ElementId id in changes.Changed)
        {
            ReRenderInPlace(NodeKey.ForElement(id), freshByKey);
        }

        // (3) Cross-references: re-render rows whose label derives from a changed/removed element (link opposite
        // paths, scene rows, program operands) — their own subtree was untouched by the change set.
        foreach (NodeKey key in dependentsToReRender)
        {
            ReRenderInPlace(key, freshByKey);
        }

        RebuildIndex(project, freshByKey);   // repoint index + dependency map (edges sourced from the fresh projection)
        return _root;
    }

    // ---- internals ----

    private Func<Project, TreeNodeViewModel> RequireBuilder() =>
        _buildForest ?? throw new InvalidOperationException(
            $"{nameof(ProjectTreeReconciler)} was created without a forest builder; Rebuild/Reconcile require one.");

    // Indexes a freshly-projected forest by NodeKey (the value source for the reconcile). Returns false if any row
    // is an id-less synthetic that can't be keyed in this pane, signalling the caller to fall back to a full rebuild.
    private bool TryIndexForest(TreeNodeViewModel forest, out Dictionary<NodeKey, TreeNodeViewModel> byKey)
    {
        byKey = new Dictionary<NodeKey, TreeNodeViewModel>();
        return TryIndexForestInto(forest, byKey);
    }

    private bool TryIndexForestInto(TreeNodeViewModel node, Dictionary<NodeKey, TreeNodeViewModel> byKey)
    {
        if (ForestKeyOf(node) is not { } key)
        {
            return false;
        }
        byKey[key] = node;
        foreach (TreeNodeViewModel child in node.Children)
        {
            if (!TryIndexForestInto(child, byKey))
            {
                return false;
            }
        }
        return true;
    }

    // Merges the OLD parent's children to match the FRESH parent's children, reusing existing instances by key
    // (preserving identity), adding fresh subtrees for new keys, and dropping vanished ones. Reads the OLD index to
    // find the parent; false when the parent is unresolvable or a child can't be keyed (→ caller falls back).
    private bool ReconcileChildrenOf(ElementId parentId, Dictionary<NodeKey, TreeNodeViewModel> freshByKey)
    {
        NodeKey parentKey = NodeKey.ForElement(parentId);
        if (freshByKey.GetValueOrDefault(parentKey) is not { } freshParent)
        {
            // The changed parent is not a row in the FRESH projection of this pane — it belongs to the other pane, so
            // this pane is unaffected by the edit at this level.
            return true;
        }
        if (_index.GetValueOrDefault(parentKey) is not { } oldParent)
        {
            // The parent is a row in the fresh tree but had no node before: the projection's conditional visibility
            // changed (e.g. an FB section revealed by its first variable, US-018). Its rendered ancestor gained a
            // child the change set can't name at that level, so rebuild this pane (safe, re-seeds the reconciler).
            return false;
        }
        return MergeChildren(oldParent, freshParent);
    }

    private static bool MergeChildren(TreeNodeViewModel oldParent, TreeNodeViewModel freshParent)
    {
        bool wasEmpty = oldParent.Children.Count == 0;
        var oldByKey = new Dictionary<NodeKey, TreeNodeViewModel>();
        foreach (TreeNodeViewModel child in oldParent.Children)
        {
            if (child.ElementId is not { } id)
            {
                return false;   // synthetic child (other panes) — fall back
            }
            oldByKey[NodeKey.ForElement(id)] = child;
        }

        var ordered = new List<TreeNodeViewModel>(freshParent.Children.Count);
        foreach (TreeNodeViewModel freshChild in freshParent.Children)
        {
            if (freshChild.ElementId is not { } id)
            {
                return false;
            }
            NodeKey key = NodeKey.ForElement(id);
            if (oldByKey.Remove(key, out TreeNodeViewModel? reused))
            {
                CopyRenderedFields(reused, freshChild);   // a reused row's own label may still have changed
                ordered.Add(reused);
            }
            else
            {
                ordered.Add(freshChild);   // new subtree — fresh instances (indexed by the closing RebuildIndex)
            }
        }
        // oldByKey now holds the vanished children; they simply drop out of the ordered list.
        ApplyChildOrder(oldParent.Children, ordered);
        // US-006: a node revealing its FIRST child opens to show it — take the projector's reveal flag (set on a
        // locality gaining contents, not on a product gaining a pin) rather than inheriting the stale collapsed
        // state. Reading the flag rather than the fresh row's IsExpanded matters because a locality's resting state
        // is closed: the two would otherwise have to be the same value and only one of them can be right.
        // A node that already had children keeps the installer's expansion by identity (US-070).
        if (wasEmpty && oldParent.Children.Count > 0 && freshParent.RevealsOnFirstChild)
        {
            oldParent.IsExpanded = true;
        }
        return true;
    }

    // Re-renders the row indexed under `key` from its fresh projection (own fields only; identity preserved).
    private void ReRenderInPlace(NodeKey key, Dictionary<NodeKey, TreeNodeViewModel> freshByKey)
    {
        if (_index.GetValueOrDefault(key) is { } node && freshByKey.GetValueOrDefault(key) is { } fresh)
        {
            CopyRenderedFields(node, fresh);
        }
    }

    // Copies the reconciler-owned re-rendered fields (W3-5 observable properties) from a fresh projection onto an
    // existing node. The observable setters no-op when the value is unchanged, so this is cheap and quiet.
    private static void CopyRenderedFields(TreeNodeViewModel target, TreeNodeViewModel source)
    {
        target.DisplayName = source.DisplayName;
        target.IconAsset = source.IconAsset;
        target.IsBold = source.IsBold;
        target.IsUnlinked = source.IsUnlinked;
        target.IsLockedFunctionBlock = source.IsLockedFunctionBlock;
        target.Tooltip = source.Tooltip;
    }

    // Transforms `collection` into `desired` order with minimal Move/Insert/Remove so unchanged rows keep their
    // Avalonia containers (a Clear+re-add would tear the whole level down). Reference identity throughout.
    private static void ApplyChildOrder(ObservableCollection<TreeNodeViewModel> collection, List<TreeNodeViewModel> desired)
    {
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(collection[i]))
            {
                collection.RemoveAt(i);
            }
        }
        for (int i = 0; i < desired.Count; i++)
        {
            TreeNodeViewModel want = desired[i];
            if (i < collection.Count && ReferenceEquals(collection[i], want))
            {
                continue;
            }
            int existing = IndexOfReference(collection, want, i);
            if (existing >= 0)
            {
                collection.Move(existing, i);
            }
            else
            {
                collection.Insert(i, want);
            }
        }
    }

    private static int IndexOfReference(ObservableCollection<TreeNodeViewModel> collection, TreeNodeViewModel target, int start)
    {
        for (int i = start; i < collection.Count; i++)
        {
            if (ReferenceEquals(collection[i], target))
            {
                return i;
            }
        }
        return -1;
    }

    // Repopulates the index + dependency map from the current forest (called after a full build or an incremental
    // reconcile). Cheap POCO walk; keeps the maps authoritative without incremental deregistration bookkeeping.
    private void RebuildIndex(Project project, IReadOnlyDictionary<NodeKey, TreeNodeViewModel>? freshByKey = null)
    {
        _index.Clear();
        _dependents.Clear();
        _rootElementId = project.Child("groups")?.Id;
        if (_root is not null)
        {
            IndexSubtree(_root, freshByKey);
        }
    }

    private void IndexSubtree(TreeNodeViewModel node, IReadOnlyDictionary<NodeKey, TreeNodeViewModel>? freshByKey)
    {
        if (ForestKeyOf(node) is { } key)
        {
            // The cross-reference edges are emitted by the projector on each row (TreeNodeViewModel.CrossReferences).
            // A reused row instance may still carry the edges from a PRIOR projection, so in an incremental reconcile
            // prefer the FRESH projection's node for this key — matching the pre-T022 behaviour, which re-derived the
            // edges from the current project on every RebuildIndex.
            TreeNodeViewModel source = freshByKey?.GetValueOrDefault(key) ?? node;
            Register(key, node, source.CrossReferences.ToArray());
        }
        foreach (TreeNodeViewModel child in node.Children)
        {
            IndexSubtree(child, freshByKey);
        }
    }

    // The reconcile key of a projected row: its own element id, or — for the synthetic Localities root — the id of
    // the `groups` container it stands for. Null for any other id-less synthetic row (which forces a fallback).
    private NodeKey? ForestKeyOf(TreeNodeViewModel node) =>
        node.ElementId is { } id ? NodeKey.ForElement(id)
        : node.IsLocalitiesRoot && _rootElementId is { } rootId ? NodeKey.ForElement(rootId)
        : null;
}
