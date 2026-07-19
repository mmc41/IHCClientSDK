using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// A-34 (US-028) — in programming mode, dragging a variable onto an events/commands container opens the same method
/// popup the two-step <i>Use in program</i> arms, and the chosen method builds the event/command. The menu-building is
/// shared (<c>RebuildProgramMenus</c>), the drop is gated on the A-27 locked-block rule, and the two-step supplement
/// stays.
/// </summary>
public class DragProgramTests : AvaloniaTestBase
{
    // An editable (empty) block in programming mode with one input variable "Doorbell", plus the Events container.
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId doorbellId, ElementId eventsId)>
        EditableProgramAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var inputSectionId = vm.InstallationNodes[0].Children[0].ElementId!.Value;   // "Input"
        await harness.Session.AddVariableAsync(inputSectionId, "resource_input", "Doorbell");
        var doorbell = TreeNodes.FindFirst(vm.InstallationNodes, n => n.IsPin && n.DisplayName == "Doorbell")!;
        var events = TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsEventsContainer)!;
        return (harness, vm, doorbell.ElementId!.Value, events.ElementId!.Value);
    }

    // The drop surfaces the SAME method set as Use-in-program, and choosing one builds the same event node.
    [Test]
    public async Task DropVariableOnEvents_OffersMethodPopup_ThenBuilds()
    {
        var (harness, vm, doorbellId, eventsId) = await EditableProgramAsync();
        using var _ = harness;

        Assert.That(vm.DragDrop.CanDropOn(doorbellId, eventsId).Effect, Is.EqualTo(DropEffect.Link), "a variable over Events is a legal authoring drop");

        await vm.DragDrop.PerformDropAsync(doorbellId, eventsId);

        // The drop armed the variable and populated the Events method popup for that container — identical to the
        // two-step Use-in-program menu (US-028).
        Assert.That(vm.ProgramEventMenu.Select(m => m.Header),
            Is.EquivalentTo(new[] { "Doorbell changes to ON", "Doorbell changes state", "Doorbell is assigned" }),
            "the drop offers the same method set as Use-in-program");

        var option = vm.ProgramEventMenu.First(m => m.Header == "Doorbell changes to ON");
        await ((IAsyncRelayCommand)option.Command!).ExecuteAsync(null);

        var eventsAfter = TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsEventsContainer)!;
        Assert.Multiple(() =>
        {
            Assert.That(eventsAfter.Children.Any(c => c.DisplayName == "Doorbell -> ON"), Is.True, "the chosen method builds the event");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // A-27: a locked (library) block's program is view-only — a program-building drop is refused, with a reason, and
    // builds nothing.
    [Test]
    public async Task DropVariable_IntoLockedBlock_Refused()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var fbNode = vm.FunctionNodes[0].Children[0].Children[0];
        vm.EnterProgrammingModeCommand.Execute(fbNode);
        Assert.That(vm.IsProgrammingBlockLocked, Is.True, "a library block is locked (view-only)");

        var variable = TreeNodes.FindFirst(vm.InstallationNodes, n => n.IsPin)!;
        var container = TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsEventsContainer || n.IsCommandsContainer)!;

        DropVerdict verdict = vm.DragDrop.CanDropOn(variable.ElementId!.Value, container.ElementId!.Value);
        await vm.DragDrop.PerformDropAsync(variable.ElementId!.Value, container.ElementId!.Value);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Ok, Is.False, "no program-building drop into a locked library block");
            Assert.That(verdict.Reason, Is.Not.Null.And.Not.Empty, "the refusal says why");
            Assert.That(vm.ProgramEventMenu, Is.Empty, "nothing was armed");
            Assert.That(vm.ProgramCommandMenu, Is.Empty);
        });
    }

    // The highlight follows legality: DragOver a program container shows the authoring Link effect; over the dragged
    // variable itself shows None.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DragOver_HighlightsProgramContainer()
    {
        var (harness, vm, doorbellId, eventsId) = await EditableProgramAsync();
        using var _ = harness;

        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        for (int i = 0; i < 4; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
        }
        CurrentTestWindow = window;

        var variable = TreeNodes.FindById(vm.InstallationNodes, doorbellId)!;
        var events = TreeNodes.FindById(vm.FunctionNodes, eventsId)!;

        var overContainer = window.DragOverEffect(variable, events);
        var overSelf = window.DragOverEffect(variable, variable);

        Assert.Multiple(() =>
        {
            Assert.That(overContainer, Is.EqualTo(DragDropEffects.Link), "a variable over a program container highlights as an authoring Link");
            Assert.That(overSelf, Is.EqualTo(DragDropEffects.None), "over itself shows no drop");
        });
    }
}
