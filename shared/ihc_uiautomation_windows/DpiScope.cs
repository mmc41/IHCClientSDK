using System;

using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace Ihc.UiAutomation;

/// <summary>
/// Runs a block of code with the calling thread declared PER-MONITOR DPI aware, restoring the thread's previous
/// awareness afterwards.
/// </summary>
/// <remarks>
/// <para><b>Why any of this is necessary.</b> UI Automation reports rectangles in PHYSICAL pixels, always. But
/// cursor and synthesized-input coordinates are interpreted in the CALLING THREAD'S DPI context: a DPI-unaware
/// thread has its coordinates scaled on the way through, so on a scaled display the click lands somewhere the
/// caller never asked for. A test host is DPI-unaware by default, which is exactly the case that breaks.</para>
///
/// <para>Entering per-monitor awareness for the duration of the call puts both spaces in physical pixels, so an
/// element's rectangle and the point clicked mean the same thing. The scope is per THREAD, so it must wrap the
/// call rather than being set once at startup — and it is restored on the way out so nothing else on the thread
/// inherits a context it did not ask for.</para>
/// </remarks>
public sealed class DpiScope : IDisposable
{
    private readonly DPI_AWARENESS_CONTEXT _previous;
    private bool _entered;

    /// <summary>Enters per-monitor DPI awareness on the calling thread.</summary>
    public static DpiScope Enter() => new();

    private DpiScope()
    {
        // Per-monitor v2 arrived in Windows 10 1607, and this assembly declares 6.1 — so on anything older the
        // scope is a no-op rather than a refusal. There is no per-monitor context to enter there, and a driver
        // that cannot enter one is still correct on an unscaled display, which is what those machines have.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
            return;

        _previous = PInvoke.SetThreadDpiAwarenessContext(
            DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        _entered = true;
    }

    /// <summary>Restores the thread's previous DPI awareness.</summary>
    public void Dispose()
    {
        // The version check is REDUNDANT with `_entered` and must stay anyway: the generated
        // SetThreadDpiAwarenessContext is annotated for Windows 10 1607 while this assembly declares 6.1, so
        // CA1416 demands the guard at the call site rather than at the one that set the flag.
        if (!_entered || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
            return;

        _entered = false;
        _ = PInvoke.SetThreadDpiAwarenessContext(_previous);
    }
}
