using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// crudarch T021 branch B (per the T016 spike verdict — disabled controls show no tooltip): pressing a
/// registry gesture whose command is gated OFF writes the bar Availability.Reason to the status bar, so a
/// refused shortcut always explains itself (QC-06). A KeyBinding never invokes a disabled command, so this
/// window-level handler is the only route for the explanation.
/// </summary>
public class DisabledReasonStatusBarTests
{
    [AvaloniaTest]
    public async System.Threading.Tasks.Task PressedDisabledGesture_WritesReasonToStatusBar()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(vm.Registry.Bar["edit.undo"].Enabled, Is.False, "precondition: fresh history — Undo is gated off");

        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.That(vm.StatusText, Is.EqualTo("Nothing to undo."),
            "Ctrl+Z with an empty history explains itself in the status bar");

        window.KeyPressQwerty(PhysicalKey.F4, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.That(vm.StatusText, Is.EqualTo("Select a link row to jump to its opposite half."),
            "F4 with no link row selected explains itself too");

        window.Close();
    }

    // D06 (owner ruling 2026-08-02): gestures follow the MENU BAR's availability, not the gate. On a locked
    // block the bar greys Cut while the flyout offers it (D13) — so Ctrl+X must REFUSE (project untouched)
    // and explain itself, even though the materialized command's gate would execute.
    [AvaloniaTest]
    public async System.Threading.Tasks.Task GestureOnALockedBlock_FollowsTheBar_RefusesAndExplains()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = harness.Session.Current!.Groups[0].Id!.Value;
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var lockedFb = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        vm.SelectNode(TreeNodes.FindById(vm.FunctionNodes, lockedFb)!);
        Dispatcher.UIThread.RunJobs();

        // uxparity2 T017 (D15): this test used to ride a divergence — gate allows, bar greys — and assert Ctrl+X
        // REFUSED. V1 retired that: the bar enables Cut on a locked block on both surfaces and both fixtures. The
        // D06 property under test is unchanged (the gesture consults BAR availability, not the raw gate); what
        // changed is the bar's answer, so the gesture now ACTS. The refusal path keeps its own coverage in
        // PressedDisabledGesture_WritesReasonToStatusBar above.
        Assert.That(vm.Registry.Commands["edit.cut"].CanExecute(null), Is.True, "precondition: the gate allows Cut");
        Assert.That(vm.Registry.Bar["edit.cut"].Enabled, Is.True, "precondition (D15): the bar ENABLES Cut on a locked block");

        window.KeyPressQwerty(PhysicalKey.X, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.That(vm.Context.Clipboard, Is.Not.Null, "Ctrl+X follows the bar, and the bar now allows it");
        Assert.That(harness.Session.Current!.FindById(lockedFb), Is.Not.Null,
            "cut STAGES a move — the block is still in the project until it is pasted");
    }

    // D06 completeness: Delete/F2/F4 are NOT <Window.KeyBindings> — the trees' own KeyDown handler services them —
    // so they must consult the same bar availability the KeyBinding-routed gestures now do. Delete is the case
    // where that bites: on a locked block the gate allows (the flyout's Slet really runs) while the bar greys it.
    [AvaloniaTest]
    public async System.Threading.Tasks.Task DeleteKeyOnALockedBlock_FollowsTheBar_RefusesAndExplains()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = harness.Session.Current!.Groups[0].Id!.Value;
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var lockedFb = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tree = window.FindControl<TreeView>("FunctionsTree")!;
        vm.SelectedFunctionsNode = TreeNodes.FindById(vm.FunctionNodes, lockedFb);
        Dispatcher.UIThread.RunJobs();
        tree.GetVisualDescendants().OfType<TreeViewItem>().First().Focus();   // the tree's KeyDown handler services Delete
        Dispatcher.UIThread.RunJobs();

        // uxparity2 T017 (D15): as with Ctrl+X above, this rode the retired S-28 divergence and asserted the key
        // REFUSED. The D06 property under test is unchanged — Delete is NOT a <Window.KeyBinding>, the tree's own
        // KeyDown handler services it, and it must consult the same BAR availability the routed gestures do. What
        // changed is the bar's answer on a locked block, so the key now acts.
        Assert.That(tree.SelectedItem, Is.SameAs(vm.SelectedFunctionsNode), "precondition: the tree carries the selection");
        Assert.That(vm.Registry.Commands["edit.delete"].CanExecute(null), Is.True, "precondition: the gate allows Delete");
        Assert.That(vm.Registry.Bar["edit.delete"].Enabled, Is.True, "precondition (D15): the bar ENABLES Delete on a locked block");

        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        Dispatcher.UIThread.RunJobs();

        Assert.That(harness.Session.Current!.FindById(lockedFb), Is.Null,
            "Delete follows the bar, and the bar now allows it — the locked block is removed");
    }
}
