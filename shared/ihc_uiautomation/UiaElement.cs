using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

using Windows.Win32.UI.Accessibility;

namespace Ihc.UiAutomation;

/// <summary>
/// One element of a live UI-Automation tree: what it publishes about itself, how to walk to its neighbours,
/// and the operations its control patterns allow.
/// </summary>
/// <remarks>
/// <para>A thin wrapper, not a snapshot. Every property reads the live element on each call, so a value can
/// change between two reads — which is the truth about a running application, and hiding it behind a cached
/// copy would produce assertions that pass against a UI that has already moved on.</para>
///
/// <para><b>Operations, not patterns.</b> UI Automation exposes capability through control-pattern interfaces,
/// but those interfaces are COM types generated INTERNAL to this assembly, so no public signature can name
/// one. Each is surfaced here as the operation a driver actually wants, and each answers "this element does not
/// support that" the same way — <see langword="false"/>, <see langword="null"/>, or an empty list — rather than
/// by throwing. A driver's job is to report a refusal, not to catch one.</para>
/// </remarks>
public sealed class UiaElement
{
    private readonly UiaSession _session;
    private readonly IUIAutomationElement _element;

    internal UiaElement(UiaSession session, IUIAutomationElement element)
    {
        _session = session;
        _element = element;
    }

    internal IUIAutomationElement Native => _element;

    /// <summary>The element's accessible name — what a screen reader announces. Localized, so never a key.</summary>
    public string Name => Read(() => _element.CurrentName.ToString());

    /// <summary>The stable, locale-independent id a driver targets. Empty when the element publishes none.</summary>
    public string AutomationId => Read(() => _element.CurrentAutomationId.ToString());

    /// <summary>What kind of control it is.</summary>
    public UiaControlType ControlType =>
        Read(() => (UiaControlType)(int)_element.CurrentControlType, UiaControlType.Unknown);

    /// <summary>
    /// The element's rectangle in PHYSICAL pixels — UI Automation reports no other kind. Anything comparing it
    /// with a cursor position has to be inside a <see cref="DpiScope"/>, or the two are in different spaces.
    /// </summary>
    public Rectangle BoundingRectangle => Read(() =>
    {
        Windows.Win32.Foundation.RECT r = _element.CurrentBoundingRectangle;
        return new Rectangle(r.left, r.top, r.right - r.left, r.bottom - r.top);
    }, Rectangle.Empty);

    /// <summary>Scrolled or clipped out of view. An offscreen element cannot be clicked where it claims to be.</summary>
    public bool IsOffscreen => Read(() => (bool)_element.CurrentIsOffscreen, false);

    /// <summary>Whether this element holds the keyboard focus.</summary>
    public bool HasKeyboardFocus => Read(() => (bool)_element.CurrentHasKeyboardFocus, false);

    /// <summary>The window handle behind it, or zero — most elements are not windows.</summary>
    public nint NativeWindowHandle =>
        Read(() => Win32Handles.ToNint(_element.CurrentNativeWindowHandle), IntPtr.Zero);

    /// <summary>The process the element belongs to. The scoping every query in this toolkit is built on.</summary>
    public int ProcessId => Read(() => _element.CurrentProcessId, 0);

    /// <summary>
    /// The element's ItemStatus — a second, machine-readable identity published beside the name, where an
    /// application has one to give. Empty when it publishes none.
    /// </summary>
    public string ItemStatus => Read(() =>
        _element.GetCurrentPropertyValue(UIA_PROPERTY_ID.UIA_ItemStatusPropertyId) as string);

    /// <summary>The Value pattern's text, or null when the element exposes no such pattern.</summary>
    public string? Value =>
        Pattern<IUIAutomationValuePattern>(UIA_PATTERN_ID.UIA_ValuePatternId) is { } value
            ? Read(() => value.CurrentValue.ToString())
            : null;

    // ── Walking ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The element's children in the CONTROL view, in the order the tree presents them.
    /// </summary>
    /// <remarks>
    /// Through the tree walker rather than a child-scoped search, and the difference is not academic: under an
    /// expanded tree row a child-scoped <c>FindAll</c> returns the wrong set — it reaches through the row's
    /// container into descendants that are not its children. The walker returns what the control view actually
    /// says, which is what a person sees.
    /// </remarks>
    public IReadOnlyList<UiaElement> Children()
    {
        List<UiaElement> children = [];
        try
        {
            IUIAutomationTreeWalker walker = _session.ControlWalker;
            IUIAutomationElement? child = walker.GetFirstChildElement(_element);
            while (child is not null)
            {
                children.Add(new UiaElement(_session, child));
                child = walker.GetNextSiblingElement(child);
            }
        }
        catch (COMException)
        {
            // A subtree that vanished mid-walk yields what was reached before it went. Reporting a partial
            // tree beats failing a whole scenario because a tooltip closed.
        }

        return children;
    }

