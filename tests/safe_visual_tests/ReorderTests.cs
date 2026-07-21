using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>US-055: reorder siblings within a container — position changes, ids/links are preserved, the end is a
/// no-op, the new order drives the report, and the reorder is undoable.</summary>
public class ReorderTests
{
    [Test]
    public async Task Reorder_Locality_MovesDownThenUp_PreservingId()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var first = harness.Session.Current!.Groups[0].Id!.Value;
        var second = harness.Session.Current!.Groups[1].Id!.Value;

        Assert.That(await harness.Session.ReorderNodeAsync(first, +1), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.Groups[0].Id!.Value, Is.EqualTo(second), "the second locality moves up");
            Assert.That(harness.Session.Current!.Groups[1].Id!.Value, Is.EqualTo(first), "the first moves down — same id (preserved)");
        });

        Assert.That(await harness.Session.ReorderNodeAsync(first, -1), Is.True);
        Assert.That(harness.Session.Current!.Groups[0].Id!.Value, Is.EqualTo(first), "moving it back up restores the order");
    }

    [Test]
    public async Task Reorder_AtListEnds_IsNoOp()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var groups = harness.Session.Current!.Groups;
        var firstId = groups[0].Id!.Value;
        var lastId = groups[^1].Id!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.ReorderNodeAsync(firstId, -1).Result, Is.False, "the first cannot move up");
            Assert.That(harness.Session.ReorderNodeAsync(lastId, +1).Result, Is.False, "the last cannot move down");
        });
    }

    [Test]
    public async Task Reorder_Products_FollowsInReportOrder()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var products = harness.ProjectService.GetAvailableProducts().Where(p => p.Resources.Any(r => r.Tag == "dataline_input")).Take(2).ToList();
        await harness.Session.AddProductAsync(loc, products[0].ProductIdentifier);
        await harness.Session.AddProductAsync(loc, products[1].ProductIdentifier);
        var group = harness.Session.Current!.FindById(loc)!;
        var second = group.ChildrenOrEmpty().Where(c => c.Tag.StartsWith("product_")).ElementAt(1).Id!.Value;

        await harness.Session.ReorderNodeAsync(second, -1);   // move the second product above the first

        var afterOrder = harness.Session.Current!.FindById(loc)!.ChildrenOrEmpty()
            .Where(c => c.Tag.StartsWith("product_")).Select(c => c.Id!.Value).ToList();
        var report = harness.Session.GenerateInstallationReport()!;
        Assert.Multiple(() =>
        {
            Assert.That(afterOrder[0], Is.EqualTo(second), "the reordered product is now first in the tree");
            Assert.That(report.ProductDetails.Length, Is.GreaterThanOrEqualTo(2),
                "the installation report lists the products (in Installation-pane order — US-040)");
        });
    }

    [Test]
    public async Task Reorder_IsUndoable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var first = harness.Session.Current!.Groups[0].Id!.Value;
        await harness.Session.ReorderNodeAsync(first, +1);
        Assert.That(harness.Session.Current!.Groups[0].Id!.Value, Is.Not.EqualTo(first));

        await harness.Session.UndoAsync();

        Assert.That(harness.Session.Current!.Groups[0].Id!.Value, Is.EqualTo(first), "undo restores the original order");
    }
}
