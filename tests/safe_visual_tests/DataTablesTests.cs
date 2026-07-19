using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>US-049 (UI face): the data-tables view-model loads the read-only system tables + editable user texts,
/// adds through the dialog prompt, and guards Delete with an app-level confirm. The user-text CRUD <b>command
/// semantics</b> (append/create-table, rename-by-id, delete-only-that-row) now live in
/// <c>safe_project_tests.SessionUserTextTests</c> (against <c>ProjectDocumentSession</c>, W2-16).</summary>
public class DataTablesTests
{
    [Test]
    public async Task SystemTables_AreTheReadOnlyBuiltins()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        var model = harness.Session.GetDataTables();

        Assert.Multiple(() =>
        {
            Assert.That(model.SystemTables.Length, Is.GreaterThanOrEqualTo(2), "the built-in system tables are shown");
            Assert.That(model.SystemTables.All(t => t.Rows.Length > 0), Is.True, "each system table lists its rows");
            Assert.That(model.UserTexts, Is.Empty, "a fresh project has no user-defined texts");
        });
    }

    // US-049: the view-model loads the tables and adds a user text through the dialog prompt.
    [Test]
    public async Task ViewModel_LoadsTables_AndAddsText()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var dt = new DataTablesViewModel(harness.Session, harness.Dialogs);
        harness.Dialogs.PropertiesResult = new PropertiesResult("By main door", string.Empty);

        await dt.AddTextCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(dt.SystemTables, Is.Not.Empty, "system tables load");
            Assert.That(dt.UserTexts.Select(t => t.Text), Does.Contain("By main door"), "the added text appears in the list");
        });
    }

    // US-049: Delete is guarded by an app-level confirm (the vendor deletes with no prompt).
    [Test]
    public async Task ViewModel_Delete_RespectsConfirm()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddUserTextAsync("Doomed");
        var dt = new DataTablesViewModel(harness.Session, harness.Dialogs);
        dt.SelectedUserText = dt.UserTexts.Single();

        harness.Dialogs.ConfirmResult = false;   // declining the guard keeps the text
        await dt.DeleteTextCommand.ExecuteAsync(null);
        Assert.That(dt.UserTexts, Is.Not.Empty, "declining the confirm keeps the text");

        dt.SelectedUserText = dt.UserTexts.Single();
        harness.Dialogs.ConfirmResult = true;
        await dt.DeleteTextCommand.ExecuteAsync(null);
        Assert.That(dt.UserTexts, Is.Empty, "confirming removes the text");
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
            Assert.That(harness.Dialogs.LastDataTablesViewModel!.SystemTables, Is.Not.Empty);
        });
    }
}
