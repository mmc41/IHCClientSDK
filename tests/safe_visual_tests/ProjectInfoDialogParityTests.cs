using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Ihc.Vis;
using ihc_openvisual.Services;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The project-information dialog must offer the same field inventory as IHC Visual's (uxparity S-32). Measured
/// against the vendor's <c>Rediger projekt oplysninger</c> on `Project1-SimpelWired.vis`: it has three groups —
///
/// <list type="bullet">
/// <item><c>Projekt oplysninger</c>: Projektnummer, Projekt type, Programmør, Tegning, Beskrivelse — FIVE fields.</item>
/// <item><c>Installatør information</c> and <c>Kunde oplysninger</c>: Navn, Vej, Telefon, Postnummer,
/// Mobil telefon, By, Email, Land — eight each.</item>
/// </list>
///
/// OpenVisual offered 3 + 8 + 8, missing <c>Projekt type</c> and <c>Tegning</c>. Both are declared in the format
/// (<c>project_info</c>'s DTD lists <c>type</c> and <c>drawing</c>), so this was surfacing data the file already
/// carries — see <c>MetadataCommandTests</c> for the engine half of the same gap.
/// </summary>
public class ProjectInfoDialogParityTests
{
    // The logical tree, not the visual one: an unshown window has applied no templates, so a ScrollViewer's content
    // is not yet in its visual tree — enumerating that way would find zero boxes and the assertion would be vacuous.
    private static string[] EditableFieldNames(Window window) => FieldNames(window);

    // Depth-first in child order, so these read out in document order — which is what a reading-order assertion needs.
    private static HeaderedContentControl[] Groups(Window window) =>
        window.GetLogicalDescendants().OfType<HeaderedContentControl>().ToArray();

    private static string[] FieldNamesIn(HeaderedContentControl group) => FieldNames(group);

    // TextBox OR AutoCompleteBox: the sixteen CONTACT fields are the vendor's editable combos, so they are
    // AutoCompleteBoxes, while the five project fields stay plain boxes. Both are "an editable field" here.
    private static string[] FieldNames(Control root) =>
        root.GetLogicalDescendants().OfType<Control>()
            .Where(c => c is TextBox or AutoCompleteBox)
            .Select(c => c.Name!)
            .ToArray();

