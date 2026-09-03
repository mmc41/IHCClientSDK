using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The registered difference "a refused edit says what to do about it" (alignment F-47), pinned on the rule it was
/// measured against: at most one modem per project (US-013).
///
/// <para>The reference application titles its box with the application's own name — <i>LK IHC Visual ®</i> — and
/// states the rule alone: "Modem er allerede indsat. Der kan kun indsættes et modem i projektet". IHC OpenVisual
/// titles it for the rule and adds the remedy. What must NOT drift is the shape: a descriptive title, and a message
/// that says both what the rule is and what to do next.</para>
///
/// <para>The rule itself — the refusal happens, at the same moment, with the same end state — is the MATCHED half
/// and is asserted here too, because a difference registered about the wording is only acceptable while the
/// behaviour underneath it is identical: refuse the second modem, add nothing, leave the first one alone.</para>
/// </summary>
public class RefusalMessageParityTests
{
    private static ProductMenuItemViewModel ModemLeaf(MainWindowViewModel vm) =>
        vm.ProductsMenu.First(c => c.Header == CatalogMenu.BusProductsCategory)
                       .Children.First(m => m.Header == "SMS Modem");

    [Test]
    public async Task SecondModem_IsRefusedWithATitleAndARemedy()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        TreeNodeViewModel locality = vm.InstallationNodes[0].Children[0];
        vm.SelectNode(locality);

        await ((IAsyncRelayCommand)ModemLeaf(vm).Command!).ExecuteAsync(null);   // the one modem the project may hold
        int afterFirst = locality.Children.Count;

        await ((IAsyncRelayCommand)ModemLeaf(vm).Command!).ExecuteAsync(null);   // refused

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastMessageTitle, Is.EqualTo("Kun ét modem"),
                "the box is titled for the rule, not for the application (the registered difference)");
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("højst").And.Contain("ét modem"),
                "the message states the rule, as the reference application's does");
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("Fjern det eksisterende modem"),
                "and adds the remedy, which is the whole of the registered difference");
            Assert.That(locality.Children, Has.Count.EqualTo(afterFirst),
                "the refusal adds nothing — the end state matches the reference application's");
        });
    }
}
