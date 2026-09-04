using System;
using System.Collections.Generic;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ihc.UiAutomation;

/// <summary>
/// The way into an application: its top-level windows, found by process rather than by searching the desktop.
/// </summary>
/// <remarks>
/// This is the toolkit's answer to the rule that no query may span the whole desktop tree. A UI-Automation
/// search from the root walks providers belonging to every running program, so one faulting provider anywhere
/// breaks the query — measured. Enumerating window handles asks the window manager instead, which knows
/// nothing about providers, and only then resolves the handles that belong to the process being driven.
/// </remarks>
public static class DesktopWindows
{
    /// <summary>
    /// The visible top-level windows of <paramref name="processId"/>, TOPMOST FIRST.
    /// </summary>
    /// <remarks>
    /// The order is the window manager's Z-order, which <c>EnumWindows</c> reports front to back. That matters
    /// wherever "the modal" means the dialog a person is looking at: UI-Automation sibling order is creation
    /// order and would name whichever window opened first, which is the one UNDERNEATH in a stack.
    /// </remarks>
    /// <param name="session">The automation session the returned elements belong to.</param>
    /// <param name="processId">The process whose windows to return.</param>
    public static IReadOnlyList<UiaElement> OfProcess(UiaSession session, int processId)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Handles first, elements second: resolving one to a UI-Automation element calls back into the provider,
        // and doing that from inside the enumeration would re-enter the window manager mid-walk.
        List<nint> handles = [];
        PInvoke.EnumWindows(
            (window, _) =>
            {
                if (IsTopLevelWindowOf(window, processId))
                    handles.Add(Win32Handles.ToNint(window));
                return true;
            },
            default);

        List<UiaElement> windows = new(handles.Count);
        foreach (nint handle in handles)
        {
            if (session.FromHandle(handle) is { } element)
                windows.Add(element);
        }

        return windows;
    }

    /// <remarks>
    /// Ownership is tested FIRST because it is the condition that rejects almost every window on the desktop,
    /// and this runs once per window per poll of a launch wait.
    /// </remarks>
    private static bool IsTopLevelWindowOf(HWND window, int processId)
    {
        if (Win32Handles.ProcessOf(window) != (uint)processId)
            return false;

        if (!PInvoke.IsWindowVisible(window))
            return false;

        // A window whose root is not itself is owned by another one — a popup or a tooltip. The caller wants
        // the windows an application presents, and a stack of those is what "open modals" means.
        return PInvoke.GetAncestor(window, GET_ANCESTOR_FLAGS.GA_ROOT) == window;
    }
}