    private static string[] LabelTextsIn(HeaderedContentControl group) =>
        group.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text!).ToArray();

    [AvaloniaTest]
    public void Dialog_OffersEveryProjectLevelFieldTheVendorOffers()
    {
        var window = new ProjectInfoWindow();

        Assert.That(EditableFieldNames(window).Where(n => n.StartsWith("Proj", StringComparison.Ordinal)), Is.EquivalentTo(new[]
        {
            "ProjNumberBox", "ProjTypeBox", "ProjProgrammerBox", "ProjDrawingBox", "ProjDescriptionBox",
        }));
    }

    /// <summary>The vendor lays the dialog out as three captioned group boxes: <c>Projekt oplysninger</c> across the
    /// top, then <c>Installatør information</c> and <c>Kunde oplysninger</c> — installer FIRST. OpenVisual presented
    /// the same fields as one narrow scrolling column with Customer above Installer, so the reading order differed and
    /// both contact groups sat below the fold. The captions and their order are the parity assertion.</summary>
    [AvaloniaTest]
    public void Dialog_PresentsThreeCaptionedGroups_InstallerBeforeCustomer()
    {
        var window = new ProjectInfoWindow();

        Assert.That(Groups(window).Select(g => g.Header), Is.EqualTo(new[] { "Projekt oplysninger", "Installatør information", "Kunde oplysninger" }));
    }

    /// <summary>The two contact groups sit side by side in one row, as in the vendor's dialog — not stacked, which is
    /// what forced the scrolling. Same grid row, different columns.</summary>
    [AvaloniaTest]
    public void ContactGroups_SitSideBySideInOneRow()
    {
        var groups = Groups(new ProjectInfoWindow());

        Assert.Multiple(() =>
        {
            Assert.That(Grid.GetRow(groups[1]), Is.EqualTo(Grid.GetRow(groups[2])), "installer and customer share a row");
            Assert.That(Grid.GetColumn(groups[1]), Is.EqualTo(0), "installer on the left");
            Assert.That(Grid.GetColumn(groups[2]), Is.EqualTo(1), "customer on the right");
        });
    }

    /// <summary>Each contact group is a two-column grid read row-major — Navn|Vej, Telefon|Postnummer,
    /// Mobil telefon|By, Email|Land — so the eight fields come out in this order. OpenVisual listed them
    /// Name/Address/City/Zip/Country/Phone/Mobile/Email, a different sequence for the same eight fields.</summary>
    [AvaloniaTest]
    public void ContactGroups_PresentTheEightFieldsInTheVendorsReadingOrder()
    {
        var groups = Groups(new ProjectInfoWindow());

        Assert.Multiple(() =>
        {
            Assert.That(FieldNamesIn(groups[1]), Is.EqualTo(new[]
            {
                "InstNameBox", "InstAddressBox", "InstPhoneBox", "InstZipBox",
                "InstMobileBox", "InstCityBox", "InstEmailBox", "InstCountryBox",
            }));
            Assert.That(FieldNamesIn(groups[2]), Is.EqualTo(new[]
            {
                "CustNameBox", "CustAddressBox", "CustPhoneBox", "CustZipBox",
                "CustMobileBox", "CustCityBox", "CustEmailBox", "CustCountryBox",
            }));
        });
    }

    /// <summary>The project group's five fields run across one row (Number, Type, Programmer, Drawing) with the wide
    /// Description beneath, as the vendor lays them out.</summary>
    [AvaloniaTest]
    public void ProjectGroup_PresentsItsFiveFieldsInTheVendorsReadingOrder()
    {
        var groups = Groups(new ProjectInfoWindow());

        Assert.That(FieldNamesIn(groups[0]), Is.EqualTo(new[]
        {
            "ProjNumberBox", "ProjTypeBox", "ProjProgrammerBox", "ProjDrawingBox", "ProjDescriptionBox",
        }));
    }

    /// <summary>Field captions are the vendor's own, now that the UI is Danish throughout: <c>Vej</c> and
    /// <c>Postnummer</c>, which is also the wording US-039 specifies. OpenVisual said "Address"/"Zip code".</summary>
    [AvaloniaTest]
    public void ContactFieldLabels_UseTheVendorsWording()
    {
        var groups = Groups(new ProjectInfoWindow());
        string[] expected = ["Navn:", "Vej:", "Telefon:", "Postnummer:", "Mobil telefon:", "By:", "Email:", "Land:"];

        Assert.Multiple(() =>
        {
            Assert.That(LabelTextsIn(groups[1]), Is.EqualTo(expected));
            Assert.That(LabelTextsIn(groups[2]), Is.EqualTo(expected));
        });
    }

    /// <summary>US-039 names the second project field <em>Projekt type</em>, as the vendor does; the dialog had
    /// shortened it to "Type".
    /// <para>
    /// <c>Beskrivelse</c> carries NO trailing colon — alone among the dialog's twenty-one captions. Read off the
    /// vendor's live dialog (<c>dialog.read</c> on <c>g10 4-10-2025</c>, 2026-08-04): every other Static ends in
    /// ':' and this one does not. It is an inconsistency in the vendor's own dialog, and mirroring it is the
    /// point — this suite pins what the vendor does, not what is tidy.
    /// </para>
    /// </summary>
    [AvaloniaTest]
    public void ProjectFieldLabels_UseTheVendorsWording()
    {
        var groups = Groups(new ProjectInfoWindow());

        Assert.That(LabelTextsIn(groups[0]), Is.EqualTo(new[]
        {
            "Projektnummer:", "Projekt type:", "Programmør:", "Tegning:", "Beskrivelse",
        }));
    }

    /// <summary>The sixteen contact fields are the vendor's editable COMBOS, each offering the matching data table
    /// (US-049): <c>Firma</c> behind the installer's Navn, <c>Kunder</c> behind the customer's, and one shared list
    /// behind each of the other seven — the vendor offered the same street/phone/zip/city/country/email/mobile
    /// list on both sides. OpenVisual had plain boxes offering nothing.</summary>
    [AvaloniaTest]
    public void ContactFields_OfferTheirDataTable()
    {
        var window = new ProjectInfoWindow();
        var suggestions = new ProjectInfoSuggestions(
            InstallerNames: ["Firma A"], CustomerNames: ["Kunde B"], Streets: ["Virum gyde 2"],
            Phones: ["23 44 52 16"], Zips: ["2830"], Mobiles: ["26 77 12 83"], Cities: ["Virum"],
            Emails: ["kunde@example.dk"], Countries: ["Danmark"]);

        window.Offer(suggestions);

        Assert.Multiple(() =>
        {
            Assert.That(window.InstNameBox.ItemsSource, Is.EqualTo(new[] { "Firma A" }), "installer Navn ← Firma");
            Assert.That(window.CustNameBox.ItemsSource, Is.EqualTo(new[] { "Kunde B" }), "customer Navn ← Kunder");
            Assert.That(window.InstAddressBox.ItemsSource, Is.SameAs(window.CustAddressBox.ItemsSource),
                "…and the other seven share one table per field, as the vendor's do");
            Assert.That(window.CustCountryBox.ItemsSource, Is.EqualTo(new[] { "Danmark" }));
        });
    }

    /// <summary>A value typed here joins the table it would have been offered from, which is how the vendor's
    /// tables fill up — every one of its <c>Kunder</c> rows was typed into this dialog, not into the data-tables
    /// editor. Committing project info therefore absorbs the contact values.</summary>
    [Test]
    public void CommittedContactValues_JoinTheDataTables()
    {
        var store = new DataTableStore(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "dt.json"));
        var info = new ProjectInfoData("d", "n", "p", "Villa", "T-1",
            Customer: new ContactInfo("Kunde Bo Bæk", "Virum gyde 2", "Virum", "2830", "Danmark", "", "", ""),
            Installer: new ContactInfo("Firma A", "Virum gyde 2", "", "", "Danmark", "", "", ""));

        store.Commit(ProjectInfoSuggestions.Absorb(store, info));

        Assert.Multiple(() =>
        {
            Assert.That(store.TextsFor("customer"), Is.EqualTo(new[] { "Kunde Bo Bæk" }));
            Assert.That(store.TextsFor("company"), Is.EqualTo(new[] { "Firma A" }));
            Assert.That(store.TextsFor("street"), Is.EqualTo(new[] { "Virum gyde 2" }),
                "the same street from both sides is stored once");
            Assert.That(store.TextsFor("country"), Is.EqualTo(new[] { "Danmark" }));
            Assert.That(store.TextsFor("projecttype"), Is.EqualTo(new[] { "Villa" }));
            Assert.That(store.TextsFor("email"), Is.Empty, "a blank field adds nothing");
        });
    }

    /// <summary>The two new fields must be wired both ways, not merely present: shown from the loaded project and
    /// returned on OK. A dialog that displays a field it then discards is the failure mode worth pinning.</summary>
    [AvaloniaTest]
    public void TypeAndDrawing_RoundTripThroughTheDialog()
    {
        var window = new ProjectInfoWindow();
        var loaded = new ProjectInfoData("d", "n", "p", "Villa", "Tegning 4b", ContactInfo.Empty, ContactInfo.Empty);

        window.Populate(loaded);
        ProjectInfoData shown = window.Collect();
        window.ProjTypeBox.Text = "Erhverv";
        window.ProjDrawingBox.Text = "T-12";
        ProjectInfoData edited = window.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo(loaded), "what was loaded is what is shown");
            Assert.That(edited.Type, Is.EqualTo("Erhverv"));
            Assert.That(edited.Drawing, Is.EqualTo("T-12"));
        });
    }

    /// <summary>End to end through the shell: edit the two fields in the dialog, then save, and assert the values
    /// reached the saved <c>.vis</c>. This is the parity scenario's own assertion — the vendor's dialog writes
    /// <c>type</c>/<c>drawing</c> into <c>project_info</c>, so OpenVisual's must too.</summary>
    [AvaloniaTest]
    public async Task EditingTypeAndDrawing_ReachesTheSavedFile()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.OpenAsync(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis"));
        harness.Dialogs.ProjectInfoResponder = current => current with { Type = "Villa", Drawing = "Tegning 4b" };
        string target = harness.TempPath("s32.vis");
        harness.Dialogs.SavePath = target;

        await vm.ProjectInfoCommand.ExecuteAsync(null);
        await harness.Session.SaveAsAsync();

        string saved = await File.ReadAllTextAsync(target, System.Text.Encoding.Latin1);
        Assert.Multiple(() =>
        {
            Assert.That(saved, Does.Contain("type=\"Villa\""));
            Assert.That(saved, Does.Contain("drawing=\"Tegning 4b\""));
        });
    }
}
