using Windows.Win32;
using Windows.Win32.Foundation;

namespace Ihc.UiAutomation;

/// <summary>
/// Conversions between the generated <see cref="HWND"/> and the <see langword="nint"/> this toolkit's public
/// surface speaks.
/// </summary>
/// <remarks>
/// CsWin32 models a window handle as a <c>void*</c> field, so every read of one is pointer code. Confining that
/// to two one-line methods keeps <c>unsafe</c> out of the wrappers, and keeps the generated type out of any
/// public signature — it is internal to this assembly and could not appear in one anyway.
/// </remarks>
internal static class Win32Handles
{
    internal static unsafe nint ToNint(HWND window) => (nint)window.Value;

    internal static HWND ToHwnd(nint value) => new(value);

    /// <summary>
    /// The process a window belongs to, or zero if the window is gone.
    /// </summary>
    /// <remarks>
    /// Zero is the answer for BOTH, deliberately. <c>GetWindowThreadProcessId</c> writes the owning process only
    /// when it can name the thread, so a zero thread id leaves the out-parameter untouched — reporting it would
    /// be handing back whatever the caller's variable happened to hold. Zero can therefore never compare equal
    /// to a live process, which is what a caller asking "are these the same process" needs.
    /// </remarks>
    internal static unsafe uint ProcessOf(HWND window)
    {
        uint owner = 0;
        return PInvoke.GetWindowThreadProcessId(window, &owner) == 0 ? 0 : owner;
    }
}
