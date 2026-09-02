using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>E10 (US-042/043): the controller-transfer stories are live-controller operations, deferred per the
/// epic and forbidden here (no controller side effects). These tests cover the genuinely offline slice — the
/// unlinked-wireless pre-flight warning — and confirm the commands never contact a controller.</summary>
public class ControllerTransferTests
{
    [Test]
    public async Task GetUnlinkedWirelessProducts_DetectsAnUnlinkedWirelessProduct()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var wireless = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith(CatalogMenu.WirelessProductsCategory, StringComparison.Ordinal));
        await harness.Session.AddProductAsync(loc, wireless.ProductIdentifier);

        var unlinked = harness.Session.GetUnlinkedWirelessProducts();

        Assert.That(unlinked, Is.Not.Empty, "a freshly inserted wireless product is not yet linked to the controller");
    }

    // US-042: Send warns about unlinked wireless products; declining the warning cancels.
    [Test]
    public async Task SendProject_UnlinkedWireless_DeclineCancels()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var wireless = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith(CatalogMenu.WirelessProductsCategory, StringComparison.Ordinal));
        await harness.Session.AddProductAsync(loc, wireless.ProductIdentifier);
        harness.Dialogs.ConfirmResult = false;   // decline the "send anyway?" warning

        await vm.SendProjectCommand.ExecuteAsync(null);

        Assert.That(vm.StatusText, Is.EqualTo("Afsendelse annulleret."), "declining the unlinked-wireless warning cancels the send");
    }

    // US-042: accepting the warning proceeds to the controller-required notice (this build never contacts a controller).
    [Test]
    public async Task SendProject_AcceptedWarning_ReportsControllerRequired()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var wireless = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith(CatalogMenu.WirelessProductsCategory, StringComparison.Ordinal));
        await harness.Session.AddProductAsync(loc, wireless.ProductIdentifier);
        harness.Dialogs.ConfirmResult = true;

        await vm.SendProjectCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("tilsluttet controller"), "the transfer requires a controller");
            Assert.That(vm.StatusText, Does.Contain("controller"));
        });
    }

    // US-043: Retrieve reports that a connected controller is required; nothing is contacted.
    [Test]
    public async Task RetrieveProject_ReportsControllerRequired()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.RetrieveProjectCommand.ExecuteAsync(null);

        Assert.That(harness.Dialogs.LastMessage, Does.Contain("tilsluttet controller"));
    }
}
