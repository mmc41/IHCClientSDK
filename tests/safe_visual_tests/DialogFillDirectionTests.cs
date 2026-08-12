using System.Linq;
using Avalonia.Headless.NUnit;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// T035, product 001: a multi-column group fills in the direction the ORIGINAL fills it, and the
/// direction is per group rather than a house rule.
///
/// <para>Measured 2026-08-12 from the recorded vendor oracle and the side-by-side composite: the SMS
/// modem's <i>Telefon numre</i> group reads <b>down each column</b> — 1–10, then 11–20, then 21–30 —
/// where OpenVisual's <c>UniformGrid</c> filled across and produced 1, 2, 3 on the first row. Same field
/// set, same column count, different reading order: RUBRIC row 3, in scope.</para>
///
/// <para>It cannot be fixed by changing how every grid fills, because the two directions both occur.
/// The S0 device's seven fields are measured row-major — <i>Navn · ledningsfarve S0- / Identifikationskode
/// · ledningsfarve S0+ / Placering · Antal pulser / Note</i> — so a global switch to column-major would
/// fix the modem and break the S0. The direction is therefore metadata, declared by the group that
/// knows it.</para>
///
/// <para>The permutation is a DISPLAY concern and is applied in the view-model, not the composer. The
/// descriptor's field order stays the declared order, because tests and the write-back address slots by
/// position and caption — reordering there would silently redefine what "slot 17" means.</para>
/// </summary>
public class DialogFillDirectionTests : AvaloniaTestBase
{
    private static ProductDialogViewModel DialogFor(string productIdentifier)
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality,
            app.GetAvailableProducts().First(p => p.ProductIdentifier == productIdentifier))).Value;
        return new ProductDialogViewModel(app.GetProductDialog(session.Current!, placed));
    }

    /// <summary>
    /// THE finding. In a three-column grid filled row-major by the renderer, reading order 1, 11, 21, 2,
    /// 12, 22 … is what puts 1–10 down the first column — which is what the vendor shows.
    /// </summary>
    [AvaloniaTest]
    public void TheModemsTelephoneGrid_ReadsDownEachColumn_AsTheVendorDoes()
    {
        ProductDialogGroupViewModel phones =
            DialogFor("_0x3103").Groups.Single(g => g.Caption == "Telefon numre");

        var displayed = phones.DisplayFields.Select(f => f.Caption).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(phones.Columns, Is.EqualTo(3), "precondition: three columns, as measured");
            Assert.That(displayed.Take(6),
                Is.EqualTo(new[] { "Nummer 1", "Nummer 11", "Nummer 21", "Nummer 2", "Nummer 12", "Nummer 22" })
                    .AsCollection,
                "a row-major renderer fed this order draws 1-10 down the first column, 11-20 down the second");
            Assert.That(displayed, Has.Count.EqualTo(30), "and every slot is still shown exactly once");
            Assert.That(displayed.Distinct().Count(), Is.EqualTo(30));
        });
    }

    /// <summary>The declared order is untouched: only the DISPLAY sequence is permuted.</summary>
    [AvaloniaTest]
    public void TheDeclaredFieldOrderIsUnchanged()
    {
        ProductDialogGroupViewModel phones =
            DialogFor("_0x3103").Groups.Single(g => g.Caption == "Telefon numre");

        Assert.That(phones.Fields.Select(f => f.Caption).Take(3),
            Is.EqualTo(new[] { "Nummer 1", "Nummer 2", "Nummer 3" }).AsCollection,
            "slot n stays at index n-1 — the write-back and the validation tests address it by position");
    }

    /// <summary>
    /// The other direction, and the reason this is metadata rather than a house rule: the S0 device's
    /// group is measured ROW-major, so it must be left alone by the same change that fixed the modem.
    /// </summary>
    [AvaloniaTest]
    public void TheS0DevicesGroup_StillReadsAcross()
    {
        ProductDialogGroupViewModel identity =
            DialogFor("_0x2313").Groups.Single(g => g.Caption == "Produkt egenskaber");   // S0 Device

        Assert.Multiple(() =>
        {
            Assert.That(identity.Columns, Is.EqualTo(2), "precondition: two columns, as measured");
            Assert.That(identity.DisplayFields.Select(f => f.Caption),
                Is.EqualTo(identity.Fields.Select(f => f.Caption)).AsCollection,
                "row-major is the default, so display order is the declared order");
        });
    }

    // ── Column span (T038) ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A field may occupy the WHOLE row. Measured on the vendor's wired and wireless dialogs alike: Note
    /// runs the full width, and the fields after it pair up beneath it — so a wired product reads
    /// <c>Navn|Placering / Note / Kabeltype|Kabelnummer / Identifikationskode|Lysgruppe</c>.
    /// <para>Flowing all seven into a uniform two-column grid puts Kabeltype beside Note and shifts every
    /// field after it into the wrong cell. That is RUBRIC row 3, found on product 003's composite and
    /// confirmed at native resolution on 004's.</para>
    /// </summary>
    [AvaloniaTest]
    public void AWiredProductsGroup_GivesNoteItsOwnFullWidthRow()
    {
        ProductDialogGroupViewModel identity =
            DialogFor("_0x2101").Groups.Single(g => g.Caption == "Produkt egenskaber");

        var rows = identity.Rows.Select(r => r.Fields.Select(f => f.Caption).ToArray()).ToList();

        Assert.That(rows, Is.EqualTo(new[]
        {
            new[] { "Navn", "Placering" },
            new[] { "Note" },
            new[] { "Kabeltype", "Kabelnummer" },
            new[] { "Identifikationskode", "Lysgruppe" },
        }).AsCollection);
    }

    /// <summary>The wireless family shares the fragment and the shape — Note spans there too (measured on
    /// product 069), which is why the span belongs to the SHARED fragment and not to one preset.</summary>
    [AvaloniaTest]
    public void AWirelessProductsGroup_GivesNoteItsOwnFullWidthRowToo()
    {
        ProductDialogGroupViewModel identity =
            DialogFor("_0x4101").Groups.Single(g => g.Caption == "Produkt egenskaber");

        var rows = identity.Rows.Select(r => r.Fields.Select(f => f.Caption).ToArray()).ToList();

        Assert.That(rows, Is.EqualTo(new[]
        {
            new[] { "Navn", "Placering" },
            new[] { "Note" },
            new[] { "Identifikationskode", "Lysgruppe" },
        }).AsCollection);
    }

    /// <summary>A span wider than the group is clamped, not honoured literally: the modem puts Note in a
    /// ONE-column group, where "span two" would otherwise ask for a column that does not exist.</summary>
    [AvaloniaTest]
    public void ASpanWiderThanTheGroupIsClamped()
    {
        ProductDialogGroupViewModel identity =
            DialogFor("_0x3103").Groups.Single(g => g.Caption == "Modem egenskaber");

        Assert.Multiple(() =>
        {
            Assert.That(identity.Columns, Is.EqualTo(1));
            Assert.That(identity.Rows.Select(r => r.Fields.Single().Caption),
                Is.EqualTo(new[] { "Navn", "Note", "Placering", "Identifikationskode" }).AsCollection,
                "one field per row, in declared order, exactly as before spans existed");
        });
    }

    /// <summary>The telephone grid keeps its three-per-row shape: every slot spans one.</summary>
    [AvaloniaTest]
    public void TheTelephoneGridStillPacksThreePerRow()
    {
        ProductDialogGroupViewModel phones =
            DialogFor("_0x3103").Groups.Single(g => g.Caption == "Telefon numre");

        Assert.Multiple(() =>
        {
            Assert.That(phones.Rows, Has.Count.EqualTo(10));
            Assert.That(phones.Rows[0].Fields.Select(f => f.Caption),
                Is.EqualTo(new[] { "Nummer 1", "Nummer 11", "Nummer 21" }).AsCollection,
                "the column-major permutation still applies, row by row");
        });
    }

    /// <summary>A single-column group is unaffected whichever direction it declares — the permutation is
    /// the identity there, and asserting it stops a future change from special-casing wrongly.</summary>
    [AvaloniaTest]
    public void ASingleColumnGroupIsNeverPermuted()
    {
        ProductDialogGroupViewModel kabling =
            DialogFor("_0x3103").Groups.Single(g => g.Caption == "Kabling");

        Assert.Multiple(() =>
        {
            Assert.That(kabling.Columns, Is.EqualTo(1));
            Assert.That(kabling.DisplayFields.Select(f => f.Caption),
                Is.EqualTo(kabling.Fields.Select(f => f.Caption)).AsCollection);
        });
    }
}
