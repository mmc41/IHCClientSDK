using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Views;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The registered difference "the free-text fields the original backs with a suggestion drop-down are plain text
/// boxes" (alignment F-13, widened to the terminal editor by F-34) — **now half-closed, and restated in the
/// vocabulary the dialogs are actually built from (D07/D12, T030).**
///
/// <para>What must not drift is that no documentation field becomes a CLOSED list. Growing a fixed drop-down would
/// refuse values the `.vis` format and the reference application both accept — <c>cable_colour</c> is
/// <c>CDATA</c>, and the original's own list mixes colour names with installer-written pair descriptions ("Brun",
/// "1-Hvid. 3-Sort"). Losing the field entirely would be worse.</para>
///
/// <para>The composer now gives the fields the original backs with a drop-down the
/// <see cref="DialogControlKind.ComboSuggest"/> kind, which renders as an ALWAYS-EDITABLE combo over the project's
/// own distinct values (D07) — so those fields are no longer a difference at all, while the rest stay
/// <see cref="DialogControlKind.Text"/>. Asserted as kinds rather than as control types by name, because the
/// control a kind renders as is the renderer's business and the descriptor is where the promise lives: a kind
/// silently changed to a closed list would fail here even if the widget still looked right.</para>
///
/// <para>Scope: every product family, not just the wired one. Since T030 there is one dialog, so the register's
/// old carve-out for the modem ("whether its equivalents fall under this difference is unruled", F-52) no longer
/// has a surface to apply to — the modem's Note and Placering ARE these fragments.</para>
/// </summary>
public class FreeTextFieldParityTests : AvaloniaTestBase
{
    /// <summary>
    /// The documentation fields the register names, and the kind each must keep — taken from the RECORDED
    /// VENDOR ORACLE for `_0x2101` (T037), not from what the code happened to do.
    /// <para>Measured: six of the seven are <c>ComboBox</c> on the vendor side — Kabeltype, Kabelnummer,
    /// Identifikationskode, Lysgruppe, Placering, Note. Only <c>Navn</c> is a plain <c>Edit</c>. Note that
    /// Identifikationskode and Lysgruppe carry ZERO items in a fresh project and are combos anyway: the
    /// KIND is the affordance, and an empty suggestion list is a combo with nothing in it yet, which is
    /// exactly what D07's project-sourced list gives on a new project.</para>
    /// <para>This table said <c>Text</c> for Kabelnummer and Lysgruppe until T037. It was written in T030
    /// by reading the fragments rather than the oracle, so it pinned the defect in place and reported
    /// green.</para>
    /// </summary>
    private static readonly (string Caption, DialogControlKind Kind)[] ProductDocumentationFields =
    [
        ("Navn", DialogControlKind.Text),
        ("Placering", DialogControlKind.ComboSuggest),
        ("Note", DialogControlKind.ComboSuggest),
        ("Kabeltype", DialogControlKind.ComboSuggest),
        ("Kabelnummer", DialogControlKind.ComboSuggest),
        ("Identifikationskode", DialogControlKind.ComboSuggest),
        ("Lysgruppe", DialogControlKind.ComboSuggest),
    ];

    [Test]
    public void ProductDialog_DocumentationFields_AreNeverClosedLists()
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality,
            app.GetAvailableProducts().First(p => p.ProductIdentifier == "_0x104"))).Value;   // a wired product

        var offered = app.GetProductDialog(session.Current!, placed)
            .Groups.SelectMany(g => g.Fields).ToDictionary(f => f.Caption, f => f.Control);

        Assert.Multiple(() =>
        {
            foreach ((string caption, DialogControlKind kind) in ProductDocumentationFields)
            {
                Assert.That(offered.ContainsKey(caption), Is.True, $"{caption} is still offered");
                Assert.That(offered[caption], Is.EqualTo(kind),
                    $"{caption} keeps its kind — a closed list here would refuse values the .vis format accepts");
            }
            // The vocabulary itself carries the guarantee: there is no closed-list kind to drift INTO (D12).
            Assert.That(System.Enum.GetNames<DialogControlKind>(), Has.None.Contains("ComboClosed"));
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
