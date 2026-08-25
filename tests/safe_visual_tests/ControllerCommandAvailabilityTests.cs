using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-4 (owner ruling 2026-08-11 "all button enablement is in scope"): the two controller-transfer
/// commands are unavailable while no controller is connected, and say so.
///
/// <para>Measured on the reference application: <i>Hent projekt</i> (24579) and <i>Send projekt</i> (24580) are
/// greyed on the toolbar AND on the Controller menu — on a fresh unnamed project and on a saved one alike, so
/// the gate is not "a project is open". Its whole Controller menu is greyed except
/// <i>Kommunikationsindstillinger…</i>: nothing that needs a controller is offered until one is there.</para>
///
/// <para>OpenVisual offered both unconditionally (Send behind a project-open gate, Retrieve behind none), while
/// its own status bar said "Ikke forbundet til controller" — so the bar advertised a transfer the app knew it
/// could not perform, and invoking it produced a "controller required" message box. That is the same fact the
/// indicator already carried, delivered later and as an interruption.</para>
///
/// <para>This matches the app's own spec rather than overriding it: FR-9.1 sends "to a connected controller",
/// and every scenario in story E10 (US-042/US-043) opens with "Given a controller is connected". E10 is
/// <b>Not Ready</b>, so no Ready story is contradicted.</para>
/// </summary>
public class ControllerCommandAvailabilityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> BuildAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        return (harness, vm);
    }

    [Test]
    public async Task Disconnected_BothTransferCommands_AreUnavailableAndExplainWhy()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        Assert.That(vm.IsControllerConnected, Is.False, "precondition: this build connects to no controller");

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "controller.send", "controller.retrieve" })
            {
                Assert.That(vm.Registry.Bar[id].Enabled, Is.False,
                    $"{id} is not offered while no controller is connected — as the reference application greys it");
                Assert.That(vm.Registry.Bar[id].Reason, Is.Not.Null.And.Not.Empty,
                    $"{id} says WHY it is unavailable, per the registered 'unavailable commands explain themselves'");
                Assert.That(vm.Registry.Toolbar[id].Enabled, Is.False,
                    $"{id}'s toolbar button follows the same rule as its menu item — one command, one availability");
            }
        });
    }

    /// <summary>
    /// The gate is the CONNECTION, not the project: the reference application greys both on a saved project
    /// too. Asserted as a transition so a gate that merely happens to be false cannot pass.
    /// </summary>
    [Test]
    public async Task Connecting_OffersTheTransferCommandsAgain()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;
        Assert.That(vm.Registry.Bar["controller.retrieve"].Enabled, Is.False, "precondition: disconnected");

        vm.IsControllerConnected = true;

        Assert.Multiple(() =>
        {
            Assert.That(vm.Registry.Bar["controller.retrieve"].Enabled, Is.True,
                "a connected controller can be read from");
            Assert.That(vm.Registry.Bar["controller.send"].Enabled, Is.True,
                "…and, with a project open, sent to");
        });
    }

    /// <summary>Send additionally needs something to send — its existing project-open gate is not replaced.</summary>
    [Test]
    public async Task Connected_ButNoProject_StillWithholdsSend()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        ShellContext connectedButClosed = vm.Context with { ProjectOpen = false, ControllerConnected = true };
        CommandSpec send = vm.Registry.Rows.Single(r => r.Id == "controller.send");

        Assert.That(CommandRegistry.For(send, connectedButClosed, Surface.MenuBar).Enabled, Is.False,
            "there is nothing to send with no project open");
    }
}
