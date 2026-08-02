using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace safe_visual_tests;

/// <summary>
/// Double-click (node activation, US-044) opens the dialog IHC Visual opens (uxparity S-30). Measured live on
/// `Project1-SimpelWired.vis`: a room opens its locality properties, a product opens the product dialog, and a
/// PIN opens its owning product's dialog — the vendor has no per-pin dialog.
/// </summary>
public class ActivateNodeParityTests
{
    private static async Task<(ShellHarness Harness, ihc_openvisual.ViewModels.MainWindowViewModel Vm)> OpenedAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.OpenAsync(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis"));
        return (harness, vm);
    }

    [Test]
    public async Task Activate_AProduct_OpensTheProductDialog()
    {
        (ShellHarness harness, var vm) = await OpenedAsync();
        using var _ = harness;
        var product = vm.InstallationNodes[0].Children[0].Children[0];

        await vm.ActivateNodeCommand.ExecuteAsync(product);

        Assert.That(harness.Dialogs.LastProductPropertiesInput, Is.Not.Null,
            "double-clicking a product opens the product dialog");
    }

    [Test]
    public async Task Activate_APinOfAProduct_OpensItsOwningProductDialog()
    {
        (ShellHarness harness, var vm) = await OpenedAsync();
        using var _ = harness;
        var pin = vm.InstallationNodes[0].Children[0].Children[0].Children.First(c => c.IsPin);

        await vm.ActivateNodeCommand.ExecuteAsync(pin);

        Assert.That(harness.Dialogs.LastProductPropertiesInput, Is.Not.Null,
            "a pin has no dialog of its own — it activates its owner");
    }

    [Test]
    public async Task Activate_ALocality_OpensTheNameNoteDialog()
    {
        (ShellHarness harness, var vm) = await OpenedAsync();
        using var _ = harness;

        await vm.ActivateNodeCommand.ExecuteAsync(vm.InstallationNodes[0].Children[0]);

        Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Edit Stue properties"));
    }
}
