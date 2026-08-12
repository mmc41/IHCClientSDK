using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-14 — what the application SAYS while an insert dialog is open.
///
/// <para>Placing a product asks for its documentation as part of placing it, and cancelling places nothing
/// (US-011, uxparity S-12). OpenVisual implements that by applying the insert first and rolling back on Cancel;
/// the reference application raises the dialog first and adds nothing until OK (measured 2026-08-11: its tree
/// item count is unchanged, 40 → 40, while the dialog is up). The end states agree either way, including the id
/// counter, so the ORDER is a registered difference — see product.md.</para>
///
/// <para>What is not a difference of order is the <b>status bar</b>. Measured live on the same run, OpenVisual's
/// status line read <c>Produktet 'Lampeudtag' indsat under Køkken</c> — completed, past tense — while the dialog
/// was still open and the installer had committed nothing, and could still press Annuller (and did: the row then
/// vanished and the line changed to <c>Indsætning af 'Lampeudtag' annulleret.</c>). That is the application
/// stating something about the project that is not true yet, which is the defect class F-43 was raised for, and
/// it is separable from the ordering: the announcement simply belongs after the commit.</para>
/// </summary>
public class InsertStatusHonestyTests
{
    private const string LampeudtagIdentifier = "Lampeudtag";

    /// <summary>The claim must not be made while the dialog is still open.</summary>
    [Test]
    public async Task WhileTheInsertDialogIsOpen_NothingClaimsTheProductIsInserted()
    {
        var (harness, vm) = await ShellAsync();
        using var _ = harness;
        string? statusDuringDialog = null;
        harness.Dialogs.ProductDialogResponder = _ =>
        {
            statusDuringDialog = vm.StatusText;
            return new ProductDialogEdits([]);   // an ordinary OK with nothing changed
        };

        await InsertLampeudtagAsync(vm);

        Assert.That(statusDuringDialog, Does.Not.Contain("indsat"),
            $"the installer can still cancel, so nothing is inserted yet — status read '{statusDuringDialog}'");
    }

    /// <summary>…and it must still be made once the dialog is committed. Without this the fix could be "never
    /// announce", which removes the false claim by removing the true one too.</summary>
    [Test]
    public async Task CommittingTheDialog_AnnouncesTheInsert()
    {
        var (harness, vm) = await ShellAsync();
        using var _ = harness;

        await InsertLampeudtagAsync(vm);

        Assert.That(vm.StatusText, Does.Contain("indsat").And.Contain("Køkken"));
    }

    /// <summary>Cancelling says so, and leaves nothing behind — the end state the ordering difference is
    /// registered on.</summary>
    [Test]
    public async Task CancellingTheDialog_SaysSo_AndInsertsNothing()
    {
        var (harness, vm) = await ShellAsync();
        using var _ = harness;
        harness.Dialogs.CancelProductDialog = true;

        await InsertLampeudtagAsync(vm);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Does.Contain("annulleret"));
            Assert.That(Kitchen(vm).Children, Is.Empty, "a cancelled insert places nothing");
        });
    }

    private static TreeNodeViewModel Kitchen(MainWindowViewModel vm) =>
        vm.InstallationNodes[0].Children.Single(c => c.DisplayName == "Køkken");

    private static async Task InsertLampeudtagAsync(MainWindowViewModel vm)
    {
        vm.SelectNode(Kitchen(vm));
        ProductMenuItemViewModel leaf = vm.ProductsMenu
            .Single(f => f.Header == CatalogMenu.WiredProductsCategory)
            .Children.Single(c => c.Header == "Output")
            .Children.Single(c => c.Header == LampeudtagIdentifier);
        await ((IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);
    }

    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> ShellAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        return (harness, vm);
    }
}
