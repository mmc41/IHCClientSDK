using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// A-32 (US-055) — dragging a sibling onto another same-tag sibling reorders it to that position, the same
/// id-preserving move as US-054 but with an in-container target index; the effect is a Move and the result matches one
/// step of the <i>Move up/down</i> supplement. The reorder legality (same parent, same tag) is the SDK's one rule
/// (<see cref="Ihc.Vis.ProjectCommands.CanReorderNode"/>, probed index-backed through the document since crudarch
/// T008), not re-encoded in the view-model.
/// </summary>
public class DragReorderTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId first, ElementId second)> BuildAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var first = harness.Session.Current!.Groups[0].Id!.Value;
        var second = harness.Session.Current!.Groups[1].Id!.Value;
        return (harness, vm, first, second);
    }

    private static async Task<(ShellHarness harness, MainWindowViewModel vm, MainWindow window, ElementId first, ElementId second)> ShowAsync()
    {
        var (harness, vm, first, second) = await BuildAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        for (int i = 0; i < 3; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
        }
        return (harness, vm, window, first, second);
    }

    // Dropping the first locality onto the second reorders it to the second's position — order [second, first], ids
    // preserved — which is exactly one Move-down of the first. The drag-over shows a Move.
    [Test]
    public async Task DropSiblingAtPosition_Reorders_MatchesMoveUpDown()
    {
        var (harness, vm, first, second) = await BuildAsync();
        using var _ = harness;

        Assert.That(vm.DragDrop.CanDropOn(first, second).Effect, Is.EqualTo(DropEffect.Move), "dropping onto a same-tag sibling is a reorder Move");

        await vm.DragDrop.PerformDropAsync(first, second);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.Groups[0].Id!.Value, Is.EqualTo(second), "the first locality moved to the second's slot");
            Assert.That(harness.Session.Current!.Groups[1].Id!.Value, Is.EqualTo(first), "order is now [second, first] — one Move-down of the first");
            Assert.That(harness.Session.Current!.FindById(first), Is.Not.Null, "the id is preserved (a move, not a copy)");
            Assert.That(vm.StatusText, Is.EqualTo("Omarrangeret."));
        });
    }

    // The highlight follows legality: DragOver a same-tag sibling shows the reorder insertion target (Move); over the
    // dragged node itself shows no drop (None).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DragOver_ShowsInsertionTarget()
    {
        var (harness, vm, window, first, second) = await ShowAsync();
        using var _ = harness;
        CurrentTestWindow = window;

        var firstNode = TreeNodes.FindById(vm.InstallationNodes, first)!;
        var secondNode = TreeNodes.FindById(vm.InstallationNodes, second)!;

        var overSibling = window.DragOverEffect(firstNode, secondNode);
        var overSelf = window.DragOverEffect(firstNode, firstNode);

        Assert.Multiple(() =>
        {
            Assert.That(overSibling, Is.EqualTo(DragDropEffects.Move), "dragging a locality over a same-tag sibling shows the reorder target");
            Assert.That(overSelf, Is.EqualTo(DragDropEffects.None), "over itself shows no drop");
        });
    }
}
