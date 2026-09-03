using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Model;

namespace Ihc.Vis.Tests;

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
        // Named as well as identified: this takes whatever comes first in the catalog, which can be one of
        // D22's shared identifiers, and the factory refuses an ambiguous id rather than guessing (T046).
        await harness.Session.AddProductAsync(loc, products[0].ProductIdentifier, products[0].DisplayName);
        await harness.Session.AddProductAsync(loc, products[1].ProductIdentifier, products[1].DisplayName);
        var group = harness.Session.Current!.FindById(loc)!;
        var second = group.Children.Where(c => c.Tag.StartsWith("product_", StringComparison.Ordinal)).ElementAt(1).Id!.Value;

        await harness.Session.ReorderNodeAsync(second, -1);   // move the second product above the first

        var after = harness.Session.Current!.FindById(loc)!.Children
            .Where(c => c.Tag.StartsWith("product_", StringComparison.Ordinal)).ToList();
        // T017: the report-order observation goes through the NEW pipeline — the generated installation
        // report lists the component blocks in tree (document) order, so the moved product renders first.
        using var output = new System.IO.MemoryStream();
        await harness.ProjectService.GenerateReport(harness.Session.Current!, ReportKind.Installation,
            ReportMode.Standard, ReportMimeTypes.PlainText, output);
        string report = System.Text.Encoding.UTF8.GetString(output.ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(after[0].Id!.Value, Is.EqualTo(second), "the reordered product is now first in the tree");
            Assert.That(report.IndexOf(after[0].GetAttribute("name")!, StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0).And.LessThan(report.IndexOf(after[1].GetAttribute("name")!, StringComparison.Ordinal)),
                "the installation report lists the products in the reordered pane order (US-040)");
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
