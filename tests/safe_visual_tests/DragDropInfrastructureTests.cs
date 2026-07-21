using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// A-30 — the shared drag-and-drop infrastructure Wave 9 rides on: a <see cref="DropVerdict"/> (ok + reason + effect +
/// route) returned by the <see cref="TreeDragDropController.CanDropOn"/> dispatcher, drop-target highlighting as
/// controller state, the drop side keyed off the <c>DataTransfer</c> (never a captured source field, so an external
/// drop works too — §0.3), and the drag source wired on <b>both</b> trees for any addressable node. The concrete
/// routes (move/reorder/link/program-build) are A-31…A-34; these tests pin the plumbing. A-P0's
/// <see cref="DragDropPocTests"/> remain the drop-target mechanism tests this builds on.
/// </summary>
public class DragDropInfrastructureTests : AvaloniaTestBase
{
    // A wired product in locality A plus an empty locality B — the smallest shape with a legal product→locality
    // route and a second locality to move to. No window (view-model layer, §0.3).
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId productId, ElementId locA, ElementId locB)>
        BuildAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var productId = harness.Session.Current!.FindById(locA)!.ChildrenOrEmpty().First(c => c.Tag.StartsWith("product_")).Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;
        return (harness, vm, productId, locA, locB);
    }

    // Shows the real window and realizes every row, so the window tests can hit-test and drive pointer input.
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, MainWindow window, ElementId productId, ElementId locA, ElementId locB)>
        ShowAsync()
    {
        var (harness, vm, productId, locA, locB) = await BuildAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        for (int i = 0; i < 3; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
        }
        return (harness, vm, window, productId, locA, locB);
    }

    // The minimal legality every Wave-9 route shares — a node cannot drop on itself — plus the shape the riders read:
    // ok + effect + a human reason. Asserts BOTH halves (refused-with-reason AND the legal product→locality Move) so
    // neither passes vacuously (§0.1).
    [Test]
    public async Task CanDropOn_RefusesNodeOnItself_WithReason()
    {
        var (harness, vm, productId, _, locB) = await BuildAsync();
        using var _ = harness;

        DropVerdict onSelf = vm.DragDrop.CanDropOn(productId, productId);
        DropVerdict onLocality = vm.DragDrop.CanDropOn(productId, locB);

        Assert.Multiple(() =>
        {
            Assert.That(onSelf.Ok, Is.False, "a node cannot be dropped onto itself");
            Assert.That(onSelf.Effect, Is.EqualTo(DropEffect.None), "a refused drop shows no effect");
            Assert.That(onSelf.Reason, Is.Not.Null.And.Not.Empty, "the refusal carries a human reason for the status bar");
            Assert.That(onLocality.Ok, Is.True, "a product may drop onto a different locality");
            Assert.That(onLocality.Effect, Is.EqualTo(DropEffect.Move), "a product→locality drop is a Move");
        });
    }

    // W3-9 — a drag-over probe resolves the concrete route ONCE and records it in the verdict, so the drop performs it
    // without re-evaluating (the old dispatcher re-asked the SDK up to 3×); and probing the legality never mutates the
    // project (no editor construction per pointer event).
    [Test]
    public async Task CanDropOn_ResolvesTheRoute_AndProbingDoesNotMutate()
    {
        var (harness, vm, productId, _, locB) = await BuildAsync();
        using var _ = harness;
        Project before = harness.Session.Current!;

        DropVerdict verdict = vm.DragDrop.CanDropOn(productId, locB);
        // Probe repeatedly, as a pointer moving over rows would.
        vm.DragDrop.CanDropOn(productId, locB);
        vm.DragDrop.CanDropOn(productId, productId);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Route, Is.EqualTo(DropRoute.Reparent), "a product→locality drop resolves to the reparent route the drop then performs directly");
            Assert.That(verdict.Effect, Is.EqualTo(DropEffect.Move), "the reparent route still presents as a Move to the drag-over cursor");
            Assert.That(harness.Session.Current, Is.SameAs(before), "a drag-over probe evaluates the verdict without constructing an editor / mutating the project");
        });
    }

    // Drop-target highlighting is controller state (the item template binds a row background to IsDropTarget): setting
    // a target marks its row, switching targets clears the old one, and null clears the highlight entirely.
    [Test]
    public async Task HighlightDropTarget_MarksTarget_AndClearsPrevious()
    {
        var (harness, vm, _, locA, locB) = await BuildAsync();
        using var _ = harness;
        var nodeA = TreeNodes.FindById(vm.InstallationNodes, locA)!;
        var nodeB = TreeNodes.FindById(vm.InstallationNodes, locB)!;

        vm.DragDrop.HighlightDropTarget(locA);
        Assert.That(nodeA.IsDropTarget, Is.True, "the drop-target row is highlighted");

        vm.DragDrop.HighlightDropTarget(locB);
        Assert.Multiple(() =>
        {
            Assert.That(nodeB.IsDropTarget, Is.True, "the new target row is highlighted");
            Assert.That(nodeA.IsDropTarget, Is.False, "the previous target's highlight is cleared");
        });

        vm.DragDrop.HighlightDropTarget(null);
        Assert.That(nodeB.IsDropTarget, Is.False, "null clears the highlight");
    }

    // The load-bearing §0.3 rule: the drop reads the dragged id from the DataTransfer, NOT the current selection or a
    // source field. Selecting a different node than the one dragged must not change what moves — and it is what makes a
    // genuine external drop (which has no in-app selection at all) work.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Drop_ReadsIdFromDataTransfer_NotSelection()
    {
        var (harness, vm, window, productId, locA, locB) = await ShowAsync();
        using var _ = harness;
        CurrentTestWindow = window;

        vm.SelectedInstallationNode = TreeNodes.FindById(vm.InstallationNodes, locA);   // select someone else

        var product = TreeNodes.FindById(vm.InstallationNodes, productId)!;
        var localityB = TreeNodes.FindById(vm.InstallationNodes, locB)!;
        window.DragOnto(product, localityB);

        Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locB),
            "the product carried by the DataTransfer moved, regardless of which node was selected");
    }

    // A-30 wires the drag SOURCE on BOTH trees (A-P0 wired only the Installation tree) and arms on any addressable node,
    // not just products (A-32/A-33/A-34 drag localities, pins and variables). Driving the gesture on a Functions-pane
    // locality must arm + initiate the source path — proving both deltas at once.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DragSourceArms_OnFunctionsPane_ForAddressableNode()
    {
        var (harness, vm, window, _, locA, _) = await ShowAsync();
        using var _d = harness;
        CurrentTestWindow = window;

        var funcLocality = TreeNodes.FindById(vm.FunctionNodes, locA)!;
        Assert.That(TreeDragData.BuildDragData(funcLocality), Is.Not.Null, "any addressable node builds a drag payload");

        var start = window.RowPoint(funcLocality);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X + 40, start.Y + 40), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Assert.That(window.DragInitiatedForTest, Is.True,
            "the drag source is wired on the Functions tree and arms on an addressable (non-product) node");
    }
}
