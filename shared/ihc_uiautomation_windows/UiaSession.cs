using System;
using System.Runtime.InteropServices;

using Windows.Win32.UI.Accessibility;

namespace Ihc.UiAutomation;

/// <summary>
/// A live connection to the Windows UI-Automation client. Everything else in this toolkit hangs off one.
/// </summary>
/// <remarks>
/// <para><b>Create it on an MTA thread.</b> The UI-Automation client is a COM object, and calling it from an STA
/// thread that never pumps messages deadlocks rather than failing. NUnit's worker threads are MTA by default,
/// which is why the end-to-end suite needs no apartment plumbing; a caller that has set STA on its own thread
/// has to provide a message pump or move the calls.</para>
///
/// <para><b>There is deliberately no way to search from the desktop root.</b> A tree-wide query walks providers
/// belonging to every running application, so one faulting provider in an unrelated program takes down the
/// whole query — measured. Reach an application through <see cref="Ihc.UiAutomation.DesktopWindows"/>, which
/// starts from a process id, and search downward from there.</para>
/// </remarks>
public sealed class UiaSession : IDisposable
{
    private IUIAutomation? _automation;
    private IUIAutomationTreeWalker? _controlWalker;

    /// <summary>Connects to the UI-Automation client.</summary>
    public UiaSession() => _automation = (IUIAutomation)new CUIAutomation8();

    internal IUIAutomation Automation =>
        _automation ?? throw new ObjectDisposedException(nameof(UiaSession));

    /// <summary>
    /// The CONTROL view of the tree. Not the raw view: the raw view carries the presentational elements a
    /// theme happens to build, which differ between themes and are not what a person sees.
    /// </summary>
    /// <remarks>
    /// Held rather than re-read: it is a COM property on the client, so fetching it per walk is a cross-process
    /// call before the walk has looked at anything, and the walker itself does not change for the session.
    /// </remarks>
    internal IUIAutomationTreeWalker ControlWalker => _controlWalker ??= Automation.ControlViewWalker;

    /// <summary>The element behind a window handle, or null if the handle names no live window.</summary>
    public UiaElement? FromHandle(nint windowHandle)
    {
        try
        {
            IUIAutomationElement element = Automation.ElementFromHandle(Win32Handles.ToHwnd(windowHandle));
            return element is null ? null : new UiaElement(this, element);
        }
        catch (COMException)
        {
            // The window closed between being enumerated and being resolved. A driver races a live application
            // by definition, so this is an ordinary outcome and not a fault.
            return null;
        }
    }

    /// <summary>
    /// Whatever currently has keyboard focus, anywhere on the desktop, or null if nothing does.
    /// Callers that care which application it belongs to must check <see cref="UiaElement.ProcessId"/>.
    /// </summary>
    public UiaElement? FocusedElement()
    {
        try
        {
            IUIAutomationElement element = Automation.GetFocusedElement();
            return element is null ? null : new UiaElement(this, element);
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>Releases the client. Elements obtained from a disposed session must not be used.</summary>
    public void Dispose()
    {
        IUIAutomation? automation = _automation;
        _automation = null;
        _controlWalker = null;
        if (automation is not null && Marshal.IsComObject(automation))
            Marshal.FinalReleaseComObject(automation);
    }
}
