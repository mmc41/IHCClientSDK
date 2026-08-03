using System;
using Avalonia.Input;

namespace ihc_openvisual.Views;

/// <summary>
/// Turns a registry row's gesture STRING into the <see cref="KeyGesture"/> the running platform actually uses.
/// <para>The rows hold gestures as plain strings on purpose (D08 — parsing belongs to the view, so the rows stay
/// headless-testable), and they spell the primary command modifier <c>Ctrl</c>. On macOS that primary modifier is
/// <b>Cmd</b>: Ctrl is a different modifier there with its own reserved meanings, so a literal <c>Ctrl+C</c> is not
/// "the copy shortcut spelled differently", it is the wrong shortcut (portability review AP-13).</para>
/// <para>Only the PRIMARY modifier is remapped. Shift is not a command modifier and rides along untouched, and a
/// gesture with no modifier at all — the function keys, Delete, Escape — is identical on every platform.</para>
/// <para>The markup does the same mapping declaratively, via <c>{OnPlatform 'Ctrl+X', macOS='Cmd+X'}</c> on the
/// KeyBindings that fire commands and the InputGesture captions that advertise them. This type exists for the one
/// route that cannot be expressed in markup: the window's gesture-matching handler, which compares a real key
/// press against the registry rows to explain a refusal in the status bar. Leave that on
/// <see cref="KeyGesture.Parse(string)"/> and the explanation silently never matches on macOS.</para>
/// </summary>
internal static class PlatformGesture
{
    /// <summary>Parses <paramref name="rowGesture"/> for the host platform.</summary>
    internal static KeyGesture Parse(string rowGesture) => Parse(rowGesture, OperatingSystem.IsMacOS());

    /// <summary>Parses <paramref name="rowGesture"/> for the named platform. The explicit
    /// <paramref name="isMacOS"/> is what lets the macOS behaviour be verified from a Windows or Linux test run.</summary>
    internal static KeyGesture Parse(string rowGesture, bool isMacOS)
    {
        KeyGesture gesture = KeyGesture.Parse(rowGesture);
        if (!isMacOS || !gesture.KeyModifiers.HasFlag(KeyModifiers.Control))
            return gesture;

        return new KeyGesture(gesture.Key, (gesture.KeyModifiers & ~KeyModifiers.Control) | KeyModifiers.Meta);
    }
}
