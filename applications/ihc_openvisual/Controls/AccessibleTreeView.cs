using System;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace ihc_openvisual.Controls;

/// <summary>
/// A <see cref="TreeView"/> whose item containers expose the UI-Automation <c>ExpandCollapse</c>
/// pattern to assistive technology and automation clients.
/// </summary>
/// <remarks>
/// Avalonia's stock <c>TreeViewItemAutomationPeer</c> implements only Scroll and SelectionItem, not
/// <see cref="IExpandCollapseProvider"/>. As a result a screen reader cannot announce a node as
/// "collapsed"/"expanded" nor expand it programmatically — the only route is the visual chevron toggle
/// button, which announces as an unrelated control. Producing <see cref="AccessibleTreeViewItem"/>
/// containers (which carry an ExpandCollapse-capable peer) closes that gap for the whole subtree.
/// <para><see cref="StyleKeyOverride"/> points at the base type so the subclass keeps <see cref="TreeView"/>'s
/// default control theme (Avalonia matches themes by exact type).</para>
/// </remarks>
public class AccessibleTreeView : TreeView
{
    protected override Type StyleKeyOverride => typeof(TreeView);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new AccessibleTreeViewItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<AccessibleTreeViewItem>(item, out recycleKey);
}

/// <summary>
/// A <see cref="TreeViewItem"/> that reports ExpandCollapse to UI Automation and produces the same
/// accessible container for its own children, so an arbitrarily deep tree stays fully accessible.
/// </summary>
public class AccessibleTreeViewItem : TreeViewItem
{
    protected override Type StyleKeyOverride => typeof(TreeViewItem);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new AccessibleTreeViewItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<AccessibleTreeViewItem>(item, out recycleKey);

    protected override AutomationPeer OnCreateAutomationPeer()
        => new ExpandCollapseTreeViewItemAutomationPeer(this);
}

/// <summary>
/// Extends Avalonia's tree-item peer with <see cref="IExpandCollapseProvider"/> so UI Automation
/// clients can read a node's expand state and expand/collapse it. A node with no children reports
/// <see cref="ExpandCollapseState.LeafNode"/>; otherwise its <see cref="TreeViewItem.IsExpanded"/> state.
/// </summary>
public class ExpandCollapseTreeViewItemAutomationPeer : TreeViewItemAutomationPeer, IExpandCollapseProvider
{
    private readonly TreeViewItem _item;

    public ExpandCollapseTreeViewItemAutomationPeer(TreeViewItem owner) : base(owner) => _item = owner;

    public ExpandCollapseState ExpandCollapseState =>
        _item.ItemCount == 0
            ? ExpandCollapseState.LeafNode
            : _item.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    // ExpandCollapse here never surfaces a context menu on expansion.
    public bool ShowsMenu => false;

    public void Expand() => _item.IsExpanded = true;

    public void Collapse() => _item.IsExpanded = false;
}
