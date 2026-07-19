using System;
using System.Collections.Generic;
using Ihc.Vis.Model;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-2: the data structures the tree reconciler (W3-4) mutates incrementally instead of clearing and
/// rebuilding both panes on every edit. Two plain dictionaries (a Frozen index is for wholesale rebuilds, not
/// incremental mutation):
/// <list type="bullet">
/// <item>the <b>index</b> — every projected row by its stable <see cref="NodeKey"/>, so a change resolves to the
/// exact row in O(1) rather than a linear <c>FindNode</c> scan (P4);</item>
/// <item>the <b>dependency map</b> — element id → the keys of rows whose text/icon are DERIVED from that OTHER
/// element (program operands, opposite-link paths, scene rows, ancestor names). When an element changes or is
/// removed, exactly these rows must be re-rendered.</item>
/// </list>
/// W3-4 adds <c>Reconcile(project, changeSet)</c> on top of this surface.
/// </summary>
public sealed class ProjectTreeReconciler
{
    // Every projected row, keyed by its stable identity — the O(1) lookup Reconcile resolves a change against.
    private readonly Dictionary<NodeKey, TreeNodeViewModel> _index = new();

    // Reverse map: an element id → the keys of rows whose rendering is derived from it. A row appears here once
    // per OTHER element it reads (not for the element it stands for). Mutated incrementally by Register.
    private readonly Dictionary<ElementId, HashSet<NodeKey>> _dependents = new();

    /// <summary>The number of indexed rows.</summary>
    public int Count => _index.Count;

    /// <summary>The row indexed under <paramref name="key"/>, or null if none.</summary>
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
}
