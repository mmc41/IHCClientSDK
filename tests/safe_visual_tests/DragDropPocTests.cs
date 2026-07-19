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
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// A-P0 — the drag-and-drop headless-feasibility POC (Wave 9 go/no-go gate), now expressed through the reusable
/// <see cref="DragDropTestSupport"/> helpers so each test names only the nodes to drag between. The question these
/// three tests answer: can an in-app tree drag-drop be <b>implemented</b> in Avalonia 12 <b>and verified in a headless
/// <c>safe_visual_tests</c> test</b>? The minimal gesture is the simplest later route — drag one product node onto
/// another locality and re-parent it via <see cref="ProjectWorkflow.MoveNodeAsync"/>.
/// <para>Verdict: PASS. Kept as A-30's first tests (backlog A-P0); grow them into the full node-kind dispatcher.</para>
/// </summary>
public class DragDropPocTests : AvaloniaTestBase
{
    /// <summary>Builds a shell with one wired product in "Living room" (locality A) and a second empty locality B,
    /// shows the real window, and expands every row so the drop targets are realized for hit-testing.</summary>
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, MainWindow window, ElementId productId, ElementId locA, ElementId locB)>
        SetupAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var locA = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locA, product.ProductIdentifier);
        var productId = harness.Session.Current!.FindById(locA)!.ChildrenOrEmpty().First(c => c.Tag.StartsWith("product_")).Id!.Value;
        var locB = (await harness.Session.AddLocalityAsync())!.Value;

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

    // Test 1 — the drop-target path is testable (the CORE go/no-go signal). Dragging the product onto locality B
    // re-parents it — asserted through BOTH the SDK document and the rebuilt view-model tree.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Poc1_Drop_ReparentsProduct_ViaDropTargetPath()
    {
        var (harness, vm, window, productId, locA, locB) = await SetupAsync();
        using var _ = harness;
        CurrentTestWindow = window;

        var product = TreeNodes.FindById(vm.InstallationNodes, productId)!;
        var localityB = TreeNodes.FindById(vm.InstallationNodes, locB)!;

        window.DragOnto(product, localityB);

        var treeLocB = TreeNodes.FindById(vm.InstallationNodes, locB)!;
        var treeLocA = TreeNodes.FindById(vm.InstallationNodes, locA)!;
        Assert.Multiple(() =>
        {
            // SDK truth — the resource id is unchanged and now parented under locality B.
            Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locB), "the SDK re-parented the product");
            Assert.That(harness.Session.Current!.FindById(productId), Is.Not.Null, "its id is preserved");
            // View-model tree — the product now renders under locality B, no longer under locality A.
            Assert.That(TreeNodes.FindById(treeLocB.Children, productId), Is.Not.Null, "the product node moved under locality B in the tree");
            Assert.That(TreeNodes.FindById(treeLocA.Children, productId), Is.Null, "it no longer renders under locality A");
        });
    }

    // Test 2 — the DragOver effect is observable, so the highlight half of A-30/A-31 is assertable: Move over a legal
    // locality, None over an illegal target (here the dragged node itself).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Poc2_DragOver_YieldsMoveOverLegalTarget_NoneOverSelf()
    {
        var (harness, vm, window, productId, locA, locB) = await SetupAsync();
        using var _ = harness;
        CurrentTestWindow = window;

        var product = TreeNodes.FindById(vm.InstallationNodes, productId)!;
        var localityB = TreeNodes.FindById(vm.InstallationNodes, locB)!;

        var overLegal = window.DragOverEffect(product, localityB);
        var overSelf = window.DragOverEffect(product, product);

        Assert.Multiple(() =>
        {
            Assert.That(overLegal, Is.EqualTo(DragDropEffects.Move), "DragOver a legal locality yields Move");
            Assert.That(overSelf, Is.EqualTo(DragDropEffects.None), "DragOver the dragged node itself yields None");
        });
    }

    // Test 3 — source-side reality check. Confirms §0.3's claim that the source DoDragDrop loop cannot be pumped
    // headlessly while the payload IS testable: (a) BuildDragData/TryGetElementId round-trip the id, and (b) driving
    // MouseDown→MouseMove past the threshold arms + initiates the source path (guarding the handledEventsToo wiring)
    // yet completes NO drop — there is no headless drag loop, so nothing moves.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Poc3_SourceSide_PayloadIsTestable_ButGestureCannotCompleteHeadlessly()
    {
        var (harness, vm, window, productId, locA, locB) = await SetupAsync();
        using var _ = harness;
        CurrentTestWindow = window;

        var product = TreeNodes.FindById(vm.InstallationNodes, productId)!;

        // (a) the factored payload helper is a plain, headless unit test.
        var built = TreeDragData.BuildDragData(product);
        Assert.That(built, Is.Not.Null, "a product node builds a drag payload");
        Assert.That(TreeDragData.TryGetElementId(built), Is.EqualTo(productId), "the payload carries the dragged element id");

        // (b) probe the source gesture through simulated pointer input.
        var start = window.RowPoint(product);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X + 40, start.Y + 40), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        TestContext.Out.WriteLine($"[A-P0] source-side probe — initiated={window.DragInitiatedForTest}, error={window.DragSourceError ?? "none"}");
        Assert.Multiple(() =>
        {
            Assert.That(window.DragInitiatedForTest, Is.True, "the source gesture arms and initiates under headless pointer input (guards the handledEventsToo wiring)");
            Assert.That(harness.Session.Current!.FindParent(productId)!.Id, Is.EqualTo(locA), "but it completes no drop headlessly, so nothing moved");
        });
    }
}
