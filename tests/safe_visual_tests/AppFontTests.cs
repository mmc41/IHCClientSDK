using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The app renders in the font it SHIPS (portability §9): <c>Avalonia.Fonts.Inter</c> is embedded in the
/// executable, and the font manager's default family points at it.
/// <para>Registering the collection is not enough on its own and that is the whole point of this test: the Inter
/// package declares no default family name, so <c>WithInterFont()</c> alone leaves every control that states no
/// <c>FontFamily</c> — which is nearly all of them — rendering in Segoe UI Variable / SF / whatever fontconfig
/// picks. That divergence is invisible on the development machine and shows up as different metrics and different
/// æ/ø/å on the other two desktops.</para>
/// </summary>
public class AppFontTests
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void DefaultFontFamily_IsTheEmbeddedInterFont()
    {
        Assert.That(FontManager.Current.DefaultFontFamily.Name, Is.EqualTo("Inter"));
    }

    /// <summary>The default reaches the controls: a control that names no font must resolve to Inter, since
    /// inheriting "whatever the platform had" is exactly the defect.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AControlThatNamesNoFont_RendersInInter()
    {
        var window = new NamePromptWindow();
        window.Show();

        var box = window.FindControl<TextBox>("NameBox");

        Assert.That(box!.FontFamily.Name, Is.EqualTo("Inter"));
    }

    /// <summary>Inter carries the Danish letters itself, so no fallback is consulted for ordinary UI text.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheAppFont_CoversTheDanishLetters()
    {
        bool found = FontManager.Current.TryGetGlyphTypeface(
            new Typeface(new FontFamily(ihc_openvisual.Program.AppFontFamily)), out GlyphTypeface? typeface);

        Assert.That(found, Is.True, "the embedded family resolves");
        Assert.Multiple(() =>
        {
            foreach (char letter in "æøåÆØÅ")
            {
                Assert.That(typeface!.CharacterToGlyphMap.TryGetGlyph(letter, out ushort glyph) && glyph != 0, Is.True,
                    $"Inter has a glyph for U+{(int)letter:X4} ({letter})");
            }
        });
    }
}
