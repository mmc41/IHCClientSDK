using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-25 — the contact line under a library block's provenance group.
///
/// <para>Measured live 2026-08-11 on a locked <c>1.1.01.e. Kip tænd sluk</c>: the reference application's
/// <c>Funktionsblok egenskaber</c> closes its <i>Oprindelige egenskaber</i> box with static control 521 reading
/// <b>"Kontakt ovennævnte i tilfælde af problemer"</b>. OpenVisual said <i>ovenstående</i> — the same meaning
/// ("the above" rather than "the above-mentioned"), and the one word in this dialog that did not match.</para>
///
/// <para>Asserted on the rendered CONTROL rather than on the markup text: what a user reads is the TextBlock, and
/// a string constant matching while the control shows something else is precisely the failure mode this
/// campaign's pixel/peer rules exist for. The line is also given a name here, so it is addressable at all.</para>
/// </summary>
public class LibraryOriginWordingParityTests
{
    private const string VendorContactLine = "Kontakt ovennævnte i tilfælde af problemer";

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheProvenanceGroup_UsesTheOriginalsContactWording()
    {
        var window = new PropertiesWindow();
        window.Populate("1.1.01.e. Kip tænd sluk", string.Empty,
            new LibraryOrigin("Kip tænd sluk", "1.1.01", "e", "17/05/2017", "Schneider Electric"));

        Assert.That(window.FindControl<TextBlock>("OriginContactLabel")!.Text, Is.EqualTo(VendorContactLine));
    }

    /// <summary>It belongs to the provenance group, so it appears only when that group does — a block authored
    /// from scratch has no master to contact about.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void WithoutAProvenanceGroup_TheContactLineIsNotShown()
    {
        var window = new PropertiesWindow();
        window.Populate("Tom blok", string.Empty);

        Assert.That(window.FindControl<StackPanel>("OriginPanel")!.IsVisible, Is.False,
            "no master, no provenance group — and so no contact line");
    }
}
