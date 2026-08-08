using System;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace ihc_openvisual.Controls;

/// <summary>
/// A <see cref="Menu"/> whose items expose the UI-Automation <c>Invoke</c> and <c>ExpandCollapse</c> patterns.
/// </summary>
/// <remarks>
/// Avalonia's stock <c>MenuItemAutomationPeer</c> implements <see cref="IToggleProvider"/> and nothing else, so a
/// menu item offers an automation client no way to be invoked and a submenu no way to be opened — measured against
/// the running app, every menu item reported exactly one UIA pattern (ScrollItem). Since the menu bar and the node
/// flyout are this app's primary command surface, that left the bulk of it reachable only by synthesizing clicks at
/// screen coordinates, and left assistive technology an item that announces no action.
/// <para>Producing <see cref="AccessibleMenuItem"/> containers closes that for generated (catalog-driven) submenus;
/// items authored in XAML are declared as <see cref="AccessibleMenuItem"/> directly. Same shape as
/// <see cref="AccessibleTreeView"/>, which does this for tree nodes.</para>
/// <para><see cref="StyleKeyOverride"/> points at the base type so the subclass keeps the default control theme
/// (Avalonia matches themes by exact type).</para>
/// </remarks>
public class AccessibleMenu : Menu
{
    protected override Type StyleKeyOverride => typeof(Menu);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new AccessibleMenuItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => MenuItemContainers.NeedsContainer(item, out recycleKey);
}

/// <summary>
/// The container rule the two accessible menu types share.
/// </summary>
internal static class MenuItemContainers
{
    /// <summary>
    /// True when <paramref name="item"/> is data that needs a container built for it. A menu element authored in
    /// markup already IS its container — and that includes <see cref="Separator"/>, which Avalonia's own rule
    /// passes through untouched. Generating one for a separator turns a divider line into a real, nameless,
    /// invokable menu item: an automation client counting the File menu finds eleven commands instead of seven,
    /// four of which do nothing, and a screen reader reads the blanks out.
    /// </summary>
    internal static bool NeedsContainer(object? item, out object? recycleKey)
    {
        recycleKey = null;
        return item is not MenuItem and not Separator;
    }
}

/// <summary>
/// A <see cref="MenuItem"/> that reports Invoke (a command item) or ExpandCollapse (a submenu host) to UI
/// Automation, and produces the same accessible container for its own children so a whole menu tree — including
/// the catalog-driven product and function-block forests — stays operable.
/// </summary>
public class AccessibleMenuItem : MenuItem
{
    protected override Type StyleKeyOverride => typeof(MenuItem);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new AccessibleMenuItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => MenuItemContainers.NeedsContainer(item, out recycleKey);

    protected override AutomationPeer OnCreateAutomationPeer() => new OperableMenuItemAutomationPeer(this);
}

/// <summary>
/// Extends Avalonia's menu-item peer with the two patterns a menu actually needs: <see cref="IInvokeProvider"/> so a
/// command item can be executed, and <see cref="IExpandCollapseProvider"/> so a submenu can be opened and closed.
/// </summary>
/// <remarks>
/// <para><c>MenuItemAutomationPeer</c> overrides <c>GetProviderCore</c>, and that method — not the CLR interface
/// list — is what the Windows UIA bridge asks. A peer that merely implemented these interfaces would therefore
/// still hand the bridge nothing, so the override below is load-bearing rather than decorative.</para>
/// <para>Invoke raises <see cref="MenuItem.ClickEvent"/> rather than executing <c>Command</c> directly: the click
/// pipeline is what runs the command and flips a ToggleType item's check state, so going around it would
/// half-perform the action. A gated-off item does nothing, exactly as a click on it does.</para>
/// <para>Dismissing the menu is a SEPARATE step, and it is not optional. Avalonia closes a menu from its
/// interaction handler on pointer-release, not from the click event, so raising the click alone leaves the menu
/// standing open over the app. Every automation client reads "the menu is still realized" as "the invoke did not
/// take" — the aui-openvisual driver reported <c>MutationFailed</c> for a locality insert that had actually
/// happened — so the peer closes it explicitly.</para>
/// </remarks>
public class OperableMenuItemAutomationPeer : MenuItemAutomationPeer, IInvokeProvider, IExpandCollapseProvider
{
    private readonly MenuItem _item;

    public OperableMenuItemAutomationPeer(MenuItem owner) : base(owner) => _item = owner;

    public void Invoke()
    {
        _item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        CloseOwningMenu();
    }

    /// <summary>Closes the surface this item belongs to. One walk serves both, because the menu bar's
    /// <see cref="Menu"/> and a flyout's <c>MenuFlyoutPresenter</c> are both <see cref="MenuBase"/>.</summary>
    private void CloseOwningMenu() =>
        _item.GetLogicalAncestors().OfType<MenuBase>().FirstOrDefault()?.Close();

    public ExpandCollapseState ExpandCollapseState =>
        !_item.HasSubMenu ? ExpandCollapseState.LeafNode
        : _item.IsSubMenuOpen ? ExpandCollapseState.Expanded
        : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => _item.HasSubMenu;

    public void Expand() => _item.Open();

    public void Collapse() => _item.Close();

    /// <summary>Surfaces the two added patterns to the platform bridge, which resolves providers through this
    /// method; anything else stays the base peer's answer (Toggle, for a checkable item).</summary>
    protected override object? GetProviderCore(Type providerType)
    {
        // A command item must not advertise ExpandCollapse and a submenu host must not advertise Invoke: a client
        // picks its interaction from what the element claims, so a leaf that answers Expand() — or a submenu that
        // answers Invoke() by "clicking" itself — sends the driver down a path that cannot work.
        if (providerType == typeof(IInvokeProvider))
            return _item.HasSubMenu ? null : this;
        if (providerType == typeof(IExpandCollapseProvider))
            return _item.HasSubMenu ? this : null;
        return base.GetProviderCore(providerType);
    }
}
