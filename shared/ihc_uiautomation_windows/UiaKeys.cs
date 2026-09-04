using System;

namespace Ihc.UiAutomation;

/// <summary>The modifier keys held while another key is pressed.</summary>
[Flags]
public enum UiaModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>Control.</summary>
    Control = 1,

    /// <summary>Shift.</summary>
    Shift = 2,

    /// <summary>Alt.</summary>
    Alt = 4,
}

/// <summary>
/// The keys this toolkit can synthesize, valued as Windows virtual-key codes.
/// </summary>
/// <remarks>
/// The values ARE the contract — they are what reaches <c>SendInput</c>. A named vocabulary rather than raw
/// codes so a caller's intent survives review, and the toolkit's own enum rather than the generated
/// <c>VIRTUAL_KEY</c> because that type is internal to this assembly.
///
/// <para>Deliberately no GRAMMAR here: nothing in this toolkit parses <c>{ENTER}</c> or <c>^z</c>. How a driver
/// spells a gesture, and which gestures it refuses, are that driver's policy.</para>
/// </remarks>
public enum UiaKey
{
    Backspace = 0x08,
    Tab = 0x09,
    Enter = 0x0D,
    Escape = 0x1B,
    Space = 0x20,
    PageUp = 0x21,
    PageDown = 0x22,
    End = 0x23,
    Home = 0x24,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    Insert = 0x2D,
    Delete = 0x2E,

    D0 = 0x30,
    D1 = 0x31,
    D2 = 0x32,
    D3 = 0x33,
    D4 = 0x34,
    D5 = 0x35,
    D6 = 0x36,
    D7 = 0x37,
    D8 = 0x38,
    D9 = 0x39,

    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,

    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
}

/// <summary>One keystroke: a key, and whatever is held down with it.</summary>
/// <param name="Key">The key pressed.</param>
/// <param name="Modifiers">The modifiers held while it is pressed.</param>
public readonly record struct UiaGesture(UiaKey Key, UiaModifiers Modifiers = UiaModifiers.None);
