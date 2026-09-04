using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ihc.UiAutomation;

/// <summary>
/// Mouse input, synthesized at the system level so the application under test cannot tell it from a person.
/// </summary>
/// <remarks>
/// Every entry point enters a <see cref="DpiScope"/> first, because the coordinates a caller passes come from a
/// UI-Automation rectangle and are therefore physical pixels — see that type for what happens otherwise.
/// </remarks>
public static class Mouse
{
    /// <summary>
    /// Moves the cursor to a physical-pixel point and clicks the primary button <paramref name="count"/> times.
    /// </summary>
    /// <remarks>
    /// <b>Each click is its own <c>SendInput</c> call, with a short gap.</b> Batching a double click into one
    /// call gives all four transitions the same timestamp, and a framework deciding whether two clicks are a
    /// DOUBLE click reads exactly those timestamps — so the batched form is reliably seen as two single clicks.
    /// The gap is a small fraction of the system double-click time, which is the budget the pair has to fit in.
    /// </remarks>
    /// <param name="x">Horizontal position, in physical pixels.</param>
    /// <param name="y">Vertical position, in physical pixels.</param>
    /// <param name="count">How many times to click. One is a plain click, two a double click.</param>
    /// <returns>
    /// <see langword="false"/> if the system refused to POSITION the cursor or to inject any of the buttons —
    /// see <see cref="Position"/> and <c>SynthesizedInput</c>.
    /// </returns>
    public static bool Click(int x, int y, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        using DpiScope scope = DpiScope.Enter();

        if (!Position(x, y))
            return false;

        // Settled before the button goes down, exactly as Wheel does: on a busy machine the target window can
        // still be handling the move when the press arrives, and the press is then delivered against a stale
        // hover state — which looks like a click that did nothing.
        Thread.Sleep(BetweenClicks);

        bool injected = true;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                Thread.Sleep(BetweenClicks);

            injected &= SynthesizedInput.Send([
                MouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN),
                MouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP),
            ]) == 2;
        }

        return injected;
    }

    /// <summary>
    /// Turns the wheel over a point, positive UP and negative DOWN, one notch being one detent.
    /// </summary>
    /// <remarks>
    /// The wheel, never Page Down, wherever a caller is looking for something in a list: paging by key moves
    /// the list's SELECTION, and an application that acts on selection would then act once per page scrolled.
    /// The wheel changes what is visible and nothing else.
    /// </remarks>
    /// <param name="x">Horizontal position of the pointer, in physical pixels.</param>
    /// <param name="y">Vertical position of the pointer, in physical pixels.</param>
    /// <param name="notches">Detents to turn: positive scrolls up, negative down.</param>
    /// <returns>
    /// <see langword="false"/> if the system refused to POSITION the cursor or to inject the turn — see
    /// <see cref="Position"/> and <c>SynthesizedInput</c>.
    /// </returns>
    public static bool Wheel(int x, int y, int notches)
    {
        using DpiScope scope = DpiScope.Enter();

        if (!Position(x, y))
            return false;

        Thread.Sleep(BetweenClicks);

        INPUT wheel = MouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL);
        // A negative delta travels as an UNSIGNED value; wrap it explicitly rather than casting a negative int.
        wheel.Anonymous.mi.mouseData = unchecked((uint)(notches * WheelDelta));
        bool injected = SynthesizedInput.Send([wheel]) == 1;
        Thread.Sleep(BetweenClicks);
        return injected;
    }

    /// <summary>One wheel detent, as Windows defines it.</summary>
    private const int WheelDelta = 120;

    private static readonly TimeSpan BetweenClicks = TimeSpan.FromMilliseconds(30);

    /// <summary>Moves the cursor without clicking, for a hover.</summary>
    /// <param name="x">Horizontal position, in physical pixels.</param>
    /// <param name="y">Vertical position, in physical pixels.</param>
    /// <returns><see langword="false"/> if the system refused the move — see <see cref="Position"/>.</returns>
    public static bool MoveTo(int x, int y)
    {
        using DpiScope scope = DpiScope.Enter();
        return Position(x, y);
    }

    /// <summary>
    /// Puts the cursor where the gesture is aimed, and says whether it arrived.
    /// </summary>
    /// <remarks>
    /// <b>Its answer decides whether there is a gesture to report at all.</b> The button and wheel events that
    /// follow carry no coordinates — they land wherever the cursor IS — so a refused move does not merely
    /// mis-aim the gesture, it aims it at whatever the person left under the pointer, and
    /// <c>SendInput</c> then reports a perfectly successful injection into the wrong control. The system refuses
    /// while the desktop is locked or a screensaver is up, inside another process's blocked-input window, and
    /// under UIPI — the same elevation mismatch <see cref="SynthesizedInput.Send"/> reports.
    /// </remarks>
    private static bool Position(int x, int y) => PInvoke.SetCursorPos(x, y);

    private static INPUT MouseEvent(MOUSE_EVENT_FLAGS flags)
    {
        INPUT input = new() { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi.dwFlags = flags;
        return input;
    }
}