    /// <summary>The first descendant publishing <paramref name="automationId"/>, or null.</summary>
    public UiaElement? FindFirstById(string automationId, UiaScope scope = UiaScope.Descendants)
    {
        ArgumentNullException.ThrowIfNull(automationId);
        return FindFirst(UIA_PROPERTY_ID.UIA_AutomationIdPropertyId, automationId, scope);
    }

    /// <summary>Every descendant publishing <paramref name="automationId"/>.</summary>
    public IReadOnlyList<UiaElement> FindAllById(string automationId, UiaScope scope = UiaScope.Descendants)
    {
        ArgumentNullException.ThrowIfNull(automationId);
        return FindAll(UIA_PROPERTY_ID.UIA_AutomationIdPropertyId, automationId, scope);
    }

    /// <summary>The first descendant whose accessible name is <paramref name="name"/>, or null.</summary>
    public UiaElement? FindFirstByName(string name, UiaScope scope = UiaScope.Descendants)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FindFirst(UIA_PROPERTY_ID.UIA_NamePropertyId, name, scope);
    }

    /// <summary>Every descendant whose accessible name is <paramref name="name"/>.</summary>
    public IReadOnlyList<UiaElement> FindAllByName(string name, UiaScope scope = UiaScope.Descendants)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FindAll(UIA_PROPERTY_ID.UIA_NamePropertyId, name, scope);
    }

    /// <summary>Every descendant of the given control type.</summary>
    public IReadOnlyList<UiaElement> FindAllByControlType(UiaControlType controlType, UiaScope scope = UiaScope.Descendants) =>
        FindAll(UIA_PROPERTY_ID.UIA_ControlTypePropertyId, (int)controlType, scope);

    /// <summary>
    /// The first descendant of the given control type that reports itself SELECTED, or null.
    /// </summary>
    /// <remarks>
    /// Asks the provider, in one query that stops at the hit, rather than enumerating a subtree and reading
    /// <c>IsSelected</c> off each element in this process. For a large tree that difference is thousands of
    /// cross-process round trips per call — and this is the kind of question a driver asks on EVERY command.
    ///
    /// <para>It exists because a container does not have to expose the Selection pattern: several common tree
    /// implementations expose <c>SelectionItem</c> on their rows and nothing on the container, so
    /// <see cref="Selection"/> answers empty for a tree that plainly has a selected row.</para>
    /// </remarks>
    public UiaElement? FindFirstSelected(UiaControlType controlType, UiaScope scope = UiaScope.Descendants)
    {
        try
        {
            IUIAutomation automation = _session.Automation;
            IUIAutomationCondition condition = automation.CreateAndCondition(
                automation.CreatePropertyCondition(UIA_PROPERTY_ID.UIA_ControlTypePropertyId, (int)controlType),
                automation.CreatePropertyCondition(UIA_PROPERTY_ID.UIA_SelectionItemIsSelectedPropertyId, true));

            IUIAutomationElement? found = _element.FindFirst(Scope(scope), condition);
            return found is null ? null : new UiaElement(_session, found);
        }
        catch (COMException)
        {
            return null;
        }
    }

    // ── Operations ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Invokes the element. False when it exposes no Invoke pattern.</summary>
    public bool Invoke() => Operate<IUIAutomationInvokePattern>(
        UIA_PATTERN_ID.UIA_InvokePatternId, pattern => pattern.Invoke());

    /// <summary>Whether the element is open, and whether it can be.</summary>
    public UiaExpandState ExpandState =>
        Pattern<IUIAutomationExpandCollapsePattern>(UIA_PATTERN_ID.UIA_ExpandCollapsePatternId) is { } pattern
            ? Read(() => (UiaExpandState)((int)pattern.CurrentExpandCollapseState + 1), UiaExpandState.NotExpandable)
            : UiaExpandState.NotExpandable;

    /// <summary>Opens the element. False when it exposes no ExpandCollapse pattern.</summary>
    public bool Expand() => Operate<IUIAutomationExpandCollapsePattern>(
        UIA_PATTERN_ID.UIA_ExpandCollapsePatternId, pattern => pattern.Expand());

    /// <summary>Closes the element. False when it exposes no ExpandCollapse pattern.</summary>
    public bool Collapse() => Operate<IUIAutomationExpandCollapsePattern>(
        UIA_PATTERN_ID.UIA_ExpandCollapsePatternId, pattern => pattern.Collapse());

    /// <summary>Selects the element within its container. False when it exposes no SelectionItem pattern.</summary>
    public bool Select() => Operate<IUIAutomationSelectionItemPattern>(
        UIA_PATTERN_ID.UIA_SelectionItemPatternId, pattern => pattern.Select());

    /// <summary>Whether this element is the selected one in its container.</summary>
    public bool IsSelected =>
        Pattern<IUIAutomationSelectionItemPattern>(UIA_PATTERN_ID.UIA_SelectionItemPatternId) is { } pattern
        && Read(() => (bool)pattern.CurrentIsSelected, false);

    /// <summary>
    /// What this CONTAINER currently has selected. Empty when it exposes no Selection pattern, which is also
    /// the answer for an element that is merely selectable rather than a container of selections.
    /// </summary>
    public IReadOnlyList<UiaElement> Selection()
    {
        if (Pattern<IUIAutomationSelectionPattern>(UIA_PATTERN_ID.UIA_SelectionPatternId) is not { } pattern)
            return [];

        try
        {
            IUIAutomationElementArray selected = pattern.GetCurrentSelection();
            List<UiaElement> elements = [];
            for (int i = 0; i < selected.Length; i++)
                elements.Add(new UiaElement(_session, selected.GetElement(i)));
            return elements;
        }
        catch (COMException)
        {
            return [];
        }
    }

    /// <summary>
    /// Scrolls this element into view within its scrolling ancestor. False when it exposes no ScrollItem
    /// pattern — which for a virtualizing list is also true of every row not yet realized.
    /// </summary>
    public bool ScrollIntoView() => Operate<IUIAutomationScrollItemPattern>(
        UIA_PATTERN_ID.UIA_ScrollItemPatternId, pattern => pattern.ScrollIntoView());

    /// <summary>
    /// Whether this element can scroll vertically — which is how a caller finds the SCROLLABLE inside a
    /// control, rather than assuming the control itself is one.
    /// </summary>
    /// <remarks>
    /// It usually is not. A themed list puts its Scroll pattern on the ScrollViewer inside its control
    /// template, and the list's own peer exposes none — so paging "the list" moves nothing at all, silently,
    /// and a search reports rows that plainly exist as missing.
    /// </remarks>
    public bool IsVerticallyScrollable =>
        Pattern<IUIAutomationScrollPattern>(UIA_PATTERN_ID.UIA_ScrollPatternId) is { } pattern
        && Read(() => (bool)pattern.CurrentVerticallyScrollable, false);

    /// <summary>
    /// Whether the element exposes the Scroll pattern AT ALL, regardless of whether it can currently scroll.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="IsVerticallyScrollable"/> because the two answer different questions when a
    /// search comes up empty: "nothing here scrolls" and "the scroller is here but says it has nowhere to go"
    /// have different causes and different fixes.
    /// </remarks>
    public bool ExposesScrollPattern =>
        Pattern<IUIAutomationScrollPattern>(UIA_PATTERN_ID.UIA_ScrollPatternId) is not null;

    /// <summary>
    /// The element's range, when it is something with one — a scroll bar, a slider, a progress bar.
    /// </summary>
    /// <remarks>
    /// The way to move a themed list whose own peer offers no Scroll pattern: its SCROLL BAR is a real element
    /// with a real range, so setting that range scrolls the list. It changes what is on screen and nothing
    /// else — no selection moves, no command runs — which is what makes it a safe way to REACH something
    /// rather than a way of faking the gesture under test.
    /// </remarks>
    public UiaRange? Range =>
        Pattern<IUIAutomationRangeValuePattern>(UIA_PATTERN_ID.UIA_RangeValuePatternId) is { } pattern
            ? Read<UiaRange?>(
                () => new UiaRange(
                    pattern.CurrentValue,
                    pattern.CurrentMinimum,
                    pattern.CurrentMaximum,
                    pattern.CurrentLargeChange),
                null)
            : null;

    /// <summary>Moves a ranged element to a value. False when it exposes no RangeValue pattern.</summary>
    public bool SetRangeValue(double value) => Operate<IUIAutomationRangeValuePattern>(
        UIA_PATTERN_ID.UIA_RangeValuePatternId, pattern => pattern.SetValue(value));

    /// <summary>
    /// Scrolls back to the top. False when the element exposes no Scroll pattern.
    /// </summary>
    /// <remarks>
    /// A search must start from a known position: a scroll offset survives between one caller's operations, so
    /// a search beginning wherever the last one stopped can only ever reach what is BELOW that point.
    /// </remarks>
    public bool ScrollToTop() => Operate<IUIAutomationScrollPattern>(
        UIA_PATTERN_ID.UIA_ScrollPatternId,
        // -1 is UIA's "leave this axis alone"; 0 is the top.
        pattern => pattern.SetScrollPercent(-1, 0));

    /// <summary>Every descendant, unfiltered, in the order the provider reports them.</summary>
    /// <remarks>
    /// The one query that can see a control's TEMPLATE parts — a themed ScrollViewer, a presenter — which the
    /// control-view walker behind <see cref="Children"/> deliberately hides.
    /// </remarks>
    public IReadOnlyList<UiaElement> FindAllDescendants()
    {
        try
        {
            return Materialize(TreeScope.TreeScope_Descendants, _session.Automation.CreateTrueCondition());
        }
        catch (COMException)
        {
            return [];
        }
    }

    /// <summary>
    /// Scrolls this CONTAINER one page vertically. False when it exposes no Scroll pattern or cannot scroll
    /// further; the second is what ends a caller's paging loop.
    /// </summary>
    public bool ScrollPage(bool down)
    {
        if (Pattern<IUIAutomationScrollPattern>(UIA_PATTERN_ID.UIA_ScrollPatternId) is not { } pattern)
            return false;

        try
        {
            if (!pattern.CurrentVerticallyScrollable)
                return false;

            pattern.Scroll(
                ScrollAmount.ScrollAmount_NoAmount,
                down ? ScrollAmount.ScrollAmount_LargeIncrement : ScrollAmount.ScrollAmount_LargeDecrement);
            return true;
        }
        catch (COMException)
        {
            // Scroll throws rather than returning a code when it is already at the end of its range.
            return false;
        }
    }

    /// <summary>Asks the element to take keyboard focus. False when it refuses or cannot.</summary>
    public bool Focus()
    {
        try
        {
            _element.SetFocus();
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────────────────────────────────

    private static TreeScope Scope(UiaScope scope) => scope switch
    {
        UiaScope.Children => TreeScope.TreeScope_Children,
        UiaScope.Subtree => TreeScope.TreeScope_Subtree,
        _ => TreeScope.TreeScope_Descendants,
    };

    private UiaElement? FindFirst(UIA_PROPERTY_ID property, object value, UiaScope scope)
    {
        try
        {
            IUIAutomationCondition condition = _session.Automation.CreatePropertyCondition(property, value);
            IUIAutomationElement? found = _element.FindFirst(Scope(scope), condition);
            return found is null ? null : new UiaElement(_session, found);
        }
        catch (COMException)
        {
            return null;
        }
    }

    private List<UiaElement> FindAll(UIA_PROPERTY_ID property, object value, UiaScope scope)
    {
        try
        {
            return Materialize(Scope(scope), _session.Automation.CreatePropertyCondition(property, value));
        }
        catch (COMException)
        {
            return [];
        }
    }

    /// <summary>Turns a UI-Automation element array into wrappers. The one place that walks one.</summary>
    private List<UiaElement> Materialize(TreeScope scope, IUIAutomationCondition condition)
    {
        IUIAutomationElementArray found = _element.FindAll(scope, condition);
        List<UiaElement> elements = [];
        for (int i = 0; i < found.Length; i++)
            elements.Add(new UiaElement(_session, found.GetElement(i)));
        return elements;
    }

    private T? Pattern<T>(UIA_PATTERN_ID patternId) where T : class
    {
        try
        {
            return _element.GetCurrentPattern(patternId) as T;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private bool Operate<T>(UIA_PATTERN_ID patternId, Action<T> operation) where T : class
    {
        if (Pattern<T>(patternId) is not { } pattern)
            return false;

        try
        {
            operation(pattern);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a live property, answering with <paramref name="whenGone"/> if the element has been destroyed.
    /// An element that disappears mid-read is ordinary in a running application; the alternative is every
    /// caller wrapping every property access.
    /// </summary>
    private static T Read<T>(Func<T> read, T whenGone)
    {
        try
        {
            return read();
        }
        catch (COMException)
        {
            return whenGone;
        }
    }

    private static string Read(Func<string?> read) => Read<string?>(read, null) ?? string.Empty;
}
