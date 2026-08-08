using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace ihc_openvisual.Controls;

/// <summary>
/// One row of a read-only pseudo-table (the data-line module map, US-050) that UI Automation can actually see: a
/// <see cref="Grid"/> that reports itself as a list item carrying the row's whole content as its name.
/// </summary>
/// <remarks>
/// <para>These tables are laid out as a header <see cref="Grid"/> above an <see cref="ItemsControl"/> of matching
/// row grids — the arrangement that gives a sighted reader columns under captions. It gives an automation client
/// nothing: Avalonia's Windows bridge exposes no Grid/Table pattern at all, so there is no cell-by-row-and-column
/// query and no programmatic association between a value and its header, and a bare <see cref="Panel"/> produces a
/// <c>NoneAutomationPeer</c> — so the rows themselves are not elements, leaving 24 loose text runs with no structure
/// between them (UX review USE-01).</para>
/// <para>What the platform CAN carry is the row as ONE named element, which is what this is: control type
/// <see cref="AutomationControlType.ListItem"/> (so the row is a real item in the tree and the enclosing list has
/// items) plus the caller's <c>AutomationProperties.Name</c> — the header captions spelled into the value, since a
/// header a client cannot reach may as well not be there — and its <c>AutomationProperties.AutomationId</c>. The
/// cell <c>TextBlock</c>s are marked <c>AccessibilityView="Raw"</c> at the call site so the row reads once rather
/// than four times.</para>
/// <para>Same shape as <see cref="AccessibleTreeView"/> and <see cref="AccessibleMenu"/>: the app supplies the peer
/// Avalonia's stock control does not. <c>StyleKeyOverride</c> is unnecessary — <see cref="Grid"/> carries no control
/// theme.</para>
/// </remarks>
public class AccessibleDataRow : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DataRowAutomationPeer(this);
}

/// <summary>A peer that reports its owner as a list item; name and id come from the attached
/// <c>AutomationProperties</c> the base peer already reads.</summary>
public class DataRowAutomationPeer : ControlAutomationPeer
{
    public DataRowAutomationPeer(Control owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
}
