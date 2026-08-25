using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-28: the initial-value field is called <b>Initial værdi</b>.
///
/// <para>Measured 2026-08-11 on a <c>Flag</c> in a block's <i>Interne variable</i> section: the reference
/// application's <c>Rediger Flag egenskaber</c> captions that group <c>&amp;Initial værdi</c>. OpenVisual called
/// it <i>Startværdi</i> — in the variable dialog and in the pin dialog both.</para>
///
/// <para>It also diverged from OpenVisual's OWN story: 03/US-011 spells the terminal editor's field
/// "<c>Initial værdi</c>" verbatim, and the test-data notes use the same words for the same concept. So one
/// concept had two names across two dialogs and its own specification — the same defect class as F-13's
/// <i>Id-kode</i>/<i>Identifikationskode</i>, and fixed the same way.</para>
/// </summary>
public class InitialValueLabelParityTests : AvaloniaTestBase
{
    private const string Caption = "Initial værdi";

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void VariableDialog_NumericInitialValue_UsesTheVendorsCaption()
    {
        var window = new VariablePropertiesWindow();
        CurrentTestWindow = window;

        Assert.That(window.FindControl<TextBlock>("NumberLabel")?.Text, Is.EqualTo(Caption),
            "the vendor's own caption for the field (measured), and the words story 03 uses for it");
    }

    /// <summary>The time-valued variant shows the same field, so it carries the same caption.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void VariableDialog_TimeInitialValue_UsesTheSameCaption()
    {
        var window = new VariablePropertiesWindow();
        CurrentTestWindow = window;

        Assert.That(window.FindControl<TextBlock>("TimeLabel")?.Text, Is.EqualTo(Caption),
            "one field, one name — a time-valued variable's initial value is still the initial value");
    }

    /// <summary>The bool variant names the same field. It used to state the value in a CheckBox's own content
    /// ("Initial værdi: ON"); the control is now the original's OFF/ON combo (alignment F-30), so the caption sits
    /// in a label beside it like every other variant's. The rule under test is unchanged — one field, one name —
    /// and it is asserted on whichever control currently carries it.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void VariableDialog_BoolInitialValue_UsesTheSameCaption()
    {
        var window = new VariablePropertiesWindow();
        CurrentTestWindow = window;

        Assert.That(window.FindControl<TextBlock>("BoolLabel")?.Text, Is.EqualTo(Caption),
            "the bool variant names the same field as every other variant");
    }

    /// <summary>A terminal's editor carries the field too, and story 03/US-011 names it there explicitly.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void PinDialog_UsesTheSameCaptionForTheSameField()
    {
        var window = new PinPropertiesWindow();
        CurrentTestWindow = window;

        Assert.That(window.FindControl<TextBlock>("InitialValueLabel")?.Text, Is.EqualTo(Caption),
            "the same field must not be called two different things in two dialogs");
    }
}
