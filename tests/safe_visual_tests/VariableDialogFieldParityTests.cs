using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.Views;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-29 and F-30 — the variable dialog's two documentation captions, and its bool value control.
/// Both measured in ONE read of the reference application's <c>Rediger Flag egenskaber</c> (2026-08-11), since
/// both live in that dialog:
/// <code>
///   Navn                              (213)
///   Tekst til funktionsdokumentation  (214)   ← OpenVisual said "Note"
///   Noter for hjælpetekst             (517)   ← OpenVisual said "Hjælpetekst"
///   Initial værdi                     (215)   ComboBox ["OFF","ON"]  ← OpenVisual used a CheckBox
///   Ved strømsvigt ▸ Gem aktuel værdi (216)
/// </code>
///
/// <para><b>The wording is per DIALOG, not global.</b> The original's PRODUCT and MODEM dialogs label the same
/// kind of field plain <c>Note</c> (measured in the same session) — so this correction is scoped to the variable
/// dialog, and renaming a shared label would have broken the two that were already right. That is the F-49
/// lesson applied before making the mistake rather than after.</para>
///
/// <para>The bool control is a combo in the original, and OpenVisual already uses ON/OFF combos for the same
/// idea elsewhere (the pin-properties and scene-value dialogs), so a checkbox here diverged from both the
/// original and the app's own convention.</para>
/// </summary>
public class VariableDialogFieldParityTests
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheTwoDocumentationFields_CarryTheOriginalsCaptions()
    {
        var window = Populated(ResourceInitialValue.OfBool(false));

        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<TextBlock>("NoteLabel")!.Text,
                Is.EqualTo("Tekst til funktionsdokumentation"));
            Assert.That(window.FindControl<TextBlock>("HelpNoteLabel")!.Text,
                Is.EqualTo("Noter for hjælpetekst"));
        });
    }

    /// <summary>The name field keeps its short caption — the original's is <c>Navn</c> here too, so this is the
    /// control that says the change above is about the original's wording and not about making things longer.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheNameField_KeepsTheOriginalsShortCaption()
    {
        Assert.That(Populated(ResourceInitialValue.OfBool(false)).FindControl<TextBlock>("NameLabel")!.Text,
            Is.EqualTo("Navn"));
    }

    /// <summary>A bool initial value is a combo of OFF/ON, in the original's order, with the current state
    /// selected.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ABoolInitialValue_IsAnOffOnCombo()
    {
        VariablePropertiesWindow window = Populated(ResourceInitialValue.OfBool(true));
        var combo = window.FindControl<ComboBox>("BoolBox")!;

        Assert.Multiple(() =>
        {
            Assert.That(combo.IsVisible, Is.True);
            Assert.That(combo.ItemsSource, Is.EqualTo(new[] { "OFF", "ON" }).AsCollection);
            Assert.That(combo.SelectedIndex, Is.EqualTo(1), "the variable is ON");
        });
    }

    /// <summary>…and it commits what is selected, so the control swap is not cosmetic.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ChoosingOff_CommitsFalse()
    {
        VariablePropertiesWindow window = Populated(ResourceInitialValue.OfBool(true));
        window.FindControl<ComboBox>("BoolBox")!.SelectedIndex = 0;

        Assert.That(window.ResultForTest().Bool, Is.False);
    }

    private static VariablePropertiesWindow Populated(ResourceInitialValue value)
    {
        var window = new VariablePropertiesWindow();
        window.Populate(new VariablePropertiesInput("Rediger Flag egenskaber", "Flag", string.Empty, value));
        return window;
    }
}
