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

        // The D13 divergence this test rides on: gate allows (the flyout's Cut really runs), bar greys.
        Assert.That(vm.Registry.Commands["edit.cut"].CanExecute(null), Is.True, "precondition: the gate allows Cut");
        Assert.That(vm.Registry.Bar["edit.cut"].Enabled, Is.False, "precondition: the bar greys Cut");

        window.KeyPressQwerty(PhysicalKey.X, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.That(vm.Context.Clipboard, Is.Null, "Ctrl+X follows the bar: the locked block is not cut");
        Assert.That(harness.Session.Current!.FindById(lockedFb), Is.Not.Null, "the block survives");
        Assert.That(vm.StatusText, Is.EqualTo("A locked block cannot be cut from the menu bar."),
            "the refused gesture explains itself in the status bar");
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

        // The D13 divergence this test rides on: gate allows (the flyout's Delete really runs), bar greys.
        Assert.That(tree.SelectedItem, Is.SameAs(vm.SelectedFunctionsNode), "precondition: the tree carries the selection");
        Assert.That(vm.Registry.Commands["edit.delete"].CanExecute(null), Is.True, "precondition: the gate allows Delete");
        Assert.That(vm.Registry.Bar["edit.delete"].Enabled, Is.False, "precondition: the bar greys Delete");

        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.FindById(lockedFb), Is.Not.Null,
                "Delete follows the bar: the locked block survives");
            Assert.That(vm.StatusText, Is.EqualTo("A locked block cannot be deleted from the menu bar."),
                "the refused key explains itself in the status bar");
        });
    }
}
