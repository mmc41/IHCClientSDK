using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-13 (tmp/align-campaign-2026-08-10.md): the product dialog's field labels are the reference
/// application's own words.
///
/// <para>Measured side by side on a `Lampeudtag` (2026-08-11, both dialogs open at once): every label matched
/// except one — the reference application labels the field <b>Identifikationskode</b>, OpenVisual labelled it
/// <i>Id-kode</i>. Story 03 (US-011) already calls the field *Identifikationskode* in its own field list, so the
/// label diverged from the reference application AND from the story describing it.</para>
///
/// <para>Scope note: the abbreviation is correct where story 09 mandates it — the documentation-report finding
/// *Mangler Id-kode* is a fixed label on a different surface and is deliberately left alone. This is about the
/// DIALOG field caption.</para>
/// </summary>
public class ProductDialogLabelParityTests : AvaloniaTestBase
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ProductDialog_LabelsTheIdentificationCode_AsTheVendorDoes()
    {
        var window = new ProductPropertiesWindow();
        CurrentTestWindow = window;

        Assert.That(window.FindControl<TextBlock>("IdentificationLabel")?.Text,
            Is.EqualTo("Identifikationskode"),
            "the vendor's own caption for the field (measured), and the word story 03 uses for it");
    }

    /// <summary>The modem dialog carries the same field, so it carries the same caption — one field, one name.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ModemDialog_UsesTheSameCaptionForTheSameField()
    {
        var window = new ModemPropertiesWindow();
        CurrentTestWindow = window;

        Assert.That(window.FindControl<TextBlock>("IdentificationLabel")?.Text,
            Is.EqualTo("Identifikationskode"),
            "the same field must not be called two different things in two dialogs");
    }
}
