using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Input;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Accelerator portability (Avalonia portability review AP-13). macOS's primary command modifier is Cmd, not Ctrl,
/// so a hard-coded <c>Ctrl+…</c> is simply the wrong shortcut there — the platform reserves Ctrl for other
/// meanings entirely.
/// <para>The registry rows deliberately hold gestures as plain STRINGS (D08: parsing to an Avalonia
/// <see cref="KeyGesture"/> is view-side), so the whole mapping lives in the view layer and no row changes.
/// Two halves have to agree: the MARKUP (what actually fires a command, and what the menus advertise) and the
/// window's gesture-matching route (which explains a refusal in the status bar). Miss the second and the
/// explanation route dies silently on macOS while everything still looks wired.</para>
/// </summary>
public class AcceleratorPortabilityTests
{
    /// <summary>The primary command modifier is Meta (Cmd) on macOS and Control elsewhere. Both branches are
    /// forced explicitly rather than read off the host, so the macOS behaviour is verified on Windows CI.</summary>
    [Test]
    public void PlatformGesture_MapsThePrimaryModifierPerPlatform()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlatformGesture.Parse("Ctrl+X", isMacOS: true).KeyModifiers, Is.EqualTo(KeyModifiers.Meta),
                "on macOS the primary modifier is Cmd");
            Assert.That(PlatformGesture.Parse("Ctrl+X", isMacOS: false).KeyModifiers, Is.EqualTo(KeyModifiers.Control),
                "elsewhere it stays Ctrl");
            Assert.That(PlatformGesture.Parse("Ctrl+X", isMacOS: true).Key, Is.EqualTo(Key.X),
                "and the key itself is untouched");
        });
    }

    /// <summary>Only the PRIMARY modifier moves. Shift is not a command modifier and must survive, and a gesture
    /// with no modifier at all (the function keys, Delete, Escape) is identical on every platform — remapping
    /// those would invent shortcuts macOS does not have.</summary>
    [Test]
    public void PlatformGesture_LeavesSecondaryModifiersAndBareKeysAlone()
    {
        KeyGesture macCombo = PlatformGesture.Parse("Ctrl+Shift+Up", isMacOS: true);

        Assert.Multiple(() =>
        {
            Assert.That(macCombo.KeyModifiers, Is.EqualTo(KeyModifiers.Meta | KeyModifiers.Shift),
                "Shift rides along; only Ctrl becomes Cmd");
            Assert.That(macCombo.Key, Is.EqualTo(Key.Up));
            foreach (string bare in new[] { "F3", "Delete", "Escape" })
            {
                Assert.That(PlatformGesture.Parse(bare, isMacOS: true).KeyModifiers, Is.EqualTo(KeyModifiers.None),
                    $"'{bare}' carries no command modifier, so macOS sees exactly the same gesture");
                Assert.That(PlatformGesture.Parse(bare, isMacOS: true).Key,
                    Is.EqualTo(KeyGesture.Parse(bare).Key), $"'{bare}' still resolves to its own key");
            }
        });
    }

    /// <summary>The markup half, and the regression net: a new accelerator added Windows-only fails here rather
    /// than shipping as a dead shortcut on macOS. Covers what FIRES a command (<c>KeyBinding Gesture</c>) and what
    /// the menus ADVERTISE (<c>InputGesture</c>) — a correct binding under a Ctrl caption is still a bug.</summary>
    [Test]
    public void EveryCommandModifierAcceleratorInTheShellDeclaresAMacBranch()
    {
        string xaml = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));

        var offenders = Regex.Matches(xaml, @"(?<attribute>Gesture|InputGesture)=""(?<value>[^""]*)""")
            .Where(m => m.Groups["value"].Value.Contains("Ctrl", System.StringComparison.OrdinalIgnoreCase))
            .Where(m => !m.Groups["value"].Value.Contains("OnPlatform", System.StringComparison.Ordinal)
                        || !m.Groups["value"].Value.Contains("macOS", System.StringComparison.Ordinal))
            .Select(m => $"{m.Groups["attribute"].Value}=\"{m.Groups["value"].Value}\"")
            .ToList();

        Assert.That(offenders, Is.Empty,
            "every Ctrl accelerator needs an {OnPlatform …, macOS='Cmd+…'} branch; found:\n"
            + string.Join("\n", offenders));

        // Armed: the scan must actually be looking at accelerators, or an empty result would prove nothing.
        Assert.That(Regex.Count(xaml, @"(Gesture|InputGesture)=""[^""]*Cmd\+[^""]*"""),
            Is.GreaterThan(10), "the shell really does declare mac branches (the scan is not vacuous)");
    }
}
