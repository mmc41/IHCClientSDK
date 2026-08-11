using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The registered difference "the free-text fields the original backs with a suggestion drop-down are plain text
/// boxes" (alignment F-13, widened to the terminal editor by F-34).
///
/// <para>What must not drift is the CONTROL, in both directions. Growing a fixed drop-down would refuse values the
/// `.vis` format and the reference application both accept — <c>cable_colour</c> is <c>CDATA</c>, and the original's
/// own list mixes colour names with installer-written pair descriptions ("Brun", "1-Hvid. 3-Sort"). Losing the field
/// entirely would be worse. So each documentation field is asserted to resolve as a <see cref="TextBox"/> — which a
/// <see cref="ComboBox"/> or an <c>AutoCompleteBox</c> of the same name would not.</para>
///
/// <para>Scope: the product dialog and the terminal address editor, which is exactly what the register names. The
/// modem dialog's equivalents are deliberately NOT asserted here — whether they fall under this difference is an
/// open question the register does not yet answer (raised as alignment F-52), and pinning an unruled surface would
/// settle it by accident.</para>
/// </summary>
public class FreeTextFieldParityTests : AvaloniaTestBase
{
    /// <summary>Every documentation field the register names on the product dialog, by control name.</summary>
    private static readonly string[] ProductDocumentationFields =
        ["NameBox", "PlaceringBox", "NoteBox", "CableTypeBox", "CableNumberBox", "IdentificationBox", "LightGroupBox"];

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ProductDialog_DocumentationFields_AreFreeTextBoxes()
    {
        var window = new ProductPropertiesWindow();
        CurrentTestWindow = window;

        Assert.Multiple(() =>
        {
            foreach (string field in ProductDocumentationFields)
            {
                Assert.That(window.FindControl<TextBox>(field), Is.Not.Null,
                    $"{field} is free text — a drop-down here would refuse values the .vis format accepts");
            }
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TerminalEditor_NoteAndCableColour_AreFreeTextBoxes()
    {
        var window = new PinPropertiesWindow();
        CurrentTestWindow = window;

        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<TextBox>("NoteBox"), Is.Not.Null, "the terminal's Note is free text");
            Assert.That(window.FindControl<TextBox>("CableColourBox"), Is.Not.Null,
                "Ledningsfarve is free text — the original's own list mixes colours with pair descriptions");
        });
    }
}