/// <summary>
/// Keyboard input, synthesized at the system level.
/// </summary>
/// <remarks>
/// <c>SendInput</c> only. The alternative a shell script would reach for, <c>SendKeys</c>, encodes a gesture as
/// a STRING and re-parses it at the far end, which is where a stray brace becomes a keystroke nobody meant. A
/// typed <see cref="UiaGesture"/> has no such gap between what a caller wrote and what is pressed.
/// </remarks>
public static class Keyboard
{
    /// <summary>
    /// Presses one gesture: modifiers down in order, the key down and up, then modifiers up in REVERSE order.
    /// </summary>
    /// <remarks>
    /// The order is what makes it a chord rather than a sequence — releasing Control before the key would send
    /// a bare key to an application that had already seen Control go up.
    /// </remarks>
    /// <returns><see langword="false"/> if the system refused to inject it — see <c>SynthesizedInput</c>.</returns>
    public static bool Send(UiaGesture gesture)
    {
        using DpiScope scope = DpiScope.Enter();

        List<VIRTUAL_KEY> modifiers = [];
        if (gesture.Modifiers.HasFlag(UiaModifiers.Control))
            modifiers.Add(VIRTUAL_KEY.VK_CONTROL);
        if (gesture.Modifiers.HasFlag(UiaModifiers.Shift))
            modifiers.Add(VIRTUAL_KEY.VK_SHIFT);
        if (gesture.Modifiers.HasFlag(UiaModifiers.Alt))
            modifiers.Add(VIRTUAL_KEY.VK_MENU);

        List<INPUT> strokes = [];
        foreach (VIRTUAL_KEY modifier in modifiers)
            strokes.Add(SynthesizedInput.Key(modifier, down: true));

        strokes.Add(SynthesizedInput.Key((VIRTUAL_KEY)(ushort)gesture.Key, down: true));
        strokes.Add(SynthesizedInput.Key((VIRTUAL_KEY)(ushort)gesture.Key, down: false));

        for (int i = modifiers.Count - 1; i >= 0; i--)
            strokes.Add(SynthesizedInput.Key(modifiers[i], down: false));

        return SynthesizedInput.Send(strokes) == strokes.Count;
    }
}

/// <summary>
/// Which window receives synthesized input.
/// </summary>
/// <remarks>
/// Synthesized input is not addressed to a window: it goes wherever the system's focus currently is. A caller
/// that types without first taking the foreground types into whatever the person left in front — which is how a
/// driver silently edits the wrong application.
/// </remarks>
public static class Foreground
{
    /// <summary>
    /// Brings a window to the front, and CONFIRMS it arrived.
    /// </summary>
    /// <remarks>
    /// <para><b><c>SetForegroundWindow</c>'s return value is worthless here.</b> It reports success while
    /// silently declining to change the foreground when the caller lacks foreground RIGHTS — which is the
    /// normal case for an automation host that was started in the background. So the answer is always a second,
    /// independent read of who is actually in front.</para>
    ///
    /// <para><b>The ALT tap is the documented way to acquire those rights</b>, and it is why this is not a
    /// one-line method. The keystroke is delivered to whatever window currently HAS the foreground, not to
    /// ours; the side effect that matters is that Windows then grants this thread the right to activate. It is
    /// tried only after the plain request has been read back and found wanting.</para>
    ///
    /// <para>A caller MUST refuse to send input when this returns false — otherwise the keystrokes land in
    /// whatever application the person actually has in front.</para>
    /// </remarks>
    /// <returns><see langword="true"/> only if the window is now the foreground window.</returns>
    public static bool Acquire(nint windowHandle)
    {
        HWND window = Win32Handles.ToHwnd(windowHandle);
        if (window.IsNull)
        {
            return false;
        }

        if (Request(window, TimeSpan.FromMilliseconds(80)))
        {
            return true;
        }

        // ONLY when the foreground belongs to ANOTHER process. The tap exists to acquire foreground rights
        // from whoever holds them; when the window in front is already one of our target's own — a dialog it
        // opened, say — those rights are not the obstacle, and the keystroke is then a stray ALT delivered
        // into the application under test, where it opens a menu or moves a focus nobody asked it to.
        if (ProcessOf(PInvoke.GetForegroundWindow()) != ProcessOf(window))
        {
            _ = SynthesizedInput.Send([
                SynthesizedInput.Key(VIRTUAL_KEY.VK_MENU, down: true),
                SynthesizedInput.Key(VIRTUAL_KEY.VK_MENU, down: false),
            ]);
        }

        return Request(window, TimeSpan.FromMilliseconds(150));
    }

    private static uint ProcessOf(HWND window)
    {
        uint owner = 0;
        unsafe
        {
            _ = PInvoke.GetWindowThreadProcessId(window, &owner);
        }

        return owner;
    }

    private static bool Request(HWND window, TimeSpan settle)
    {
        // Restored first: a minimized window cannot become the foreground one, and its rectangle would be
        // off-screen even if it did.
        _ = PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_RESTORE);
        _ = PInvoke.SetForegroundWindow(window);
        Thread.Sleep(settle);
        return PInvoke.GetForegroundWindow() == window;
    }

    /// <summary>The window currently in front, or zero if there is none.</summary>
    public static nint Current() => Win32Handles.ToNint(PInvoke.GetForegroundWindow());
}

internal static class SynthesizedInput
{
    /// <summary>One key transition, as <c>SendInput</c> wants it.</summary>
    internal static INPUT Key(VIRTUAL_KEY key, bool down)
    {
        INPUT input = new() { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki.wVk = key;
        input.Anonymous.ki.dwFlags = down ? 0 : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        return input;
    }

    /// <summary>
    /// Hands a whole gesture to the system in ONE call. Splitting it across calls lets another process's input
    /// interleave, which turns a chord into a sequence.
    /// </summary>
    /// <returns>
    /// How many events were actually injected. <b>Fewer than asked for means the input never happened</b> —
    /// UIPI blocks injection into a more privileged window, and a wrong <c>cbSize</c> is rejected outright.
    /// Discarding this is how a driver reports a gesture that the system silently refused.
    /// </returns>
    internal static uint Send(List<INPUT> inputs)
    {
        if (inputs.Count == 0)
            return 0;

        return PInvoke.SendInput(CollectionsMarshal.AsSpan(inputs), Marshal.SizeOf<INPUT>());
    }
}
