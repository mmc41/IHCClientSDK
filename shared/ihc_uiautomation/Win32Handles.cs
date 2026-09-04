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
}
