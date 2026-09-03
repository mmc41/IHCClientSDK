using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The tree refresh's two paths, and the sweep that runs beside them.
///
/// An ordinary edit should RECONCILE the existing nodes in place; a rebuild tears every node instance down
/// and re-creates it. From outside, both merely look slow, so an edit that silently starts rebuilding is
/// invisible until someone notices the app has become sluggish on large projects. Naming the path is what
/// turns that into a graph with two lines instead of one.
/// </summary>
[TestFixture]
public class TreeUpdateTelemetryTests
{
    /// <summary>The tree-update kinds seen so far, oldest first.</summary>
    private static string[] TreeKinds(TelemetryCapture capture) => capture
        .SpansNamed("MainWindowViewModel.TreeUpdate")
        .Select(s => s.GetTagItem("ihc.tree.update")?.ToString() ?? "<none>")
        .ToArray();

    /// <summary>
    /// The gate's assertion, both halves in one flow: a load must rebuild (there is nothing to reconcile
    /// against yet), and every ordinary edit after it must reconcile.
    /// </summary>
    [Test]
    public async Task ALoadRebuilds_AndEveryOrdinaryEditAfterItReconciles()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "MainWindowViewModel.TreeUpdate", "CommandRegistry.OnContextChanged" },
            instruments: new[] { "ihc.ui.tree_update.duration", "ihc.ui.context_rebuild.duration" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();

        await vm.InitializeAsync();
        string[] afterLoad = TreeKinds(capture);

        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(locality);
        await harness.Session.AddEmptyFunctionBlockAsync(locality);
        await harness.Session.AddEmptyFunctionBlockAsync(locality);

        string[] all = TreeKinds(capture);
        string[] afterEdits = all.Skip(afterLoad.Length).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(afterLoad, Is.Not.Empty, "the load refreshes the tree");
            Assert.That(afterLoad, Does.Contain("rebuild"),
                "there is nothing to reconcile against on the first build");
            Assert.That(afterEdits, Is.Not.Empty, "each edit refreshes the tree");
            Assert.That(afterEdits.All(k => k == "reconcile"), Is.True,
                $"every ordinary edit must reconcile in place; saw [{string.Join(", ", afterEdits)}] - " +
                "a rebuild here is the performance cliff this attribute exists to expose");
        });
    }

    [Test]
    public async Task TheTreeUpdateIsTimed_WithItsPathAsTheDimension()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "MainWindowViewModel.TreeUpdate", "CommandRegistry.OnContextChanged" },
            instruments: new[] { "ihc.ui.tree_update.duration", "ihc.ui.context_rebuild.duration" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        IReadOnlyList<CapturedPoint> points = capture.PointsOf("ihc.ui.tree_update.duration");

        Assert.Multiple(() =>
        {
            Assert.That(points, Is.Not.Empty);
            Assert.That(points.All(p => p.Tags.ContainsKey("ihc.tree.update")), Is.True,
                "a duration without its path is one undifferentiated line - exactly what this replaces");
        });
    }

    [Test]
    public async Task TheContextSweepIsTimed()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "MainWindowViewModel.TreeUpdate", "CommandRegistry.OnContextChanged" },
            instruments: new[] { "ihc.ui.tree_update.duration", "ihc.ui.context_rebuild.duration" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(capture.Spans.Any(s => s.OperationName == "CommandRegistry.OnContextChanged"), Is.True);
            Assert.That(capture.Points.Any(p => p.Instrument == "ihc.ui.context_rebuild.duration"), Is.True,
                "the sweep runs on every selection change, so its cost is paid constantly");
        });
    }
}
