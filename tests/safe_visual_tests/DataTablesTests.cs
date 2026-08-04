using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>
/// US-049, measured against the vendor's <i>Rediger data tabeller</i> on <c>g10 4-10-2025</c> (2026-08-04): a
/// selectable list of EIGHTEEN named tables on the left, the SELECTED table's user-defined texts on the right,
/// Tilføj / Rediger / Slet over those texts (the latter two greyed until a text is picked), and OK / Annuller.
///
/// <para>
/// The tables are <b>application</b> state, not project state. Proven on the vendor: the values it listed under
/// <c>Kunder</c> occur nowhere in the open project's <c>.vis</c>, and several were entered while other projects
/// were open. IHC Visual declares the eighteen in <c>Data\userEditableTables.txttables</c>.
/// </para>
/// <para>
/// What this replaces: OpenVisual listed the open project's <c>enum_definition</c>s as "System tabeller
/// (skrivebeskyttet)" — function-block types such as <i>Persienne tilstand</i>, which the vendor's dialog does not
/// show at all — beside one global user-text list read from an enum named <c>User-defined texts</c>. No <c>.vis</c>
/// in the corpus contains that name, so that pane could never be anything but empty: the feature was unreachable,
/// not merely differently shaped.
/// </para>
/// </summary>
public class DataTablesTests
{
    private static DataTableStore Store() =>
        new(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "datatables.json"));

    /// <summary>The vendor's eighteen captions, in the vendor's order — which is its manifest's order.</summary>
    [Test]
    public void Tables_AreTheVendorsEighteen_InTheVendorsOrder()
    {
        var vm = new DataTablesViewModel(Store(), new FakeDialogService());

        Assert.That(vm.Tables.Select(t => t.Name), Is.EqualTo(new[]
        {
            "Kunder", "Firma", "Mobil telefonnumre", "Telefon numre", "email adresser", "Vejnavne", "By",
            "Post numre", "Land", "Ledningsfarver", "Kabelnummer", "Kabeltyper", "Produkt position",
            "Note tekster", "Lysgrupper", "Projekt typer", "Datalinie modul lokationer",
            "Produkt identifikationskoder",
        }));
    }

    /// <summary>The vendor opens with the first table selected and its texts shown.</summary>
    [Test]
    public void Dialog_OpensOnTheFirstTable()
    {
        var vm = new DataTablesViewModel(Store(), new FakeDialogService());

        Assert.That(vm.SelectedTable?.Name, Is.EqualTo("Kunder"));
    }

    /// <summary>The right list belongs to the LEFT list's selection — the defect the vendor comparison exposed was
    /// a right pane that was global rather than per-table.</summary>
    [Test]
    public async Task Texts_FollowTheSelectedTable()
    {
        var dialogs = new FakeDialogService();
        var vm = new DataTablesViewModel(Store(), dialogs);
        dialogs.PropertiesResult = new PropertiesResult("Kunde Bo Bæk", string.Empty);
        await vm.AddTextCommand.ExecuteAsync(null);                       // added under Kunder

        vm.SelectedTable = vm.Tables.Single(t => t.Name == "Vejnavne");
        var underStreets = vm.Texts.ToArray();
        vm.SelectedTable = vm.Tables.Single(t => t.Name == "Kunder");

        Assert.Multiple(() =>
        {
            Assert.That(underStreets, Is.Empty, "a text added to Kunder does not appear under Vejnavne");
            Assert.That(vm.Texts, Is.EqualTo(new[] { "Kunde Bo Bæk" }), "…and is still there under Kunder");
        });
    }

    /// <summary>Rediger and Slet are greyed until a text row is picked — read off the vendor's dialog, where they
    /// stayed greyed with seven texts listed and none selected.</summary>
    [Test]
    public async Task EditAndDelete_AreDisabledUntilATextIsSelected()
    {
        var dialogs = new FakeDialogService();
        var vm = new DataTablesViewModel(Store(), dialogs);
        dialogs.PropertiesResult = new PropertiesResult("Morten", string.Empty);
        await vm.AddTextCommand.ExecuteAsync(null);
        vm.SelectedText = null;

        Assert.That(vm.HasSelectedText, Is.False, "Rediger and Slet bind IsEnabled to this");

        vm.SelectedText = "Morten";
        Assert.That(vm.HasSelectedText, Is.True);
    }

    /// <summary>Slet removes at once with no confirmation — the vendor asks for none (US-049), which is safe here
    /// because the deletion lives in the working copy until OK, so Annuller is the undo.</summary>
    [Test]
    public async Task Delete_RemovesImmediately_AndAnnullerIsTheUndo()
    {
        var dialogs = new FakeDialogService();
        DataTableStore store = Store();
        var vm = new DataTablesViewModel(store, dialogs);
        dialogs.PropertiesResult = new PropertiesResult("Doomed", string.Empty);
        await vm.AddTextCommand.ExecuteAsync(null);
        vm.Commit();                                                      // OK: "Doomed" is now stored

        var second = new DataTablesViewModel(store, dialogs);
        second.SelectedText = "Doomed";
        second.DeleteTextCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(second.Texts, Is.Empty, "deleted at once, with no confirmation prompt");
            Assert.That(dialogs.ConfirmCalls, Is.Zero, "…and nothing was asked");
            Assert.That(store.TextsFor("customer"), Is.EqualTo(new[] { "Doomed" }),
                "the store is untouched until OK — closing with Annuller keeps the text");
        });
    }

    /// <summary>OK commits the working copy to the store, and the store outlives the dialog — the tables are
    /// application state, so they are there for the next project too.</summary>
    [Test]
    public async Task Ok_CommitsToTheStore_WhichPersistsAcrossDialogs()
    {
        var dialogs = new FakeDialogService();
        DataTableStore store = Store();
        var vm = new DataTablesViewModel(store, dialogs);
        dialogs.PropertiesResult = new PropertiesResult("Virum gyde 2", string.Empty);
        vm.SelectedTable = vm.Tables.Single(t => t.Name == "Vejnavne");
        await vm.AddTextCommand.ExecuteAsync(null);

        vm.Commit();
        var reopened = new DataTablesViewModel(store, dialogs);
        reopened.SelectedTable = reopened.Tables.Single(t => t.Name == "Vejnavne");

        Assert.That(reopened.Texts, Is.EqualTo(new[] { "Virum gyde 2" }));
    }

    /// <summary>The store round-trips through its own file, so the texts survive a restart.</summary>
    [Test]
    public void Store_RoundTripsThroughItsFile()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "datatables.json");
        var store = new DataTableStore(path);

        store.Commit(new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<string>>
        {
            ["customer"] = new[] { "Kunde Bo Bæk", "Morten" },
            ["street"] = new[] { "Virum gyde 2" },
        });
        var reloaded = new DataTableStore(path);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.TextsFor("customer"), Is.EqualTo(new[] { "Kunde Bo Bæk", "Morten" }));
            Assert.That(reloaded.TextsFor("street"), Is.EqualTo(new[] { "Virum gyde 2" }));
            Assert.That(reloaded.TextsFor("country"), Is.Empty, "a table never added to reads empty, not missing");
        });
    }

    // US-049: the Documentation menu command opens the data-tables dialog.
    [Test]
    public async Task DataTablesCommand_OpensTheDialog()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.DataTablesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.ShowDataTablesCalls, Is.EqualTo(1));
            Assert.That(harness.Dialogs.LastDataTablesViewModel!.Tables, Has.Count.EqualTo(18));
        });
    }
}
