using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>US-049: the data-tables model — read-only system tables (built-in enum definitions) and CRUD over
/// the editable user-defined texts.</summary>
public class DataTablesTests
{
    private static ElementId ParseId(string token)
    {
        ElementId.TryParse(token, out ElementId id);
        return id;
    }

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

    [Test]
    public async Task AddUserText_AppendsToTheEditableList()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        var ok = await harness.Session.AddUserTextAsync("By main door");

        var texts = harness.Session.GetDataTables().UserTexts;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(texts.Select(t => t.Text), Does.Contain("By main door"));
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    [Test]
    public async Task EditUserText_ChangesTheText()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddUserTextAsync("Old text");
        var id = ParseId(harness.Session.GetDataTables().UserTexts.Single().Id);

        var ok = await harness.Session.UpdateUserTextAsync(id, "New text");

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.Session.GetDataTables().UserTexts.Single().Text, Is.EqualTo("New text"));
        });
    }

    [Test]
    public async Task DeleteUserText_RemovesOnlyThatText()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddUserTextAsync("Keep");
        await harness.Session.AddUserTextAsync("Remove");
        var remove = ParseId(harness.Session.GetDataTables().UserTexts.First(t => t.Text == "Remove").Id);

        var ok = await harness.Session.DeleteUserTextAsync(remove);

        var texts = harness.Session.GetDataTables().UserTexts.Select(t => t.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(texts, Does.Contain("Keep").And.Not.Contain("Remove"));
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
